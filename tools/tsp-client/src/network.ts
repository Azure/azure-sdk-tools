import { createDefaultHttpClient, createPipelineRequest } from "@azure/core-rest-pipeline";
import { stat } from "fs/promises";

const httpClient = createDefaultHttpClient();

export async function fetch(url: string, method: "GET" | "HEAD" = "GET"): Promise<string> {
  const result = await httpClient.sendRequest(createPipelineRequest({ url, method }));
  if (result.status !== 200) {
    throw new Error(`failed to fetch ${url}: ${result.status}`);
  }
  return String(result.bodyAsText);
}

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
