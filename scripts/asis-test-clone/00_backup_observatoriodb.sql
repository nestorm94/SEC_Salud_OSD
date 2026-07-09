/*
================================================================================
 00_backup_observatoriodb.sql
================================================================================
 PROPÓSITO:
   Genera un respaldo completo (.bak) de ObservatorioDB antes del clon ASIS.
   Solo lectura sobre datos productivos; no modifica tablas ni esquema.

 BASE DE DATOS DESTINO:
   ObservatorioDB (instancia localhost\SQLEXPRESS2025; Express sin COMPRESSION).

 DEPENDENCIAS (ejecutar antes):
   Ninguna. Es el primer paso del pipeline FASE 0.

 ORDEN DE EJECUCIÓN:
   00 (este script) -> 01_restore -> 02_validacion -> 04_normalizacion...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\00_backup_observatoriodb.sql
================================================================================
*/
SET NOCOUNT ON;
GO

/* --- Variables de ruta y nombre de archivo de respaldo --- */
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

/* --- BACKUP: escribe .bak con INIT (sobrescribe) y CHECKSUM para integridad --- */
DECLARE @sql nvarchar(max) = N'
BACKUP DATABASE [' + @DbOriginal + N']
TO DISK = @path
WITH INIT, CHECKSUM, STATS = 5;';

EXEC sp_executesql @sql, N'@path nvarchar(500)', @path = @BackupFile;

/* --- Verificación de legibilidad del archivo sin restaurar --- */
RESTORE VERIFYONLY FROM DISK = @BackupFile;

PRINT N'Backup y VERIFYONLY OK.';
GO
