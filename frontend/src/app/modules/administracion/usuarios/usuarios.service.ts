import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { UsuarioAdmin } from '../../../shared/models/api.models';

export interface CrearUsuarioDto {
  nombre_usuario: string;
  password: string;
  email?: string;
  dependencia_id?: number;
  roles?: string[];
}

export interface ActualizarUsuarioDto {
  email?: string;
  dependencia_id?: number;
  password?: string;
  roles?: string[];
}

@Injectable({ providedIn: 'root' })
export class UsuariosService {
  private readonly http = inject(HttpClient);

  listar() {
    return this.http.get<{ usuarios: UsuarioAdmin[] }>(`${environment.apiUrl}/admin/usuarios`);
  }

  obtener(id: number) {
    return this.http.get<UsuarioAdmin>(`${environment.apiUrl}/admin/usuarios/${id}`);
  }

  crear(dto: CrearUsuarioDto) {
    return this.http.post(`${environment.apiUrl}/usuarios`, dto);
  }

  actualizar(id: number, dto: ActualizarUsuarioDto) {
    return this.http.put(`${environment.apiUrl}/admin/usuarios/${id}`, dto);
  }

  setActivo(id: number, activo: boolean) {
    return this.http.patch(`${environment.apiUrl}/admin/usuarios/${id}/activo`, { activo });
  }

  actualizarRoles(id: number, roles: string[]) {
    return this.http.put(`${environment.apiUrl}/admin/usuarios/${id}/roles`, { roles });
  }

  eliminar(id: number) {
    return this.http.delete<{ ok: boolean; activo: boolean }>(
      `${environment.apiUrl}/admin/usuarios/${id}`
    );
  }
}
