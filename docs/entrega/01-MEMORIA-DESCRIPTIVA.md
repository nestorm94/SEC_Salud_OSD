# 01 — Memoria descriptiva

**Observatorio de Salud Departamental — Casanare (OSD)**  
Versión 1.0 — Julio 2026

---

## 1. Introducción

La **Secretaría de Salud Departamental de Casanare** requiere una plataforma tecnológica que centralice la **carga, validación y consulta** de información del Observatorio de Salud Departamental (OSD), alineada con el modelo de áreas temáticas y con fuentes oficiales del DANE y del sector salud.

El sistema **Observatorios Salud Departamental Casanare** es una aplicación web que permite a las dependencias territoriales cargar archivos Excel según plantillas oficiales, someterlos a validación técnica, aprobarlos institucionalmente y consultar indicadores de **población, mortalidad, nacimientos** y otros módulos analíticos.

---

## 2. Problema que resuelve

Antes de esta solución, el manejo de información del observatorio presentaba retos como:

- Cargas de archivos sin trazabilidad centralizada ni control por dependencia.
- Validación manual de estructura Excel propensa a errores.
- Consultas de población dispersas en vistas SQL sin interfaz unificada.
- Ausencia de roles diferenciados (operador, validador, administrador).
- Dificultad para alimentar tableros externos (Looker Studio) con datos estandarizados.

---

## 3. Objetivos del sistema

| Objetivo | Descripción |
|----------|-------------|
| **Centralizar cargues** | Un solo portal para dependencias con flujo Validar → Enviar → Aprobar |
| **Garantizar calidad** | Validación automática contra diccionario de datos y reglas geográficas DANE |
| **Control de acceso** | Usuarios por dependencia, roles y permisos granulares |
| **Consultar indicadores** | Población, ASIS departamental, mortalidad por próstata, exportación Excel |
| **Trazabilidad** | Historial de cargues, errores de validación y auditoría administrativa |
| **Integración** | Endpoints públicos para Looker Studio / Google Sheets (indicadores seleccionados) |

---

## 4. Alcance funcional entregado

### 4.1 Módulos operativos

| Módulo | Funcionalidad |
|--------|---------------|
| **Autenticación** | Login JWT, renovación automática de sesión, cierre por inactividad |
| **Dashboard** | Resumen de cargues, pendientes, errores y actividad reciente |
| **Archivos / Cargues** | Subida Excel OSC, validación, envío, descarga, eliminación |
| **Validaciones** | Cola de aprobación para validadores y administradores |
| **Proyección población** | Consulta paginada con filtros DANE y exportación Excel |
| **ASIS departamental** | Población, mortalidad, nacimientos e indicadores derivados |
| **Indicador próstata** | Consulta interna y endpoint público para dashboards |
| **Administración** | Usuarios, roles, dependencias, líneas temáticas, indicadores, plantillas |

### 4.2 Fuera de alcance actual (pendiente o futuro)

- Generación automática del documento Word/PDF del ASIS departamental completo.
- Notificaciones por correo electrónico en cargues/aprobaciones.
- Migración total de población a capa `fact` en producción (en laboratorio ASIS Test).
- App móvil nativa.

---

## 5. Usuarios del sistema

| Perfil | Descripción |
|--------|-------------|
| **Administrador (ADMIN)** | Gestión total: usuarios, catálogos, aprobaciones, auditoría |
| **Validador (VALIDADOR)** | Revisa y aprueba/rechaza cargues de todas las dependencias |
| **Coordinador de dependencia** | Carga archivos de su dependencia |
| **Responsable temático** | Carga en líneas/áreas asignadas |
| **Consulta (CONSULTA)** | Solo lectura de indicadores |
| **Auditor (AUDITOR)** | Consulta de trazabilidad y log de auditoría |

---

## 6. Modelo de negocio

```
Dependencia
  └── Línea temática (ASEG, ECNT, VSP, ETC, ECON, …)
        └── Indicador
              └── Plantilla de carga (Excel OSC)
                    └── Archivo → Validación → Aprobación → Datos
```

Cada archivo Excel debe contener obligatoriamente las hojas **Diccionario_datos** y **DATA**, según plantilla OSC V.2.

---

## 7. Arquitectura resumida

- **Frontend:** Angular 19 (interfaz principal) + portal HTML legacy en transición.
- **Backend:** ASP.NET Core minimal API (.NET).
- **Base de datos:** SQL Server (`ObservatorioDB`).
- **Despliegue:** IIS en Windows Server (puerto 8081) o desarrollo local (5289/4200).
- **Seguridad:** JWT + BCrypt; endpoints públicos solo para indicadores de consulta.

Ver detalle en [05-ARQUITECTURA.md](05-ARQUITECTURA.md).

---

## 8. Fuentes de datos

| Fuente | Uso |
|--------|-----|
| Cargues Excel OSC | Indicadores por dependencia y línea temática |
| Vistas DANE población | Proyección nacional, departamental, municipal |
| `fact_defunciones_casanare_normalizada` | Mortalidad ASIS |
| `fact_nacimientos_casanare_normalizada` | Nacimientos ASIS (ambiente de pruebas) |
| Catálogos `dim_*` | Departamentos, municipios, sexo, área, edad, etc. |

---

## 9. Beneficios para la Secretaría

1. **Estandarización** de cargues con reglas automáticas y diccionario de datos.
2. **Transparencia** en el flujo de aprobación y historial de cambios.
3. **Consulta unificada** de población y ASIS sin depender de herramientas externas para operación diaria.
4. **Escalabilidad** mediante vistas SQL y procedimientos almacenados.
5. **Base para tableros** institucionales vía endpoints públicos y exportación Excel.

---

## 10. Entregables

| Entregable | Formato |
|------------|---------|
| Código fuente | Repositorio Git |
| Base de datos | Scripts SQL + instancia `ObservatorioDB` |
| Documentación | Carpeta `docs/entrega/` |
| Manual de usuario | [02-MANUAL-USUARIO.md](02-MANUAL-USUARIO.md) |
| Manual técnico | [04-MANUAL-TECNICO.md](04-MANUAL-TECNICO.md) |
| Acta de entrega | [11-ACTA-ENTREGA.md](11-ACTA-ENTREGA.md) |

---

## 11. Requisitos de infraestructura

| Componente | Requisito mínimo |
|------------|------------------|
| SO | Windows Server 2019+ o Windows 10/11 (desarrollo) |
| IIS | 10+ con ASP.NET Core Hosting Bundle |
| SQL Server | 2019 / 2022 |
| .NET | Runtime ASP.NET Core correspondiente al proyecto |
| Navegador | Chrome, Edge o Firefox actualizado |

---

## 12. Contacto y soporte

Completar en el acta de entrega:

| Rol | Nombre | Contacto |
|-----|--------|----------|
| Responsable proyecto OSD | _Pendiente_ | |
| Soporte técnico | _Pendiente_ | |
| Administrador de base de datos | _Pendiente_ | |

---

*Documento elaborado como parte de la entrega final del Observatorio de Salud Departamental Casanare.*
