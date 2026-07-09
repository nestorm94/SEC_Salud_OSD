import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardResumen } from '../../shared/models/api.models';

/**
 * Servicio del panel principal del OSD.
 * Obtiene el resumen de archivos, cargues y actividad reciente.
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  /**
   * Consulta métricas agregadas para las tarjetas y tabla del dashboard.
   * @returns Observable con totales y últimos cargues.
   */
  getResumen(): Observable<DashboardResumen> {
    return this.http.get<DashboardResumen>(`${environment.apiUrl}/dashboard/resumen`);
  }
}
