import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/**
 * Guard funcional que protege rutas del layout principal del OSD.
 * Redirige a login si no hay token o si el JWT expiró.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.getToken()) {
    return router.createUrlTree(['/login']);
  }
  if (auth.isTokenExpired()) {
    auth.logout({ sessionExpired: true });
    return router.createUrlTree(['/login'], { queryParams: { expired: '1' } });
  }
  return true;
};
