import dotenv from 'dotenv';
import { join } from 'path';

export function applyDotEnv() {
  const envPath = join(import.meta.dirname, `../../env/.env.${process.env.NODE_ENV}`);
  dotenv.config({ path: envPath });
}
