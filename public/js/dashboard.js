import { apiUrl, fetchJson } from "./portal/layout.js";
import { formatearSoloFecha } from "./fechas.js";

const STAT_ICONOS = {
  archivos: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg>',
  pendiente: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z"/></svg>',
  error: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg>',
  aprobado: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>',
};

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function claseBadgeEstado(estado) {
  const e = (estado || "").toUpperCase();
  if (e.includes("EXITOSO") || e.includes("APROBADO") || e === "ENVIADO" || e === "VALIDADO")
    return "badge-carga badge-carga--exito";
  if (e.includes("ERROR") || e.includes("RECHAZADO") || e === "RECHAZADO")
    return e.includes("RECHAZADO") ? "badge-carga badge-carga--rechazado" : "badge-carga badge-carga--error";
  if (e.includes("PENDIENTE") || e === "RECIBIDO" || e.includes("VALIDACION"))
    return "badge-carga badge-carga--pendiente";
  return "badge-carga badge-carga--pendiente";
}

function tarjetaStat(clase, icono, label, valor, linkTexto, href) {
  return `
    <article class="stat-card stat-card--${clase}">
      <div class="stat-card__top">
        <div class="stat-card__icon">${icono}</div>
        <div>
          <p class="stat-card__label">${escapeHtml(label)}</p>
          <p class="stat-card__valor">${escapeHtml(String(valor))}</p>
        </div>
      </div>
      <a class="stat-card__link" href="${href}">${escapeHtml(linkTexto)} ›</a>
    </article>`;
}

function renderDashboard(data) {
  const root = document.getElementById("dashboard-root");
  if (!root) return;

  const ultimos = data.ultimos_cargues || [];
  const filas =
    ultimos.length === 0
      ? '<tr><td colspan="5" style="text-align:center;color:#94a3b8">Sin cargues recientes</td></tr>'
      : ultimos
          .map(
            (u) => `
        <tr>
          <td>
            <div class="celda-archivo">
              <span class="icono-excel">XLS</span>
              <span>${escapeHtml(u.archivo)}</span>
            </div>
          </td>
          <td><span class="${claseBadgeEstado(u.estado)}">${escapeHtml(u.estado)}</span></td>
          <td>${escapeHtml(u.dependencia || "—")}</td>
          <td>${escapeHtml(u.usuario || "—")}</td>
          <td>${escapeHtml(formatearSoloFecha(u.fecha))}</td>
        </tr>`
          )
          .join("");

  root.innerHTML = `
    <header class="dashboard-hero">
      <div class="dashboard-hero__text">
        <h1>¡Bienvenido! 👋</h1>
        <p>Resumen del observatorio según su rol y dependencia.</p>
      </div>
      <div class="dashboard-hero__logo">
        Secretaría de Salud<br />Casanare
      </div>
    </header>

    <section class="dashboard-stats" aria-label="Indicadores">
      ${tarjetaStat("verde", STAT_ICONOS.archivos, "Archivos totales", data.total_archivos, "Ver todos", "/index.html")}
      ${tarjetaStat("amarillo", STAT_ICONOS.pendiente, "Cargas pendientes", data.cargas_pendientes, "Ver pendientes", "/cargas.html")}
      ${tarjetaStat("rojo", STAT_ICONOS.error, "Con error", data.cargas_con_error, "Ver con error", "/cargas.html")}
      ${tarjetaStat("azul", STAT_ICONOS.aprobado, "Aprobadas", data.cargas_aprobadas, "Ver aprobadas", "/cargas.html")}
    </section>

    <section class="dashboard-section">
      <h2>Últimos cargues</h2>
      <div class="tabla-wrap">
        <table class="tabla-dashboard">
          <thead>
            <tr>
              <th>Archivo</th>
              <th>Estado</th>
              <th>Dependencia</th>
              <th>Usuario</th>
              <th>Fecha</th>
            </tr>
          </thead>
          <tbody>${filas}</tbody>
        </table>
      </div>
      <a class="dashboard-footer-link" href="/cargas.html">Ver historial completo ›</a>
    </section>

    <footer class="portal-footer">
      <span>© Gobernación de Casanare · Secretaría de Salud Departamental</span>
      <span>Observatorio de Salud Departamental</span>
    </footer>`;
}

export async function cargarDashboard() {
  const root = document.getElementById("dashboard-root");
  if (!root) return;
  root.innerHTML = '<div class="dashboard-loading">Cargando resumen…</div>';

  try {
    const { res, data } = await fetchJson(apiUrl("/api/dashboard/resumen"));
    if (!res.ok) {
      root.innerHTML = `<div class="dashboard-error">${escapeHtml(data.error || `Error HTTP ${res.status}`)}</div>`;
      return;
    }
    renderDashboard(data);
  } catch (e) {
    root.innerHTML = `<div class="dashboard-error">Error de conexión: ${escapeHtml(e.message)}. Ejecute <code>.\\ejecutar-api.ps1</code></div>`;
  }
}
