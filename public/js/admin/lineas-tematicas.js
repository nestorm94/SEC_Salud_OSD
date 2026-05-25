import { initPortal, fetchJson, apiUrl } from "../portal/layout.js";

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

async function cargar() {
  const { res, data } = await fetchJson(apiUrl("/api/admin/lineas-tematicas"));
  if (!res.ok) throw new Error(data.error || res.status);
  lineas = data.lineas_tematicas || [];
  const tb = document.getElementById("tb");
  if (!lineas.length) {
    tb.innerHTML = `<tr><td colspan="5">Sin líneas temáticas.</td></tr>`;
    return;
  }
  tb.innerHTML = lineas
    .map(
      (l) => `<tr class="${l.activo ? "" : "fila-inactiva"}">
      <td>${l.id}</td><td>${esc(l.codigo)}</td><td>${esc(l.nombre)}</td>
      <td>${l.activo ? "Sí" : "No"}</td>
      <td class="celda-acciones">
        <button type="button" class="btn-mini" data-edit="${l.id}">Editar</button>
      </td></tr>`
    )
    .join("");
  tb.querySelectorAll("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => abrirEditar(Number(b.dataset.edit)))
  );
}

function cerrarForm() {
  document.getElementById("panel-form").hidden = true;
}

function abrirNuevo() {
  document.getElementById("titulo-form").textContent = "Nueva línea temática";
  document.getElementById("f-id").value = "";
  document.getElementById("f-codigo").value = "";
  document.getElementById("f-codigo").disabled = false;
  document.getElementById("f-nombre").value = "";
  document.getElementById("f-descripcion").value = "";
  document.getElementById("wrap-activo").hidden = true;
  document.getElementById("panel-form").hidden = false;
}

function abrirEditar(id) {
  const l = lineas.find((x) => x.id === id);
  if (!l) return;
  document.getElementById("titulo-form").textContent = "Editar línea temática";
  document.getElementById("f-id").value = l.id;
  document.getElementById("f-codigo").value = l.codigo;
  document.getElementById("f-codigo").disabled = true;
  document.getElementById("f-nombre").value = l.nombre;
  document.getElementById("f-descripcion").value = l.descripcion || "";
  document.getElementById("f-activo").checked = l.activo;
  document.getElementById("wrap-activo").hidden = false;
  document.getElementById("panel-form").hidden = false;
}

document.getElementById("btn-nuevo")?.addEventListener("click", abrirNuevo);
document.getElementById("btn-cancelar")?.addEventListener("click", cerrarForm);

document.getElementById("form-linea")?.addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const id = document.getElementById("f-id").value;
  const body = {
    codigo: document.getElementById("f-codigo").value.trim(),
    nombre: document.getElementById("f-nombre").value.trim(),
    descripcion: document.getElementById("f-descripcion").value.trim() || null,
    activo: document.getElementById("f-activo").checked,
  };
  try {
    if (id) {
      const { res, data } = await fetchJson(apiUrl(`/api/admin/lineas-tematicas/${id}`), {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || res.status);
      msg("Línea temática actualizada.", "ok");
    } else {
      const { res, data } = await fetchJson(apiUrl("/api/admin/lineas-tematicas"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || res.status);
      msg("Línea temática creada.", "ok");
    }
    cerrarForm();
    await cargar();
  } catch (e) {
    msg(e.message, "error");
  }
});

if (!(await initPortal("/admin/lineas-tematicas.html", { requiereAdmin: true }))) throw 0;
try {
  await cargar();
} catch (e) {
  msg(e.message, "error");
}
