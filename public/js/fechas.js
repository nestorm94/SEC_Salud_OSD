/** Zona horaria del observatorio (fechas almacenadas en UTC en SQL Server). */
const ZONA_COLOMBIA = "America/Bogota";

/**
 * Convierte valores ISO de la API a Date.
 * Sin indicador de zona (SYSUTCDATETIME) se interpretan como UTC.
 */
export function parseFechaApi(valor) {
  if (valor == null || valor === "") return null;
  const s = String(valor).trim();
  if (!s) return null;
  const tieneZona = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(s);
  const iso = tieneZona ? s : s.includes("T") ? `${s}Z` : s;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** Solo fecha (dd/mm/aaaa) — para columnas «Validación», «Envío», «Fecha». */
export function formatearSoloFecha(valor) {
  const d = parseFechaApi(valor);
  if (!d) return "—";
  return d.toLocaleDateString("es-CO", {
    timeZone: ZONA_COLOMBIA,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** Fecha y hora — para auditoría o historial de eventos. */
export function formatearFechaHora(valor) {
  const d = parseFechaApi(valor);
  if (!d) return "—";
  return d.toLocaleString("es-CO", {
    timeZone: ZONA_COLOMBIA,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });
}
