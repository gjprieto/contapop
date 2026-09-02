# Frontend Code Guidelines

## Scope And Baseline

These guidelines apply to `src/frontend`, which uses React 19, TypeScript 5.9, Vite 8, and ESLint 9. The current compiler configuration enables strict TypeScript checks, unused-code checks, bundler module resolution, and the React JSX transform.

The frontend is the web experience for the distributed backend. It calls only the Experience API under `/api`; it must not call System or Process APIs, Dapr, Redis, databases, or service-specific infrastructure directly.

## Core Rules

- Use TypeScript for all application code. Do not introduce JavaScript application files.
- Prefer function components, named exports, and explicit `type` definitions for component props and API contracts.
- Keep a component, hook, or function focused on one responsibility. Extract only when code becomes reusable or a component's purpose becomes unclear.
- Keep domain and transport terminology consistent with the API contracts. Do not create frontend-only names for the same business concept.
- Use the existing ESLint configuration and run `npm run lint` and `npm run build` before completing frontend work.
- Do not use `any`, non-null assertions, or type assertions to bypass errors. Narrow unknown values at boundaries instead.
- Do not store secrets, service credentials, internal URLs, or authorization decisions in browser code.

## Project Structure

Organize code by user-facing feature. Keep cross-feature primitives in `shared` and app composition in `app`.

```text
src/
  app/
    providers/
    router.tsx
  features/
    invoices/
      api/
      components/
      hooks/
      pages/
      types.ts
    clients/
  shared/
    api/
    components/
    formatting/
    hooks/
  main.tsx
```

- `features` owns feature pages, feature-specific components, query hooks, and view models.
- `shared/api` owns the Experience API client and generic transport utilities.
- `shared/components` contains genuinely reusable, accessible presentation primitives. Do not move a feature component there prematurely.
- `app` owns global providers, routes, error boundaries, and application bootstrap.
- Keep test files next to the code they verify using `*.test.ts` or `*.test.tsx`.
- Use lowercase, delimiter-separated filenames: `invoice-list.tsx`, `use-invoice-list.ts`, and `format-money.ts`.

## Components And Props

Use semantic HTML first. A component should receive the data and callbacks it needs, rather than reaching into unrelated global state.

```tsx
type InvoiceSummaryProps = {
  invoice: InvoiceListItem;
  onOpen: (invoiceId: string) => void;
};

export function InvoiceSummary({ invoice, onOpen }: InvoiceSummaryProps) {
  return (
    <article aria-labelledby={`invoice-${invoice.id}`}>
      <h2 id={`invoice-${invoice.id}`}>{invoice.number}</h2>
      <p>{formatMoney(invoice.totalMinor, invoice.currency)}</p>
      <button type="button" onClick={() => onOpen(invoice.id)}>
        View invoice
      </button>
    </article>
  );
}
```

- Define prop types near the component and avoid `React.FC`.
- Use stable domain identifiers as keys. Do not use array indexes for records that can be inserted, removed, sorted, or filtered.
- Use `children` only when composition is the component's purpose; do not use it as an untyped escape hatch.
- Keep data loading outside leaf components. Pass loaded data into presentational components.
- Derive display values during rendering. Do not copy props or server data into local state unless the user is intentionally editing a draft.

```tsx
// Preferred: derived from the current input.
const overdueInvoices = invoices.filter((invoice) => invoice.status === 'overdue');

// Avoid: state that can become stale or needs synchronization effects.
const [overdueInvoices, setOverdueInvoices] = useState<InvoiceListItem[]>([]);
```

## State And Effects

Choose state according to its owner and lifetime.

| State | Location | Examples |
| --- | --- | --- |
| Server state | TanStack Query hooks | Invoices, clients, balances, workflow status |
| URL state | React Router search parameters and route parameters | Current invoice, filters, page, sort order |
| Local UI state | `useState` or `useReducer` | Open dialog, selected row, unsaved form state |
| Cross-application UI state | A small provider only when necessary | Current authenticated user, locale, theme |

- Do not use `useEffect` to derive state, transform props, submit forms, or fetch server data once TanStack Query is available.
- Use `useEffect` only to synchronize React with an external system, such as a browser API, subscription, or imperative third-party widget. Always clean up subscriptions and timers.
- Use `useEffectEvent` for non-reactive callbacks read inside effects when it prevents reconnecting or resubscribing due to unrelated state changes.
- Use `startTransition` for non-urgent state updates caused by navigation or expensive filtering. Use `useDeferredValue` when a rendered value may lag behind fast user input.
- Do not add `useMemo` or `useCallback` by default. Add them only after measurement, to preserve referential identity required by an API, or when the existing project conventions require them.

```tsx
import { startTransition, useDeferredValue, useState } from 'react';

export function InvoiceSearch({ invoices }: { invoices: InvoiceListItem[] }) {
  const [query, setQuery] = useState('');
  const deferredQuery = useDeferredValue(query);
  const normalizedQuery = deferredQuery.trim().toLocaleLowerCase();
  const results = invoices.filter((invoice) =>
    invoice.number.toLocaleLowerCase().includes(normalizedQuery),
  );

  return (
    <>
      <input
        type="search"
        value={query}
        onChange={(event) => {
          startTransition(() => setQuery(event.target.value));
        }}
        aria-label="Search invoices"
      />
      <InvoiceList invoices={results} />
    </>
  );
}
```

## Experience API Client

Use one typed client boundary for the Experience API. Feature code calls feature-specific functions or hooks; it must not scatter raw `fetch` calls across components.

```ts
// shared/api/client.ts
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly requestId?: string,
  ) {
    super(message);
  }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    credentials: 'include',
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null;
    throw new ApiError(
      problem?.detail ?? 'The request could not be completed.',
      response.status,
      response.headers.get('x-request-id') ?? undefined,
    );
  }

  return response.json() as Promise<T>;
}
```

- Use relative `/api` URLs so Vite can proxy requests in development and the production host can serve the same frontend without environment-specific API URLs.
- Set `credentials: 'include'` when the selected authentication design uses secure cookies. Do not manually persist session tokens in `localStorage`.
- Treat successful HTTP responses as untrusted at the transport boundary when contracts are not generated. Validate critical payloads with Zod before they reach feature code.
- Map RFC 7807 problem details to safe, actionable user messages. Log request IDs and technical context without exposing server internals in the UI.
- Use `AbortSignal` provided by the query library for cancellable requests. Never suppress an aborted request as an unknown error.

## Server State With TanStack Query

Adopt TanStack Query before building feature data fetching. Query keys are a stable public convention within the frontend and must include every server-side input that changes the result.

```ts
// features/invoices/api/invoice-queries.ts
import { queryOptions, useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../../shared/api/client';
import type { InvoiceListItem, InvoiceListFilters } from '../types';

export const invoiceKeys = {
  all: ['invoices'] as const,
  list: (filters: InvoiceListFilters) => [...invoiceKeys.all, 'list', filters] as const,
  detail: (invoiceId: string) => [...invoiceKeys.all, 'detail', invoiceId] as const,
};

export function invoiceListOptions(filters: InvoiceListFilters) {
  const parameters = new URLSearchParams();

  if (filters.status) {
    parameters.set('status', filters.status);
  }

  return queryOptions({
    queryKey: invoiceKeys.list(filters),
    queryFn: ({ signal }) =>
      apiRequest<InvoiceListItem[]>(`/invoices?${parameters}`, { signal }),
    staleTime: 30_000,
  });
}

export function useInvoiceList(filters: InvoiceListFilters) {
  return useQuery(invoiceListOptions(filters));
}
```

```tsx
// features/invoices/components/issue-invoice-button.tsx
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiRequest } from '../../../shared/api/client';
import { invoiceKeys } from '../api/invoice-queries';

type IssueInvoiceInput = {
  invoiceId: string;
  idempotencyKey: string;
};

export function IssueInvoiceButton({ invoiceId }: { invoiceId: string }) {
  const queryClient = useQueryClient();
  const issueInvoice = useMutation({
    mutationFn: ({ invoiceId: id, idempotencyKey }: IssueInvoiceInput) =>
      apiRequest<void>(`/invoices/${id}/issue`, {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: invoiceKeys.all });
    },
  });

  return (
    <button
      type="button"
      disabled={issueInvoice.isPending}
      onClick={() => issueInvoice.mutate({ invoiceId, idempotencyKey: crypto.randomUUID() })}
    >
      {issueInvoice.isPending ? 'Issuing...' : 'Issue invoice'}
    </button>
  );
}
```

- Never use a query for a write or a mutation for a read.
- Invalidate affected query keys after a successful mutation. Use optimistic updates only when the rollback behavior is simple, tested, and clearly communicated to the user.
- Keep a mutation idempotency key stable for a retried user action. Generate it when the action begins and retain it until success or final failure; do not generate a new key for an automatic retry.
- Treat event-driven projection updates as eventually consistent. Show pending states after commands when a read model may not immediately reflect a completed write.

## Forms And Validation

Use React Hook Form with Zod for new forms. Client validation improves feedback but does not replace server-side validation and authorization.

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

const expenseSchema = z.object({
  description: z.string().trim().min(1, 'Enter a description.').max(200),
  amountMinor: z.coerce.number().int().positive('Enter an amount greater than zero.'),
  currency: z.string().length(3).toUpperCase(),
  incurredOn: z.string().date(),
});

type ExpenseFormValues = z.infer<typeof expenseSchema>;

export function ExpenseForm({ onSubmit }: { onSubmit: (values: ExpenseFormValues) => Promise<void> }) {
  const form = useForm<ExpenseFormValues>({
    resolver: zodResolver(expenseSchema),
    defaultValues: { description: '', amountMinor: 0, currency: 'USD', incurredOn: '' },
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} noValidate>
      <label htmlFor="expense-description">Description</label>
      <input id="expense-description" {...form.register('description')} />
      {form.formState.errors.description && (
        <p role="alert">{form.formState.errors.description.message}</p>
      )}

      <button type="submit" disabled={form.formState.isSubmitting}>
        Save expense
      </button>
    </form>
  );
}
```

- Use a native `<form>`, visible labels, appropriate `name`, `type`, `autocomplete`, and `inputMode` values.
- Bind server validation errors to fields where possible and display a form-level error for failures that are not field-specific.
- Disable duplicate submissions while a mutation is pending, but rely on server idempotency as the correctness mechanism.
- Keep monetary values in minor units in application state and API payloads. Format for display only.

## Routing And URL State

Use React Router for route ownership, route parameters, loaders where appropriate, and navigation. Make shareable views addressable and retain their filters in the URL.

```tsx
import { Link, useSearchParams } from 'react-router-dom';

export function InvoiceListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = searchParams.get('status') ?? 'all';
  const { data = [], isPending, isError } = useInvoiceList({ status });

  if (isPending) return <p role="status">Loading invoices...</p>;
  if (isError) return <p role="alert">Invoices could not be loaded.</p>;

  return (
    <main>
      <h1>Invoices</h1>
      <select
        aria-label="Invoice status"
        value={status}
        onChange={(event) => setSearchParams({ status: event.target.value })}
      >
        <option value="all">All invoices</option>
        <option value="draft">Draft</option>
        <option value="issued">Issued</option>
      </select>
      <ul>
        {data.map((invoice) => (
          <li key={invoice.id}>
            <Link to={`/invoices/${invoice.id}`}>{invoice.number}</Link>
          </li>
        ))}
      </ul>
    </main>
  );
}
```

- Use links for navigation and buttons for actions. Do not attach navigation behavior to generic containers.
- Validate route and search parameters before using them in API requests.
- Add route-level error boundaries and not-found screens. Do not leave the entire application blank when one page fails.

## Accessibility And UI

- Meet WCAG 2.2 AA as the baseline: keyboard operation, visible focus, sufficient contrast, semantic structure, and accessible error messages are required.
- Use native HTML controls before adding an ARIA role. Incorrect ARIA is worse than no ARIA.
- Associate each input with a `<label>`, and associate validation text with the field using `aria-describedby` when needed.
- Use `aria-live` or `role="status"` for asynchronous loading and success feedback, and `role="alert"` for errors requiring immediate announcement.
- Use a tested accessible primitive, such as Radix UI, for dialogs, menus, comboboxes, and similar complex interactions. Verify focus management in tests.
- Respect `prefers-reduced-motion`; do not communicate essential state through animation, color, or iconography alone.
- Design responsive layouts from narrow widths upward and test keyboard, touch, 200% zoom, and small-screen behavior.

## Error Handling And Observability

- Every page with a server dependency displays loading, empty, error, and success states.
- Provide a retry action for recoverable failures. Do not retry unsafe mutations automatically unless the idempotency contract is confirmed.
- Include the server request ID in support-facing error details when one is available, but do not show stack traces, internal URLs, or raw API errors to users.
- Emit structured frontend telemetry only through the approved observability integration. Include route, feature, request ID, correlation ID when available, and error category. Never include financial details, credentials, or raw form content.

```tsx
type QueryStateProps = {
  isLoading: boolean;
  isError: boolean;
  isEmpty: boolean;
  onRetry: () => void;
  children: React.ReactNode;
};

export function QueryState({ isLoading, isError, isEmpty, onRetry, children }: QueryStateProps) {
  if (isLoading) return <p role="status">Loading...</p>;

  if (isError) {
    return (
      <div role="alert">
        <p>We could not load this information.</p>
        <button type="button" onClick={onRetry}>Try again</button>
      </div>
    );
  }

  if (isEmpty) return <p>No records found.</p>;

  return <>{children}</>;
}
```

## Formatting And Financial Data

Keep business values in their native representation and format them at the presentation boundary.

```ts
export function formatMoney(amountMinor: number, currency: string, locale = navigator.language) {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
  }).format(amountMinor / 100);
}

export function formatAccountingDate(value: string, locale = navigator.language) {
  const date = new Date(`${value}T00:00:00`);
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(date);
}
```

- Do not use floating-point values for persisted or transmitted monetary amounts.
- Do not use `Date` for a date-only value before appending a local time component; otherwise timezone conversion can display the previous day.
- Make currency explicit for every amount. Never infer it from browser locale.
- Centralize number, money, and date formatting rather than duplicating formatting expressions in components.

## Styling

- Prefer component-scoped CSS or CSS Modules. Keep global CSS limited to tokens, resets, typography, and layout primitives.
- Use design tokens for color, spacing, typography, border radius, shadows, and responsive breakpoints. Do not repeat unexplained pixel values across components.
- Use meaningful class names based on component structure or state, not appearance alone.
- Do not use inline styles except for dynamic values that cannot be represented through classes or custom properties.
- Verify light, dark, high-contrast, focus, disabled, loading, error, and narrow-width states when they apply.

## Testing And Review Checklist

Use Vitest, React Testing Library, and MSW for frontend tests, as defined in `tech-stack.md`. Prefer tests that exercise user-observable behavior over implementation details.

```tsx
import { http, HttpResponse } from 'msw';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { server } from '../../../test/server';
import { InvoiceListPage } from './invoice-list-page';

it('retries after the invoice list request fails', async () => {
  let attempts = 0;
  server.use(
    http.get('/api/invoices', () => {
      attempts += 1;
      return attempts === 1
        ? HttpResponse.json({ detail: 'Temporary failure.' }, { status: 503 })
        : HttpResponse.json([{ id: 'inv_1', number: 'INV-001' }]);
    }),
  );

  const user = userEvent.setup();
  render(<InvoiceListPage />);

  await user.click(await screen.findByRole('button', { name: 'Try again' }));

  expect(await screen.findByRole('link', { name: 'INV-001' })).toBeVisible();
});
```

Before review, confirm:

- `npm run lint` and `npm run build` succeed.
- New behavior has focused tests for successful, loading, empty, failure, and relevant permission states.
- API usage goes through the Experience API client and uses a stable query key or mutation idempotency key.
- All new interactive controls work with keyboard and expose an accessible name.
- The UI handles eventual consistency after commands without misleading the user.
- No secrets, raw financial data in telemetry, unsafe HTML, `any`, or unreviewed type assertions were introduced.
