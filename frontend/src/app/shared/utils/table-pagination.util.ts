/** Tamaño máximo de filas por página en tablas del portal. */
export const TABLE_PAGE_SIZE = 10;

/**
 * Devuelve la porción de ítems correspondiente a una página.
 * @param items Lista completa a paginar.
 * @param page Número de página (base 1).
 * @param pageSize Cantidad de filas por página.
 */
export function paginateSlice<T>(items: readonly T[], page: number, pageSize = TABLE_PAGE_SIZE): T[] {
  const p = Math.max(1, page);
  const start = (p - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

/**
 * Calcula el total de páginas a partir del número de registros.
 * @param totalItems Cantidad total de ítems.
 * @param pageSize Cantidad de filas por página.
 */
export function computeTotalPages(totalItems: number, pageSize = TABLE_PAGE_SIZE): number {
  if (totalItems <= 0) return 1;
  return Math.ceil(totalItems / pageSize);
}
