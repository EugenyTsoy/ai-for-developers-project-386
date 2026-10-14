// Cross-platform orchestrator reproducing the full derived contract chain:
//
//   TypeSpec -> OpenAPI -> frontend types -> backend generated artifacts
//
// Invoked via `npm run generate:contract`. Fails fast on the first failing
// step (non-zero exit or spawn error). Deliberately does NOT restore pinned
// tools (`npm ci` / `dotnet tool restore`) — that is the caller's job, as it
// is in CI. Tool versions live only in package.json / .config/dotnet-tools.json.
//
// The canonical generated outputs and the directories used to scope the drift
// check are declared here once and shared with the drift checker, so the
// ownership list lives in a single place.

import { spawnSync } from 'node:child_process';
import { readFileSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const nodeRequire = createRequire(import.meta.url);
const ROOT = fileURLToPath(new URL('..', import.meta.url));

export const OPENAPI_FILE = 'contract/openapi/callbooking.openapi.yaml';
export const FRONTEND_TYPES_FILE = 'src/frontend/callbooking.web/src/api/generated/schema.d.ts';
export const BACKEND_CONTRACT_FILE = 'src/backend/CallBooking.Api/Generated/Contract.g.cs';

// Canonical generated outputs owned by this orchestrator (cleaned before
// regeneration). Order mirrors the pipeline.
export const GENERATED_FILES = [OPENAPI_FILE, FRONTEND_TYPES_FILE, BACKEND_CONTRACT_FILE];

// Directories used to scope the drift check. Wider than the exact files on
// purpose: they also surface untracked files a generator might leave behind.
export const GENERATED_DIRS = [
  'contract/openapi',
  'src/frontend/callbooking.web/src/api/generated',
  'src/backend/CallBooking.Api/Generated',
];

function run(label, command, args) {
  process.stdout.write(`\n[generate:contract] ${label}\n`);
  const result = spawnSync(command, args, { cwd: ROOT, stdio: 'inherit' });
  if (result.error) {
    process.stderr.write(`[generate:contract] cannot spawn ${command}: ${result.error.message}\n`);
    process.exit(1);
  }
  if (result.status !== 0) {
    process.stderr.write(`[generate:contract] step failed (exit ${result.status}): ${label}\n`);
    process.exit(result.status ?? 1);
  }
}

// Resolve a CLI shipped as a node_modules bin to its JS entrypoint so it can
// be executed with `node` directly — no shell, no platform shim (.cmd/.ps1).
function binEntry(packageName, binName) {
  const pkgJsonPath = nodeRequire.resolve(`${packageName}/package.json`);
  const pkgDir = dirname(pkgJsonPath);
  const pkg = JSON.parse(readFileSync(pkgJsonPath, 'utf8'));
  const entry = typeof pkg.bin === 'string'
    ? pkg.bin
    : binName
      ? pkg.bin[binName]
      : Object.values(pkg.bin)[0];
  return join(pkgDir, entry);
}

export function cleanGeneratedOutputs() {
  for (const relativePath of GENERATED_FILES) {
    rmSync(join(ROOT, relativePath), { force: true });
  }
}

export function generateContract() {
  cleanGeneratedOutputs();

  // 1. TypeSpec -> OpenAPI
  run('TypeSpec -> OpenAPI (tsp compile contract)', process.execPath, [
    binEntry('@typespec/compiler', 'tsp'),
    'compile',
    'contract',
  ]);

  // 2. OpenAPI -> frontend generated types
  run('OpenAPI -> frontend types (openapi-typescript)', process.execPath, [
    binEntry('openapi-typescript'),
    OPENAPI_FILE,
    '-o',
    FRONTEND_TYPES_FILE,
  ]);

  // 3. OpenAPI -> backend generated contract artifacts (NSwag)
  run('OpenAPI -> backend contract (nswag)', 'dotnet', [
    'tool',
    'run',
    'nswag',
    'run',
    'nswag.json',
  ]);

  process.stdout.write('\n[generate:contract] chain complete.\n');
}

const isDirectRun = process.argv[1]
  && import.meta.url === pathToFileURL(resolve(process.argv[1])).href;

if (isDirectRun) {
  generateContract();
}
