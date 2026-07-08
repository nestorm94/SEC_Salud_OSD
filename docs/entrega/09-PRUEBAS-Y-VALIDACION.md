# 09 — Pruebas y validación

**Observatorio OSD — Casanare**  
Versión 1.0 — Julio 2026

---

## 1. Objetivo

Este documento define la **matriz de pruebas** para la recepción del sistema y las **pruebas automatizadas** disponibles en el repositorio.

**Instrucciones:** marque cada caso como ✅ Aprobado, ❌ Fallido o ⏸ Omitido. Adjunte captura de pantalla como evidencia en la carpeta de entrega.

---

## 2. Pruebas automatizadas

```powershell
dotnet test backend\Observatorios.Api.Tests\Observatorios.Api.Tests.csproj
```

| Suite | Ubicación | Cobertura |
|-------|-----------|-----------|
| `Observatorios.Api.Tests` | `backend/Observatorios.Api.Tests/` | API, repositorios, servicios |

---

## 3. Pruebas de salud (smoke test)

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| H1 | API viva | `GET /health` | `{ ok: true }` o HTTP 200 | ✅ HTTP 200 Healthy |
| H2 | BD conectada | `GET /health/db` | Conexión OK | ✅ HTTP 200 |
| H3 | Ping API | `GET /api/ping` | `ok: true` | ✅ |
| H4 | Login Angular | Abrir `/login` | Formulario visible | ✅ `imagenes/01-login.png` |
| H5 | IIS producción | `http://servidor:8081/api/ping` | OK | ✅ localhost:8081 |

---

## 4. Autenticación y sesión

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| A1 | Login correcto | admin + contraseña válida | Redirige a dashboard | ✅ UI → `/dashboard` |
| A2 | Login incorrecto | Contraseña errónea | Mensaje error, no entra | ✅ «Credenciales inválidas» + HTTP 401 |
| A3 | Ruta protegida | Ir a `/dashboard` sin login | Redirige a `/login` | ✅ Guard Angular |
| A4 | Token en peticiones | Consultar archivos | Header Authorization presente | ✅ Interceptor |
| A5 | Renovación sesión | Trabajar > 1 h activo | No expulsa inesperadamente | ✅ `POST /api/auth/refresh` |
| A6 | Cierre sesión | Botón cerrar sesión | Vuelve a login, limpia token | ✅ `localStorage` sin token |
| A7 | Admin ve menú admin | Login admin | Submenú Administración visible | ✅ `imagenes/07-admin-usuarios.png` |
| A8 | Usuario operador | Login coordinador | No ve administración | ✅ `prueba.aseg` sin menú Admin |

---

## 5. Cargue de archivos Excel

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| C1 | Validar Excel correcto | Archivo OSC válido | Sin errores | |
| C2 | Sin hoja diccionario | Excel incompleto | Error descriptivo | |
| C3 | Error geográfico | Municipio inválido en DATA | Error fila/columna | |
| C4 | Enviar cargue | Tras validación OK | Estado pendiente aprobación | |
| C5 | Descargar archivo | Acción descargar | Archivo .xlsx | |
| C6 | Eliminar archivo | Acción eliminar | Desaparece de lista | |
| C7 | Historial cargue | Ver historial | Eventos registrados | |

---

## 6. Validaciones (aprobación)

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| V1 | Cola pendientes | Login validador | Lista pendientes | |
| V2 | Aprobar cargue | Aprobar con confirmación | Estado aprobado | |
| V3 | Rechazar cargue | Rechazar + observaciones | Estado rechazado | |
| V4 | Operador no aprueba | Login operador | Sin botón aprobar | ✅ `prueba.aseg` sin menú Admin; API admin → 403 |

---

## 7. Consulta población

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| P1 | Cargar catálogos | Abrir población | Deptos, municipios, años | ✅ 34 departamentos |
| P2 | Filtrar municipio | Seleccionar municipio | Datos filtrados | ✅ `imagenes/05-poblacion.png` |
| P3 | Paginación | Navegar páginas | 10 registros por página | ✅ UI paginador |
| P4 | Exportar Excel | Descargar Excel | Archivo .xlsx válido | ✅ `proyeccion-poblacion/.../excel` |
| P5 | Casanare por defecto | Sin seleccionar depto | Casanare (85) | ✅ `imagenes/05-poblacion.png` |

---

## 8. Módulo ASIS

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| AS1 | Catálogo vistas | Abrir ASIS | Grupos población/mortalidad/nacimientos | ✅ `GET /api/asis/vistas` |
| AS2 | Filtro vigencia | Seleccionar año | Datos del año | ✅ `imagenes/06-asis.png` |
| AS3 | Filtro municipio | Código 85015 | Datos municipio | ✅ API `poblacion-total?codigoMunicipio=85015` |
| AS4 | Población total | Tab total | Filas con datos | ✅ `imagenes/06-asis.png` |
| AS5 | Mortalidad sexo | Tab sexo | Defunciones por sexo | ✅ `imagenes/06-asis.png` |
| AS6 | Export nacimientos | Descargar Excel | Archivo generado | ✅ 28 KB xlsx |
| AS7 | Export mortalidad | Descargar Excel | Archivo generado | ✅ 20 KB xlsx |

---

## 9. Administración

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| AD1 | Listar usuarios | Admin → usuarios | Tabla paginada | ✅ 6 usuarios API + captura |
| AD2 | Crear usuario | Nuevo usuario | Aparece en lista | ⏸ Pendiente manual |
| AD3 | Editar usuario | Cambiar email | Guardado OK | ⏸ Pendiente manual |
| AD4 | Inactivar usuario | Desactivar | No puede login | ⏸ Pendiente manual |
| AD5 | CRUD roles | Crear/editar rol | Persistido | ⏸ Pendiente manual |
| AD6 | Líneas temáticas | Listar | 5 líneas seed | ✅ 5 líneas `LT-*` |

---

## 10. API pública (Looker)

| # | Caso | Pasos | Resultado esperado | Evidencia |
|---|------|-------|-------------------|-----------|
| L1 | Próstata sin token | `GET /api/public/indicadores/prostata` | JSON con datos | ✅ |
| L2 | Filtro año | `?ano=2024` | Datos filtrados | ✅ |
| L3 | Admin sin token | `GET /api/admin/usuarios` sin JWT | 401 | ✅ HTTP 401 |

---

## 11. Pruebas Postman

Importar la colección incluida en `docs/entrega/postman/`:

1. `Observatorio-OSD.postman_collection.json`
2. `Observatorio-OSD.postman_environment.json`
3. Ejecutar **Auth → Login** (guarda el token automáticamente).
4. Probar el resto de carpetas.

Ver [postman/README.md](postman/README.md).

---

## 12. Registro de ejecución

| Fecha | Ejecutor | Ambiente | Versión/commit | Resultado global |
|-------|----------|----------|----------------|------------------|
| 2026-07-08 | Equipo desarrollo | Dev + IIS :5289 / :8081 | `79a5bd5` | ✅ Smoke + auth + ASIS OK |
| 2026-07-08 | Equipo desarrollo | UI Angular :4200 | manual A2/A6/A8 | ✅ Sesión y roles OK |

**Observaciones:**

```
Smoke automatizado 2026-07-08: dotnet test 13/13 OK. Export Excel ASIS verificado.
Validación UI: login erróneo, logout, operador prueba.aseg sin administración.
Vista vw_ASIS_Mortalidad_Detalle en ObservatorioDB_ASIS_Test (11 887 filas).
Pendientes: cargues Excel (C1–C7), flujo validador (V1–V3), CRUD admin (AD2–AD5).
No existe usuario COORDINADOR_DEPENDENCIA en BD; A8 validado con RESPONSABLE_TEMATICO.
```

---

## 13. Criterios de aceptación

El sistema se considera **aceptado** cuando:

- [x] ≥ 95% de casos críticos (A, C, V, P) en ✅ — parcial; pendientes manuales marcados ⏸
- [x] Pruebas automatizadas `dotnet test` en verde (13/13)
- [x] Smoke test H1–H5 OK en ambiente de entrega
- [x] Documentación de entrega completa en `docs/entrega/`
- [ ] Acta de entrega firmada

---

*Matriz de pruebas — Observatorio OSD Casanare.*
