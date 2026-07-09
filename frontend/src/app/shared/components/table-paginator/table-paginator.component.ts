import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TABLE_PAGE_SIZE } from '../../utils/table-pagination.util';

/**
 * Controles de paginación para tablas con datos en cliente.
 * Muestra navegación anterior o siguiente y un resumen de la página actual.
 */
@Component({
  selector: 'app-table-paginator',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (totalItems > pageSize) {
      <div class="osd-paginator paginacion" role="navigation" aria-label="Paginación de tabla">
        <button
          type="button"
          class="btn-secondary btn-sm"
          [disabled]="page <= 1"
          (click)="go(page - 1)"
        >
          Anterior
        </button>
        <span>Página {{ page }} de {{ totalPages }} · {{ totalItems }} registro{{ totalItems === 1 ? '' : 's' }}</span>
        <button
          type="button"
          class="btn-secondary btn-sm"
          [disabled]="page >= totalPages"
          (click)="go(page + 1)"
        >
          Siguiente
        </button>
      </div>
    }
  `
})
export class TablePaginatorComponent {
  /** Número de página actual (base 1). */
  @Input({ required: true }) page!: number;
  /** Total de páginas calculado a partir del número de registros. */
  @Input({ required: true }) totalPages!: number;
  /** Cantidad total de registros en la lista. */
  @Input({ required: true }) totalItems!: number;
  /** Tamaño de página; por defecto usa el valor global de tablas. */
  @Input() pageSize = TABLE_PAGE_SIZE;
  /** Emite el nuevo número de página al navegar. */
  @Output() pageChange = new EventEmitter<number>();

  /**
   * Navega a la página indicada si está dentro del rango válido.
   * @param p Número de página destino.
   */
  go(p: number): void {
    if (p >= 1 && p <= this.totalPages) {
      this.pageChange.emit(p);
    }
  }
}
