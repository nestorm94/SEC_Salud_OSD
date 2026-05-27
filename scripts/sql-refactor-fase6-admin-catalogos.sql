/*
FASE 6 - Administración (líneas, indicadores, plantillas, áreas),
ArchivoCarga, auditoría y catálogo de validación Excel.
*/
SET NOCOUNT ON;
GO

/* ========================= VISTAS ========================= */
CREATE OR ALTER VIEW dbo.vw_LineaTematica_Listado
AS
SELECT Id, Codigo, Nombre, Descripcion, Activo, CreadoEn
FROM dbo.LineaTematica;
GO

CREATE OR ALTER VIEW dbo.vw_Indicador_Listado
AS
SELECT
    i.Id, i.LineaTematicaId, lt.Nombre AS LineaTematicaNombre,
    i.Codigo, i.Nombre, i.Descripcion, i.Activo, i.CreadoEn, i.ColumnasObligatoriasJson
FROM dbo.Indicador i
INNER JOIN dbo.LineaTematica lt ON lt.Id = i.LineaTematicaId;
GO

CREATE OR ALTER VIEW dbo.vw_Plantilla_Listado
AS
SELECT
    p.Id, p.Codigo, p.Nombre, p.Descripcion, p.DependenciaId, d.Nombre AS DependenciaNombre,
    p.Activo, p.CreadoEn,
    (SELECT COUNT(1) FROM dbo.CamposPlantilla c WHERE c.PlantillaId = p.Id) AS TotalCampos
FROM dbo.PlantillasCarga p
LEFT JOIN dbo.Dependencias d ON d.Id = p.DependenciaId;
GO

CREATE OR ALTER VIEW dbo.vw_AreaTematica_Listado
AS
SELECT a.Id, a.DependenciaId, d.Nombre AS DependenciaNombre, a.Codigo, a.Nombre, a.Activo
FROM dbo.AreaTematica a
INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId;
GO

/* ========================= LÍNEA TEMÁTICA ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_LineaTematica_Listar
    @SoloActivas bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Codigo, Nombre, Descripcion, Activo, CreadoEn
    FROM dbo.vw_LineaTematica_Listado
    WHERE (@SoloActivas = 0 OR Activo = 1)
    ORDER BY Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LineaTematica_Obtener
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Codigo, Nombre, Descripcion, Activo, CreadoEn
    FROM dbo.vw_LineaTematica_Listado
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LineaTematica_Crear
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.LineaTematica (Codigo, Nombre, Descripcion)
    OUTPUT INSERTED.Id
    VALUES (LTRIM(RTRIM(@Codigo)), LTRIM(RTRIM(@Nombre)), @Descripcion);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LineaTematica_Actualizar
    @Id int,
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL,
    @Activo bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LineaTematica
    SET Codigo = LTRIM(RTRIM(@Codigo)),
        Nombre = LTRIM(RTRIM(@Nombre)),
        Descripcion = @Descripcion,
        Activo = @Activo
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_LineaTematica_ContarIndicadores
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1) FROM dbo.Indicador WHERE LineaTematicaId = @Id;
END;
GO

/* ========================= INDICADOR (app) ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Indicador_Listar
    @LineaTematicaId int = NULL,
    @SoloActivas bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.Id, i.LineaTematicaId, i.LineaTematicaNombre, i.Codigo, i.Nombre, i.Descripcion, i.Activo, i.CreadoEn
    FROM dbo.vw_Indicador_Listado i
    INNER JOIN dbo.LineaTematica lt ON lt.Id = i.LineaTematicaId
    WHERE (@LineaTematicaId IS NULL OR i.LineaTematicaId = @LineaTematicaId)
      AND (@SoloActivas = 0 OR (i.Activo = 1 AND lt.Activo = 1))
    ORDER BY i.LineaTematicaNombre, i.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_Obtener
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, LineaTematicaId, LineaTematicaNombre, Codigo, Nombre, Descripcion, Activo, CreadoEn
    FROM dbo.vw_Indicador_Listado
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_ObtenerColumnasObligatoriasJson
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ColumnasObligatoriasJson FROM dbo.Indicador WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_PerteneceALinea
    @IndicadorId int,
    @LineaTematicaId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM dbo.Indicador
        WHERE Id = @IndicadorId AND LineaTematicaId = @LineaTematicaId AND Activo = 1
    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS Existe;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_Crear
    @LineaTematicaId int,
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Indicador (LineaTematicaId, Codigo, Nombre, Descripcion)
    OUTPUT INSERTED.Id
    VALUES (@LineaTematicaId, LTRIM(RTRIM(@Codigo)), LTRIM(RTRIM(@Nombre)), @Descripcion);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_Actualizar
    @Id int,
    @LineaTematicaId int,
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL,
    @Activo bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Indicador
    SET LineaTematicaId = @LineaTematicaId,
        Codigo = LTRIM(RTRIM(@Codigo)),
        Nombre = LTRIM(RTRIM(@Nombre)),
        Descripcion = @Descripcion,
        Activo = @Activo
    WHERE Id = @Id;
END;
GO

/* ========================= PLANTILLAS ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Codigo, Nombre, Descripcion, DependenciaId, DependenciaNombre, Activo, CreadoEn, TotalCampos
    FROM dbo.vw_Plantilla_Listado
    ORDER BY Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Crear
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL,
    @DependenciaId int = NULL,
    @Activo bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.PlantillasCarga (Codigo, Nombre, Descripcion, DependenciaId, Activo)
    OUTPUT INSERTED.Id
    VALUES (UPPER(LTRIM(RTRIM(@Codigo))), LTRIM(RTRIM(@Nombre)), @Descripcion, @DependenciaId, @Activo);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Actualizar
    @Id int,
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL,
    @DependenciaId int = NULL,
    @Activo bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PlantillasCarga
    SET Codigo = UPPER(LTRIM(RTRIM(@Codigo))),
        Nombre = LTRIM(RTRIM(@Nombre)),
        Descripcion = @Descripcion,
        DependenciaId = @DependenciaId,
        Activo = @Activo
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Campos_Listar
    @PlantillaId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PlantillaId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden
    FROM dbo.CamposPlantilla
    WHERE PlantillaId = @PlantillaId
    ORDER BY Orden, Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Campo_Crear
    @PlantillaId int,
    @NombreCampo nvarchar(200),
    @TipoDato nvarchar(50),
    @Obligatorio bit,
    @Descripcion nvarchar(500) = NULL,
    @Longitud int = NULL,
    @Formato nvarchar(200) = NULL,
    @ValoresPermitidos nvarchar(4000) = NULL,
    @Orden int
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CamposPlantilla
        (PlantillaId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden)
    OUTPUT INSERTED.Id
    VALUES (@PlantillaId, @NombreCampo, @TipoDato, @Obligatorio, @Descripcion, @Longitud, @Formato, @ValoresPermitidos, @Orden);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Plantilla_Campo_Eliminar
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.CamposPlantilla WHERE Id = @Id;
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO

/* ========================= ÁREA TEMÁTICA ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_AreaTematica_Listar
    @DependenciaId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, DependenciaId, DependenciaNombre, Codigo, Nombre, Activo
    FROM dbo.vw_AreaTematica_Listado
    WHERE (@DependenciaId IS NULL OR DependenciaId = @DependenciaId)
    ORDER BY DependenciaNombre, Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AreaTematica_Crear
    @DependenciaId int,
    @Codigo nvarchar(50),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AreaTematica (DependenciaId, Codigo, Nombre, Descripcion)
    OUTPUT INSERTED.Id
    VALUES (@DependenciaId, UPPER(LTRIM(RTRIM(@Codigo))), LTRIM(RTRIM(@Nombre)), @Descripcion);
END;
GO

/* ========================= ARCHIVO CARGA (vínculo) ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_ArchivoCarga_Sincronizar
    @ArchivoId int,
    @UsuarioId int,
    @DependenciaId int,
    @AreaTematicaId int,
    @PlantillaCargaId int = NULL,
    @Estado nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.ArchivoCarga WHERE ArchivoId = @ArchivoId)
    BEGIN
        UPDATE dbo.ArchivoCarga
        SET Estado = @Estado,
            AreaTematicaId = @AreaTematicaId,
            PlantillaCargaId = @PlantillaCargaId
        WHERE ArchivoId = @ArchivoId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.ArchivoCarga
            (ArchivoId, UsuarioId, DependenciaId, AreaTematicaId, PlantillaCargaId, Estado)
        VALUES
            (@ArchivoId, @UsuarioId, @DependenciaId, @AreaTematicaId, @PlantillaCargaId, @Estado);
    END
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ArchivoCarga_ActualizarEstadoPorCarga
    @CargaArchivoId int,
    @Estado nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ac
    SET ac.Estado = @Estado,
        ac.FechaFin = CASE
            WHEN @Estado IN (N'APROBADO', N'RECHAZADO', N'VALIDADO_EXITOSO', N'VALIDADO_CON_ERRORES', N'CARGADO_BD')
            THEN SYSUTCDATETIME()
            ELSE ac.FechaFin
        END
    FROM dbo.ArchivoCarga ac
    INNER JOIN dbo.CargasArchivo c ON c.ArchivoId = ac.ArchivoId
    WHERE c.Id = @CargaArchivoId;
END;
GO

/* ========================= AUDITORÍA ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Auditoria_Registrar
    @UsuarioId int = NULL,
    @Accion nvarchar(200),
    @Entidad nvarchar(100) = NULL,
    @EntidadId nvarchar(100) = NULL,
    @Detalle nvarchar(max) = NULL,
    @IpOrigen nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditoriaSistema (UsuarioId, Accion, Entidad, EntidadId, Detalle, IpOrigen)
    VALUES (@UsuarioId, @Accion, @Entidad, @EntidadId, @Detalle, @IpOrigen);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auditoria_Listar
    @Top int = 200
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top < 1 SET @Top = 1;
    IF @Top > 5000 SET @Top = 5000;

    SELECT TOP (@Top)
        a.Id, a.Fecha, u.NombreUsuario, a.Accion, a.Entidad, a.EntidadId, a.Detalle
    FROM dbo.AuditoriaSistema a
    LEFT JOIN dbo.Usuarios u ON u.Id = a.UsuarioId
    ORDER BY a.Fecha DESC;
END;
GO
