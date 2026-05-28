import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./modules/dashboard/dashboard.component').then((m) => m.DashboardComponent)
      },
      {
        path: 'archivos',
        loadComponent: () =>
          import('./modules/archivos/archivos.component').then((m) => m.ArchivosComponent)
      },
      {
        path: 'validaciones',
        loadComponent: () =>
          import('./modules/validaciones/validaciones.component').then((m) => m.ValidacionesComponent)
      },
      {
        path: 'poblacion',
        loadComponent: () =>
          import('./modules/poblacion/poblacion.component').then((m) => m.PoblacionComponent)
      },
      {
        path: 'administracion',
        canActivate: [adminGuard],
        children: [
          { path: '', redirectTo: 'usuarios', pathMatch: 'full' },
          {
            path: 'usuarios',
            loadComponent: () =>
              import('./modules/administracion/usuarios/usuarios-list.component').then(
                (m) => m.UsuariosListComponent
              )
          },
          {
            path: 'usuarios/nuevo',
            loadComponent: () =>
              import('./modules/administracion/usuarios/usuario-form.component').then(
                (m) => m.UsuarioFormComponent
              )
          },
          {
            path: 'usuarios/:id',
            loadComponent: () =>
              import('./modules/administracion/usuarios/usuario-form.component').then(
                (m) => m.UsuarioFormComponent
              )
          },
          {
            path: 'roles',
            loadComponent: () =>
              import('./modules/administracion/roles/roles-list.component').then((m) => m.RolesListComponent)
          },
          {
            path: 'lineas-tematicas',
            loadComponent: () =>
              import('./modules/administracion/lineas-tematicas/lineas-tematicas-list.component').then(
                (m) => m.LineasTematicasListComponent
              )
          },
          {
            path: 'indicadores',
            loadComponent: () =>
              import('./modules/administracion/indicadores/indicadores-list.component').then(
                (m) => m.IndicadoresListComponent
              )
          },
          {
            path: 'dependencias',
            loadComponent: () =>
              import('./modules/administracion/dependencias/dependencias-list.component').then(
                (m) => m.DependenciasListComponent
              )
          },
          {
            path: 'plantillas',
            loadComponent: () =>
              import('./modules/administracion/plantillas/plantillas-list.component').then(
                (m) => m.PlantillasListComponent
              )
          },
          {
            path: 'plantillas/:id/campos',
            loadComponent: () =>
              import('./modules/administracion/campos-plantilla/campos-plantilla.component').then(
                (m) => m.CamposPlantillaComponent
              )
          }
        ]
      }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
