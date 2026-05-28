import { Component } from '@angular/core';

/** Contenedor estándar para iconos de acción en celdas de tabla */
@Component({
  selector: 'app-table-actions',
  standalone: true,
  template: `<div class="table-actions"><ng-content /></div>`
})
export class TableActionsComponent {}
