/** Diálogos del portal (mismo estilo que cargas.html / portal.css). */

let listo = false;

function asegurarModal() {
  if (listo) return;
  const html = `
<div id="modal-sistema" class="modal-overlay" hidden role="presentation">
  <div class="modal-panel modal-panel--dialogo" role="dialog" aria-modal="true" aria-labelledby="modal-sistema-titulo">
    <button type="button" class="modal-cerrar" id="modal-sistema-cerrar-x" aria-label="Cerrar">×</button>
    <h2 id="modal-sistema-titulo"></h2>
    <p id="modal-sistema-mensaje" class="modal-mensaje"></p>
    <div id="modal-sistema-campo" class="modal-campo" hidden>
      <label id="modal-sistema-label" for="modal-sistema-input"></label>
      <textarea id="modal-sistema-input" rows="3" class="input-portal"></textarea>
      <p id="modal-sistema-error" class="mensaje error" hidden></p>
    </div>
    <div class="modal-acciones" id="modal-sistema-acciones"></div>
  </div>
</div>`;
  document.body.insertAdjacentHTML("beforeend", html);

  const overlay = document.getElementById("modal-sistema");
  overlay.addEventListener("click", (ev) => {
    if (ev.target === overlay && overlay.dataset.cancelOnBackdrop === "1") {
      overlay._resolver?.(false);
      cerrar();
    }
  });
  document.getElementById("modal-sistema-cerrar-x").addEventListener("click", () => {
    overlay._resolver?.(false);
    cerrar();
  });
  document.addEventListener("keydown", (ev) => {
    if (ev.key === "Escape" && !overlay.hidden) {
      overlay._resolver?.(false);
      cerrar();
    }
  });
  listo = true;
}

function overlay() {
  return document.getElementById("modal-sistema");
}

function cerrar() {
  const el = overlay();
  if (!el) return;
  el.hidden = true;
  el._resolver = null;
  document.body.style.overflow = "";
}

function abrir() {
  const el = overlay();
  el.hidden = false;
  document.body.style.overflow = "hidden";
}

function limpiarAcciones() {
  document.getElementById("modal-sistema-acciones").innerHTML = "";
}

function boton(texto, clase, onClick) {
  const b = document.createElement("button");
  b.type = "button";
  b.className = clase;
  b.textContent = texto;
  b.addEventListener("click", onClick);
  document.getElementById("modal-sistema-acciones").appendChild(b);
  return b;
}

/**
 * @param {{ titulo?: string, mensaje: string, aceptar?: string, cancelar?: string, peligro?: boolean }} opts
 * @returns {Promise<boolean>}
 */
export function confirmar(opts) {
  asegurarModal();
  const el = overlay();
  return new Promise((resolve) => {
    el._resolver = resolve;
    el.dataset.cancelOnBackdrop = "1";
    document.getElementById("modal-sistema-titulo").textContent =
      opts.titulo || "Confirmar";
    document.getElementById("modal-sistema-mensaje").textContent = opts.mensaje;
    document.getElementById("modal-sistema-campo").hidden = true;
    document.getElementById("modal-sistema-error").hidden = true;
    limpiarAcciones();
    boton(opts.cancelar || "Cancelar", "btn secundario", () => {
      resolve(false);
      cerrar();
    });
    boton(
      opts.aceptar || "Aceptar",
      opts.peligro ? "btn btn-dialogo-peligro" : "btn",
      () => {
        resolve(true);
        cerrar();
      }
    ).focus();
    abrir();
  });
}

/**
 * @param {{ titulo?: string, mensaje?: string, label?: string, placeholder?: string, obligatorio?: boolean, valorInicial?: string }} opts
 * @returns {Promise<string|null>} null si canceló
 */
export function solicitarTexto(opts) {
  asegurarModal();
  const el = overlay();
  return new Promise((resolve) => {
    el._resolver = (ok) => resolve(ok ? null : null);
    el.dataset.cancelOnBackdrop = "1";
    document.getElementById("modal-sistema-titulo").textContent =
      opts.titulo || "Observaciones";
    document.getElementById("modal-sistema-mensaje").textContent = opts.mensaje || "";
    document.getElementById("modal-sistema-mensaje").hidden = !opts.mensaje;

    const campo = document.getElementById("modal-sistema-campo");
    campo.hidden = false;
    const label = document.getElementById("modal-sistema-label");
    label.textContent = opts.label || "Comentario";
    const input = document.getElementById("modal-sistema-input");
    input.value = opts.valorInicial || "";
    input.placeholder = opts.placeholder || "";
    const err = document.getElementById("modal-sistema-error");
    err.hidden = true;

    const cancelar = () => {
      resolve(null);
      cerrar();
    };

    const aceptar = () => {
      const v = input.value.trim();
      if (opts.obligatorio && !v) {
        err.textContent = "Este campo es obligatorio.";
        err.hidden = false;
        input.focus();
        return;
      }
      resolve(v);
      cerrar();
    };

    limpiarAcciones();
    boton("Cancelar", "btn secundario", cancelar);
    const btnOk = boton("Aceptar", "btn", aceptar);
    abrir();
    input.focus();
    input.onkeydown = (ev) => {
      if (ev.key === "Enter" && !ev.shiftKey) {
        ev.preventDefault();
        aceptar();
      }
    };
    el._resolver = cancelar;
  });
}

/**
 * @param {{ titulo?: string, mensaje: string, tipo?: 'ok'|'error'|'info' }} opts
 */
export function mostrarMensaje(opts) {
  asegurarModal();
  const el = overlay();
  return new Promise((resolve) => {
    el.dataset.cancelOnBackdrop = "0";
    document.getElementById("modal-sistema-titulo").textContent =
      opts.titulo || (opts.tipo === "error" ? "Error" : "Aviso");
    const msg = document.getElementById("modal-sistema-mensaje");
    msg.textContent = opts.mensaje;
    msg.hidden = false;
    msg.className =
      "modal-mensaje" + (opts.tipo === "error" ? " mensaje error" : opts.tipo === "ok" ? " mensaje ok" : "");
    document.getElementById("modal-sistema-campo").hidden = true;
    limpiarAcciones();
    boton("Cerrar", "btn", () => {
      resolve();
      cerrar();
    });
    abrir();
  });
}
