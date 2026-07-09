/**
 * @fileoverview Configuración de la URL base de la API para el portal HTML legacy del OSD
 * (Observatorio de Salud Departamental de Casanare).
 * Resuelve el origen de la API según localStorage, meta HTML o reglas por puerto.
 *
 * Si abre el HTML con Live Server / Five Server (puerto 5500, 5173, etc.), el "origen" de la
 * página NO es Kestrel: hay que llamar a http://localhost:5289 (o el puerto donde corre dotnet run).
 */
const PUERTOS_SOLO_FRONTEND = new Set([
  "5500", "5501", "8080", "8081", "3000", "5173", "4173", "4200", "8888",
]);

/** Puertos donde esta misma app ASP.NET suele servir HTML + API a la vez */
const PUERTOS_API_LOCAL = new Set(["5289", "5290", "7236", "8081"]);

function sinBarraFinal(s) {
  return s.replace(/\/+$/, "");
}

function esHostLocal(hostname) {
  return (
    hostname === "localhost" ||
    hostname === "127.0.0.1" ||
    hostname === "[::1]" ||
    hostname === "::1"
  );
}

/**
 * Obtiene la URL base de la API según el entorno (localStorage, meta, puerto actual).
 * @returns {string} Origen de la API sin barra final, o cadena vacía fuera del navegador.
 */
export function apiBaseUrl() {
  if (typeof window === "undefined") return "";

  const { protocol, hostname, port } = window.location;
  const hostLocal = esHostLocal(hostname);
  const puertoActual = port || "";

  try {
    const ls = localStorage.getItem("observatorios.apiOrigen");
    if (ls && /^https?:\/\//i.test(ls)) {
      const override = sinBarraFinal(ls);
      /* Evita mezclar JWT entre 5289 y 8081 por un origen guardado a mano */
      if (hostLocal && PUERTOS_API_LOCAL.has(puertoActual)) {
        try {
          const u = new URL(override);
          if (esHostLocal(u.hostname) && u.port !== puertoActual) {
            localStorage.removeItem("observatorios.apiOrigen");
          } else {
            return override;
          }
        } catch {
          localStorage.removeItem("observatorios.apiOrigen");
        }
      } else {
        return override;
      }
    }
  } catch {
    /* private mode */
  }

  const meta = document.querySelector('meta[name="observatorios-api-base"]')?.getAttribute("content")?.trim();
  if (meta && /^https?:\/\//i.test(meta)) return sinBarraFinal(meta);
  if (protocol === "file:") return "http://localhost:5289";

  const p = puertoActual;

  if (hostLocal && PUERTOS_API_LOCAL.has(p))
    return sinBarraFinal(window.location.origin);

  if (hostLocal && PUERTOS_SOLO_FRONTEND.has(p))
    return "http://localhost:5289";

  /* Cualquier otro localhost (p. ej. Live Server en puerto raro): API por defecto en 5289 */
  if (hostLocal) return "http://localhost:5289";

  return sinBarraFinal(window.location.origin);
}

/**
 * Construye una URL absoluta hacia un endpoint de la API.
 * @param {string} path - Ruta del endpoint (con o sin barra inicial).
 * @returns {string} URL completa lista para fetch.
 */
export function apiUrl(path) {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  const base = apiBaseUrl();
  return base ? `${base}${normalized}` : normalized;
}
