import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { UsuariosService } from './usuarios.service';
import { DependenciasService } from '../dependencias/dependencias.service';
import { RolesService } from '../roles/roles.service';
import { Dependencia, Rol } from '../../../shared/models/api.models';

/**
 * Formulario de creación y edición de usuarios del OSD.
 * En edición deshabilita el nombre de usuario y hace opcional la contraseña.
 */
@Component({
  selector: 'app-usuario-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageHeaderComponent],
  templateUrl: './usuario-form.component.html',
  styleUrl: './usuario-form.component.scss'
})
export class UsuarioFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly usuariosService = inject(UsuariosService);
  private readonly dependenciasService = inject(DependenciasService);
  private readonly rolesService = inject(RolesService);

  esEdicion = signal(false);
  usuarioId = signal<number | null>(null);
  dependencias = signal<Dependencia[]>([]);
  rolesDisponibles = signal<Rol[]>([]);
  loading = signal(false);
  error = signal('');
  rolesSeleccionados: string[] = ['Operador'];

  form = this.fb.nonNullable.group({
    nombre_usuario: ['', Validators.required],
    password: [''],
    email: [''],
    dependencia_id: [null as number | null]
  });

  ngOnInit(): void {
    this.dependenciasService.listar().subscribe((r) => this.dependencias.set(r.dependencias || []));
    this.rolesService.listar().subscribe((r) => this.rolesDisponibles.set(r.roles || []));

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'nuevo') {
      const id = Number(idParam);
      this.esEdicion.set(true);
      this.usuarioId.set(id);
      this.form.controls.password.clearValidators();
      this.usuariosService.obtener(id).subscribe({
        next: (u) => {
          this.form.patchValue({
            nombre_usuario: u.nombre_usuario,
            email: u.email || '',
            dependencia_id: u.dependencia_id ?? null
          });
          this.form.controls.nombre_usuario.disable();
          this.rolesSeleccionados = [...(u.roles || [])];
        },
        error: () => this.error.set('Usuario no encontrado.')
      });
    } else {
      this.form.controls.password.setValidators(Validators.required);
    }
  }

  /**
   * Alterna la selección de un rol en el checklist.
   * @param nombre Nombre del rol a marcar o desmarcar.
   */
  toggleRol(nombre: string): void {
    const idx = this.rolesSeleccionados.indexOf(nombre);
    if (idx >= 0) {
      this.rolesSeleccionados = this.rolesSeleccionados.filter((r) => r !== nombre);
    } else {
      this.rolesSeleccionados = [...this.rolesSeleccionados, nombre];
    }
  }

  /**
   * Indica si un rol está seleccionado actualmente.
   * @param nombre Nombre del rol.
   * @returns true si el rol está en la lista seleccionada.
   */
  tieneRol(nombre: string): boolean {
    return this.rolesSeleccionados.includes(nombre);
  }

  /**
   * Persiste el usuario (crear o actualizar) y redirige al listado.
   * Asigna rol Operador por defecto en creación si no se seleccionó ninguno.
   */
  guardar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set('');
    const raw = this.form.getRawValue();

    if (this.esEdicion() && this.usuarioId()) {
      const dto = {
        email: raw.email || undefined,
        dependencia_id: raw.dependencia_id ?? undefined,
        password: raw.password || undefined,
        roles: this.rolesSeleccionados
      };
      this.usuariosService.actualizar(this.usuarioId()!, dto).subscribe({
        next: () => this.router.navigate(['/administracion/usuarios']),
        error: (err) => {
          this.error.set(err?.error?.error || 'Error al actualizar.');
          this.loading.set(false);
        }
      });
    } else {
      this.usuariosService
        .crear({
          nombre_usuario: raw.nombre_usuario,
          password: raw.password,
          email: raw.email || undefined,
          dependencia_id: raw.dependencia_id ?? undefined,
          roles: this.rolesSeleccionados.length ? this.rolesSeleccionados : ['Operador']
        })
        .subscribe({
          next: () => this.router.navigate(['/administracion/usuarios']),
          error: (err) => {
            this.error.set(err?.error?.error || 'Error al crear usuario.');
            this.loading.set(false);
          }
        });
    }
  }
}
