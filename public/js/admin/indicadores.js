/**
 * @fileoverview Administración de indicadores (admin/indicadores.html) del portal HTML legacy del OSD.
 * CRUD de indicadores asociados a líneas temáticas, con filtro por línea.
 */
import { initPortal, fetchJson, apiUrl } from "../portal/layout.js";
import { iconButton, iconActionsHtml } from "../shared/icon-actions.js";

let indicadores = [];
let lineas = [];

function msg(texto, tipo = "") {
  const el = document.getElementById("msg");
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
}

function esc(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function llenarSelectLineas(sel, incluirTodas = false) {
  sel.innerHTML = incluirTodas ? '<option value="">— Todas las líneas —</option>' : "";
  for (const l of lineas) {
    const o = document.createElement("option");
    o.value = l.id;
    o.textContent = l.nombre;
    sel.appendChild(o);
  }
}

/**
 * Carga el catálogo de líneas temáticas activas en los selectores de filtro y formulario.
 * @returns {Promise<void>}
 */
async function cargarLineas() {
  const { res, data } = await fetchJson(apiUrl("/api/admin/lineas-tematicas"));
  if (!res.ok) throw new Error(data.error || res.status);
  lineas = (data.lineas_tematicas || []).filter((l) => l.activo);
  llenarSelectLineas(document.getElementById("filtro-linea"), true);
  llenarSelectLineas(document.getElementById("f-linea"), false);
}

/**
 * Carga y renderiza la tabla de indicadores, opcionalmente filtrada por línea temática.
 * @returns {Promise<void>}
 */
async function cargar() {
  const filtro = document.getElementById("filtro-linea").value;
  const q = filtro ? `?linea_tematica_id=${filtro}` : "";
  const { res, data } = await fetchJson(apiUrl(`/api/admin/indicadores${q}`));
  if (!res.ok) throw new Error(data.error || res.status);
  indicadores = data.indicadores || [];
  const tb = document.getElementById("tb");
  if (!indicadores.length) {
    tb.innerHTML = `<tr><td colspan="6">Sin indicadores.</td></tr>`;
    return;
  }
  tb.innerHTML = indicadores
    .map(
      (i) => `<tr class="${i.activo ? "" : "fila-inactiva"}">
      <td>${i.id}</td><td>${esc(i.linea_tematica)}</td><td>${esc(i.codigo)}</td><td>${esc(i.nombre)}</td>
      <td>${i.activo ? "Sí" : "No"}</td>
      <td class="celda-acciones acciones-celda">${iconActionsHtml(iconButton("edit", "Editar indicador", { attrs: `data-edit="${i.id}"` }))}</td></tr>`
    )
    .join("");
  tb.querySelectorAll("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => abrirEditar(Number(b.dataset.edit)))
  );
}

function cerrarForm() {
  document.getElementById("panel-form").hidden = true;
}

/** Abre el formulario en modo creación de un nuevo indicador. */
function abrirNuevo() {
  document.getElementById("titulo-form").textContent = "Nuevo indicador";
  document.getElementById("f-id").value = "";
  document.getElementById("f-codigo").value = "";
  document.getElementById("f-codigo").disabled = false;
  document.getElementById("f-nombre").value = "";
  document.getElementById("f-descripcion").value = "";
  const filtro = document.getElementById("filtro-linea").value;
  if (filtro) document.getElementById("f-linea").value = filtro;
  document.getElementById("wrap-activo").hidden = true;
  document.getElementById("panel-form").hidden = false;
}

/**
 * Abre el formulario en modo edición para el indicador indicado.
 * @param {number} id - Identificador del indicador.
 */
function abrirEditar(id) {
  const i = indicadores.find((x) => x.id === id);
  if (!i) return;
  document.getElementById("titulo-form").textContent = "Editar indicador";
  document.getElementById("f-id").value = i.id;
  document.getElementById("f-linea").value = i.linea_tematica_id;
  document.getElementById("f-codigo").value = i.codigo;
  document.getElementById("f-codigo").disabled = true;
  document.getElementById("f-nombre").value = i.nombre;
  document.getElementById("f-descripcion").value = i.descripcion || "";
  document.getElementById("f-activo").checked = i.activo;
  document.getElementById("wrap-activo").hidden = false;
  document.getElementById("panel-form").hidden = false;
}

document.getElementById("filtro-linea")?.addEventListener("change", () => cargar().catch((e) => msg(e.message, "error")));
document.getElementById("btn-nuevo")?.addEventListener("click", abrirNuevo);
document.getElementById("btn-cancelar")?.addEventListener("click", cerrarForm);

document.getElementById("form-ind")?.addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const id = document.getElementById("f-id").value;
  const body = {
    linea_tematica_id: Number(document.getElementById("f-linea").value),
    codigo: document.getElementById("f-codigo").value.trim(),
    nombre: document.getElementById("f-nombre").value.trim(),
    descripcion: document.getElementById("f-descripcion").value.trim() || null,
    activo: document.getElementById("f-activo").checked,
  };
  try {
    if (id) {
      const { res, data } = await fetchJson(apiUrl(`/api/admin/indicadores/${id}`), {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || res.status);
      msg("Indicador actualizado.", "ok");
    } else {
      const { res, data } = await fetchJson(apiUrl("/api/admin/indicadores"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || res.status);
      msg("Indicador creado.", "ok");
    }
    cerrarForm();
    await cargar();
  } catch (e) {
    msg(e.message, "error");
  }
});

if (!(await initPortal("/admin/indicadores.html", { requiereAdmin: true }))) throw 0;
try {
  await cargarLineas();
  await cargar();
} catch (e) {
  msg(e.message, "error");
}
