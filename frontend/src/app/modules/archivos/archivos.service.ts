import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ArchivoItem } from '../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class ArchivosService {
  private readonly http = inject(HttpClient);

  listar() {
    return this.http.get<{ archivos: ArchivoItem[] }>(`${environment.apiUrl}/archivos`);
  }

  descargar(id: number) {
    return this.http.get(`${environment.apiUrl}/archivos/${id}/descargar`, {
      responseType: 'blob',
      observe: 'response'
    });
  }

  eliminar(id: number) {
    return this.http.delete<{ ok: boolean }>(`${environment.apiUrl}/archivos/${id}`);
  }

  subirExcel(archivo: File, dependenciaId?: number) {
    const form = new FormData();
    form.append('archivo', archivo);
    if (dependenciaId != null) {
      form.append('dependencia_id', String(dependenciaId));
    }
    return this.http.post(`${environment.apiUrl}/cargas/excel`, form);
  }
}
