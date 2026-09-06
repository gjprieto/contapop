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
