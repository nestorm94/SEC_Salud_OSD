/**
 * Base de la API. Orden: localStorage → meta observatorios-api-base → reglas por puerto.
 *
 * Si abre el HTML con Live Server / Five Server (puerto 5500, 5173, etc.), el "origen" de la
 * página NO es Kestrel: hay que llamar a http://localhost:5289 (o el puerto donde corre dotnet run).
 */
const PUERTOS_SOLO_FRONTEND = new Set([
  "5500", "5501", "8080", "8081", "3000", "5173", "4173", "4200", "8888",
]);

/** Puertos donde esta misma app ASP.NET suele servir HTML + API a la vez */
const PUERTOS_API_LOCAL = new Set(["5289", "5290", "7236"]);

function sinBarraFinal(s) {
  return s.replace(/\/+$/, "");
}

export function apiBaseUrl() {
  if (typeof window === "undefined") return "";

  try {
    const ls = localStorage.getItem("observatorios.apiOrigen");
    if (ls && /^https?:\/\//i.test(ls)) return sinBarraFinal(ls);
  } catch {
    /* private mode */
  }

  const meta = document.querySelector('meta[name="observatorios-api-base"]')?.getAttribute("content")?.trim();
  if (meta && /^https?:\/\//i.test(meta)) return sinBarraFinal(meta);

  const { protocol, hostname, port } = window.location;
  if (protocol === "file:") return "http://localhost:5289";

  const p = port || "";
  const hostLocal =
    hostname === "localhost" ||
    hostname === "127.0.0.1" ||
    hostname === "[::1]" ||
    hostname === "::1";

  if (hostLocal && PUERTOS_API_LOCAL.has(p))
    return sinBarraFinal(window.location.origin);

  if (hostLocal && PUERTOS_SOLO_FRONTEND.has(p))
    return "http://localhost:5289";

  /* Cualquier otro localhost (p. ej. Live Server en puerto raro): API por defecto en 5289 */
  if (hostLocal) return "http://localhost:5289";

  return sinBarraFinal(window.location.origin);
}

export function apiUrl(path) {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  const base = apiBaseUrl();
  return base ? `${base}${normalized}` : normalized;
}
