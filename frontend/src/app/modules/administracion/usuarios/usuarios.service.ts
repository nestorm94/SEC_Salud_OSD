import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { UsuarioAdmin } from '../../../shared/models/api.models';

/** Datos para crear un nuevo usuario en el OSD. */
export interface CrearUsuarioDto {
  nombre_usuario: string;
  password: string;
  email?: string;
  dependencia_id?: number;
  roles?: string[];
}

/** Datos parciales para actualizar un usuario existente. */
export interface ActualizarUsuarioDto {
  email?: string;
  dependencia_id?: number;
  password?: string;
  roles?: string[];
}

/**
 * Servicio de administración de usuarios del OSD.
 * Expone CRUD, activación y asignación de roles contra endpoints /admin/usuarios.
 */
@Injectable({ providedIn: 'root' })
export class UsuariosService {
  private readonly http = inject(HttpClient);

  /**
   * Lista todos los usuarios del sistema.
   * @returns Observable con arreglo de usuarios administrables.
   */
  listar(): Observable<{ usuarios: UsuarioAdmin[] }> {
    return this.http.get<{ usuarios: UsuarioAdmin[] }>(`${environment.apiUrl}/admin/usuarios`);
  }

  /**
   * Obtiene el detalle de un usuario por ID.
   * @param id Identificador del usuario.
   * @returns Observable con perfil administrativo.
   */
  obtener(id: number): Observable<UsuarioAdmin> {
    return this.http.get<UsuarioAdmin>(`${environment.apiUrl}/admin/usuarios/${id}`);
  }

  /**
   * Crea un usuario nuevo (endpoint público de registro inicial).
   * @param dto Credenciales, dependencia y roles iniciales.
   * @returns Observable con respuesta del servidor.
   */
  crear(dto: CrearUsuarioDto): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/usuarios`, dto);
  }

  /**
   * Actualiza datos de perfil, contraseña y roles de un usuario.
   * @param id Identificador del usuario.
   * @param dto Campos a modificar.
   * @returns Observable con respuesta del servidor.
   */
  actualizar(id: number, dto: ActualizarUsuarioDto): Observable<unknown> {
    return this.http.put(`${environment.apiUrl}/admin/usuarios/${id}`, dto);
  }

  /**
   * Activa o desactiva un usuario sin eliminarlo.
   * @param id Identificador del usuario.
   * @param activo Nuevo estado activo/inactivo.
   * @returns Observable con respuesta del servidor.
   */
  setActivo(id: number, activo: boolean): Observable<unknown> {
    return this.http.patch(`${environment.apiUrl}/admin/usuarios/${id}/activo`, { activo });
  }

  /**
   * Reemplaza la lista completa de roles de un usuario.
   * @param id Identificador del usuario.
   * @param roles Nombres de roles asignados.
   * @returns Observable con respuesta del servidor.
   */
  actualizarRoles(id: number, roles: string[]): Observable<unknown> {
    return this.http.put(`${environment.apiUrl}/admin/usuarios/${id}/roles`, { roles });
  }

  /**
   * Desactiva lógicamente un usuario (soft delete).
   * @param id Identificador del usuario.
   * @returns Observable con confirmación y estado resultante.
   */
  eliminar(id: number): Observable<{ ok: boolean; activo: boolean }> {
    return this.http.delete<{ ok: boolean; activo: boolean }>(
      `${environment.apiUrl}/admin/usuarios/${id}`
    );
  }
}
