/*
FASE 0 — Backup completo de ObservatorioDB (ambiente ASIS / estandarización).
NO modifica datos. Solo genera archivo .bak.

Instancia esperada: localhost\SQLEXPRESS2025
Edición Express: sin COMPRESSION.

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\00_backup_observatoriodb.sql
*/
SET NOCOUNT ON;
GO

DECLARE @Fecha nvarchar(10) = CONVERT(nvarchar(10), GETDATE(), 23); /* yyyy-MM-dd */
DECLARE @BackupDir nvarchar(400) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS2025\MSSQL\Backup\';
DECLARE @BackupFile nvarchar(500) = @BackupDir + N'ObservatorioDB_' + @Fecha + N'.bak';
DECLARE @DbOriginal sysname = N'ObservatorioDB';

IF DB_ID(@DbOriginal) IS NULL
BEGIN
    RAISERROR(N'La base %s no existe en esta instancia.', 16, 1, @DbOriginal);
    RETURN;
END

PRINT N'=== BACKUP ObservatorioDB ===';
PRINT N'Fecha: ' + @Fecha;
PRINT N'Archivo: ' + @BackupFile;

DECLARE @sql nvarchar(max) = N'
BACKUP DATABASE [' + @DbOriginal + N']
TO DISK = @path
WITH INIT, CHECKSUM, STATS = 5;';

EXEC sp_executesql @sql, N'@path nvarchar(500)', @path = @BackupFile;

RESTORE VERIFYONLY FROM DISK = @BackupFile;

PRINT N'Backup y VERIFYONLY OK.';
GO
