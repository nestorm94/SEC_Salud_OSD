import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CargaErroresResponse, CargaItem } from '../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class CargasService {
  private readonly http = inject(HttpClient);

  listar(dependenciaId?: number) {
    const params: Record<string, string> = {};
    if (dependenciaId != null) {
      params['dependencia_id'] = String(dependenciaId);
    }
    return this.http.get<{ cargas: CargaItem[] }>(`${environment.apiUrl}/cargas`, { params });
  }

  getErrores(cargaId: number) {
    return this.http.get<CargaErroresResponse>(`${environment.apiUrl}/cargas/${cargaId}/errores`);
  }

  aprobar(cargaId: number, observaciones?: string) {
    return this.http.post(`${environment.apiUrl}/cargas/${cargaId}/aprobar`, { observaciones });
  }

  rechazar(cargaId: number, observaciones: string) {
    return this.http.post(`${environment.apiUrl}/cargas/${cargaId}/rechazar`, { observaciones });
  }
}
