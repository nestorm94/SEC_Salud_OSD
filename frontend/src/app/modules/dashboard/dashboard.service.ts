import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { DashboardResumen } from '../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  getResumen() {
    return this.http.get<DashboardResumen>(`${environment.apiUrl}/dashboard/resumen`);
  }
}
