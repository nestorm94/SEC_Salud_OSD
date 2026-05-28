import { Component, inject, input, signal, OnInit, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../core/auth/auth.service';
import { BRANDING } from '../../shared/branding';

interface MenuItem {
  label: string;
  route: string;
  icon: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);

  /** Controlado por el layout en pantallas pequeñas */
  readonly menuOpen = input(false);

  readonly branding = BRANDING;
  readonly adminExpanded = signal(false);

  readonly menuPrincipal: MenuItem[] = [
    { label: 'Panel principal', route: '/dashboard', icon: 'dashboard' },
    { label: 'Archivos', route: '/archivos', icon: 'folder_open' },
    { label: 'Validaciones', route: '/validaciones', icon: 'fact_check' },
    { label: 'Proyección población', route: '/poblacion', icon: 'groups' }
  ];

  readonly menuAdmin: MenuItem[] = [
    { label: 'Usuarios', route: '/administracion/usuarios', icon: 'people' },
    { label: 'Roles', route: '/administracion/roles', icon: 'admin_panel_settings' },
    { label: 'Líneas temáticas', route: '/administracion/lineas-tematicas', icon: 'category' },
    { label: 'Indicadores', route: '/administracion/indicadores', icon: 'analytics' },
    { label: 'Dependencias', route: '/administracion/dependencias', icon: 'business' },
    { label: 'Plantillas', route: '/administracion/plantillas', icon: 'description' }
  ];

  ngOnInit(): void {
    this.syncAdminExpanded(this.router.url);
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((e) => this.syncAdminExpanded(e.urlAfterRedirects));
  }

  toggleAdmin(): void {
    this.adminExpanded.update((v) => !v);
  }

  private syncAdminExpanded(url: string): void {
    if (url.includes('/administracion')) {
      this.adminExpanded.set(true);
    }
  }
}
