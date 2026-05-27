# Scripts SQL — Refactor Observatorio OSD

Ejecutar en **orden** sobre la base `ObservatorioDB`:

| Orden | Archivo | Contenido |
|-------|---------|-----------|
| 1 | `sql-refactor-fase1-vistas-sp.sql` | Cargas, historial, indicador próstata |
| 2 | `sql-refactor-fase2-usuarios-roles-dependencias.sql` | Usuarios, roles, dependencias |
| 3 | `sql-refactor-fase3-dashboard.sql` | Dashboard KPIs + últimos cargues |
| 4 | `sql-refactor-fase4-proyeccion-catalogos.sql` | Catálogos y proyección paginada |
| 5 | `sql-refactor-fase5-cargas-archivos-writes.sql` | TVP, escrituras cargas/archivos |

```powershell
$S = "localhost\SQLEXPRESS2025"
$D = "ObservatorioDB"

sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase1-vistas-sp.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase2-usuarios-roles-dependencias.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase3-dashboard.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase4-proyeccion-catalogos.sql"
sqlcmd -S $S -d $D -E -b -i "sql-refactor-fase5-cargas-archivos-writes.sql"
```

**Documentación completa:** [../docs/SQL-SERVER-CATALOGO-OBJETOS.md](../docs/SQL-SERVER-CATALOGO-OBJETOS.md)
