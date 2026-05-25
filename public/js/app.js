import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { getUsuario, cerrarSesion, authHeaders, puedeAdministrar } from "./auth.js";

function $(sel) {
  return document.querySelector(sel);
}

/** Archivo validado en la sesión actual; requerido para Enviar. */
let archivoIdValidado = null;

function msgCarga(texto, tipo = "") {
  const panel = $("#panel-estado-validacion");
  const el = $("#mensaje-carga");
  if (panel) panel.hidden = !texto && !$("#lista-errores-validacion")?.childElementCount;
  if (!el) return;
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
  if (texto && panel) panel.hidden = false;
}

function mostrarErroresValidacion(errores) {
  const ul = $("#lista-errores-validacion");
  if (!ul) return;
  ul.innerHTML = "";
  if (!errores?.length) {
    ul.hidden = true;
    return;
  }
  for (const e of errores) {
    const li = document.createElement("li");
    li.textContent = e;
    ul.appendChild(li);
  }
  ul.hidden = false;
  $("#panel-estado-validacion").hidden = false;
}

function resetEstadoValidacion() {
  archivoIdValidado = null;
  const btnEnviar = $("#btn-enviar");
  if (btnEnviar) btnEnviar.disabled = true;
  mostrarErroresValidacion([]);
  msgCarga("", "");
  const panel = $("#panel-estado-validacion");
  if (panel) panel.hidden = true;
}

function habilitarEnviar(archivoId) {
  archivoIdValidado = archivoId;
  const btn = $("#btn-enviar");
  if (btn) btn.disabled = !archivoId;
}

function msgLista(texto, tipo = "") {
  const el = $("#mensaje-lista");
  if (!el) return;
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function formatearFecha(iso) {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("es-CO");
  } catch {
    return String(iso);
  }
}

function badgeEstado(estado, etiqueta) {
  const map = {
    PendienteValidacion: "pendiente",
    Validado: "validado",
    Rechazado: "rechazado",
    Enviado: "enviado",
  };
  const cls = map[estado] || "pendiente";
  return `<span class="badge-estado badge-estado--${cls}">${escapeHtml(etiqueta || estado)}</span>`;
}

function extraerLineas(data) {
  return data.lineas_tematicas ?? data.LineasTematicas ?? data.lineas ?? [];
}

function extraerIndicadores(data) {
  return data.indicadores ?? data.Indicadores ?? [];
}

function esArchivoPermitido(nombre) {
  const n = (nombre || "").toLowerCase();
  return n.endsWith(".xlsx") || n.endsWith(".csv");
}

export async function cargarLineasTematicas() {
  const sel = $("#linea-tematica-id");
  if (!sel) throw new Error("No se encontró el selector de línea temática.");

  sel.disabled = true;
  sel.innerHTML = '<option value="">— Cargando líneas temáticas… —</option>';
  msgCarga("Cargando líneas temáticas…", "");

  const { res, data } = await fetchJson(apiUrl("/api/lineas-tematicas"));
  if (!res.ok) throw new Error(data.error || data.title || `Error HTTP ${res.status}`);

  const lineas = extraerLineas(data);
  if (!lineas.length) {
    sel.innerHTML = '<option value="">— No hay líneas temáticas (contacte al administrador) —</option>';
    msgCarga("No hay líneas temáticas en el catálogo. Reinicie la API para aplicar el seed.", "error");
    return [];
  }

  sel.innerHTML = '<option value="">— Seleccione línea temática —</option>';
  for (const l of lineas) {
    const opt = document.createElement("option");
    opt.value = l.id;
    opt.textContent = l.nombre;
    opt.dataset.codigo = l.codigo || "";
    sel.appendChild(opt);
  }
  const u = getUsuario();
  const soloSuLinea = !puedeAdministrar() && u?.linea_tematica_id;

  if (lineas.length === 1 || soloSuLinea) {
    sel.value = String(lineas[0].id);
    sel.disabled = true;
    msgCarga("", "");
    await cargarIndicadores(lineas[0].id);
  } else {
    sel.disabled = false;
    msgCarga("", "");
  }

  return lineas;
}

export async function cargarIndicadores(lineaId) {
  const sel = $("#indicador-id");
  if (!sel) return;

  sel.innerHTML = '<option value="">— Cargando… —</option>';
  sel.disabled = true;

  if (!lineaId) {
    sel.innerHTML = '<option value="">— Primero elija línea temática —</option>';
    return;
  }

  const { res, data } = await fetchJson(
    apiUrl(`/api/indicadores?linea_tematica_id=${encodeURIComponent(lineaId)}`)
  );
  if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);

  const inds = extraerIndicadores(data);
  sel.innerHTML = inds.length
    ? '<option value="">— Seleccione indicador —</option>'
    : '<option value="">— Sin indicadores en esta línea —</option>';
  for (const i of inds) {
    const opt = document.createElement("option");
    opt.value = i.id;
    opt.textContent = i.nombre;
    sel.appendChild(opt);
  }
  sel.disabled = inds.length === 0;
}

function cerrarModal(id) {
  const el = document.getElementById(id);
  if (el) el.hidden = true;
}

async function verDetalle(id) {
  const { res, data } = await fetchJson(apiUrl(`/api/archivos/${id}`));
  if (!res.ok) {
    alert(data.error || "No se pudo cargar el detalle.");
    return;
  }
  const errores = (data.errores_validacion || []).map((e) => `<li>${escapeHtml(e)}</li>`).join("");
  const el = document.getElementById("contenido-detalle");
  el.innerHTML = `
    <p><strong>Archivo:</strong> ${escapeHtml(data.nombre_original)}</p>
    <p><strong>Estado:</strong> ${badgeEstado(data.estado, data.estado_etiqueta)}</p>
    <p><strong>Línea temática:</strong> ${escapeHtml(data.linea_tematica || "—")}</p>
    <p><strong>Indicador:</strong> ${escapeHtml(data.indicador || "—")}</p>
    <p><strong>Fecha validación:</strong> ${escapeHtml(formatearFecha(data.fecha_validacion))}</p>
    <p><strong>Fecha envío:</strong> ${escapeHtml(formatearFecha(data.fecha_envio))}</p>
    <p><strong>Usuario:</strong> ${escapeHtml(data.subido_por || "—")}</p>
    <p><strong>Observaciones:</strong> ${escapeHtml(data.observaciones || "—")}</p>
    ${errores ? `<p><strong>Errores de validación:</strong></p><ul>${errores}</ul>` : ""}`;
  document.getElementById("modal-detalle").hidden = false;
}

async function cargarLista() {
  const cuerpo = $("#cuerpo-tabla");
  if (!cuerpo) return;
  cuerpo.innerHTML = "";
  msgLista("Cargando archivos…", "");
  try {
    const { res, data } = await fetchJson(apiUrl("/api/archivos"));
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);
    const rows = data.archivos || data.Archivos || [];
    if (rows.length === 0) {
      msgLista("No hay archivos registrados.", "");
      return;
    }
    msgLista(`${rows.length} archivo(s).`, "");
    for (const a of rows) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${escapeHtml(a.nombre_original)}</td>
        <td>${escapeHtml(a.linea_tematica || "—")}</td>
        <td>${escapeHtml(a.indicador || "—")}</td>
        <td>${badgeEstado(a.estado, a.estado_etiqueta)}</td>
        <td>${escapeHtml(formatearFecha(a.fecha_validacion))}</td>
        <td>${escapeHtml(formatearFecha(a.fecha_envio))}</td>
        <td>${escapeHtml(a.subido_por || "—")}</td>
        <td class="acciones-celda celda-acciones"></td>`;
      const acc = tr.querySelector(".acciones-celda");
      const btnDet = document.createElement("button");
      btnDet.type = "button";
      btnDet.className = "btn-mini";
      btnDet.textContent = "Detalle";
      btnDet.addEventListener("click", () => verDetalle(a.id));
      acc.appendChild(btnDet);
      if (a.estado === "Enviado" || a.estado === "Validado") {
        const btnDl = document.createElement("button");
        btnDl.type = "button";
        btnDl.className = "btn-mini";
        btnDl.textContent = "Descargar";
        btnDl.addEventListener("click", () => descargarArchivo(a.id, a.nombre_original));
        acc.appendChild(btnDl);
      }
      if (a.estado !== "Enviado") {
        const btnDel = document.createElement("button");
        btnDel.type = "button";
        btnDel.className = "btn-mini btn-peligro";
        btnDel.textContent = "Eliminar";
        btnDel.addEventListener("click", () => eliminarArchivo(a.id));
        acc.appendChild(btnDel);
      }
      cuerpo.appendChild(tr);
    }
  } catch (e) {
    msgLista(String(e.message), "error");
  }
}

async function descargarArchivo(id, nombre) {
  const res = await fetch(apiUrl(`/api/archivos/${id}/descargar`), { headers: authHeaders() });
  if (!res.ok) {
    alert("No se pudo descargar el archivo.");
    return;
  }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nombre || "archivo";
  a.click();
  URL.revokeObjectURL(url);
}

async function eliminarArchivo(id) {
  if (!confirm("¿Eliminar este archivo del servidor y de la base de datos?")) return;
  try {
    const { res, data } = await fetchJson(apiUrl(`/api/archivos/${id}`), { method: "DELETE" });
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);
    if (archivoIdValidado === id) resetEstadoValidacion();
    await cargarLista();
  } catch (e) {
    msgLista(String(e.message), "error");
  }
}

async function validarArchivo() {
  const input = $("#input-archivo");
  const lineaId = $("#linea-tematica-id")?.value;
  const indicadorId = $("#indicador-id")?.value;
  if (!lineaId || !indicadorId) {
    msgCarga("Seleccione línea temática e indicador.", "error");
    return;
  }
  if (!input.files?.length) {
    msgCarga("Seleccione un archivo Excel o CSV.", "error");
    return;
  }
  const file = input.files[0];
  if (!esArchivoPermitido(file.name)) {
    msgCarga("Solo archivos .xlsx o .csv.", "error");
    return;
  }

  resetEstadoValidacion();
  const btnValidar = $("#btn-validar");
  const btnEnviar = $("#btn-enviar");
  if (btnValidar) btnValidar.disabled = true;
  msgCarga("Validando archivo, por favor espere…", "info");

  const fd = new FormData();
  fd.append("archivo", file);
  fd.append("linea_tematica_id", lineaId);
  fd.append("indicador_id", indicadorId);
  const obs = $("#observaciones")?.value?.trim();
  if (obs) fd.append("observaciones", obs);

  try {
    const { res, data } = await fetchJson(apiUrl("/api/archivos/validar"), { method: "POST", body: fd });
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);

    const errores = data.errores || [];
    mostrarErroresValidacion(errores);

    if (data.valido) {
      msgCarga(data.mensaje || "Archivo validado correctamente. Puede continuar con el envío.", "exito");
      habilitarEnviar(data.archivo_id);
    } else {
      msgCarga(
        data.mensaje ||
          "El archivo presenta inconsistencias. Corrija el archivo y vuelva a cargarlo.",
        "error"
      );
    }
    await cargarLista();
  } catch (e) {
    msgCarga(String(e.message), "error");
  } finally {
    if (btnValidar) btnValidar.disabled = false;
  }
}

async function enviarArchivo() {
  if (!archivoIdValidado) {
    msgCarga("Debe validar el archivo antes de enviarlo.", "error");
    return;
  }

  const btnEnviar = $("#btn-enviar");
  if (btnEnviar) btnEnviar.disabled = true;
  msgCarga("Enviando archivo…", "info");

  try {
    const { res, data } = await fetchJson(apiUrl("/api/archivos/enviar"), {
      method: "POST",
      headers: { ...authHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify({ archivo_id: archivoIdValidado }),
    });
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);

    msgCarga(data.mensaje || "Archivo enviado correctamente.", "exito");
    resetEstadoValidacion();
    $("#input-archivo").value = "";
    await cargarLista();
  } catch (e) {
    msgCarga(String(e.message), "error");
    if (archivoIdValidado) habilitarEnviar(archivoIdValidado);
  }
}

function enlazarEventos() {
  document.getElementById("btn-logout")?.addEventListener("click", () => {
    cerrarSesion();
    window.location.href = "/login.html";
  });

  $("#linea-tematica-id")?.addEventListener("change", () => {
    const id = $("#linea-tematica-id").value;
    cargarIndicadores(id).catch((e) => msgCarga(`Indicadores: ${e.message}`, "error"));
    const ind = $("#indicador-id");
    if (ind) ind.value = "";
    resetEstadoValidacion();
  });

  $("#indicador-id")?.addEventListener("change", () => resetEstadoValidacion());
  $("#input-archivo")?.addEventListener("change", () => resetEstadoValidacion());

  $("#form-carga")?.addEventListener("submit", (ev) => ev.preventDefault());

  $("#btn-validar")?.addEventListener("click", () => validarArchivo());
  $("#btn-enviar")?.addEventListener("click", () => enviarArchivo());

  document.querySelectorAll("[data-cerrar]").forEach((btn) => {
    btn.addEventListener("click", () => cerrarModal(btn.dataset.cerrar));
  });
  document.getElementById("modal-detalle")?.addEventListener("click", (ev) => {
    if (ev.target.id === "modal-detalle") cerrarModal("modal-detalle");
  });

  $("#btn-refrescar")?.addEventListener("click", () => cargarLista());
  $("#btn-recargar-catalogos")?.addEventListener("click", async () => {
    try {
      await cargarLineasTematicas();
      msgCarga("Catálogo actualizado.", "exito");
    } catch (e) {
      msgCarga(e.message, "error");
    }
  });
}

/** Llamar después de initPortal (sesión lista). */
export async function inicializarPaginaCarga() {
  resetEstadoValidacion();
  enlazarEventos();
  try {
    await cargarLineasTematicas();
    await cargarLista();
  } catch (e) {
    msgCarga(`No se pudo cargar el catálogo: ${e.message}`, "error");
    console.error("[carga]", e);
    await cargarLista().catch(() => {});
  }
}
