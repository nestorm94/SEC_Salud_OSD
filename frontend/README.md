# Observatorio Salud — Frontend Angular

Aplicación Angular 19 (standalone) para el Observatorio de Salud Departamental Casanare.

## Requisitos

- Node.js 20+
- API en ejecución: `..\ejecutar-api.ps1` (puerto **5289**)

## Desarrollo

```bash
npm install
npm start
```

Abre **http://localhost:4200** — el proxy reenvía `/api` a `http://localhost:5289`.

## Login inicial

| Usuario | Contraseña |
|---------|------------|
| admin   | Admin123!  |

## Estructura

```
src/app/
├── core/          # Auth, guards, interceptor
├── layout/        # Sidebar, header, main-layout
├── modules/       # dashboard, archivos, validaciones, poblacion, administracion
├── shared/        # Modelos y componentes reutilizables
└── login/
```

## Producción

```bash
npm run build
```

Sirva `dist/frontend/browser` detrás de IIS/nginx o configure la API para servir esos estáticos.
