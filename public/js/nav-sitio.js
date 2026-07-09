/**
 * @fileoverview Menú superior unificado del portal HTML legacy del OSD.
 * Inicializa navegación, datos de usuario y enlaces a panel y administración.
 */
import { requerirAuth, getUsuario, cerrarSesion, refrescarSesion, puedeAdministrar } from "./auth.js";

/**
 * Inicializa la barra de navegación del sitio: sesión, usuario y enlaces contextuales.
 * @returns {Promise<boolean>} false si no hay sesión válida; true si el nav quedó listo.
 */
export async function initNavSitio() {
  if (!requerirAuth()) return false;
  await refrescarSesion();

  const u = getUsuario();
  const info = document.getElementById("usuario-info");
  if (info) {
    info.textContent = `${u?.nombre || ""} · ${u?.dependencia || "Todas"} · ${(u?.roles || []).join(", ")}`;
  }

  document.getElementById("btn-logout")?.addEventListener("click", () => {
    cerrarSesion();
    window.location.href = "/login.html";
  });

  const nav = document.querySelector(".nav-sitio");
  if (!nav || nav.querySelector("[data-nav-panel]")) return true;

  const admin = puedeAdministrar();
  const panel = document.createElement("a");
  panel.href = "/dashboard.html";
  panel.className = "nav-sitio__link";
  panel.textContent = "Panel";
  panel.dataset.navPanel = "1";
  nav.insertBefore(panel, nav.firstChild);

  if (admin) {
    const sep = document.createElement("span");
    sep.className = "nav-sitio__sep";
    sep.textContent = "|";
    nav.insertBefore(sep, nav.querySelector(".nav-logout"));

    const adminLink = document.createElement("a");
    adminLink.href = "/admin/usuarios.html";
    adminLink.className = "nav-sitio__link nav-sitio__link--admin";
    adminLink.textContent = "Administración";
    nav.insertBefore(adminLink, nav.querySelector(".nav-logout"));
  }

  return true;
}
