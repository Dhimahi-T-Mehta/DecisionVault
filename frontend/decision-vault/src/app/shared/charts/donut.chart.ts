import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Slice {
  label: string;
  value: number;
  color?: string;
}

const PALETTE = ['#f0b429', '#5b8def', '#4fc3a1', '#e57373', '#b58cd9', '#8d99ae', '#f4a259', '#6fcf97'];

@Component({
  selector: 'dv-donut',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 200 160'" role="img" [attr.aria-label]="title()">
      <g transform="translate(100,84)">
        @for (arc of arcs(); track arc.label) {
          <path [attr.d]="arc.d" [attr.fill]="arc.color" opacity="0.92">
            <title>{{ arc.label }}: {{ arc.value }}</title>
          </path>
        }
        <circle r="44" [attr.fill]="holeColor()" />
        <text y="-4" text-anchor="middle" class="donut-total">{{ total() }}</text>
        <text y="14" text-anchor="middle" class="donut-caption">{{ centerLabel() }}</text>
      </g>
    </svg>
    <div class="legend">
      @for (s of slices(); track s.label) {
        <span class="legend-item">
          <span class="dot" [style.background]="colorOf(s)"></span>
          {{ s.label }} <b>{{ s.value }}</b>
        </span>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; }
    .donut-total { font-size: 22px; font-weight: 700; fill: var(--text, #e8ecf4); }
    .donut-caption { font-size: 9px; fill: var(--text-dim, #93a1b8); letter-spacing: 0.08em; text-transform: uppercase; }
    .legend { display: flex; flex-wrap: wrap; gap: 6px 14px; margin-top: 8px; font-size: 12px; color: var(--text-dim, #93a1b8); }
    .dot { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-right: 5px; }
    b { color: var(--text, #e8ecf4); }
  `
})
export class DonutChart {
  readonly slices = input.required<Slice[]>();
  readonly title = input('Distribution');
  readonly centerLabel = input('TOTAL');
  readonly holeColor = input('var(--surface-2, #16213a)');

  readonly total = computed(() => this.slices().reduce((sum, s) => sum + s.value, 0));

  readonly arcs = computed(() => {
    const total = this.total();
    if (total <= 0) return [];
    const arcs: { label: string; value: number; color: string; d: string }[] = [];
    let angle = -Math.PI / 2;
    for (const s of this.slices()) {
      if (s.value <= 0) continue;
      const sweep = (s.value / total) * Math.PI * 2;
      const end = angle + sweep;
      const large = sweep > Math.PI ? 1 : 0;
      const x1 = 70 * Math.cos(angle), y1 = 70 * Math.sin(angle);
      const x2 = 70 * Math.cos(end), y2 = 70 * Math.sin(end);
      arcs.push({
        label: s.label,
        value: s.value,
        color: this.colorOf(s),
        d: `M ${x1} ${y1} A 70 70 0 ${sweep > Math.PI ? 1 : 0} 1 ${x2} ${y2} L 0 0 Z`
      });
      angle = end;
    }
    return arcs;
  });

  colorOf(s: Slice): string {
    if (s.color) return s.color;
    const i = this.slices().indexOf(s) % PALETTE.length;
    return PALETTE[i];
  }
}
