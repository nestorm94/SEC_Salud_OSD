import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IndicadorProstataDto } from '../../shared/models/api.models';

/** Filtros opcionales para la consulta del indicador de mortalidad por próstata. */
export interface ProstataFiltros {
  codigoDane?: string;
  territorio?: string;
  regional?: string;
  anio?: number;
  area?: string;
  limit?: number;
}

/**
 * Servicio del indicador de mortalidad por cáncer de próstata en Casanare.
 * La API devuelve el conjunto de datos; el filtrado fino se realiza en el componente.
 */
@Injectable({ providedIn: 'root' })
export class ProstataService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /**
   * Consulta registros del indicador con filtros opcionales en query string.
   * @param filtros Criterios territoriales, temporales y límite de filas.
   * @returns Observable con metadatos del indicador y arreglo de datos.
   */
  consultar(filtros: ProstataFiltros = {}): Observable<{
    indicador: string;
    fuente: string;
    datos: IndicadorProstataDto[];
  }> {
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
