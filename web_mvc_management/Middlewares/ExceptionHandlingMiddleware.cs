using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Data.SqlClient;

namespace web_mvc_management.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsClientCancelledException(ex, context))
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context);
        }
    }

    public static bool IsClientCancelledException(Exception ex, HttpContext? context = null)
    {
        if (context?.RequestAborted.IsCancellationRequested == true)
        {
            return true;
        }

        if (ex is OperationCanceledException or TaskCanceledException)
        {
            return true;
        }

        if (ex is SqlException sqlEx &&
            sqlEx.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ex.InnerException != null)
        {
            return IsClientCancelledException(ex.InnerException, context);
        }

        return false;
    }

    private static Task HandleExceptionAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        // Nếu là request AJAX/API → trả JSON
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new
            {
                error = "Đã xảy ra lỗi hệ thống.",
                traceId = context.TraceIdentifier
            });
        }

        // Nếu là request thường → redirect đến trang lỗi
        context.Response.Redirect("/Home/Error");
        return Task.CompletedTask;
    }
}


public sealed class SafeCancellationTokenModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.ModelType == typeof(CancellationToken)
            ? SafeCancellationTokenModelBinder.Instance
            : null;
    }

    private sealed class SafeCancellationTokenModelBinder : IModelBinder
    {
        public static readonly SafeCancellationTokenModelBinder Instance = new();

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var token = Debugger.IsAttached
                ? CancellationToken.None
                : bindingContext.HttpContext.RequestAborted;

            var model = (object)token;
            bindingContext.ValidationState.Add(model, new ValidationStateEntry { SuppressValidation = true });
            bindingContext.Result = ModelBindingResult.Success(model);
            return Task.CompletedTask;
        }
    }
}




