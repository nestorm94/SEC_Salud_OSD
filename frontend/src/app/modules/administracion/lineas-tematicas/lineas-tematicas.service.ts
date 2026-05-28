import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { LineaTematicaItem } from '../../../shared/models/api.models';

export interface LineaTematicaDto {
  codigo: string;
  nombre: string;
  descripcion?: string | null;
  activo?: boolean;
}

@Injectable({ providedIn: 'root' })
export class LineasTematicasService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/lineas-tematicas`;

  listar() {
    return this.http.get<{ lineas_tematicas: LineaTematicaItem[] }>(this.base);
  }

  crear(dto: LineaTematicaDto) {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  actualizar(id: number, dto: LineaTematicaDto) {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }
}
