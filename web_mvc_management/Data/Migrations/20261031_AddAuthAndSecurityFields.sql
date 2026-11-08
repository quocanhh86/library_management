-- 1. Bảng staff: role, failed_login_attempts, last_login_attempt, locked_until, security_stamp
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('staff') AND name = 'role')
BEGIN
    ALTER TABLE staff ADD role VARCHAR(20) NOT NULL CONSTRAINT df_staff_role DEFAULT 'Librarian';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('staff') AND name = 'failed_login_attempts')
BEGIN
    ALTER TABLE staff ADD failed_login_attempts INT NOT NULL CONSTRAINT df_staff_failed_login DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('staff') AND name = 'last_login_attempt')
BEGIN
    ALTER TABLE staff ADD last_login_attempt DATETIMEOFFSET NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('staff') AND name = 'locked_until')
BEGIN
    ALTER TABLE staff ADD locked_until DATETIMEOFFSET NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('staff') AND name = 'security_stamp')
BEGIN
    ALTER TABLE staff ADD security_stamp VARCHAR(36) NOT NULL CONSTRAINT df_staff_security_stamp DEFAULT NEWID();
END

EXEC('UPDATE staff SET role = ''Admin'' WHERE username = ''admin''');

-- 2. Bảng readers: failed_login_attempts, last_login_attempt, locked_until, security_stamp
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('readers') AND name = 'failed_login_attempts')
BEGIN
    ALTER TABLE readers ADD failed_login_attempts INT NOT NULL CONSTRAINT df_readers_failed_login DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('readers') AND name = 'last_login_attempt')
BEGIN
    ALTER TABLE readers ADD last_login_attempt DATETIMEOFFSET NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('readers') AND name = 'locked_until')
BEGIN
    ALTER TABLE readers ADD locked_until DATETIMEOFFSET NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('readers') AND name = 'security_stamp')
BEGIN
    ALTER TABLE readers ADD security_stamp VARCHAR(36) NOT NULL CONSTRAINT df_readers_security_stamp DEFAULT NEWID();
END

-- 3. Tạo bảng otps bền vững (SEC-08, SEC-12)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'otps')
BEGIN
    CREATE TABLE otps (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        email VARCHAR(255) NOT NULL,
        otp_code VARCHAR(10) NOT NULL,
        purpose VARCHAR(30) NOT NULL,
        failed_attempts INT NOT NULL DEFAULT 0,
        is_used BIT NOT NULL DEFAULT 0,
        expires_at DATETIMEOFFSET NOT NULL,
        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );

    CREATE INDEX idx_otps_lookup ON otps (email, purpose, is_used, expires_at);
END

-- 4. Nâng cấp toàn bộ ràng buộc CHECK trên cột [status] để hỗ trợ trạng thái xóa mềm 'DELETED'
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE COL_NAME(parent_object_id, parent_column_id) = 'status'
      AND definition NOT LIKE '%DELETED%'
)
BEGIN
    DECLARE @statusConstraintSql NVARCHAR(MAX) = N'';
    SELECT @statusConstraintSql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
        + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + CHAR(13)
        + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
        + N' ADD CONSTRAINT ' + QUOTENAME(name) + N' CHECK ([status] IN (''ACTIVE'', ''INACTIVE'', ''DELETED''));' + CHAR(13)
    FROM sys.check_constraints
    WHERE COL_NAME(parent_object_id, parent_column_id) = 'status'
      AND definition NOT LIKE '%DELETED%';

    IF LEN(@statusConstraintSql) > 0
        EXEC sp_executesql @statusConstraintSql;
END
