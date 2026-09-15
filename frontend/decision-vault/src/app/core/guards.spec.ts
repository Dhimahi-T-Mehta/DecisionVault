import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, type CanActivateFn } from '@angular/router';
import { authGuard, adminGuard } from './guards';
import { AuthService } from './auth.service';
import type { AuthResponse } from './models';

const asUser: AuthResponse = {
  token: 't',
  expiresAt: new Date(Date.now() + 3.6e6).toISOString(),
  user: { id: 1, fullName: 'U', email: 'u@example.com', role: 'User', createdAt: '2026-01-01T00:00:00Z' }
};
const asAdmin: AuthResponse = { ...asUser, user: { ...asUser.user, role: 'Admin' } };

function runGuard(guard: CanActivateFn): ReturnType<Router['createUrlTree']> | boolean {
  return TestBed.runInInjectionContext(() => guard(undefined as never, undefined as never)) as
    ReturnType<Router['createUrlTree']> | boolean;
}

describe('guards', () => {
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => localStorage.clear());

  it('authGuard lets an authenticated user through', () => {
    auth.applySession(asUser);
    expect(runGuard(authGuard)).toBe(true);
  });

  it('authGuard redirects anonymous users to /login', () => {
    const result = runGuard(authGuard) as ReturnType<Router['createUrlTree']>;
    expect(router.serializeUrl(result)).toBe('/login');
  });

  it('adminGuard bounces plain users to /dashboard', () => {
    auth.applySession(asUser);
    const result = runGuard(adminGuard) as ReturnType<Router['createUrlTree']>;
    expect(router.serializeUrl(result)).toBe('/dashboard');
  });

  it('adminGuard admits admins', () => {
    auth.applySession(asAdmin);
    expect(runGuard(adminGuard)).toBe(true);
  });

  it('adminGuard sends anonymous users to /login first', () => {
    const result = runGuard(adminGuard) as ReturnType<Router['createUrlTree']>;
    expect(router.serializeUrl(result)).toBe('/login');
  });
});
