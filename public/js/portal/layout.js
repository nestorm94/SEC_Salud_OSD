/**
 * @fileoverview Layout compartido del portal HTML legacy del OSD.
 * Sidebar, autenticación, permisos por rol y utilidades de tablas para todas las páginas del portal.
 */
import {
  requerirAuth,
  getUsuario,
  cerrarSesion,
  puedeAdministrar,
  refrescarSesion,
  tieneRol,
} from "../auth.js";
import { fetchJson } from "../fetchJson.js";
import { apiUrl } from "../config.js";

export { tieneRol, puedeAdministrar };

/** @returns {boolean} true si el usuario puede aprobar o rechazar cargues. */
export function puedeValidar() {
  return puedeAdministrar() || tieneRol("VALIDADOR");
}

/** @returns {boolean} true si el usuario puede subir archivos Excel. */
export function puedeCargar() {
  return (
    puedeAdministrar() ||
    tieneRol("RESPONSABLE_TEMATICO") ||
    tieneRol("COORDINADOR_DEPENDENCIA")
  );
}

const ENLACES_OPERACION = [
  { href: "/dashboard.html", label: "Panel principal", icon: "home" },
  { href: "/index.html", label: "Carga Excel", icon: "upload" },
  { href: "/cargas.html", label: "Historial de cargas", icon: "history" },
  { href: "/validaciones.html", label: "Validaciones", soloValidador: true, icon: "check" },
  { href: "/proyeccion-poblacion.html", label: "Proyección población", icon: "people" },
];

const NAV_ICONOS = {
  home: '<svg class="nav-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/></svg>',
  upload: '<svg class="nav-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z"/></svg>',
  history: '<svg class="nav-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>',
  people: '<svg class="nav-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"/></svg>',
  check: '<svg class="nav-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg>',
};

/** Incrementar al cambiar menú o assets del portal (evita caché del navegador). */
export const PORTAL_ASSET_VERSION = "20250528";

const ENLACES_ADMIN = [
  { href: "/admin/usuarios.html", label: "Usuarios" },
  { href: "/admin/roles.html", label: "Roles" },
  { href: "/admin/lineas-tematicas.html", label: "Líneas temáticas" },
  { href: "/admin/indicadores.html", label: "Indicadores" },
  { href: "/admin/dependencias.html", label: "Dependencias" },
  { href: "/admin/areas-tematicas.html", label: "Áreas OSC" },
  { href: "/admin/plantillas.html", label: "Plantillas" },
];

function claseActivo(href, activePath) {
  return href === activePath ? ' class="activo"' : "";
}

function inyectarCssResponsive() {
  if (document.getElementById("css-responsive")) return;
  const link = document.createElement("link");
  link.id = "css-responsive";
  link.rel = "stylesheet";
  link.href = `/css/responsive.css?v=${PORTAL_ASSET_VERSION}`;
  document.head.appendChild(link);
}

function inyectarMaterialIcons() {
  if (document.getElementById("material-icons-font")) return;
  const link = document.createElement("link");
  link.id = "material-icons-font";
  link.rel = "stylesheet";
  link.href = "https://fonts.googleapis.com/icon?family=Material+Icons";
  document.head.appendChild(link);
}

/** Menú lateral colapsable en pantallas pequeñas. */
function configurarNavMovil() {
  const layout = document.querySelector(".portal-layout");
  const sidebar = document.querySelector(".portal-sidebar");
  if (!layout || !sidebar || document.getElementById("portal-menu-toggle")) return;

  const btn = document.createElement("button");
  btn.type = "button";
  btn.id = "portal-menu-toggle";
  btn.className = "portal-menu-toggle";
  btn.setAttribute("aria-label", "Abrir menú de navegación");
  btn.setAttribute("aria-expanded", "false");
  btn.innerHTML = "&#9776;";

  const overlay = document.createElement("div");
  overlay.className = "portal-sidebar-overlay";
  overlay.setAttribute("aria-hidden", "true");

  layout.insertBefore(overlay, sidebar);
  layout.insertBefore(btn, layout.firstChild);

  const cerrar = () => {
    document.body.classList.remove("portal-nav-abierto");
    btn.setAttribute("aria-expanded", "false");
  };

  const abrir = () => {
    document.body.classList.add("portal-nav-abierto");
    btn.setAttribute("aria-expanded", "true");
  };

  btn.addEventListener("click", () => {
    if (document.body.classList.contains("portal-nav-abierto")) cerrar();
    else abrir();
  });
  overlay.addEventListener("click", cerrar);
  sidebar.querySelectorAll("nav a").forEach((a) => a.addEventListener("click", cerrar));
  window.addEventListener("resize", () => {
    if (window.innerWidth > 900) cerrar();
  });
}

/**
 * Renderiza el menú lateral con enlaces de operación y administración según permisos.
 * @param {string} activePath - Ruta actual para marcar el enlace activo.
 */
export function pintarSidebar(activePath) {
  const aside = document.querySelector(".portal-sidebar");
  if (!aside) return;

  const operacion = ENLACES_OPERACION.filter(
    (e) => !e.soloValidador || puedeValidar()
  );
  const admin = puedeAdministrar();

  const icono = (k) => NAV_ICONOS[k] || "";

  let html = `<div class="portal-sidebar-brand">
      <div class="portal-sidebar-brand__gov">Gobernación de Casanare</div>
      <div class="portal-sidebar-brand__escudo" aria-hidden="true">☀</div>
    </div>
    <div class="portal-sidebar-inner">
    <strong>Observatorio OSD</strong>
    <p id="portal-usuario" class="subtitulo"></p>
    <nav class="portal-nav-operacion">`;
  for (const e of operacion) {
    html += `<a href="${e.href}"${claseActivo(e.href, activePath)}>${icono(e.icon)}${e.label}</a>`;
  }
  html += `</nav>`;

  if (admin) {
    const enAdmin = activePath.startsWith("/admin/");
    const expandido = enAdmin ? "true" : "false";
    html += `<div class="portal-nav-group">
      <button type="button" class="portal-nav-group-toggle" id="portal-admin-toggle" aria-expanded="${expandido}" aria-controls="portal-nav-admin">
        <span>Administración</span>
        <span class="portal-nav-chevron" aria-hidden="true">▼</span>
      </button>
      <nav id="portal-nav-admin" class="portal-nav-admin portal-nav-submenu${enAdmin ? " is-expanded" : ""}">`;
    for (const e of ENLACES_ADMIN) {
      html += `<a href="${e.href}"${claseActivo(e.href, activePath)}>${e.label}</a>`;
    }
    html += `</nav></div>`;
  }

  html += `<div class="portal-sidebar-landscape" aria-hidden="true"></div>
    <button type="button" id="btn-logout" class="btn secundario portal-logout">Cerrar sesión</button>
    </div>`;
  aside.innerHTML = html;
}

/**
 * Inicializa una página del portal: sesión, sidebar, permisos y cierre de sesión.
 * @param {string} activePath - Ruta de la página para resaltar en el menú.
 * @param {{ requiereAdmin?: boolean }} [options] - Si requiereAdmin, redirige si no es administrador.
 * @returns {Promise<boolean>} false si no hay sesión o permiso; true si el portal quedó listo.
 */
export async function initPortal(activePath, options = {}) {
  if (!requerirAuth()) return false;

  document.body.classList.add("portal-tema-claro");
  inyectarCssResponsive();
  inyectarMaterialIcons();

  const sesion = await refrescarSesion();
  if (!sesion) {
    const next = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.replace(`/login.html?next=${next}`);
    return false;
  }

  if (options.requiereAdmin && !puedeAdministrar()) {
    window.location.replace("/dashboard.html?error=sin_permiso");
    return false;
  }

  pintarSidebar(activePath);
  configurarNavMovil();

  const u = getUsuario();
  document.getElementById("portal-usuario")?.replaceChildren(
    document.createTextNode(
      `${u?.nombre || u?.email || "Usuario"} · ${(u?.roles || []).join(", ") || "sin rol"}`
    )
  );

  document.querySelectorAll("[data-admin-only]").forEach((el) => {
    if (el.closest(".portal-sidebar")) return;
    el.hidden = !puedeAdministrar();
  });

  document.querySelectorAll("[data-validador-only]").forEach((el) => {
    if (el.closest(".portal-sidebar")) return;
    el.hidden = !puedeValidar();
  });

  document.getElementById("portal-admin-toggle")?.addEventListener("click", () => {
    const btn = document.getElementById("portal-admin-toggle");
    const sub = document.getElementById("portal-nav-admin");
    if (!btn || !sub) return;
    const open = btn.getAttribute("aria-expanded") === "true";
    btn.setAttribute("aria-expanded", open ? "false" : "true");
    sub.classList.toggle("is-expanded", !open);
  });

  document.getElementById("btn-logout")?.addEventListener("click", () => {
    cerrarSesion();
    window.location.href = "/login.html";
  });

  const err = new URLSearchParams(window.location.search).get("error");
  if (err === "sin_permiso") {
    const main = document.querySelector(".portal-main");
    if (main) {
      const p = document.createElement("p");
      p.className = "mensaje error";
      p.textContent = "No tiene permisos para acceder a esa sección.";
      main.prepend(p);
    }
  }

  return true;
}

/**
 * Carga datos de un endpoint y rellena un tbody con filas generadas por mapRow.
 * @param {string} endpoint - Ruta relativa de la API (p. ej. /api/admin/roles).
 * @param {(row: object) => string} mapRow - Función que devuelve HTML de una fila <tr>.
 * @param {string} tbodyId - id del elemento tbody destino.
 * @param {string} [emptyMsg] - Mensaje cuando no hay registros.
 * @returns {Promise<void>}
 */
export async function cargarTabla(endpoint, mapRow, tbodyId, emptyMsg = "Sin registros") {
  const tbody = document.getElementById(tbodyId);
  if (!tbody) return;
  tbody.innerHTML = `<tr><td colspan="99">Cargando…</td></tr>`;
  try {
    const { res, data } = await fetchJson(apiUrl(endpoint));
    if (!res.ok) throw new Error(data.error || res.status);
    const key = Object.keys(data).find((k) => Array.isArray(data[k]));
    const rows = key ? data[key] : [];
    if (!rows.length) {
      tbody.innerHTML = `<tr><td colspan="99">${emptyMsg}</td></tr>`;
      return;
    }
    tbody.innerHTML = rows.map(mapRow).join("");
  } catch (e) {
    tbody.innerHTML = `<tr><td colspan="99" class="error">${e.message}</tr>`;
  }
}

export { apiUrl, fetchJson };
