import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TABLE_PAGE_SIZE } from '../../utils/table-pagination.util';

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
  @Input({ required: true }) page!: number;
  @Input({ required: true }) totalPages!: number;
  @Input({ required: true }) totalItems!: number;
  @Input() pageSize = TABLE_PAGE_SIZE;
  @Output() pageChange = new EventEmitter<number>();

  go(p: number): void {
    if (p >= 1 && p <= this.totalPages) {
      this.pageChange.emit(p);
    }
  }
}
