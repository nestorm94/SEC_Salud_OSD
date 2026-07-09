/**
 * @fileoverview Formateo de fechas UTC de la API a zona horaria de Colombia.
 * Utilidad compartida del portal HTML legacy del OSD.
 */
/** Zona horaria del observatorio (fechas almacenadas en UTC en SQL Server). */
const ZONA_COLOMBIA = "America/Bogota";

/**
 * Convierte valores ISO de la API a Date.
 * Sin indicador de zona (SYSUTCDATETIME) se interpretan como UTC.
 * @param {string|Date|null|undefined} valor - Fecha en formato ISO o similar.
 * @returns {Date|null}
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

/**
 * Formatea solo la fecha (dd/mm/aaaa) en zona Colombia.
 * @param {string|Date|null|undefined} valor - Valor de fecha de la API.
 * @returns {string} Fecha formateada o "—" si no es válida.
 */
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

/**
 * Formatea fecha y hora en zona Colombia (auditoría, historial de eventos).
 * @param {string|Date|null|undefined} valor - Valor de fecha de la API.
 * @returns {string} Fecha y hora formateadas o "—" si no es válida.
 */
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
