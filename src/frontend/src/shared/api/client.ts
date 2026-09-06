export class ApiError extends Error {
  readonly status: number;
  readonly requestId?: string;

  constructor(
    message: string,
    status: number,
    requestId?: string,
  ) {
    super(message);
    this.status = status;
    this.requestId = requestId;
  }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return response.json() as Promise<T>;
}

export async function apiRequestVoid(path: string, init?: RequestInit): Promise<void> {
  const response = await fetch(path, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response);
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = await response.json().catch((): null => null) as { detail?: string; title?: string } | null;
  return new ApiError(
    problem?.detail ?? problem?.title ?? 'The request could not be completed.',
    response.status,
    response.headers.get('x-request-id') ?? undefined,
  );
}
