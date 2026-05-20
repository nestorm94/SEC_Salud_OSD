import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Dependencia } from '../../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class DependenciasService {
  private readonly http = inject(HttpClient);

  listar() {
    return this.http.get<{ dependencias: Dependencia[] }>(`${environment.apiUrl}/dependencias`);
  }

  crear(codigo: string, nombre: string) {
    return this.http.post(`${environment.apiUrl}/dependencias`, { codigo, nombre });
  }
}
