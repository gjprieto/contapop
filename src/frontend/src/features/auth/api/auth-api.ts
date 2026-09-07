import { apiRequest, apiRequestVoid } from '../../../shared/api/client';

export type CurrentUser = {
  userId: string;
  tenantId: string;
  projectId: string;
  name: string;
  email: string;
  theme: string;
  language: string;
  notificationsEnabled: boolean;
  version: number;
};

export type LoginInput = {
  email: string;
  password: string;
};

export type UpdateUserProfileInput = {
  name: string;
};

export type UpdateUserPreferencesInput = {
  theme?: 'light' | 'dark';
  language?: string;
  notificationsEnabled?: boolean;
};

export type UpdateUserProfileResponse = Pick<CurrentUser, 'userId' | 'name' | 'version'> & {
  updatedAt: string;
};

export type UpdateUserPreferencesResponse = Pick<CurrentUser, 'userId' | 'theme' | 'language' | 'notificationsEnabled' | 'version'> & {
  updatedAt: string;
};

export function getCurrentUser(signal?: AbortSignal) {
  return apiRequest<CurrentUser>('/experience/v1/user', { signal });
}

export function login(input: LoginInput) {
  return apiRequestVoid('/experience/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
}

export function updateUserProfile(input: UpdateUserProfileInput & { version: number }) {
  return apiRequest<UpdateUserProfileResponse>('/experience/v1/user', {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'If-Match': `"${input.version}"` },
    body: JSON.stringify({ name: input.name }),
  });
}

export function updateUserPreferences(input: UpdateUserPreferencesInput & { version: number }) {
  return apiRequest<UpdateUserPreferencesResponse>('/experience/v1/settings', {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'If-Match': `"${input.version}"` },
    body: JSON.stringify({
      theme: input.theme,
      language: input.language,
      notificationsEnabled: input.notificationsEnabled,
    }),
  });
}
