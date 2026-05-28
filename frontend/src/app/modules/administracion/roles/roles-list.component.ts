import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { IconActionComponent } from '../../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../../shared/components/table-actions/table-actions.component';
import { RolesService } from './roles.service';
import { Rol } from '../../../shared/models/api.models';
import { mapHttpErrorMessage } from '../../../core/utils/http-error.util';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

@Component({
  selector: 'app-roles-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageHeaderComponent,
    LoadingStateComponent,
    ModalComponent,
    IconActionComponent,
    TableActionsComponent,
    TablePaginatorComponent
  ],
  templateUrl: './roles-list.component.html',
  styleUrl: './roles-list.component.scss'
})
export class RolesListComponent implements OnInit {
  private readonly rolesService = inject(RolesService);

  roles = signal<Rol[]>([]);
  readonly pag = tablePagination(this.roles);
  loading = signal(true);
  error = signal('');
  mensaje = signal('');

  modalAbierto = signal(false);
  rolEditando = signal<Rol | null>(null);
  guardando = signal(false);

  nombre = '';
  descripcion = '';

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.loading.set(true);
    this.rolesService.listar().subscribe({
      next: (res) => {
        this.roles.set(res.roles || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'Error al cargar roles.'));
        this.loading.set(false);
      }
    });
  }

  abrirCrear(): void {
    this.rolEditando.set(null);
    this.nombre = '';
    this.descripcion = '';
    this.error.set('');
    this.modalAbierto.set(true);
  }

  abrirEditar(rol: Rol): void {
    this.rolEditando.set(rol);
    this.nombre = rol.nombre;
    this.descripcion = rol.descripcion || '';
    this.error.set('');
    this.modalAbierto.set(true);
  }

  cerrarModal(): void {
    this.modalAbierto.set(false);
    this.rolEditando.set(null);
  }

  guardar(): void {
    if (!this.nombre.trim()) {
      this.error.set('El nombre del rol es obligatorio.');
      return;
    }
    const dto = { nombre: this.nombre.trim(), descripcion: this.descripcion.trim() || undefined };
    this.guardando.set(true);
    this.error.set('');

    const edit = this.rolEditando();
    const onOk = () => {
      this.mensaje.set(edit ? 'Rol actualizado.' : 'Rol creado.');
      this.guardando.set(false);
      this.cerrarModal();
      this.cargar();
    };
    const onErr = (err: unknown) => {
      this.error.set(mapHttpErrorMessage(err, 'No se pudo guardar el rol.'));
      this.guardando.set(false);
    };

    if (edit) {
      this.rolesService.actualizar(edit.id, dto).subscribe({ next: onOk, error: onErr });
    } else {
      this.rolesService.crear(dto).subscribe({ next: onOk, error: onErr });
    }
  }

  eliminar(rol: Rol): void {
    if (!confirm(`¿Eliminar el rol "${rol.nombre}"?`)) return;
    this.error.set('');
    this.rolesService.eliminar(rol.id).subscribe({
      next: () => {
        this.mensaje.set('Rol eliminado.');
        this.cargar();
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'No se pudo eliminar el rol.'));
      }
    });
  }

  tituloModal(): string {
    return this.rolEditando() ? `Editar rol #${this.rolEditando()!.id}` : 'Nuevo rol';
  }
}
