/* CRUD roles — ejecutar en ObservatorioDB */
SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Roles_Obtener
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Descripcion FROM dbo.Roles WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Roles_Crear
    @Nombre nvarchar(50),
    @Descripcion nvarchar(300) = NULL,
    @NuevoId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Roles (Nombre, Descripcion)
    VALUES (LTRIM(RTRIM(@Nombre)), @Descripcion);
    SET @NuevoId = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Roles_Actualizar
    @Id int,
    @Nombre nvarchar(50),
    @Descripcion nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Roles
    SET Nombre = LTRIM(RTRIM(@Nombre)), Descripcion = @Descripcion
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Roles_Eliminar
    @Id int
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.UsuarioRol WHERE RolId = @Id)
    BEGIN
        RAISERROR(N'No se puede eliminar: hay usuarios con este rol.', 16, 1);
        RETURN;
    END
    DELETE FROM dbo.Roles WHERE Id = @Id;
END
GO
