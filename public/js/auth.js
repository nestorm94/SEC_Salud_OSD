/**
 * @fileoverview Gestión de sesión JWT y permisos por rol en el portal HTML legacy del OSD.
 * Persiste token y usuario en localStorage; redirige a login cuando la sesión no es válida.
 */
import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";

const TOKEN_KEY = "observatorios.token";
const USER_KEY = "observatorios.usuario";

const ROLES_ADMIN = new Set(["admin", "administrador", "ADMIN", "ADMINISTRADOR"]);

/** @returns {string} Token JWT almacenado, o cadena vacía si no hay sesión. */
export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY) || "";
  } catch {
    return "";
  }
}

/**
 * Indica si no hay token o el JWT ya venció (evita ráfagas de 401 en catálogos).
 * @returns {boolean}
 */
export function tokenExpirado() {
  const t = getToken();
  if (!t) return true;
  try {
    const part = t.split(".")[1];
    if (!part) return true;
    const json = atob(part.replace(/-/g, "+").replace(/_/g, "/"));
    const payload = JSON.parse(json);
    const exp = payload.exp;
    if (typeof exp !== "number") return false;
    return Date.now() >= exp * 1000 - 15_000;
  } catch {
    return true;
  }
}

/** @returns {object|null} Usuario normalizado de la sesión actual, o null. */
export function getUsuario() {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

/**
 * Persiste token y datos de usuario tras un login exitoso.
 * @param {string} token - JWT emitido por la API.
 * @param {object} usuario - Perfil devuelto por /api/auth/login o /api/auth/me.
 */
export function guardarSesion(token, usuario) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(normalizarUsuario(usuario)));
}

/** Elimina token y usuario de localStorage. */
export function cerrarSesion() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

function normalizarUsuario(u) {
  if (!u) return u;
  const roles = Array.isArray(u.roles) ? u.roles : [];
  return {
    id: u.id,
    nombre: u.nombre || u.nombre_usuario || "",
    email: u.email || null,
    dependencia_id: u.dependencia_id ?? null,
    dependencia: u.dependencia || u.dependencia_nombre || null,
    linea_tematica_id: u.linea_tematica_id ?? null,
    linea_tematica: u.linea_tematica || null,
    roles,
  };
}

/**
 * Actualiza el perfil del usuario desde la API (corrige sesiones antiguas sin roles).
 * @returns {Promise<object|null>} Usuario actualizado, o null si la sesión no es válida.
 */
export async function refrescarSesion() {
  if (!getToken()) return null;
  try {
    const { res, data } = await fetchJson(apiUrl("/api/auth/me"));
    if (!res.ok) {
      cerrarSesion();
      return null;
    }
    const usuario = normalizarUsuario(data.usuario || data);
    localStorage.setItem(USER_KEY, JSON.stringify(usuario));
    return usuario;
  } catch {
    return null;
  }
}

/** @returns {boolean} true si el usuario tiene rol de administrador. */
export function esAdministrador() {
  const u = getUsuario();
  return (
    u?.roles?.some((r) => ROLES_ADMIN.has(String(r).toLowerCase())) ?? false
  );
}

/**
 * Comprueba si el usuario tiene un rol concreto (comparación insensible a mayúsculas).
 * @param {string} rol - Nombre del rol a verificar.
 * @returns {boolean}
 */
export function tieneRol(rol) {
  const u = getUsuario();
  return u?.roles?.some((r) => String(r).toLowerCase() === rol.toLowerCase()) ?? false;
}

/** @returns {boolean} true si el usuario puede acceder a secciones de administración. */
export function puedeAdministrar() {
  return esAdministrador() || tieneRol("ADMIN");
}

/**
 * Redirige a login si no hay sesión válida; debe llamarse al cargar páginas protegidas.
 * @returns {boolean} false si redirigió; true si la sesión es válida.
 */
export function requerirAuth() {
  if (!getToken() || tokenExpirado()) {
    cerrarSesion();
    const next = encodeURIComponent(
      window.location.pathname + window.location.search + window.location.hash
    );
    window.location.replace(`/login.html?next=${next}`);
    return false;
  }
  return true;
}

/**
 * Cabeceras HTTP con Authorization Bearer si hay token.
 * @param {Record<string, string>} [extra] - Cabeceras adicionales.
 * @returns {Record<string, string>}
 */
export function authHeaders(extra = {}) {
  const h = { ...extra };
  const t = getToken();
  if (t) h.Authorization = `Bearer ${t}`;
  return h;
}
