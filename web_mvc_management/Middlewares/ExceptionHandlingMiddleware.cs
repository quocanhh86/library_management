using System.Net;

namespace web_mvc_management.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
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

