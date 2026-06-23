/*
Orquestador normalizacion poblacion con version DANE.
Opcion A: recarga completa de la misma id_proyeccion_dane (DELETE + INSERT).
SOLO ObservatorioDB_ASIS_Test.

Ejemplo:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\11_usp_normalizar_poblacion_todo.sql
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

CREATE OR ALTER PROCEDURE dbo.usp_ASIS_Normalizar_Poblacion_Todo
    @nombre_proyeccion varchar(150),
    @anio_publicacion  int,
    @fuente            varchar(200) = NULL,
    @descripcion       varchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
    BEGIN
        RAISERROR(N'Solo ObservatorioDB_ASIS_Test.', 16, 1);
        RETURN;
    END

    DECLARE @id_proyeccion_dane int;

    EXEC dbo.usp_ASIS_CrearProyeccionDANE
        @nombre_proyeccion = @nombre_proyeccion,
        @anio_publicacion = @anio_publicacion,
        @fuente = @fuente,
        @descripcion = @descripcion,
        @id_proyeccion_dane = @id_proyeccion_dane OUTPUT;

    PRINT N'=== usp_ASIS_Normalizar_Poblacion_Todo id_proyeccion_dane='
        + CAST(@id_proyeccion_dane AS nvarchar(20)) + N' ===';

    /* Opcion A: recarga misma proyeccion (no afecta otras vigencias) */
    DECLARE @filasPrevias int = (
        SELECT COUNT(*) FROM dbo.fact_poblacion_proyeccion
        WHERE id_proyeccion_dane = @id_proyeccion_dane
    );
    IF @filasPrevias > 0
    BEGIN
        DELETE FROM dbo.fact_poblacion_proyeccion
        WHERE id_proyeccion_dane = @id_proyeccion_dane;
        PRINT N'Opcion A: eliminadas ' + CAST(@filasPrevias AS nvarchar(20))
            + N' filas previas de la misma proyeccion.';
    END

    EXEC dbo.usp_ASIS_Normalizar_Poblacion_Nacional @id_proyeccion_dane = @id_proyeccion_dane;
    EXEC dbo.usp_ASIS_Normalizar_Poblacion_Departamental @id_proyeccion_dane = @id_proyeccion_dane;
    EXEC dbo.usp_ASIS_Normalizar_Poblacion_Municipal @id_proyeccion_dane = @id_proyeccion_dane;

    SELECT @id_proyeccion_dane AS id_proyeccion_dane,
           @nombre_proyeccion AS nombre_proyeccion,
           @anio_publicacion AS anio_publicacion,
           COUNT(*) AS filas_cargadas
    FROM dbo.fact_poblacion_proyeccion
    WHERE id_proyeccion_dane = @id_proyeccion_dane;

    PRINT N'=== FIN Normalizar_Poblacion_Todo ===';
END
GO

PRINT N'11_usp_normalizar_poblacion_todo.sql OK';
GO

/* Ejemplo de carga (descomentar para ejecutar):
EXEC dbo.usp_ASIS_Normalizar_Poblacion_Todo
    @nombre_proyeccion = N'Proyeccion DANE 2025',
    @anio_publicacion = 2025,
    @fuente = N'DANE PPED staging',
    @descripcion = N'Carga normalizada desde tablas fuente actuales';
GO
*/
