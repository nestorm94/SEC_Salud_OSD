import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CatalogoSimpleDto, DepartamentoDto, MunicipioDto } from '../../shared/models/api.models';

@Injectable({ providedIn: 'root' })
export class CatalogoService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getDepartamentos() {
    return this.http.get<{ departamentos: DepartamentoDto[] }>(
      `${this.base}/catalogos/departamentos`
    );
  }

  getMunicipios() {
    return this.http.get<{ municipios: MunicipioDto[] }>(
      `${this.base}/catalogos/municipios`
    );
  }

  getMunicipiosPorDepartamento(codigoDepartamento: string) {
    return this.http.get<{ municipios: MunicipioDto[] }>(
      `${this.base}/catalogos/municipios/${encodeURIComponent(codigoDepartamento)}`
    );
  }

  getRegionales() {
    return this.http.get<{ regionales: CatalogoSimpleDto[] }>(
      `${this.base}/catalogos/regionales`
    );
  }

  getAreas() {
    return this.http.get<{ areas: CatalogoSimpleDto[] }>(`${this.base}/catalogos/areas`);
  }

  getSexos() {
    return this.http.get<{ sexos: CatalogoSimpleDto[] }>(`${this.base}/catalogos/sexos`);
  }

  getAnios() {
    return this.http.get<{ anios: CatalogoSimpleDto[] }>(`${this.base}/catalogos/anios`);
  }
}

