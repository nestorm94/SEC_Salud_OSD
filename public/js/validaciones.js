/**
 * @fileoverview Aprobación de cargues (validaciones.html) del portal HTML legacy del OSD.
 * Gestiona cargues pendientes y archivos validados sin enviar para usuarios con rol VALIDADOR.
 */
import { initPortal, fetchJson, apiUrl, puedeValidar } from "./portal/layout.js";
import { formatearSoloFecha } from "./fechas.js";
import { confirmar, solicitarTexto, mostrarMensaje } from "./portal/modal.js?v=7";

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function esPendienteAprobacion(estado) {
  const e = (estado || "").toUpperCase().trim();
  return e === "VALIDADO_EXITOSO" || e === "VALIDADO_OK";
}

function badgeEstado(estado) {
  const e = (estado || "").toUpperCase();
  let cls = "badge-carga badge-carga--pendiente";
  if (e.includes("EXITOSO") || e === "VALIDADO_OK" || e === "APROBADO") cls = "badge-carga badge-carga--exito";
  if (e.includes("ERROR") || e === "RECHAZADO") cls = "badge-carga badge-carga--error";
  return `<span class="${cls}">${escapeHtml(estado)}</span>`;
}

function filaVacia(colspan, mensaje) {
  return `<tr><td colspan="${colspan}" style="text-align:center;color:#64748b">${escapeHtml(mensaje)}</td></tr>`;
}

function agregarBotonesCarga(acc, c) {
  const btnAp = document.createElement("button");
  btnAp.type = "button";
  btnAp.className = "btn-mini btn-primario";
  btnAp.textContent = "Aprobar";
  btnAp.addEventListener("click", () => aprobar(c.id));
  acc.appendChild(btnAp);

  const btnRj = document.createElement("button");
  btnRj.type = "button";
  btnRj.className = "btn-mini btn-peligro";
  btnRj.textContent = "Rechazar";
  btnRj.addEventListener("click", () => rechazar(c.id));
  acc.appendChild(btnRj);

  if ((c.total_errores || 0) > 0) {
    const btnEr = document.createElement("button");
    btnEr.type = "button";
    btnEr.className = "btn-mini";
    btnEr.textContent = "Errores";
    btnEr.addEventListener("click", () => verErrores(c.id));
    acc.appendChild(btnEr);
  }
}

async function aprobar(id) {
  const obs = await solicitarTexto({
    titulo: "Aprobar cargue",
    label: "Observaciones (opcional)",
    placeholder: "Comentario de aprobación…",
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
  await cargarPendientes();
}

async function rechazar(id) {
  const obs = await solicitarTexto({
    titulo: "Rechazar cargue",
    mensaje: "Indique el motivo del rechazo.",
    label: "Motivo",
    obligatorio: true,
    placeholder: "Motivo del rechazo…",
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
  await cargarPendientes();
}

async function enviarYAprobarArchivo(archivoId) {
  const ok = await confirmar({
    titulo: "Enviar y aprobar",
    mensaje:
      "¿Enviar este archivo y aprobar el cargue si la validación es exitosa?",
    aceptar: "Enviar y aprobar",
    cancelar: "Cancelar",
  });
  if (!ok) return;

  const obs = await solicitarTexto({
    titulo: "Observaciones",
    label: "Observaciones de aprobación (opcional)",
    obligatorio: false,
  });
  if (obs === null) return;

  const { res, data } = await fetchJson(apiUrl(`/api/archivos/${archivoId}/enviar-y-aprobar`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  if (!res.ok) {
    await mostrarMensaje({ tipo: "error", mensaje: data.error || "No se pudo enviar y aprobar." });
    return;
  }
  if (!data.aprobado && !data.procesamiento_valido) {
    await mostrarMensaje({
      tipo: "error",
      titulo: "Validación con errores",
      mensaje:
        data.mensaje ||
        `El envío tiene ${data.total_errores || 0} error(es). Revise el archivo en Carga Excel.`,
    });
  } else if (!data.aprobado) {
    await mostrarMensaje({
      tipo: "info",
      mensaje: data.mensaje || "Enviado. Revise la tabla de pendientes para aprobar.",
    });
  } else {
    await mostrarMensaje({ tipo: "ok", titulo: "Listo", mensaje: data.mensaje || "Cargue enviado y aprobado." });
  }
  await cargarPendientes();
}

async function rechazarArchivo(archivoId) {
  const obs = await solicitarTexto({
    titulo: "Rechazar archivo",
    mensaje: "El archivo volverá a estado rechazado.",
    label: "Motivo del rechazo",
    obligatorio: true,
  });
  if (obs === null) return;

  const { res, data } = await fetchJson(apiUrl(`/api/archivos/${archivoId}/rechazar-validacion`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  if (!res.ok) {
    await mostrarMensaje({ tipo: "error", mensaje: data.error || "No se pudo rechazar el archivo." });
    return;
  }
  await cargarPendientes();
}

async function verErrores(id) {
  const { res, data } = await fetchJson(apiUrl(`/api/cargas/${id}/errores`));
  if (!res.ok) {
    await mostrarMensaje({ tipo: "error", mensaje: data.error || "No se pudieron cargar los errores." });
    return;
  }
  const errores = data.errores || [];
  await mostrarMensaje({
    titulo: "Errores de validación",
    mensaje: errores.length
      ? errores.map((e) => `Fila ${e.fila ?? "—"} · ${e.columna}: ${e.mensaje}`).join("\n")
      : "Sin errores registrados.",
  });
}

async function cargarPendientes() {
  const tbCargas = document.getElementById("tb-cargas-aprobacion");
  const tbArchivos = document.getElementById("tb-archivos-validados");
  const msg = document.getElementById("msg-validaciones");

  if (!tbCargas || !tbArchivos) return;

  tbCargas.innerHTML = filaVacia(6, "Cargando…");
  tbArchivos.innerHTML = filaVacia(6, "Cargando…");
  if (msg) {
    msg.textContent = "";
    msg.className = "mensaje";
  }

  try {
    const [resCargas, resArchivos, resTodas] = await Promise.all([
      fetchJson(apiUrl("/api/cargas/pendientes-aprobacion")),
      fetchJson(apiUrl("/api/archivos")),
      fetchJson(apiUrl("/api/cargas")),
    ]);

    if (!resCargas.res.ok) throw new Error(resCargas.data.error || resCargas.res.status);
    if (!resArchivos.res.ok) throw new Error(resArchivos.data.error || resArchivos.res.status);

    let pendientes = resCargas.data.cargas || [];
    if (pendientes.length === 0 && resTodas.res.ok) {
      pendientes = (resTodas.data.cargas || []).filter((c) => esPendienteAprobacion(c.estado));
    }

    if (pendientes.length === 0) {
      tbCargas.innerHTML = filaVacia(
        6,
        "No hay cargues pendientes. Tras Enviar en Carga Excel deben quedar en VALIDADO_EXITOSO, o use Enviar y aprobar en la tabla inferior."
      );
    } else {
      tbCargas.innerHTML = "";
      for (const c of pendientes) {
        const tr = document.createElement("tr");
        tr.innerHTML = `
          <td>${c.id}</td>
          <td>${escapeHtml(c.archivo)}</td>
          <td>${escapeHtml(c.dependencia || "—")}</td>
          <td>${escapeHtml(c.usuario || "—")}</td>
          <td>${badgeEstado(c.estado)}</td>
          <td class="celda-acciones"></td>`;
        agregarBotonesCarga(tr.querySelector(".celda-acciones"), c);
        tbCargas.appendChild(tr);
      }
    }

    const archivos = (resArchivos.data.archivos || []).filter((a) => {
      const e = (a.estado || "").toLowerCase();
      return e === "validado";
    });

    if (archivos.length === 0) {
      tbArchivos.innerHTML = filaVacia(6, "No hay archivos validados pendientes de envío.");
    } else {
      tbArchivos.innerHTML = "";
      for (const a of archivos) {
        const tr = document.createElement("tr");
        tr.innerHTML = `
          <td>${a.id}</td>
          <td>${escapeHtml(a.nombre_original)}</td>
          <td>${escapeHtml(a.linea_tematica || "—")}</td>
          <td>${escapeHtml(a.subido_por || "—")}</td>
          <td>${escapeHtml(formatearSoloFecha(a.fecha_validacion))}</td>
          <td class="celda-acciones"></td>`;
        const acc = tr.querySelector(".celda-acciones");
        const btnEa = document.createElement("button");
        btnEa.type = "button";
        btnEa.className = "btn-mini btn-primario";
        btnEa.textContent = "Enviar y aprobar";
        btnEa.addEventListener("click", () => enviarYAprobarArchivo(a.id));
        acc.appendChild(btnEa);
        const btnRj = document.createElement("button");
        btnRj.type = "button";
        btnRj.className = "btn-mini btn-peligro";
        btnRj.textContent = "Rechazar";
        btnRj.addEventListener("click", () => rechazarArchivo(a.id));
        acc.appendChild(btnRj);
        tbArchivos.appendChild(tr);
      }
    }

    if (msg) {
      const hint =
        archivos.length > 0 && pendientes.length === 0
          ? " Use «Enviar y aprobar» en la tabla inferior o pida al responsable que pulse Enviar en Carga Excel."
          : "";
      msg.textContent = `${pendientes.length} cargue(s) por aprobar · ${archivos.length} archivo(s) validado(s) sin enviar.${hint}`;
    }
  } catch (e) {
    tbCargas.innerHTML = filaVacia(6, `Error: ${e.message}`);
    tbArchivos.innerHTML = filaVacia(6, "");
    if (msg) {
      msg.textContent = String(e.message);
      msg.className = "mensaje error";
    }
  }
}

/**
 * Inicializa la página de validaciones; verifica permisos y carga tablas de pendientes.
 * @returns {Promise<boolean>} false si no hay sesión o permiso; true si quedó operativa.
 */
export async function initValidaciones() {
  if (!(await initPortal("/validaciones.html"))) return false;

  const main = document.querySelector(".portal-main");
  if (!puedeValidar()) {
    if (main) {
      main.innerHTML =
        '<h1>Validación de cargues</h1><p class="mensaje error">Su rol no tiene permiso para esta sección. Se requiere rol VALIDADOR o ADMIN.</p>';
    }
    return false;
  }

  await cargarPendientes();
  document.getElementById("btn-refrescar-validaciones")?.addEventListener("click", cargarPendientes);
  return true;
}
