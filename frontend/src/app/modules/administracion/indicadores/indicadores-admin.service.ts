import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { IndicadorItem } from '../../../shared/models/api.models';

/** Payload para crear o actualizar un indicador bajo una línea temática. */
export interface IndicadorDto {
  linea_tematica_id: number;
  codigo: string;
  nombre: string;
  descripcion?: string | null;
  activo?: boolean;
}

/**
 * Servicio de administración de indicadores del OSD.
 * Permite CRUD de indicadores asociados a líneas temáticas.
 */
@Injectable({ providedIn: 'root' })
export class IndicadoresAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/indicadores`;

  /**
   * Lista indicadores, opcionalmente filtrados por línea temática.
   * @param lineaTematicaId Si se indica, limita resultados a esa línea.
   * @returns Observable con arreglo de indicadores.
   */
  listar(lineaTematicaId?: number | null): Observable<{ indicadores: IndicadorItem[] }> {
    let params = new HttpParams();
    if (lineaTematicaId) {
      params = params.set('linea_tematica_id', String(lineaTematicaId));
    }
    return this.http.get<{ indicadores: IndicadorItem[] }>(this.base, { params });
  }

  /**
   * Crea un indicador nuevo.
   * @param dto Datos del indicador y línea asociada.
   * @returns Observable con ID generado.
   */
  crear(dto: IndicadorDto): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.base, dto);
  }

  /**
   * Actualiza un indicador existente.
   * @param id Identificador del indicador.
   * @param dto Datos actualizados.
   * @returns Observable con confirmación.
   */
  actualizar(id: number, dto: IndicadorDto): Observable<{ ok: boolean }> {
    return this.http.put<{ ok: boolean }>(`${this.base}/${id}`, dto);
  }
}
