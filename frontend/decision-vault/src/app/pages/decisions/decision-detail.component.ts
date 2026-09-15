import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService, FinalizeRequest, OptionUpsert, ReviewUpsert } from '../../core/api.service';
import {
  DECISION_STATUSES, DecisionDto, DecisionEventDto, DecisionReviewDto, STATUS_LABELS
} from '../../core/models';
import { ApiClientError } from '../../core/api';

type Modal = 'option' | 'reason' | 'finalize' | 'review' | 'edit' | null;

@Component({
  selector: 'dv-decision-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, ReactiveFormsModule],
  template: `
    @if (error(); as e) {
      <div class="alert">{{ e }} <button type="button" class="btn ghost small" (click)="load()">Retry</button></div>
    }

    @if (loading()) {
      <div class="card skeleton" style="min-height:220px"></div>
    } @else if (decision(); as d) {
      <a routerLink="/decisions" class="back">← All decisions</a>

      <div class="head">
        <div>
          <div class="title-row">
            <h1>{{ d.title }}</h1>
            <span class="pill big" [attr.data-status]="d.status">{{ statusLabel(d.status) }}</span>
          </div>
          <p class="meta">{{ d.categoryName }} · created {{ d.createdAt | date: 'MMM d, y' }}
            @if (d.decisionDate) { · decided {{ d.decisionDate | date: 'MMM d, y' }} }</p>
        </div>
        <div class="head-actions">
          <button type="button" class="btn ghost small" (click)="openEdit(d)">Edit</button>
          <button type="button" class="btn danger small" (click)="confirmingDelete.set(true)">Delete</button>
        </div>
      </div>

      <div class="stepper">
        @for (s of statuses; track s) {
          <div class="step" [class.done]="stepIndex(d.status) >= $index" [class.current]="s === d.status">
            <span class="dot"></span><span class="lbl">{{ statusLabel(s) }}</span>
          </div>
        }
      </div>

      @if (confirmingDelete()) {
        <div class="card warn-card">
          <p>Delete “{{ d.title }}”? This removes its options, reasons, review and timeline permanently.</p>
          <div class="row-actions">
            <button type="button" class="btn ghost small" (click)="confirmingDelete.set(false)">Keep it</button>
            <button type="button" class="btn danger small" (click)="doDelete(d)">Delete decision</button>
          </div>
        </div>
      }

      @if (actionError(); as ae) { <div class="alert">{{ ae }}</div> }

      <div class="cols">
        <div class="col-main">
          @if (d.description) { <section class="card"><h2>Context</h2><p class="pre">{{ d.description }}</p></section> }

          @if (d.expectedOutcome || d.confidenceScore != null) {
            <section class="card">
              <h2>Expectations</h2>
              <div class="kv-grid">
                @if (d.selectedOptionName) { <div class="kv"><span>Chosen option</span><b>{{ d.selectedOptionName }}</b></div> }
                @if (d.confidenceScore != null) { <div class="kv"><span>Confidence</span><b>{{ d.confidenceScore }}%</b></div> }
                @if (d.expectedSuccessScore != null) { <div class="kv"><span>Expected success</span><b>{{ d.expectedSuccessScore }}%</b></div> }
              </div>
              @if (d.expectedOutcome) { <p class="pre">{{ d.expectedOutcome }}</p> }
            </section>
          }

          <section class="card">
            <div class="sec-head">
              <h2>Options ({{ d.options.length }})</h2>
              @if (canEditOptions(d)) {
                <button type="button" class="btn ghost small" (click)="openOptionModal()">+ Add option</button>
              }
            </div>
            @if (d.options.length === 0) {
              <p class="empty">No options yet. A decision needs at least two before you can finalize it.</p>
            } @else {
              <div class="option-list">
                @for (o of d.options; track o.id) {
                  <div class="option" [class.selected]="o.id === d.selectedOptionId">
                    <div class="option-main">
                      <div class="option-title">
                        <b>{{ o.name }}</b>
                        @if (o.id === d.selectedOptionId) { <span class="chosen">✓ chosen</span> }
                      </div>
                      @if (o.description) { <p>{{ o.description }}</p> }
                      @if (o.advantages) { <p class="pro">+ {{ o.advantages }}</p> }
                      @if (o.disadvantages) { <p class="con">− {{ o.disadvantages }}</p> }
                    </div>
                    <div class="option-side">
                      @if (o.score != null) { <div class="score-chip">score {{ o.score }}/10</div> }
                      @if (o.weight != null) { <div class="score-chip dim">weight {{ o.weight }}/10</div> }
                      @if (canEditOptions(d)) {
                        <div class="row-actions">
                          <button type="button" class="btn ghost small" (click)="openOptionModal(o)">Edit</button>
                          <button type="button" class="btn ghost small" (click)="removeOption(d, o.id)">Delete</button>
                          @if (canSelect(d) && o.id !== d.selectedOptionId) {
                            <button type="button" class="btn amber small" (click)="select(d, o.id)">Select</button>
                          }
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </section>

          <section class="card">
            <div class="sec-head">
              <h2>Reasoning ({{ d.reasons.length }})</h2>
              @if (canEditReasons(d)) {
                <button type="button" class="btn ghost small" (click)="openReasonModal()">+ Add reason</button>
              }
            </div>
            @if (d.reasons.length === 0) {
              <p class="empty">No reasoning recorded yet.</p>
            } @else {
              <ul class="reasons">
                @for (r of d.reasons; track r.id) {
                  <li [attr.data-type]="r.type">
                    <span class="rtag">{{ r.type }}</span>
                    <div>
                      <span class="rcat">{{ r.category }}</span>
                      <p>{{ r.text }}</p>
                    </div>
                    @if (canEditReasons(d)) {
                      <button type="button" class="btn ghost small" (click)="removeReason(d, r.id)">✕</button>
                    }
                  </li>
                }
              </ul>
            }
          </section>
        </div>

        <div class="col-side">
          <section class="card">
            <h2>Next actions</h2>
            <div class="lifecycle">
              @for (t of transitionsFor(d); track t) {
                <button type="button" class="btn primary block" (click)="transition(d, t)">
                  {{ transitionLabel(t) }}
                </button>
              }
              @if (canSelect(d)) { <p class="hint">Select an option above (or finalize) to decide.</p> }
              @if (canFinalize(d)) {
                <button type="button" class="btn amber block" (click)="openFinalize(d)">Finalize decision…</button>
              }
              @if (canReview(d)) {
                <button type="button" class="btn amber block" (click)="openReview(d)">Record outcome review…</button>
              }
              @if (transitionsFor(d).length === 0 && !canFinalize(d) && !canReview(d)) {
                <p class="hint">Lifecycle complete. Review the outcome below.</p>
              }
            </div>
          </section>

          @if (d.review; as r) {
            <section class="card">
              <h2>Review</h2>
              <div class="review-verdict" [class.good]="r.isSuccessful" [class.bad]="!r.isSuccessful">
                {{ r.isSuccessful ? 'Successful' : 'Not successful' }} · {{ r.outcomeRating }}/5
                @if (r.wouldChooseAgain) { · would choose again }
              </div>
              <p class="pre">{{ r.actualOutcome }}</p>
              @if (r.whatWentWell) { <p class="rv"><b>What went well</b>{{ r.whatWentWell }}</p> }
              @if (r.whatWentWrong) { <p class="rv"><b>What went wrong</b>{{ r.whatWentWrong }}</p> }
              @if (r.lessonsLearned) { <p class="rv"><b>Lessons</b>{{ r.lessonsLearned }}</p> }
            </section>
          }

          <section class="card">
            <h2>Timeline</h2>
            @if (timeline().length === 0) {
              <p class="empty">No events yet.</p>
            } @else {
              <ul class="timeline">
                @for (ev of timeline(); track ev.id) {
                  <li>
                    <span class="tdot"></span>
                    <div>
                      <b>{{ ev.eventType }}</b>
                      <p>{{ ev.description }}</p>
                      <time>{{ ev.createdAt | date: 'MMM d, y HH:mm' }}</time>
                    </div>
                  </li>
                }
              </ul>
            }
          </section>
        </div>
      </div>
    }

    <!-- Option modal -->
    @if (modal() === 'option') {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h3>{{ editingOption() ? 'Edit option' : 'Add option' }}</h3>
          <form [formGroup]="optionForm" (ngSubmit)="saveOption()">
            <label>Name *<input formControlName="name" placeholder="Option name" /></label>
            <label>Description<input formControlName="description" placeholder="What is this option?" /></label>
            <label>Advantages<input formControlName="advantages" placeholder="Upsides, comma-separated is fine" /></label>
            <label>Disadvantages<input formControlName="disadvantages" placeholder="Downsides, risks" /></label>
            <div class="two">
              <label>Score (0–10)<input type="number" min="0" max="10" formControlName="score" /></label>
              <label>Weight (0–10)<input type="number" min="0" max="10" formControlName="weight" /></label>
            </div>
            <div class="row-actions">
              <button type="button" class="btn ghost" (click)="closeModal()">Cancel</button>
              <button type="submit" class="btn primary" [disabled]="optionForm.invalid || busy()">Save</button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- Reason modal -->
    @if (modal() === 'reason') {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h3>Add reason</h3>
          <form [formGroup]="reasonForm" (ngSubmit)="saveReason()">
            <label>Type *
              <select formControlName="type">
                <option value="Pro">Pro</option>
                <option value="Con">Con</option>
                <option value="Note">Note</option>
              </select>
            </label>
            <label>Category *<input formControlName="category" placeholder="e.g. Cost, Time, Career" /></label>
            <label>Reasoning *<textarea rows="3" formControlName="text" placeholder="Why does this matter for the decision?"></textarea></label>
            <div class="row-actions">
              <button type="button" class="btn ghost" (click)="closeModal()">Cancel</button>
              <button type="submit" class="btn primary" [disabled]="reasonForm.invalid || busy()">Add</button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- Finalize modal -->
    @if (modal() === 'finalize') {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h3>Finalize decision</h3>
          <p class="modal-sub">Commit to an option and record your expectations — you will review against these later.</p>
          <form [formGroup]="finalizeForm" (ngSubmit)="saveFinalize()">
            <label>Selected option *
              <select formControlName="selectedOptionId">
                <option [ngValue]="null" disabled>Choose…</option>
                @for (o of decision()?.options ?? []; track o.id) { <option [ngValue]="o.id">{{ o.name }}</option> }
              </select>
            </label>
            <div class="two">
              <label>Confidence (1–100) *<input type="number" min="1" max="100" formControlName="confidenceScore" /></label>
              <label>Expected success (1–100) *<input type="number" min="1" max="100" formControlName="expectedSuccessScore" /></label>
            </div>
            <label>Expected outcome *<textarea rows="3" formControlName="expectedOutcome" placeholder="What does success look like?"></textarea></label>
            <div class="row-actions">
              <button type="button" class="btn ghost" (click)="closeModal()">Cancel</button>
              <button type="submit" class="btn amber" [disabled]="finalizeForm.invalid || busy()">Finalize</button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- Review modal -->
    @if (modal() === 'review') {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h3>Record outcome review</h3>
          <p class="modal-sub">Be honest — the value comes from tracking reality, not from feeling good.</p>
          <form [formGroup]="reviewForm" (ngSubmit)="saveReview()">
            <label>Actual outcome *<textarea rows="3" formControlName="actualOutcome" placeholder="What actually happened?"></textarea></label>
            <label>Outcome rating (1–5) *
              <select formControlName="outcomeRating">
                @for (r of [1,2,3,4,5]; track r) { <option [value]="r">{{ r }} — {{ ratingLabel(r) }}</option> }
              </select>
            </label>
            <label>What went well<input formControlName="whatWentWell" /></label>
            <label>What went wrong<input formControlName="whatWentWrong" /></label>
            <label>Lessons learned<input formControlName="lessonsLearned" /></label>
            <label class="check"><input type="checkbox" formControlName="wouldChooseAgain" /> Would choose this again</label>
            @if (reviewWarning(); as w) { <p class="field-error">{{ w }}</p> }
            <div class="row-actions">
              <button type="button" class="btn ghost" (click)="closeModal()">Cancel</button>
              <button type="submit" class="btn amber" [disabled]="reviewForm.invalid || busy()">Submit review</button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- Edit modal -->
    @if (modal() === 'edit') {
      <div class="modal-backdrop" (click)="closeModal()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h3>Edit decision</h3>
          <form [formGroup]="editForm" (ngSubmit)="saveEdit()">
            <label>Title *<input formControlName="title" /></label>
            <label>Description<textarea rows="3" formControlName="description"></textarea></label>
            <div class="two">
              <label>Confidence (1–100)<input type="number" min="1" max="100" formControlName="confidenceScore" /></label>
              <label>Expected success (1–100)<input type="number" min="1" max="100" formControlName="expectedSuccessScore" /></label>
            </div>
            <label>Expected outcome<textarea rows="2" formControlName="expectedOutcome"></textarea></label>
            <div class="row-actions">
              <button type="button" class="btn ghost" (click)="closeModal()">Cancel</button>
              <button type="submit" class="btn primary" [disabled]="editForm.invalid || busy()">Save</button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
  styles: `
    .back { color: var(--text-dim, #93a1b8); text-decoration: none; font-size: 13px; display: inline-block; margin-bottom: 12px; }
    .back:hover { color: #f0b429; }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; margin-bottom: 14px; }
    .title-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    h1 { margin: 0; font-size: 24px; }
    .meta, .head p { color: var(--text-dim, #93a1b8); font-size: 13px; margin: 6px 0 0; }
    .stepper { display: flex; gap: 4px; flex-wrap: wrap; margin-bottom: 20px; }
    .step { display: flex; align-items: center; gap: 6px; padding: 6px 12px; border-radius: 999px; background: var(--surface-2, #16213a); color: var(--text-dim, #93a1b8); font-size: 11.5px; }
    .step .dot { width: 8px; height: 8px; border-radius: 50%; background: rgba(147,161,184,0.4); }
    .step.done { color: #4fc3a1; } .step.done .dot { background: #4fc3a1; }
    .step.current { color: #f0b429; font-weight: 700; } .step.current .dot { background: #f0b429; }
    .cols { display: grid; grid-template-columns: 1fr 340px; gap: 16px; align-items: start; }
    .col-side { display: flex; flex-direction: column; gap: 16px; }
    .card { background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.12); border-radius: 12px; padding: 18px; margin-bottom: 16px; }
    .card h2 { margin: 0 0 12px; font-size: 13px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.06em; }
    .sec-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; gap: 10px; }
    .sec-head h2 { margin: 0; }
    .pre { white-space: pre-wrap; color: var(--text, #e8ecf4); font-size: 14px; margin: 8px 0 0; }
    .kv-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; margin-bottom: 8px; }
    .kv { background: var(--surface-2, #16213a); border-radius: 9px; padding: 10px 12px; }
    .kv span { display: block; font-size: 10.5px; text-transform: uppercase; letter-spacing: 0.06em; color: var(--text-dim, #93a1b8); }
    .kv b { font-size: 15px; }
    .option { display: flex; justify-content: space-between; gap: 14px; padding: 12px; border: 1px solid rgba(147,161,184,0.1); border-radius: 10px; margin-bottom: 8px; }
    .option.selected { border-color: rgba(240,180,41,0.5); background: rgba(240,180,41,0.05); }
    .option p { margin: 4px 0 0; font-size: 13px; color: var(--text-dim, #93a1b8); }
    .option .pro { color: #4fc3a1; } .option .con { color: #e57373; }
    .option-title b { font-size: 14px; }
    .chosen { color: #f0b429; font-size: 11.5px; font-weight: 700; margin-left: 8px; }
    .option-side { display: flex; flex-direction: column; align-items: flex-end; gap: 6px; min-width: 130px; }
    .score-chip { font-size: 11px; background: var(--surface-2, #16213a); padding: 2px 8px; border-radius: 6px; color: #f0b429; font-weight: 600; }
    .score-chip.dim { color: var(--text-dim, #93a1b8); }
    .row-actions { display: flex; gap: 6px; flex-wrap: wrap; }
    .reasons { list-style: none; padding: 0; margin: 0; }
    .reasons li { display: flex; gap: 10px; align-items: flex-start; padding: 10px 0; border-bottom: 1px solid rgba(147,161,184,0.07); }
    .reasons li:last-child { border-bottom: none; }
    .rtag { font-size: 10.5px; font-weight: 800; padding: 3px 8px; border-radius: 6px; text-transform: uppercase; }
    li[data-type='Pro'] .rtag { background: rgba(79,195,161,0.15); color: #4fc3a1; }
    li[data-type='Con'] .rtag { background: rgba(229,115,115,0.15); color: #e57373; }
    li[data-type='Note'] .rtag { background: rgba(91,141,239,0.15); color: #5b8def; }
    .rcat { font-size: 11px; color: var(--text-dim, #93a1b8); text-transform: uppercase; letter-spacing: 0.05em; }
    .reasons p { margin: 2px 0 0; font-size: 13.5px; }
    .lifecycle { display: flex; flex-direction: column; gap: 8px; }
    .hint { color: var(--text-dim, #93a1b8); font-size: 12px; margin: 4px 0 0; }
    .review-verdict { display: inline-block; padding: 4px 10px; border-radius: 8px; font-weight: 700; font-size: 12.5px; margin-bottom: 8px; }
    .review-verdict.good { background: rgba(79,195,161,0.15); color: #4fc3a1; }
    .review-verdict.bad { background: rgba(229,115,115,0.15); color: #e57373; }
    .rv { font-size: 13px; margin: 8px 0 0; color: var(--text-dim, #93a1b8); }
    .rv b { display: block; color: var(--text, #e8ecf4); font-size: 11px; text-transform: uppercase; letter-spacing: 0.05em; }
    .timeline { list-style: none; padding: 0; margin: 0; }
    .timeline li { display: flex; gap: 10px; padding: 8px 0; border-bottom: 1px solid rgba(147,161,184,0.07); }
    .timeline li:last-child { border-bottom: none; }
    .tdot { width: 8px; height: 8px; border-radius: 50%; background: #f0b429; margin-top: 5px; flex-shrink: 0; }
    .timeline b { font-size: 12px; }
    .timeline p { margin: 2px 0; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    .timeline time { font-size: 11px; color: rgba(147,161,184,0.6); }
    .empty { color: var(--text-dim, #93a1b8); font-size: 13px; }
    .pill { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 600; background: var(--surface-2, #16213a); color: var(--text-dim, #93a1b8); }
    .pill.big { font-size: 12.5px; padding: 5px 14px; }
    .pill[data-status='Reviewed'] { background: rgba(79,195,161,0.15); color: #4fc3a1; }
    .pill[data-status='Decided'], .pill[data-status='InProgress'] { background: rgba(91,141,239,0.15); color: #5b8def; }
    .pill[data-status='ReadyForReview'] { background: rgba(240,180,41,0.15); color: #f0b429; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 16px; display: flex; gap: 12px; align-items: center; }
    .warn-card { border-color: rgba(229,115,115,0.4); }
    .warn-card p { margin: 0 0 10px; font-size: 13.5px; }
    .modal-backdrop { position: fixed; inset: 0; background: rgba(5,10,20,0.7); display: grid; place-items: center; z-index: 50; padding: 16px; }
    .modal { width: 100%; max-width: 480px; background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.2); border-radius: 14px; padding: 22px; max-height: 90vh; overflow-y: auto; }
    .modal h3 { margin: 0 0 8px; }
    .modal-sub { color: var(--text-dim, #93a1b8); font-size: 12.5px; margin: 0 0 14px; }
    form { display: flex; flex-direction: column; gap: 12px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    label.check { flex-direction: row; align-items: center; gap: 8px; }
    input, select, textarea { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 9px 11px; font-size: 13.5px; outline: none; font-family: inherit; }
    input:focus, select:focus, textarea:focus { border-color: #f0b429; }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .field-error { color: #f1a1a1; font-size: 12px; margin: 0; }
    @media (max-width: 980px) { .cols { grid-template-columns: 1fr; } }
  `
})
export class DecisionDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);

  readonly statuses = DECISION_STATUSES;

  readonly decision = signal<DecisionDto | null>(null);
  readonly timeline = signal<DecisionEventDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly busy = signal(false);
  readonly modal = signal<Modal>(null);
  readonly confirmingDelete = signal(false);
  readonly editingOption = signal<number | null>(null);

  readonly optionForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
    advantages: [''],
    disadvantages: [''],
    score: [null as number | null],
    weight: [null as number | null]
  });

  readonly reasonForm = this.fb.nonNullable.group({
    type: ['Pro', Validators.required],
    category: ['', Validators.required],
    text: ['', Validators.required]
  });

  readonly finalizeForm = this.fb.nonNullable.group({
    selectedOptionId: [null as number | null, Validators.required],
    confidenceScore: [70, [Validators.required, Validators.min(1), Validators.max(100)]],
    expectedSuccessScore: [75, [Validators.required, Validators.min(1), Validators.max(100)]],
    expectedOutcome: ['', [Validators.required, Validators.minLength(1)]]
  });

  readonly reviewForm = this.fb.nonNullable.group({
    actualOutcome: ['', Validators.required],
    outcomeRating: ['3', Validators.required],
    whatWentWell: [''],
    whatWentWrong: [''],
    lessonsLearned: [''],
    wouldChooseAgain: [false]
  });

  readonly editForm = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(3)]],
    description: [''],
    confidenceScore: [null as number | null],
    expectedSuccessScore: [null as number | null],
    expectedOutcome: ['']
  });

  readonly reviewWarning = computed(() => {
    const rating = Number(this.reviewForm.getRawValue().outcomeRating);
    const again = this.reviewForm.getRawValue().wouldChooseAgain;
    return rating >= 3 && !again
      ? 'Rating ≥ 3 with “would not choose again” records this decision as NOT successful.'
      : null;
  });

  private decisionId = 0;

  constructor() {
    this.route.paramMap.subscribe(params => {
      this.decisionId = Number(params.get('id'));
      void this.load();
    });
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [d, t] = await Promise.all([
        firstValueFrom(this.api.decision(this.decisionId)),
        firstValueFrom(this.api.timeline(this.decisionId)).catch(() => [] as DecisionEventDto[])
      ]);
      this.decision.set(d);
      this.timeline.set(t);
    } catch (err) {
      this.error.set(err instanceof ApiClientError ? err.message : 'Failed to load decision.');
      this.decision.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  // ---------- permission helpers ----------
  stepIndex(s: string): number { return DECISION_STATUSES.indexOf(s as never); }

  statusLabel(s: string): string { return STATUS_LABELS[s as keyof typeof STATUS_LABELS] ?? s; }

  canEditOptions(d: DecisionDto): boolean { return d.status === 'Draft' || d.status === 'Evaluating'; }
  canSelect(d: DecisionDto): boolean { return d.status === 'Draft' || d.status === 'Evaluating'; }
  canEditReasons(d: DecisionDto): boolean { return d.status !== 'ReadyForReview' && d.status !== 'Reviewed'; }
  canFinalize(d: DecisionDto): boolean { return d.status === 'Draft' || d.status === 'Evaluating'; }
  canReview(d: DecisionDto): boolean { return d.status === 'ReadyForReview'; }

  transitionsFor(d: DecisionDto): string[] {
    switch (d.status) {
      case 'Draft': return ['Evaluating'];
      case 'Evaluating': return ['Draft', 'Decided'];
      case 'Decided': return ['Evaluating', 'InProgress'];
      case 'InProgress': return ['ReadyForReview'];
      case 'ReadyForReview': return [];
      case 'Reviewed': return [];
      default: return [];
    }
  }

  transitionLabel(t: string): string {
    return t === 'Decided' ? 'Mark decided (option selected)'
      : t === 'Evaluating' ? 'Re-open evaluation'
      : t === 'InProgress' ? 'Start executing'
      : t === 'ReadyForReview' ? 'Ready for review'
      : t === 'Draft' ? 'Back to draft'
      : t;
  }

  // ---------- actions ----------
  openOptionModal(option?: { id: number; name: string; description: string | null; advantages: string | null; disadvantages: string | null; score: number | null; weight: number | null }): void {
    this.editingOption.set(option?.id ?? null);
    this.optionForm.reset({
      name: option?.name ?? '',
      description: option?.description ?? '',
      advantages: option?.advantages ?? '',
      disadvantages: option?.disadvantages ?? '',
      score: option?.score ?? null,
      weight: option?.weight ?? null
    });
    this.modal.set('option');
  }

  openReasonModal(): void { this.reasonForm.reset({ type: 'Pro', category: '', text: '' }); this.modal.set('reason'); }

  openFinalize(d: DecisionDto): void {
    this.finalizeForm.patchValue({
      selectedOptionId: d.selectedOptionId,
      confidenceScore: d.confidenceScore ?? 70,
      expectedSuccessScore: d.expectedSuccessScore ?? 75,
      expectedOutcome: d.expectedOutcome ?? ''
    });
    this.modal.set('finalize');
  }

  openReview(_d: DecisionDto): void {
    this.reviewForm.reset({ actualOutcome: '', outcomeRating: '3', whatWentWell: '', whatWentWrong: '', lessonsLearned: '', wouldChooseAgain: false });
    this.modal.set('review');
  }

  openEdit(d: DecisionDto): void {
    this.editForm.patchValue({
      title: d.title,
      description: d.description ?? '',
      confidenceScore: d.confidenceScore,
      expectedSuccessScore: d.expectedSuccessScore,
      expectedOutcome: d.expectedOutcome ?? ''
    });
    this.modal.set('edit');
  }

  closeModal(): void { this.modal.set(null); }

  async run<T>(action: () => Promise<T>): Promise<T | undefined> {
    if (this.busy()) return undefined;
    this.busy.set(true);
    this.actionError.set(null);
    try {
      return await action();
    } catch (err) {
      this.actionError.set(err instanceof ApiClientError ? err.message : 'Action failed. Please try again.');
      return undefined;
    } finally {
      this.busy.set(false);
    }
  }

  async saveOption(): Promise<void> {
    const v = this.optionForm.getRawValue();
    const body: OptionUpsert = {
      name: v.name, description: v.description || null, advantages: v.advantages || null,
      disadvantages: v.disadvantages || null, score: v.score, weight: v.weight ?? 0
    };
    const editing = this.editingOption();
    await this.run(async () => editing
      ? this.api.updateOption(this.decisionId, editing, body).toPromise()
      : this.api.addOption(this.decisionId, body).toPromise());
    this.closeModal();
    await this.load();
  }

  async saveReason(): Promise<void> {
    const v = this.reasonForm.getRawValue();
    await this.run(() => this.api.addReason(this.decisionId, {
      type: v.type as 'Pro' | 'Con' | 'Note', category: v.category, text: v.text
    }).toPromise());
    this.closeModal();
    await this.load();
  }

  async saveFinalize(): Promise<void> {
    const v = this.finalizeForm.getRawValue();
    if (v.selectedOptionId == null) return;
    const body: FinalizeRequest = {
      selectedOptionId: v.selectedOptionId,
      confidenceScore: v.confidenceScore,
      expectedSuccessScore: v.expectedSuccessScore,
      expectedOutcome: v.expectedOutcome
    };
    await this.run(() => this.api.finalize(this.decisionId, body).toPromise());
    this.closeModal();
    await this.load();
  }

  async saveReview(): Promise<void> {
    const v = this.reviewForm.getRawValue();
    const body: ReviewUpsert = {
      actualOutcome: v.actualOutcome,
      outcomeRating: Number(v.outcomeRating),
      whatWentWell: v.whatWentWell || null,
      whatWentWrong: v.whatWentWrong || null,
      lessonsLearned: v.lessonsLearned || null,
      wouldChooseAgain: v.wouldChooseAgain
    };
    await this.run(() => this.api.submitReview(this.decisionId, body).toPromise());
    this.closeModal();
    await this.load();
  }

  async saveEdit(): Promise<void> {
    const d = this.decision();
    if (!d) return;
    const v = this.editForm.getRawValue();
    await this.run(() => this.api.updateDecision(this.decisionId, {
      title: v.title,
      description: v.description || null,
      categoryId: d.categoryId,
      confidenceScore: v.confidenceScore,
      expectedSuccessScore: v.expectedSuccessScore,
      expectedOutcome: v.expectedOutcome || null
    }).toPromise());
    this.closeModal();
    await this.load();
  }

  async select(d: DecisionDto, optionId: number): Promise<void> {
    await this.run(() => this.api.selectOption(d.id, optionId).toPromise());
    await this.load();
  }

  async transition(d: DecisionDto, status: string): Promise<void> {
    await this.run(() => this.api.transition(d.id, status).toPromise());
    await this.load();
  }

  async removeOption(d: DecisionDto, optionId: number): Promise<void> {
    if (!confirm('Delete this option?')) return;
    await this.run(() => this.api.deleteOption(d.id, optionId).toPromise());
    await this.load();
  }

  async removeReason(d: DecisionDto, reasonId: number): Promise<void> {
    if (!confirm('Delete this reason?')) return;
    await this.run(() => this.api.deleteReason(d.id, reasonId).toPromise());
    await this.load();
  }

  async doDelete(d: DecisionDto): Promise<void> {
    if (!confirm(`Delete "${d.title}" permanently? Its options, reasons, and review are removed too.`)) return;
    const ok = await this.run(() => this.api.deleteDecision(d.id).toPromise());
    if (ok !== undefined) await this.router.navigate(['/decisions']);
  }

  ratingLabel(r: number): string {
    return ['Awful', 'Poor', 'Mixed', 'Good', 'Excellent'][r - 1] ?? String(r);
  }
}
