import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { IndicadorItem } from '../../../shared/models/api.models';

export interface IndicadorDto {
  linea_tematica_id: number;
  codigo: string;
  nombre: string;
  descripcion?: string | null;
  activo?: boolean;
}

@Injectable({ providedIn: 'root' })
export class IndicadoresAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/indicadores`;

  listar(lineaTematicaId?: number | null) {
    let params = new HttpParams();
    if (lineaTematicaId) {
      params = params.set('linea_tematica_id', String(lineaTematicaId));
    }
    return this.http.get<{ indicadores: IndicadorItem[] }>(this.base, { params });
  }

  crear(dto: IndicadorDto) {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  actualizar(id: number, dto: IndicadorDto) {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }
}
