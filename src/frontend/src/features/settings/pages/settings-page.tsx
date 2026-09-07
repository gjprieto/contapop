import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { updateUserPreferences, type CurrentUser } from '../../auth/api/auth-api';
import { authKeys, useCurrentUser } from '../../auth/api/auth-queries';

const preferencesSchema = z.object({
  theme: z.enum(['light', 'dark']),
  language: z.enum(['es', 'en']),
  notificationsEnabled: z.boolean(),
});

type PreferencesFormValues = z.infer<typeof preferencesSchema>;

export function SettingsPage() {
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const form = useForm<PreferencesFormValues>({
    resolver: zodResolver(preferencesSchema),
    defaultValues: { theme: 'light', language: 'es', notificationsEnabled: true },
  });
  const savePreferences = useMutation({
    mutationFn: (values: PreferencesFormValues) =>
      updateUserPreferences({ ...values, version: currentUser.data?.version ?? 0 }),
    onSuccess: (preferences) => {
      queryClient.setQueryData(authKeys.currentUser, (user: CurrentUser | undefined) =>
        user ? { ...user, ...preferences } : user,
      );
      document.documentElement.dataset.theme = preferences.theme;
    },
  });

  useEffect(() => {
    if (currentUser.data) {
      const theme = currentUser.data.theme === 'dark' ? 'dark' : 'light';
      const language = currentUser.data.language === 'en' ? 'en' : 'es';
      form.reset({ theme, language, notificationsEnabled: currentUser.data.notificationsEnabled });
      document.documentElement.dataset.theme = theme;
    }
  }, [currentUser.data, form]);

  if (currentUser.isPending) return <main className="page"><p role="status">Loading your preferences...</p></main>;
  if (currentUser.isError || !currentUser.data) return <main className="page"><p role="alert">We could not load your preferences.</p></main>;

  return (
    <main className="page">
      <div className="page-head"><div><p className="eyebrow">Account</p><h1>Settings</h1><p className="page-intro">Choose how Contapop looks and communicates with you.</p></div></div>
      <section className="form-card" aria-labelledby="preferences-title">
        <h2 id="preferences-title">Preferences</h2>
        <form onSubmit={form.handleSubmit((values) => savePreferences.mutate(values))} noValidate>
          <div className="form-field"><label htmlFor="theme">Theme</label><select id="theme" {...form.register('theme')}><option value="light">Light</option><option value="dark">Dark</option></select></div>
          <div className="form-field"><label htmlFor="language">Language</label><select id="language" {...form.register('language')}><option value="es">Spanish</option><option value="en">English</option></select></div>
          <label className="checkbox-field" htmlFor="notifications-enabled"><input id="notifications-enabled" type="checkbox" {...form.register('notificationsEnabled')} /> Enable notifications</label>
          {savePreferences.isError && <p className="form-error" role="alert">We could not save your preferences. Please try again.</p>}
          {savePreferences.isSuccess && <p className="form-success" role="status">Preferences saved.</p>}
          <button className="btn btn-primary" type="submit" disabled={savePreferences.isPending}>{savePreferences.isPending ? 'Saving...' : 'Save preferences'}</button>
        </form>
      </section>
    </main>
  );
}
