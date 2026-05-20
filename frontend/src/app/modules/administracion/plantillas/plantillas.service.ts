import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CampoPlantilla, Plantilla } from '../../../shared/models/api.models';

export interface PlantillaDto {
  codigo: string;
  nombre: string;
  descripcion?: string;
  dependencia_id?: number;
  activo: boolean;
}

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

@Injectable({ providedIn: 'root' })
export class PlantillasService {
  private readonly http = inject(HttpClient);

  listar() {
    return this.http.get<{ plantillas: Plantilla[] }>(`${environment.apiUrl}/admin/plantillas`);
  }

  crear(dto: PlantillaDto) {
    return this.http.post(`${environment.apiUrl}/admin/plantillas`, dto);
  }

  actualizar(id: number, dto: PlantillaDto) {
    return this.http.put(`${environment.apiUrl}/admin/plantillas/${id}`, dto);
  }

  listarCampos(plantillaId: number) {
    return this.http.get<{ campos: CampoPlantilla[] }>(
      `${environment.apiUrl}/admin/plantillas/${plantillaId}/campos`
    );
  }

  crearCampo(plantillaId: number, dto: CampoPlantillaDto) {
    return this.http.post(`${environment.apiUrl}/admin/plantillas/${plantillaId}/campos`, dto);
  }

  eliminarCampo(campoId: number) {
    return this.http.delete(`${environment.apiUrl}/admin/plantillas/campos/${campoId}`);
  }
}
