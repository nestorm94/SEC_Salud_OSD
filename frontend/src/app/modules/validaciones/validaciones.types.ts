/** Origen del registro en el módulo de validaciones. */
export type ValidacionOrigen = 'carga' | 'archivo';

/**
 * Fila unificada de la bandeja de validaciones,
 * ya provenga de una carga masiva o de un archivo individual.
 */
export interface ValidacionFila {
  /** Tipo de entidad de la que proviene el registro. */
  origen: ValidacionOrigen;
  /** Identificador numérico de la carga o del archivo. */
  id: number;
  /** Nombre del archivo asociado al registro. */
  archivo: string;
  /** Dependencia o entidad que reportó los datos. */
  dependencia: string;
  /** Estado crudo devuelto por la API. */
  estado: string;
  /** Etiqueta legible del estado para mostrar en la interfaz. */
  estadoEtiqueta: string;
  /** Cantidad de errores detectados durante la validación. */
  total_errores: number;
  /** Usuario que cargó o registró el archivo. */
  usuario: string;
}

/**
 * Normaliza estados legacy de cargas al vocabulario actual del flujo.
 * @param estado Valor crudo devuelto por la API.
 * @returns Estado homologado en mayúsculas.
 */
export function normalizarEstadoCarga(estado: string): string {
  const e = (estado || '').trim().toUpperCase();
  if (e === 'SUBIDO') return 'RECIBIDO';
  if (e === 'VALIDANDO') return 'EN_VALIDACION';
  if (e === 'VALIDADO_OK') return 'VALIDADO_EXITOSO';
  return e;
}

/**
 * Indica si el registro aún está pendiente de revisión por un validador.
 * @param fila Registro de la bandeja de validaciones.
 */
export function esPendienteValidacion(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    const n = normalizarEstadoCarga(fila.estado);
    return n === 'VALIDADO_EXITOSO' || n === 'EN_VALIDACION' || n === 'RECIBIDO';
  }
  return fila.estado.toLowerCase() === 'validado';
}

/**
 * Indica si el registro fue validado pero contiene errores o fue rechazado.
 * @param fila Registro de la bandeja de validaciones.
 */
export function esConErroresValidacion(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    return normalizarEstadoCarga(fila.estado) === 'VALIDADO_CON_ERRORES';
  }
  return fila.estado.toLowerCase() === 'rechazado';
}

/**
 * Indica si el validador puede aprobar el registro según su origen y estado.
 * @param fila Registro de la bandeja de validaciones.
 */
export function puedeAprobarFila(fila: ValidacionFila): boolean {
  if (fila.origen === 'carga') {
    return normalizarEstadoCarga(fila.estado) === 'VALIDADO_EXITOSO';
  }
  return fila.estado.toLowerCase() === 'validado';
}
