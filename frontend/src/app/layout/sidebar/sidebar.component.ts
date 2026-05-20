import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

interface MenuItem {
  label: string;
  route: string;
  icon?: string;
  adminOnly?: boolean;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  private readonly auth = inject(AuthService);

  readonly menuPrincipal: MenuItem[] = [
    { label: 'Panel principal', route: '/dashboard', icon: '📊' },
    { label: 'Archivos', route: '/archivos', icon: '📁' },
    { label: 'Validaciones', route: '/validaciones', icon: '✓' },
    { label: 'Proyección población', route: '/poblacion', icon: '👥' }
  ];

  readonly menuAdmin: MenuItem[] = [
    { label: 'Usuarios', route: '/administracion/usuarios', adminOnly: true },
    { label: 'Roles', route: '/administracion/roles', adminOnly: true },
    { label: 'Dependencias', route: '/administracion/dependencias', adminOnly: true },
    { label: 'Plantillas', route: '/administracion/plantillas', adminOnly: true }
  ];

  isAdmin(): boolean {
    return this.auth.isAdminUser();
  }
}
