# Scripts SQL — Refactor Observatorio OSD

Ejecutar en **orden** sobre la base `ObservatorioDB`:

| Orden | Archivo | Contenido |
|-------|---------|-----------|
| 0 | `schema-bootstrap.sql` | DDL tablas base (idempotente) |
| 0b | `schema-seed-minimo.sql` | Roles y dependencia CAS-SALUD |
| 1 | `sql-refactor-fase1-vistas-sp.sql` | Cargas, historial, indicador próstata |
| 2 | `sql-refactor-fase2-usuarios-roles-dependencias.sql` | Usuarios, roles, dependencias |
| 3 | `sql-refactor-fase3-dashboard.sql` | Dashboard KPIs + últimos cargues |
| 4 | `sql-refactor-fase4-proyeccion-catalogos.sql` | Catálogos y proyección paginada |
| 5 | `sql-refactor-fase5-cargas-archivos-writes.sql` | TVP, escrituras cargas/archivos |
| 6 | `sql-refactor-fase6-admin-catalogos.sql` | Admin: líneas, indicadores, plantillas, áreas, auditoría, ArchivoCarga |

```powershell
$S = "localhost\SQLEXPRESS2025"
$D = "ObservatorioDB"

sqlcmd -S $S -d $D -E -b -i "schema-bootstrap.sql"
sqlcmd -S $S -d $D -E -b -i "schema-seed-minimo.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase1-vistas-sp.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase2-usuarios-roles-dependencias.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase3-dashboard.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase4-proyeccion-catalogos.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase5-cargas-archivos-writes.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase6-admin-catalogos.sql"
```

**Documentación completa:** [../docs/SQL-SERVER-CATALOGO-OBJETOS.md](../docs/SQL-SERVER-CATALOGO-OBJETOS.md)
