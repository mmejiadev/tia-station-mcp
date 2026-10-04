import tailwind from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

/**
 * Where the harness API listens.
 *
 * @remarks
 * Loopback and the port `apiServer.ts` defaults to. The dev server proxies `/api` there so the
 * browser only ever talks to one origin: no cross-origin request in development, and the built
 * dashboard served from anywhere still asks for `/api` relative to wherever it was served from.
 */
const ApiOrigin = 'http://127.0.0.1:4317';

/**
 * Where the web platform listens (platform/src/server/serveCli.ts).
 *
 * @remarks
 * Its paths are listed before `/api`, because the first matching entry wins: sign-in and the
 * person's own data go to the platform, everything else under `/api` to the harness. The sign-in
 * providers return to `/api/auth/callback/...` on this page's origin, which is why the platform is
 * reached through here rather than on its own port.
 */
const PlatformOrigin = 'http://127.0.0.1:4318';

const PlatformPaths = ['/api/auth', '/api/me', '/api/identities'];

export default defineConfig({
  plugins: [react(), tailwind()],
  resolve: {
    // shadcn's components import each other through this alias; it is their convention, not ours.
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  server: {
    // The dev server is bound to loopback for the same reason the API is: what is on the other side
    // of this proxy is an audit trail of everything the server has changed.
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      ...Object.fromEntries(PlatformPaths.map((path) => [path, { target: PlatformOrigin, changeOrigin: false }])),
      '/api': { target: ApiOrigin, changeOrigin: false }
    }
  }
});
