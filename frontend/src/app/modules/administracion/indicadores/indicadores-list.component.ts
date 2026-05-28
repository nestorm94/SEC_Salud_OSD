import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { IconActionComponent } from '../../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../../shared/components/table-actions/table-actions.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { IndicadoresAdminService } from './indicadores-admin.service';
import { LineasTematicasService } from '../lineas-tematicas/lineas-tematicas.service';
import { IndicadorItem, LineaTematicaItem } from '../../../shared/models/api.models';
import { mapHttpErrorMessage } from '../../../core/utils/http-error.util';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

@Component({
  selector: 'app-indicadores-list',
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
  templateUrl: './indicadores-list.component.html'
})
export class IndicadoresListComponent implements OnInit {
  private readonly indicadoresService = inject(IndicadoresAdminService);
  private readonly lineasService = inject(LineasTematicasService);

  lineas = signal<LineaTematicaItem[]>([]);
  indicadores = signal<IndicadorItem[]>([]);
  readonly pag = tablePagination(this.indicadores);
  loading = signal(true);
  error = signal('');
  mensaje = signal('');
  modalAbierto = signal(false);
  editando = signal<IndicadorItem | null>(null);
  guardando = signal(false);

  filtroLineaId: number | null = null;
  lineaTematicaId = 0;
  codigo = '';
  nombre = '';
  descripcion = '';
  activo = true;

  ngOnInit(): void {
    this.lineasService.listar().subscribe({
      next: (res) => {
        this.lineas.set((res.lineas_tematicas || []).filter((l) => l.activo !== false));
        this.cargarIndicadores();
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'Error al cargar líneas temáticas.'));
        this.loading.set(false);
      }
    });
  }

  cargarIndicadores(): void {
    this.loading.set(true);
    this.indicadoresService.listar(this.filtroLineaId).subscribe({
      next: (res) => {
        this.indicadores.set(res.indicadores || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapHttpErrorMessage(err, 'Error al cargar indicadores.'));
        this.loading.set(false);
      }
    });
  }

  onFiltroChange(): void {
    this.cargarIndicadores();
  }

  abrirCrear(): void {
    this.editando.set(null);
    this.codigo = '';
    this.nombre = '';
    this.descripcion = '';
    this.activo = true;
    this.lineaTematicaId = this.filtroLineaId ?? this.lineas()[0]?.id ?? 0;
    this.error.set('');
    this.modalAbierto.set(true);
  }

  abrirEditar(ind: IndicadorItem): void {
    this.editando.set(ind);
    this.lineaTematicaId = ind.linea_tematica_id;
    this.codigo = ind.codigo;
    this.nombre = ind.nombre;
    this.descripcion = ind.descripcion || '';
    this.activo = ind.activo ?? true;
    this.error.set('');
    this.modalAbierto.set(true);
  }

  cerrarModal(): void {
    this.modalAbierto.set(false);
    this.editando.set(null);
  }

  guardar(): void {
    if (!this.lineaTematicaId || !this.codigo.trim() || !this.nombre.trim()) {
      this.error.set('Línea temática, código y nombre son obligatorios.');
      return;
    }
    const dto = {
      linea_tematica_id: this.lineaTematicaId,
      codigo: this.codigo.trim(),
      nombre: this.nombre.trim(),
      descripcion: this.descripcion.trim() || null,
      activo: this.activo
    };
    this.guardando.set(true);
    this.error.set('');

    const edit = this.editando();
    const onOk = () => {
      this.mensaje.set(edit ? 'Indicador actualizado.' : 'Indicador creado.');
      this.guardando.set(false);
      this.cerrarModal();
      this.cargarIndicadores();
    };
    const onErr = (err: unknown) => {
      this.error.set(mapHttpErrorMessage(err, 'No se pudo guardar el indicador.'));
      this.guardando.set(false);
    };

    if (edit) {
      this.indicadoresService.actualizar(edit.id, dto).subscribe({ next: onOk, error: onErr });
    } else {
      this.indicadoresService.crear(dto).subscribe({ next: onOk, error: onErr });
    }
  }

  tituloModal(): string {
    return this.editando() ? `Editar indicador #${this.editando()!.id}` : 'Nuevo indicador';
  }

  codigoBloqueado(): boolean {
    return !!this.editando();
  }
}
