-- USO PUNTUAL (pruebas): borra todos los cargues, historiales y archivos.
-- NO es parte del sistema; ejecutar manualmente cuando necesite empezar de cero.
-- NO borra: usuarios, roles, dependencias, líneas temáticas, indicadores, plantillas, catálogos DIVIPOLA.
--
-- IIS / Production usa SQL Express (appsettings.Production.json):
--   sqlcmd -S "localhost\SQLEXPRESS2025" -d ObservatorioDB -i scripts\limpiar-cargues-y-archivos.sql
-- Desarrollo con LocalDB (appsettings.json):
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d ObservatorioDB -i scripts\limpiar-cargues-y-archivos.sql
-- Luego vacíe uploads: C:\Hosting\ObservatorioOSD\uploads

SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @cargas INT = (SELECT COUNT(1) FROM dbo.CargasArchivo);
DECLARE @archivos INT = (SELECT COUNT(1) FROM dbo.Archivos);

DELETE FROM dbo.CargasArchivo;

IF OBJECT_ID(N'dbo.ArchivoCarga', N'U') IS NOT NULL
    DELETE FROM dbo.ArchivoCarga;

DELETE FROM dbo.Archivos;

IF OBJECT_ID(N'dbo.Archivo', N'U') IS NOT NULL
    DELETE FROM dbo.Archivo;

IF OBJECT_ID(N'dbo.AuditoriaSistema', N'U') IS NOT NULL
    DELETE FROM dbo.AuditoriaSistema
    WHERE Entidad IN (N'Archivos', N'CargasArchivo', N'ArchivoCarga');

COMMIT TRANSACTION;

PRINT CONCAT(N'Eliminados: ', @cargas, N' cargue(s), ', @archivos, N' archivo(s) en BD.');
PRINT N'Borre también los archivos en la carpeta uploads del servidor IIS.';
