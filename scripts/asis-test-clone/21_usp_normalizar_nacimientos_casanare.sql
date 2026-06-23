/*
Normaliza staging -> fact_nacimientos_casanare_normalizada.
Ejecutar despues de cargar nacimientos_casanare_staging (script load-nacimientos-csv.ps1).

  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\21_usp_normalizar_nacimientos_casanare.sql
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

CREATE OR ALTER PROCEDURE dbo.usp_normalizar_nacimientos_casanare
    @reemplazar bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF OBJECT_ID(N'dbo.nacimientos_casanare_staging', N'U') IS NULL
    BEGIN
        RAISERROR(N'Falta tabla nacimientos_casanare_staging. Ejecute load-nacimientos-csv.ps1', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    IF @reemplazar = 1
        TRUNCATE TABLE dbo.fact_nacimientos_casanare_normalizada;

    EXEC dbo.usp_sync_catalogos_nacimientos_staging;

    DECLARE @idAreaSinInfo int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'SIN INFORMACION');
    DECLARE @idNivelSin int = (SELECT TOP 1 id_nivel_educativo FROM dbo.dim_nivel_educativo WHERE etiqueta_dane LIKE N'SIN INFORM%');
    DECLARE @idEtniaNr int = (SELECT TOP 1 id_pertenencia_etnica FROM dbo.dim_pertenencia_etnica WHERE etiqueta_dane LIKE N'NO REPORTADO%');

    ;WITH src AS (
        SELECT
            RIGHT(N'00' + LTRIM(RTRIM(s.codigo_departamento)), 2) AS codigo_departamento,
            NULLIF(LTRIM(RTRIM(s.codigo_municipio)), N'') AS codigo_municipio,
            s.vigencia AS anio,
            s.nacimientos AS numero_nacimientos,
            s.sexo,
            s.nombre_area_residencia,
            s.codigo_area_residencia,
            s.grupo_etareo_quinquenios_dane,
            s.nivel_educativo,
            s.pertenencia_etnica,
            s.peso_al_nacer,
            s.semanas_gestacion
        FROM dbo.nacimientos_casanare_staging AS s
        WHERE s.vigencia IS NOT NULL
          AND s.nacimientos IS NOT NULL
          AND s.nacimientos > 0
    ),
    resolved AS (
        SELECT
            src.codigo_departamento,
            src.codigo_municipio,
            sx.id_sexo,
            COALESCE(ma.id_area, ma2.id_area, @idAreaSinInfo) AS id_area,
            COALESCE(mg.id_grupo_edad_madre, mg2.id_grupo_edad_madre, mg3.id_grupo_edad_madre, mg4.id_grupo_edad_madre, mg5.id_grupo_edad_madre) AS id_grupo_edad_madre,
            COALESCE(mne.id_nivel_educativo, mne2.id_nivel_educativo, mne3.id_nivel_educativo, @idNivelSin) AS id_nivel_educativo,
            COALESCE(mpe.id_pertenencia_etnica, mpe2.id_pertenencia_etnica, mpe3.id_pertenencia_etnica, @idEtniaNr) AS id_pertenencia_etnica,
            mp.id_peso_al_nacer,
            ms.id_semanas_gestacion,
            src.anio,
            src.numero_nacimientos
        FROM src
        INNER JOIN dbo.dim_sexo AS sx
            ON sx.sexo = src.sexo
           AND sx.sexo IN (N'FEMENINO', N'MASCULINO', N'INDETERMINADO')
        LEFT JOIN dbo.map_area_residencia_fuente AS ma
            ON ma.fuente_tabla = N'nacimientos_casanare'
           AND ma.columna_origen = N'nombre_area_residencia'
           AND ma.valor_origen = src.nombre_area_residencia
           AND ma.vigente = 1
        LEFT JOIN dbo.dim_area_residencia AS ma2
            ON ma2.codigo_area = LTRIM(RTRIM(src.codigo_area_residencia))
           AND ma2.estado = 1
        LEFT JOIN dbo.map_grupo_edad_madre_fuente AS mg
            ON mg.fuente_tabla = N'nacimientos_casanare'
           AND mg.columna_origen = N'grupo_etareo_quinquenios_dane'
           AND mg.valor_origen = src.grupo_etareo_quinquenios_dane
           AND mg.vigente = 1
        LEFT JOIN dbo.dim_grupo_edad_madre AS mg2
            ON mg2.etiqueta_rango = src.grupo_etareo_quinquenios_dane
        LEFT JOIN dbo.dim_grupo_edad_madre AS mg3
            ON mg3.edad_minima IS NOT NULL
           AND src.grupo_etareo_quinquenios_dane LIKE N'De ' + CAST(mg3.edad_minima AS nvarchar(10)) + N' %'
        LEFT JOIN dbo.dim_grupo_edad_madre AS mg4
            ON src.grupo_etareo_quinquenios_dane LIKE N'60%'
           AND mg4.codigo = N'QM11'
        LEFT JOIN dbo.dim_grupo_edad_madre AS mg5
            ON UPPER(LTRIM(RTRIM(src.grupo_etareo_quinquenios_dane))) IN (N'NO REPORTADO', N'NO REPORTADA')
           AND mg5.codigo = N'QM98'
        LEFT JOIN dbo.map_nivel_educativo_fuente AS mne
            ON mne.fuente_tabla = N'nacimientos_casanare'
           AND mne.columna_origen = N'nivel_educativo'
           AND mne.valor_origen = src.nivel_educativo
           AND mne.vigente = 1
        LEFT JOIN dbo.dim_nivel_educativo AS mne2
            ON mne2.etiqueta_dane = src.nivel_educativo
        LEFT JOIN dbo.dim_nivel_educativo AS mne3
            ON src.nivel_educativo LIKE mne3.codigo_dane + N' %'
           AND mne3.codigo_dane NOT IN (N'SIN', N'NR')
        LEFT JOIN dbo.map_pertenencia_etnica_fuente AS mpe
            ON mpe.fuente_tabla = N'nacimientos_casanare'
           AND mpe.columna_origen = N'pertenencia_etnica'
           AND mpe.valor_origen = src.pertenencia_etnica
           AND mpe.vigente = 1
        LEFT JOIN dbo.dim_pertenencia_etnica AS mpe2
            ON mpe2.etiqueta_dane = src.pertenencia_etnica
        LEFT JOIN dbo.dim_pertenencia_etnica AS mpe3
            ON src.pertenencia_etnica LIKE mpe3.codigo_dane + N' %'
           AND mpe3.codigo_dane NOT IN (N'NR')
        LEFT JOIN dbo.map_peso_al_nacer_fuente AS mp
            ON mp.fuente_tabla = N'nacimientos_casanare'
           AND mp.columna_origen = N'peso_al_nacer'
           AND mp.valor_origen = src.peso_al_nacer
           AND mp.vigente = 1
        LEFT JOIN dbo.map_semanas_gestacion_fuente AS ms
            ON ms.fuente_tabla = N'nacimientos_casanare'
           AND ms.columna_origen = N'semanas_gestacion'
           AND ms.valor_origen = src.semanas_gestacion
           AND ms.vigente = 1
    )
    INSERT INTO dbo.fact_nacimientos_casanare_normalizada (
        codigo_departamento, codigo_municipio, id_sexo, id_area, id_grupo_edad_madre,
        id_nivel_educativo, id_pertenencia_etnica,
        id_peso_al_nacer, id_semanas_gestacion, anio, numero_nacimientos, fecha_carga
    )
    SELECT
        r.codigo_departamento,
        r.codigo_municipio,
        r.id_sexo,
        r.id_area,
        r.id_grupo_edad_madre,
        r.id_nivel_educativo,
        r.id_pertenencia_etnica,
        r.id_peso_al_nacer,
        r.id_semanas_gestacion,
        r.anio,
        r.numero_nacimientos,
        SYSDATETIME()
    FROM resolved AS r
    WHERE r.id_grupo_edad_madre IS NOT NULL;

    DECLARE @ins int = @@ROWCOUNT;
    DECLARE @staging int = (SELECT COUNT(*) FROM dbo.nacimientos_casanare_staging WHERE vigencia IS NOT NULL);
    DECLARE @sin_area int = (
        SELECT COUNT(*) FROM dbo.nacimientos_casanare_staging AS s
        WHERE s.vigencia IS NOT NULL AND s.nacimientos > 0
          AND NOT EXISTS (
              SELECT 1 FROM dbo.map_area_residencia_fuente AS m
              WHERE m.valor_origen = s.nombre_area_residencia AND m.fuente_tabla = N'nacimientos_casanare'
          )
          AND NOT EXISTS (
              SELECT 1 FROM dbo.dim_area_residencia AS a WHERE a.codigo_area = LTRIM(RTRIM(s.codigo_area_residencia))
          )
    );

    COMMIT TRANSACTION;

    SELECT @ins AS filas_insertadas, @staging AS filas_staging, @sin_area AS filas_sin_area_mapeada;
END
GO

PRINT N'=== FIN 21_usp_normalizar_nacimientos_casanare ===';
GO
