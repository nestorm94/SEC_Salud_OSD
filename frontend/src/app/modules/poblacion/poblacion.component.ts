import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { TABLE_PAGE_SIZE } from '../../shared/utils/table-pagination.util';
import { PoblacionService, VistaPoblacion } from './poblacion.service';
import { CatalogoSimpleDto, DepartamentoDto, MunicipioDto, ProyeccionResponse } from '../../shared/models/api.models';
import { CatalogoService } from '../../core/services/catalogo.service';

/**
 * Consulta de proyección poblacional del OSD con tres vistas tabulares
 * y filtros territoriales/demográficos con paginación server-side.
 */
@Component({
  selector: 'app-poblacion',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, LoadingStateComponent, TablePaginatorComponent],
  templateUrl: './poblacion.component.html',
  styleUrl: './poblacion.component.scss'
})
export class PoblacionComponent implements OnInit {
  private readonly poblacionService = inject(PoblacionService);
  private readonly catalogoService = inject(CatalogoService);

  tabActiva = signal<VistaPoblacion>('nacional-casanare');
  datos = signal<ProyeccionResponse | null>(null);
  loading = signal(false);
  error = signal('');

  pagina = 1;
  readonly tamanoPagina = TABLE_PAGE_SIZE;

  departamentos: DepartamentoDto[] = [];
  municipios: MunicipioDto[] = [];
  regionales: CatalogoSimpleDto[] = [];
  areas: CatalogoSimpleDto[] = [];
  sexos: CatalogoSimpleDto[] = [];
  anios: CatalogoSimpleDto[] = [];
  catalogosError = signal('');

  /** Valores vacíos en filtros significan "Todos" en la consulta a la API. */
  filtroDepartamento = '';
  filtroMunicipio = '';
  filtroRegional = '';
  filtroArea = '';
  filtroSexo = '';
  filtroAnio = '';

  readonly tabs: { clave: VistaPoblacion; label: string }[] = [
    { clave: 'nacional-casanare', label: 'Nacional / Casanare' },
    { clave: 'curso-vida', label: 'Curso de vida' },
    { clave: 'quinquenios', label: 'Quinquenios' }
  ];

  /**
   * Cambia la vista activa y reinicia la paginación.
   * @param clave Identificador de la pestaña seleccionada.
   */
  cambiarTab(clave: VistaPoblacion): void {
    this.tabActiva.set(clave);
    this.pagina = 1;
    this.consultar();
  }

  /** Carga catálogos de filtros en paralelo; acumula errores parciales sin bloquear la consulta. */
  private cargarCatalogos(): void {
    const errores: string[] = [];

    const onError = (nombre: string) => () => {
      errores.push(nombre);
      this.catalogosError.set(
        `No se pudieron cargar algunos filtros (${errores.join(', ')}). Recargue la página o contacte al administrador.`
      );
    };

    this.catalogoService.getDepartamentos().subscribe({
      next: ({ departamentos }) => (this.departamentos = departamentos || []),
      error: onError('departamentos')
    });
    this.catalogoService.getRegionales().subscribe({
      next: ({ regionales }) => (this.regionales = regionales || []),
      error: onError('regionales')
    });
    this.catalogoService.getAreas().subscribe({
      next: ({ areas }) => (this.areas = areas || []),
      error: onError('áreas')
    });
    this.catalogoService.getSexos().subscribe({
      next: ({ sexos }) => (this.sexos = sexos || []),
      error: onError('sexos')
    });
    this.catalogoService.getAnios().subscribe({
      next: ({ anios }) => (this.anios = anios || []),
      error: onError('años')
    });
  }

  /**
   * Al cambiar departamento, recarga municipios dependientes y consulta de nuevo.
   * Limpia municipio seleccionado para evitar combinaciones inválidas.
   */
  onDepartamentoChange(): void {
    this.filtroMunicipio = '';
    this.municipios = [];
    if (this.filtroDepartamento) {
      this.catalogoService.getMunicipiosPorDepartamento(this.filtroDepartamento).subscribe({
        next: ({ municipios }) => (this.municipios = municipios || []),
        error: () => (this.municipios = [])
      });
    }
    this.pagina = 1;
    this.consultar();
  }

  /** Reconsulta al cambiar municipio, reiniciando en página 1. */
  onMunicipioChange(): void {
    this.pagina = 1;
    this.consultar();
  }

  /** Reconsulta al cambiar cualquier filtro demográfico o territorial. */
  onFiltroChange(): void {
    this.pagina = 1;
    this.consultar();
  }

  /** Restablece todos los filtros y vuelve a la primera página. */
  limpiarFiltros(): void {
    this.filtroDepartamento = '';
    this.filtroMunicipio = '';
    this.filtroRegional = '';
    this.filtroArea = '';
    this.filtroSexo = '';
    this.filtroAnio = '';
    this.municipios = [];
    this.pagina = 1;
    this.consultar();
  }

  /**
   * Ejecuta la consulta paginada contra la API con los filtros actuales.
   * Los parámetros vacíos se omiten para que el servidor devuelva el universo completo.
   */
  consultar(): void {
    this.loading.set(true);
    this.error.set('');
    this.poblacionService
      .consultar(this.tabActiva(), {
        pagina: this.pagina,
        tamanoPagina: this.tamanoPagina,
        regional: this.filtroRegional || undefined,
        area: this.filtroArea || undefined,
        sexo: this.filtroSexo || undefined,
        ano: this.filtroAnio ? Number(this.filtroAnio) : undefined,
        codigoDepartamento: this.filtroDepartamento || undefined,
        codigoMunicipio: this.filtroMunicipio || undefined
      })
      .subscribe({
        next: (r) => {
          this.datos.set(r);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.error || 'Error al consultar proyección.');
          this.loading.set(false);
        }
      });
  }

  /** Navega a la página anterior si existe. */
  paginaAnterior(): void {
    if (this.pagina > 1) {
      this.pagina--;
      this.consultar();
    }
  }

  /** Navega a la página siguiente según totalPaginas devuelto por la API. */
  paginaSiguiente(): void {
    const d = this.datos();
    if (d && this.pagina < d.totalPaginas) {
      this.pagina++;
      this.consultar();
    }
  }

  /**
   * Salta a una página específica.
   * @param p Número de página destino (1-based).
   */
  irAPagina(p: number): void {
    if (p === this.pagina) return;
    this.pagina = p;
    this.consultar();
  }

  ngOnInit(): void {
    this.cargarCatalogos();
    this.consultar();
  }

  /**
   * Obtiene el valor de celda tolerando diferencias de mayúsculas en nombres de columna.
   * @param fila Registro devuelto por la API.
   * @param col Nombre de columna esperado en la plantilla.
   * @returns Representación en cadena del valor o vacío.
   */
  cellValue(fila: Record<string, unknown>, col: string): string {
    const v = fila[col] ?? fila[col.toLowerCase()];
    return v != null ? String(v) : '';
  }
}
