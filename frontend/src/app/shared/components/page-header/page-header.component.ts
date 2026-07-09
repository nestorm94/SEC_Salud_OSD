import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Cabecera estándar de página con título, subtítulo opcional
 * y ranura para acciones proyectadas a la derecha.
 */
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './page-header.component.html'
})
export class PageHeaderComponent {
  /** Título principal de la vista. */
  @Input({ required: true }) title!: string;
  /** Texto descriptivo opcional bajo el título. */
  @Input() subtitle?: string;
  /** Si es falso, oculta la zona de acciones aunque haya contenido proyectado. */
  @Input() hasActions = true;
}
