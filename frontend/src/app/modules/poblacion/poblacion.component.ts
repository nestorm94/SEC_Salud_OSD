import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PoblacionService, VistaPoblacion } from './poblacion.service';
import { CatalogoSimpleDto, DepartamentoDto, MunicipioDto, ProyeccionResponse } from '../../shared/models/api.models';
import { CatalogoService } from '../../core/services/catalogo.service';

@Component({
  selector: 'app-poblacion',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
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
  tamanoPagina = 20;

  // Catálogos dinámicos
  departamentos: DepartamentoDto[] = [];
  municipios: MunicipioDto[] = [];
  regionales: CatalogoSimpleDto[] = [];
  areas: CatalogoSimpleDto[] = [];
  sexos: CatalogoSimpleDto[] = [];
  anios: CatalogoSimpleDto[] = [];

  // Filtros (códigos o valores). Vacío = “Todos”.
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

  cambiarTab(clave: VistaPoblacion): void {
    this.tabActiva.set(clave);
    this.pagina = 1;
    this.consultar();
  }

  private cargarCatalogos(): void {
    this.catalogoService.getDepartamentos().subscribe({
      next: ({ departamentos }) => (this.departamentos = departamentos || []),
      error: () => (this.departamentos = [])
    });
    this.catalogoService.getRegionales().subscribe({
      next: ({ regionales }) => (this.regionales = regionales || []),
      error: () => (this.regionales = [])
    });
    this.catalogoService.getAreas().subscribe({
      next: ({ areas }) => (this.areas = areas || []),
      error: () => (this.areas = [])
    });
    this.catalogoService.getSexos().subscribe({
      next: ({ sexos }) => (this.sexos = sexos || []),
      error: () => (this.sexos = [])
    });
    this.catalogoService.getAnios().subscribe({
      next: ({ anios }) => (this.anios = anios || []),
      error: () => (this.anios = [])
    });
  }

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

  onMunicipioChange(): void {
    this.pagina = 1;
    this.consultar();
  }

  onFiltroChange(): void {
    this.pagina = 1;
    this.consultar();
  }

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

  paginaAnterior(): void {
    if (this.pagina > 1) {
      this.pagina--;
      this.consultar();
    }
  }

  paginaSiguiente(): void {
    const d = this.datos();
    if (d && this.pagina < d.totalPaginas) {
      this.pagina++;
      this.consultar();
    }
  }

  ngOnInit(): void {
    this.cargarCatalogos();
    this.consultar();
  }

  cellValue(fila: Record<string, unknown>, col: string): string {
    const v = fila[col] ?? fila[col.toLowerCase()];
    return v != null ? String(v) : '';
  }
}
