import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { requerirAuth, getUsuario, cerrarSesion, esAdministrador } from "./auth.js";

if (!requerirAuth()) throw new Error("redirect");

const u = getUsuario();
document.getElementById("usuario-info").textContent =
  `${u?.nombre || ""} · ${u?.dependencia || "Administrador"} · ${(u?.roles || []).join(", ")}`;

document.getElementById("btn-logout").addEventListener("click", () => {
  cerrarSesion();
  window.location.href = "/login.html";
});

function estadoClase(estado) {
  if (estado === "VALIDADO_OK" || estado === "APROBADO") return "exito";
  if (estado === "VALIDADO_CON_ERRORES" || estado === "RECHAZADO") return "error";
  return "";
}

async function cargarLista() {
  const msg = document.getElementById("mensaje-cargas");
  const cuerpo = document.getElementById("cuerpo-cargas");
  cuerpo.innerHTML = "";
  msg.textContent = "Cargando…";
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
        <td>${escapeHtml(String(c.fecha_inicio || ""))}</td>
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

      if (c.estado === "VALIDADO_OK") {
        const ba = document.createElement("button");
        ba.type = "button";
        ba.className = "btn primario";
        ba.textContent = "Aprobar";
        ba.addEventListener("click", () => aprobar(c.id));
        acc.appendChild(ba);
      }
      if (c.estado === "VALIDADO_OK" || c.estado === "VALIDADO_CON_ERRORES") {
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
  const panel = document.getElementById("panel-errores");
  document.getElementById("carga-errores-id").textContent = id;
  const cuerpo = document.getElementById("cuerpo-errores");
  cuerpo.innerHTML = "";
  const { res, data } = await fetchJson(apiUrl(`/api/cargas/${id}/errores`));
  if (!res.ok) return;
  for (const e of data.errores || []) {
    const tr = document.createElement("tr");
    tr.innerHTML = `<td>${e.fila ?? "—"}</td><td>${escapeHtml(e.columna || "—")}</td><td>${escapeHtml(e.tipo || "")}</td><td>${escapeHtml(e.mensaje)}</td>`;
    cuerpo.appendChild(tr);
  }
  panel.hidden = false;
}

async function verHistorial(cargaId) {
  const panel = document.getElementById("panel-historial");
  const lista = document.getElementById("lista-historial");
  lista.innerHTML = "";
  const { res, data } = await fetchJson(apiUrl(`/api/cargas/historial?carga_id=${cargaId}`));
  if (!res.ok) return;
  for (const h of data.historial || []) {
    const li = document.createElement("li");
    li.innerHTML = `<strong>${escapeHtml(h.accion)}</strong> — ${escapeHtml(h.usuario || "sistema")} — <time>${h.fecha}</time>${h.detalle ? `<br/><small>${escapeHtml(h.detalle)}</small>` : ""}`;
    lista.appendChild(li);
  }
  panel.hidden = false;
}

async function aprobar(id) {
  const obs = prompt("Observaciones (opcional):") || "";
  await fetchJson(apiUrl(`/api/cargas/${id}/aprobar`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  await cargarLista();
}

async function rechazar(id) {
  const obs = prompt("Motivo del rechazo:") || "";
  await fetchJson(apiUrl(`/api/cargas/${id}/rechazar`), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ observaciones: obs }),
  });
  await cargarLista();
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

document.getElementById("btn-refrescar").addEventListener("click", cargarLista);
cargarLista();
