/**
 * @fileoverview Cliente HTTP JSON del portal HTML legacy del OSD.
 * Envuelve fetch con JWT, manejo de sesión expirada y parseo seguro del cuerpo de respuesta.
 */
import { authHeaders, cerrarSesion, tokenExpirado } from "./auth.js";
import { apiBaseUrl } from "./config.js";

function esRutaPublica(url) {
  return /\/api\/auth\/login$/i.test(url) || /\/api\/ping$/i.test(url) || /\/api\/salud$/i.test(url);
}

function redirigirLoginSiAplica() {
  if (typeof window === "undefined") return;
  if (window.location.pathname.includes("login.html")) return;
  const next = encodeURIComponent(
    window.location.pathname + window.location.search + window.location.hash
  );
  window.location.replace(`/login.html?next=${next}`);
}

/**
 * Realiza fetch y parsea JSON de forma segura (evita errores con cuerpo vacío o HTML).
 * Incluye JWT si existe sesión, salvo en rutas públicas (login, ping, salud).
 * @param {string} url - URL del endpoint.
 * @param {RequestInit & { sinAuth?: boolean }} [options] - Opciones de fetch; sinAuth omite el token.
 * @returns {Promise<{ res: Response, data: object }>}
 */
export async function fetchJson(url, options = {}) {
  const sinAuth = options.sinAuth === true || esRutaPublica(url);
  if (!sinAuth && tokenExpirado()) {
    cerrarSesion();
    redirigirLoginSiAplica();
    throw new Error("Sesión expirada o no autorizado. Vuelva a iniciar sesión.");
  }
  const headers = sinAuth
    ? { ...(options.headers || {}) }
    : authHeaders(options.headers || {});

  let res;
  try {
    res = await fetch(url, { ...options, headers });
  } catch {
    const base = apiBaseUrl() || "la API";
    throw new Error(
      `No se pudo conectar con ${base}. Compruebe que la API esté en ejecución (.\\ejecutar-api.ps1) y recargue la página.`
    );
  }

  const text = await res.text();
  const trimmed = text.trim();

  if (res.status === 401 && !esRutaPublica(url)) {
    cerrarSesion();
    redirigirLoginSiAplica();
    throw new Error("Sesión expirada o no autorizado. Vuelva a iniciar sesión.");
  }

  if (!trimmed) {
    if (!res.ok) {
      if (res.status === 404) {
        throw new Error(
          `HTTP 404 en «${url}». Si usa IIS (8081), ejecute .\\scripts\\publicar-iis.ps1. En desarrollo: .\\ejecutar-api.ps1 y /api/ping.`
        );
      }
      throw new Error(`El servidor respondió HTTP ${res.status} sin contenido.`);
    }
    return { res, data: {} };
  }

  try {
    return { res, data: JSON.parse(text) };
  } catch {
    const preview = trimmed.slice(0, 280).replace(/\s+/g, " ");
    throw new Error(`Respuesta no es JSON (HTTP ${res.status}). ${preview || "Cuerpo ilegible."}`);
  }
}
