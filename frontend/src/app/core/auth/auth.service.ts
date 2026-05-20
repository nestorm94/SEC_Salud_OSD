import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, UsuarioSesion } from '../../shared/models/api.models';

const TOKEN_KEY = 'observatorios.token';
const USER_KEY = 'observatorios.usuario';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _usuario = signal<UsuarioSesion | null>(this.loadUsuario());
  readonly usuario = this._usuario.asReadonly();
  readonly isAuthenticated = computed(() => !!this.getToken());
  readonly isAdmin = computed(() => this.checkAdmin(this._usuario()));

  login(credentials: LoginRequest) {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, credentials).pipe(
      tap((res) => {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.usuario));
        this._usuario.set(res.usuario);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._usuario.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getUsuario(): UsuarioSesion | null {
    return this._usuario();
  }

  isAdminUser(): boolean {
    return this.checkAdmin(this._usuario());
  }

  private checkAdmin(user: UsuarioSesion | null): boolean {
    if (!user?.roles?.length) return false;
    return user.roles.some(
      (r) => r.toUpperCase() === 'ADMINISTRADOR' || r.toUpperCase() === 'ADMIN'
    );
  }

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
