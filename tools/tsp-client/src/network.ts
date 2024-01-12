import { stat } from "fs/promises";

export function isValidUrl(url: string) {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}

// Checks if a file exists locally
export function doesFileExist(path: string): Promise<boolean> {
  return stat(path).then(() => true).catch(() => false);
}
