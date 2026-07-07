# 10 — Operación y soporte

**Observatorio OSD — Casanare**  
Versión 1.0 — Julio 2026

---

## 1. Responsabilidades operativas

| Rol | Responsabilidad |
|-----|-----------------|
| **Administrador aplicación** | Usuarios, roles, catálogos |
| **DBA** | Backups SQL, scripts, rendimiento |
| **Infraestructura** | IIS, certificados, firewall |
| **Soporte N1** | Incidencias de usuario (login, cargues) |
| **Soporte N2** | Errores API, BD, despliegues |

---

## 2. Servicios Windows / IIS

| Componente | Nombre | Puerto |
|------------|--------|--------|
| Sitio IIS | `ObservatorioOSD` | 8081 |
| App Pool | `ObservatorioOSDPool` | — |
| Ruta física | `C:\Hosting\ObservatorioOSD` | — |

### Comandos útiles

```powershell
# Reciclar aplicación
.\scripts\reciclar-sitio-iis.ps1

# Republicar
.\scripts\publicar-iis.ps1
```

---

## 3. Monitoreo

### Endpoints de salud

| URL | Uso |
|-----|-----|
| `http://servidor:8081/health` | Monitoreo uptime |
| `http://servidor:8081/health/db` | Alerta si SQL cae |

### Qué revisar diariamente

- [ ] `/health` responde 200
- [ ] `/health/db` responde OK
- [ ] Espacio en disco (`uploads/`, logs IIS)
- [ ] App pool en estado **Started**

### Logs

| Origen | Ubicación típica |
|--------|------------------|
| IIS stdout | `C:\Hosting\ObservatorioOSD\logs\` |
| Event Viewer | Windows Logs → Application |
| SQL Server | Error log de instancia |

---

## 4. Backups

### Base de datos

```powershell
# Script incluido para clon ASIS (adaptable a prod)
sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\00_backup_observatoriodb.sql
```

**Recomendación:**

| Tipo | Frecuencia | Retención |
|------|------------|-----------|
| Completo | Diario | 30 días |
| Antes de despliegue | Siempre | Hasta validar |
| Antes de script SQL | Siempre | 7 días |

### Archivos cargados

Respaldar carpeta `uploads/` junto con la BD (rutas en tabla `Archivos`).

---

## 5. Restauración

### Base de datos

1. Detener sitio IIS (`app_offline.htm` o stop site).
2. `RESTORE DATABASE ObservatorioDB FROM DISK = '...' WITH REPLACE`.
3. Verificar `grant-iis-observatorio-sql.sql` si cambió el pool.
4. Iniciar sitio y probar `/health/db`.

### Aplicación

1. Restaurar carpeta `C:\Hosting\ObservatorioOSD` desde backup.
2. Reciclar app pool.

---

## 6. Mantenimiento programado

| Tarea | Frecuencia |
|-------|------------|
| Backup BD + uploads | Diario |
| Revisar usuarios activos | Mensual |
| Actualizar catálogos DANE | Según publicación DANE |
| Revisar espacio `uploads/` | Semanal |
| Probar restore de backup | Trimestral |
| Rotar logs IIS | Según política TI |

---

## 7. Despliegue de actualizaciones

Ver [04-MANUAL-TECNICO.md](04-MANUAL-TECNICO.md) §11.

**Checklist rápido:**

1. ☐ Backup BD
2. ☐ Backup `uploads/`
3. ☐ Ejecutar SQL nuevo (si aplica)
4. ☐ `publicar-iis.ps1`
5. ☐ Reciclar pool
6. ☐ Smoke test H1–H5
7. ☐ Login + consulta población

---

## 8. Gestión de incidentes

### Nivel 1 — Usuario

| Síntoma | Acción |
|---------|--------|
| No puede entrar | Verificar usuario activo; reset contraseña |
| Excel con errores | Revisar plantilla OSC; corregir y revalidar |
| Sesión expiró | Volver a iniciar sesión |

### Nivel 2 — Técnico

| Síntoma | Acción |
|---------|--------|
| 502 en consultas | Revisar SQL Server, vistas `vw_*` |
| 401 masivo | Verificar `Jwt:Key`, reloj servidor |
| Sitio no carga | App pool, Hosting Bundle .NET |
| Disco lleno | Limpiar logs; archivar `uploads/` antiguos |

### Escalamiento

Completar contactos en acta de entrega:

| Nivel | Contacto | Teléfono |
|-------|----------|----------|
| Soporte aplicación | | |
| DBA | | |
| Infraestructura | | |

---

## 9. Limpieza de datos de prueba

Script manual (solo entornos de prueba):

```powershell
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB -E -i scripts\limpiar-cargues-y-archivos.sql
```

> **No ejecutar en producción** sin autorización y backup.

---

## 10. Capacidad y rendimiento

| Consulta pesada | Mitigación |
|-----------------|------------|
| Proyección población legacy | Caché en API; filtros obligatorios |
| ASIS pirámide poblacional | Paginación; export bajo demanda |
| Listado cargues | Paginación 10 registros |

Si el rendimiento degrada: revisar índices SQL, estadísticas y planes de ejecución en vistas `vw_Reporte_*`.

---

## 11. Continuidad del negocio

| Escenario | RTO sugerido | RPO sugerido |
|-----------|--------------|--------------|
| Caída IIS | < 1 hora | 0 (redeploy) |
| Caída SQL | < 4 horas | 24 h (backup diario) |
| Pérdida uploads | < 4 horas | 24 h |

Ajustar según política institucional de la Secretaría.

---

*Operación y soporte — Observatorio OSD Casanare.*
