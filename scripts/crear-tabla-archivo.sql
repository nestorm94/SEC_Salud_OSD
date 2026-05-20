-- Ejecutar en SSMS conectado al MISMO servidor donde quieres la base ObservatorioDB.
-- Si la app usa LocalDB, conéctate a (localdb)\MSSQLLocalDB antes de ejecutar esto.

USE ObservatorioDB;
GO

IF OBJECT_ID(N'dbo.Archivo', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Archivos', N'Archivo', N'OBJECT';
    ELSE
    BEGIN
        CREATE TABLE dbo.Archivo (
            Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            NombreOriginal   NVARCHAR(260) NOT NULL,
            NombreAlmacenado NVARCHAR(260) NOT NULL,
            RutaRelativa     NVARCHAR(400) NOT NULL,
            TipoMime         NVARCHAR(200) NULL,
            TamanoBytes      BIGINT NULL,
            CreadoEn         DATETIME2(0) NOT NULL
                CONSTRAINT DF_Archivo_CreadoEn DEFAULT (SYSUTCDATETIME())
        );
    END
END
GO
