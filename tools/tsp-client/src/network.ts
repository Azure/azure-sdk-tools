import { createDefaultHttpClient, createPipelineRequest } from "@azure/core-rest-pipeline";

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

export function doesFileExist(url: string): Promise<boolean> {
  return fetch(url, "HEAD")
    .then(() => true)
    .catch(() => false);
}
