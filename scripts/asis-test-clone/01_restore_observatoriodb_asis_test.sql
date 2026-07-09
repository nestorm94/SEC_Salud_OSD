/*
================================================================================
 01_restore_observatoriodb_asis_test.sql
================================================================================
 PROPÓSITO:
   Restaura el .bak de ObservatorioDB como ObservatorioDB_ASIS_Test usando MOVE
   a archivos MDF/LDF distintos. No altera la base productiva original.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (nueva base en la misma instancia).

 DEPENDENCIAS (ejecutar antes):
   - 00_backup_observatoriodb.sql (genera ObservatorioDB_YYYY-MM-DD.bak)

 ORDEN DE EJECUCIÓN:
   00 -> 01 (este script) -> 02_validacion -> 04_normalizacion...

 NOTA:
   Para reemplazar un clon existente, confirmar y poner @PermitirReemplazo = 1.

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\01_restore_observatoriodb_asis_test.sql
================================================================================
*/
SET NOCOUNT ON;
GO

DECLARE @Fecha nvarchar(10) = CONVERT(nvarchar(10), GETDATE(), 23);
DECLARE @BackupDir nvarchar(400) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS2025\MSSQL\Backup\';
DECLARE @BackupFile nvarchar(500) = @BackupDir + N'ObservatorioDB_' + @Fecha + N'.bak';

DECLARE @DbOriginal sysname = N'ObservatorioDB';
DECLARE @DbTest sysname = N'ObservatorioDB_ASIS_Test';
DECLARE @PermitirReemplazo bit = 0; /* 0 = abortar si ya existe; 1 = DROP y restaurar de nuevo */

DECLARE @DataDir nvarchar(400) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS2025\MSSQL\DATA\';
DECLARE @Mdf nvarchar(500) = @DataDir + @DbTest + N'.mdf';
DECLARE @Ldf nvarchar(500) = @DataDir + @DbTest + N'_log.ldf';
DECLARE @drop nvarchar(max);
DECLARE @restore nvarchar(max);

/* --- Comprobar que el .bak del día existe antes de restaurar --- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.dm_os_file_exists(@BackupFile)
    WHERE file_exists = 1
)
BEGIN
    /* Fallback si dm_os_file_exists no está disponible */
    IF OBJECT_ID(N'tempdb..#fe') IS NOT NULL DROP TABLE #fe;
    CREATE TABLE #fe (ExistsFlag int, IsDirectory int, ParentExists int);
    INSERT #fe EXEC master.dbo.xp_fileexist @BackupFile;
    IF NOT EXISTS (SELECT 1 FROM #fe WHERE ExistsFlag = 1)
    BEGIN
        RAISERROR(N'No se encontró el backup: %s. Ejecute primero 00_backup_observatoriodb.sql', 16, 1, @BackupFile);
        RETURN;
    END
END

IF DB_ID(@DbTest) IS NOT NULL
BEGIN
    IF @PermitirReemplazo = 0
    BEGIN
        RAISERROR(
            N'La base %s ya existe. No se reemplaza ( @PermitirReemplazo = 0 ). Confirme y vuelva a ejecutar con @PermitirReemplazo = 1.',
            16, 1, @DbTest);
        RETURN;
    END

    PRINT N'Eliminando base de prueba existente: ' + @DbTest;
    SET @drop = N'ALTER DATABASE ' + QUOTENAME(@DbTest) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
              + N'DROP DATABASE ' + QUOTENAME(@DbTest) + N';';
    EXEC sp_executesql @drop;
END

PRINT N'=== RESTORE -> ' + @DbTest + N' ===';
PRINT N'Origen backup: ' + @BackupFile;
PRINT N'MDF: ' + @Mdf;
PRINT N'LDF: ' + @Ldf;

/* --- RESTORE con MOVE: archivos físicos separados del original --- */
SET @restore = N'RESTORE DATABASE ' + QUOTENAME(@DbTest)
    + N' FROM DISK = ' + QUOTENAME(@BackupFile, CHAR(39))
    + N' WITH MOVE N''ObservatorioDB'' TO ' + QUOTENAME(@Mdf, CHAR(39))
    + N', MOVE N''ObservatorioDB_log'' TO ' + QUOTENAME(@Ldf, CHAR(39))
    + N', RECOVERY, STATS = 5;';
EXEC sp_executesql @restore;

PRINT N'Restore completado: ' + @DbTest;
GO
