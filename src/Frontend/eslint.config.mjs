import nextConfig from "eslint-config-next";

const config = [
  {
    ignores: ["coverage/**", ".next/**", "node_modules/**"],
  },
  ...nextConfig,
];

export default config;
