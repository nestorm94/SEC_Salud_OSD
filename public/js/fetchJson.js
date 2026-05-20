import { authHeaders } from "./auth.js";

/**
 * fetch + lectura segura del cuerpo (evita "Unexpected end of JSON input" si viene vacío o HTML).
 * Incluye JWT si existe sesión.
 * @returns {{ res: Response, data: object }}
 */
export async function fetchJson(url, options = {}) {
  const headers = authHeaders(options.headers || {});
  const res = await fetch(url, { ...options, headers });
  const text = await res.text();
  const trimmed = text.trim();

  if (res.status === 401 && !url.includes("/auth/login")) {
    throw new Error("Sesión expirada o no autorizado. Vuelva a iniciar sesión.");
  }

  if (!trimmed) {
    if (!res.ok) {
      if (res.status === 404) {
        throw new Error(
          `HTTP 404 en «${url}». Ejecute .\\ejecutar-api.ps1 y compruebe /api/ping.`
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
