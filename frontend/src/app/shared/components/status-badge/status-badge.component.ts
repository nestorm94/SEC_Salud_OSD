import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="badge" [class]="cssClass()">{{ estado }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }
    .ok { background: #d1fae5; color: #065f46; }
    .error { background: #fee2e2; color: #991b1b; }
    .pending { background: #fef3c7; color: #92400e; }
    .info { background: #e0f2fe; color: #075985; }
    .neutral { background: #f1f5f9; color: #475569; }
  `
})
export class StatusBadgeComponent {
  @Input({ required: true }) estado!: string;

  cssClass = computed(() => {
    const e = (this.estado || '').toUpperCase();
    if (e.includes('OK') || e === 'APROBADO') return 'ok';
    if (e.includes('ERROR') || e === 'RECHAZADO') return 'error';
    if (e === 'VALIDANDO' || e === 'SUBIDO') return 'pending';
    if (e.includes('VALIDADO')) return 'info';
    return 'neutral';
  });
}
