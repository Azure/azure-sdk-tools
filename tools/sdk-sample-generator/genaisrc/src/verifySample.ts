import { getTypechecker, getToolNames } from "./languages.ts";
import { getTypecheckingPrompt } from "./sampleGuidelines.ts";
import type { Sample } from "./types.ts";

export async function verifySample(inputs: {
    model: string;
    clientDist?: string;
    pkgName?: string;
    sample: Sample;
}): Promise<Sample> {
    const { model, sample, clientDist, pkgName } = inputs;
    const tc = getTypechecker(sample.language);
    const tools = getToolNames(sample.language);
    let content = sample.content;
    let attempt = 0;
    const maxAttempts = 5;
    while (true) {
        const tcRes = await tc({
            code: content,
            clientDist,
            pkgName,
        });
        if (tcRes.succeeded || attempt++ >= maxAttempts) {
            return { ...sample, content };
        }
        const promptRes = await runPrompt(
            (ctx) => {
                const code = ctx.def("SAMPLE", content);
                ctx.$`
    You are an expert ${sample.language} developer and code reviewer.
    Your task is to fix the following issues in the sample code in ${code}:
    ${tcRes.output}.
  
    Respond in ${sample.language}. No yapping, no markdown, no code fences, no XML tags,
    no string delimiters wrapping it, no extra text, and no explanations.
  
    ${getTypecheckingPrompt(sample.language)}`;
            },
            {
                model,
                tools,
            },
        );
        content = promptRes.text;
    }
}
