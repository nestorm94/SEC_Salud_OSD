import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, finalize, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

let refreshRequest: ReturnType<AuthService['refreshToken']> | null = null;

/** Ante 401 intenta renovar el token una vez; si falla, cierra sesión. */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const isAuthRoute = req.url.includes('/auth/login') || req.url.includes('/auth/refresh');
      if (err.status !== 401 || isAuthRoute) {
        return throwError(() => err);
      }

      if (!auth.getToken()) {
        auth.logout({ sessionExpired: true });
        return throwError(() => err);
      }

      refreshRequest ??= auth.refreshToken().pipe(
        finalize(() => {
          refreshRequest = null;
        })
      );

      return refreshRequest.pipe(
        switchMap(() => {
          const token = auth.getToken();
          if (!token) {
            auth.logout({ sessionExpired: true });
            return throwError(() => err);
          }
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
        }),
        catchError((refreshErr) => {
          auth.logout({ sessionExpired: true });
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
