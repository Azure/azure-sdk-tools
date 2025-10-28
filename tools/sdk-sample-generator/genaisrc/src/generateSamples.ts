import { getFileExtension, getToolNames } from "./languages.ts";
import { sampleGuidelines } from "./sampleGuidelines.ts";
import { verifySample } from "./verifySample.ts";
import type { Language, SampleIdea, Sample } from "./types.ts";

function generateCurlSample(sampleIdea: SampleIdea): Sample {
    let curlCmds: string[] = [];

    for (const request of sampleIdea.requests) {
        const fullUrl = `"\${RESOURCE_URI%/}/${request.path.replace(/^\/+/, "")}"`;

        let curlCmd = `curl -i -X ${request.method.toUpperCase()} ${fullUrl}`;

        let hasAuthOrKeyHeader = false;
        if (request.headers) {
            for (const h of request.headers) {
                const nameLower = h.name.toLowerCase();
                if (nameLower === "authorization") {
                    hasAuthOrKeyHeader = true;
                    curlCmd += ` -H "${h.name}: Bearer \${AUTHORIZATION}"`;
                } else if (nameLower.includes("key")) {
                    hasAuthOrKeyHeader = true;
                    curlCmd += ` -H "${h.name}: \${AUTHORIZATION}"`;
                } else {
                    curlCmd += ` -H "${h.name}: ${h.value}"`;
                }
            }
        }

        if (!hasAuthOrKeyHeader) {
            curlCmd += ` -H "Authorization: \${AUTHORIZATION}"`;
        }

        if (request.queryParams && request.queryParams.length > 0) {
            const params = request.queryParams
                .map(
                    (q) =>
                        `${encodeURIComponent(q.name)}=${encodeURIComponent(q.value)}`,
                )
                .join("&");
            curlCmd = curlCmd.replace(
                /"([^"]+)"/,
                (_, url) => `"${url}?${params}"`,
            );
        }

        if (request?.body) {
            const renderedBody =
                typeof request?.body === "object"
                    ? JSON.stringify(request?.body)
                    : request.body;
            curlCmd += ` -d '${renderedBody}'`;
        }
        curlCmds.push(curlCmd);
    }

    return {
        fileName: `${sampleIdea.fileName}.${getFileExtension("curl")}`,
        content: curlCmds.join("\n\n"),
        language: "curl",
        executable: true,
    };
}

async function generateSampleCode(inputs: {
    spec: WorkspaceFile[];
    model: string;
    language: Language;
    sampleIdea: SampleIdea;
}): Promise<Sample> {
    const { spec, model, language, sampleIdea } = inputs;

    if (language === "curl") {
        return generateCurlSample(sampleIdea);
    }

    const res = await runPrompt(
        async (ctx) => {
            const clientApi = ctx.def("FILE", spec);
            const sampleIdeaStr = ctx.def("SAMPLE", JSON.stringify(sampleIdea));
            ctx.$`
  You are an expert ${language} developer and code reviewer.

  Your task is to generate idiomatic, easy-to-follow ${language} sample for
  the scenario described in ${sampleIdeaStr} using the API specification in ${clientApi}.
  
  Respond in ${language}. No yapping, no markdown, no code fences, no XML tags,
  no string delimiters wrapping it, no extra text, and no explanations.
  
  ${sampleGuidelines(language)}
  `;
        },
        {
            model,
            tools: getToolNames(language),
        },
    );
    return {
        fileName: `${sampleIdea.fileName}.${getFileExtension(language)}`,
        content: res.text,
        language,
    };
}

export async function generateSamples(inputs: {
    spec: WorkspaceFile[];
    model: string;
    language: Language;
    clientDist?: string;
    pkgName?: string;
    sampleIdeas: SampleIdea[];
}): Promise<Sample[]> {
    const { sampleIdeas, language, model, spec, clientDist, pkgName } = inputs;

    const jobs = host.promiseQueue(Math.ceil(sampleIdeas.length / 2));
    return jobs.mapAll(sampleIdeas, async (sampleIdea) => {
        const sample = await generateSampleCode({
            sampleIdea,
            spec,
            model,
            language,
        });
        const verifiedSample = await verifySample({
            sample,
            model,
            clientDist,
            pkgName,
        });
        return verifiedSample;
    });
}
