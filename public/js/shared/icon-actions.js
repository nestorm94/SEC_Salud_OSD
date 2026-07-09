/**
 * @fileoverview Botones de acción con iconos Material Icons para tablas del portal HTML legacy del OSD.
 */

/**
 * Genera HTML de un botón con icono Material y etiqueta accesible.
 * @param {string} icon - Nombre del icono Material (p. ej. "edit", "delete").
 * @param {string} label - Texto para title y aria-label.
 * @param {{ variant?: string, attrs?: string }} [opts] - Variante CSS y atributos HTML extra.
 * @returns {string} HTML del botón.
 */
export function iconButton(icon, label, { variant = "", attrs = "" } = {}) {
  const v = variant ? ` icon-btn--${variant}` : "";
  return `<button type="button" class="icon-btn${v}" title="${label}" aria-label="${label}" ${attrs}><span class="material-icons" aria-hidden="true">${icon}</span></button>`;
}

/**
 * Envuelve uno o más botones de icono en un contenedor de acciones de tabla.
 * @param {string} buttonsHtml - HTML concatenado de botones iconButton.
 * @returns {string} HTML del contenedor .table-actions.
 */
export function iconActionsHtml(buttonsHtml) {
  return `<div class="table-actions">${buttonsHtml}</div>`;
}
