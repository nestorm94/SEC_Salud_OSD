import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PoblacionService, VistaPoblacion } from './poblacion.service';
import { ProyeccionResponse } from '../../shared/models/api.models';

@Component({
  selector: 'app-poblacion',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  templateUrl: './poblacion.component.html',
  styleUrl: './poblacion.component.scss'
})
export class PoblacionComponent implements OnInit {
  private readonly poblacionService = inject(PoblacionService);

  tabActiva = signal<VistaPoblacion>('nacional-casanare');
  datos = signal<ProyeccionResponse | null>(null);
  loading = signal(false);
  error = signal('');

  pagina = 1;
  tamanoPagina = 20;

  readonly tabs: { clave: VistaPoblacion; label: string }[] = [
    { clave: 'nacional-casanare', label: 'Nacional / Casanare' },
    { clave: 'curso-vida', label: 'Curso de vida' },
    { clave: 'quinquenios', label: 'Quinquenios' }
  ];

  cambiarTab(clave: VistaPoblacion): void {
    this.tabActiva.set(clave);
    this.pagina = 1;
    this.consultar();
  }

  consultar(): void {
    this.loading.set(true);
    this.error.set('');
    this.poblacionService
      .consultar(this.tabActiva(), { pagina: this.pagina, tamanoPagina: this.tamanoPagina })
      .subscribe({
        next: (r) => {
          this.datos.set(r);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.error || 'Error al consultar proyección.');
          this.loading.set(false);
        }
      });
  }

  paginaAnterior(): void {
    if (this.pagina > 1) {
      this.pagina--;
      this.consultar();
    }
  }

  paginaSiguiente(): void {
    const d = this.datos();
    if (d && this.pagina < d.totalPaginas) {
      this.pagina++;
      this.consultar();
    }
  }

  ngOnInit(): void {
    this.consultar();
  }

  cellValue(fila: Record<string, unknown>, col: string): string {
    const v = fila[col] ?? fila[col.toLowerCase()];
    return v != null ? String(v) : '';
  }
}
