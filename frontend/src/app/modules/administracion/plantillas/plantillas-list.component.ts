import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PlantillasService } from './plantillas.service';
import { DependenciasService } from '../dependencias/dependencias.service';
import { Dependencia, Plantilla } from '../../../shared/models/api.models';

@Component({
  selector: 'app-plantillas-list',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageHeaderComponent],
  templateUrl: './plantillas-list.component.html',
  styleUrl: './plantillas-list.component.scss'
})
export class PlantillasListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly plantillasService = inject(PlantillasService);
  private readonly dependenciasService = inject(DependenciasService);

  plantillas = signal<Plantilla[]>([]);
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

  cargar(): void {
    this.loading.set(true);
    this.plantillasService.listar().subscribe({
      next: (res) => {
        this.plantillas.set(res.plantillas || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar plantillas.');
        this.loading.set(false);
      }
    });
  }

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
