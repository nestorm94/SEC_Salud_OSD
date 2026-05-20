import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { guardarSesion, getToken } from "./auth.js";

const params = new URLSearchParams(window.location.search);
const next = params.get("next") || "/index.html";

if (getToken()) {
  window.location.href = next;
}

function setMensaje(texto, tipo) {
  const el = document.getElementById("mensaje-login");
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
}

document.getElementById("form-login").addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const usuario = document.getElementById("usuario").value.trim();
  const password = document.getElementById("password").value;
  setMensaje("Autenticando…", "");

  try {
    const { res, data } = await fetchJson(apiUrl("/api/auth/login"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ usuario, password }),
    });
    if (!res.ok) throw new Error(data.error || `Error ${res.status}`);
    guardarSesion(data.token, data.usuario);
    window.location.href = next;
  } catch (e) {
    setMensaje(String(e.message), "error");
  }
});
