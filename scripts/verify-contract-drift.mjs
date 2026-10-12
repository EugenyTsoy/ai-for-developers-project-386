// Regenerates the full contract chain and then checks that the canonical
// generated paths are clean in git. Detects modified, deleted and untracked
// generated files (so `git diff --exit-code` alone would not suffice).
//
// Scoped to the generated directories only, so unrelated working-tree changes
// do not produce false positives.

import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { generateContract, GENERATED_DIRS } from './generate-contract.mjs';

const ROOT = fileURLToPath(new URL('..', import.meta.url));

function gitStatusPorcelain() {
  const result = spawnSync('git', ['status', '--porcelain', '--', ...GENERATED_DIRS], {
    cwd: ROOT,
    encoding: 'utf8',
  });
  if (result.error) {
    process.stderr.write(`[verify:contract-drift] cannot run git: ${result.error.message}\n`);
    process.exit(1);
  }
  if (result.status !== 0) {
    process.stderr.write(`[verify:contract-drift] git status failed (exit ${result.status}).\n`);
    process.exit(result.status ?? 1);
  }
  return result.stdout;
}

function main() {
  process.stdout.write('[verify:contract-drift] regenerating the full chain.\n');
  generateContract();

  const dirty = gitStatusPorcelain().trim();
  if (dirty === '') {
    process.stdout.write('[verify:contract-drift] OK: generated contract paths are clean.\n');
    process.exit(0);
  }

  process.stderr.write(
    `[verify:contract-drift] DRIFT DETECTED in generated contract paths:\n${dirty}\n`
    + 'Run `npm run generate:contract` and commit the regenerated artifacts.\n',
  );
  process.exit(1);
}

const isDirectRun = process.argv[1]
  && import.meta.url === pathToFileURL(resolve(process.argv[1])).href;

if (isDirectRun) {
  main();
}