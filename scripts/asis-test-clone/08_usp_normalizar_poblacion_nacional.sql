/*
================================================================================
 08_usp_normalizar_poblacion_nacional.sql
================================================================================
 PROPÓSITO:
   Procedimiento que normaliza población a nivel NACION desde la tabla DANE
   PPED_AreaSexoEdadNac_1950_2070 hacia fact_poblacion_proyeccion.
   Despivotea columnas Hombres_* y Mujeres_* por edad simple y área geográfica.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test.

 DEPENDENCIAS:
   - 07_fact_poblacion_proyeccion.sql (tabla fact + fn_ASIS_Resolver_IdArea)
   - 14_proyeccion_dane_versionamiento.sql (dim_proyeccion_dane)
   - Tabla fuente: dbo.PPED_AreaSexoEdadNac_1950_2070
   - dim_sexo, map_area_residencia_fuente

 ORDEN DE EJECUCIÓN:
   Después de 07 y 14. Invocado por 11_usp_normalizar_poblacion_todo.sql
   o directamente: EXEC dbo.usp_ASIS_Normalizar_Poblacion_Nacional @id_proyeccion_dane = N;
================================================================================
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Solo ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_ASIS_Normalizar_Poblacion_Nacional
    @id_proyeccion_dane int   /* Versión de proyección DANE a cargar */
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
    BEGIN
        RAISERROR(N'Solo ObservatorioDB_ASIS_Test.', 16, 1);
        RETURN;
    END

    /* Validar que la proyección DANE existe */
    IF @id_proyeccion_dane IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.dim_proyeccion_dane WHERE id_proyeccion_dane = @id_proyeccion_dane
    )
    BEGIN
        RAISERROR(N'id_proyeccion_dane invalido: %d', 16, 1, @id_proyeccion_dane);
        RETURN;
    END

    DECLARE @fuente varchar(150) = N'PPED_AreaSexoEdadNac_1950_2070';
    DECLARE @oid int = OBJECT_ID(N'dbo.PPED_AreaSexoEdadNac_1950_2070');
    IF @oid IS NULL
    BEGIN
        RAISERROR(N'Falta tabla PPED_AreaSexoEdadNac_1950_2070', 16, 1);
        RETURN;
    END

    /* Detectar nombres de columnas año y área (posiciones 3 y 4 en la fuente) */
    DECLARE @colAno sysname, @colArea sysname;
    SELECT @colAno = name FROM sys.columns WHERE object_id = @oid AND column_id = 3;
    SELECT @colArea = name FROM sys.columns WHERE object_id = @oid AND column_id = 4;

    /* Agregar dinámicamente columnas de edad por sexo para UNPIVOT */
    DECLARE @homCols nvarchar(max), @mujCols nvarchar(max);
    SELECT @homCols = STRING_AGG(QUOTENAME(name), N',') WITHIN GROUP (ORDER BY column_id)
    FROM sys.columns WHERE object_id = @oid AND name LIKE N'Hombres_%';
    SELECT @mujCols = STRING_AGG(QUOTENAME(name), N',') WITHIN GROUP (ORDER BY column_id)
    FROM sys.columns WHERE object_id = @oid AND name LIKE N'Mujeres_%';

    IF @homCols IS NULL OR @mujCols IS NULL
    BEGIN
        RAISERROR(N'No se encontraron columnas Hombres_/Mujeres_', 16, 1);
        RETURN;
    END

    DECLARE @idSexoH int = (SELECT id_sexo FROM dbo.dim_sexo WHERE sexo = N'MASCULINO');
    DECLARE @idSexoM int = (SELECT id_sexo FROM dbo.dim_sexo WHERE sexo = N'FEMENINO');

    /* Recarga idempotente: borrar filas previas de esta fuente y proyección */
    DELETE FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = @fuente AND id_proyeccion_dane = @id_proyeccion_dane;

    DECLARE @sql nvarchar(max);

    /*
      INSERT-SELECT dinámico: población nacional MASCULINO por edad simple.
      Fuente: PPED_AreaSexoEdadNac_1950_2070 -> UNPIVOT columnas Hombres_*.
      Filtro: excluir filas con AREA = 'TOTAL'; resolver id_area vía función.
      Territorio: cod 00/00000 (nivel NACION, sin departamento/municipio).
    */
    SET @sql = N'
    INSERT INTO dbo.fact_poblacion_proyeccion (
        id_proyeccion_dane, nivel_territorial, tipo_registro, id_departamento, id_municipio,
        cod_departamento, cod_municipio, codigo_dane, anio, id_area, id_sexo,
        edad_simple, edad_etiqueta, poblacion, fuente_tabla
    )
    SELECT
        @idProy, N''NACION'', N''EDAD_SIMPLE'', NULL, NULL, N''00'', NULL, N''00000'',
        CAST(u.ano_val AS int),
        dbo.fn_ASIS_Resolver_IdArea(CAST(u.area_val AS nvarchar(300))),
        @idSexoH,
        CASE WHEN u.edad_col LIKE N''%y_m%'' OR u.edad_col LIKE N''%100%'' THEN 100
             ELSE TRY_CAST(REPLACE(u.edad_col, N''Hombres_'', N'''') AS int) END,
        CASE WHEN u.edad_col LIKE N''%y_m%'' THEN N''100+''
             ELSE REPLACE(u.edad_col, N''Hombres_'', N'''') END,
        CAST(ROUND(u.poblacion, 0) AS bigint),
        @fuente
    FROM (
        SELECT CAST(' + QUOTENAME(@colAno) + N' AS sql_variant) AS ano_val,
               CAST(' + QUOTENAME(@colArea) + N' AS nvarchar(300)) AS area_val,
               ' + @homCols + N'
        FROM dbo.PPED_AreaSexoEdadNac_1950_2070
        /* Filtro área: excluir agregado TOTAL (se cargan desgloses urbano/rural) */
        WHERE UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colArea) + N' AS nvarchar(300))))) <> N''TOTAL''
    ) AS s
    UNPIVOT (poblacion FOR edad_col IN (' + @homCols + N')) AS u
    WHERE u.poblacion IS NOT NULL AND u.poblacion <> 0
      AND dbo.fn_ASIS_Resolver_IdArea(CAST(u.area_val AS nvarchar(300))) IS NOT NULL';

    EXEC sys.sp_executesql @sql,
        N'@idSexoH int, @fuente varchar(150), @idProy int',
        @idSexoH = @idSexoH, @fuente = @fuente, @idProy = @id_proyeccion_dane;

    /*
      INSERT-SELECT dinámico: población nacional FEMENINO por edad simple.
      Misma lógica que hombres; UNPIVOT columnas Mujeres_*.
    */
    SET @sql = N'
    INSERT INTO dbo.fact_poblacion_proyeccion (
        id_proyeccion_dane, nivel_territorial, tipo_registro, id_departamento, id_municipio,
        cod_departamento, cod_municipio, codigo_dane, anio, id_area, id_sexo,
        edad_simple, edad_etiqueta, poblacion, fuente_tabla
    )
    SELECT
        @idProy, N''NACION'', N''EDAD_SIMPLE'', NULL, NULL, N''00'', NULL, N''00000'',
        CAST(u.ano_val AS int),
        dbo.fn_ASIS_Resolver_IdArea(CAST(u.area_val AS nvarchar(300))),
        @idSexoM,
        CASE WHEN u.edad_col LIKE N''%y_m%'' OR u.edad_col LIKE N''%100%'' THEN 100
             ELSE TRY_CAST(REPLACE(u.edad_col, N''Mujeres_'', N'''') AS int) END,
        CASE WHEN u.edad_col LIKE N''%y_m%'' THEN N''100+''
             ELSE REPLACE(u.edad_col, N''Mujeres_'', N'''') END,
        CAST(ROUND(u.poblacion, 0) AS bigint),
        @fuente
    FROM (
        SELECT CAST(' + QUOTENAME(@colAno) + N' AS sql_variant) AS ano_val,
               CAST(' + QUOTENAME(@colArea) + N' AS nvarchar(300)) AS area_val,
               ' + @mujCols + N'
        FROM dbo.PPED_AreaSexoEdadNac_1950_2070
        WHERE UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colArea) + N' AS nvarchar(300))))) <> N''TOTAL''
    ) AS s
    UNPIVOT (poblacion FOR edad_col IN (' + @mujCols + N')) AS u
    WHERE u.poblacion IS NOT NULL AND u.poblacion <> 0
      AND dbo.fn_ASIS_Resolver_IdArea(CAST(u.area_val AS nvarchar(300))) IS NOT NULL';

    EXEC sys.sp_executesql @sql,
        N'@idSexoM int, @fuente varchar(150), @idProy int',
        @idSexoM = @idSexoM, @fuente = @fuente, @idProy = @id_proyeccion_dane;

    PRINT N'usp_ASIS_Normalizar_Poblacion_Nacional: ' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' (ultimo batch)';
END
GO

PRINT N'08_usp_normalizar_poblacion_nacional.sql OK';
GO
