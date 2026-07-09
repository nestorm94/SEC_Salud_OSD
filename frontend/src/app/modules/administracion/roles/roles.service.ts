import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Rol } from '../../../shared/models/api.models';

/** Payload para crear o actualizar un rol de seguridad. */
export interface RolDto {
  nombre: string;
  descripcion?: string;
}

/**
 * Servicio de administración de roles del OSD.
 * Gestiona el catálogo de roles asignables a usuarios.
 */
@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/roles`;

  /**
   * Lista todos los roles definidos en el sistema.
   * @returns Observable con arreglo de roles.
   */
  listar(): Observable<{ roles: Rol[] }> {
    return this.http.get<{ roles: Rol[] }>(this.base);
  }

  /**
   * Obtiene un rol por identificador.
   * @param id ID del rol.
   * @returns Observable con detalle del rol.
   */
  obtener(id: number): Observable<Rol> {
    return this.http.get<Rol>(`${this.base}/${id}`);
  }

  /**
   * Crea un rol nuevo.
   * @param dto Nombre y descripción opcional.
   * @returns Observable con ID generado.
   */
  crear(dto: RolDto): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  /**
   * Actualiza nombre y descripción de un rol.
   * @param id ID del rol.
   * @param dto Datos actualizados.
   * @returns Observable con confirmación.
   */
  actualizar(id: number, dto: RolDto): Observable<{ ok: boolean }> {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }

  /**
   * Elimina un rol del catálogo.
   * @param id ID del rol.
   * @returns Observable con confirmación.
   */
  eliminar(id: number): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.base}/${id}`);
  }
}
