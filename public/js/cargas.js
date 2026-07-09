/**
 * @fileoverview Historial de cargas (cargas.html) del portal HTML legacy del OSD.
 * Lista cargues, errores de validación, historial de eventos y acciones de aprobación/rechazo.
 */
import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { initPortal, puedeValidar } from "./portal/layout.js";
import { solicitarTexto, mostrarMensaje } from "./portal/modal.js?v=7";
import { formatearSoloFecha, formatearFechaHora } from "./fechas.js";

function abrirModal(id) {
  const el = document.getElementById(id);
  if (el) el.hidden = false;
}

function cerrarModal(id) {
  const el = document.getElementById(id);
  if (el) el.hidden = true;
}

function enlazarModales() {
  document.querySelectorAll("[data-cerrar-modal]").forEach((btn) => {
    btn.addEventListener("click", () => cerrarModal(btn.dataset.cerrarModal));
  });
  document.querySelectorAll(".modal-overlay").forEach((overlay) => {
    overlay.addEventListener("click", (ev) => {
      if (ev.target === overlay) cerrarModal(overlay.id);
    });
  });
  document.addEventListener("keydown", (ev) => {
    if (ev.key !== "Escape") return;
    for (const overlay of document.querySelectorAll(".modal-overlay:not([hidden])")) {
      cerrarModal(overlay.id);
    }
  });
}

function estadoClase(estado) {
  const e = (estado || "").toUpperCase();
  if (e === "VALIDADO_OK" || e === "VALIDADO_EXITOSO" || e === "APROBADO") return "exito";
  if (e === "VALIDADO_CON_ERRORES" || e === "RECHAZADO") return "error";
  return "";
}

function esPendienteAprobacion(estado) {
  const e = (estado || "").toUpperCase();
  return e === "VALIDADO_OK" || e === "VALIDADO_EXITOSO";
}

async function cargarLista() {
  const msg = document.getElementById("mensaje-cargas");
  const cuerpo = document.getElementById("cuerpo-cargas");
  cuerpo.innerHTML = "";
  msg.textContent = "Cargando…";
  msg.className = "mensaje";
  const puedeAprobar = puedeValidar();

  try {
    const { res, data } = await fetchJson(apiUrl("/api/cargas"));
    if (!res.ok) throw new Error(data.error || res.status);
    const rows = data.cargas || [];
    if (!rows.length) {
      msg.textContent = "No hay cargues registrados.";
      return;
    }
    msg.textContent = `${rows.length} cargue(s).`;
    for (const c of rows) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${c.id}</td>
        <td>${escapeHtml(c.archivo)}</td>
        <td>${escapeHtml(c.dependencia)}</td>
        <td><span class="badge ${estadoClase(c.estado)}">${escapeHtml(c.estado)}</span></td>
        <td>${c.total_errores}</td>
        <td>${escapeHtml(formatearSoloFecha(c.fecha_inicio))}</td>
        <td class="acciones-celda"></td>`;
      const acc = tr.querySelector(".acciones-celda");
      if (c.total_errores > 0) {
        const b = document.createElement("button");
        b.type = "button";
        b.className = "btn secundario";
        b.textContent = "Errores";
        b.addEventListener("click", () => verErrores(c.id));
        acc.appendChild(b);
      }
      const bh = document.createElement("button");
      bh.type = "button";
      bh.className = "btn secundario";
      bh.textContent = "Historial";
      bh.addEventListener("click", () => verHistorial(c.id));
      acc.appendChild(bh);

      if (puedeAprobar && esPendienteAprobacion(c.estado)) {
        const ba = document.createElement("button");
        ba.type = "button";
        ba.className = "btn primario";
        ba.textContent = "Aprobar";
        ba.addEventListener("click", () => aprobar(c.id));
        acc.appendChild(ba);
      }
      if (
        puedeAprobar &&
        (esPendienteAprobacion(c.estado) || (c.estado || "").toUpperCase() === "VALIDADO_CON_ERRORES")
      ) {
        const br = document.createElement("button");
        br.type = "button";
        br.className = "btn peligro";
        br.textContent = "Rechazar";
        br.addEventListener("click", () => rechazar(c.id));
        acc.appendChild(br);
      }
      cuerpo.appendChild(tr);
    }
  } catch (e) {
    msg.textContent = String(e.message);
    msg.className = "mensaje error";
  }
}

async function verErrores(id) {
  document.getElementById("carga-errores-id").textContent = id;
  const cuerpo = document.getElementById("cuerpo-errores");
  const msg = document.getElementById("msg-errores-modal");
  cuerpo.innerHTML = "";
  msg.textContent = "Cargando errores…";
  msg.className = "mensaje";
  abrirModal("modal-errores");

  try {
    const { res, data } = await fetchJson(apiUrl(`/api/cargas/${id}/errores`));
    if (!res.ok) throw new Error(data.error || res.status);
    const errores = data.errores || [];
    if (!errores.length) {
      msg.textContent = "No hay errores registrados para esta carga.";
      return;
    }
    msg.textContent = `${errores.length} error(es) encontrado(s).`;
    for (const e of errores) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td>${e.fila ?? "—"}</td><td>${escapeHtml(e.columna || "—")}</td><td>${escapeHtml(e.tipo || "")}</td><td>${escapeHtml(e.mensaje)}</td>`;
      cuerpo.appendChild(tr);
    }
  } catch (e) {
    msg.textContent = String(e.message);
    msg.className = "mensaje error";
  }
}

async function verHistorial(cargaId) {
  document.getElementById("carga-historial-id").textContent = cargaId;
  const lista = document.getElementById("lista-historial");
  const msg = document.getElementById("msg-historial-modal");
  lista.innerHTML = "";
  msg.textContent = "Cargando historial…";
  msg.className = "mensaje";
  abrirModal("modal-historial");

  try {
    const { res, data } = await fetchJson(apiUrl(`/api/cargas/historial?carga_id=${cargaId}`));
    if (!res.ok) throw new Error(data.error || res.status);
    const historial = data.historial || [];
    if (!historial.length) {
      msg.textContent = "Sin eventos en el historial.";
      return;
    }
    msg.textContent = `${historial.length} evento(s).`;
    for (const h of historial) {
      const li = document.createElement("li");
      li.innerHTML = `<strong>${escapeHtml(h.accion)}</strong> — ${escapeHtml(h.usuario || "sistema")} — <time>${escapeHtml(formatearFechaHora(h.fecha))}</time>${h.detalle ? `<br/><small>${escapeHtml(h.detalle)}</small>` : ""}`;
      lista.appendChild(li);
    }
  } catch (e) {
    msg.textContent = String(e.message);
    msg.className = "mensaje error";
  }
}

async function aprobar(id) {
  const obs = await solicitarTexto({
    titulo: "Aprobar cargue",
    label: "Observaciones (opcional)",
    obligatorio: false,
  });
  if (obs === null) return;

  const { res, data } = await fetchJson(apiUrl(`/api/cargas/${id}/aprobar`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  if (!res.ok) {
    await mostrarMensaje({ tipo: "error", mensaje: data.error || "No se pudo aprobar." });
    return;
  }
  await cargarLista();
}

async function rechazar(id) {
  const obs = await solicitarTexto({
    titulo: "Rechazar cargue",
    label: "Motivo del rechazo",
    obligatorio: true,
  });
  if (obs === null) return;

  const { res, data } = await fetchJson(apiUrl(`/api/cargas/${id}/rechazar`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  if (!res.ok) {
    await mostrarMensaje({ tipo: "error", mensaje: data.error || "No se pudo rechazar." });
    return;
  }
  await cargarLista();
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

/**
 * Inicializa la página de historial de cargas: portal, modales y tabla principal.
 * @returns {Promise<boolean>} false si no hay sesión; true si la página quedó operativa.
 */
export async function initCargasHistorial() {
  if (!(await initPortal("/cargas.html"))) return false;
  enlazarModales();
  document.getElementById("btn-refrescar")?.addEventListener("click", cargarLista);
  await cargarLista();
  return true;
}
