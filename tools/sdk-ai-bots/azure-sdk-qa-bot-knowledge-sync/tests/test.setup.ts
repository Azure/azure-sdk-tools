import * as fs from 'fs';
import * as path from 'path';
import { afterAll } from 'vitest';

// Global cleanup for any leftover temp directories created during tests.
// Patterns: tmp-*, cfg-*

function removeIfMatch(base: string, prefix: string) {
  if (!fs.existsSync(base)) return;
  for (const entry of fs.readdirSync(base)) {
    if (entry.startsWith(prefix)) {
      const full = path.join(base, entry);
      try {
        fs.rmSync(full, { recursive: true, force: true });
      } catch { /* ignore */ }
    }
  }
}

afterAll(() => {
  const cwd = process.cwd();
  removeIfMatch(cwd, 'tmp-');
  removeIfMatch(cwd, 'cfg-');
});
