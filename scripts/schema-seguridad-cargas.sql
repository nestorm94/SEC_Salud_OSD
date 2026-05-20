-- Esquema completo: seguridad, archivos por dependencia y cargas Excel validadas.
-- Ejecutar en SSMS en el mismo servidor que appsettings.json (p. ej. (localdb)\MSSQLLocalDB).

USE ObservatorioDB;
GO

-- La API también ejecuta este esquema al arrancar (ObservatorioDbSchema).
-- Tablas: Dependencias, Roles, Usuarios, UsuarioRol, Archivos, CargasArchivo,
--         DiccionarioArchivo, CamposDiccionario, DatosCargados, ErroresValidacion, HistorialCarga

PRINT 'Use ejecutar-api.ps1 para aplicar/migrar el esquema automáticamente.';
GO
