import { Component, inject, OnInit, signal } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { IconActionComponent } from '../../shared/components/icon-action/icon-action.component';
import { TableActionsComponent } from '../../shared/components/table-actions/table-actions.component';
import { TablePaginatorComponent } from '../../shared/components/table-paginator/table-paginator.component';
import { tablePagination } from '../../shared/utils/table-pagination.state';

import { ArchivosService } from './archivos.service';

import { AuthService } from '../../core/auth/auth.service';

import {

  ArchivoItem,

  IndicadorItem,

  LineaTematicaItem,

  ValidacionArchivoResponse

} from '../../shared/models/api.models';

import { mapHttpErrorMessage } from '../../core/utils/http-error.util';



@Component({

  selector: 'app-archivos',

  standalone: true,

  imports: [

    CommonModule,

    FormsModule,

    PageHeaderComponent,

    StatusBadgeComponent,

    LoadingStateComponent,
    IconActionComponent,
    TableActionsComponent,
    TablePaginatorComponent

  ],

  templateUrl: './archivos.component.html',

  styleUrl: './archivos.component.scss'

})

export class ArchivosComponent implements OnInit {

  private readonly archivosService = inject(ArchivosService);

  private readonly auth = inject(AuthService);



  archivos = signal<ArchivoItem[]>([]);
  readonly pagArchivos = tablePagination(this.archivos);

  lineas = signal<LineaTematicaItem[]>([]);

  indicadores = signal<IndicadorItem[]>([]);



  loading = signal(true);

  loadingCatalogos = signal(true);

  validando = signal(false);

  enviando = signal(false);



  mensaje = signal('');

  error = signal('');

  catalogoError = signal('');



  lineaSeleccionada = signal<number | null>(null);

  indicadorSeleccionado = signal<number | null>(null);

  observaciones = '';

  archivoSeleccionado: File | null = null;



  archivoIdValidado = signal<number | null>(null);

  resultadoValidacion = signal<ValidacionArchivoResponse | null>(null);



  ngOnInit(): void {

    this.cargarCatalogos();

    this.cargarLista();

  }



  cargarCatalogos(): void {

    this.loadingCatalogos.set(true);

    this.catalogoError.set('');

    this.archivosService.listarLineasTematicas().subscribe({

      next: (res) => {

        const lineas = res.lineas_tematicas ?? [];

        this.lineas.set(lineas);

        this.loadingCatalogos.set(false);



        const usuario = this.auth.getUsuario();

        const lineaUsuario = usuario?.linea_tematica_id;

        const esAdmin = this.auth.isAdminUser();



        if (lineas.length === 1) {

          this.seleccionarLinea(lineas[0].id);

        } else if (!esAdmin && lineaUsuario) {

          const existe = lineas.some((l) => l.id === lineaUsuario);

          if (existe) this.seleccionarLinea(lineaUsuario);

        }

      },

      error: (err) => {

        this.catalogoError.set(

          mapHttpErrorMessage(err, 'No se pudo cargar el catálogo de líneas temáticas.')

        );

        this.loadingCatalogos.set(false);

      }

    });

  }



  onLineaChange(lineaId: number | string | null): void {

    const id = lineaId === '' || lineaId == null ? null : Number(lineaId);

    this.seleccionarLinea(id);

  }



  seleccionarLinea(lineaId: number | null): void {

    this.lineaSeleccionada.set(lineaId);

    this.indicadorSeleccionado.set(null);

    this.indicadores.set([]);

    this.resetValidacion();



    if (!lineaId) return;



    this.archivosService.listarIndicadores(lineaId).subscribe({

      next: (res) => this.indicadores.set(res.indicadores ?? []),

      error: (err) =>

        this.catalogoError.set(mapHttpErrorMessage(err, 'No se pudieron cargar los indicadores.'))

    });

  }



  onIndicadorChange(indicadorId: number | string | null): void {

    this.indicadorSeleccionado.set(

      indicadorId === '' || indicadorId == null ? null : Number(indicadorId)

    );

    this.resetValidacion();

  }



  onArchivoChange(event: Event): void {

    const input = event.target as HTMLInputElement;

    this.archivoSeleccionado = input.files?.[0] ?? null;

    this.resetValidacion();

  }



  validar(): void {

    const lineaId = this.lineaSeleccionada();

    const indicadorId = this.indicadorSeleccionado();



    if (!lineaId || !indicadorId) {

      this.error.set('Seleccione línea temática e indicador.');

      return;

    }

    if (!this.archivoSeleccionado) {

      this.error.set('Seleccione un archivo Excel (.xlsx) con plantilla OSC.');

      return;

    }

    if (!this.archivoSeleccionado.name.toLowerCase().endsWith('.xlsx')) {

      this.error.set('Solo se aceptan archivos .xlsx');

      return;

    }



    const form = new FormData();

    form.append('archivo', this.archivoSeleccionado);

    form.append('linea_tematica_id', String(lineaId));

    form.append('indicador_id', String(indicadorId));

    if (this.observaciones.trim()) form.append('observaciones', this.observaciones.trim());



    this.validando.set(true);

    this.error.set('');

    this.mensaje.set('Validando archivo, por favor espere…');

    this.resetValidacion(false);



    this.archivosService.validar(form).subscribe({

      next: (res) => {

        this.resultadoValidacion.set(res);

        this.validando.set(false);

        if (res.valido) {

          this.archivoIdValidado.set(res.archivo_id);

          this.mensaje.set(res.mensaje ?? 'Archivo validado. Puede enviarlo.');

        } else {

          this.archivoIdValidado.set(null);

          this.mensaje.set(

            res.mensaje ?? 'El archivo tiene errores. Corrija y vuelva a validar.'

          );

        }

        this.cargarLista();

      },

      error: (err) => {

        this.validando.set(false);

        this.mensaje.set('');

        this.error.set(mapHttpErrorMessage(err, 'Error al validar el archivo.'));

      }

    });

  }



  enviar(): void {

    const archivoId = this.archivoIdValidado();

    if (!archivoId) {

      this.error.set('Debe validar el archivo antes de enviarlo.');

      return;

    }



    this.enviando.set(true);

    this.error.set('');

    this.mensaje.set('Enviando archivo…');



    this.archivosService.enviar(archivoId).subscribe({

      next: (res) => {

        this.enviando.set(false);

        this.mensaje.set(res.mensaje ?? 'Archivo enviado correctamente.');

        this.resetValidacion();

        this.archivoSeleccionado = null;

        this.cargarLista();

      },

      error: (err) => {

        this.enviando.set(false);

        this.error.set(mapHttpErrorMessage(err, 'Error al enviar el archivo.'));

      }

    });

  }



  resetValidacion(limpiarMensaje = true): void {

    this.archivoIdValidado.set(null);

    this.resultadoValidacion.set(null);

    if (limpiarMensaje) this.mensaje.set('');

  }



  cargarLista(): void {

    this.loading.set(true);

    this.archivosService.listar().subscribe({

      next: (res) => {

        this.archivos.set(res.archivos || []);
        this.pagArchivos.resetPage();

        this.loading.set(false);

      },

      error: (err) => {

        this.error.set(mapHttpErrorMessage(err, 'Error al cargar archivos.'));

        this.loading.set(false);

      }

    });

  }



  descargar(item: ArchivoItem): void {

    this.archivosService.descargar(item.id).subscribe({

      next: (res) => {

        const blob = res.body!;

        const url = URL.createObjectURL(blob);

        const a = document.createElement('a');

        a.href = url;

        a.download = item.nombre_original;

        a.click();

        URL.revokeObjectURL(url);

      },

      error: () => this.error.set('Error al descargar.')

    });

  }



  eliminar(item: ArchivoItem): void {

    if (!confirm(`¿Eliminar "${item.nombre_original}"?`)) return;

    this.archivosService.eliminar(item.id).subscribe({

      next: () => {

        if (this.archivoIdValidado() === item.id) this.resetValidacion();

        this.cargarLista();

      },

      error: () => this.error.set('Error al eliminar.')

    });

  }



  formatBytes(bytes: number): string {

    if (bytes < 1024) return `${bytes} B`;

    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;

    return `${(bytes / 1048576).toFixed(1)} MB`;

  }



  lineaBloqueada(): boolean {

    const u = this.auth.getUsuario();

    return !this.auth.isAdminUser() && !!u?.linea_tematica_id && this.lineas().length <= 1;

  }

}

