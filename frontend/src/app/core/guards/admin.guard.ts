import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/**
 * Guard funcional para el módulo de administración del OSD.
 * Solo permite el acceso a usuarios autenticados con rol de administrador.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated() && auth.isAdminUser()) {
    return true;
  }
  return router.createUrlTree(['/dashboard']);
};
