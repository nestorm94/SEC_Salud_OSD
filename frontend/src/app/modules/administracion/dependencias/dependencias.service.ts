import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Dependencia } from '../../../shared/models/api.models';

/**
 * Servicio de dependencias organizacionales del OSD.
 * Las dependencias son entidades que reportan archivos e indicadores al observatorio.
 */
@Injectable({ providedIn: 'root' })
export class DependenciasService {
  private readonly http = inject(HttpClient);

  /**
   * Lista dependencias registradas en el sistema.
   * @returns Observable con arreglo de dependencias.
   */
  listar(): Observable<{ dependencias: Dependencia[] }> {
    return this.http.get<{ dependencias: Dependencia[] }>(`${environment.apiUrl}/dependencias`);
  }

  /**
   * Registra una nueva dependencia.
   * @param codigo Código corto identificador.
   * @param nombre Nombre descriptivo de la dependencia.
   * @returns Observable con respuesta del servidor.
   */
  crear(codigo: string, nombre: string): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/dependencias`, { codigo, nombre });
  }
}
