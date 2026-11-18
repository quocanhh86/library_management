using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace web_mvc_management.Data.Interceptors;

/// <summary>
/// Interceptor kết nối đồng bộ cục bộ trên Loopback để loại bỏ độ trễ/treo bất đồng bộ của SNI trên .NET 10.
/// </summary>
public sealed class FastLocalSqlConnectionInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        return InterceptionResult.Suppress();
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
        return ValueTask.FromResult(InterceptionResult.Suppress());
    }

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        if (connection.State != System.Data.ConnectionState.Closed)
        {
            connection.Close();
        }
        return ValueTask.FromResult(InterceptionResult.Suppress());
    }
}

/// <summary>
/// Bọc DbDataReader đồng bộ để mọi thao tác ReadAsync / IsDBNullAsync / GetFieldValueAsync / DisposeAsync
/// của EF Core đều chạy trên luồng đồng bộ nhất quán với command.ExecuteReader(), không trộn lẫn Sync/Async trên TDS stream.
/// </summary>
public sealed class SyncDbDataReader(DbDataReader inner) : DbDataReader
{
    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override int VisibleFieldCount => inner.VisibleFieldCount;

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override System.Collections.IEnumerator GetEnumerator() => inner.GetEnumerator();
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool NextResult() => inner.NextResult();
    public override bool Read() => inner.Read();
    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(inner.Read());
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(inner.NextResult());
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Task.FromResult(inner.IsDBNull(ordinal));
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) => Task.FromResult(inner.GetFieldValue<T>(ordinal));

    public override void Close() => inner.Close();
    public override Task CloseAsync()
    {
        inner.Close();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        inner.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Interceptor thực thi DbCommand qua đường dẫn đồng bộ trên kết nối Loopback cục bộ
/// nhằm triệt tiêu hoàn toàn lỗi Win32Exception (258) wait timeout của native SNI.dll
/// trong EndExecuteNonQueryAsync / EndExecuteReaderAsync trên .NET 10.0.
/// </summary>
public sealed class FastLocalSqlCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        var reader = new SyncDbDataReader(command.ExecuteReader());
        return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var reader = new SyncDbDataReader(command.ExecuteReader());
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(reader));
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var rows = command.ExecuteNonQuery();
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(rows));
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        var scalar = command.ExecuteScalar();
        return ValueTask.FromResult(InterceptionResult<object>.SuppressWithResult(scalar!));
    }
}
