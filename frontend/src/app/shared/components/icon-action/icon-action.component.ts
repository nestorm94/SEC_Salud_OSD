import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Variante visual del botón o enlace de icono. */
export type IconActionVariant = 'default' | 'success' | 'danger' | 'ghost';

/**
 * Botón o enlace compacto con icono Material Icons.
 * Renderiza enlace cuando se provee `link`; de lo contrario, botón con evento `action`.
 */
@Component({
  selector: 'app-icon-action',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (link(); as route) {
      <a
        class="icon-btn"
        [class.icon-btn--success]="variant() === 'success'"
        [class.icon-btn--danger]="variant() === 'danger'"
        [class.icon-btn--ghost]="variant() === 'ghost'"
        [routerLink]="$any(route)"
        [title]="label()"
        [attr.aria-label]="label()"
      >
        <span class="material-icons" aria-hidden="true">{{ icon() }}</span>
      </a>
    } @else {
      <button
        type="button"
        class="icon-btn"
        [class.icon-btn--success]="variant() === 'success'"
        [class.icon-btn--danger]="variant() === 'danger'"
        [class.icon-btn--ghost]="variant() === 'ghost'"
        [disabled]="disabled()"
        [title]="label()"
        [attr.aria-label]="label()"
        (click)="action.emit()"
      >
        <span class="material-icons" aria-hidden="true">{{ icon() }}</span>
      </button>
    }
  `
})
export class IconActionComponent {
  /** Nombre del icono Material Icons (ej. `delete`, `download`). */
  readonly icon = input.required<string>();
  /** Texto para tooltip y accesibilidad. */
  readonly label = input.required<string>();
  /** Variante de color o estilo del control. */
  readonly variant = input<IconActionVariant>('default');
  /** Deshabilita el botón cuando no hay enlace. */
  readonly disabled = input(false);
  /** Ruta para `routerLink`; si se define, renderiza enlace en lugar de botón. */
  readonly link = input<unknown>(null);
  /** Emite al pulsar el botón (no aplica cuando hay `link`). */
  readonly action = output<void>();
}
