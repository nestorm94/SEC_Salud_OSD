import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ArchivosService } from './archivos.service';
import { ArchivoItem } from '../../shared/models/api.models';

@Component({
  selector: 'app-archivos',
  standalone: true,
  imports: [CommonModule, PageHeaderComponent],
  templateUrl: './archivos.component.html',
  styleUrl: './archivos.component.scss'
})
export class ArchivosComponent implements OnInit {
  private readonly archivosService = inject(ArchivosService);

  archivos = signal<ArchivoItem[]>([]);
  loading = signal(true);
  uploading = signal(false);
  mensaje = signal('');
  error = signal('');

  ngOnInit(): void {
    this.cargarLista();
  }

  cargarLista(): void {
    this.loading.set(true);
    this.archivosService.listar().subscribe({
      next: (res) => {
        this.archivos.set(res.archivos || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar archivos.');
        this.loading.set(false);
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      this.error.set('Solo se aceptan archivos .xlsx');
      return;
    }
    this.uploading.set(true);
    this.error.set('');
    this.mensaje.set('');
    this.archivosService.subirExcel(file).subscribe({
      next: () => {
        this.mensaje.set('Archivo cargado y procesado correctamente.');
        this.uploading.set(false);
        input.value = '';
        this.cargarLista();
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'Error al subir el archivo.');
        this.uploading.set(false);
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
      next: () => this.cargarLista(),
      error: () => this.error.set('Error al eliminar.')
    });
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }
}
