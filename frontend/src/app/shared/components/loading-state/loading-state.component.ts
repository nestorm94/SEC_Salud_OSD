import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-state',
  standalone: true,
  template: `
    <div class="loading" role="status" aria-live="polite">
      <span class="osd-spinner" aria-hidden="true"></span>
      <span>{{ message }}</span>
    </div>
  `,
  styles: `
    .osd-spinner {
      display: inline-block;
      width: 40px;
      height: 40px;
      border: 3px solid #dde6f0;
      border-top-color: #0b1f3a;
      border-radius: 50%;
      animation: osd-spin 0.75s linear infinite;
    }
    @keyframes osd-spin {
      to {
        transform: rotate(360deg);
      }
    }
  `
})
export class LoadingStateComponent {
  @Input() message = 'Cargando...';
}
