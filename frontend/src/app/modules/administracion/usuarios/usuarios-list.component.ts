import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { IconActionComponent } from '../../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../../shared/components/table-actions/table-actions.component';
import { UsuariosService } from './usuarios.service';
import { UsuarioAdmin } from '../../../shared/models/api.models';
import { mapHttpErrorMessage } from '../../../core/utils/http-error.util';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

@Component({
  selector: 'app-usuarios-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageHeaderComponent,
    LoadingStateComponent,
    StatusBadgeComponent,
    IconActionComponent,
    TableActionsComponent,
    TablePaginatorComponent
  ],
  templateUrl: './usuarios-list.component.html',
  styleUrl: './usuarios-list.component.scss'
})
export class UsuariosListComponent implements OnInit {
  private readonly usuariosService = inject(UsuariosService);

  usuarios = signal<UsuarioAdmin[]>([]);
  readonly pag = tablePagination(this.usuarios);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.cargar();
  }

  toggleActivo(u: UsuarioAdmin): void {
    this.usuariosService.setActivo(u.id, !u.activo).subscribe({
      next: () => (u.activo = !u.activo),
      error: () => this.error.set('Error al cambiar estado.')
    });
  }

  eliminar(u: UsuarioAdmin): void {
    if (!confirm(`¿Desactivar el usuario "${u.nombre_usuario}"?`)) return;
    this.usuariosService.eliminar(u.id).subscribe({
      next: () => {
        u.activo = false;
        this.cargar();
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'No se pudo desactivar el usuario.');
      }
    });
  }

  private cargar(): void {
    this.loading.set(true);
    this.usuariosService.listar().subscribe({
      next: (res) => {
        this.usuarios.set(res.usuarios || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(
          mapHttpErrorMessage(err, 'No se pudo cargar la lista de usuarios. Verifique que la API esté en ejecución.')
        );
        this.loading.set(false);
      }
    });
  }
}
