/*
================================================================================
 02_validacion_restore_observatoriodb_asis_test.sql
================================================================================
 PROPÓSITO:
   Compara ObservatorioDB_ASIS_Test contra ObservatorioDB: conteo de tablas,
   filas en dimensiones/hechos clave y ejecución TOP 1 de vistas principales.
   Solo lectura; no modifica ninguna base.

 BASE DE DATOS DESTINO:
   Consulta cruzada ObservatorioDB y ObservatorioDB_ASIS_Test (misma instancia).

 DEPENDENCIAS (ejecutar antes):
   - 00_backup_observatoriodb.sql
   - 01_restore_observatoriodb_asis_test.sql

 ORDEN DE EJECUCIÓN:
   00 -> 01 -> 02 (este script) -> 04_normalizacion...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\02_validacion_restore_observatoriodb_asis_test.sql
================================================================================
*/
SET NOCOUNT ON;
GO

DECLARE @DbOriginal sysname = N'ObservatorioDB';
DECLARE @DbTest sysname = N'ObservatorioDB_ASIS_Test';

PRINT N'=== VALIDACION CLON ASIS TEST ===';
PRINT N'Original: ' + @DbOriginal;
PRINT N'Prueba:   ' + @DbTest;

IF DB_ID(@DbOriginal) IS NULL
BEGIN
    RAISERROR(N'Falta base original %s', 16, 1, @DbOriginal);
    RETURN;
END

IF DB_ID(@DbTest) IS NULL
BEGIN
    RAISERROR(N'Falta base de prueba %s. Ejecute 01_restore_observatoriodb_asis_test.sql', 16, 1, @DbTest);
    RETURN;
END

DECLARE @tabOrig int, @tabTest int;
DECLARE @sql nvarchar(max);

SET @sql = N'SELECT @c = COUNT(*) FROM [' + @DbOriginal + N'].sys.tables WHERE is_ms_shipped = 0';
EXEC sp_executesql @sql, N'@c int OUTPUT', @c = @tabOrig OUTPUT;
SET @sql = N'SELECT @c = COUNT(*) FROM [' + @DbTest + N'].sys.tables WHERE is_ms_shipped = 0';
EXEC sp_executesql @sql, N'@c int OUTPUT', @c = @tabTest OUTPUT;

PRINT N'';
PRINT N'--- Tablas (usuario) ---';
PRINT N'Original: ' + CAST(@tabOrig AS nvarchar(20));
PRINT N'Prueba:   ' + CAST(@tabTest AS nvarchar(20));
PRINT CASE WHEN @tabOrig = @tabTest THEN N'OK: mismo número de tablas' ELSE N'DIFERENCIA en conteo de tablas' END;

PRINT N'';
PRINT N'--- Conteos tablas principales ---';

DECLARE @objetos TABLE (
    objeto sysname NOT NULL,
    origen bigint NULL,
    prueba bigint NULL
);

/* --- Tablas de referencia para comparar conteos original vs clon --- */
INSERT @objetos (objeto) VALUES
(N'dim_departamento'),
(N'dim_municipio'),
(N'dim_sexo'),
(N'dim_area_residencia'),
(N'dim_grupo_edad'),
(N'dim_curso_vida'),
(N'fact_defunciones_casanare_normalizada');

DECLARE @obj sysname, @cntOrig bigint, @cntTest bigint;

DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT objeto FROM @objetos;
OPEN c;
FETCH NEXT FROM c INTO @obj;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @cntOrig = NULL;
    SET @cntTest = NULL;
    SET @sql = N'IF OBJECT_ID(N''' + @DbOriginal + N'.dbo.' + @obj + N''', N''U'') IS NOT NULL
        SELECT @n = COUNT_BIG(1) FROM [' + @DbOriginal + N'].dbo.[' + @obj + N']';
    BEGIN TRY
        EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @cntOrig OUTPUT;
    END TRY
    BEGIN CATCH
        SET @cntOrig = NULL;
    END CATCH

    SET @sql = N'IF OBJECT_ID(N''' + @DbTest + N'.dbo.' + @obj + N''', N''U'') IS NOT NULL
        SELECT @n = COUNT_BIG(1) FROM [' + @DbTest + N'].dbo.[' + @obj + N']';
    BEGIN TRY
        EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @cntTest OUTPUT;
    END TRY
    BEGIN CATCH
        SET @cntTest = NULL;
    END CATCH

    UPDATE @objetos SET origen = @cntOrig, prueba = @cntTest WHERE objeto = @obj;
    FETCH NEXT FROM c INTO @obj;
END
CLOSE c;
DEALLOCATE c;

SELECT
    objeto,
    origen AS conteo_original,
    prueba AS conteo_prueba,
    CASE
        WHEN origen IS NULL OR prueba IS NULL THEN N'FALTA OBJETO'
        WHEN origen = prueba THEN N'OK'
        ELSE N'DIFERENCIA'
    END AS resultado
FROM @objetos
ORDER BY objeto;

PRINT N'';
PRINT N'--- Vistas principales (TOP 1) ---';

DECLARE @vistas TABLE (vista sysname NOT NULL, ok_origen bit NULL, ok_prueba bit NULL);
/* --- Vistas API: probar SELECT TOP 1 en ambas bases --- */
INSERT @vistas (vista) VALUES
(N'vw_Poblacion_Nacional_Casanare'),
(N'vw_Reporte_Poblacion_CursoVida_Unificado'),
(N'vw_Reporte_Poblacion_Quinquenios_Unificado'),
(N'vw_Defunciones_Casanare_Por_Sexo'),
(N'vw_Defunciones_Casanare_Por_Curso_Vida'),
(N'vw_Defunciones_Casanare_Por_Area');

DECLARE @v sysname, @okO bit, @okT bit, @probe int;
DECLARE cv CURSOR LOCAL FAST_FORWARD FOR SELECT vista FROM @vistas;
OPEN cv;
FETCH NEXT FROM cv INTO @v;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @okO = 0;
    SET @okT = 0;

    IF OBJECT_ID(QUOTENAME(@DbOriginal) + N'.dbo.' + QUOTENAME(@v), N'V') IS NOT NULL
    BEGIN
        SET @sql = N'SELECT TOP (1) @x = 1 FROM ' + QUOTENAME(@DbOriginal) + N'.dbo.' + QUOTENAME(@v);
        BEGIN TRY
            EXEC sp_executesql @sql, N'@x int OUTPUT', @x = @probe OUTPUT;
            SET @okO = 1;
        END TRY
        BEGIN CATCH
            SET @okO = 0;
        END CATCH
    END

    IF OBJECT_ID(QUOTENAME(@DbTest) + N'.dbo.' + QUOTENAME(@v), N'V') IS NOT NULL
    BEGIN
        SET @sql = N'SELECT TOP (1) @x = 1 FROM ' + QUOTENAME(@DbTest) + N'.dbo.' + QUOTENAME(@v);
        BEGIN TRY
            EXEC sp_executesql @sql, N'@x int OUTPUT', @x = @probe OUTPUT;
            SET @okT = 1;
        END TRY
        BEGIN CATCH
            SET @okT = 0;
        END CATCH
    END

    UPDATE @vistas SET ok_origen = @okO, ok_prueba = @okT WHERE vista = @v;
    FETCH NEXT FROM cv INTO @v;
END
CLOSE cv;
DEALLOCATE cv;

SELECT
    vista,
    CASE ok_origen WHEN 1 THEN N'OK' ELSE N'ERROR' END AS original,
    CASE ok_prueba WHEN 1 THEN N'OK' ELSE N'ERROR' END AS prueba
FROM @vistas
ORDER BY vista;

PRINT N'';
PRINT N'=== FIN VALIDACION ===';
GO
