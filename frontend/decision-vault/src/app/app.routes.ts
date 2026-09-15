import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/guards';
import { LayoutComponent } from './layout/layout.component';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent), title: 'Sign in — DecisionVault' },
  { path: 'register', loadComponent: () => import('./pages/register/register.component').then(m => m.RegisterComponent), title: 'Create account — DecisionVault' },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent), title: 'Dashboard — DecisionVault' },
      { path: 'decisions', loadComponent: () => import('./pages/decisions/decisions-list.component').then(m => m.DecisionsListComponent), title: 'Decisions — DecisionVault' },
      { path: 'decisions/new', loadComponent: () => import('./pages/decisions/decision-create.component').then(m => m.DecisionCreateComponent), title: 'New decision — DecisionVault' },
      { path: 'decisions/:id', loadComponent: () => import('./pages/decisions/decision-detail.component').then(m => m.DecisionDetailComponent), title: 'Decision — DecisionVault' },
      { path: 'analytics', loadComponent: () => import('./pages/analytics/analytics.component').then(m => m.AnalyticsComponent), title: 'Analytics — DecisionVault' },
      { path: 'profile', loadComponent: () => import('./pages/profile/profile.component').then(m => m.ProfileComponent), title: 'Profile — DecisionVault' },
      { path: 'admin', canActivate: [adminGuard], loadComponent: () => import('./pages/admin/admin.component').then(m => m.AdminComponent), title: 'Admin — DecisionVault' }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
