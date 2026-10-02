import nextConfig from "eslint-config-next";

// Generated coverage output is not source. Vitest writes it into the project directory when `npm test` runs
// with coverage enabled, so without this the linter reports warnings inside generated files that no one can
// meaningfully act on.
const config = [
  {
    ignores: ["coverage/**", ".next/**", "node_modules/**"],
  },
  ...nextConfig,
];

export default config;
