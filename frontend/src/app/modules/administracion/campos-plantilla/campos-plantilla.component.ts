import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PlantillasService } from '../plantillas/plantillas.service';
import { CampoPlantilla } from '../../../shared/models/api.models';

@Component({
  selector: 'app-campos-plantilla',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageHeaderComponent],
  templateUrl: './campos-plantilla.component.html',
  styleUrl: './campos-plantilla.component.scss'
})
export class CamposPlantillaComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly plantillasService = inject(PlantillasService);

  plantillaId = signal(0);
  campos = signal<CampoPlantilla[]>([]);
  loading = signal(true);
  error = signal('');
  mostrarForm = signal(false);

  form = this.fb.nonNullable.group({
    nombre_campo: ['', Validators.required],
    tipo_dato: ['texto', Validators.required],
    obligatorio: [false],
    descripcion: [''],
    longitud: [null as number | null],
    formato: [''],
    valores_permitidos: [''],
    orden: [1, Validators.required]
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.plantillaId.set(id);
    this.cargar();
  }

  cargar(): void {
    this.loading.set(true);
    this.plantillasService.listarCampos(this.plantillaId()).subscribe({
      next: (res) => {
        this.campos.set(res.campos || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar campos.');
        this.loading.set(false);
      }
    });
  }

  crear(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.plantillasService
      .crearCampo(this.plantillaId(), {
        nombre_campo: v.nombre_campo,
        tipo_dato: v.tipo_dato,
        obligatorio: v.obligatorio,
        descripcion: v.descripcion || undefined,
        longitud: v.longitud ?? undefined,
        formato: v.formato || undefined,
        valores_permitidos: v.valores_permitidos || undefined,
        orden: v.orden
      })
      .subscribe({
        next: () => {
          this.form.reset({ tipo_dato: 'texto', obligatorio: false, orden: this.campos().length + 1 });
          this.mostrarForm.set(false);
          this.cargar();
        },
        error: (err) => this.error.set(err?.error?.error || 'Error al crear campo.')
      });
  }

  eliminar(campo: CampoPlantilla): void {
    if (!confirm(`¿Eliminar campo "${campo.nombre_campo}"?`)) return;
    this.plantillasService.eliminarCampo(campo.id).subscribe({
      next: () => this.cargar(),
      error: () => this.error.set('Error al eliminar.')
    });
  }
}
