import fs from 'fs';
import path from 'path';

/**
 * Loads a test asset file and parses it as JSON
 * @param filename The name of the asset file
 * @returns The parsed JSON content
 */
export function loadTestAsset<T = any>(filename: string): T {
  const filePath = path.join(__dirname, '../assets', filename);
  const content = fs.readFileSync(filePath, 'utf-8');
  return JSON.parse(content) as T;
}

/**
 * Loads a Rust API input file and the corresponding expected output file
 * @param baseName The base name of the test files without extension
 * @returns Object containing input and expected output
 */
export function loadTestPair(baseName: string) {
  const input = loadTestAsset(`${baseName}.rust.json`);
  const expectedOutput = loadTestAsset(`${baseName}.json`);
  return { input, expectedOutput };
}
