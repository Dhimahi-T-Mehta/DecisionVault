import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Bar {
  label: string;
  value: number;
  color?: string;
}

/** Horizontal bar chart — data labels drawn as SVG text. */
@Component({
  selector: 'dv-bars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 320 ' + height()" role="img" [attr.aria-label]="title()">
      @for (row of rows(); track row.label) {
        <text x="0" [attr.y]="row.y + 11" class="bar-label">{{ row.label }}</text>
        <rect x="110" [attr.y]="row.y" [attr.width]="Math.max(row.w, row.value > 0 ? 3 : 0)" height="14" rx="3"
              [attr.fill]="row.color ?? 'var(--accent, #f0b429)'" opacity="0.9">
          <title>{{ row.label }}: {{ row.value }}</title>
        </rect>
        <text [attr.x]="112 + Math.max(row.w, row.value > 0 ? 3 : 0) + 6" [attr.y]="row.y + 11" class="bar-value">{{ row.value }}</text>
      }
    </svg>
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; }
    .bar-label { font-size: 10px; fill: var(--text-dim, #93a1b8); }
    .bar-value { font-size: 10px; fill: var(--text, #e8ecf4); font-weight: 600; }
  `
})
export class BarChart {
  readonly bars = input.required<Bar[]>();
  readonly title = input('Breakdown');
  readonly rowHeight = input(24);

  readonly height = computed(() => Math.max(this.bars().length, 1) * this.rowHeight() + 4);

  readonly rows = computed(() => {
    const max = Math.max(...this.bars().map(b => b.value), 1);
    const usable = 320 - 110 - 40;
    return this.bars().map((b, i) => ({
      ...b,
      y: i * this.rowHeight() + 2,
      w: (b.value / max) * usable
    }));
  });

  protected readonly Math = Math;
}
