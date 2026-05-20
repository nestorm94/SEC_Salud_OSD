import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PaginatedQuery, ProyeccionResponse } from '../../shared/models/api.models';

export type VistaPoblacion = 'nacional-casanare' | 'curso-vida' | 'quinquenios';

@Injectable({ providedIn: 'root' })
export class PoblacionService {
  private readonly http = inject(HttpClient);

  consultar(vista: VistaPoblacion, query: PaginatedQuery = {}) {
    let params = new HttpParams();
    if (query.pagina != null) params = params.set('pagina', query.pagina);
    if (query.tamanoPagina != null) params = params.set('tamanoPagina', query.tamanoPagina);
    if (query.territorio) params = params.set('territorio', query.territorio);
    if (query.regional) params = params.set('regional', query.regional);
    if (query.area) params = params.set('area', query.area);
    if (query.sexo) params = params.set('sexo', query.sexo);
    if (query.ano != null) params = params.set('ano', query.ano);

    return this.http.get<ProyeccionResponse>(
      `${environment.apiUrl}/proyeccion-poblacion/${vista}`,
      { params }
    );
  }
}
