# Sistema de diseño — Observatorio de Salud OSD

UI institucional tipo dashboard analítico (Power BI / ADRES). Estilos globales en SCSS; **sin dependencia de `@angular/material` en runtime** (preparado para migración). Iconos: fuente [Material Icons](https://fonts.google.com/icons).

## Estructura SCSS

| Archivo | Contenido |
|---------|-----------|
| `src/styles/_variables.scss` | Paleta, tipografía, espaciado, sombras, CSS vars |
| `src/styles/_mixins.scss` | Cards, botones, tablas, flex, breakpoints |
| `src/styles/_theme.scss` | Tokens compatibles con Material (futuro) |
| `src/styles/_layout.scss` | `.page-container`, `.page-header`, breadcrumbs |
| `src/styles/_components.scss` | Cards, botones, formularios, tablas, tabs, paginador |
| `src/styles/_sidebar.scss` | Navegación lateral |
| `src/styles/_header.scss` | Barra superior |
| `src/styles/_login.scss` | Pantalla de acceso |
| `src/styles.scss` | Reset global e imports |

## Marca (logos)

Archivos en `frontend/public/branding/`:

| Archivo | Uso |
|---------|-----|
| `logo-horizontal.png` | Sidebar, header móvil |
| `logo-stacked.png` | Login, favicon |

Constantes: `src/app/shared/branding.ts`

## Paleta

| Token | Valor |
|-------|-------|
| Azul oscuro | `#0B1F3A` |
| Verde institucional (logo) | `#1F7A3A` |
| Rojo logo | `#E31E24` |
| Amarillo logo | `#F5C400` |
| Fondo | `#F4F7FA` |
| Bordes | `#DDE6F0` |
| Texto | `#1E293B` |

## Clases reutilizables

- **Layout:** `.page-container`, `.page-header`, `.filter-card`, `.content-card`, `.table-card`
- **Datos:** `.stat-card`, `.grid-cards`, `.responsive-table`, `.responsive-table-wrap`
- **Formularios:** `.form-grid`, `.filtros-grid`, `.form-group`, `.form-actions`
- **Botones:** `.btn`, `.btn-primary`, `.btn-secondary`, `.btn-success`, `.btn-danger`, `.btn-ghost`
- **Tabs:** `.osd-tabs`, `.tab-btn`
- **Estados:** `.alert`, `.hint`, `.empty-state`, `app-status-badge`
- **Carga:** `app-loading-state`

## Layout global

- `MainLayoutComponent`: sidebar + header + breadcrumbs + `router-outlet`
- Sidebar colapsable en móvil (backdrop + menú hamburguesa)
- Contenido centrado con `max-width: 1440px`

## Acciones en tablas (transversal)

```html
<td class="actions" data-label="Acciones">
  <app-table-actions>
    <app-icon-action icon="download" label="Descargar" (action)="..." />
    <app-icon-action icon="delete" label="Eliminar" variant="danger" (action)="..." />
    <app-icon-action icon="edit" label="Editar" [link]="['/ruta', id]" />
  </app-table-actions>
</td>
```

Variantes: `default` | `success` | `danger` | `ghost`

## Componentes compartidos

- `app-icon-action` — botón o enlace con icono Material
- `app-table-actions` — contenedor de acciones en tabla
- `app-page-header` — título, subtítulo y acciones
- `app-breadcrumbs` — migas de pan automáticas por ruta
- `app-loading-state` — spinner institucional
- `app-status-badge` — estados de cargas/validaciones

## Migrar a Angular Material (opcional)

Cuando la red permita instalar paquetes:

```bash
cd frontend
npx ng add @angular/material --defaults --skip-confirmation
```

Luego personalizar tema en `src/styles/_material-theme.scss` con los colores de `_variables.scss` y sustituir gradualmente `<select>` / `<button>` nativos por `mat-form-field`, `mat-select`, `mat-button`.

## Build

```bash
npm run build
npm start
```
