import { mkdirp, pathExists } from 'fs-extra';
import { Project, ScriptTarget } from 'ts-morph';

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

export function createTestAstContext(baselineApiView: string, currentApiView: string) {
  const project = new Project({
    compilerOptions: { target: ScriptTarget.ES2022 },
  });
  const baseline = project.createSourceFile('review/baseline/index.ts', baselineApiView);
  const current = project.createSourceFile('review/current/index.ts', currentApiView);
  return { baseline, current };
}
