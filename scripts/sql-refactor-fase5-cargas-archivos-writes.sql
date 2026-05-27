/*
FASE 5 - Escrituras transaccionales: cargas, archivos, bulk TVP.
Lecturas de listado de archivos sin TOP.
*/
SET NOCOUNT ON;
GO

/* =========================
   TIPOS TABLA (TVP)
   ========================= */
IF TYPE_ID(N'dbo.Tvp_CampoDiccionario') IS NULL
CREATE TYPE dbo.Tvp_CampoDiccionario AS TABLE
(
    NombreCampo       nvarchar(200) NOT NULL,
    TipoDato          nvarchar(50)  NOT NULL,
    Obligatorio       bit           NOT NULL,
    Descripcion       nvarchar(500) NULL,
    Longitud          int           NULL,
    Formato           nvarchar(200) NULL,
    ValoresPermitidos nvarchar(4000) NULL,
    Orden             int           NOT NULL
);
GO

IF TYPE_ID(N'dbo.Tvp_DatosCargados') IS NULL
CREATE TYPE dbo.Tvp_DatosCargados AS TABLE
(
    NumeroFila int           NOT NULL,
    DatosJson  nvarchar(max) NOT NULL
);
GO

IF TYPE_ID(N'dbo.Tvp_ErrorValidacion') IS NULL
CREATE TYPE dbo.Tvp_ErrorValidacion AS TABLE
(
    NumeroFila    int            NULL,
    NombreColumna nvarchar(200)  NULL,
    Mensaje       nvarchar(2000) NOT NULL,
    TipoError     nvarchar(100)  NULL
);
GO

/* =========================
   VISTAS: ARCHIVOS (lectura)
   ========================= */
CREATE OR ALTER VIEW dbo.vw_Archivos_Listado
AS
SELECT
    a.Id,
    a.DependenciaId,
    d.Nombre AS DependenciaNombre,
    a.LineaTematicaId,
    lt.Nombre AS LineaTematicaNombre,
    a.IndicadorId,
    ind.Nombre AS IndicadorNombre,
    a.NombreOriginal,
    a.TipoMime,
    a.TamanoBytes,
    a.CreadoEn,
    u.NombreUsuario AS SubidoPor,
    a.SubidoPorUsuarioId,
    a.Observaciones,
    a.RutaRelativa,
    a.Estado,
    a.FechaValidacion,
    a.FechaEnvio
FROM dbo.Archivos a
INNER JOIN dbo.Dependencias d ON d.Id = a.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = a.LineaTematicaId
LEFT JOIN dbo.Indicador ind ON ind.Id = a.IndicadorId
LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId;
GO

CREATE OR ALTER VIEW dbo.vw_Carga_Detalle
AS
SELECT
    c.Id,
    c.ArchivoId,
    c.DependenciaId,
    d.Nombre AS DependenciaNombre,
    c.UsuarioId,
    u.NombreUsuario,
    c.Estado,
    c.Observaciones,
    c.FechaInicio,
    c.FechaFin,
    a.NombreOriginal,
    a.RutaRelativa
FROM dbo.CargasArchivo c
INNER JOIN dbo.Dependencias d ON d.Id = c.DependenciaId
INNER JOIN dbo.Usuarios u ON u.Id = c.UsuarioId
INNER JOIN dbo.Archivos a ON a.Id = c.ArchivoId;
GO

/* =========================
   SP: ARCHIVOS (escritura + lectura)
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Archivo_Insertar
    @DependenciaId int,
    @LineaTematicaId int,
    @IndicadorId int,
    @NombreOriginal nvarchar(260),
    @NombreAlmacenado nvarchar(260),
    @RutaRelativa nvarchar(400),
    @TipoMime nvarchar(200) = NULL,
    @TamanoBytes bigint = NULL,
    @SubidoPorUsuarioId int = NULL,
    @Observaciones nvarchar(1000) = NULL,
    @Estado nvarchar(30) = N'PendienteValidacion'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Estado IS NULL OR LTRIM(RTRIM(@Estado)) = N''
        SET @Estado = N'PendienteValidacion';

    INSERT INTO dbo.Archivos
        (DependenciaId, LineaTematicaId, IndicadorId, NombreOriginal, NombreAlmacenado,
         RutaRelativa, TipoMime, TamanoBytes, SubidoPorUsuarioId, Observaciones, Estado)
    OUTPUT INSERTED.Id
    VALUES
        (@DependenciaId, @LineaTematicaId, @IndicadorId, @NombreOriginal, @NombreAlmacenado,
         @RutaRelativa, @TipoMime, @TamanoBytes, @SubidoPorUsuarioId, @Observaciones, @Estado);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_ActualizarValidacion
    @Id int,
    @Estado nvarchar(30),
    @ErroresValidacionJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Archivos
    SET Estado = @Estado,
        FechaValidacion = SYSUTCDATETIME(),
        ErroresValidacionJson = @ErroresValidacionJson
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_MarcarEnviado
    @Id int,
    @Estado nvarchar(30) = N'ENVIADO'
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Archivos
    SET Estado = @Estado,
        FechaEnvio = SYSUTCDATETIME()
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_ObtenerEstado
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Estado, SubidoPorUsuarioId, DependenciaId, RutaRelativa, NombreOriginal,
           LineaTematicaId, IndicadorId, Observaciones
    FROM dbo.Archivos
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_Obtener
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.Id, a.DependenciaId, a.LineaTematicaId, a.IndicadorId,
        a.NombreOriginal, a.NombreAlmacenado, a.RutaRelativa, a.TipoMime, a.TamanoBytes,
        a.CreadoEn, a.Observaciones, lt.Nombre AS LineaTematicaNombre, ind.Nombre AS IndicadorNombre,
        u.NombreUsuario AS SubidoPor, a.SubidoPorUsuarioId,
        a.Estado, a.FechaValidacion, a.FechaEnvio, a.ErroresValidacionJson
    FROM dbo.Archivos a
    LEFT JOIN dbo.LineaTematica lt ON lt.Id = a.LineaTematicaId
    LEFT JOIN dbo.Indicador ind ON ind.Id = a.IndicadorId
    LEFT JOIN dbo.Usuarios u ON u.Id = a.SubidoPorUsuarioId
    WHERE a.Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_Listar
    @DependenciaId int = NULL,
    @LineaTematicaId int = NULL,
    @SubidoPorUsuarioId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, DependenciaId, DependenciaNombre,
        LineaTematicaId, LineaTematicaNombre,
        IndicadorId, IndicadorNombre,
        NombreOriginal, TipoMime, TamanoBytes, CreadoEn, SubidoPor,
        Observaciones, RutaRelativa, Estado, FechaValidacion, FechaEnvio
    FROM dbo.vw_Archivos_Listado v
    WHERE (@DependenciaId IS NULL OR v.DependenciaId = @DependenciaId)
      AND (@LineaTematicaId IS NULL OR v.LineaTematicaId = @LineaTematicaId)
      AND (@SubidoPorUsuarioId IS NULL OR v.SubidoPorUsuarioId = @SubidoPorUsuarioId)
    ORDER BY v.Id DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Archivo_Eliminar
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Archivos WHERE Id = @Id;
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO

/* =========================
   SP: CARGAS (escritura + lectura puntual)
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Carga_Crear
    @ArchivoId int,
    @DependenciaId int,
    @UsuarioId int,
    @Estado nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CargasArchivo (ArchivoId, DependenciaId, UsuarioId, Estado)
    OUTPUT INSERTED.Id
    VALUES (@ArchivoId, @DependenciaId, @UsuarioId, @Estado);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_ActualizarEstado
    @Id int,
    @Estado nvarchar(50),
    @Observaciones nvarchar(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CargasArchivo
    SET Estado = @Estado,
        Observaciones = COALESCE(@Observaciones, Observaciones),
        FechaFin = CASE
            WHEN @Estado IN (N'VALIDADO_OK', N'VALIDADO_EXITOSO', N'VALIDADO_CON_ERRORES',
                             N'APROBADO', N'RECHAZADO', N'CARGADO_BD')
            THEN SYSUTCDATETIME()
            ELSE FechaFin
        END
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_RegistrarHistorial
    @CargaId int,
    @UsuarioId int = NULL,
    @Accion nvarchar(100),
    @Detalle nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.HistorialCarga (CargaArchivoId, UsuarioId, Accion, Detalle)
    VALUES (@CargaId, @UsuarioId, @Accion, @Detalle);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_Obtener
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, ArchivoId, DependenciaId, DependenciaNombre, UsuarioId, NombreUsuario,
           Estado, Observaciones, FechaInicio, FechaFin, NombreOriginal, RutaRelativa
    FROM dbo.vw_Carga_Detalle
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_Errores_Listar
    @CargaId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, NumeroFila, NombreColumna, Mensaje, TipoError
    FROM dbo.ErroresValidacion
    WHERE CargaArchivoId = @CargaId
    ORDER BY COALESCE(NumeroFila, 0), Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_LimpiarResultadosValidacion
    @CargaId int
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRAN;

        DELETE FROM dbo.DatosCargados WHERE CargaArchivoId = @CargaId;
        DELETE FROM dbo.ErroresValidacion WHERE CargaArchivoId = @CargaId;
        DELETE cd
        FROM dbo.CamposDiccionario cd
        INNER JOIN dbo.DiccionarioArchivo da ON da.Id = cd.DiccionarioArchivoId
        WHERE da.CargaArchivoId = @CargaId;
        DELETE FROM dbo.DiccionarioArchivo WHERE CargaArchivoId = @CargaId;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_GuardarDiccionario
    @CargaId int,
    @Campos dbo.Tvp_CampoDiccionario READONLY,
    @DiccionarioId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.DiccionarioArchivo (CargaArchivoId)
        VALUES (@CargaId);

        SET @DiccionarioId = SCOPE_IDENTITY();

        INSERT INTO dbo.CamposDiccionario
            (DiccionarioArchivoId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden)
        SELECT @DiccionarioId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden
        FROM @Campos;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_GuardarDatosBulk
    @CargaId int,
    @Filas dbo.Tvp_DatosCargados READONLY
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM @Filas) RETURN;

    INSERT INTO dbo.DatosCargados (CargaArchivoId, NumeroFila, DatosJson)
    SELECT @CargaId, NumeroFila, DatosJson
    FROM @Filas;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Carga_GuardarErroresBulk
    @CargaId int,
    @Errores dbo.Tvp_ErrorValidacion READONLY
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM @Errores) RETURN;

    INSERT INTO dbo.ErroresValidacion (CargaArchivoId, NumeroFila, NombreColumna, Mensaje, TipoError)
    SELECT @CargaId, NumeroFila, NombreColumna, Mensaje, TipoError
    FROM @Errores;
END;
GO
