import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Point {
  x: number;   // category index
  y: number;   // value
  label?: string;
  /** Calendar year when x is a month number (1-12); axis labels append it. */
  year?: number;
}

/** Simple line/area chart over a 0..100 or auto-scaled domain. */
@Component({
  selector: 'dv-line',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 320 ' + height()" role="img" [attr.aria-label]="title()">
      <line [attr.x1]="pad" [attr.y1]="height() - pad" [attr.x2]="320 - pad" [attr.y2]="height() - pad" class="axis" />
      <line [attr.x1]="pad" [attr.y1]="pad" [attr.x2]="pad" [attr.y2]="height() - pad" class="axis" />
      @if (points().length > 1) {
        <path [attr.d]="areaPath()" [attr.fill]="color()" opacity="0.15" />
        <path [attr.d]="linePath()" fill="none" [attr.stroke]="color()" stroke-width="2.5" stroke-linejoin="round" />
      }
      @for (p of placed(); track p.x) {
        <circle [attr.cx]="p.cx" [attr.cy]="p.cy" r="3.2" [attr.fill]="color()">
          <title>{{ p.label ?? p.x }}: {{ p.y }}</title>
        </circle>
      }
      @for (t of xLabels(); track t.i) {
        <text [attr.x]="t.x" [attr.y]="height() - 4" text-anchor="middle" class="x-label">{{ t.text }}</text>
      }
    </svg>
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; }
    .axis { stroke: rgba(147,161,184,0.25); stroke-width: 1; }
    .x-label { font-size: 9px; fill: var(--text-dim, #93a1b8); }
  `
})
export class LineChart {
  readonly points = input.required<Point[]>();
  readonly title = input('Trend');
  readonly color = input('#5b8def');
  readonly height = input(170);
  readonly xLabelFor = input<((p: Point) => string) | null>(null);
  readonly pad = 30;

  readonly placed = computed(() => {
    const pts = this.points();
    if (pts.length === 0) return [];
    const max = Math.max(...pts.map(p => p.y), 1);
    const w = 320 - this.pad * 2;
    const h = this.height() - this.pad - 16;
    return pts.map((p, i) => ({
      ...p,
      cx: pts.length === 1 ? this.pad + w / 2 : this.pad + (i / (pts.length - 1)) * w,
      cy: this.height() - 16 - (p.y / max) * h
    }));
  });

  readonly linePath = computed(() =>
    this.placed().map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.cx.toFixed(1)} ${p.cy.toFixed(1)}`).join(' '));

  readonly areaPath = computed(() => {
    const pts = this.placed();
    if (pts.length < 2) return '';
    const base = this.height() - 16;
    return `${this.linePath()} L ${pts[pts.length - 1].cx.toFixed(1)} ${base} L ${pts[0].cx.toFixed(1)} ${base} Z`;
  });

  readonly xLabels = computed(() => {
    const fmt = this.xLabelFor();
    if (!fmt) return [];
    return this.placed()
      .filter((_, i) => this.placed().length <= 6 || i % Math.ceil(this.placed().length / 6) === 0)
      .map(p => ({ i: p.x, x: p.cx, text: fmt(p) }));
  });
}
