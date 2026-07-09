import { Component } from '@angular/core';

/**
 * Contenedor estándar para iconos de acción en celdas de tabla.
 * Agrupa botones o enlaces compactos con espaciado uniforme.
 */
@Component({
  selector: 'app-table-actions',
  standalone: true,
  template: `<div class="table-actions"><ng-content /></div>`
})
export class TableActionsComponent {}
