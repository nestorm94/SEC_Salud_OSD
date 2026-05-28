import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

export type IconActionVariant = 'default' | 'success' | 'danger' | 'ghost';

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
  /** Nombre del icono Material Icons (ej. `delete`, `download`) */
  readonly icon = input.required<string>();
  /** Texto para tooltip y accesibilidad */
  readonly label = input.required<string>();
  readonly variant = input<IconActionVariant>('default');
  readonly disabled = input(false);
  /** Si se define, renderiza enlace en lugar de botón */
  /** Ruta para `routerLink` (string, array o comandos con params) */
  readonly link = input<unknown>(null);
  readonly action = output<void>();
}
