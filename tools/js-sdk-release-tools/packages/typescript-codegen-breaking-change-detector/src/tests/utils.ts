import { mkdirp, pathExists } from 'fs-extra';

export function getFormattedDate(): string {
  const today = new Date();

  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, '0');
  const day = String(today.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

export async function createTempFolder(tempFolderPrefix: string): Promise<string> {
  const maxRetry = 1000;
  let tempFolder = '';
  for (let i = 0; i < maxRetry; i++) {
    tempFolder = `${tempFolderPrefix}-${Math.round(Math.random() * 1000)}`;
    if (await pathExists(tempFolder)) continue;

    await mkdirp(tempFolder);
    return tempFolder;
  }
  throw new Error(`Failed to create temp folder at "${tempFolder}" for ${maxRetry} times`);
}
