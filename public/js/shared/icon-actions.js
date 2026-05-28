/** Botones de acción con iconos Material (portal estático). */
export function iconButton(icon, label, { variant = "", attrs = "" } = {}) {
  const v = variant ? ` icon-btn--${variant}` : "";
  return `<button type="button" class="icon-btn${v}" title="${label}" aria-label="${label}" ${attrs}><span class="material-icons" aria-hidden="true">${icon}</span></button>`;
}

export function iconActionsHtml(buttonsHtml) {
  return `<div class="table-actions">${buttonsHtml}</div>`;
}
