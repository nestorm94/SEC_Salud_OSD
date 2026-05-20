import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { UsuariosService } from './usuarios.service';
import { UsuarioAdmin } from '../../../shared/models/api.models';

@Component({
  selector: 'app-usuarios-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageHeaderComponent],
  templateUrl: './usuarios-list.component.html',
  styleUrl: './usuarios-list.component.scss'
})
export class UsuariosListComponent implements OnInit {
  private readonly usuariosService = inject(UsuariosService);

  usuarios = signal<UsuarioAdmin[]>([]);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.usuariosService.listar().subscribe({
      next: (res) => {
        this.usuarios.set(res.usuarios || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar usuarios.');
        this.loading.set(false);
      }
    });
  }

  toggleActivo(u: UsuarioAdmin): void {
    this.usuariosService.setActivo(u.id, !u.activo).subscribe({
      next: () => (u.activo = !u.activo),
      error: () => this.error.set('Error al cambiar estado.')
    });
  }
}
