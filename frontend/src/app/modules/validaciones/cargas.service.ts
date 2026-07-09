import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CargaErroresResponse, CargaItem } from '../../shared/models/api.models';

/**
 * Servicio de cargues masivos del OSD.
 * Gestiona listado, detalle de errores y flujo de aprobación/rechazo por validadores.
 */
@Injectable({ providedIn: 'root' })
export class CargasService {
  private readonly http = inject(HttpClient);

  /**
   * Lista cargues procesados, opcionalmente filtrados por dependencia.
   * @param dependenciaId Si se indica, limita resultados a esa dependencia.
   * @returns Observable con arreglo de cargues.
   */
  listar(dependenciaId?: number): Observable<{ cargas: CargaItem[] }> {
    const params: Record<string, string> = {};
    if (dependenciaId != null) {
      params['dependencia_id'] = String(dependenciaId);
    }
    return this.http.get<{ cargas: CargaItem[] }>(`${environment.apiUrl}/cargas`, { params });
  }

  /**
   * Obtiene el detalle de errores de validación de un cargue.
   * @param cargaId Identificador del cargue.
   * @returns Observable con lista de errores por fila/columna.
   */
  getErrores(cargaId: number): Observable<CargaErroresResponse> {
    return this.http.get<CargaErroresResponse>(`${environment.apiUrl}/cargas/${cargaId}/errores`);
  }

  /**
   * Aprueba un cargue pendiente de revisión.
   * @param cargaId Identificador del cargue.
   * @param observaciones Comentarios opcionales del validador.
   * @returns Observable con respuesta del servidor.
   */
  aprobar(cargaId: number, observaciones?: string): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/cargas/${cargaId}/aprobar`, { observaciones });
  }

  /**
   * Rechaza un cargue con observaciones obligatorias.
   * @param cargaId Identificador del cargue.
   * @param observaciones Motivo del rechazo.
   * @returns Observable con respuesta del servidor.
   */
  rechazar(cargaId: number, observaciones: string): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/cargas/${cargaId}/rechazar`, { observaciones });
  }
}
