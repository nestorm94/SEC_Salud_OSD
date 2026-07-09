import { computed, Signal, signal } from '@angular/core';
import { computeTotalPages, paginateSlice, TABLE_PAGE_SIZE } from './table-pagination.util';

/**
 * Crea señales reactivas para paginar en cliente una lista completa.
 * @param source Señal con todos los ítems de la tabla.
 * @returns Objeto con página actual, rebanada paginada, totales y acciones de navegación.
 */
export function tablePagination<T>(source: Signal<readonly T[]>) {
  const page = signal(1);
  const paginated = computed(() => paginateSlice(source(), page()));
  const totalPages = computed(() => computeTotalPages(source().length));
  const totalItems = computed(() => source().length);

  /** Reinicia la paginación a la primera página. */
  const resetPage = () => page.set(1);

  /**
   * Establece la página activa acotada al rango válido.
   * @param p Número de página destino (base 1).
   */
  const setPage = (p: number) => {
    const max = totalPages();
    page.set(Math.min(Math.max(1, p), max));
  };

  return { page, paginated, totalPages, totalItems, resetPage, setPage, pageSize: TABLE_PAGE_SIZE };
}
