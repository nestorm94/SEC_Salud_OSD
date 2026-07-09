import { Injectable, inject, OnDestroy } from '@angular/core';
import { AuthService } from './auth.service';

/** Intervalo entre evaluaciones de renovación de token (5 minutos). */
const CHECK_INTERVAL_MS = 5 * 60 * 1000;
/** Renovar el JWT cuando queden menos de 90 minutos para expirar. */
const REFRESH_BEFORE_MS = 90 * 60 * 1000;
/** Tiempo máximo de inactividad antes de dejar de renovar (45 minutos). */
const MAX_IDLE_MS = 45 * 60 * 1000;

/**
 * Mantiene viva la sesión del OSD renovando el JWT automáticamente
 * mientras el usuario permanece activo y la pestaña está visible.
 */
@Injectable({ providedIn: 'root' })
export class SessionKeepAliveService implements OnDestroy {
  private readonly auth = inject(AuthService);

  private timer: ReturnType<typeof setInterval> | null = null;
  private lastActivity = Date.now();
  private refreshing = false;
  private started = false;

  /**
   * Inicia el monitoreo de actividad y el temporizador de renovación.
   * Es idempotente: no crea listeners duplicados si ya está activo.
   */
  start(): void {
    if (this.started) return;
    this.started = true;
    this.lastActivity = Date.now();
    this.bindActivity();
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    this.timer = setInterval(() => this.evaluate(), CHECK_INTERVAL_MS);
    this.evaluate();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  /** Detiene temporizador y listeners de actividad. */
  stop(): void {
    this.started = false;
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  /** Al volver a la pestaña, reevalúa si conviene renovar el token. */
  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.evaluate();
    }
  };

  /** Registra eventos de interacción del usuario para detectar inactividad. */
  private bindActivity(): void {
    const bump = () => {
      this.lastActivity = Date.now();
    };
    for (const ev of ['click', 'keydown', 'mousemove', 'scroll', 'touchstart'] as const) {
      document.addEventListener(ev, bump, { passive: true });
    }
  }

  /**
   * Decide si debe solicitarse un refresh del JWT según expiración,
   * visibilidad de la pestaña y tiempo desde la última actividad.
   */
  private evaluate(): void {
    if (!this.auth.getToken()) return;
    if (document.visibilityState === 'hidden') return;
    if (Date.now() - this.lastActivity > MAX_IDLE_MS) return;

    const remaining = this.auth.getTokenRemainingMs();
    if (remaining === null || remaining <= 0 || remaining > REFRESH_BEFORE_MS) return;

    this.tryRefresh();
  }

  /** Ejecuta la renovación evitando peticiones concurrentes. */
  private tryRefresh(): void {
    if (this.refreshing) return;
    this.refreshing = true;
    this.auth.refreshToken().subscribe({
      next: () => {
        this.refreshing = false;
      },
      error: () => {
        this.refreshing = false;
      }
    });
  }
}
