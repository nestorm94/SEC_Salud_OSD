import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';
import { BreadcrumbsComponent } from '../../shared/components/breadcrumbs/breadcrumbs.component';
import { AuthService } from '../../core/auth/auth.service';
import { SessionKeepAliveService } from '../../core/auth/session-keepalive.service';

/**
 * Contenedor raíz de las vistas autenticadas.
 * Orquesta sidebar, cabecera, migas de pan y el enrutador principal.
 */
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, HeaderComponent, BreadcrumbsComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly sessionKeepAlive = inject(SessionKeepAliveService);

  /** Indica si el menú lateral está visible (útil en pantallas pequeñas). */
  readonly sidebarOpen = signal(false);

  /** Inicia el keep-alive de sesión y refresca el token al cargar el layout. */
  ngOnInit(): void {
    this.sessionKeepAlive.start();
    this.auth.refrescarSesion()?.subscribe({ error: () => {} });
  }

  /** Detiene el keep-alive al destruir el layout. */
  ngOnDestroy(): void {
    this.sessionKeepAlive.stop();
  }

  /** Alterna la visibilidad del menú lateral. */
  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  /** Cierra el menú lateral sin alternar su estado previo. */
  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }
}
