import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Diálogo modal accesible con cabecera, cuerpo y pie proyectables.
 * Se cierra al pulsar el botón de cierre o al hacer clic en el overlay.
 */
@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (open()) {
      <div class="osd-modal-overlay" (click)="close.emit()" role="presentation">
        <div
          class="osd-modal-panel"
          [class.osd-modal-panel--wide]="wide()"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          (click)="$event.stopPropagation()"
        >
          <header class="osd-modal-panel__header">
            <h2 class="osd-modal-panel__title" [id]="titleId">{{ title() }}</h2>
            <button type="button" class="icon-btn icon-btn--ghost" (click)="close.emit()" aria-label="Cerrar">
              <span class="material-icons" aria-hidden="true">close</span>
            </button>
          </header>
          <div class="osd-modal-panel__body">
            <ng-content />
          </div>
          <footer class="osd-modal-panel__footer">
            <ng-content select="[modalFooter]" />
          </footer>
        </div>
      </div>
    }
  `
})
export class ModalComponent {
  /** Controla la visibilidad del modal. */
  readonly open = input.required<boolean>();
  /** Título mostrado en la cabecera del diálogo. */
  readonly title = input.required<string>();
  /** Si es verdadero, amplía el ancho del panel. */
  readonly wide = input(false);
  /** Emite al solicitar el cierre del modal. */
  readonly close = output<void>();

  /** Identificador único para asociar `aria-labelledby` con el título. */
  readonly titleId = `osd-modal-${Math.random().toString(36).slice(2, 9)}`;
}
