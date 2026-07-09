import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { TABLE_PAGE_SIZE, paginateSlice, computeTotalPages } from '../../shared/utils/table-pagination.util';
import { IndicadorProstataDto } from '../../shared/models/api.models';
import { ProstataService } from './prostata.service';

/**
 * Visualización del indicador de mortalidad por cáncer de próstata en Casanare.
 * Carga el dataset completo una vez y aplica filtros y paginación en el cliente.
 */
@Component({
  selector: 'app-prostata',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, LoadingStateComponent, TablePaginatorComponent],
  templateUrl: './prostata.component.html',
  styleUrl: './prostata.component.scss'
})
export class ProstataComponent implements OnInit {
  private readonly prostataService = inject(ProstataService);

  private readonly datosCompletos = signal<IndicadorProstataDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly fuente = signal('');

  pagina = 1;
  readonly tamanoPagina = TABLE_PAGE_SIZE;

  filtroAnio = '2024';
  filtroRegional = '';
  filtroArea = '';
  filtroTerritorio = '';
  filtroCodigoDane = '';

  readonly regionales = ['Centro', 'Norte', 'Sur', 'No aplica'];
  readonly areas = ['Total', 'Urbana', 'Rural disperso'];
  readonly anios = Array.from({ length: 2025 - 2005 + 1 }, (_, i) => String(2025 - i));

  /** Territorios únicos derivados de los datos cargados, ordenados alfabéticamente. */
  readonly territorios = computed(() => {
    const seen = new Set<string>();
    const list: string[] = [];
    for (const r of this.datosCompletos()) {
      const t = r.territorio?.trim();
      if (t && !seen.has(t)) {
        seen.add(t);
        list.push(t);
      }
    }
    return list.sort((a, b) => a.localeCompare(b, 'es'));
  });

  /** Filtrado client-side: todas las condiciones activas deben cumplirse (AND). */
  readonly filas = computed(() => {
    const anio = this.filtroAnio ? Number(this.filtroAnio) : null;
    const regional = this.filtroRegional.trim();
    const area = this.filtroArea.trim();
    const territorio = this.filtroTerritorio.trim();
    const codigo = this.filtroCodigoDane.trim();

    return this.datosCompletos().filter((r) => {
      if (anio != null && r.anio !== anio) return false;
      if (regional && r.regional !== regional) return false;
      if (area && r.area !== area) return false;
      if (territorio && r.territorio !== territorio) return false;
      if (codigo && r.codigoDane !== codigo) return false;
      return true;
    });
  });

  readonly filasPagina = computed(() => paginateSlice(this.filas(), this.pagina, this.tamanoPagina));
  readonly totalPaginas = computed(() => computeTotalPages(this.filas().length, this.tamanoPagina));

  ngOnInit(): void {
    this.cargarDatos();
  }

  /** Obtiene hasta 20 000 registros del indicador para filtrar localmente. */
  cargarDatos(): void {
    this.loading.set(true);
    this.error.set('');

    this.prostataService.consultar({ limit: 20000 }).subscribe({
      next: (res) => {
        this.datosCompletos.set(res.datos || []);
        this.fuente.set(res.fuente || '');
        this.pagina = 1;
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'Error al consultar mortalidad por próstata.');
        this.datosCompletos.set([]);
        this.loading.set(false);
      }
    });
  }

  /** Reinicia la paginación al aplicar filtros (los computed recalculan automáticamente). */
  aplicarFiltros(): void {
    this.pagina = 1;
  }

  /** Limpia todos los filtros y vuelve a la primera página. */
  limpiarFiltros(): void {
    this.filtroAnio = '';
    this.filtroRegional = '';
    this.filtroArea = '';
    this.filtroTerritorio = '';
    this.filtroCodigoDane = '';
    this.pagina = 1;
  }

  /**
   * Cambia la página visible sin nueva petición HTTP.
   * @param p Número de página destino.
   */
  irAPagina(p: number): void {
    if (p === this.pagina) return;
    this.pagina = p;
  }

  /**
   * Formatea números con locale colombiano.
   * @param v Valor numérico o nulo.
   * @param decimales Cantidad de decimales a mostrar.
   * @returns Cadena formateada o vacía si no hay valor.
   */
  formatNum(v: number | null | undefined, decimales = 0): string {
    if (v == null || Number.isNaN(v)) return '';
    return new Intl.NumberFormat('es-CO', {
      minimumFractionDigits: decimales,
      maximumFractionDigits: decimales
    }).format(v);
  }
}
