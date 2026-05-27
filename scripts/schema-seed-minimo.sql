/*
Datos de referencia mínimos (roles y dependencia base).
Ejecutar después de schema-bootstrap.sql y antes o después de las fases de SP.
*/
SET NOCOUNT ON;
GO

MERGE dbo.Roles AS t USING (VALUES
 (N'ADMIN', N'Acceso total al observatorio'),
 (N'COORDINADOR_DEPENDENCIA', N'Gestión de su dependencia'),
 (N'RESPONSABLE_TEMATICO', N'Carga en áreas asignadas'),
 (N'VALIDADOR', N'Revisión de cargues'),
 (N'CONSULTA', N'Solo lectura'),
 (N'AUDITOR', N'Trazabilidad y auditoría'),
 (N'Administrador', N'Alias ADMIN'),
 (N'Operador', N'Alias operador legacy')
) AS s(Nombre, Descripcion) ON t.Nombre = s.Nombre
WHEN NOT MATCHED THEN INSERT (Nombre, Descripcion) VALUES (s.Nombre, s.Descripcion);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Dependencias WHERE Codigo = N'CAS-SALUD')
    INSERT INTO dbo.Dependencias (Codigo, Nombre)
    VALUES (N'CAS-SALUD', N'Secretaría de Salud — Casanare');
GO
