import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { LineaTematicaItem } from '../../../shared/models/api.models';

/** Payload para crear o actualizar una línea temática. */
export interface LineaTematicaDto {
  codigo: string;
  nombre: string;
  descripcion?: string | null;
  activo?: boolean;
}

/**
 * Servicio de administración de líneas temáticas del OSD.
 * Agrupa indicadores de salud bajo categorías reportables.
 */
@Injectable({ providedIn: 'root' })
export class LineasTematicasService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/lineas-tematicas`;

  /**
   * Lista líneas temáticas registradas (activas e inactivas).
   * @returns Observable con catálogo administrativo.
   */
  listar(): Observable<{ lineas_tematicas: LineaTematicaItem[] }> {
    return this.http.get<{ lineas_tematicas: LineaTematicaItem[] }>(this.base);
  }

  /**
   * Registra una nueva línea temática.
   * @param dto Código, nombre, descripción y estado activo.
   * @returns Observable con ID generado.
   */
  crear(dto: LineaTematicaDto): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  /**
   * Actualiza una línea temática existente.
   * @param id Identificador de la línea.
   * @param dto Datos actualizados.
   * @returns Observable con confirmación.
   */
  actualizar(id: number, dto: LineaTematicaDto): Observable<{ ok: boolean }> {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }
}
