/**
 * Menú superior unificado (páginas legacy + enlace al panel y administración).
 */
import { requerirAuth, getUsuario, cerrarSesion, refrescarSesion, puedeAdministrar } from "./auth.js";

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
