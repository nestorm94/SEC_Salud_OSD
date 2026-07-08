import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AsisQuery, AsisProyeccionDto, AsisVistasMeta, ProyeccionResponse } from '../../shared/models/api.models';

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

export interface AsisVigenciaDto {
  codigo: string;
  nombre: string;
}

@Injectable({ providedIn: 'root' })
export class AsisService {
  private readonly http = inject(HttpClient);

  getVistasMeta() {
    return this.http.get<AsisVistasMeta & { vistas: string[] }>(`${environment.apiUrl}/asis/vistas`);
  }

  /** Años desde vw_ASIS_* (no depende del SP de proyección con columnas acentuadas). */
  getVigencias() {
    return this.http.get<{ vigencias: AsisVigenciaDto[] }>(`${environment.apiUrl}/asis/catalogos/vigencias`);
  }

  getProyecciones() {
    return this.http.get<{ proyecciones: AsisProyeccionDto[] }>(`${environment.apiUrl}/asis/catalogos/proyecciones`);
  }

  consultar(vista: VistaAsis, query: AsisQuery = {}) {
    let params = new HttpParams();
    if (query.pagina != null) params = params.set('pagina', query.pagina);
    if (query.tamanoPagina != null) params = params.set('tamanoPagina', query.tamanoPagina);
    if (query.vigencia != null) params = params.set('vigencia', query.vigencia);
    if (query.codigoMunicipio) params = params.set('codigoMunicipio', query.codigoMunicipio);
    if (query.nivelTerritorio) params = params.set('nivelTerritorio', query.nivelTerritorio);
    if (query.idProyeccionDane != null) params = params.set('idProyeccionDane', query.idProyeccionDane);

    return this.http.get<ProyeccionResponse>(`${environment.apiUrl}/asis/indicadores/${vista}`, { params });
  }

  descargarExcel(modulo: 'nacimientos' | 'mortalidad', query: AsisQuery = {}) {
    let params = new HttpParams();
    if (query.vigencia != null) params = params.set('vigencia', query.vigencia);
    if (query.codigoMunicipio) params = params.set('codigoMunicipio', query.codigoMunicipio);
    return this.http.get(`${environment.apiUrl}/asis/export/${modulo}/excel`, {
      params,
      responseType: 'blob'
    });
  }
}
