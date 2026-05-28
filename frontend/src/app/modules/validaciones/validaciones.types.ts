export type ValidacionOrigen = 'carga' | 'archivo';

export interface ValidacionFila {
  origen: ValidacionOrigen;
  id: number;
  archivo: string;
  dependencia: string;
  estado: string;
  estadoEtiqueta: string;
  total_errores: number;
  usuario: string;
}

export function normalizarEstadoCarga(estado: string): string {
  const e = (estado || '').trim().toUpperCase();
  if (e === 'SUBIDO') return 'RECIBIDO';
  if (e === 'VALIDANDO') return 'EN_VALIDACION';
  if (e === 'VALIDADO_OK') return 'VALIDADO_EXITOSO';
  return e;
}

export function esPendienteValidacion(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    const n = normalizarEstadoCarga(fila.estado);
    return n === 'VALIDADO_EXITOSO' || n === 'EN_VALIDACION' || n === 'RECIBIDO';
  }
  return fila.estado.toLowerCase() === 'validado';
}

export function esConErroresValidacion(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    return normalizarEstadoCarga(fila.estado) === 'VALIDADO_CON_ERRORES';
  }
  return fila.estado.toLowerCase() === 'rechazado';
}

export function puedeAprobarFila(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    return normalizarEstadoCarga(fila.estado) === 'VALIDADO_EXITOSO';
  }
  return fila.estado.toLowerCase() === 'validado';
}
