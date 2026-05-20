import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { requerirAuth, getUsuario, cerrarSesion, esAdministrador, authHeaders } from "./auth.js";

if (!requerirAuth()) throw new Error("redirect");

const u = getUsuario();
const info = document.getElementById("usuario-info");
if (info) {
  info.textContent = `${u?.nombre || ""} · ${u?.dependencia || "Todas las dependencias"} · ${(u?.roles || []).join(", ")}`;
}

document.getElementById("btn-logout")?.addEventListener("click", () => {
  cerrarSesion();
  window.location.href = "/login.html";
});

function $(sel) {
  return document.querySelector(sel);
}

function setMensaje(el, texto, tipo) {
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
}

async function cargarLista() {
  const cuerpo = $("#cuerpo-tabla");
  const msg = $("#mensaje-lista");
  cuerpo.innerHTML = "";
  setMensaje(msg, "Cargando…", "");
  try {
    const { res, data } = await fetchJson(apiUrl("/api/archivos"));
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);
    const rows = data.archivos || [];
    if (rows.length === 0) {
      setMensaje(msg, "No hay archivos aún.", "");
      return;
    }
    setMensaje(msg, `${rows.length} archivo(s).`, "");
    for (const a of rows) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${a.id}</td>
        <td>${escapeHtml(a.nombre_original)}</td>
        <td>${escapeHtml(a.dependencia || "")}</td>
        <td><code>${escapeHtml(a.tipo_mime || "—")}</code></td>
        <td>${formatearTamano(a.tamano_bytes)}</td>
        <td>${escapeHtml(a.creado_en || "")}</td>
        <td class="acciones-celda"></td>
      `;
      const acc = tr.querySelector(".acciones-celda");
      const btnDl = document.createElement("button");
      btnDl.type = "button";
      btnDl.className = "btn secundario";
      btnDl.textContent = "Descargar";
      btnDl.addEventListener("click", () => descargarArchivo(a.id, a.nombre_original));
      acc.appendChild(btnDl);
      const btnDel = document.createElement("button");
      btnDel.type = "button";
      btnDel.className = "btn peligro";
      btnDel.textContent = "Eliminar";
      btnDel.addEventListener("click", () => eliminarArchivo(a.id));
      acc.appendChild(btnDel);
      cuerpo.appendChild(tr);
    }
  } catch (e) {
    setMensaje(msg, String(e.message), "error");
  }
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

function formatearTamano(bytes) {
  if (bytes == null) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
  const msg = $("#mensaje-lista");
  try {
    const { res, data } = await fetchJson(apiUrl(`/api/archivos/${id}`), { method: "DELETE" });
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);
    await cargarLista();
  } catch (e) {
    setMensaje(msg, String(e.message), "error");
  }
}

$("#form-carga").addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const input = $("#input-archivo");
  const msg = $("#mensaje-carga");
  if (!input.files?.length) {
    setMensaje(msg, "Elige un archivo Excel (.xlsx).", "error");
    return;
  }
  const file = input.files[0];
  if (!file.name.toLowerCase().endsWith(".xlsx")) {
    setMensaje(msg, "Solo archivos .xlsx con hojas Diccionario_Datos y Datos.", "error");
    return;
  }
  const fd = new FormData();
  fd.append("archivo", file);
  if (esAdministrador()) {
    const depSel = $("#dependencia-id");
    if (depSel?.value) fd.append("dependencia_id", depSel.value);
  }
  setMensaje(msg, "Subiendo y validando…", "");
  try {
    const { res, data } = await fetchJson(apiUrl("/api/cargas/excel"), { method: "POST", body: fd });
    if (!res.ok) throw new Error(data.error || `Error HTTP ${res.status}`);
    const ok = data.valido;
    setMensaje(
      msg,
      ok
        ? `Carga #${data.carga_id} validada correctamente (VALIDADO_OK).`
        : `Carga #${data.carga_id}: ${data.total_errores} error(es). Ver historial de cargues.`,
      ok ? "exito" : "error"
    );
    input.value = "";
    await cargarLista();
  } catch (e) {
    setMensaje(msg, String(e.message), "error");
  }
});

$("#btn-refrescar").addEventListener("click", () => cargarLista());

if (esAdministrador()) cargarDependenciasSelect();

async function cargarDependenciasSelect() {
  const sel = $("#dependencia-id");
  if (!sel) return;
  try {
    const { res, data } = await fetchJson(apiUrl("/api/dependencias"));
    if (!res.ok) return;
    for (const d of data.dependencias || []) {
      const opt = document.createElement("option");
      opt.value = d.id;
      opt.textContent = `${d.codigo} — ${d.nombre}`;
      sel.appendChild(opt);
    }
  } catch {
    /* opcional */
  }
}

cargarLista();
