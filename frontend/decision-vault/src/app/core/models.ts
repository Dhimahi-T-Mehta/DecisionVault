// DTO mirrors of the API contract (docs/api-contract.md). Enums are strings.

export interface UserDto {
  id: number;
  fullName: string;
  email: string;
  role: 'Admin' | 'User';
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: UserDto;
}

export interface CategoryDto {
  id: number;
  name: string;
  kind: string;
  description: string | null;
}

export interface CategoryUpsert {
  name: string;
  kind: string;
  description?: string | null;
}

export const CATEGORY_KINDS = ['Career', 'Education', 'Financial', 'Health', 'Technology', 'Personal', 'Business', 'Other'] as const;

export const DECISION_STATUSES = ['Draft', 'Evaluating', 'Decided', 'InProgress', 'ReadyForReview', 'Reviewed'] as const;
export type DecisionStatus = (typeof DECISION_STATUSES)[number];

export interface DecisionOptionDto {
  id: number;
  decisionId: number;
  name: string;
  description: string | null;
  advantages: string | null;
  disadvantages: string | null;
  score: number | null;
  weight: number | null;
}

export interface DecisionReasonDto {
  id: number;
  decisionId: number;
  type: 'Pro' | 'Con' | 'Note';
  category: string;
  text: string;
  createdAt: string;
}

export interface DecisionReviewDto {
  id: number;
  decisionId: number;
  actualOutcome: string;
  outcomeRating: number;
  whatWentWell: string | null;
  whatWentWrong: string | null;
  lessonsLearned: string | null;
  wouldChooseAgain: boolean;
  isSuccessful: boolean;
  reviewedAt: string;
}

export interface DecisionEventDto {
  id: number;
  eventType: string;
  description: string;
  createdAt: string;
}

export interface DecisionListItemDto {
  id: number;
  title: string;
  status: DecisionStatus;
  categoryName: string;
  confidenceScore: number | null;
  expectedSuccessScore: number | null;
  isSuccessful: boolean | null;
  outcomeRating: number | null;
  optionsCount: number;
  createdAt: string;
  reviewDate: string | null;
}

export interface DecisionDto {
  id: number;
  title: string;
  description: string | null;
  status: DecisionStatus;
  categoryId: number;
  categoryName: string;
  decisionDate: string | null;
  reviewDate: string | null;
  confidenceScore: number | null;
  expectedSuccessScore: number | null;
  expectedOutcome: string | null;
  selectedOptionId: number | null;
  selectedOptionName: string | null;
  createdAt: string;
  updatedAt: string | null;
  options: DecisionOptionDto[];
  reasons: DecisionReasonDto[];
  review: DecisionReviewDto | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DashboardDto {
  totalDecisions: number;
  pendingReview: number;
  successful: number;
  unsuccessful: number;
  averageConfidence: number;
  decisionAccuracy: number;
  decisionsByCategory: { categoryId: number; categoryName: string; count: number }[];
  outcomeDistribution: { label: string; count: number }[];
  decisionsByStatus: { status: string; count: number }[];
  recentDecisions: DecisionListItemDto[];
  monthlyTrend: { year: number; month: number; total: number; successful: number }[];
}
export interface AnalyticsDto {
  confidenceDistribution: { bucket: string; count: number }[];
  expectedVsActual: { decisionId: number; title: string; expected: number; actual: number }[];
  decisionPerformanceScore: number;
  successRateTrend: { year: number; month: number; successRate: number }[];
}

export interface ProfileSummaryDto {
  memberSince: string;
  totalDecisions: number;
  reviewedCount: number;
  averageOutcomeRating: number;
}

export interface AdminDashboardDto {
  totalUsers: number;
  activeUsers: number;
  totalDecisions: number;
  decisionsByStatus: { status: string; count: number }[];
  decisionsByCategory: { categoryId: number; name: string; count: number }[];
  overallSuccessRate: number;
  recentActivity: ActivityDto[];
}

export interface AdminUserDto {
  id: number;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  decisionCount: number;
}

export interface ApiError {
  success: false;
  message: string;
  errors: string[];
  timestamp: string;
}

export const STATUS_LABELS: Record<DecisionStatus, string> = {
  Draft: 'Draft',
  Evaluating: 'Evaluating',
  Decided: 'Decided',
  InProgress: 'In Progress',
  ReadyForReview: 'Ready for Review',
  Reviewed: 'Reviewed'
};

export interface ActivityDto {
  id: number;
  eventType: string;
  description: string;
  userEmail: string;
  createdAt: string;
}
