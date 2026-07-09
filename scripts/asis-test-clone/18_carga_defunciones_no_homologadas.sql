/*
================================================================================
 18_carga_defunciones_no_homologadas.sql
================================================================================
 PROPÓSITO:
   Inserta en fact_defunciones_casanare_normalizada las filas de [Defunciones Casanare]
   con curso de vida / quinquenios = "No Definido" o "No Reportado", resolviendo
   FKs de sexo y área mediante JOIN a dimensiones.

 BASE DE DATOS DESTINO:
   ObservatorioDB u ObservatorioDB_ASIS_Test.

 DEPENDENCIAS (ejecutar antes):
   - 17_catalogo_defunciones_no_definido_reportado.sql (GE09/GE10, CV07/CV08)
   - 04_normalizacion_catalogos_geograficos.sql (dim_area_residencia con estado)
   - Tabla fact_defunciones_casanare_normalizada existente

 ORDEN DE EJECUCIÓN:
   17 -> 18 (este script) -> validaciones / vistas mortalidad

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\18_carga_defunciones_no_homologadas.sql
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() NOT IN (N'ObservatorioDB_ASIS_Test', N'ObservatorioDB')
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar en ObservatorioDB u ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.dim_grupo_edad WHERE codigo = N'GE09')
BEGIN
    RAISERROR(N'Falta catalogo GE09/GE10. Ejecute primero 17_catalogo_defunciones_no_definido_reportado.sql', 16, 1);
    RETURN;
END
GO

PRINT N'=== 18 - Carga defunciones No Definido / No Reportado al fact ===';
GO

DECLARE @oid int = OBJECT_ID(N'dbo.[Defunciones Casanare]');
DECLARE @colQuin sysname, @colCurso sysname, @colAnio sysname, @colDef sysname;

SELECT @colQuin = MAX(CASE WHEN column_id = 5 THEN name END),
       @colCurso = MAX(CASE WHEN column_id = 6 THEN name END),
       @colAnio = MAX(CASE WHEN column_id = 9 THEN name END),
       @colDef = MAX(CASE WHEN column_id = 10 THEN name END)
FROM sys.columns
WHERE object_id = @oid;

IF @colQuin IS NULL OR @colCurso IS NULL OR @colAnio IS NULL OR @colDef IS NULL
BEGIN
    RAISERROR(N'No se resolvieron columnas de [Defunciones Casanare] desde sys.columns', 16, 1);
    RETURN;
END

DECLARE @idGeNoDef int = (SELECT id_grupo_edad FROM dbo.dim_grupo_edad WHERE codigo = N'GE09');
DECLARE @idGeNoRep int = (SELECT id_grupo_edad FROM dbo.dim_grupo_edad WHERE codigo = N'GE10');
DECLARE @idCvNoDef int = (SELECT id_curso_vida FROM dbo.dim_curso_vida WHERE codigo = N'CV07');
DECLARE @idCvNoRep int = (SELECT id_curso_vida FROM dbo.dim_curso_vida WHERE codigo = N'CV08');

/* --- CTE src: filas fuente No Definido/Reportado; resolved: JOIN dim_sexo y dim_area --- */
DECLARE @sql nvarchar(max) = N'
BEGIN TRANSACTION;

;WITH src AS (
    SELECT
        RIGHT(N''00'' + LTRIM(RTRIM(CAST(r.CODIGO_DEPARTAMENTO AS nvarchar(10)))), 2) AS codigo_departamento,
        CASE WHEN r.CODIGO_MUNICIPIO IS NULL THEN NULL
             ELSE LTRIM(RTRIM(CAST(r.CODIGO_MUNICIPIO AS nvarchar(10)))) END AS codigo_municipio,
        r.' + QUOTENAME(@colAnio) + N' AS anio,
        r.' + QUOTENAME(@colDef) + N' AS numero_defunciones,
        r.Sexo,
        r.Area_Residencia,
        CASE r.' + QUOTENAME(@colCurso) + N'
            WHEN N''No Definido'' THEN @idGeNoDef
            WHEN N''No Reportado'' THEN @idGeNoRep
        END AS id_grupo_edad,
        CASE r.' + QUOTENAME(@colCurso) + N'
            WHEN N''No Definido'' THEN @idCvNoDef
            WHEN N''No Reportado'' THEN @idCvNoRep
        END AS id_curso_vida
    FROM dbo.[Defunciones Casanare] AS r
    WHERE r.' + QUOTENAME(@colCurso) + N' IN (N''No Definido'', N''No Reportado'')
      AND r.' + QUOTENAME(@colQuin) + N' IN (N''No Definido'', N''No Reportado'')
),
resolved AS (
    SELECT
        s.codigo_departamento,
        s.codigo_municipio,
        s.id_grupo_edad,
        s.id_curso_vida,
        sx.id_sexo,
        ar.id_area,
        s.anio,
        s.numero_defunciones
    FROM src AS s
    INNER JOIN dbo.dim_sexo AS sx
        ON sx.sexo = s.Sexo
       AND sx.sexo IN (N''FEMENINO'', N''MASCULINO'', N''INDETERMINADO'')
    INNER JOIN dbo.dim_area_residencia AS ar
        ON ar.area_original = s.Area_Residencia
       AND (ar.estado = 1 OR ar.estado IS NULL)
    WHERE s.id_grupo_edad IS NOT NULL
      AND s.id_curso_vida IS NOT NULL
)
INSERT INTO dbo.fact_defunciones_casanare_normalizada (
    codigo_departamento,
    codigo_municipio,
    id_grupo_edad,
    id_curso_vida,
    id_area,
    id_sexo,
    anio,
    numero_defunciones,
    fecha_carga
)
SELECT
    r.codigo_departamento,
    r.codigo_municipio,
    r.id_grupo_edad,
    r.id_curso_vida,
    r.id_area,
    r.id_sexo,
    r.anio,
    r.numero_defunciones,
    SYSDATETIME()
FROM resolved AS r
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.fact_defunciones_casanare_normalizada AS f
    WHERE ISNULL(f.codigo_departamento, N'''') = ISNULL(r.codigo_departamento, N'''')
      AND ISNULL(f.codigo_municipio, N'''') = ISNULL(r.codigo_municipio, N'''')
      AND f.id_grupo_edad = r.id_grupo_edad
      AND f.id_curso_vida = r.id_curso_vida
      AND f.id_area = r.id_area
      AND f.id_sexo = r.id_sexo
      AND f.anio = r.anio
      AND f.numero_defunciones = r.numero_defunciones
);

SELECT @ins = @@ROWCOUNT;

COMMIT TRANSACTION;
';

DECLARE @ins int;
EXEC sp_executesql @sql,
    N'@idGeNoDef int, @idGeNoRep int, @idCvNoDef int, @idCvNoRep int, @ins int OUTPUT',
    @idGeNoDef, @idGeNoRep, @idCvNoDef, @idCvNoRep, @ins OUTPUT;

PRINT N'Filas insertadas en fact: ' + CAST(@ins AS nvarchar(20));
GO

/* Validacion Yopal 85001 / 2005 y dept 85 */
DECLARE @oid2 int = OBJECT_ID(N'dbo.[Defunciones Casanare]');
DECLARE @colCurso2 sysname, @colAnio2 sysname, @colDef2 sysname;

SELECT @colCurso2 = MAX(CASE WHEN column_id = 6 THEN name END),
       @colAnio2 = MAX(CASE WHEN column_id = 9 THEN name END),
       @colDef2 = MAX(CASE WHEN column_id = 10 THEN name END)
FROM sys.columns
WHERE object_id = @oid2;

/* --- Validación Yopal 85001/2005: JOIN fact ↔ dim_grupo_edad GE09/GE10 --- */
DECLARE @valSql nvarchar(max) = N'
PRINT N''--- Validacion Yopal 85001 / 2005 ---'';
SELECT
    (SELECT SUM(r.' + QUOTENAME(@colDef2) + N')
     FROM dbo.[Defunciones Casanare] AS r
     WHERE r.CODIGO_MUNICIPIO = 85001 AND r.CODIGO_DEPARTAMENTO = 85 AND r.' + QUOTENAME(@colAnio2) + N' = 2005) AS crudo,
    (SELECT SUM(numero_defunciones)
     FROM dbo.fact_defunciones_casanare_normalizada
     WHERE codigo_municipio = N''85001'' AND codigo_departamento = N''85'' AND anio = 2005) AS fact;

PRINT N''--- Total dept 85 No Definido/Reportado ---'';
SELECT
    (SELECT SUM(r.' + QUOTENAME(@colDef2) + N')
     FROM dbo.[Defunciones Casanare] AS r
     WHERE r.CODIGO_DEPARTAMENTO = 85
       AND r.' + QUOTENAME(@colCurso2) + N' IN (N''No Definido'', N''No Reportado'')) AS crudo_dept85,
    (SELECT SUM(f.numero_defunciones)
     FROM dbo.fact_defunciones_casanare_normalizada AS f
     INNER JOIN dbo.dim_grupo_edad AS ge ON ge.id_grupo_edad = f.id_grupo_edad
     WHERE f.codigo_departamento = N''85''
       AND ge.codigo IN (N''GE09'', N''GE10'')) AS fact_dept85;
';

EXEC sp_executesql @valSql;
GO

PRINT N'=== FIN 18_carga_defunciones_no_homologadas ===';
GO
