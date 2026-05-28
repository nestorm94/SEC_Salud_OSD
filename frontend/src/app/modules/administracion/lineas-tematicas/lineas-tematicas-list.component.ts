import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { IconActionComponent } from '../../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../../shared/components/table-actions/table-actions.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { LineasTematicasService } from './lineas-tematicas.service';
import { LineaTematicaItem } from '../../../shared/models/api.models';
import { mapHttpErrorMessage } from '../../../core/utils/http-error.util';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

@Component({
  selector: 'app-lineas-tematicas-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageHeaderComponent,
    LoadingStateComponent,
    ModalComponent,
    IconActionComponent,
    TableActionsComponent,
    StatusBadgeComponent,
    TablePaginatorComponent
  ],
  templateUrl: './lineas-tematicas-list.component.html'
})
export class LineasTematicasListComponent implements OnInit {
  private readonly service = inject(LineasTematicasService);

  lineas = signal<LineaTematicaItem[]>([]);
  readonly pag = tablePagination(this.lineas);
  loading = signal(true);
  error = signal('');
  mensaje = signal('');
  modalAbierto = signal(false);
  editando = signal<LineaTematicaItem | null>(null);
  guardando = signal(false);

  codigo = '';
  nombre = '';
  descripcion = '';
  activo = true;

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.loading.set(true);
    this.service.listar().subscribe({
      next: (res) => {
        this.lineas.set(res.lineas_tematicas || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'Error al cargar líneas temáticas.'));
        this.loading.set(false);
      }
    });
  }

  abrirCrear(): void {
    this.editando.set(null);
    this.codigo = '';
    this.nombre = '';
    this.descripcion = '';
    this.activo = true;
    this.error.set('');
    this.modalAbierto.set(true);
  }

  abrirEditar(linea: LineaTematicaItem): void {
    this.editando.set(linea);
    this.codigo = linea.codigo;
    this.nombre = linea.nombre;
    this.descripcion = linea.descripcion || '';
    this.activo = linea.activo ?? true;
    this.error.set('');
    this.modalAbierto.set(true);
  }

  cerrarModal(): void {
    this.modalAbierto.set(false);
    this.editando.set(null);
  }

  guardar(): void {
    if (!this.codigo.trim() || !this.nombre.trim()) {
      this.error.set('Código y nombre son obligatorios.');
      return;
    }
    const dto = {
      codigo: this.codigo.trim(),
      nombre: this.nombre.trim(),
      descripcion: this.descripcion.trim() || null,
      activo: this.activo
    };
    this.guardando.set(true);
    this.error.set('');

    const edit = this.editando();
    const onOk = () => {
      this.mensaje.set(edit ? 'Línea temática actualizada.' : 'Línea temática creada.');
      this.guardando.set(false);
      this.cerrarModal();
      this.cargar();
    };
    const onErr = (err: unknown) => {
      this.error.set(mapHttpErrorMessage(err, 'No se pudo guardar la línea temática.'));
      this.guardando.set(false);
    };

    if (edit) {
      this.service.actualizar(edit.id, dto).subscribe({ next: onOk, error: onErr });
    } else {
      this.service.crear(dto).subscribe({ next: onOk, error: onErr });
    }
  }

  tituloModal(): string {
    return this.editando() ? `Editar línea #${this.editando()!.id}` : 'Nueva línea temática';
  }

  codigoBloqueado(): boolean {
    return !!this.editando();
  }
}
