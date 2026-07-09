import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';

interface Crumb {
  label: string;
  url?: string;
}

const LABELS: Record<string, string> = {
  dashboard: 'Panel principal',
  archivos: 'Archivos',
  validaciones: 'Validaciones',
  poblacion: 'Proyección población',
  prostata: 'Mortalidad próstata',
  asis: 'ASIS Departamental',
  administracion: 'Administración',
  usuarios: 'Usuarios',
  nuevo: 'Nuevo',
  roles: 'Roles',
  dependencias: 'Dependencias',
  plantillas: 'Plantillas',
  campos: 'Campos'
};

/**
 * Migas de pan derivadas de la URL activa.
 * Muestra la jerarquía de navegación solo cuando hay más de un nivel.
 */
@Component({
  selector: 'app-breadcrumbs',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (crumbs().length > 1) {
      <nav class="osd-breadcrumbs" aria-label="Ruta de navegación">
        @for (c of crumbs(); track c.label; let last = $last) {
          @if (!last && c.url) {
            <a [routerLink]="c.url">{{ c.label }}</a>
            <span class="osd-breadcrumbs__sep" aria-hidden="true">/</span>
          } @else {
            <span class="osd-breadcrumbs__current">{{ c.label }}</span>
          }
        }
      </nav>
    }
  `
})
export class BreadcrumbsComponent {
  private readonly router = inject(Router);

  /** Lista reactiva de segmentos de ruta con etiquetas legibles. */
  readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.buildCrumbs())
    ),
    { initialValue: this.buildCrumbs() }
  );

  /** Construye la cadena de migas a partir de los segmentos de la URL actual. */
  private buildCrumbs(): Crumb[] {
    const url = this.router.url.split('?')[0];
    const segments = url.split('/').filter(Boolean);
    const crumbs: Crumb[] = [{ label: 'Inicio', url: '/dashboard' }];
    let path = '';

    for (let i = 0; i < segments.length; i++) {
      const seg = segments[i];
      path += `/${seg}`;
      const isId = /^\d+$/.test(seg);
      const label = isId ? `Detalle #${seg}` : LABELS[seg] ?? seg;
      const isLast = i === segments.length - 1;
      crumbs.push({ label, url: isLast ? undefined : path });
    }

    return crumbs;
  }
}
