import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Componente raíz de la aplicación Angular.
 * Delega la navegación al enrutador mediante un único `router-outlet`.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: []
})
export class AppComponent {}
