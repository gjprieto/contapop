import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { updateUserProfile, type CurrentUser } from '../../auth/api/auth-api';
import { authKeys, useCurrentUser } from '../../auth/api/auth-queries';

const profileSchema = z.object({
  name: z.string().trim().min(1, 'Enter your name.').max(200, 'Your name must be 200 characters or fewer.'),
});

type ProfileFormValues = z.infer<typeof profileSchema>;

export function UserPage() {
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { name: '' },
  });
  const saveProfile = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateUserProfile({ ...values, version: currentUser.data?.version ?? 0 }),
    onSuccess: (profile) => {
      queryClient.setQueryData(authKeys.currentUser, (user: CurrentUser | undefined) =>
        user ? { ...user, ...profile } : user,
      );
    },
  });

  useEffect(() => {
    if (currentUser.data) form.reset({ name: currentUser.data.name });
  }, [currentUser.data, form]);

  if (currentUser.isPending) return <main className="page"><p role="status">Loading your profile...</p></main>;
  if (currentUser.isError || !currentUser.data) return <main className="page"><p role="alert">We could not load your profile.</p></main>;

  return (
    <main className="page">
      <div className="page-head"><div><p className="eyebrow">Account</p><h1>Your profile</h1><p className="page-intro">Keep the name shown throughout your workspace up to date.</p></div></div>
      <section className="form-card" aria-labelledby="profile-details-title">
        <h2 id="profile-details-title">Profile details</h2>
        <form onSubmit={form.handleSubmit((values) => saveProfile.mutate(values))} noValidate>
          <div className="form-field">
            <label htmlFor="profile-name">Name</label>
            <input id="profile-name" autoComplete="name" {...form.register('name')} aria-describedby={form.formState.errors.name ? 'profile-name-error' : undefined} />
            {form.formState.errors.name && <p id="profile-name-error" className="form-error" role="alert">{form.formState.errors.name.message}</p>}
          </div>
          <div className="form-field"><label htmlFor="profile-email">Email address</label><input id="profile-email" value={currentUser.data.email} readOnly /></div>
          {saveProfile.isError && <p className="form-error" role="alert">We could not save your profile. Please try again.</p>}
          {saveProfile.isSuccess && <p className="form-success" role="status">Profile saved.</p>}
          <button className="btn btn-primary" type="submit" disabled={saveProfile.isPending}>{saveProfile.isPending ? 'Saving...' : 'Save profile'}</button>
        </form>
      </section>
    </main>
  );
}
