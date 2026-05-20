import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { RolesService } from './roles.service';
import { Rol } from '../../../shared/models/api.models';

@Component({
  selector: 'app-roles-list',
  standalone: true,
  imports: [CommonModule, PageHeaderComponent],
  templateUrl: './roles-list.component.html',
  styleUrl: './roles-list.component.scss'
})
export class RolesListComponent implements OnInit {
  private readonly rolesService = inject(RolesService);

  roles = signal<Rol[]>([]);
  loading = signal(true);
  error = signal('');

  ngOnInit(): void {
    this.rolesService.listar().subscribe({
      next: (res) => {
        this.roles.set(res.roles || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar roles.');
        this.loading.set(false);
      }
    });
  }
}
