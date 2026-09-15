import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Pair {
  label: string;
  expected: number;
  actual: number;
}

/** Grouped vertical bars: expected vs actual per item (0-100 domain). */
@Component({
  selector: 'dv-compare',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 320 ' + height()" role="img" [attr.aria-label]="title()">
      @for (g of groups(); track g.label) {
        <rect [attr.x]="g.x" [attr.y]="g.ye" width="14" [attr.height]="g.he" rx="3" fill="#5b8def" opacity="0.85">
          <title>{{ g.label }} — expected: {{ g.expected }}</title>
        </rect>
        <rect [attr.x]="g.x + 17" [attr.y]="g.ya" width="14" [attr.height]="g.ha" rx="3" fill="#f0b429" opacity="0.85">
          <title>{{ g.label }} — actual: {{ g.actual }}</title>
        </rect>
        <text [attr.x]="g.x + 16" [attr.y]="height() - 6" text-anchor="middle" class="g-label">{{ short(g.label) }}</text>
      }
    </svg>
    <div class="legend">
      <span><i class="sw expected"></i>Expected</span>
      <span><i class="sw amber"></i>Actual</span>
    </div>
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; }
    .g-label { font-size: 8.5px; fill: var(--text-dim, #93a1b8); }
    .legend { display: flex; gap: 16px; font-size: 11px; color: var(--text-dim, #93a1b8); margin-top: 6px; }
    .sw { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 5px; background: #5b8def; }
    .sw.amber { background: #f0b429; }
  `
})
export class CompareChart {
  readonly pairs = input.required<Pair[]>();
  readonly title = input('Expected vs Actual');
  readonly height = input(190);
  readonly pad = 26;

  readonly groups = computed(() => {
    const plotHeight = this.height() - this.pad - 14;
    return this.pairs().map((p, i) => {
      const x = this.pad + i * 58;
      return {
        ...p,
        x,
        ye: this.pad + plotHeight - (p.expected / 100) * plotHeight,
        ya: this.pad + plotHeight - (p.actual / 100) * plotHeight,
        he: (p.expected / 100) * plotHeight,
        ha: (p.actual / 100) * plotHeight
      };
    });
  });

  short(label: string): string {
    return label.length > 12 ? label.slice(0, 11) + '…' : label;
  }
}
