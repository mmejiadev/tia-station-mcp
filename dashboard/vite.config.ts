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
      '/api': { target: ApiOrigin, changeOrigin: false }
    }
  }
});
