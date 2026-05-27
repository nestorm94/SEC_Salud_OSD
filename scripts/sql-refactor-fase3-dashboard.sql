/*
FASE 3 - Dashboard
Objetivo: centralizar KPIs y últimos cargues desde SQL Server (vw_/usp_).
No rompe lógica existente: si el usp_ no existe, el backend usa fallback legacy.
*/
SET NOCOUNT ON;
GO

/* =========================
   VISTA: Últimos cargues base
   ========================= */
CREATE OR ALTER VIEW dbo.vw_Dashboard_UltimosCargues
AS
SELECT
    c.Id,
    c.DependenciaId,
    d.Nombre AS Dependencia,
    c.Estado,
    a.NombreOriginal AS Archivo,
    COALESCE(c.FechaFin, c.FechaInicio) AS Fecha,
    c.UsuarioId,
    u.NombreUsuario AS Usuario
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId
LEFT JOIN dbo.Usuarios u ON u.Id = c.UsuarioId;
GO

/* =========================
   SP: Resumen dashboard
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Resumen
    @DependenciaId int = NULL,
    @SubidoPorUsuarioId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* 1) KPIs (1er result set) */
    SELECT
        (SELECT COUNT(1)
         FROM dbo.Archivos a
         WHERE (@DependenciaId IS NULL OR a.DependenciaId = @DependenciaId)
           AND (@SubidoPorUsuarioId IS NULL OR a.SubidoPorUsuarioId = @SubidoPorUsuarioId)
        ) AS TotalArchivos,

        (
            /* Pendientes por estado en CargasArchivo */
            (SELECT COUNT(1)
             FROM dbo.CargasArchivo c
             WHERE c.Estado IN (N'RECIBIDO', N'EN_VALIDACION', N'SUBIDO', N'VALIDANDO')
               AND (@DependenciaId IS NULL OR c.DependenciaId = @DependenciaId)
               AND (@SubidoPorUsuarioId IS NULL OR c.UsuarioId = @SubidoPorUsuarioId)
            )
            +
            /* Pendientes por estado en Archivos */
            (SELECT COUNT(1)
             FROM dbo.Archivos a
             WHERE a.Estado IN (N'PendienteValidacion', N'Validado')
               AND (@DependenciaId IS NULL OR a.DependenciaId = @DependenciaId)
               AND (@SubidoPorUsuarioId IS NULL OR a.SubidoPorUsuarioId = @SubidoPorUsuarioId)
            )
        ) AS CargasPendientes,

        (
            /* Con error por estado en CargasArchivo */
            (SELECT COUNT(1)
             FROM dbo.CargasArchivo c
             WHERE c.Estado = N'VALIDADO_CON_ERRORES'
               AND (@DependenciaId IS NULL OR c.DependenciaId = @DependenciaId)
               AND (@SubidoPorUsuarioId IS NULL OR c.UsuarioId = @SubidoPorUsuarioId)
            )
            +
            /* Con error por estado en Archivos */
            (SELECT COUNT(1)
             FROM dbo.Archivos a
             WHERE a.Estado = N'Rechazado'
               AND (@DependenciaId IS NULL OR a.DependenciaId = @DependenciaId)
               AND (@SubidoPorUsuarioId IS NULL OR a.SubidoPorUsuarioId = @SubidoPorUsuarioId)
            )
        ) AS CargasConError,

        (SELECT COUNT(1)
         FROM dbo.CargasArchivo c
         WHERE c.Estado = N'APROBADO'
           AND (@DependenciaId IS NULL OR c.DependenciaId = @DependenciaId)
           AND (@SubidoPorUsuarioId IS NULL OR c.UsuarioId = @SubidoPorUsuarioId)
        ) AS CargasAprobadas;

    /* 2) Últimos cargues (2do result set) */
    SELECT TOP (10)
        Id,
        Dependencia,
        Estado,
        Archivo,
        Fecha,
        Usuario
    FROM dbo.vw_Dashboard_UltimosCargues
    WHERE (@DependenciaId IS NULL OR DependenciaId = @DependenciaId)
      AND (@SubidoPorUsuarioId IS NULL OR UsuarioId = @SubidoPorUsuarioId)
    ORDER BY Fecha DESC, Id DESC;
END;
GO

