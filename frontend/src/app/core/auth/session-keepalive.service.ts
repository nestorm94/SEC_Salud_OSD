import { Injectable, inject, OnDestroy } from '@angular/core';
import { AuthService } from './auth.service';

/** Renueva el JWT antes de que expire mientras el usuario sigue activo en la aplicación. */
const CHECK_INTERVAL_MS = 5 * 60 * 1000;
const REFRESH_BEFORE_MS = 90 * 60 * 1000;
const MAX_IDLE_MS = 45 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class SessionKeepAliveService implements OnDestroy {
  private readonly auth = inject(AuthService);

  private timer: ReturnType<typeof setInterval> | null = null;
  private lastActivity = Date.now();
  private refreshing = false;
  private started = false;

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

  stop(): void {
    this.started = false;
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
  }

  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.evaluate();
    }
  };

  private bindActivity(): void {
    const bump = () => {
      this.lastActivity = Date.now();
    };
    for (const ev of ['click', 'keydown', 'mousemove', 'scroll', 'touchstart'] as const) {
      document.addEventListener(ev, bump, { passive: true });
    }
  }

  private evaluate(): void {
    if (!this.auth.getToken()) return;
    if (document.visibilityState === 'hidden') return;
    if (Date.now() - this.lastActivity > MAX_IDLE_MS) return;

    const remaining = this.auth.getTokenRemainingMs();
    if (remaining === null || remaining <= 0 || remaining > REFRESH_BEFORE_MS) return;

    this.tryRefresh();
  }

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
