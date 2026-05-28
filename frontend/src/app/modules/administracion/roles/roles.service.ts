import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Rol } from '../../../shared/models/api.models';

export interface RolDto {
  nombre: string;
  descripcion?: string;
}

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/roles`;

  listar() {
    return this.http.get<{ roles: Rol[] }>(this.base);
  }

  obtener(id: number) {
    return this.http.get<Rol>(`${this.base}/${id}`);
  }

  crear(dto: RolDto) {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  actualizar(id: number, dto: RolDto) {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }

  eliminar(id: number) {
    return this.http.delete<{ ok: boolean }>(`${this.base}/${id}`);
  }
}
