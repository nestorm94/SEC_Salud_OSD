/*
Corrige tildes en catalogos nacimientos (años, más, información).
Ejecutar si en UI aparece "aÃ±os" u otros caracteres corruptos.

  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -f 65001 -i scripts\asis-test-clone\23_fix_encoding_nacimientos.sql
*/
SET NOCOUNT ON;
GO

PRINT N'=== 23 - Fix encoding catalogos nacimientos ===';

UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 10 a 14 años'  WHERE codigo = N'QM01';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 15 a 19 años'  WHERE codigo = N'QM02';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 20 a 24 años'  WHERE codigo = N'QM03';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 25 a 29 años'  WHERE codigo = N'QM04';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 30 a 34 años'  WHERE codigo = N'QM05';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 35 a 39 años'  WHERE codigo = N'QM06';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 40 a 44 años'  WHERE codigo = N'QM07';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 45 a 49 años'  WHERE codigo = N'QM08';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 50 a 54 años'  WHERE codigo = N'QM09';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'De 55 a 59 años'  WHERE codigo = N'QM10';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'60 años y más'    WHERE codigo = N'QM11';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'No reportado'     WHERE codigo = N'QM98';
UPDATE dbo.dim_grupo_edad_madre SET etiqueta_rango = N'Sin información' WHERE codigo = N'QM99';

UPDATE dbo.dim_semanas_gestacion SET etiqueta_rango = N'5 - DE 42 O MÁS SEMANAS' WHERE codigo = N'SG05';
UPDATE dbo.dim_semanas_gestacion SET nombre_categoria = N'De 42 o más semanas'    WHERE codigo = N'SG05';
UPDATE dbo.dim_semanas_gestacion SET etiqueta_rango = N'SIN INFORMACIÓN'          WHERE codigo = N'SG99';
UPDATE dbo.dim_semanas_gestacion SET nombre_categoria = N'Sin información'        WHERE codigo = N'SG99';

UPDATE dbo.dim_peso_al_nacer SET etiqueta_rango = N'SIN INFORMACIÓN', nombre_categoria = N'Sin información' WHERE codigo = N'P99';

UPDATE dbo.dim_nivel_educativo SET etiqueta_dane = N'SIN INFORMACIÓN' WHERE etiqueta_dane LIKE N'SIN INFORM%';
UPDATE dbo.dim_pertenencia_etnica SET etiqueta_dane = N'NO REPORTADO' WHERE etiqueta_dane LIKE N'NO REPORTADO%';

/* Re-sincronizar mapeos grupo edad madre */
DELETE FROM dbo.map_grupo_edad_madre_fuente;
INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', g.etiqueta_rango, g.id_grupo_edad_madre
FROM dbo.dim_grupo_edad_madre AS g;

IF NOT EXISTS (
    SELECT 1 FROM dbo.map_grupo_edad_madre_fuente
    WHERE valor_origen = N'No Reportado' AND columna_origen = N'grupo_etareo_quinquenios_dane'
)
    INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
    SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', N'No Reportado', g.id_grupo_edad_madre
    FROM dbo.dim_grupo_edad_madre AS g WHERE g.codigo = N'QM98';

IF NOT EXISTS (
    SELECT 1 FROM dbo.map_grupo_edad_madre_fuente
    WHERE valor_origen = N'SIN INFORMACION' AND columna_origen = N'grupo_etareo_quinquenios_dane'
)
    INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
    SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', N'SIN INFORMACION', g.id_grupo_edad_madre
    FROM dbo.dim_grupo_edad_madre AS g WHERE g.codigo = N'QM99';

SELECT codigo, etiqueta_rango FROM dbo.dim_grupo_edad_madre ORDER BY orden_visualizacion;
GO

PRINT N'=== FIN 23_fix_encoding_nacimientos ===';
GO
