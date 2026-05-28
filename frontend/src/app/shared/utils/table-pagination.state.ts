import { computed, Signal, signal } from '@angular/core';
import { computeTotalPages, paginateSlice, TABLE_PAGE_SIZE } from './table-pagination.util';

/** Paginación en cliente para listas cargadas completas. */
export function tablePagination<T>(source: Signal<readonly T[]>) {
  const page = signal(1);
  const paginated = computed(() => paginateSlice(source(), page()));
  const totalPages = computed(() => computeTotalPages(source().length));
  const totalItems = computed(() => source().length);

  const resetPage = () => page.set(1);
  const setPage = (p: number) => {
    const max = totalPages();
    page.set(Math.min(Math.max(1, p), max));
  };

  return { page, paginated, totalPages, totalItems, resetPage, setPage, pageSize: TABLE_PAGE_SIZE };
}
