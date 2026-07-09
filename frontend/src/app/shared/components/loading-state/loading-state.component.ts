import { Component, Input } from '@angular/core';

/**
 * Indicador de carga reutilizable con mensaje configurable.
 * Incluye spinner animado y atributos de accesibilidad.
 */
@Component({
  selector: 'app-loading-state',
  standalone: true,
  template: `
    <div class="loading" role="status" aria-live="polite">
      <span class="osd-spinner" aria-hidden="true"></span>
      <span>{{ message }}</span>
    </div>
  `,
  styles: `
    .osd-spinner {
      display: inline-block;
      width: 40px;
      height: 40px;
      border: 3px solid #dde6f0;
      border-top-color: #0b1f3a;
      border-radius: 50%;
      animation: osd-spin 0.75s linear infinite;
    }
    @keyframes osd-spin {
      to {
        transform: rotate(360deg);
      }
    }
  `
})
export class LoadingStateComponent {
  /** Texto mostrado junto al indicador de progreso. */
  @Input() message = 'Cargando...';
}
