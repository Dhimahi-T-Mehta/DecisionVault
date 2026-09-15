import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ApiClientError } from '../../core/api';

@Component({
  selector: 'dv-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <div class="brand">
          <div class="brand-mark">DV</div>
          <h1>Decision<b>Vault</b></h1>
          <p class="sub">Record decisions. Track outcomes. Learn from evidence.</p>
        </div>

        @if (error(); as e) {
          <div class="alert" role="alert">{{ e }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <label>Email
            <input type="email" formControlName="email" autocomplete="email" placeholder="you@example.com" />
          </label>
          <label>Password
            <input type="password" formControlName="password" autocomplete="current-password" placeholder="••••••••" />
          </label>
          <button type="submit" class="btn primary block" [disabled]="form.invalid || busy()">
            @if (busy()) { Signing in… } @else { Sign in }
          </button>
        </form>

        <p class="swap">No account? <a routerLink="/register">Create one</a></p>
        <div class="demo-hint">
          <span>Demo:</span> demo&#64;example.com / Demo#12345
        </div>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; min-height: 100vh; background: radial-gradient(1200px 600px at 70% -10%, #1d2a47 0%, #0b1220 55%); }
    .auth-wrap { display: grid; place-items: center; min-height: 100vh; padding: 24px; }
    .auth-card { width: 100%; max-width: 400px; background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.14); border-radius: 16px; padding: 34px 30px; box-shadow: 0 24px 60px rgba(0,0,0,0.45); }
    .brand { text-align: center; margin-bottom: 22px; }
    .brand-mark { width: 44px; height: 44px; margin: 0 auto 10px; border-radius: 12px; background: linear-gradient(135deg, #f0b429, #d99a1b); color: #0b1220; font-weight: 800; display: grid; place-items: center; font-size: 16px; }
    h1 { font-size: 22px; margin: 0; letter-spacing: 0.02em; }
    h1 b { color: #f0b429; }
    .sub { color: var(--text-dim, #93a1b8); font-size: 12.5px; margin: 6px 0 0; }
    form { display: flex; flex-direction: column; gap: 14px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    input { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 10px 12px; font-size: 14px; outline: none; }
    input:focus { border-color: #f0b429; }
    .btn.block { width: 100%; margin-top: 4px; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 14px; }
    .swap { text-align: center; font-size: 13px; color: var(--text-dim, #93a1b8); margin: 18px 0 0; }
    .swap a { color: #f0b429; }
    .demo-hint { margin-top: 14px; text-align: center; font-size: 11.5px; color: var(--text-dim, #93a1b8); background: var(--surface-2, #16213a); border-radius: 8px; padding: 8px; }
    .demo-hint span { color: #f0b429; }
  `
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });
  readonly busy = signal(false);
  readonly error = signal<string | null>(this.route.snapshot.queryParamMap.get('expired') === '1'
    ? 'Your session expired. Please sign in again.'
    : null);

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigate(['/dashboard']),
      error: (err: unknown) => {
        this.error.set(err instanceof ApiClientError ? err.message : 'Sign in failed. Please try again.');
        this.busy.set(false);
      }
    });
  }
}
