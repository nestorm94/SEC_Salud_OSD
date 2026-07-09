import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DependenciasService } from './dependencias.service';
import { Dependencia } from '../../../shared/models/api.models';
import { TablePaginatorComponent } from '../../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../../shared/utils/table-pagination.state';

/**
 * Administración de dependencias organizacionales del OSD.
 * Listado paginado y formulario inline para registrar nuevas dependencias.
 */
@Component({
  selector: 'app-dependencias-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageHeaderComponent,
    LoadingStateComponent,
    StatusBadgeComponent,
    TablePaginatorComponent
  ],
  templateUrl: './dependencias-list.component.html',
  styleUrl: './dependencias-list.component.scss'
})
export class DependenciasListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dependenciasService = inject(DependenciasService);

  dependencias = signal<Dependencia[]>([]);
  readonly pag = tablePagination(this.dependencias);
  loading = signal(true);
  error = signal('');
  mensaje = signal('');
  mostrarForm = signal(false);

  form = this.fb.nonNullable.group({
    codigo: ['', Validators.required],
    nombre: ['', Validators.required]
  });

  ngOnInit(): void {
    this.cargar();
  }

  /** Recarga el listado paginado de dependencias. */
  cargar(): void {
    this.loading.set(true);
    this.dependenciasService.listar().subscribe({
      next: (res) => {
        this.dependencias.set(res.dependencias || []);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar dependencias.');
        this.loading.set(false);
      }
    });
  }

  /**
   * Valida y registra una nueva dependencia.
   * Oculta el formulario y refresca la tabla en éxito.
   */
  crear(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { codigo, nombre } = this.form.getRawValue();
    this.dependenciasService.crear(codigo, nombre).subscribe({
      next: () => {
        this.mensaje.set('Dependencia creada.');
        this.form.reset();
        this.mostrarForm.set(false);
        this.cargar();
      },
      error: (err) => this.error.set(err?.error?.error || 'Error al crear.')
    });
  }
}
