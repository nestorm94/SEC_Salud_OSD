import { HttpErrorResponse } from '@angular/common/http';

/**
 * Traduce un error HTTP a un mensaje legible para el usuario.
 * @param err Error capturado (típicamente `HttpErrorResponse`).
 * @param fallback Mensaje por defecto si no se puede determinar uno específico.
 * @returns Texto descriptivo listo para mostrar en la interfaz.
 */
export function mapHttpErrorMessage(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) {
    return fallback;
  }

  if (err.status === 0) {
    return 'No se pudo conectar con el servidor. Verifique que la API esté en ejecución (puerto 5289).';
  }

  if (err.status === 401) {
    return 'Sesión expirada o no autorizada. Inicie sesión nuevamente.';
  }

  if (err.status === 403) {
    return 'No tiene permisos para ver esta información.';
  }

  if (typeof err.error === 'object' && err.error && 'error' in err.error) {
    const msg = (err.error as { error?: string }).error;
    if (msg) return msg;
  }

  if (typeof err.error === 'string' && err.error.trim()) {
    return err.error;
  }

  return fallback;
}
