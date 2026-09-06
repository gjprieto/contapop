import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // The Experience API owns the browser-facing /experience route prefix.
      '/experience': {
        target: process.env.EXPERIENCE_API_HTTPS || process.env.EXPERIENCE_API_HTTP,
        changeOrigin: true
      }
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
  }
});
