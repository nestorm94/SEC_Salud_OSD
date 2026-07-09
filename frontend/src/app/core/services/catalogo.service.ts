import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CatalogoSimpleDto, DepartamentoDto, MunicipioDto } from '../../shared/models/api.models';

/**
 * Servicio de catálogos territoriales y demográficos del OSD.
 * Centraliza las consultas a endpoints de departamentos, municipios, regionales, áreas, sexos y años.
 */
@Injectable({ providedIn: 'root' })
export class CatalogoService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /**
   * Lista todos los departamentos disponibles (código DANE y nombre).
   * @returns Observable con el arreglo de departamentos.
   */
  getDepartamentos(): Observable<{ departamentos: DepartamentoDto[] }> {
    return this.http.get<{ departamentos: DepartamentoDto[] }>(
      `${this.base}/catalogos/departamentos`
    );
  }

  /**
   * Lista todos los municipios del catálogo nacional.
   * @returns Observable con el arreglo de municipios.
   */
  getMunicipios(): Observable<{ municipios: MunicipioDto[] }> {
    return this.http.get<{ municipios: MunicipioDto[] }>(
      `${this.base}/catalogos/municipios`
    );
  }

  /**
   * Filtra municipios pertenecientes a un departamento dado.
   * @param codigoDepartamento Código DANE del departamento (ej. "85" para Casanare).
   * @returns Observable con municipios del departamento.
   */
  getMunicipiosPorDepartamento(codigoDepartamento: string): Observable<{ municipios: MunicipioDto[] }> {
    return this.http.get<{ municipios: MunicipioDto[] }>(
      `${this.base}/catalogos/municipios/${encodeURIComponent(codigoDepartamento)}`
    );
  }

  /**
   * Obtiene las regionales de salud configuradas en el sistema.
   * @returns Observable con catálogo de regionales.
   */
  getRegionales(): Observable<{ regionales: CatalogoSimpleDto[] }> {
    return this.http.get<{ regionales: CatalogoSimpleDto[] }>(
      `${this.base}/catalogos/regionales`
    );
  }

  /**
   * Obtiene las áreas geográficas (urbana/rural) del catálogo.
   * @returns Observable con catálogo de áreas.
   */
  getAreas(): Observable<{ areas: CatalogoSimpleDto[] }> {
    return this.http.get<{ areas: CatalogoSimpleDto[] }>(`${this.base}/catalogos/areas`);
  }

  /**
   * Obtiene los valores de sexo para filtros de proyección poblacional.
   * @returns Observable con catálogo de sexos.
   */
  getSexos(): Observable<{ sexos: CatalogoSimpleDto[] }> {
    return this.http.get<{ sexos: CatalogoSimpleDto[] }>(`${this.base}/catalogos/sexos`);
  }

  /**
   * Obtiene los años disponibles para consultas históricas o de proyección.
   * @returns Observable con catálogo de años.
   */
  getAnios(): Observable<{ anios: CatalogoSimpleDto[] }> {
    return this.http.get<{ anios: CatalogoSimpleDto[] }>(`${this.base}/catalogos/anios`);
  }
}
