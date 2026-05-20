import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Rol } from '../../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);

  listar() {
    return this.http.get<{ roles: Rol[] }>(`${environment.apiUrl}/admin/roles`);
  }
}
