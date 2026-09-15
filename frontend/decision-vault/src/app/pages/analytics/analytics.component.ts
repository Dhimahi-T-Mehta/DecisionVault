import { ChangeDetectionStrategy, Component, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AnalyticsDto } from '../../core/models';
import { DonutChart, Slice } from '../../shared/charts/donut.chart';
import { BarChart, Bar } from '../../shared/charts/bar.chart';
import { CompareChart, Pair } from '../../shared/charts/compare.chart';

@Component({
  selector: 'dv-analytics',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DonutChart, BarChart, CompareChart],
  template: `
    <div class="page-head">
      <div>
        <h1>Analytics</h1>
        <p class="sub">How well do your expectations match reality?</p>
      </div>
      <span class="score-badge" [class.good]="scoreGood()" [class.mid]="scoreMid()">
        Performance score: {{ data()?.decisionPerformanceScore ?? '—' }}
      </span>
    </div>

    @if (error(); as e) {
      <div class="alert">{{ e }} <button type="button" class="btn ghost small" (click)="load()">Retry</button></div>
    }

    @if (loading()) {
      <div class="grid"><div class="card skeleton tall"></div><div class="card skeleton tall"></div></div>
    } @else if (data(); as d) {
      @if (d.expectedVsActual.length === 0 && d.confidenceDistribution.length === 0) {
        <div class="card empty-state">
          <h2>Not enough data yet</h2>
          <p>Analytics compare expected vs. actual outcomes, so they need reviewed decisions.
            Move decisions through <b>Ready for review → Reviewed</b> to populate this page.</p>
        </div>
      }

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ d.expectedVsActual.length }}</span><span class="kpi-label">Compared decisions</span></div>
        <div class="kpi good"><span class="kpi-value">{{ successRatePct() }}%</span><span class="kpi-label">Success rate</span></div>
        <div class="kpi amber"><span class="kpi-value">{{ avgRating() }}/5</span><span class="kpi-label">Avg outcome rating</span></div>
        <div class="kpi"><span class="kpi-value">{{ d.decisionPerformanceScore }}</span><span class="kpi-label">Performance score</span></div>
      </div>

      <div class="grid">
        <div class="card">
          <h2>Confidence distribution</h2>
          <dv-bars [bars]="confidenceBars(d)" title="Confidence distribution" />
        </div>
        <div class="card">
          <h2>Expected vs actual</h2>
          <dv-compare [pairs]="comparePairs(d)" title="Expected vs actual" />
        </div>
      </div>

      <div class="card">
        <h2>Outcome contribution by decision</h2>
        <dv-donut [slices]="categorySlices(d)" title="Category performance" />
      </div>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 18px; flex-wrap: wrap; }
    h1 { margin: 0; font-size: 24px; }
    .sub { margin: 4px 0 0; color: var(--text-dim, #93a1b8); font-size: 13px; }
    .score-badge { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.2); border-radius: 999px; padding: 8px 16px; font-weight: 700; font-size: 13.5px; }
    .score-badge.good { color: #4fc3a1; border-color: rgba(79,195,161,0.4); }
    .score-badge.mid { color: #f0b429; border-color: rgba(240,180,41,0.4); }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 14px; margin-bottom: 20px; }
    .kpi { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 16px; }
    .kpi-value { display: block; font-size: 24px; font-weight: 700; }
    .kpi-label { font-size: 11px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .kpi.good .kpi-value { color: #4fc3a1; }
    .kpi.amber .kpi-value { color: #f0b429; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 18px; }
    .card h2 { margin: 0 0 12px; font-size: 13px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .tall { min-height: 180px; }
    .empty-state h2 { margin: 0 0 8px; }
    .empty-state p { color: var(--text-dim, #93a1b8); font-size: 13.5px; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 16px; display: flex; gap: 12px; align-items: center; }
  `
})
export class AnalyticsComponent {
  private readonly api = inject(ApiService);

  readonly data = signal<AnalyticsDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  /** Most recent monthly success rate from the trend, 0-100. */
  readonly successRatePct = computed(() => {
    const trend = this.data()?.successRateTrend ?? [];
    if (trend.length === 0) return 0;
    const last = trend[trend.length - 1];
    return Math.round(last.successRate);
  });
  /** Average outcome rating derived from compared decisions (actual 0-100 mapped to /5). */
  readonly avgRating = computed(() => {
    const rows = this.data()?.expectedVsActual ?? [];
    if (rows.length === 0) return '—';
    const avg = rows.reduce((sum, r) => sum + r.actual, 0) / rows.length / 20;
    return avg.toFixed(1);
  });

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.data.set(await firstValueFrom(this.api.analytics()));
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load analytics.');
    } finally {
      this.loading.set(false);
    }
  }

  confidenceBars(d: AnalyticsDto): Bar[] {
    const labels: Record<string, string> = {
      Low: 'Low (1–39)', Medium: 'Medium (40–69)', High: 'High (70–89)', VeryHigh: 'Very high (90–100)'
    };
    return d.confidenceDistribution.map(b => ({ label: labels[b.bucket] ?? b.bucket, value: b.count }));
  }

  comparePairs(d: AnalyticsDto): Pair[] {
    return d.expectedVsActual.slice(0, 6).map(p => ({
      label: p.title,
      expected: p.expected,
      actual: p.actual
    }));
  }

  categorySlices(d: AnalyticsDto): Slice[] {
    return d.expectedVsActual.map(p => ({ label: p.title, value: p.actual }));
  }

  scoreGood(): boolean { const s = this.data()?.decisionPerformanceScore; return s != null && s >= 70; }
  scoreMid(): boolean { const s = this.data()?.decisionPerformanceScore; return s != null && s >= 40 && s < 70; }
}
