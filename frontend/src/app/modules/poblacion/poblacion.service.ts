import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaginatedQuery, ProyeccionResponse } from '../../shared/models/api.models';

/** Vistas disponibles del módulo de proyección poblacional. */
export type VistaPoblacion = 'nacional-casanare' | 'curso-vida' | 'quinquenios';

/**
 * Servicio de consulta de proyección poblacional del OSD.
 * Expone las tres vistas tabulares con filtros territoriales y demográficos paginados.
 */
@Injectable({ providedIn: 'root' })
export class PoblacionService {
  private readonly http = inject(HttpClient);

  /**
   * Consulta una vista de proyección con filtros y paginación server-side.
   * @param vista Clave de la vista (nacional-casanare, curso-vida o quinquenios).
   * @param query Parámetros opcionales de página, territorio, regional, área, sexo y año.
   * @returns Observable con columnas, filas y metadatos de paginación.
   */
  consultar(vista: VistaPoblacion, query: PaginatedQuery = {}): Observable<ProyeccionResponse> {
    let params = new HttpParams();
    if (query.pagina != null) params = params.set('pagina', query.pagina);
    if (query.tamanoPagina != null) params = params.set('tamanoPagina', query.tamanoPagina);
    if (query.territorio) params = params.set('territorio', query.territorio);
    if (query.regional) params = params.set('regional', query.regional);
    if (query.area) params = params.set('area', query.area);
    if (query.sexo) params = params.set('sexo', query.sexo);
    if (query.ano != null) params = params.set('ano', query.ano);
    if (query.codigoDepartamento) params = params.set('codigoDepartamento', query.codigoDepartamento);
    if (query.codigoMunicipio) params = params.set('codigoMunicipio', query.codigoMunicipio);

    return this.http.get<ProyeccionResponse>(
      `${environment.apiUrl}/proyeccion-poblacion/${vista}`,
      { params }
    );
  }
}
