import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { IconActionComponent } from '../../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../../shared/components/table-actions/table-actions.component';
import { PlantillasService } from './plantillas.service';
import { DependenciasService } from '../dependencias/dependencias.service';
import { Dependencia, Plantilla } from '../../../shared/models/api.models';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

/**
 * Administración de plantillas Excel OSC del OSD.
 * Listado paginado con enlace a gestión de campos y formulario de creación.
 */
@Component({
  selector: 'app-plantillas-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    PageHeaderComponent,
    LoadingStateComponent,
    StatusBadgeComponent,
    IconActionComponent,
    TableActionsComponent,
    TablePaginatorComponent
  ],
  templateUrl: './plantillas-list.component.html',
  styleUrl: './plantillas-list.component.scss'
})
export class PlantillasListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly plantillasService = inject(PlantillasService);
  private readonly dependenciasService = inject(DependenciasService);

  plantillas = signal<Plantilla[]>([]);
  readonly pag = tablePagination(this.plantillas);
  dependencias = signal<Dependencia[]>([]);
  loading = signal(true);
  error = signal('');
  mostrarForm = signal(false);

  form = this.fb.nonNullable.group({
    codigo: ['', Validators.required],
    nombre: ['', Validators.required],
    descripcion: [''],
    dependencia_id: [null as number | null],
    activo: [true]
  });

  ngOnInit(): void {
    this.dependenciasService.listar().subscribe((r) => this.dependencias.set(r.dependencias || []));
    this.cargar();
  }

  /** Recarga el listado paginado de plantillas. */
  cargar(): void {
    this.loading.set(true);
    this.plantillasService.listar().subscribe({
      next: (res) => {
        this.plantillas.set(res.plantillas || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar plantillas.');
        this.loading.set(false);
      }
    });
  }

  /**
   * Crea una plantilla nueva a partir del formulario reactivo.
   * Resetea el formulario y oculta el panel de creación en éxito.
   */
  crear(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.plantillasService
      .crear({
        codigo: v.codigo,
        nombre: v.nombre,
        descripcion: v.descripcion || undefined,
        dependencia_id: v.dependencia_id ?? undefined,
        activo: v.activo
      })
      .subscribe({
        next: () => {
          this.form.reset({ activo: true });
          this.mostrarForm.set(false);
          this.cargar();
        },
        error: (err) => this.error.set(err?.error?.error || 'Error al crear.')
      });
  }
}
