import type {
  ChangePasswordRequest,
  EmailOnlyRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  UserResponse,
} from '@commerce/shared-types';
import type { ApiClient } from './client';
import type { CsrfMemory } from './csrf';
import { isApiError } from './errors';

export interface AuthApi {
  register: (body: RegisterRequest) => Promise<UserResponse>;
  login: (body: LoginRequest) => Promise<UserResponse>;
  logout: () => Promise<void>;
  logoutAll: () => Promise<void>;
  /** `null` when the browser has no session. Other failures are thrown. */
  currentUser: () => Promise<UserResponse | null>;
  requestEmailVerification: (email?: string | null) => Promise<void>;
  confirmEmailVerification: (token: string) => Promise<void>;
  requestPasswordReset: (email: string) => Promise<void>;
  confirmPasswordReset: (body: ResetPasswordRequest) => Promise<void>;
  profile: () => Promise<UserResponse>;
  updateProfile: (body: UpdateProfileRequest) => Promise<UserResponse>;
  changePassword: (body: ChangePasswordRequest) => Promise<void>;
  adminLogin: (body: LoginRequest) => Promise<UserResponse>;
  /** Throws `ApiError` with status 401 or 403. Does not hide a forbidden session. */
  adminCurrentUser: () => Promise<UserResponse>;
}

export function createAuthApi(client: ApiClient, csrf: CsrfMemory): AuthApi {
  async function send<T>(method: 'post' | 'patch', path: string, body?: unknown): Promise<T> {
    await csrf.ensure(client);
    const run = () =>
      method === 'post' ? client.post<T>(path, body) : client.patch<T>(path, body);
    try {
      return await run();
    } catch (error) {
      if (!isStaleCsrf(error)) throw error;
      csrf.clear();
      await csrf.ensure(client);
      return run();
    }
  }

  return {
    register: (body) => send<UserResponse>('post', '/api/v1/auth/register', body),
    login: (body) => send<UserResponse>('post', '/api/v1/auth/login', body),
    logout: () => send<void>('post', '/api/v1/auth/logout'),
    logoutAll: () => send<void>('post', '/api/v1/auth/logout-all'),
    currentUser: () => readOptionalUser(client, '/api/v1/auth/me'),
    requestEmailVerification: (email = null) =>
      send<void>('post', '/api/v1/auth/email-verification/request', {
        email,
      } satisfies EmailOnlyRequest),
    confirmEmailVerification: (token) =>
      send<void>('post', '/api/v1/auth/email-verification/confirm', { token }),
    requestPasswordReset: (email) =>
      send<void>('post', '/api/v1/auth/password-reset/request', { email }),
    confirmPasswordReset: (body) => send<void>('post', '/api/v1/auth/password-reset/confirm', body),
    profile: () => client.get<UserResponse>('/api/v1/account/profile'),
    updateProfile: (body) => send<UserResponse>('patch', '/api/v1/account/profile', body),
    changePassword: (body) => send<void>('post', '/api/v1/account/change-password', body),
    adminLogin: (body) => send<UserResponse>('post', '/api/v1/admin/auth/login', body),
    adminCurrentUser: () => client.get<UserResponse>('/api/v1/admin/auth/me'),
  };
}

async function readOptionalUser(client: ApiClient, path: string): Promise<UserResponse | null> {
  try {
    return await client.get<UserResponse>(path);
  } catch (error) {
    if (isApiError(error) && error.status === 401) return null;
    throw error;
  }
}

function isStaleCsrf(error: unknown): boolean {
  return (
    isApiError(error) &&
    error.status === 400 &&
    error.title === 'The request could not be validated.'
  );
}
