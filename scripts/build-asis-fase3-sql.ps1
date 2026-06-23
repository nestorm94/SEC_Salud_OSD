# Genera sql-refactor-fase7-asis-03-vistas-poblacion-mortalidad.sql
# Lee nombres de columna con acentos desde SQL Server (evita corrupcion UTF-8 en sqlcmd).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'sql-refactor-fase7-asis-03-vistas-poblacion-mortalidad.sql'
$hCols = (Get-Content (Join-Path $root '_asis_unpivot_h.txt') -Raw -Encoding UTF8).Trim()
$mCols = (Get-Content (Join-Path $root '_asis_unpivot_m.txt') -Raw -Encoding UTF8).Trim()

function Get-ColumnNames {
    param([string]$ObjectName)
    $cs = "Server=localhost\SQLEXPRESS2025;Database=ObservatorioDB;Trusted_Connection=True;TrustServerCertificate=True"
    $cn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = @"
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@obj)
ORDER BY c.column_id
"@
        $null = $cmd.Parameters.AddWithValue('@obj', $ObjectName)
        $r = $cmd.ExecuteReader()
        $list = New-Object System.Collections.Generic.List[string]
        while ($r.Read()) { $list.Add([string]$r.GetString(0)) | Out-Null }
        return $list
    }
    finally { $cn.Close() }
}

function Bracket([string]$n) { "[$n]" }

$pobCols = Get-ColumnNames 'dbo.vw_Poblacion_Nacional_Casanare'
$qCols = Get-ColumnNames 'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado'
$cCols = Get-ColumnNames 'dbo.vw_Reporte_Poblacion_CursoVida_Unificado'
$ppedCols = Get-ColumnNames 'dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]'

$cDane = Bracket ($pobCols[0])
$cAno = Bracket ($pobCols[6])
$cArea = Bracket ($pobCols[4])
$cPob = Bracket ($pobCols[7])
$cSexo = Bracket ($pobCols[5])
$cRegional = Bracket ($pobCols[3])

$qDane = Bracket ($qCols[0])
$qAno = Bracket ($qCols[6])
$qArea = Bracket ($qCols[4])
$qPob = Bracket ($qCols[7])
$qSexo = Bracket ($qCols[5])
$qQuinq = Bracket ($qCols[7])

$cDane2 = Bracket ($cCols[0])
$cAno2 = Bracket ($cCols[6])
$cArea2 = Bracket ($cCols[4])
$cPob2 = Bracket ($cCols[7])
$cSexo2 = Bracket ($cCols[5])
$cCurso = Bracket ($cCols[7])
$cCursoNom = Bracket ($cCols[8])

$ppedArea = Bracket (($ppedCols | Where-Object { $_ -like '*REA*' } | Select-Object -First 1))

$header = @'
/*
FASE 3 ASIS - Vistas poblacion y mortalidad (Casanare, DANE 85).
ObservatorioDB - prefijo vw_ASIS_
*/
SET NOCOUNT ON;
GO

'@

$pob = @"
/* ========== POBLACION ========== */

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Total
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    CAST(p.$cAno AS int) AS vigencia,
    CAST(SUM(CAST(p.$cPob AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Poblacion_Nacional_Casanare' AS fuente_datos,
    N'Casanare DANE 85; Sexo=Total; Area=Total' AS criterio_agregacion
FROM dbo.vw_Poblacion_Nacional_Casanare AS p
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
WHERE LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))) = N'85'
  AND p.$cSexo = N'Total'
  AND p.$cArea = N'Total'
GROUP BY de.cod_departamento, de.nombre_departamento, CAST(p.$cAno AS int);
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Municipio
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    mu.codigo_dane AS codigo_municipio,
    mu.nombre_municipio,
    MAX(p.$cRegional) AS regional,
    CAST(p.$cAno AS int) AS vigencia,
    CAST(SUM(CAST(p.$cPob AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Poblacion_Nacional_Casanare' AS fuente_datos,
    N'Municipio Casanare; Sexo=Total; Area=Total' AS criterio_agregacion
FROM dbo.vw_Poblacion_Nacional_Casanare AS p
INNER JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))
   AND mu.cod_departamento = N'85'
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = mu.cod_departamento
WHERE LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
  AND LEFT(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))), 2) = N'85'
  AND p.$cSexo = N'Total'
  AND p.$cArea = N'Total'
GROUP BY de.cod_departamento, de.nombre_departamento, mu.codigo_dane, mu.nombre_municipio, CAST(p.$cAno AS int);
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Sexo
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    ds.id_sexo,
    ds.sexo AS sexo_dim,
    p.$cSexo AS sexo_proyeccion,
    CAST(p.$cAno AS int) AS vigencia,
    CAST(SUM(CAST(p.$cPob AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Poblacion_Nacional_Casanare' AS fuente_datos,
    N'Hombres/Mujeres; Area=Total' AS criterio_agregacion
FROM dbo.vw_Poblacion_Nacional_Casanare AS p
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
INNER JOIN dbo.dim_sexo AS ds
    ON ds.sexo = CASE p.$cSexo WHEN N'Hombres' THEN N'MASCULINO' WHEN N'Mujeres' THEN N'FEMENINO' END
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))
   AND mu.cod_departamento = N'85'
WHERE (LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))) = N'85'
    OR (LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))), 2) = N'85'))
  AND p.$cSexo IN (N'Hombres', N'Mujeres')
  AND p.$cArea = N'Total'
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, ds.id_sexo, ds.sexo, p.$cSexo, CAST(p.$cAno AS int);
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Area
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    da.id_area,
    da.area_normalizada,
    p.$cArea AS area_proyeccion,
    CAST(p.$cAno AS int) AS vigencia,
    CAST(SUM(CAST(p.$cPob AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Poblacion_Nacional_Casanare' AS fuente_datos,
    N'Urbano/Rural; Sexo=Total' AS criterio_agregacion
FROM dbo.vw_Poblacion_Nacional_Casanare AS p
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
INNER JOIN dbo.dim_area_residencia AS da ON da.area_normalizada = p.$cArea
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))
   AND mu.cod_departamento = N'85'
WHERE (LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))) = N'85'
    OR (LEN(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))), 2) = N'85'))
  AND p.$cSexo = N'Total'
  AND p.$cArea IN (N'Urbano', N'Rural')
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(p.$cDane AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, da.id_area, da.area_normalizada, p.$cArea, CAST(p.$cAno AS int);
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_GrupoEdad
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    q.$qQuinq AS grupo_quinquenal,
    dg.id_grupo_edad,
    dg.codigo AS codigo_grupo_edad_dim,
    dg.nombre_grupo_edad,
    CAST(q.$qAno AS int) AS vigencia,
    CAST(SUM(CAST(q.$qPob AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Reporte_Poblacion_Quinquenios_Unificado' AS fuente_datos,
    N'Quinquenios DANE; Sexo=Total; Area=Total' AS criterio_agregacion
FROM dbo.vw_Reporte_Poblacion_Quinquenios_Unificado AS q
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10))))
   AND mu.cod_departamento = N'85'
LEFT JOIN dbo.dim_grupo_edad AS dg
    ON q.$qQuinq LIKE N'%' + dg.etiqueta_rango + N'%' COLLATE Latin1_General_CI_AI
    OR q.$qQuinq LIKE N'%' + dg.nombre_grupo_edad + N'%' COLLATE Latin1_General_CI_AI
WHERE (LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10)))) = N'85'
    OR (LEN(LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10)))), 2) = N'85'))
  AND q.$qSexo = N'Total'
  AND q.$qArea = N'Total'
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(q.$qDane AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, q.$qQuinq, dg.id_grupo_edad, dg.codigo, dg.nombre_grupo_edad, CAST(q.$qAno AS int);
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    c.$cCurso AS curso_vida_proyeccion,
    dc.id_curso_vida,
    dc.codigo AS codigo_curso_vida_dim,
    dc.nombre_curso_vida,
    CAST(c.$cAno2 AS int) AS vigencia,
    CAST(SUM(CAST(c.$cPob2 AS decimal(18, 2))) AS bigint) AS poblacion,
    N'vw_Reporte_Poblacion_CursoVida_Unificado' AS fuente_datos,
    N'Curso de vida; Sexo=Total; Area=Total' AS criterio_agregacion
FROM dbo.vw_Reporte_Poblacion_CursoVida_Unificado AS c
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10))))
   AND mu.cod_departamento = N'85'
LEFT JOIN dbo.dim_curso_vida AS dc
    ON c.$cCurso LIKE N'%' + dc.nombre_curso_vida + N'%' COLLATE Latin1_General_CI_AI
WHERE (LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10)))) = N'85'
    OR (LEN(LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10)))), 2) = N'85'))
  AND c.$cSexo2 = N'Total'
  AND c.$cArea2 = N'Total'
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(c.$cDane2 AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, c.$cCurso, dc.id_curso_vida, dc.codigo, dc.nombre_curso_vida, CAST(c.$cAno2 AS int);
GO

"@

$piramide = @"

CREATE OR ALTER VIEW dbo.vw_ASIS_Piramide_Poblacional
AS
WITH src AS (
    SELECT *
    FROM dbo.[PPED-AreaSexoEdadMun-2018-2042_VP] AS t
    WHERE LTRIM(RTRIM(CAST(t.DP AS nvarchar(10)))) = N'85'
),
h AS (
    SELECT
        LTRIM(RTRIM(CAST(DP AS nvarchar(10)))) AS codigo_departamento,
        LTRIM(RTRIM(CAST(MPIO AS nvarchar(10)))) AS codigo_municipio,
        LTRIM(RTRIM(CAST(DPMP AS nvarchar(20)))) AS codigo_departamento_municipio,
        CAST(ANO AS int) AS vigencia,
        $ppedArea AS area_geografica,
        N'MASCULINO' AS sexo,
        TRY_CAST(
            CASE
                WHEN nombre_columna LIKE N'%100_y_m%' THEN N'100'
                ELSE REPLACE(nombre_columna, N'Hombres_', N'')
            END AS int) AS edad_simple,
        CAST(poblacion AS decimal(18, 2)) AS poblacion
    FROM src
    UNPIVOT (poblacion FOR nombre_columna IN ($hCols)) AS unpiv_h
),
m AS (
    SELECT
        LTRIM(RTRIM(CAST(DP AS nvarchar(10)))) AS codigo_departamento,
        LTRIM(RTRIM(CAST(MPIO AS nvarchar(10)))) AS codigo_municipio,
        LTRIM(RTRIM(CAST(DPMP AS nvarchar(20)))) AS codigo_departamento_municipio,
        CAST(ANO AS int) AS vigencia,
        $ppedArea AS area_geografica,
        N'FEMENINO' AS sexo,
        TRY_CAST(
            CASE
                WHEN nombre_columna LIKE N'%100_y_m%' THEN N'100'
                ELSE REPLACE(nombre_columna, N'Mujeres_', N'')
            END AS int) AS edad_simple,
        CAST(poblacion AS decimal(18, 2)) AS poblacion
    FROM src
    UNPIVOT (poblacion FOR nombre_columna IN ($mCols)) AS unpiv_m
),
unioned AS (
    SELECT * FROM h
    UNION ALL
    SELECT * FROM m
)
SELECT
    u.codigo_departamento,
    de.nombre_departamento,
    u.codigo_municipio,
    mu.nombre_municipio,
    CAST(NULL AS nvarchar(100)) AS regional,
    u.codigo_departamento_municipio,
    u.vigencia,
    u.area_geografica,
    ds.id_sexo,
    ds.sexo AS sexo_dim,
    u.edad_simple,
    u.poblacion,
    N'PPED-AreaSexoEdadMun-2018-2042_VP' AS fuente_datos,
    N'Edad simple 0-100+; UNPIVOT Hombres/Mujeres' AS criterio_agregacion
FROM unioned AS u
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = u.codigo_departamento
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = u.codigo_municipio
   AND mu.cod_departamento = u.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.sexo = u.sexo
WHERE u.poblacion IS NOT NULL AND u.poblacion <> 0;
GO

"@

$mort = @'
/* ========== MORTALIDAD ========== */

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_Total
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.anio AS vigencia,
    SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada' AS fuente_datos,
    N'Casanare codigo_departamento=85' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_Municipio
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.codigo_municipio,
    COALESCE(mu.nombre_municipio, N'SIN MUNICIPIO DANE') AS nombre_municipio,
    CAST(NULL AS nvarchar(100)) AS regional,
    f.anio AS vigencia,
    SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada' AS fuente_datos,
    N'Agregado por codigo_municipio (incluye NULL)' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_Sexo
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    ds.id_sexo, ds.sexo AS sexo_dim, f.anio AS vigencia,
    SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada + dim_sexo' AS fuente_datos,
    N'FK id_sexo' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, ds.id_sexo, ds.sexo, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_Area
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    da.id_area, da.area_normalizada, f.anio AS vigencia,
    SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada + dim_area_residencia' AS fuente_datos,
    N'FK id_area' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, da.id_area, da.area_normalizada, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_GrupoEdad
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    dg.id_grupo_edad, dg.codigo AS codigo_grupo_edad, dg.nombre_grupo_edad, dg.etiqueta_rango,
    f.anio AS vigencia, SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada + dim_grupo_edad' AS fuente_datos,
    N'FK id_grupo_edad' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_grupo_edad AS dg ON dg.id_grupo_edad = f.id_grupo_edad
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    dg.id_grupo_edad, dg.codigo, dg.nombre_grupo_edad, dg.etiqueta_rango, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_CursoVida
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    dc.id_curso_vida, dc.codigo AS codigo_curso_vida, dc.nombre_curso_vida,
    f.anio AS vigencia, SUM(f.numero_defunciones) AS defunciones,
    N'fact_defunciones_casanare_normalizada + dim_curso_vida' AS fuente_datos,
    N'FK id_curso_vida' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_curso_vida AS dc ON dc.id_curso_vida = f.id_curso_vida
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    dc.id_curso_vida, dc.codigo, dc.nombre_curso_vida, f.anio;
GO

'@

$indicadores = @'
/* ========== INDICADORES DERIVADOS ========== */

CREATE OR ALTER VIEW dbo.vw_ASIS_Tasa_Bruta_Mortalidad
AS
SELECT
    N'DEPARTAMENTO' AS nivel_territorio,
    d.codigo_departamento, d.nombre_departamento,
    CAST(NULL AS nvarchar(10)) AS codigo_municipio,
    CAST(NULL AS nvarchar(200)) AS nombre_municipio,
    d.vigencia, d.defunciones, p.poblacion,
    CAST(CASE WHEN p.poblacion > 0 THEN (CAST(d.defunciones AS decimal(18,6)) / CAST(p.poblacion AS decimal(18,6))) * 1000.0 ELSE NULL END AS decimal(18,6)) AS tasa_bruta_mortalidad,
    N'(defunciones / poblacion) * 1000' AS formula,
    N'vw_ASIS_Mortalidad_Total + vw_ASIS_Poblacion_Total' AS fuente_datos
FROM dbo.vw_ASIS_Mortalidad_Total AS d
INNER JOIN dbo.vw_ASIS_Poblacion_Total AS p ON p.codigo_departamento = d.codigo_departamento AND p.vigencia = d.vigencia
UNION ALL
SELECT
    N'MUNICIPIO', m.codigo_departamento, m.nombre_departamento, m.codigo_municipio, m.nombre_municipio,
    m.vigencia, m.defunciones, p.poblacion,
    CAST(CASE WHEN p.poblacion > 0 THEN (CAST(m.defunciones AS decimal(18,6)) / CAST(p.poblacion AS decimal(18,6))) * 1000.0 ELSE NULL END AS decimal(18,6)),
    N'(defunciones / poblacion) * 1000', N'vw_ASIS_Mortalidad_Municipio + vw_ASIS_Poblacion_Municipio'
FROM dbo.vw_ASIS_Mortalidad_Municipio AS m
INNER JOIN dbo.vw_ASIS_Poblacion_Municipio AS p ON p.codigo_municipio = m.codigo_municipio AND p.vigencia = m.vigencia
WHERE m.codigo_municipio IS NOT NULL;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Serie_Mortalidad
AS
SELECT
    t.codigo_departamento, t.nombre_departamento, t.vigencia, t.defunciones, p.poblacion,
    CAST(CASE WHEN p.poblacion > 0 THEN (CAST(t.defunciones AS decimal(18,6)) / CAST(p.poblacion AS decimal(18,6))) * 1000.0 ELSE NULL END AS decimal(18,6)) AS tasa_bruta_mortalidad,
    N'vw_ASIS_Mortalidad_Total + vw_ASIS_Poblacion_Total' AS fuente_datos,
    N'Serie 2005-2025 Casanare' AS criterio_agregacion
FROM dbo.vw_ASIS_Mortalidad_Total AS t
INNER JOIN dbo.vw_ASIS_Poblacion_Total AS p ON p.codigo_departamento = t.codigo_departamento AND p.vigencia = t.vigencia
WHERE t.vigencia BETWEEN 2005 AND 2025;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Comparativo_Poblacion_Mortalidad
AS
SELECT
    COALESCE(p.codigo_departamento, m.codigo_departamento) AS codigo_departamento,
    COALESCE(p.nombre_departamento, m.nombre_departamento) AS nombre_departamento,
    COALESCE(p.codigo_municipio, m.codigo_municipio) AS codigo_municipio,
    COALESCE(p.nombre_municipio, m.nombre_municipio) AS nombre_municipio,
    COALESCE(p.regional, m.regional) AS regional,
    COALESCE(p.vigencia, m.vigencia) AS vigencia,
    p.poblacion, m.defunciones,
    CAST(CASE WHEN p.poblacion > 0 AND m.defunciones IS NOT NULL
         THEN (CAST(m.defunciones AS decimal(18,6)) / CAST(p.poblacion AS decimal(18,6))) * 1000.0 ELSE NULL END AS decimal(18,6)) AS tasa_bruta_mortalidad,
    N'vw_ASIS_Poblacion_Municipio FULL JOIN vw_ASIS_Mortalidad_Municipio' AS fuente_datos
FROM dbo.vw_ASIS_Poblacion_Municipio AS p
FULL OUTER JOIN dbo.vw_ASIS_Mortalidad_Municipio AS m
    ON m.codigo_municipio = p.codigo_municipio AND m.vigencia = p.vigencia AND m.codigo_departamento = p.codigo_departamento
WHERE COALESCE(p.codigo_departamento, m.codigo_departamento) = N'85';
GO

'@

$validacion = @'
PRINT N'--- vw_ASIS_Poblacion_Total ---';
SELECT TOP (10) * FROM dbo.vw_ASIS_Poblacion_Total ORDER BY vigencia DESC;
PRINT N'--- vw_ASIS_Piramide_Poblacional ---';
SELECT TOP (10) * FROM dbo.vw_ASIS_Piramide_Poblacional ORDER BY vigencia DESC, codigo_municipio, edad_simple;
PRINT N'--- vw_ASIS_Serie_Mortalidad ---';
SELECT TOP (10) * FROM dbo.vw_ASIS_Serie_Mortalidad ORDER BY vigencia;
PRINT N'Fase 3 ASIS: vistas vw_ASIS_* listas.';
GO
'@

$content = $header + $pob + $piramide + $mort + $indicadores + $validacion
$utf16 = New-Object System.Text.UnicodeEncoding $false, $true
[System.IO.File]::WriteAllText($out, $content, $utf16)
Write-Host "OK: $out ($($content.Length) chars)"
Write-Host "Columnas: $cDane | $cAno | $cArea | $ppedArea"
