import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { DashboardService } from './dashboard.service';
import { DashboardResumen } from '../../shared/models/api.models';
import { mapHttpErrorMessage } from '../../core/utils/http-error.util';
import { tablePagination } from '../../shared/utils/table-pagination.state';

/**
 * Panel principal del OSD con indicadores de cargas y listado paginado de actividad reciente.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    LoadingStateComponent,
    TablePaginatorComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);

  loading = signal(true);
  error = signal('');
  resumen = signal<DashboardResumen | null>(null);

  readonly ultimos = computed(() => this.resumen()?.ultimos_cargues ?? []);
  readonly pag = tablePagination(this.ultimos);

  ngOnInit(): void {
    this.cargarResumen();
  }

  /** Obtiene el resumen del dashboard y reinicia la paginación de la tabla. */
  cargarResumen(): void {
    this.loading.set(true);
    this.error.set('');
    this.dashboardService.getResumen().subscribe({
      next: (r) => {
        this.resumen.set(r);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'No se pudo cargar el resumen.'));
        this.loading.set(false);
      }
    });
  }

  /**
   * Traduce el origen técnico del registro a etiqueta legible en la UI.
   * @param origen Valor devuelto por la API ("Archivo" u otro).
   * @returns Etiqueta "Prevalidación" o "Cargue".
   */
  etiquetaOrigen(origen?: string): string {
    return origen === 'Archivo' ? 'Prevalidación' : 'Cargue';
  }
}
