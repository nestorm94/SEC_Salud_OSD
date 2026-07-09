/**
 * @fileoverview Administración de usuarios (admin/usuarios.html) del portal HTML legacy del OSD.
 * CRUD de usuarios, asignación de roles y líneas temáticas.
 */
import { initPortal, fetchJson, apiUrl } from "../portal/layout.js";
import { getUsuario } from "../auth.js";
import { iconButton, iconActionsHtml } from "../shared/icon-actions.js";

let lineasTematicas = [];
let rolesDisponibles = [];
let usuarioActualId = null;

const ROLES_ADMIN = new Set(["ADMIN", "ADMINISTRADOR", "ADMINISTRADOR"]);

/**
 * Inicializa la página de administración de usuarios: portal, catálogos y tabla.
 * @returns {Promise<void>}
 */
export async function initAdminUsuarios() {
  if (!(await initPortal("/admin/usuarios.html", { requiereAdmin: true }))) return;

  usuarioActualId = getUsuario()?.id ?? null;
  await cargarCatalogos();
  enlazarFormularios();
  await refrescarTabla();
}

async function cargarCatalogos() {
  const [lineaRes, rolRes] = await Promise.all([
    fetchJson(apiUrl("/api/admin/lineas-tematicas")),
    fetchJson(apiUrl("/api/admin/roles")),
  ]);
  lineasTematicas = lineaRes.res.ok ? lineaRes.data.lineas_tematicas || [] : [];
  rolesDisponibles = rolRes.res.ok ? rolRes.data.roles || [] : [];
  llenarSelectLineas(document.getElementById("f-linea-tematica"));
  llenarSelectLineas(document.getElementById("e-linea-tematica"));
  llenarRolesCheckboxes("roles-nuevo", ["RESPONSABLE_TEMATICO"]);
  llenarRolesCheckboxes("roles-editar", []);
}

function llenarSelectLineas(sel) {
  if (!sel) return;
  sel.innerHTML = '<option value="">— Sin asignar (acceso total si es admin) —</option>';
  lineasTematicas
    .filter((l) => l.activo !== false)
    .forEach((l) => {
      const o = document.createElement("option");
      o.value = l.id;
      o.textContent = l.nombre;
      sel.appendChild(o);
    });
}

function llenarRolesCheckboxes(containerId, seleccionados) {
  const box = document.getElementById(containerId);
  if (!box) return;
  const set = new Set((seleccionados || []).map((r) => r.toUpperCase()));
  box.innerHTML = rolesDisponibles
    .map(
      (r) => `
    <label class="chk-rol">
      <input type="checkbox" name="rol" value="${esc(r.nombre)}" ${set.has(r.nombre.toUpperCase()) ? "checked" : ""} />
      ${esc(r.nombre)}
    </label>`
    )
    .join("");
}

function rolesSeleccionados(containerId) {
  const box = document.getElementById(containerId);
  if (!box) return [];
  return [...box.querySelectorAll('input[name="rol"]:checked')].map((i) => i.value);
}

function esRolAdmin(roles) {
  return (roles || []).some((r) => ROLES_ADMIN.has(String(r).toUpperCase()));
}

function esc(s) {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/"/g, "&quot;");
}

async function refrescarTabla() {
  const tbody = document.getElementById("tb");
  const msg = document.getElementById("msg-usuarios");
  tbody.innerHTML = `<tr><td colspan="7">Cargando…</td></tr>`;
  msg.textContent = "";

  try {
    const { res, data } = await fetchJson(apiUrl("/api/admin/usuarios"));
    if (!res.ok) throw new Error(data.error || res.status);

    const rows = data.usuarios || [];
    if (!rows.length) {
      tbody.innerHTML = `<tr><td colspan="7">No hay usuarios.</td></tr>`;
      return;
    }

    tbody.innerHTML = rows
      .map((u) => {
        const esYo = usuarioActualId === u.id;
        let acciones = '<span class="hint">(usted)</span>';
        if (!esYo) {
          const btns = [
            iconButton("edit", "Editar usuario", { attrs: `data-editar="${u.id}"` }),
            u.activo
              ? iconButton("person_off", "Desactivar usuario", {
                  variant: "danger",
                  attrs: `data-desactivar="${u.id}"`,
                })
              : iconButton("person_add", "Activar usuario", {
                  variant: "success",
                  attrs: `data-activar="${u.id}"`,
                }),
          ];
          if (u.activo) {
            btns.push(
              iconButton("delete", "Eliminar (desactivar) usuario", {
                variant: "danger",
                attrs: `data-eliminar="${u.id}"`,
              })
            );
          }
          acciones = iconActionsHtml(btns.join(""));
        }
        return `<tr class="${u.activo ? "" : "fila-inactiva"}">
          <td>${u.id}</td>
          <td>${esc(u.nombre_usuario)}</td>
          <td>${esc(u.email)}</td>
          <td>${esc(u.linea_tematica || "—")}</td>
          <td>${(u.roles || []).map(esc).join(", ")}</td>
          <td>${u.activo ? "Sí" : "No"}</td>
          <td class="celda-acciones acciones-celda">${acciones}</td>
        </tr>`;
      })
      .join("");

    tbody.querySelectorAll("[data-editar]").forEach((btn) =>
      btn.addEventListener("click", () => abrirEditar(Number(btn.dataset.editar)))
    );
    tbody.querySelectorAll("[data-desactivar]").forEach((btn) =>
      btn.addEventListener("click", () => cambiarActivo(Number(btn.dataset.desactivar), false))
    );
    tbody.querySelectorAll("[data-activar]").forEach((btn) =>
      btn.addEventListener("click", () => cambiarActivo(Number(btn.dataset.activar), true))
    );
    tbody.querySelectorAll("[data-eliminar]").forEach((btn) =>
      btn.addEventListener("click", () => eliminarUsuario(Number(btn.dataset.eliminar)))
    );
  } catch (e) {
    tbody.innerHTML = `<tr><td colspan="7" class="error">${esc(e.message)}</td></tr>`;
  }
}

function enlazarFormularios() {
  document.getElementById("btn-nuevo")?.addEventListener("click", () => {
    document.getElementById("panel-nuevo").hidden = false;
    document.getElementById("form-nuevo").reset();
    llenarRolesCheckboxes("roles-nuevo", ["RESPONSABLE_TEMATICO"]);
  });
  document.getElementById("btn-cancelar-nuevo")?.addEventListener("click", () => {
    document.getElementById("panel-nuevo").hidden = true;
  });

  document.getElementById("form-nuevo")?.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const msg = document.getElementById("msg-usuarios");
    const roles = rolesSeleccionados("roles-nuevo");
    const lineaId = parseInt(document.getElementById("f-linea-tematica").value, 10) || null;

    if (!roles.length) {
      msg.textContent = "Seleccione al menos un rol.";
      msg.className = "mensaje error";
      return;
    }
    if (!esRolAdmin(roles) && !lineaId) {
      msg.textContent = "Los usuarios que no son administradores deben tener una línea temática asignada.";
      msg.className = "mensaje error";
      return;
    }

    const body = {
      nombre_usuario: document.getElementById("f-usuario").value.trim(),
      password: document.getElementById("f-password").value,
      email: document.getElementById("f-email").value.trim() || null,
      linea_tematica_id: lineaId,
      roles,
    };

    try {
      const { res, data } = await fetchJson(apiUrl("/api/admin/usuarios"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || "No se pudo crear");
      msg.textContent = "Usuario creado.";
      msg.className = "mensaje ok";
      document.getElementById("panel-nuevo").hidden = true;
      await refrescarTabla();
    } catch (e) {
      msg.textContent = e.message;
      msg.className = "mensaje error";
    }
  });

  document.getElementById("form-editar")?.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const id = Number(document.getElementById("e-id").value);
    const msg = document.getElementById("msg-editar");
    const roles = rolesSeleccionados("roles-editar");
    const lineaId = parseInt(document.getElementById("e-linea-tematica").value, 10) || null;

    if (!roles.length) {
      msg.textContent = "Seleccione al menos un rol.";
      msg.className = "mensaje error";
      return;
    }
    if (!esRolAdmin(roles) && !lineaId) {
      msg.textContent = "Asigne una línea temática (obligatorio si no es administrador).";
      msg.className = "mensaje error";
      return;
    }

    const body = {
      email: document.getElementById("e-email").value.trim() || null,
      linea_tematica_id: lineaId,
      roles,
    };
    const pwd = document.getElementById("e-password").value;
    if (pwd) body.password = pwd;

    try {
      const { res, data } = await fetchJson(apiUrl(`/api/admin/usuarios/${id}`), {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(data.error || "No se pudo guardar");
      msg.textContent = "Cambios guardados. El usuario debe volver a iniciar sesión para ver la línea asignada.";
      msg.className = "mensaje ok";
      cerrarModal();
      await refrescarTabla();
    } catch (e) {
      msg.textContent = e.message;
      msg.className = "mensaje error";
    }
  });

  document.getElementById("modal-cerrar-x")?.addEventListener("click", cerrarModal);
  document.getElementById("modal-cerrar-2")?.addEventListener("click", cerrarModal);
  document.getElementById("modal-editar")?.addEventListener("click", (ev) => {
    if (ev.target.id === "modal-editar") cerrarModal();
  });
}

async function abrirEditar(id) {
  const msg = document.getElementById("msg-editar");
  msg.textContent = "";
  try {
    const { res, data } = await fetchJson(apiUrl(`/api/admin/usuarios/${id}`));
    if (!res.ok) throw new Error(data.error || "No encontrado");

    document.getElementById("e-id").value = data.id;
    document.getElementById("e-usuario").value = data.nombre_usuario;
    document.getElementById("e-email").value = data.email || "";
    document.getElementById("e-password").value = "";
    document.getElementById("e-linea-tematica").value = data.linea_tematica_id ?? "";
    document.getElementById("e-activo").checked = data.activo;
    llenarRolesCheckboxes("roles-editar", data.roles || []);

    document.getElementById("modal-editar").hidden = false;
  } catch (e) {
    alert(e.message);
  }
}

function cerrarModal() {
  document.getElementById("modal-editar").hidden = true;
}

async function eliminarUsuario(id) {
  if (!confirm(`¿Desactivar el usuario #${id}?`)) return;
  const msg = document.getElementById("msg-usuarios");
  try {
    const { res, data } = await fetchJson(apiUrl(`/api/admin/usuarios/${id}`), { method: "DELETE" });
    if (!res.ok) throw new Error(data.error || "Error");
    msg.textContent = "Usuario desactivado.";
    msg.className = "mensaje ok";
    await refrescarTabla();
  } catch (e) {
    msg.textContent = e.message;
    msg.className = "mensaje error";
  }
}

async function cambiarActivo(id, activo) {
  const accion = activo ? "activar" : "desactivar";
  if (!confirm(`¿Confirma ${accion} al usuario #${id}?`)) return;

  const msg = document.getElementById("msg-usuarios");
  try {
    const { res, data } = await fetchJson(apiUrl(`/api/admin/usuarios/${id}/activo`), {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ activo }),
    });
    if (!res.ok) throw new Error(data.error || "Error");
    msg.textContent = activo ? "Usuario activado." : "Usuario desactivado.";
    msg.className = "mensaje ok";
    await refrescarTabla();
  } catch (e) {
    msg.textContent = e.message;
    msg.className = "mensaje error";
  }
}

initAdminUsuarios();
