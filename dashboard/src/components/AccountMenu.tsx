import { LogIn, LogOut, UserRound } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { authClient, Providers } from '../auth/authClient.ts';

/**
 * Who is signed in, and the way in or out.
 *
 * @remarks
 * Signing in changes who the page says you are and nothing else: the dashboard still has no endpoint
 * that changes a project, and signing in does not give it one.
 *
 * When the platform is not running the session cannot be asked for; that is said in words rather
 * than shown as signed out, because "signed out" would send somebody to sign in to a server that is
 * not there.
 */
export function AccountMenu(): ReactNode {
  const { data, isPending, error } = authClient.useSession();

  if (isPending) {
    return <span className="text-muted-foreground text-xs">Checking sign-in…</span>;
  }

  if (error !== null) {
    return (
      <span className="text-muted-foreground text-xs" title="Start it with npm run serve in platform/.">
        Sign-in unavailable: the platform is not running
      </span>
    );
  }

  return data === null ? <SignInButtons /> : <SignedIn name={data.user.name} image={data.user.image ?? null} />;
}

function SignInButtons(): ReactNode {
  const [failure, setFailure] = useState('');

  const signIn = async (provider: (typeof Providers)[number]['id']): Promise<void> => {
    const result = await authClient.signIn.social({ provider, callbackURL: window.location.href });

    setFailure(result.error?.message ?? '');
  };

  return (
    <div className="flex items-center gap-2">
      {Providers.map((provider) => (
        <Button key={provider.id} variant="outline" size="sm" onClick={() => void signIn(provider.id)}>
          <LogIn className="size-4" aria-hidden="true" />
          Sign in with {provider.label}
        </Button>
      ))}
      {failure.length > 0 ? (
        <span role="alert" className="text-xs text-[var(--status-critical)]">
          {failure}
        </span>
      ) : undefined}
    </div>
  );
}

function SignedIn({ name, image }: { name: string; image: string | null }): ReactNode {
  return (
    <div className="flex items-center gap-2">
      <span className="flex items-center gap-2 rounded-md border px-3 py-1.5 text-xs">
        {image === null ? (
          <UserRound className="size-4" aria-hidden="true" />
        ) : (
          <img src={image} alt="" className="size-5 rounded-full" referrerPolicy="no-referrer" />
        )}
        {name}
      </span>
      <Button variant="outline" size="sm" onClick={() => void authClient.signOut()}>
        <LogOut className="size-4" aria-hidden="true" />
        Sign out
      </Button>
    </div>
  );
}
