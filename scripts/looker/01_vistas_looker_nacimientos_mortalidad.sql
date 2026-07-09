/*
================================================================================
 01_vistas_looker_nacimientos_mortalidad.sql
================================================================================
 PROPÓSITO:
   Crea vistas planas para Looker Studio (prefijo vw_Looker_*) con datos
   de nacimientos y mortalidad sin agregación adicional — una fila por
   combinación en la tabla hecho, con columnas en PascalCase para BI.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test u ObservatorioDB.

 DEPENDENCIAS:
   - fact_nacimientos_casanare_normalizada (21_usp_normalizar_nacimientos_casanare.sql)
   - fact_defunciones_casanare_normalizada (carga defunciones)
   - Catálogos dim_* correspondientes

 ORDEN DE EJECUCIÓN:
   Después de normalización de hechos (21 y defunciones). Antes de conectar Looker.

   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -f 65001 -i scripts\looker\01_vistas_looker_nacimientos_mortalidad.sql
================================================================================
*/
SET NOCOUNT ON;
GO

IF DB_NAME() NOT IN (N'ObservatorioDB_ASIS_Test', N'ObservatorioDB')
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar en ObservatorioDB u ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== Vistas Looker Studio: nacimientos y mortalidad ===';
GO

/*
  Vista Looker nacimientos: fila por registro normalizado con dimensiones descriptivas.
  Fuente: fact_nacimientos_casanare_normalizada + JOINs a dim_departamento, dim_municipio,
  dim_sexo, dim_grupo_edad_madre, dim_peso_al_nacer, dim_semanas_gestacion.
*/
CREATE OR ALTER VIEW dbo.vw_Looker_Nacimientos_Casanare
AS
SELECT
    f.codigo_departamento AS CodigoDepartamento,
    de.nombre_departamento AS NombreDepartamento,
    f.codigo_municipio AS CodigoMunicipio,
    mu.nombre_municipio AS NombreMunicipio,
    f.anio AS Vigencia,
    ds.sexo AS Sexo,
    gm.etiqueta_rango AS GrupoEtareoQuinqueniosDane,
    f.id_peso_al_nacer AS IdPesoAlNacer,
    p.etiqueta_rango AS PesoAlNacer,
    f.id_semanas_gestacion AS IdSemanasGestacion,
    sg.etiqueta_rango AS SemanasGestacion,
    f.numero_nacimientos AS Nacimientos
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de
    ON de.cod_departamento = f.codigo_departamento
/* JOIN municipio: nombre por código DANE y departamento */
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = f.codigo_municipio
   AND mu.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds
    ON ds.id_sexo = f.id_sexo
INNER JOIN dbo.dim_grupo_edad_madre AS gm
    ON gm.id_grupo_edad_madre = f.id_grupo_edad_madre
LEFT JOIN dbo.dim_peso_al_nacer AS p
    ON p.id_peso_al_nacer = f.id_peso_al_nacer
LEFT JOIN dbo.dim_semanas_gestacion AS sg
    ON sg.id_semanas_gestacion = f.id_semanas_gestacion
/* Filtro geográfico: Casanare código DANE 85 */
WHERE f.codigo_departamento = N'85';
GO

/*
  Vista Looker mortalidad: fila por registro normalizado con sexo y grupo etario.
  Fuente: fact_defunciones_casanare_normalizada + dimensiones.
  CausaBasica reservada (NULL) para futura extensión.
*/
CREATE OR ALTER VIEW dbo.vw_Looker_Mortalidad_Casanare
AS
SELECT
    f.codigo_departamento AS CodigoDepartamento,
    de.nombre_departamento AS NombreDepartamento,
    f.codigo_municipio AS CodigoMunicipio,
    COALESCE(mu.nombre_municipio, N'SIN MUNICIPIO DANE') AS NombreMunicipio,
    f.anio AS Vigencia,
    ds.sexo AS Sexo,
    ge.etiqueta_rango AS GrupoEtareoQuinqueniosDane,
    CAST(NULL AS nvarchar(500)) AS CausaBasica,
    f.numero_defunciones AS Defunciones
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de
    ON de.cod_departamento = f.codigo_departamento
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = f.codigo_municipio
   AND mu.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds
    ON ds.id_sexo = f.id_sexo
INNER JOIN dbo.dim_grupo_edad AS ge
    ON ge.id_grupo_edad = f.id_grupo_edad
WHERE f.codigo_departamento = N'85';
GO

PRINT N'Vistas creadas: vw_Looker_Nacimientos_Casanare, vw_Looker_Mortalidad_Casanare';
SELECT COUNT(*) AS FilasNacimientos FROM dbo.vw_Looker_Nacimientos_Casanare;
SELECT COUNT(*) AS FilasMortalidad FROM dbo.vw_Looker_Mortalidad_Casanare;
GO

PRINT N'=== FIN 01_vistas_looker_nacimientos_mortalidad ===';
GO
