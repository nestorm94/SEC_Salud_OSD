/** Tamaño máximo de filas por página en tablas del portal. */
export const TABLE_PAGE_SIZE = 10;

export function paginateSlice<T>(items: readonly T[], page: number, pageSize = TABLE_PAGE_SIZE): T[] {
  const p = Math.max(1, page);
  const start = (p - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

export function computeTotalPages(totalItems: number, pageSize = TABLE_PAGE_SIZE): number {
  if (totalItems <= 0) return 1;
  return Math.ceil(totalItems / pageSize);
}
