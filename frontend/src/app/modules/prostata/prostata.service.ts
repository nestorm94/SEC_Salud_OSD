import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { IndicadorProstataDto } from '../../shared/models/api.models';

export interface ProstataFiltros {
  codigoDane?: string;
  territorio?: string;
  regional?: string;
  anio?: number;
  area?: string;
  limit?: number;
}

@Injectable({ providedIn: 'root' })
export class ProstataService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  consultar(filtros: ProstataFiltros = {}) {
    let params = new HttpParams();
    if (filtros.codigoDane) params = params.set('codigoDane', filtros.codigoDane);
    if (filtros.territorio) params = params.set('territorio', filtros.territorio);
    if (filtros.regional) params = params.set('regional', filtros.regional);
    if (filtros.anio != null) params = params.set('anio', String(filtros.anio));
    if (filtros.area) params = params.set('area', filtros.area);
    if (filtros.limit != null) params = params.set('limit', String(filtros.limit));

    return this.http.get<{
      indicador: string;
      fuente: string;
      datos: IndicadorProstataDto[];
    }>(`${this.base}/indicadores/prostata`, { params });
  }
}
