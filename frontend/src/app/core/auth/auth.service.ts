import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, UsuarioSesion } from '../../shared/models/api.models';

const TOKEN_KEY = 'observatorios.token';
const USER_KEY = 'observatorios.usuario';

/**
 * Servicio central de autenticación del OSD.
 * Gestiona login, logout, renovación de JWT y estado reactivo de la sesión del usuario.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _usuario = signal<UsuarioSesion | null>(this.loadUsuario());
  /** Usuario autenticado actual (solo lectura). */
  readonly usuario = this._usuario.asReadonly();
  /** Indica si existe un token JWT almacenado. */
  readonly isAuthenticated = computed(() => !!this.getToken());
  /** Indica si el usuario tiene rol de administrador. */
  readonly isAdmin = computed(() => this.checkAdmin(this._usuario()));

  /**
   * Autentica al usuario contra la API y persiste token y perfil en localStorage.
   * @param credentials Usuario y contraseña del formulario de login.
   * @returns Observable con la respuesta del servidor (token y datos de usuario).
   */
  login(credentials: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, credentials).pipe(
      tap((res) => {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.usuario));
        this._usuario.set(res.usuario);
      })
    );
  }

  /**
   * Cierra la sesión local y redirige al login.
   * @param options.sessionExpired Si es true, muestra mensaje de sesión expirada en la URL.
   */
  logout(options?: { sessionExpired?: boolean }): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._usuario.set(null);
    this.router.navigate(['/login'], {
      queryParams: options?.sessionExpired ? { expired: '1' } : {}
    });
  }

  /**
   * Obtiene el JWT almacenado en localStorage.
   * @returns Token Bearer o null si no hay sesión.
   */
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  /**
   * Devuelve el perfil del usuario en memoria.
   * @returns Datos de sesión o null si no está autenticado.
   */
  getUsuario(): UsuarioSesion | null {
    return this._usuario();
  }

  /**
   * Comprueba si el usuario actual tiene privilegios de administrador.
   * @returns true si posee rol ADMIN, ADMINISTRADOR o ADMINISTRATOR.
   */
  isAdminUser(): boolean {
    return this.checkAdmin(this._usuario());
  }

  /**
   * Sincroniza usuario y roles desde el endpoint /auth/me.
   * Útil tras cambios de permisos en el servidor sin reautenticar.
   * @returns Observable vacío si no hay token; en caso contrario, actualiza el perfil local.
   */
  refrescarSesion(): Observable<{ usuario: UsuarioSesion }> | void {
    if (!this.getToken()) return;
    return this.http.get<{ usuario: UsuarioSesion }>(`${environment.apiUrl}/auth/me`).pipe(
      tap((res) => {
        const u = res.usuario;
        if (u) {
          localStorage.setItem(USER_KEY, JSON.stringify(u));
          this._usuario.set(u);
        }
      })
    );
  }

  /**
   * Renueva el JWT mientras la sesión sigue activa, sin pedir contraseña.
   * @returns Observable con el nuevo token y perfil de usuario.
   */
  refreshToken(): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, {}).pipe(
      tap((res) => {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.usuario));
        this._usuario.set(res.usuario);
      })
    );
  }

  /**
   * Calcula el tiempo restante antes de expirar el JWT.
   * @returns Milisegundos hasta expiración, o null si no hay token o no se puede decodificar.
   */
  getTokenRemainingMs(): number | null {
    const exp = this.getTokenExpirationEpochMs();
    return exp === null ? null : exp - Date.now();
  }

  /**
   * Indica si el JWT actual ya expiró o no es legible.
   * @returns true si no hay tiempo restante válido.
   */
  isTokenExpired(): boolean {
    const remaining = this.getTokenRemainingMs();
    return remaining === null || remaining <= 0;
  }

  /** Decodifica el claim `exp` del payload JWT (base64). */
  private getTokenExpirationEpochMs(): number | null {
    const token = this.getToken();
    if (!token) return null;
    const parts = token.split('.');
    if (parts.length < 2) return null;
    try {
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const payload = JSON.parse(atob(base64)) as { exp?: number };
      return typeof payload.exp === 'number' ? payload.exp * 1000 : null;
    } catch {
      return null;
    }
  }

  /** Evalúa si los roles del usuario incluyen alguno de administrador. */
  private checkAdmin(user: UsuarioSesion | null): boolean {
    if (!user?.roles?.length) return false;
    return user.roles.some((r) => {
      const n = String(r).trim().toUpperCase();
      return n === 'ADMIN' || n === 'ADMINISTRADOR' || n === 'ADMINISTRATOR';
    });
  }

  /** Restaura el usuario desde localStorage al iniciar la aplicación. */
  private loadUsuario(): UsuarioSesion | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UsuarioSesion;
    } catch {
      return null;
    }
  }
}
