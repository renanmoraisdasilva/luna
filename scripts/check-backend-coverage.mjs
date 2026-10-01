#!/usr/bin/env node
// Evaluates the merged backend branch-coverage gate.
//
// The threshold was previously asserted in scripts/test-coverage.sh and never evaluated by CI, so no
// coverage number existed anywhere in the repository. This script is the single place the backend gate is
// decided, and it is called both by CI and by scripts/test-coverage.sh so the two cannot disagree.
//
// Usage: node scripts/check-backend-coverage.mjs <path-to-Cobertura.xml>
//
// The gate sits just below the measured value so it fails on a regression rather than on the current
// backlog. See documentation/roadmap.md, "Recorded quality gate".

import { readFileSync, appendFileSync } from 'node:fs';

const [coberturaPath] = process.argv.slice(2);

if (!coberturaPath) {
  console.error('Usage: node scripts/check-backend-coverage.mjs <path-to-Cobertura.xml>');
  process.exit(1);
}

const minimum = Number(process.env.BACKEND_BRANCH_MINIMUM ?? '0.80');

let branchRate = Number.NaN;
try {
  const match = readFileSync(coberturaPath, 'utf8').match(/branch-rate="([0-9.]+)"/);
  branchRate = match ? Number(match[1]) : Number.NaN;
} catch (error) {
  console.error(`Could not read the merged coverage report at ${coberturaPath}: ${error.message}`);
  process.exit(1);
}

const measured = Number.isFinite(branchRate) ? `${(branchRate * 100).toFixed(2)}%` : 'unavailable';
const summary = `## Backend coverage\n\nBranch coverage: ${measured} (gate: ${(minimum * 100).toFixed(0)}%)\n`;

if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
}

if (!Number.isFinite(branchRate) || branchRate < minimum) {
  console.error(
    `Backend branch coverage must be at least ${(minimum * 100).toFixed(0)}%, measured ${measured}.`
  );
  process.exit(1);
}

console.log(summary.trim());
