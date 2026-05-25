-- Ejecutar en SSMS conectado a localhost\SQLEXPRESS2025 como administrador.
-- Otorga acceso al App Pool de IIS al sitio ObservatorioOSD.

USE [master];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'IIS APPPOOL\ObservatorioOSDPool')
    CREATE LOGIN [IIS APPPOOL\ObservatorioOSDPool] FROM WINDOWS;
GO

USE [ObservatorioDB];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'IIS APPPOOL\ObservatorioOSDPool')
    CREATE USER [IIS APPPOOL\ObservatorioOSDPool] FOR LOGIN [IIS APPPOOL\ObservatorioOSDPool];
GO
ALTER ROLE [db_owner] ADD MEMBER [IIS APPPOOL\ObservatorioOSDPool];
GO
