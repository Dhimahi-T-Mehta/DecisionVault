import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ApiClientError } from '../../core/api';

@Component({
  selector: 'dv-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <div class="brand">
          <div class="brand-mark">DV</div>
          <h1>Create your <b>vault</b></h1>
          <p class="sub">Start recording decisions and learning from outcomes.</p>
        </div>

        @if (error(); as e) {
          <div class="alert" role="alert">{{ e }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <label>Full name
            <input type="text" formControlName="fullName" autocomplete="name" placeholder="Ada Lovelace" />
          </label>
          <label>Email
            <input type="email" formControlName="email" autocomplete="email" placeholder="you@example.com" />
          </label>
          <label>Password
            <input type="password" formControlName="password" autocomplete="new-password" placeholder="8+ chars, upper, lower, digit" />
          </label>
          <label>Confirm password
            <input type="password" formControlName="confirm" autocomplete="new-password" placeholder="Repeat password" />
          </label>
          @if (mismatch()) {
            <p class="field-error">Passwords do not match.</p>
          }
          <button type="submit" class="btn primary block" [disabled]="form.invalid || busy()">
            @if (busy()) { Creating… } @else { Create account }
          </button>
        </form>

        <p class="swap">Already registered? <a routerLink="/login">Sign in</a></p>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; min-height: 100vh; background: radial-gradient(1200px 600px at 30% -10%, #1d2a47 0%, #0b1220 55%); }
    .auth-wrap { display: grid; place-items: center; min-height: 100vh; padding: 24px; }
    .auth-card { width: 100%; max-width: 420px; background: var(--surface-1, #0e1626); border: 1px solid rgba(147,161,184,0.14); border-radius: 16px; padding: 32px 30px; box-shadow: 0 24px 60px rgba(0,0,0,0.45); }
    .brand { text-align: center; margin-bottom: 20px; }
    .brand-mark { width: 44px; height: 44px; margin: 0 auto 10px; border-radius: 12px; background: linear-gradient(135deg, #f0b429, #d99a1b); color: #0b1220; font-weight: 800; display: grid; place-items: center; font-size: 16px; }
    h1 { font-size: 21px; margin: 0; }
    h1 b { color: #f0b429; }
    .sub { color: var(--text-dim, #93a1b8); font-size: 12.5px; margin: 6px 0 0; }
    form { display: flex; flex-direction: column; gap: 13px; }
    label { display: flex; flex-direction: column; gap: 6px; font-size: 12.5px; color: var(--text-dim, #93a1b8); }
    input { background: var(--surface-2, #16213a); border: 1px solid rgba(147,161,184,0.2); color: var(--text, #e8ecf4); border-radius: 9px; padding: 10px 12px; font-size: 14px; outline: none; }
    input:focus { border-color: #f0b429; }
    .field-error { color: #f1a1a1; font-size: 12px; margin: -6px 0 0; }
    .btn.block { width: 100%; margin-top: 4px; }
    .alert { background: rgba(229,115,115,0.12); border: 1px solid rgba(229,115,115,0.4); color: #f1a1a1; border-radius: 9px; padding: 10px 12px; font-size: 13px; margin-bottom: 12px; }
    .swap { text-align: center; font-size: 13px; color: var(--text-dim, #93a1b8); margin: 16px 0 0; }
    .swap a { color: #f0b429; }
  `
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,72}$/)]],
    confirm: ['', Validators.required]
  });
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly mismatch = signal(false);

  submit(): void {
    const { password, confirm } = this.form.getRawValue();
    if (password !== confirm) {
      this.mismatch.set(true);
      return;
    }
    this.mismatch.set(false);
    if (this.form.invalid || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);
    const { fullName, email } = this.form.getRawValue();
    this.auth.register(fullName, email, password).subscribe({
      next: () => void this.router.navigate(['/dashboard']),
      error: (err: unknown) => {
        this.error.set(err instanceof ApiClientError ? err.message : 'Registration failed. Please try again.');
        this.busy.set(false);
      }
    });
  }
}
