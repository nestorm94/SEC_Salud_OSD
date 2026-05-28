import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  ArchivoItem,
  IndicadorItem,
  LineaTematicaItem,
  ValidacionArchivoResponse
} from '../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class ArchivosService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/archivos`;

  listar() {
    return this.http.get<{ archivos: ArchivoItem[] }>(this.base);
  }

  listarLineasTematicas() {
    return this.http.get<{ lineas_tematicas: LineaTematicaItem[] }>(
      `${environment.apiUrl}/lineas-tematicas`
    );
  }

  listarIndicadores(lineaTematicaId: number) {
    return this.http.get<{ indicadores: IndicadorItem[] }>(`${environment.apiUrl}/indicadores`, {
      params: { linea_tematica_id: lineaTematicaId }
    });
  }

  validar(form: FormData) {
    return this.http.post<ValidacionArchivoResponse>(`${this.base}/validar`, form);
  }

  enviar(archivoId: number) {
    return this.http.post<{ mensaje?: string; ok?: boolean }>(`${this.base}/enviar`, {
      archivo_id: archivoId
    });
  }

  obtenerDetalle(id: number) {
    return this.http.get<ArchivoItem & { errores_validacion?: string[] }>(`${this.base}/${id}`);
  }

  descargar(id: number) {
    return this.http.get(`${this.base}/${id}/descargar`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  eliminar(id: number) {
    return this.http.delete<{ ok: boolean }>(`${this.base}/${id}`);
  }

  enviarYAprobar(archivoId: number, observaciones?: string) {
    return this.http.post<{
      ok?: boolean;
      mensaje?: string;
      aprobado?: boolean;
      procesamiento_valido?: boolean;
      total_errores?: number;
    }>(`${this.base}/${archivoId}/enviar-y-aprobar`, { observaciones: observaciones ?? null });
  }

  rechazarValidacion(archivoId: number, observaciones: string) {
    return this.http.post<{ ok: boolean }>(`${this.base}/${archivoId}/rechazar-validacion`, {
      observaciones
    });
  }
}
