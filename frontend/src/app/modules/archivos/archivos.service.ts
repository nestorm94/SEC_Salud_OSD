import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ArchivoItem,
  IndicadorItem,
  LineaTematicaItem,
  ValidacionArchivoResponse
} from '../../shared/models/api.models';

/**
 * Servicio de gestión de archivos OSC del OSD.
 * Cubre listado, prevalidación, envío, descarga y flujo de aprobación/rechazo administrativo.
 */
@Injectable({ providedIn: 'root' })
export class ArchivosService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/archivos`;

  /**
   * Lista los archivos visibles para el usuario según su dependencia y permisos.
   * @returns Observable con el arreglo de archivos.
   */
  listar(): Observable<{ archivos: ArchivoItem[] }> {
    return this.http.get<{ archivos: ArchivoItem[] }>(this.base);
  }

  /**
   * Obtiene líneas temáticas activas para el selector de carga.
   * @returns Observable con catálogo de líneas temáticas.
   */
  listarLineasTematicas(): Observable<{ lineas_tematicas: LineaTematicaItem[] }> {
    return this.http.get<{ lineas_tematicas: LineaTematicaItem[] }>(
      `${environment.apiUrl}/lineas-tematicas`
    );
  }

  /**
   * Filtra indicadores asociados a una línea temática.
   * @param lineaTematicaId Identificador de la línea seleccionada.
   * @returns Observable con indicadores disponibles para carga.
   */
  listarIndicadores(lineaTematicaId: number): Observable<{ indicadores: IndicadorItem[] }> {
    return this.http.get<{ indicadores: IndicadorItem[] }>(`${environment.apiUrl}/indicadores`, {
      params: { linea_tematica_id: lineaTematicaId }
    });
  }

  /**
   * Ejecuta la prevalidación estructural y de datos de un archivo Excel OSC.
   * @param form FormData con archivo, línea temática, indicador y observaciones opcionales.
   * @returns Observable con resultado de validación y errores detallados.
   */
  validar(form: FormData): Observable<ValidacionArchivoResponse> {
    return this.http.post<ValidacionArchivoResponse>(`${this.base}/validar`, form);
  }

  /**
   * Envía a procesamiento un archivo previamente validado.
   * @param archivoId Identificador del archivo en estado válido.
   * @returns Observable con mensaje de confirmación del servidor.
   */
  enviar(archivoId: number): Observable<{ mensaje?: string; ok?: boolean }> {
    return this.http.post<{ mensaje?: string; ok?: boolean }>(`${this.base}/enviar`, {
      archivo_id: archivoId
    });
  }

  /**
   * Obtiene metadatos y errores de validación de un archivo.
   * @param id Identificador del archivo.
   * @returns Observable con detalle ampliado del archivo.
   */
  obtenerDetalle(id: number): Observable<ArchivoItem & { errores_validacion?: string[] }> {
    return this.http.get<ArchivoItem & { errores_validacion?: string[] }>(`${this.base}/${id}`);
  }

  /**
   * Descarga el archivo original almacenado en el servidor.
   * @param id Identificador del archivo.
   * @returns Observable con la respuesta HTTP y cuerpo blob.
   */
  descargar(id: number): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/${id}/descargar`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  /**
   * Elimina un archivo que aún no fue enviado o procesado.
   * @param id Identificador del archivo.
   * @returns Observable con confirmación de eliminación.
   */
  eliminar(id: number): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.base}/${id}`);
  }

  /**
   * Flujo administrativo: envía el archivo y lo aprueba en un solo paso.
   * @param archivoId Identificador del archivo pendiente de validación.
   * @param observaciones Comentarios opcionales del revisor.
   * @returns Observable con resultado de aprobación y conteo de errores si aplica.
   */
  enviarYAprobar(
    archivoId: number,
    observaciones?: string
  ): Observable<{
    ok?: boolean;
    mensaje?: string;
    aprobado?: boolean;
    procesamiento_valido?: boolean;
    total_errores?: number;
  }> {
    return this.http.post<{
      ok?: boolean;
      mensaje?: string;
      aprobado?: boolean;
      procesamiento_valido?: boolean;
      total_errores?: number;
    }>(`${this.base}/${archivoId}/enviar-y-aprobar`, { observaciones: observaciones ?? null });
  }

  /**
   * Rechaza la prevalidación de un archivo con observaciones del revisor.
   * @param archivoId Identificador del archivo.
   * @param observaciones Motivo del rechazo (obligatorio en UI).
   * @returns Observable con confirmación.
   */
  rechazarValidacion(archivoId: number, observaciones: string): Observable<{ ok: boolean }> {
    return this.http.post<{ ok: boolean }>(`${this.base}/${archivoId}/rechazar-validacion`, {
      observaciones
    });
  }
}
