/*
FASE 2 - Usuarios, roles y dependencias.
Vistas (vw_) para lecturas reutilizables; procedimientos (usp_) para filtros y escrituras.
*/
SET NOCOUNT ON;
GO

/* ========================= VISTAS ========================= */

CREATE OR ALTER VIEW dbo.vw_Usuarios_Auth
AS
SELECT
    u.Id,
    u.DependenciaId,
    u.LineaTematicaId,
    u.NombreUsuario,
    u.Email,
    u.PasswordHash,
    u.Activo,
    d.Nombre AS DependenciaNombre,
    lt.Nombre AS LineaTematicaNombre
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId;
GO

CREATE OR ALTER VIEW dbo.vw_Usuarios_Listado
AS
SELECT
    u.Id,
    u.NombreUsuario,
    u.Email,
    u.Activo,
    u.DependenciaId,
    d.Nombre AS DependenciaNombre,
    u.LineaTematicaId,
    lt.Nombre AS LineaTematicaNombre,
    (
        SELECT STRING_AGG(r.Nombre, N',') WITHIN GROUP (ORDER BY r.Nombre)
        FROM dbo.UsuarioRol ur
        INNER JOIN dbo.Roles r ON r.Id = ur.RolId
        WHERE ur.UsuarioId = u.Id
    ) AS RolesCsv
FROM dbo.Usuarios u
LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId;
GO

CREATE OR ALTER VIEW dbo.vw_Roles_Listado
AS
SELECT Id, Nombre, Descripcion
FROM dbo.Roles;
GO

CREATE OR ALTER VIEW dbo.vw_Dependencias_Listado
AS
SELECT Id, Codigo, Nombre, Activo, CreadoEn
FROM dbo.Dependencias;
GO

/* ========================= USUARIOS: LECTURA ========================= */

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ObtenerPorNombre
    @NombreUsuario nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, DependenciaId, LineaTematicaId, NombreUsuario, Email,
        PasswordHash, Activo, DependenciaNombre, LineaTematicaNombre
    FROM dbo.vw_Usuarios_Auth
    WHERE NombreUsuario = @NombreUsuario;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ObtenerPorEmail
    @Email nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, DependenciaId, LineaTematicaId, NombreUsuario, Email,
        PasswordHash, Activo, DependenciaNombre, LineaTematicaNombre
    FROM dbo.vw_Usuarios_Auth
    WHERE Email = @Email;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ObtenerPorId
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.Id,
        u.NombreUsuario,
        u.Email,
        u.Activo,
        u.DependenciaId,
        d.Nombre AS DependenciaNombre,
        u.LineaTematicaId,
        lt.Nombre AS LineaTematicaNombre,
        (
            SELECT STRING_AGG(r.Nombre, N',') WITHIN GROUP (ORDER BY r.Nombre)
            FROM dbo.UsuarioRol ur
            INNER JOIN dbo.Roles r ON r.Id = ur.RolId
            WHERE ur.UsuarioId = u.Id
        ) AS RolesCsv
    FROM dbo.Usuarios u
    LEFT JOIN dbo.Dependencias d ON d.Id = u.DependenciaId
    LEFT JOIN dbo.LineaTematica lt ON lt.Id = u.LineaTematicaId
    WHERE u.Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id,
        NombreUsuario,
        Email,
        Activo,
        DependenciaId,
        DependenciaNombre,
        LineaTematicaId,
        LineaTematicaNombre,
        RolesCsv
    FROM dbo.vw_Usuarios_Listado
    ORDER BY NombreUsuario;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ObtenerRoles
    @UsuarioId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Nombre
    FROM dbo.UsuarioRol ur
    INNER JOIN dbo.Roles r ON r.Id = ur.RolId
    WHERE ur.UsuarioId = @UsuarioId
    ORDER BY r.Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ObtenerAreasTematicas
    @UsuarioId int
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID(N'dbo.UsuarioAreaTematica', N'U') IS NULL
    BEGIN
        SELECT CAST(NULL AS int) AS AreaTematicaId WHERE 1 = 0;
        RETURN;
    END;

    SELECT AreaTematicaId
    FROM dbo.UsuarioAreaTematica
    WHERE UsuarioId = @UsuarioId
    ORDER BY AreaTematicaId;
END
GO

/* ========================= USUARIOS: ESCRITURA ========================= */

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_Crear
    @DependenciaId int = NULL,
    @LineaTematicaId int = NULL,
    @NombreUsuario nvarchar(100),
    @Email nvarchar(256) = NULL,
    @PasswordHash nvarchar(200),
    @RolesCsv nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Id int;

    INSERT INTO dbo.Usuarios (DependenciaId, LineaTematicaId, NombreUsuario, Email, PasswordHash, Activo)
    VALUES (@DependenciaId, @LineaTematicaId, @NombreUsuario, @Email, @PasswordHash, 1);

    SET @Id = SCOPE_IDENTITY();

    IF NULLIF(LTRIM(RTRIM(@RolesCsv)), N'') IS NOT NULL
    BEGIN
        INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
        SELECT @Id, r.Id
        FROM STRING_SPLIT(@RolesCsv, N',') s
        INNER JOIN dbo.Roles r ON r.Nombre = LTRIM(RTRIM(s.value))
        WHERE LTRIM(RTRIM(s.value)) <> N'';
    END;

    COMMIT TRAN;

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_Actualizar
    @Id int,
    @Email nvarchar(256) = NULL,
    @DependenciaId int = NULL,
    @LineaTematicaId int = NULL,
    @PasswordHash nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @PasswordHash IS NOT NULL AND LTRIM(RTRIM(@PasswordHash)) <> N''
    BEGIN
        UPDATE dbo.Usuarios
        SET Email = @Email,
            DependenciaId = @DependenciaId,
            LineaTematicaId = @LineaTematicaId,
            PasswordHash = @PasswordHash
        WHERE Id = @Id;
    END
    ELSE
    BEGIN
        UPDATE dbo.Usuarios
        SET Email = @Email,
            DependenciaId = @DependenciaId,
            LineaTematicaId = @LineaTematicaId
        WHERE Id = @Id;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_SetActivo
    @Id int,
    @Activo bit
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Usuarios SET Activo = @Activo WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ActualizarRoles
    @UsuarioId int,
    @RolesCsv nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DELETE FROM dbo.UsuarioRol WHERE UsuarioId = @UsuarioId;

    IF NULLIF(LTRIM(RTRIM(@RolesCsv)), N'') IS NOT NULL
    BEGIN
        INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
        SELECT @UsuarioId, r.Id
        FROM STRING_SPLIT(@RolesCsv, N',') s
        INNER JOIN dbo.Roles r ON r.Nombre = LTRIM(RTRIM(s.value))
        WHERE LTRIM(RTRIM(s.value)) <> N'';
    END;

    COMMIT TRAN;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Usuario_ActualizarAreasTematicas
    @UsuarioId int,
    @AreaIdsCsv nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'dbo.UsuarioAreaTematica', N'U') IS NULL
        RETURN;

    SET XACT_ABORT ON;
    BEGIN TRAN;

    DELETE FROM dbo.UsuarioAreaTematica WHERE UsuarioId = @UsuarioId;

    IF NULLIF(LTRIM(RTRIM(@AreaIdsCsv)), N'') IS NOT NULL
    BEGIN
        INSERT INTO dbo.UsuarioAreaTematica (UsuarioId, AreaTematicaId)
        SELECT @UsuarioId, TRY_CONVERT(int, LTRIM(RTRIM(s.value)))
        FROM STRING_SPLIT(@AreaIdsCsv, N',') s
        WHERE TRY_CONVERT(int, LTRIM(RTRIM(s.value))) IS NOT NULL;
    END;

    COMMIT TRAN;
END
GO

/* ========================= ROLES ========================= */

CREATE OR ALTER PROCEDURE dbo.usp_Roles_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Descripcion
    FROM dbo.vw_Roles_Listado
    ORDER BY Nombre;
END
GO

/* ========================= DEPENDENCIAS ========================= */

CREATE OR ALTER PROCEDURE dbo.usp_Dependencia_Listar
    @SoloActivas bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Codigo, Nombre, Activo, CreadoEn
    FROM dbo.vw_Dependencias_Listado
    WHERE (@SoloActivas = 0 OR Activo = 1)
    ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dependencia_ObtenerPorId
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Codigo, Nombre, Activo, CreadoEn
    FROM dbo.vw_Dependencias_Listado
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dependencia_Crear
    @Codigo nvarchar(50),
    @Nombre nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Dependencias (Codigo, Nombre)
    OUTPUT INSERTED.Id
    VALUES (UPPER(LTRIM(RTRIM(@Codigo))), LTRIM(RTRIM(@Nombre)));
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dependencia_ObtenerOCrear
    @Codigo nvarchar(50),
    @Nombre nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @c nvarchar(50) = UPPER(LTRIM(RTRIM(@Codigo)));
    DECLARE @n nvarchar(200) = LTRIM(RTRIM(@Nombre));
    IF @n = N'' SET @n = @c;

    DECLARE @Id int;
    SELECT @Id = Id FROM dbo.Dependencias WHERE Codigo = @c;

    IF @Id IS NULL
    BEGIN
        INSERT INTO dbo.Dependencias (Codigo, Nombre)
        VALUES (@c, @n);
        SET @Id = SCOPE_IDENTITY();
    END;

    SELECT @Id AS Id;
END
GO
