import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CampoPlantilla, Plantilla } from '../../../shared/models/api.models';

/** Datos para crear o actualizar una plantilla OSC. */
export interface PlantillaDto {
  codigo: string;
  nombre: string;
  descripcion?: string;
  dependencia_id?: number;
  activo: boolean;
}

/** Definición de un campo/columna dentro de una plantilla Excel OSC. */
export interface CampoPlantillaDto {
  nombre_campo: string;
  tipo_dato: string;
  obligatorio: boolean;
  descripcion?: string;
  longitud?: number;
  formato?: string;
  valores_permitidos?: string;
  orden: number;
}

/**
 * Servicio de administración de plantillas OSC del OSD.
 * Gestiona plantillas y su esquema de campos para validación de archivos.
 */
@Injectable({ providedIn: 'root' })
export class PlantillasService {
  private readonly http = inject(HttpClient);

  /**
   * Lista plantillas registradas con conteo de campos.
   * @returns Observable con arreglo de plantillas.
   */
  listar(): Observable<{ plantillas: Plantilla[] }> {
    return this.http.get<{ plantillas: Plantilla[] }>(`${environment.apiUrl}/admin/plantillas`);
  }

  /**
   * Crea una plantilla nueva.
   * @param dto Metadatos de la plantilla.
   * @returns Observable con respuesta del servidor.
   */
  crear(dto: PlantillaDto): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/admin/plantillas`, dto);
  }

  /**
   * Actualiza metadatos de una plantilla.
   * @param id Identificador de la plantilla.
   * @param dto Datos actualizados.
   * @returns Observable con respuesta del servidor.
   */
  actualizar(id: number, dto: PlantillaDto): Observable<unknown> {
    return this.http.put(`${environment.apiUrl}/admin/plantillas/${id}`, dto);
  }

  /**
   * Lista campos definidos para una plantilla.
   * @param plantillaId Identificador de la plantilla.
   * @returns Observable con esquema de campos ordenados.
   */
  listarCampos(plantillaId: number): Observable<{ campos: CampoPlantilla[] }> {
    return this.http.get<{ campos: CampoPlantilla[] }>(
      `${environment.apiUrl}/admin/plantillas/${plantillaId}/campos`
    );
  }

  /**
   * Añade un campo al esquema de la plantilla.
   * @param plantillaId Identificador de la plantilla.
   * @param dto Definición del campo (tipo, obligatoriedad, orden).
   * @returns Observable con respuesta del servidor.
   */
  crearCampo(plantillaId: number, dto: CampoPlantillaDto): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/admin/plantillas/${plantillaId}/campos`, dto);
  }

  /**
   * Elimina un campo del esquema de plantilla.
   * @param campoId Identificador del campo.
   * @returns Observable con respuesta del servidor.
   */
  eliminarCampo(campoId: number): Observable<unknown> {
    return this.http.delete(`${environment.apiUrl}/admin/plantillas/campos/${campoId}`);
  }
}
