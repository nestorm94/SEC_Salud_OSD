import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';
import { IconActionComponent } from '../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../shared/components/table-actions/table-actions.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../shared/utils/table-pagination.state';
import { CargasService } from './cargas.service';
import { ArchivosService } from '../archivos/archivos.service';
import { CargaError } from '../../shared/models/api.models';
import {
  ValidacionFila,
  esConErroresValidacion,
  esPendienteValidacion,
  puedeAprobarFila
} from './validaciones.types';

type FiltroCarga = 'todas' | 'pendientes' | 'errores';

@Component({
  selector: 'app-validaciones',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    LoadingStateComponent,
    ModalComponent,
    IconActionComponent,
    TableActionsComponent,
    TablePaginatorComponent
  ],
  templateUrl: './validaciones.component.html',
  styleUrl: './validaciones.component.scss'
})
export class ValidacionesComponent implements OnInit {
  private readonly cargasService = inject(CargasService);
  private readonly archivosService = inject(ArchivosService);

  filas = signal<ValidacionFila[]>([]);
  filtro = signal<FiltroCarga>('todas');
  loading = signal(true);
  error = signal('');
  mensaje = signal('');

  filaSeleccionada = signal<ValidacionFila | null>(null);
  modalDetalleAbierto = signal(false);
  modalRechazoAbierto = signal(false);

  errores = signal<CargaError[]>([]);
  cargandoErrores = signal(false);
  procesando = signal(false);
  observaciones = '';

  filasFiltradas = computed(() => {
    const list = this.filas();
    const f = this.filtro();
    if (f === 'pendientes') {
      return list.filter((x) => esPendienteValidacion(x));
    }
    if (f === 'errores') {
      return list.filter((x) => esConErroresValidacion(x));
    }
    return list;
  });

  readonly pag = tablePagination(this.filasFiltradas);

  onFiltroChange(valor: FiltroCarga): void {
    this.filtro.set(valor);
    this.pag.resetPage();
  }

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      cargas: this.cargasService.listar(),
      archivos: this.archivosService.listar()
    }).subscribe({
      next: ({ cargas, archivos }) => {
        const filas: ValidacionFila[] = [];

        for (const c of cargas.cargas || []) {
          filas.push({
            origen: 'carga',
            id: c.id,
            archivo: c.archivo,
            dependencia: c.dependencia,
            estado: c.estado,
            estadoEtiqueta: c.estado,
            total_errores: c.total_errores ?? 0,
            usuario: c.usuario
          });
        }

        for (const a of archivos.archivos || []) {
          const estado = a.estado || '';
          filas.push({
            origen: 'archivo',
            id: a.id,
            archivo: a.nombre_original,
            dependencia: a.dependencia || a.linea_tematica || '—',
            estado,
            estadoEtiqueta: a.estado_etiqueta || estado,
            total_errores: estado.toLowerCase() === 'rechazado' ? 1 : 0,
            usuario: a.subido_por
          });
        }

        filas.sort((a, b) => a.archivo.localeCompare(b.archivo, 'es'));
        this.filas.set(filas);
        this.pag.resetPage();
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar validaciones.');
        this.loading.set(false);
      }
    });
  }

  abrirDetalle(fila: ValidacionFila): void {
    this.error.set('');
    this.filaSeleccionada.set(fila);
    this.modalAbiertoDetalle();
    this.cargandoErrores.set(true);
    this.errores.set([]);

    if (fila.origen === 'carga') {
      this.cargasService.getErrores(fila.id).subscribe({
        next: (res) => {
          this.errores.set(res.errores || []);
          this.cargandoErrores.set(false);
        },
        error: () => {
          this.error.set('Error al cargar errores del cargue.');
          this.cargandoErrores.set(false);
        }
      });
      return;
    }

    this.archivosService.obtenerDetalle(fila.id).subscribe({
      next: (det) => {
        const lista = det.errores_validacion || [];
        this.errores.set(
          lista.map((msg, i) => ({
            id: i + 1,
            fila: 0,
            columna: '—',
            mensaje: msg,
            tipo: 'VALIDACION'
          }))
        );
        this.cargandoErrores.set(false);
      },
      error: () => {
        this.error.set('Error al cargar errores del archivo.');
        this.cargandoErrores.set(false);
      }
    });
  }

  abrirRechazo(fila: ValidacionFila): void {
    if (!puedeAprobarFila(fila)) return;
    this.error.set('');
    this.filaSeleccionada.set(fila);
    this.observaciones = '';
    this.modalRechazoAbierto.set(true);
    this.modalDetalleAbierto.set(false);
  }

  cerrarModales(): void {
    this.modalDetalleAbierto.set(false);
    this.modalRechazoAbierto.set(false);
    this.filaSeleccionada.set(null);
    this.errores.set([]);
    this.observaciones = '';
  }

  aprobar(fila: ValidacionFila): void {
    if (!puedeAprobarFila(fila) || this.procesando()) return;
    this.procesando.set(true);
    this.error.set('');

    if (fila.origen === 'archivo') {
      this.archivosService.enviarYAprobar(fila.id).subscribe({
        next: (res) => {
          this.procesando.set(false);
          this.cerrarModales();
          if (res.aprobado) {
            this.mensaje.set(res.mensaje || `Archivo #${fila.id} enviado y aprobado.`);
          } else {
            this.error.set(
              res.mensaje ||
                `El archivo tiene errores (${res.total_errores ?? 0}). Revise el detalle en Carga de archivos.`
            );
          }
          this.cargar();
        },
        error: (err) => {
          this.error.set(err?.error?.error || 'No se pudo enviar y aprobar el archivo.');
          this.procesando.set(false);
        }
      });
      return;
    }

    this.cargasService.aprobar(fila.id).subscribe({
      next: () => {
        this.mensaje.set(`Cargue #${fila.id} aprobado.`);
        this.procesando.set(false);
        this.cerrarModales();
        this.cargar();
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'No se pudo aprobar el cargue.');
        this.procesando.set(false);
      }
    });
  }

  confirmarRechazo(): void {
    const fila = this.filaSeleccionada();
    if (!fila || this.procesando()) return;
    if (!this.observaciones.trim()) {
      this.error.set('Indique observaciones para rechazar.');
      return;
    }

    this.procesando.set(true);
    this.error.set('');

    if (fila.origen === 'archivo') {
      this.archivosService.rechazarValidacion(fila.id, this.observaciones.trim()).subscribe({
        next: () => {
          this.mensaje.set(`Archivo #${fila.id} rechazado.`);
          this.procesando.set(false);
          this.cerrarModales();
          this.cargar();
        },
        error: (err) => {
          this.error.set(err?.error?.error || 'No se pudo rechazar el archivo.');
          this.procesando.set(false);
        }
      });
      return;
    }

    this.cargasService.rechazar(fila.id, this.observaciones.trim()).subscribe({
      next: () => {
        this.mensaje.set(`Cargue #${fila.id} rechazado.`);
        this.procesando.set(false);
        this.cerrarModales();
        this.cargar();
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'No se pudo rechazar el cargue.');
        this.procesando.set(false);
      }
    });
  }

  puedeAprobar(fila: ValidacionFila): boolean {
    return puedeAprobarFila(fila);
  }

  puedeRechazar(fila: ValidacionFila): boolean {
    return puedeAprobarFila(fila);
  }

  etiquetaOrigen(fila: ValidacionFila): string {
    return fila.origen === 'carga' ? 'Cargue' : 'Prevalidación';
  }

  tituloModalDetalle(): string {
    const f = this.filaSeleccionada();
    if (!f) return 'Detalle';
    return f.origen === 'carga' ? `Cargue #${f.id}` : `Archivo #${f.id}`;
  }

  tituloModalRechazo(): string {
    const f = this.filaSeleccionada();
    if (!f) return 'Rechazar';
    return f.origen === 'carga' ? `Rechazar cargue #${f.id}` : `Rechazar archivo #${f.id}`;
  }

  private modalAbiertoDetalle(): void {
    this.modalDetalleAbierto.set(true);
    this.modalRechazoAbierto.set(false);
  }
}
