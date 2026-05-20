import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CargasService } from './cargas.service';
import { CargaError, CargaItem } from '../../shared/models/api.models';

type FiltroCarga = 'todas' | 'pendientes' | 'errores';

@Component({
  selector: 'app-validaciones',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, StatusBadgeComponent],
  templateUrl: './validaciones.component.html',
  styleUrl: './validaciones.component.scss'
})
export class ValidacionesComponent implements OnInit {
  private readonly cargasService = inject(CargasService);

  cargas = signal<CargaItem[]>([]);
  filtro = signal<FiltroCarga>('todas');
  loading = signal(true);
  error = signal('');
  mensaje = signal('');

  cargaSeleccionada = signal<CargaItem | null>(null);
  errores = signal<CargaError[]>([]);
  cargandoErrores = signal(false);
  observaciones = '';

  cargasFiltradas = computed(() => {
    const list = this.cargas();
    const f = this.filtro();
    if (f === 'pendientes') {
      return list.filter((c) =>
        ['SUBIDO', 'VALIDANDO', 'VALIDADO_OK'].includes(c.estado.toUpperCase())
      );
    }
    if (f === 'errores') {
      return list.filter((c) => c.estado.toUpperCase() === 'VALIDADO_CON_ERRORES');
    }
    return list;
  });

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.loading.set(true);
    this.cargasService.listar().subscribe({
      next: (res) => {
        this.cargas.set(res.cargas || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar validaciones.');
        this.loading.set(false);
      }
    });
  }

  verErrores(carga: CargaItem): void {
    this.cargaSeleccionada.set(carga);
    this.cargandoErrores.set(true);
    this.errores.set([]);
    this.cargasService.getErrores(carga.id).subscribe({
      next: (res) => {
        this.errores.set(res.errores || []);
        this.cargandoErrores.set(false);
      },
      error: () => {
        this.error.set('Error al cargar errores.');
        this.cargandoErrores.set(false);
      }
    });
  }

  cerrarDetalle(): void {
    this.cargaSeleccionada.set(null);
    this.errores.set([]);
    this.observaciones = '';
  }

  aprobar(carga: CargaItem): void {
    this.cargasService.aprobar(carga.id, this.observaciones || undefined).subscribe({
      next: () => {
        this.mensaje.set('Carga aprobada.');
        this.cerrarDetalle();
        this.cargar();
      },
      error: (err) => this.error.set(err?.error?.error || 'No se pudo aprobar.')
    });
  }

  rechazar(carga: CargaItem): void {
    if (!this.observaciones.trim()) {
      this.error.set('Indique observaciones para rechazar.');
      return;
    }
    this.cargasService.rechazar(carga.id, this.observaciones).subscribe({
      next: () => {
        this.mensaje.set('Carga rechazada.');
        this.cerrarDetalle();
        this.cargar();
      },
      error: (err) => this.error.set(err?.error?.error || 'No se pudo rechazar.')
    });
  }

  puedeAprobar(carga: CargaItem): boolean {
    return carga.estado.toUpperCase() === 'VALIDADO_OK';
  }
}
