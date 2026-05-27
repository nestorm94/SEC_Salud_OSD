import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";

const TOKEN_KEY = "observatorios.token";
const USER_KEY = "observatorios.usuario";

const ROLES_ADMIN = new Set(["admin", "administrador", "ADMIN", "ADMINISTRADOR"]);

export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY) || "";
  } catch {
    return "";
  }
}

/** true si no hay token o el JWT ya venció (evita ráfagas de 401 en catálogos). */
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

export function getUsuario() {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function guardarSesion(token, usuario) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(normalizarUsuario(usuario)));
}

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

/** Actualiza roles desde el JWT vía API (corrige sesiones antiguas sin roles). */
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

export function esAdministrador() {
  const u = getUsuario();
  return (
    u?.roles?.some((r) => ROLES_ADMIN.has(String(r).toLowerCase())) ?? false
  );
}

export function tieneRol(rol) {
  const u = getUsuario();
  return u?.roles?.some((r) => String(r).toLowerCase() === rol.toLowerCase()) ?? false;
}

export function puedeAdministrar() {
  return esAdministrador() || tieneRol("ADMIN");
}

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

export function authHeaders(extra = {}) {
  const h = { ...extra };
  const t = getToken();
  if (t) h.Authorization = `Bearer ${t}`;
  return h;
}
