import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="osd-badge" [class]="badgeClass()">{{ estado || '—' }}</span>`,
  styles: `
    .osd-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.2rem 0.65rem;
      border-radius: 999px;
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      white-space: nowrap;
      background: #f1f5f9;
      color: #64748b;
    }
    .osd-badge--ok {
      background: #e8f5ec;
      color: #155724;
    }
    .osd-badge--warn {
      background: #fff7ed;
      color: #c2410c;
    }
    .osd-badge--err {
      background: #fef2f2;
      color: #b91c1c;
    }
    .osd-badge--info {
      background: #e8eef5;
      color: #0b1f3a;
    }
  `
})
export class StatusBadgeComponent {
  @Input() estado = '';

  readonly badgeClass = computed(() => {
    const e = (this.estado || '').toUpperCase();
    if (e.includes('APROB') || e.includes('EXITOSO') || e.includes('VALIDADO_OK')) return 'osd-badge--ok';
    if (e.includes('ERROR') || e.includes('RECHAZ')) return 'osd-badge--err';
    if (e.includes('PEND') || e.includes('VALID') || e.includes('RECIB')) return 'osd-badge--warn';
    return 'osd-badge--info';
  });
}
