/*
================================================================================
 09_usp_normalizar_poblacion_departamental.sql
================================================================================
 PROPÓSITO:
   Procedimiento que normaliza población a nivel DEPARTAMENTO desde
   Poblacion_por_Departamento hacia fact_poblacion_proyeccion.
   Carga totales por sexo (Hombres/Mujeres) y área geográfica por departamento.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test.

 DEPENDENCIAS:
   - 07_fact_poblacion_proyeccion.sql
   - 14_proyeccion_dane_versionamiento.sql
   - Tabla fuente: dbo.Poblacion_por_Departamento
   - dim_departamento, dim_sexo, fn_ASIS_Resolver_IdArea

 ORDEN DE EJECUCIÓN:
   Después de 08 (nacional). Invocado por 11 o directamente con @id_proyeccion_dane.
   Nota: incluye todos los departamentos de la fuente (no filtra Casanare 85).
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

CREATE OR ALTER PROCEDURE dbo.usp_ASIS_Normalizar_Poblacion_Departamental
    @id_proyeccion_dane int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
    BEGIN
        RAISERROR(N'Solo ObservatorioDB_ASIS_Test.', 16, 1);
        RETURN;
    END

    IF @id_proyeccion_dane IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.dim_proyeccion_dane WHERE id_proyeccion_dane = @id_proyeccion_dane
    )
    BEGIN
        RAISERROR(N'id_proyeccion_dane invalido: %d', 16, 1, @id_proyeccion_dane);
        RETURN;
    END

    DECLARE @fuente varchar(150) = N'Poblacion_por_Departamento';
    DECLARE @idSexoH int = (SELECT id_sexo FROM dbo.dim_sexo WHERE sexo = N'MASCULINO');
    DECLARE @idSexoM int = (SELECT id_sexo FROM dbo.dim_sexo WHERE sexo = N'FEMENINO');

    DELETE FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = @fuente AND id_proyeccion_dane = @id_proyeccion_dane;

    /*
      INSERT-SELECT: totales departamentales por sexo y área.
      Fuente: Poblacion_por_Departamento.
      JOIN dim_departamento por cod_departamento (CODIGO_DANE normalizado a 2 dígitos).
      CROSS APPLY duplica fila en Mujeres y Hombres.
      Filtro: excluir AREA_GEOGRAFICA = 'TOTAL'; requiere id_area resuelto.
    */
    INSERT INTO dbo.fact_poblacion_proyeccion (
        id_proyeccion_dane, nivel_territorial, tipo_registro, id_departamento, id_municipio,
        cod_departamento, cod_municipio, codigo_dane, anio, id_area, id_sexo,
        edad_simple, edad_etiqueta, poblacion, fuente_tabla
    )
    SELECT
        @id_proyeccion_dane,
        N'DEPARTAMENTO',
        N'TOTAL_SEXO',
        d.id_departamento,
        NULL,
        RIGHT(N'00' + LTRIM(RTRIM(CAST(p.CODIGO_DANE AS varchar(10)))), 2),
        NULL,
        NULL,
        CAST(p.ano AS int),
        dbo.fn_ASIS_Resolver_IdArea(CAST(p.AREA_GEOGRAFICA AS nvarchar(300))),
        v.id_sexo,
        NULL,
        NULL,
        CAST(v.poblacion AS bigint),
        @fuente
    FROM dbo.Poblacion_por_Departamento AS p
    /* JOIN geográfico: homologar código DANE departamento (2 dígitos) */
    LEFT JOIN dbo.dim_departamento AS d
        ON d.cod_departamento = RIGHT(N'00' + LTRIM(RTRIM(CAST(p.CODIGO_DANE AS varchar(10)))), 2)
    /* Desdoblar Total_Mujeres y Total_Hombres en filas separadas */
    CROSS APPLY (VALUES
        (@idSexoM, p.Total_Mujeres),
        (@idSexoH, p.Total_Hombres)
    ) AS v (id_sexo, poblacion)
    WHERE UPPER(LTRIM(RTRIM(CAST(p.AREA_GEOGRAFICA AS nvarchar(300))))) <> N'TOTAL'
      AND v.poblacion IS NOT NULL
      AND dbo.fn_ASIS_Resolver_IdArea(CAST(p.AREA_GEOGRAFICA AS nvarchar(300))) IS NOT NULL;

    PRINT N'usp_ASIS_Normalizar_Poblacion_Departamental: ' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' filas';
END
GO

PRINT N'09_usp_normalizar_poblacion_departamental.sql OK';
GO
