import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('starts unauthenticated with empty storage', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.user()).toBeNull();
    expect(service.isAdmin()).toBeFalse();
  });

  it('login stores token, user and expiry, then reports authenticated', () => {
    const res = {
      token: 'jwt-abc',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 7, fullName: 'Demo', email: 'demo@example.com', role: 'User', createdAt: '2026-01-01T00:00:00Z' }
    };
    service.login('demo@example.com', 'Demo#12345').subscribe(r => expect(r.user.email).toBe('demo@example.com'));

    const req = http.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'demo@example.com', password: 'Demo#12345' });
    req.flush(res);

    expect(service.token()).toBe('jwt-abc');
    expect(service.user()?.role).toBe('User');
    expect(service.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem('dv_token')).toBe('jwt-abc');
  });

  it('logout clears the session', () => {
    const res = {
      token: 'jwt-xyz',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 1, fullName: 'A', email: 'a@example.com', role: 'Admin', createdAt: '2026-01-01T00:00:00Z' }
    };
    service.login('a@example.com', 'Password#1').subscribe();
    http.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(res);
    expect(service.isAuthenticated()).toBeTrue();

    service.logout();
    expect(service.token()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem('dv_token')).toBeNull();
  });

  it('isAdmin reflects the stored role', () => {
    const res = {
      token: 'jwt-admin',
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: 2, fullName: 'Admin', email: 'admin@example.com', role: 'Admin', createdAt: '2026-01-01T00:00:00Z' }
    };
    service.login('admin@example.com', 'Password#1').subscribe();
    http.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(res);
    expect(service.isAdmin()).toBeTrue();
  });
});
