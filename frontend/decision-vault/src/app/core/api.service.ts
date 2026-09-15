import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import type {
  ActivityDto, AdminDashboardDto, AdminUserDto, AnalyticsDto, CategoryDto, CategoryUpsert, DashboardDto,
  DecisionDto, DecisionListItemDto, DecisionOptionDto, DecisionReasonDto, DecisionReviewDto,
  DecisionEventDto, PagedResult, ProfileSummaryDto, UserDto
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // ---------- Auth ----------
  me(): Observable<UserDto> { return this.http.get<UserDto>(`${this.base}/api/auth/me`); }

  // ---------- Categories ----------
  categories(): Observable<CategoryDto[]> { return this.http.get<CategoryDto[]>(`${this.base}/api/categories`); }
  createCategory(body: CategoryUpsert): Observable<CategoryDto> { return this.http.post<CategoryDto>(`${this.base}/api/categories`, body); }
  updateCategory(id: number, body: CategoryUpsert): Observable<CategoryDto> { return this.http.put<CategoryDto>(`${this.base}/api/categories/${id}`, body); }
  deleteCategory(id: number): Observable<void> { return this.http.delete<void>(`${this.base}/api/categories/${id}`); }

  // ---------- Decisions ----------
  decisions(params: DecisionListParams): Observable<PagedResult<DecisionListItemDto>> {
    let hp = new HttpParams()
      .set('page', params.page)
      .set('pageSize', params.pageSize)
      .set('sortBy', params.sortBy ?? 'CreatedAt')
      .set('sortDir', params.sortDir ?? 'desc');
    if (params.search) hp = hp.set('search', params.search);
    if (params.categoryId != null) hp = hp.set('categoryId', params.categoryId);
    if (params.status) hp = hp.set('status', params.status);
    if (params.outcome) hp = hp.set('outcome', params.outcome);
    if (params.minConfidence != null) hp = hp.set('minConfidence', params.minConfidence);
    if (params.maxConfidence != null) hp = hp.set('maxConfidence', params.maxConfidence);
    return this.http.get<PagedResult<DecisionListItemDto>>(`${this.base}/api/decisions`, { params: hp });
  }

  decision(id: number): Observable<DecisionDto> { return this.http.get<DecisionDto>(`${this.base}/api/decisions/${id}`); }
  createDecision(body: DecisionCreate): Observable<DecisionDto> { return this.http.post<DecisionDto>(`${this.base}/api/decisions`, body); }
  updateDecision(id: number, body: DecisionUpdate): Observable<DecisionDto> { return this.http.put<DecisionDto>(`${this.base}/api/decisions/${id}`, body); }
  deleteDecision(id: number): Observable<void> { return this.http.delete<void>(`${this.base}/api/decisions/${id}`); }

  addOption(id: number, body: OptionUpsert): Observable<DecisionOptionDto> { return this.http.post<DecisionOptionDto>(`${this.base}/api/decisions/${id}/options`, body); }
  updateOption(id: number, optionId: number, body: OptionUpsert): Observable<DecisionOptionDto> { return this.http.put<DecisionOptionDto>(`${this.base}/api/decisions/${id}/options/${optionId}`, body); }
  deleteOption(id: number, optionId: number): Observable<void> { return this.http.delete<void>(`${this.base}/api/decisions/${id}/options/${optionId}`); }
  setUserRole(id: number, role: 'User' | 'Admin'): Observable<AdminUserDto> { return this.http.put<AdminUserDto>(`${this.base}/api/admin/users/${id}/role`, { role }); }

  addReason(id: number, body: ReasonUpsert): Observable<DecisionReasonDto> { return this.http.post<DecisionReasonDto>(`${this.base}/api/decisions/${id}/reasons`, body); }
  deleteReason(id: number, reasonId: number): Observable<void> { return this.http.delete<void>(`${this.base}/api/decisions/${id}/reasons/${reasonId}`); }

  selectOption(id: number, optionId: number): Observable<DecisionDto> { return this.http.post<DecisionDto>(`${this.base}/api/decisions/${id}/select-option`, { optionId }); }
  transition(id: number, status: string): Observable<DecisionDto> { return this.http.post<DecisionDto>(`${this.base}/api/decisions/${id}/transition`, { status }); }
  finalize(id: number, body: FinalizeRequest): Observable<DecisionDto> { return this.http.post<DecisionDto>(`${this.base}/api/decisions/${id}/finalize`, body); }

  submitReview(id: number, body: ReviewUpsert): Observable<DecisionReviewDto> { return this.http.post<DecisionReviewDto>(`${this.base}/api/decisions/${id}/review`, body); }
  review(id: number): Observable<DecisionReviewDto> { return this.http.get<DecisionReviewDto>(`${this.base}/api/decisions/${id}/review`); }
  timeline(id: number): Observable<DecisionEventDto[]> { return this.http.get<DecisionEventDto[]>(`${this.base}/api/decisions/${id}/timeline`); }

  // ---------- Dashboard / Analytics ----------
  dashboard(): Observable<DashboardDto> { return this.http.get<DashboardDto>(`${this.base}/api/dashboard`); }
  analytics(): Observable<AnalyticsDto> { return this.http.get<AnalyticsDto>(`${this.base}/api/dashboard/analytics`); }
  profileSummary(): Observable<ProfileSummaryDto> { return this.http.get<ProfileSummaryDto>(`${this.base}/api/dashboard/profile-summary`); }

  // ---------- Admin ----------
  adminDashboard(): Observable<AdminDashboardDto> { return this.http.get<AdminDashboardDto>(`${this.base}/api/admin/dashboard`); }
  adminUsers(search: string | null, page: number, pageSize: number): Observable<PagedResult<AdminUserDto>> {
    let hp = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) hp = hp.set('search', search);
    return this.http.get<PagedResult<AdminUserDto>>(`${this.base}/api/admin/users`, { params: hp });
  }
  setUserActive(id: number, isActive: boolean): Observable<AdminUserDto> {
    return this.http.put<AdminUserDto>(`${this.base}/api/admin/users/${id}/status`, { isActive });
  }
  adminActivity(page: number, pageSize: number): Observable<PagedResult<ActivityDto>> {
    return this.http.get<PagedResult<ActivityDto>>(`${this.base}/api/admin/activity`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize)
    });
  }
}

export interface DecisionListParams {
  page: number;
  pageSize: number;
  search?: string | null;
  categoryId?: number | null;
  status?: string | null;
  outcome?: string | null;
  minConfidence?: number | null;
  maxConfidence?: number | null;
  sortBy?: string;
  sortDir?: string;
}

export interface DecisionCreate { title: string; description?: string | null; categoryId: number; }
export interface DecisionUpdate {
  title: string;
  description?: string | null;
  categoryId: number;
  decisionDate?: string | null;
  reviewDate?: string | null;
  confidenceScore?: number | null;
  expectedSuccessScore?: number | null;
  expectedOutcome?: string | null;
}
export interface OptionUpsert {
  name: string;
  description?: string | null;
  advantages?: string | null;
  disadvantages?: string | null;
  score?: number | null;
  weight?: number | null;
}
export interface ReasonUpsert { type: 'Pro' | 'Con' | 'Note'; category: string; text: string; }
export interface FinalizeRequest {
  selectedOptionId: number;
  confidenceScore: number;
  expectedSuccessScore: number;
  expectedOutcome: string;
}
export interface ReviewUpsert {
  actualOutcome: string;
  outcomeRating: number;
  whatWentWell?: string | null;
  whatWentWrong?: string | null;
  lessonsLearned?: string | null;
  wouldChooseAgain: boolean;
}
