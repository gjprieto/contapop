import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { ApiError } from '../../../shared/api/client';
import { login, type LoginInput } from '../api/auth-api';
import { authKeys } from '../api/auth-queries';

const loginSchema = z.object({
  email: z.string().trim().email('Enter a valid email address.'),
  password: z.string().min(1, 'Enter your password.'),
});

type LoginFormValues = z.infer<typeof loginSchema>;
type LoginLocationState = { from?: { pathname?: string } };

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });
  const loginMutation = useMutation({
    mutationFn: (values: LoginInput) => login(values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: authKeys.currentUser });
      const state = location.state as LoginLocationState | null;
      navigate(state?.from?.pathname ?? '/', { replace: true });
    },
  });

  const formError = loginMutation.error instanceof ApiError && loginMutation.error.status === 401
    ? 'The email address or password is incorrect.'
    : loginMutation.isError
      ? 'We could not sign you in. Please try again.'
      : undefined;

  return (
    <main className="login-page">
      <section className="login-panel" aria-labelledby="login-title">
        <div className="login-brand" aria-hidden="true">C</div>
        <p className="eyebrow">Contapop</p>
        <h1 id="login-title">Sign in to your books</h1>
        <p className="login-intro">Use the account supplied for your private pilot workspace.</p>
        <form onSubmit={form.handleSubmit((values) => loginMutation.mutate(values))} noValidate>
          <label htmlFor="email">Email address</label>
          <input id="email" type="email" autoComplete="email" {...form.register('email')} aria-describedby={form.formState.errors.email ? 'email-error' : undefined} />
          {form.formState.errors.email && <p id="email-error" className="form-error" role="alert">{form.formState.errors.email.message}</p>}

          <label htmlFor="password">Password</label>
          <input id="password" type="password" autoComplete="current-password" {...form.register('password')} aria-describedby={form.formState.errors.password ? 'password-error' : undefined} />
          {form.formState.errors.password && <p id="password-error" className="form-error" role="alert">{form.formState.errors.password.message}</p>}

          {formError && <p className="form-error form-error-global" role="alert">{formError}</p>}
          <button className="btn btn-primary btn-block" type="submit" disabled={loginMutation.isPending}>
            {loginMutation.isPending ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
