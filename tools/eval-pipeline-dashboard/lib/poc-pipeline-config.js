import { readFile } from "node:fs/promises";

function requiredString(value, field) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${field} must be a non-empty string.`);
  }
  return value.trim();
}

export function parsePocPipelineConfig(source, sourceName = "pipeline configuration") {
  if (source === null || typeof source !== "object" || Array.isArray(source)) {
    throw new Error(`${sourceName} must contain a JSON object.`);
  }

  const defaultAdoProject = requiredString(source.adoProject, `${sourceName}.adoProject`);
  if (source.sources === null || typeof source.sources !== "object" || Array.isArray(source.sources)) {
    throw new Error(`${sourceName}.sources must contain a JSON object.`);
  }

  const sources = new Map(
    Object.entries(source.sources).map(([name, path]) => [
      requiredString(name, `${sourceName}.sources key`),
      requiredString(path, `${sourceName}.sources.${name}`),
    ])
  );
  if (sources.size === 0) {
    throw new Error(`${sourceName}.sources must define at least one result source.`);
  }
  if (!Array.isArray(source.pipelines) || source.pipelines.length === 0) {
    throw new Error(`${sourceName}.pipelines must contain at least one pipeline.`);
  }

  const identities = new Set();
  const pipelines = source.pipelines.map((entry, index) => {
    const field = `${sourceName}.pipelines[${index}]`;
    if (entry === null || typeof entry !== "object" || Array.isArray(entry)) {
      throw new Error(`${field} must contain a JSON object.`);
    }

    const adoProject = entry.adoProject === undefined
      ? defaultAdoProject
      : requiredString(entry.adoProject, `${field}.adoProject`);
    const repository = requiredString(entry.repository, `${field}.repository`);
    const pipeline = requiredString(entry.pipeline, `${field}.pipeline`);
    const pipelineDefinitionId = requiredString(
      entry.pipelineDefinitionId,
      `${field}.pipelineDefinitionId`
    );
    if (!Array.isArray(entry.resultSources) || entry.resultSources.length === 0) {
      throw new Error(`${field}.resultSources must contain at least one source name.`);
    }

    const identity = `${adoProject}\u0000${pipelineDefinitionId}`;
    if (identities.has(identity)) {
      throw new Error(
        `${field} duplicates pipeline definition '${pipelineDefinitionId}' in '${adoProject}'.`
      );
    }
    identities.add(identity);

    const resultSources = entry.resultSources.map((value, sourceIndex) => {
      const name = requiredString(value, `${field}.resultSources[${sourceIndex}]`);
      const path = sources.get(name);
      if (!path) {
        throw new Error(`${field}.resultSources[${sourceIndex}] references unknown source '${name}'.`);
      }
      return path;
    });

    return { adoProject, repository, pipeline, pipelineDefinitionId, resultSources };
  });

  return { pipelines };
}

export async function loadPocPipelineConfig(configPath) {
  let text;
  try {
    text = await readFile(configPath, "utf8");
  } catch (error) {
    throw new Error(`Unable to read pipeline configuration '${configPath}'.`, { cause: error });
  }

  let source;
  try {
    source = JSON.parse(text);
  } catch (error) {
    throw new Error(`Invalid JSON in pipeline configuration '${configPath}'.`, { cause: error });
  }
  return parsePocPipelineConfig(source, configPath);
}