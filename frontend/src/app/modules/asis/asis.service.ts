import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AsisQuery, AsisProyeccionDto, AsisVistasMeta, ProyeccionResponse } from '../../shared/models/api.models';

/** Claves de las vistas tabulares ASIS expuestas por la API (Fase 4). */
export type VistaAsis =
  | 'poblacion-total'
  | 'poblacion-municipio'
  | 'poblacion-sexo'
  | 'poblacion-area'
  | 'poblacion-grupo-edad'
  | 'poblacion-curso-vida'
  | 'piramide-poblacional'
  | 'mortalidad-total'
  | 'mortalidad-municipio'
  | 'mortalidad-sexo'
  | 'mortalidad-area'
  | 'mortalidad-grupo-edad'
  | 'mortalidad-curso-vida'
  | 'mortalidad-detalle'
  | 'nacimientos-total'
  | 'nacimientos-municipio'
  | 'nacimientos-sexo'
  | 'nacimientos-area'
  | 'nacimientos-grupo-edad'
  | 'nacimientos-detalle'
  | 'nacimientos-nivel-educativo'
  | 'nacimientos-pertenencia-etnica'
  | 'nacimientos-peso-al-nacer'
  | 'nacimientos-semanas-gestacion'
  | 'tasa-bruta-mortalidad'
  | 'serie-mortalidad'
  | 'comparativo-poblacion-mortalidad';

/** Elemento del catálogo de vigencias (años) ASIS. */
export interface AsisVigenciaDto {
  codigo: string;
  nombre: string;
}

/**
 * Servicio de indicadores ASIS del OSD (población, mortalidad, nacimientos).
 * Consulta vistas paginadas, catálogos auxiliares y exportación Excel de microdatos.
 */
@Injectable({ providedIn: 'root' })
export class AsisService {
  private readonly http = inject(HttpClient);

  /**
   * Obtiene metadatos de configuración ASIS (capa de población, proyección por defecto).
   * @returns Observable con flags de capa legacy/fact y lista de vistas disponibles.
   */
  getVistasMeta(): Observable<AsisVistasMeta & { vistas: string[] }> {
    return this.http.get<AsisVistasMeta & { vistas: string[] }>(`${environment.apiUrl}/asis/vistas`);
  }

  /**
   * Lista años de vigencia desde vistas vw_ASIS_* (independiente del SP de proyección).
   * @returns Observable con catálogo de vigencias.
   */
  getVigencias(): Observable<{ vigencias: AsisVigenciaDto[] }> {
    return this.http.get<{ vigencias: AsisVigenciaDto[] }>(`${environment.apiUrl}/asis/catalogos/vigencias`);
  }

  /**
   * Lista proyecciones DANE disponibles para filtros de población en capa fact.
   * @returns Observable con proyecciones configuradas.
   */
  getProyecciones(): Observable<{ proyecciones: AsisProyeccionDto[] }> {
    return this.http.get<{ proyecciones: AsisProyeccionDto[] }>(`${environment.apiUrl}/asis/catalogos/proyecciones`);
  }

  /**
   * Consulta una vista ASIS con paginación y filtros territoriales/temporales.
   * @param vista Clave de la vista (ej. poblacion-total, mortalidad-detalle).
   * @param query Parámetros opcionales de página, vigencia, municipio, nivel y proyección DANE.
   * @returns Observable con respuesta tabular paginada.
   */
  consultar(vista: VistaAsis, query: AsisQuery = {}): Observable<ProyeccionResponse> {
    let params = new HttpParams();
    if (query.pagina != null) params = params.set('pagina', query.pagina);
    if (query.tamanoPagina != null) params = params.set('tamanoPagina', query.tamanoPagina);
    if (query.vigencia != null) params = params.set('vigencia', query.vigencia);
    if (query.codigoMunicipio) params = params.set('codigoMunicipio', query.codigoMunicipio);
    if (query.nivelTerritorio) params = params.set('nivelTerritorio', query.nivelTerritorio);
    if (query.idProyeccionDane != null) params = params.set('idProyeccionDane', query.idProyeccionDane);

    return this.http.get<ProyeccionResponse>(`${environment.apiUrl}/asis/indicadores/${vista}`, { params });
  }

  /**
   * Genera archivo Excel de microdatos de nacimientos o defunciones.
   * @param modulo Módulo a exportar: nacimientos o mortalidad.
   * @param query Filtros de vigencia y municipio aplicados a la exportación.
   * @returns Observable con blob del archivo .xlsx.
   */
  descargarExcel(modulo: 'nacimientos' | 'mortalidad', query: AsisQuery = {}): Observable<Blob> {
    let params = new HttpParams();
    if (query.vigencia != null) params = params.set('vigencia', query.vigencia);
    if (query.codigoMunicipio) params = params.set('codigoMunicipio', query.codigoMunicipio);
    return this.http.get(`${environment.apiUrl}/asis/export/${modulo}/excel`, {
      params,
      responseType: 'blob'
    });
  }
}
