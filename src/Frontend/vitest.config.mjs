import { defineConfig } from 'vitest/config';

export default defineConfig({
  esbuild: {
    jsx: 'automatic',
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    coverage: {
      exclude: [
        '**/*.d.ts',
        '**/.next/**',
        '**/next.config.mjs',
        '**/eslint.config.mjs',
        '**/vitest.config.mjs',
        '**/postcss.config.mjs',
        '**/tailwind.config.ts',
        '**/types/**',
        '**/app/layout.tsx',
        '**/app/page.tsx',
        '**/app/(store)/account/page.tsx',
        '**/app/(store)/cart/page.tsx',
        '**/app/login/page.tsx',
        '**/app/register/page.tsx',
        '**/app/swagger/page.tsx',
        '**/lib/api/swagger.ts',
        '**/lib/queries/swagger.ts',
        '**/components/providers/QueryProvider.tsx',
        '**/components/swagger/SwaggerExplorer.tsx',
      ],
      thresholds: {
        branches: 90,
      },
    },
  },
});
