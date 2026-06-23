/*
FASE 0 — Tabla map_area_residencia_fuente (ETAPA 3).
SOLO ObservatorioDB_ASIS_Test.

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\05_map_area_residencia_fuente.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar unicamente en ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== ETAPA 3 — map_area_residencia_fuente ===';
GO

IF OBJECT_ID(N'dbo.map_area_residencia_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_area_residencia_fuente (
        id_mapeo           int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla       varchar(150)  NOT NULL,
        columna_origen     varchar(150)  NOT NULL,
        valor_origen       nvarchar(300) NOT NULL,
        id_area            int           NULL,
        codigo_area        varchar(20)   NULL,
        area_normalizada   varchar(100)  NULL,
        vigente            bit           NOT NULL CONSTRAINT DF_map_area_vigente DEFAULT (1),
        fecha_creacion     datetime      NOT NULL CONSTRAINT DF_map_area_fecha DEFAULT (GETDATE()),
        CONSTRAINT PK_map_area_residencia_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_area_dim_area FOREIGN KEY (id_area)
            REFERENCES dbo.dim_area_residencia (id_area)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_area_fuente_valor
        ON dbo.map_area_residencia_fuente (fuente_tabla, columna_origen, valor_origen);

    PRINT N'Tabla map_area_residencia_fuente creada.';
END
ELSE
BEGIN
    PRINT N'Tabla map_area_residencia_fuente ya existe; repoblar equivalencias.';
    DELETE FROM dbo.map_area_residencia_fuente;
END
GO

DECLARE @Urbano int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'1');
DECLARE @RuralCentro int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'2');
DECLARE @RuralDispersa int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'3');
DECLARE @Indeterminado int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'INDETERMINADO');
DECLARE @SinInfo int = (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'SIN INFORMACION');

DECLARE @m TABLE (
    fuente_tabla   varchar(150)  NOT NULL,
    columna_origen varchar(150)  NOT NULL,
    valor_origen   nvarchar(300) NOT NULL,
    id_area        int           NULL,
    codigo_area    varchar(20)   NULL,
    area_norm      varchar(100)  NULL
);

INSERT @m (fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_norm) VALUES
/* Urbano */
(N'*', N'AREA_GEOGRAFICA', N'Cabecera', @Urbano, N'1', N'Urbano'),
(N'*', N'AREA_GEOGRAFICA', N'Cabecera Municipal', @Urbano, N'1', N'Urbano'),
(N'*', N'Area', N'Urbano', @Urbano, N'1', N'Urbano'),
(N'*', N'Area', N'Urbana', @Urbano, N'1', N'Urbano'),
(N'*', N'Area_Residencia', N'1 - CABECERA', @Urbano, N'1', N'Urbano'),
/* Rural — centro poblado */
(N'*', N'Area_Residencia', N'2 - CENTRO POBLADO', @RuralCentro, N'2', N'Rural'),
/* Rural — dispersa / agregados DANE */
(N'*', N'Area_Residencia', N'3 - AREA RURAL DISPERSA', @RuralDispersa, N'3', N'Rural'),
(N'*', N'AREA_GEOGRAFICA', N'Centros Poblados y Rural Disperso', @RuralDispersa, N'3', N'Rural'),
(N'*', N'Area', N'Rural', @RuralDispersa, N'3', N'Rural'),
(N'*', N'Area', N'Rural disperso', @RuralDispersa, N'3', N'Rural'),
/* Total — agregado, sin id_area */
(N'*', N'AREA_GEOGRAFICA', N'Total', NULL, NULL, N'Total'),
(N'*', N'Area', N'Total', NULL, NULL, N'Total'),
/* Sin información */
(N'*', N'Area_Residencia', N'SIN INFORMACION', @SinInfo, N'SIN INFORMACION', N'SIN INFORMACION'),
(N'*', N'Area_Residencia', N'SIN INFORMACIÓN', @SinInfo, N'SIN INFORMACION', N'SIN INFORMACION'),
(N'*', N'Area_Residencia', N'INDETERMINADO', @Indeterminado, N'INDETERMINADO', N'Indeterminado'),
(N'*', N'Area_Residencia', N'NO REPORTADO', @SinInfo, N'SIN INFORMACION', N'SIN INFORMACION');

INSERT dbo.map_area_residencia_fuente (
    fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_normalizada, vigente
)
SELECT fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_norm, 1
FROM @m;

PRINT N'Filas insertadas en map_area_residencia_fuente: ' + CAST(@@ROWCOUNT AS nvarchar(20));
GO

PRINT N'=== FIN 05_map_area_residencia_fuente ===';
GO
