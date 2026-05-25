import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";

const TOKEN_KEY = "observatorios.token";
const USER_KEY = "observatorios.usuario";

const ROLES_ADMIN = new Set(["admin", "administrador"]);

export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY) || "";
  } catch {
    return "";
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
    if (!res.ok) return getUsuario();
    const usuario = normalizarUsuario(data.usuario || data);
    localStorage.setItem(USER_KEY, JSON.stringify(usuario));
    return usuario;
  } catch {
    return getUsuario();
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
  if (!getToken()) {
    const next = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.href = `/login.html?next=${next}`;
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
