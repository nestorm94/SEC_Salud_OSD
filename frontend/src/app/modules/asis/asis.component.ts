import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { TABLE_PAGE_SIZE } from '../../shared/utils/table-pagination.util';
import { AsisService, AsisVigenciaDto, VistaAsis } from './asis.service';
import { ProyeccionResponse, MunicipioDto, AsisProyeccionDto } from '../../shared/models/api.models';
import { CatalogoService } from '../../core/services/catalogo.service';

type GrupoAsis = 'poblacion' | 'mortalidad' | 'nacimientos' | 'indicadores';

interface TabAsis {
  clave: VistaAsis;
  label: string;
  grupo: GrupoAsis;
}

/**
 * Módulo ASIS del OSD: consulta tabular de población, mortalidad, nacimientos e indicadores
 * para Casanare, con filtros por vigencia/municipio, paginación server-side y exportación Excel.
 */
@Component({
  selector: 'app-asis',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, LoadingStateComponent, TablePaginatorComponent],
  templateUrl: './asis.component.html',
  styleUrl: './asis.component.scss'
})
export class AsisComponent implements OnInit {
  private readonly asisService = inject(AsisService);
  private readonly catalogoService = inject(CatalogoService);

  readonly grupoActivo = signal<GrupoAsis>('poblacion');
  tabActiva = signal<VistaAsis>('poblacion-total');
  datos = signal<ProyeccionResponse | null>(null);
  loading = signal(false);
  descargando = signal(false);
  error = signal('');

  pagina = 1;
  readonly tamanoPagina = TABLE_PAGE_SIZE;

  municipios: MunicipioDto[] = [];
  vigencias: AsisVigenciaDto[] = [];
  proyecciones: AsisProyeccionDto[] = [];

  capaPoblacion = signal<'legacy' | 'fact'>('legacy');
  filtroMunicipio = '';
  filtroVigencia = '';
  filtroNivelTerritorio = '';
  filtroProyeccion = '';

  readonly codigoDepartamentoCasanare = '85';

  readonly tabs: TabAsis[] = [
    { clave: 'poblacion-total', label: 'Total', grupo: 'poblacion' },
    { clave: 'poblacion-municipio', label: 'Municipio', grupo: 'poblacion' },
    { clave: 'poblacion-sexo', label: 'Sexo', grupo: 'poblacion' },
    { clave: 'poblacion-area', label: 'Área', grupo: 'poblacion' },
    { clave: 'poblacion-grupo-edad', label: 'Grupo edad', grupo: 'poblacion' },
    { clave: 'poblacion-curso-vida', label: 'Curso de vida', grupo: 'poblacion' },
    { clave: 'piramide-poblacional', label: 'Pirámide', grupo: 'poblacion' },
    { clave: 'mortalidad-total', label: 'Total', grupo: 'mortalidad' },
    { clave: 'mortalidad-municipio', label: 'Municipio', grupo: 'mortalidad' },
    { clave: 'mortalidad-detalle', label: 'Detalle', grupo: 'mortalidad' },
    { clave: 'mortalidad-sexo', label: 'Sexo', grupo: 'mortalidad' },
    { clave: 'mortalidad-area', label: 'Área', grupo: 'mortalidad' },
    { clave: 'mortalidad-grupo-edad', label: 'Grupo edad', grupo: 'mortalidad' },
    { clave: 'mortalidad-curso-vida', label: 'Curso de vida', grupo: 'mortalidad' },
    { clave: 'nacimientos-total', label: 'Total', grupo: 'nacimientos' },
    { clave: 'nacimientos-municipio', label: 'Municipio', grupo: 'nacimientos' },
    { clave: 'nacimientos-detalle', label: 'Detalle', grupo: 'nacimientos' },
    { clave: 'nacimientos-sexo', label: 'Sexo', grupo: 'nacimientos' },
    { clave: 'nacimientos-area', label: 'Área', grupo: 'nacimientos' },
    { clave: 'nacimientos-grupo-edad', label: 'Edad madre', grupo: 'nacimientos' },
    { clave: 'nacimientos-nivel-educativo', label: 'Nivel educativo', grupo: 'nacimientos' },
    { clave: 'nacimientos-pertenencia-etnica', label: 'Etnia', grupo: 'nacimientos' },
    { clave: 'nacimientos-peso-al-nacer', label: 'Peso al nacer', grupo: 'nacimientos' },
    { clave: 'nacimientos-semanas-gestacion', label: 'Semanas gestación', grupo: 'nacimientos' },
    { clave: 'tasa-bruta-mortalidad', label: 'Tasa bruta', grupo: 'indicadores' },
    { clave: 'serie-mortalidad', label: 'Serie 2005–2025', grupo: 'indicadores' },
    { clave: 'comparativo-poblacion-mortalidad', label: 'Comparativo', grupo: 'indicadores' }
  ];

  readonly tabsDelGrupo = computed(() => this.tabs.filter((t) => t.grupo === this.grupoActivo()));

  private readonly clavesSinMunicipio: VistaAsis[] = [
    'poblacion-total',
    'mortalidad-total',
    'nacimientos-total',
    'serie-mortalidad'
  ];

  readonly muestraFiltroMunicipio = computed(
    () => !this.clavesSinMunicipio.includes(this.tabActiva())
  );

  readonly muestraFiltroNivel = computed(() => this.tabActiva() === 'tasa-bruta-mortalidad');

  readonly muestraFiltroProyeccion = computed(
    () => this.capaPoblacion() === 'fact' && this.grupoActivo() === 'poblacion'
  );

  readonly muestraDescargaExcel = computed(() => {
    const g = this.grupoActivo();
    const tab = this.tabActiva();
    return g === 'nacimientos' || g === 'mortalidad'
      || tab.startsWith('nacimientos-') || tab.startsWith('mortalidad-');
  });

  readonly etiquetaDescargaExcel = computed(() => {
    if (this.descargando()) return 'Generando Excel…';
    return this.esModuloMortalidad()
      ? 'Descargar defunciones (Excel)'
      : 'Descargar nacimientos (Excel)';
  });

  /** Expuesto al template para el banner de exportación. */
  readonly esModuloMortalidad = computed(() =>
    this.grupoActivo() === 'mortalidad' || this.tabActiva().startsWith('mortalidad-')
  );

  private moduloExportacion(): 'nacimientos' | 'mortalidad' {
    return this.esModuloMortalidad() ? 'mortalidad' : 'nacimientos';
  }

  /** Columnas técnicas / metadata que no se muestran en la tabla. */
  private readonly columnasOcultas = new Set([
    'id_proyeccion_dane',
    'nombre_proyeccion',
    'fuente_datos',
    'criterio_agregacion',
    'id_sexo',
    'id_area',
    'id_grupo_edad',
    'id_curso_vida',
    'codigo_territorio_dane',
    'area_proyeccion',
    'sexo_dim',
    'codigo_grupo_edad_dim',
    'codigo_grupo_edad',
    'codigo_curso_vida_dim',
    'id_grupo_edad_madre',
    'codigo_grupo_edad_madre',
    'id_nivel_educativo',
    'id_pertenencia_etnica',
    'id_peso_al_nacer',
    'id_semanas_gestacion',
    'codigo_nivel_educativo',
    'codigo_pertenencia_etnica',
    'codigo_peso_al_nacer',
    'codigo_semanas_gestacion',
    'categoria_normalizada',
    'grupo_edad_madre'
  ]);

  readonly columnasVisibles = computed(() => {
    const cols = this.datos()?.columnas ?? [];
    return cols.filter((c) => !this.columnasOcultas.has(c.trim().toLowerCase()));
  });

  /**
   * Cambia el grupo de pestañas (población, mortalidad, nacimientos, indicadores)
   * y activa la primera vista del grupo.
   * @param grupo Identificador del grupo seleccionado.
   */
  cambiarGrupo(grupo: GrupoAsis): void {
    this.grupoActivo.set(grupo);
    const primero = this.tabs.find((t) => t.grupo === grupo);
    if (primero) this.cambiarTab(primero.clave);
  }

  /**
   * Cambia la vista ASIS activa, sincroniza el grupo y dispara consulta paginada.
   * @param clave Clave de la vista (ej. mortalidad-detalle).
   */
  cambiarTab(clave: VistaAsis): void {
    this.tabActiva.set(clave);
    const tab = this.tabs.find((t) => t.clave === clave);
    if (tab) this.grupoActivo.set(tab.grupo);
    this.pagina = 1;
    this.consultar();
  }

  /** Reconsulta al modificar filtros, reiniciando en página 1. */
  onFiltroChange(): void {
    this.pagina = 1;
    this.consultar();
  }

  /** Restablece filtros ASIS y aplica proyección DANE por defecto si aplica. */
  limpiarFiltros(): void {
    this.filtroMunicipio = '';
    this.filtroVigencia = '';
    this.filtroNivelTerritorio = '';
    this.filtroProyeccion = this.proyeccionDefault();
    this.pagina = 1;
    this.consultar();
  }

  /**
   * Consulta la vista ASIS activa con filtros y paginación.
   * Normaliza nivel de territorio y omite idProyeccionDane fuera de capa fact/población.
   */
  consultar(): void {
    this.loading.set(true);
    this.error.set('');
    const nivel =
      this.filtroNivelTerritorio === 'DEPARTAMENTO' || this.filtroNivelTerritorio === 'MUNICIPIO'
        ? this.filtroNivelTerritorio
        : undefined;

    const vista = this.tabActiva();
    const codigoMun = this.codigoMunicipioFiltro();

    this.asisService
      .consultar(vista, {
        pagina: this.pagina,
        tamanoPagina: this.tamanoPagina,
        vigencia: this.filtroVigencia ? Number(this.filtroVigencia) : undefined,
        codigoMunicipio: codigoMun,
        nivelTerritorio: nivel,
        idProyeccionDane: this.idProyeccionFiltro()
      })
      .subscribe({
        next: (r) => {
          this.datos.set(r);
          this.loading.set(false);
        },
        error: (err) => {
          const msg = err?.error?.error;
          const esHtml =
            typeof err?.error === 'string' && err.error.trimStart().startsWith('<');
          const detalle = err?.error?.detalle;
          this.error.set(
            esHtml
              ? 'La API devolvió HTML en lugar de JSON. Reinicie Observatorios.Api (código ASIS nuevo) o use ng serve con proxy a :5289.'
              : [msg, detalle].filter(Boolean).join(' ') || err?.message || 'Error al consultar indicadores ASIS.'
          );
          this.loading.set(false);
        }
      });
  }

  /**
   * Navega a otra página de resultados (paginación server-side).
   * @param p Número de página destino.
   */
  irAPagina(p: number): void {
    if (p === this.pagina) return;
    this.pagina = p;
    this.consultar();
  }

  /**
   * Descarga Excel de nacimientos o defunciones según el módulo activo.
   * Aplica los mismos filtros de vigencia y municipio de la consulta en pantalla.
   */
  descargarExcel(): void {
    if (!this.muestraDescargaExcel()) return;
    const grupo = this.moduloExportacion();

    this.descargando.set(true);
    this.error.set('');

    this.asisService
      .descargarExcel(grupo, {
        vigencia: this.filtroVigencia ? Number(this.filtroVigencia) : undefined,
        codigoMunicipio: this.codigoMunicipioFiltro()
      })
      .subscribe({
        next: (blob) => {
          const prefijo = grupo === 'nacimientos' ? 'Nacimientos-Casanare' : 'Defunciones-Casanare';
          const a = document.createElement('a');
          a.href = URL.createObjectURL(blob);
          a.download = `${prefijo}-${new Date().toISOString().slice(0, 10)}.xlsx`;
          a.click();
          URL.revokeObjectURL(a.href);
          this.descargando.set(false);
        },
        error: () => {
          this.error.set('No se pudo generar el archivo Excel. Reinicie la API si acaba de desplegar cambios.');
          this.descargando.set(false);
        }
      });
  }

  ngOnInit(): void {
    this.asisService.getVistasMeta().subscribe({
      next: (meta) => {
        this.capaPoblacion.set(meta.capaPoblacion === 'fact' ? 'fact' : 'legacy');
        this.filtroProyeccion = String(meta.idProyeccionDaneDefault ?? 1);
      },
      error: () => this.capaPoblacion.set('legacy')
    });
    this.catalogoService.getMunicipiosPorDepartamento(this.codigoDepartamentoCasanare).subscribe({
      next: ({ municipios }) => {
        this.municipios = (municipios || []).map((m) => this.normalizarMunicipio(m));
      },
      error: () => (this.municipios = [])
    });
    this.asisService.getVigencias().subscribe({
      next: ({ vigencias }) => {
        this.vigencias = (vigencias || []).map((v) => ({
          codigo: v.codigo ?? String((v as { Codigo?: string }).Codigo ?? ''),
          nombre: v.nombre ?? String((v as { Nombre?: string }).Nombre ?? v.codigo ?? '')
        }));
      },
      error: () => (this.vigencias = [])
    });
    this.asisService.getProyecciones().subscribe({
      next: ({ proyecciones }) => {
        this.proyecciones = proyecciones || [];
        if (!this.filtroProyeccion && this.proyecciones.length) {
          this.filtroProyeccion = String(this.proyecciones[0].id);
        }
      },
      error: () => (this.proyecciones = [])
    });
    this.consultar();
  }

  private proyeccionDefault(): string {
    return this.filtroProyeccion || (this.proyecciones[0] ? String(this.proyecciones[0].id) : '1');
  }

  private idProyeccionFiltro(): number | undefined {
    if (this.capaPoblacion() !== 'fact' || this.grupoActivo() !== 'poblacion') return undefined;
    const id = Number(this.filtroProyeccion);
    return Number.isFinite(id) && id > 0 ? id : undefined;
  }

  /** Código DANE 5 dígitos para query ?codigoMunicipio= */
  private codigoMunicipioFiltro(): string | undefined {
    const raw = String(this.filtroMunicipio ?? '').trim();
    if (!raw) return undefined;
    return /^\d+$/.test(raw) ? raw.padStart(5, '0') : raw;
  }

  private normalizarMunicipio(m: MunicipioDto & { CodigoDane?: string; NombreMunicipio?: string }): MunicipioDto {
    const cod = String(m.codigoDane ?? m.CodigoDane ?? '').trim();
    return {
      codigoDane: /^\d+$/.test(cod) ? cod.padStart(5, '0') : cod,
      codigoDepartamento: m.codigoDepartamento ?? (m as { CodigoDepartamento?: string }).CodigoDepartamento ?? '85',
      nombreMunicipio: m.nombreMunicipio ?? m.NombreMunicipio ?? '',
      regional: m.regional ?? (m as { Regional?: string }).Regional ?? ''
    };
  }

  /**
   * Obtiene valor de celda con formato numérico localizado cuando aplica.
   * @param fila Registro de la API.
   * @param col Nombre de columna.
   * @returns Texto a mostrar en la celda.
   */
  cellValue(fila: Record<string, unknown>, col: string): string {
    const v = fila[col] ?? fila[col.toLowerCase()];
    if (v == null) return '';
    if (typeof v === 'number') return Number.isInteger(v) ? String(v) : v.toLocaleString('es-CO', { maximumFractionDigits: 4 });
    return String(v);
  }

  /**
   * Ancho sugerido por columna para el colgroup de la tabla ASIS.
   * @param col Nombre de columna.
   * @returns Ancho CSS (rem).
   */
  anchoColumna(col: string): string {
    const key = col.trim().toLowerCase();
    const anchos: Record<string, string> = {
      codigo_departamento: '3.5rem',
      codigo_municipio: '4.75rem',
      vigencia: '4.5rem',
      anio: '4.5rem',
      edad_simple: '3.75rem',
      poblacion: '6.25rem',
      defunciones: '5.75rem',
      nacimientos: '5.75rem',
      tasa_bruta_mortalidad: '5.5rem',
      sexo_proyeccion: '5.5rem',
      area_normalizada: '5rem',
      nivel_territorio: '6.5rem',
      regional: '7rem',
      nombre_departamento: '7.5rem',
      nombre_municipio: '9rem',
      nombre_territorio: '9rem',
      nombre_grupo_edad: '10rem',
      nombre_curso_vida: '11rem',
      grupo_quinquenal: '9rem',
      grupo_edad_madre: '9rem',
      grupo_etareo_quinquenios_dane: '9rem',
      nivel_educativo: '11rem',
      pertenencia_etnica: '12rem',
      peso_al_nacer: '12rem',
      semanas_gestacion: '12rem',
      area_residencia: '5rem',
      curso_vida_proyeccion: '11rem',
    };
    if (anchos[key]) return anchos[key];
    if (key.includes('codigo') || key.includes('código')) return '4.75rem';
    if (key.includes('tasa') || key.includes('poblacion') || key.includes('defuncion')) return '6rem';
    if (key.includes('nombre') || key.includes('grupo') || key.includes('curso')) return '9.5rem';
    return '6.5rem';
  }

  /**
   * Clase CSS de alineación según tipo semántico de la columna.
   * @param col Nombre de columna.
   * @returns Clase col--code, col--num o col--text.
   */
  claseColumna(col: string): string {
    const key = col.trim().toLowerCase();
    if (
      key === 'vigencia' ||
      key === 'anio' ||
      key === 'año' ||
      key.startsWith('codigo') ||
      key.startsWith('código') ||
      key === 'edad_simple'
    ) {
      return 'col--code';
    }
    if (
      key.includes('poblacion') ||
      key.includes('defuncion') ||
      key.includes('nacimiento') ||
      key.includes('tasa') ||
      key.includes('total')
    ) {
      return 'col--num';
    }
    return 'col--text';
  }
}
