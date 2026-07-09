import { Component, inject, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Barra superior de la aplicación autenticada.
 * Muestra información de sesión y emite eventos para abrir o cerrar el menú lateral.
 */
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  readonly auth = inject(AuthService);

  /** Emite cuando el usuario solicita alternar la visibilidad del sidebar. */
  readonly menuToggle = output<void>();

  /** Cierra la sesión actual y redirige al flujo de autenticación. */
  logout(): void {
    this.auth.logout();
  }
}
