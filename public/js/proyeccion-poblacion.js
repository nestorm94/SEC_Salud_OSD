import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";

const titulos = {
  "nacional-casanare": "Población nacional Casanare",
  "curso-vida": "Reporte población — curso de vida (unificado)",
  quinquenios: "Reporte población — quinquenios (unificado)",
};

/** Página actual por vista (clave hash) */
let paginaPorVista = {};
/** Filtros actuales por vista */
let filtrosPorVista = {};

function setMensaje(texto, tipo) {
  const el = document.getElementById("mensaje-proyeccion");
  el.textContent = texto;
  el.className = "mensaje" + (tipo ? ` ${tipo}` : "");
}

function claveDesdeHash() {
  const h = (location.hash || "#nacional-casanare").slice(1).toLowerCase();
  return titulos[h] ? h : "nacional-casanare";
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s == null ? "" : String(s);
  return d.innerHTML;
}

function formatearCelda(col, valor) {
  if (valor === null || valor === undefined) return "—";
  if (typeof valor === "number" && Number.isFinite(valor)) {
    const c = col.toLowerCase();
    if (c.includes("población") || c.includes("poblacion") || c === "año" || c === "ano")
      return valor.toLocaleString("es-CO");
    return String(valor);
  }
  return escapeHtml(String(valor));
}

function tamanoPaginaActual() {
  const sel = document.getElementById("select-tamano-pagina");
  const n = parseInt(sel.value, 10);
  return Number.isFinite(n) ? n : 10;
}

function obtenerPagina(clave) {
  const p = paginaPorVista[clave];
  return typeof p === "number" && p >= 1 ? p : 1;
}

function establecerPagina(clave, p) {
  paginaPorVista[clave] = Math.max(1, p);
}

function leerFiltrosUI() {
  const territorio = document.getElementById("filtro-territorio").value.trim();
  const regional = document.getElementById("filtro-regional").value.trim();
  const area = document.getElementById("filtro-area").value.trim();
  const sexo = document.getElementById("filtro-sexo").value.trim();
  const anoRaw = document.getElementById("filtro-ano").value.trim();
  const ano = anoRaw ? parseInt(anoRaw, 10) : null;
  return {
    territorio: territorio || "",
    regional: regional || "",
    area: area || "",
    sexo: sexo || "",
    ano: Number.isFinite(ano) ? String(ano) : "",
  };
}

function aplicarFiltrosUI(f) {
  document.getElementById("filtro-territorio").value = f?.territorio || "";
  document.getElementById("filtro-regional").value = f?.regional || "";
  document.getElementById("filtro-area").value = f?.area || "";
  document.getElementById("filtro-sexo").value = f?.sexo || "";
  document.getElementById("filtro-ano").value = f?.ano || "";
}

async function cargarVista(opciones = {}) {
  const { resetPagina } = opciones;
  const clave = claveDesdeHash();

  if (resetPagina) {
    establecerPagina(clave, 1);
  }

  const tamano = tamanoPaginaActual();
  let pagina = obtenerPagina(clave);
  const filtros = filtrosPorVista[clave] || leerFiltrosUI();
  filtrosPorVista[clave] = filtros;
  aplicarFiltrosUI(filtros);

  document.getElementById("titulo-vista").textContent = titulos[clave] || clave;
  setMensaje("Cargando…", "");

  const thead = document.getElementById("tabla-head");
  const tbody = document.getElementById("tabla-body");
  thead.innerHTML = "";
  tbody.innerHTML = "";

  const barraPag = document.getElementById("barra-paginacion");
  barraPag.hidden = true;

  document.querySelectorAll(".proyeccion-vistas a").forEach((a) => {
    a.classList.toggle("activo", a.dataset.clave === clave);
  });

  const params = new URLSearchParams({
    pagina: String(pagina),
    tamanoPagina: String(tamano),
  });
  if (filtros.territorio) params.set("territorio", filtros.territorio);
  if (filtros.regional) params.set("regional", filtros.regional);
  if (filtros.area) params.set("area", filtros.area);
  if (filtros.sexo) params.set("sexo", filtros.sexo);
  if (filtros.ano) params.set("ano", filtros.ano);

  try {
    const { res, data } = await fetchJson(
      apiUrl(`/api/proyeccion-poblacion/${encodeURIComponent(clave)}?${params}`)
    );
    if (!res.ok) {
      throw new Error(
        data.error || data.title || data.detail || `Error HTTP ${res.status}`
      );
    }

    const columnas = data.columnas || [];
    const filas = data.filas || [];
    const totalFilas = Number(data.totalFilas) || 0;
    const totalPaginas = Number(data.totalPaginas) || 0;
    const paginaSrv = Number(data.pagina) || pagina;
    const tamSrv = Number(data.tamanoPagina) || tamano;

    establecerPagina(clave, paginaSrv);

    if (columnas.length === 0 && totalFilas === 0) {
      setMensaje("No hay registros en esta vista.", "");
      return;
    }
    if (columnas.length === 0) {
      setMensaje("La vista no devolvió columnas.", "error");
      return;
    }

    const trh = document.createElement("tr");
    for (const c of columnas) {
      const th = document.createElement("th");
      th.textContent = c;
      trh.appendChild(th);
    }
    thead.appendChild(trh);

    for (const fila of filas) {
      const tr = document.createElement("tr");
      for (const c of columnas) {
        const td = document.createElement("td");
        td.innerHTML = formatearCelda(c, fila[c]);
        tr.appendChild(td);
      }
      tbody.appendChild(tr);
    }

    document.getElementById("paginacion-info").textContent =
      totalFilas > 0
        ? `Total: ${totalFilas.toLocaleString("es-CO")} registro(s) · Página ${paginaSrv} de ${Math.max(1, totalPaginas)} · ${tamSrv} por página`
        : "Sin registros";

    const btnAnt = document.getElementById("btn-pagina-anterior");
    const btnSig = document.getElementById("btn-pagina-siguiente");
    btnAnt.disabled = paginaSrv <= 1;
    btnSig.disabled = totalPaginas <= 0 || paginaSrv >= totalPaginas;

    barraPag.hidden = totalFilas <= tamSrv && totalPaginas <= 1;

    setMensaje(
      totalFilas > 10 || totalPaginas > 1
        ? `Mostrando ${filas.length} fila(s) en esta página.`
        : `Mostrando ${filas.length} fila(s).`,
      "exito"
    );
  } catch (e) {
    setMensaje(String(e.message), "error");
  }
}

document.getElementById("btn-cargar").addEventListener("click", () => cargarVista({ resetPagina: true }));

document.getElementById("select-tamano-pagina").addEventListener("change", () => cargarVista({ resetPagina: true }));

function onCambioFiltros() {
  const clave = claveDesdeHash();
  filtrosPorVista[clave] = leerFiltrosUI();
  cargarVista({ resetPagina: true });
}

["filtro-territorio", "filtro-regional", "filtro-area", "filtro-sexo", "filtro-ano"].forEach((id) => {
  const el = document.getElementById(id);
  if (!el) return;
  el.addEventListener("change", onCambioFiltros);
});

document.getElementById("btn-limpiar-filtros").addEventListener("click", () => {
  const clave = claveDesdeHash();
  filtrosPorVista[clave] = { territorio: "", regional: "", area: "", sexo: "", ano: "" };
  aplicarFiltrosUI(filtrosPorVista[clave]);
  cargarVista({ resetPagina: true });
});

document.getElementById("btn-pagina-anterior").addEventListener("click", () => {
  const clave = claveDesdeHash();
  establecerPagina(clave, obtenerPagina(clave) - 1);
  cargarVista();
});

document.getElementById("btn-pagina-siguiente").addEventListener("click", () => {
  const clave = claveDesdeHash();
  establecerPagina(clave, obtenerPagina(clave) + 1);
  cargarVista();
});

document.querySelectorAll(".proyeccion-vistas a").forEach((a) => {
  a.addEventListener("click", (ev) => {
    ev.preventDefault();
    const clave = a.dataset.clave;
    if (clave) location.hash = clave;
  });
});

window.addEventListener("hashchange", () => cargarVista({ resetPagina: true }));

if (!location.hash) {
  location.hash = "nacional-casanare";
} else {
  cargarVista();
}
