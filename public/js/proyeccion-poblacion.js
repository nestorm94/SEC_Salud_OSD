import { apiUrl } from "./config.js";
import { fetchJson } from "./fetchJson.js";
import { getToken, tokenExpirado, authHeaders } from "./auth.js";

const titulos = {
  "nacional-casanare": "Población nacional Casanare",
  "curso-vida": "Reporte población — curso de vida (unificado)",
  quinquenios: "Reporte población — quinquenios (unificado)",
};

/** Casanare (DANE 85) — consulta acotada por defecto */
const CODIGO_DEPARTAMENTO_CASANARE = "85";

function filtrosPorDefecto() {
  return {
    codigoDepartamento: CODIGO_DEPARTAMENTO_CASANARE,
    codigoMunicipio: "",
    regional: "",
    area: "",
    sexo: "",
    ano: "",
  };
}

function asegurarFiltrosVista(clave) {
  if (!filtrosPorVista[clave]) filtrosPorVista[clave] = filtrosPorDefecto();
  return filtrosPorVista[clave];
}

/** Página actual por vista (clave hash) */
let paginaPorVista = {};
/** Filtros actuales por vista */
let filtrosPorVista = {};

/** Catálogos cargados una vez */
let catalogosListos = false;
/** Evita renderizar respuestas viejas cuando hay llamadas simultáneas. */
let ultimaCargaVistaId = 0;

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
    if (c === "año" || c === "ano")
      return String(Math.trunc(valor));
    if (c.includes("población") || c.includes("poblacion"))
      return valor.toLocaleString("es-CO");
    return String(valor);
  }
  return escapeHtml(String(valor));
}

async function descargarConsultaExcel() {
  const clave = claveDesdeHash();
  const filtros = leerFiltrosUI();
  const btn = document.getElementById("btn-descargar-csv");
  if (btn) btn.disabled = true;
  setMensaje("Generando Excel...", "");

  try {
    const params = new URLSearchParams();
    if (filtros.codigoDepartamento) params.set("codigoDepartamento", filtros.codigoDepartamento);
    if (filtros.codigoMunicipio) params.set("codigoMunicipio", filtros.codigoMunicipio);
    if (filtros.regional) params.set("regional", filtros.regional);
    if (filtros.area) params.set("area", filtros.area);
    if (filtros.sexo) params.set("sexo", filtros.sexo);
    if (filtros.ano) params.set("ano", filtros.ano);

    const resp = await fetch(
      apiUrl(`/api/proyeccion-poblacion/${encodeURIComponent(clave)}/excel?${params}`),
      { headers: authHeaders() }
    );
    if (!resp.ok) {
      let msg = `Error HTTP ${resp.status}`;
      try {
        const body = await resp.json();
        msg = body?.error || msg;
      } catch {
        // ignore body parse
      }
      throw new Error(msg);
    }

    const blob = await resp.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    const disp = resp.headers.get("content-disposition") || "";
    const match = /filename="?([^"]+)"?/i.exec(disp);
    a.href = url;
    a.download = match?.[1] || `proyeccion-${clave}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);

    setMensaje("Excel descargado correctamente.", "exito");
  } catch (e) {
    setMensaje(`No se pudo descargar Excel: ${String(e.message)}`, "error");
  } finally {
    if (btn) btn.disabled = false;
  }
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

function prop(obj, camel, pascal) {
  return obj?.[camel] ?? obj?.[pascal] ?? "";
}

function llenarSelect(selectEl, opciones, valorSeleccionado, placeholderTodos) {
  selectEl.innerHTML = "";
  const optTodos = document.createElement("option");
  optTodos.value = "";
  optTodos.textContent = placeholderTodos;
  selectEl.appendChild(optTodos);
  for (const o of opciones) {
    const opt = document.createElement("option");
    opt.value = o.value;
    opt.textContent = o.label;
    selectEl.appendChild(opt);
  }
  if (valorSeleccionado) selectEl.value = valorSeleccionado;
}

async function cargarCatalogos() {
  if (catalogosListos) return;
  if (!getToken() || tokenExpirado()) return;
  setMensaje("Cargando catálogos…", "");

  try {
    const { res, data } = await fetchJson(apiUrl("/api/catalogos/proyeccion"));
    if (!res.ok) throw new Error(data?.error || "No se pudieron cargar los catálogos.");

    const departamentos = (data.departamentos || []).map((d) => {
      const cod = prop(d, "codigoDane", "CodigoDane");
      const nom = prop(d, "nombreDepartamento", "NombreDepartamento");
      return { value: cod, label: `${cod} — ${nom}` };
    });

    const regionales = (data.regionales || []).map((r) => ({
      value: prop(r, "codigo", "Codigo"),
      label: prop(r, "nombre", "Nombre"),
    }));

    const areas = (data.areas || []).map((a) => ({
      value: prop(a, "codigo", "Codigo"),
      label: prop(a, "nombre", "Nombre"),
    }));

    const sexos = (data.sexos || []).map((s) => ({
      value: prop(s, "codigo", "Codigo"),
      label: prop(s, "nombre", "Nombre"),
    }));

    const anios = (data.anios || []).map((a) => ({
      value: prop(a, "codigo", "Codigo"),
      label: prop(a, "nombre", "Nombre"),
    }));

    llenarSelect(
      document.getElementById("filtro-departamento"),
      departamentos,
      "",
      "Todos los departamentos"
    );
    llenarSelect(document.getElementById("filtro-regional"), regionales, "", "Todas las regionales");
    llenarSelect(document.getElementById("filtro-area"), areas, "", "Todas las áreas");
    llenarSelect(document.getElementById("filtro-sexo"), sexos, "", "Todos los sexos");
    llenarSelect(document.getElementById("filtro-ano"), anios, "", "Todos los años");

    catalogosListos = true;
    setMensaje("", "");
  } catch (e) {
    const msg = String(e.message);
    if (
      msg.includes("Sesión expirada") ||
      msg.includes("401") ||
      msg.toLowerCase().includes("unauthorized")
    ) {
      return;
    }
    setMensaje(msg, "error");
  }
}

async function cargarMunicipiosPorDepartamento(codigoDepartamento, valorMunicipio = "") {
  const selMun = document.getElementById("filtro-municipio");
  const hint = document.getElementById("hint-municipio");

  if (!codigoDepartamento) {
    selMun.disabled = true;
    llenarSelect(selMun, [], "", "Todos los municipios");
    hint.textContent = "Seleccione un departamento para ver municipios.";
    return;
  }

  selMun.disabled = true;
  hint.textContent = "Cargando municipios…";

  try {
    const { res, data } = await fetchJson(
      apiUrl(`/api/catalogos/municipios/${encodeURIComponent(codigoDepartamento)}`)
    );
    if (!res.ok) throw new Error(data?.error || `Error HTTP ${res.status}`);

    const municipios = (data.municipios || []).map((m) => {
      const cod = prop(m, "codigoDane", "CodigoDane");
      const nom = prop(m, "nombreMunicipio", "NombreMunicipio");
      return { value: cod, label: `${cod} — ${nom}` };
    });

    llenarSelect(selMun, municipios, valorMunicipio, "Todos los municipios");
    selMun.disabled = false;
    hint.textContent = municipios.length
      ? `${municipios.length} municipio(s) disponibles.`
      : "Sin municipios para este departamento.";
  } catch (e) {
    llenarSelect(selMun, [], "", "Todos los municipios");
    selMun.disabled = true;
    hint.textContent = String(e.message);
  }
}

function leerFiltrosUI() {
  return {
    codigoDepartamento: document.getElementById("filtro-departamento").value.trim(),
    codigoMunicipio: document.getElementById("filtro-municipio").value.trim(),
    regional: document.getElementById("filtro-regional").value.trim(),
    area: document.getElementById("filtro-area").value.trim(),
    sexo: document.getElementById("filtro-sexo").value.trim(),
    ano: document.getElementById("filtro-ano").value.trim(),
  };
}

function aplicarFiltrosUI(f) {
  const dep = f?.codigoDepartamento || "";
  const mun = f?.codigoMunicipio || "";
  document.getElementById("filtro-departamento").value = dep;
  document.getElementById("filtro-regional").value = f?.regional || "";
  document.getElementById("filtro-area").value = f?.area || "";
  document.getElementById("filtro-sexo").value = f?.sexo || "";
  document.getElementById("filtro-ano").value = f?.ano || "";

  if (dep) {
    cargarMunicipiosPorDepartamento(dep, mun);
  } else {
    const selMun = document.getElementById("filtro-municipio");
    selMun.disabled = true;
    llenarSelect(selMun, [], "", "Todos los municipios");
    document.getElementById("hint-municipio").textContent =
      "Seleccione un departamento para ver municipios.";
  }
}

async function cargarVista(opciones = {}) {
  const cargaId = ++ultimaCargaVistaId;
  const { resetPagina } = opciones;
  const clave = claveDesdeHash();
  asegurarFiltrosVista(clave);

  if (!catalogosListos) await cargarCatalogos();

  if (resetPagina) {
    establecerPagina(clave, 1);
  }

  const tamano = tamanoPaginaActual();
  let pagina = obtenerPagina(clave);
  const filtros = leerFiltrosUI();
  filtrosPorVista[clave] = filtros;
  aplicarFiltrosUI(filtros);

  document.getElementById("titulo-vista").textContent = titulos[clave] || clave;
  setMensaje("Cargando datos…", "");

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
  if (filtros.codigoDepartamento) params.set("codigoDepartamento", filtros.codigoDepartamento);
  if (filtros.codigoMunicipio) params.set("codigoMunicipio", filtros.codigoMunicipio);
  if (filtros.regional) params.set("regional", filtros.regional);
  if (filtros.area) params.set("area", filtros.area);
  if (filtros.sexo) params.set("sexo", filtros.sexo);
  if (filtros.ano) params.set("ano", filtros.ano);

  try {
    const { res, data } = await fetchJson(
      apiUrl(`/api/proyeccion-poblacion/${encodeURIComponent(clave)}?${params}`)
    );
    if (cargaId !== ultimaCargaVistaId) return;
    if (!res.ok) {
      const err =
        data?.error ||
        data?.title ||
        data?.detail ||
        (res.status === 401 ? "Sesión expirada o no autorizado. Vuelva a iniciar sesión." : `Error HTTP ${res.status}`);
      throw new Error(err);
    }

    const columnas = data.columnas || [];
    const filas = data.filas || [];
    const totalFilas = Number(data.totalFilas) || 0;
    const totalPaginas = Number(data.totalPaginas) || 0;
    const paginaSrv = Number(data.pagina) || pagina;
    const tamSrv = Number(data.tamanoPagina) || tamano;

    establecerPagina(clave, paginaSrv);

    if (columnas.length === 0 && totalFilas === 0) {
      setMensaje("No hay registros con los filtros seleccionados.", "");
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

    const depLabel = filtros.codigoDepartamento
      ? document.querySelector(`#filtro-departamento option[value="${CSS.escape(filtros.codigoDepartamento)}"]`)?.textContent
      : "";
    const munLabel = filtros.codigoMunicipio
      ? document.querySelector(`#filtro-municipio option[value="${CSS.escape(filtros.codigoMunicipio)}"]`)?.textContent
      : "";
    let extra = "";
    if (depLabel) extra += ` · Depto: ${depLabel}`;
    if (munLabel) extra += ` · Mun: ${munLabel}`;

    setMensaje(
      `Mostrando ${filas.length} fila(s) en esta página${extra}.`,
      "exito"
    );
  } catch (e) {
    if (cargaId !== ultimaCargaVistaId) return;
    setMensaje(String(e.message), "error");
  }
}

function onCambioFiltros() {
  const clave = claveDesdeHash();
  filtrosPorVista[clave] = leerFiltrosUI();
  cargarVista({ resetPagina: true });
}

async function onDepartamentoChange() {
  const cod = document.getElementById("filtro-departamento").value.trim();
  document.getElementById("filtro-municipio").value = "";
  await cargarMunicipiosPorDepartamento(cod, "");
  onCambioFiltros();
}

document.getElementById("btn-cargar").addEventListener("click", () => cargarVista({ resetPagina: true }));
document.getElementById("btn-descargar-csv").addEventListener("click", descargarConsultaExcel);

document.getElementById("select-tamano-pagina").addEventListener("change", () => cargarVista({ resetPagina: true }));

document.getElementById("filtro-departamento").addEventListener("change", onDepartamentoChange);
document.getElementById("filtro-municipio").addEventListener("change", onCambioFiltros);
document.getElementById("filtro-regional").addEventListener("change", onCambioFiltros);
document.getElementById("filtro-area").addEventListener("change", onCambioFiltros);
document.getElementById("filtro-sexo").addEventListener("change", onCambioFiltros);
document.getElementById("filtro-ano").addEventListener("change", onCambioFiltros);

document.getElementById("btn-limpiar-filtros").addEventListener("click", () => {
  const clave = claveDesdeHash();
  filtrosPorVista[clave] = filtrosPorDefecto();
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

(async () => {
  await cargarCatalogos();
  const clave = claveDesdeHash();
  if (!location.hash) location.hash = "nacional-casanare";
  const f = asegurarFiltrosVista(claveDesdeHash());
  document.getElementById("filtro-departamento").value = f.codigoDepartamento;
  await cargarMunicipiosPorDepartamento(f.codigoDepartamento, f.codigoMunicipio);
  await cargarVista();
})();
