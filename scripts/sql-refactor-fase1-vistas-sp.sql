/*
FASE 1 - Migracion hacia SQL Server centrado en vistas/procedimientos.
No elimina objetos existentes; crea piezas nuevas para adopcion incremental.
*/
SET NOCOUNT ON;
GO

/* =========================
   VISTAS: CARGAS / HISTORIAL
   ========================= */
CREATE OR ALTER VIEW dbo.vw_Cargas_Listado
AS
SELECT
    c.Id,
    c.DependenciaId,
    d.Nombre AS DependenciaNombre,
    c.Estado,
    c.FechaInicio,
    c.FechaFin,
    a.NombreOriginal AS NombreArchivo,
    u.NombreUsuario,
    (
        SELECT COUNT_BIG(1)
        FROM dbo.ErroresValidacion e
        WHERE e.CargaArchivoId = c.Id
    ) AS TotalErrores
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId;
GO

CREATE OR ALTER VIEW dbo.vw_Carga_Historial
AS
SELECT
    h.Id,
    h.CargaArchivoId,
    h.UsuarioId,
    u.NombreUsuario,
    h.Accion,
    h.Detalle,
    h.Fecha,
    c.DependenciaId
FROM dbo.HistorialCarga h
INNER JOIN dbo.CargasArchivo c ON c.Id = h.CargaArchivoId
LEFT JOIN dbo.Usuarios u ON u.Id = h.UsuarioId;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Indicador_Prostata_Listar
    @CodigoDane nvarchar(20) = NULL,
    @Territorio nvarchar(250) = NULL,
    @Regional nvarchar(200) = NULL,
    @Anio int = NULL,
    @Area nvarchar(100) = NULL,
    @MaxRows int = 20000
AS
BEGIN
    SET NOCOUNT ON;
    SET @MaxRows = ISNULL(@MaxRows, 20000);
    IF @MaxRows < 1 SET @MaxRows = 1;
    IF @MaxRows > 200000 SET @MaxRows = 200000;

    DECLARE @obj int = OBJECT_ID(N'dbo.vw_Tasa_Mortalidad_Prostata_Validada', N'V');
    IF @obj IS NULL
    BEGIN
        SELECT TOP (0)
            CAST(NULL AS nvarchar(20)) AS CodigoDane,
            CAST(NULL AS nvarchar(250)) AS Territorio,
            CAST(NULL AS nvarchar(250)) AS CodigoTerritorio,
            CAST(NULL AS nvarchar(200)) AS Regional,
            CAST(NULL AS int) AS Anio,
            CAST(NULL AS nvarchar(100)) AS Area,
            CAST(NULL AS decimal(18,4)) AS Muertes,
            CAST(NULL AS decimal(18,4)) AS Poblacion,
            CAST(NULL AS decimal(18,6)) AS Coeficiente,
            CAST(NULL AS decimal(18,6)) AS Tasa;
        RETURN;
    END;

    DECLARE @c1 sysname, @c2 sysname, @c3 sysname, @c4 sysname, @c5 sysname,
            @c6 sysname, @c7 sysname, @c8 sysname, @c9 sysname, @c10 sysname;

    SELECT
        @c1 = MAX(CASE WHEN column_id = 1 THEN name END),
        @c2 = MAX(CASE WHEN column_id = 2 THEN name END),
        @c3 = MAX(CASE WHEN column_id = 3 THEN name END),
        @c4 = MAX(CASE WHEN column_id = 4 THEN name END),
        @c5 = MAX(CASE WHEN column_id = 5 THEN name END),
        @c6 = MAX(CASE WHEN column_id = 6 THEN name END),
        @c7 = MAX(CASE WHEN column_id = 7 THEN name END),
        @c8 = MAX(CASE WHEN column_id = 8 THEN name END),
        @c9 = MAX(CASE WHEN column_id = 9 THEN name END),
        @c10 = MAX(CASE WHEN column_id = 10 THEN name END)
    FROM sys.columns
    WHERE object_id = @obj;

    DECLARE @sql nvarchar(max) = N'
SELECT TOP (@MaxRows)
    CAST(v.' + QUOTENAME(@c1) + N' AS nvarchar(20)) AS CodigoDane,
    CAST(v.' + QUOTENAME(@c2) + N' AS nvarchar(250)) AS Territorio,
    CAST(v.' + QUOTENAME(@c3) + N' AS nvarchar(250)) AS CodigoTerritorio,
    CAST(v.' + QUOTENAME(@c4) + N' AS nvarchar(200)) AS Regional,
    TRY_CONVERT(int, v.' + QUOTENAME(@c5) + N') AS Anio,
    CAST(v.' + QUOTENAME(@c6) + N' AS nvarchar(100)) AS Area,
    COALESCE(
        TRY_CONVERT(decimal(18,4), v.' + QUOTENAME(@c7) + N'),
        TRY_CONVERT(decimal(18,4), REPLACE(CAST(v.' + QUOTENAME(@c7) + N' AS nvarchar(100)), N'','', N''.''))
    ) AS Muertes,
    COALESCE(
        TRY_CONVERT(decimal(18,4), v.' + QUOTENAME(@c8) + N'),
        TRY_CONVERT(decimal(18,4), REPLACE(CAST(v.' + QUOTENAME(@c8) + N' AS nvarchar(100)), N'','', N''.''))
    ) AS Poblacion,
    COALESCE(
        TRY_CONVERT(decimal(18,6), v.' + QUOTENAME(@c9) + N'),
        TRY_CONVERT(decimal(18,6), REPLACE(CAST(v.' + QUOTENAME(@c9) + N' AS nvarchar(100)), N'','', N''.''))
    ) AS Coeficiente,
    COALESCE(
        TRY_CONVERT(decimal(18,6), v.' + QUOTENAME(@c10) + N'),
        TRY_CONVERT(decimal(18,6), REPLACE(CAST(v.' + QUOTENAME(@c10) + N' AS nvarchar(100)), N'','', N''.''))
    ) AS Tasa
FROM dbo.vw_Tasa_Mortalidad_Prostata_Validada v
WHERE (@CodigoDane IS NULL OR LTRIM(RTRIM(@CodigoDane)) = N'''' OR CAST(v.' + QUOTENAME(@c1) + N' AS nvarchar(20)) = @CodigoDane)
  AND (@Territorio IS NULL OR LTRIM(RTRIM(@Territorio)) = N'''' OR CAST(v.' + QUOTENAME(@c2) + N' AS nvarchar(250)) = @Territorio)
  AND (@Regional IS NULL OR LTRIM(RTRIM(@Regional)) = N'''' OR CAST(v.' + QUOTENAME(@c4) + N' AS nvarchar(200)) = @Regional)
  AND (@Anio IS NULL OR TRY_CONVERT(int, v.' + QUOTENAME(@c5) + N') = @Anio)
  AND (@Area IS NULL OR LTRIM(RTRIM(@Area)) = N'''' OR CAST(v.' + QUOTENAME(@c6) + N' AS nvarchar(100)) = @Area)
ORDER BY TRY_CONVERT(int, v.' + QUOTENAME(@c5) + N'), CAST(v.' + QUOTENAME(@c2) + N' AS nvarchar(250));';

    EXEC sp_executesql
        @sql,
        N'@CodigoDane nvarchar(20), @Territorio nvarchar(250), @Regional nvarchar(200), @Anio int, @Area nvarchar(100), @MaxRows int',
        @CodigoDane = @CodigoDane,
        @Territorio = @Territorio,
        @Regional = @Regional,
        @Anio = @Anio,
        @Area = @Area,
        @MaxRows = @MaxRows;
END
GO

/* =========================
   SP: CARGAS / HISTORIAL
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Carga_Listar
    @DependenciaId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        DependenciaId,
        DependenciaNombre,
        Estado,
        FechaInicio,
        FechaFin,
        NombreArchivo,
        NombreUsuario,
        CONVERT(int, TotalErrores) AS TotalErrores
    FROM dbo.vw_Cargas_Listado
    WHERE (@DependenciaId IS NULL OR DependenciaId = @DependenciaId)
    ORDER BY COALESCE(FechaFin, FechaInicio) DESC, Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_ListarPorUsuario
    @UsuarioId int,
    @DependenciaId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        l.Id,
        l.DependenciaId,
        l.DependenciaNombre,
        l.Estado,
        l.FechaInicio,
        l.FechaFin,
        l.NombreArchivo,
        l.NombreUsuario,
        CONVERT(int, l.TotalErrores) AS TotalErrores
    FROM dbo.vw_Cargas_Listado l
    INNER JOIN dbo.CargasArchivo c ON c.Id = l.Id
    WHERE c.UsuarioId = @UsuarioId
      AND (@DependenciaId IS NULL OR l.DependenciaId = @DependenciaId)
    ORDER BY COALESCE(l.FechaFin, l.FechaInicio) DESC, l.Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_Historial_Listar
    @CargaId int = NULL,
    @DependenciaId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        CargaArchivoId,
        UsuarioId,
        NombreUsuario,
        Accion,
        Detalle,
        Fecha
    FROM dbo.vw_Carga_Historial
    WHERE (@CargaId IS NULL OR CargaArchivoId = @CargaId)
      AND (@DependenciaId IS NULL OR DependenciaId = @DependenciaId)
    ORDER BY Fecha DESC;
END
GO
