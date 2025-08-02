import { getToolNames } from "./languages.ts";
import type { Language, Sample } from "./types.ts";
import { verifySample } from "./verifySample.ts";

async function reviewSample(inputs: {
    sample: Sample;
    model: string;
    language: Language;
}): Promise<Sample> {
    const { sample, language, model } = inputs;
    const res = await runPrompt(
        (ctx) => {
            const code = ctx.def("SAMPLE", sample.content);
            ctx.$`
  You are an expert ${language} developer and code reviewer.
  Your task is to review and update the sample code in ${code}
  to fix correctness issues if any and to follow best practices.
  
  Respond in ${language}. No yapping, no markdown, no code fences, no XML tags,
  no string delimiters wrapping it, no extra text, and no explanations.

  Make sure the sample is using token credential for authentication.
  Make sure there is no unused imports nor dead code.
  Make sure there is no type casting nor type assertions.
  Make sure the code is complete and self-contained.
  Make sure there is no usage of any deprecated or obsolete features of the ${language} language.
  Make sure the sample typechecks successfully.
  Make sure there is no dummy code or placeholder values in the sample.
  Make sure runtime errors are handled properly.
  `;
        },
        {
            model,
            tools: getToolNames(language),
        },
    );
    return {
        fileName: sample.fileName,
        language: sample.language,
        content: res.text,
    };
}

export async function reviewSamples(inputs: {
    samples: Sample[];
    model: string;
    skipReview: boolean;
    language: Language;
    clientDist?: string;
    pkgName?: string;
}): Promise<Sample[]> {
    const { samples, skipReview, language, model, clientDist, pkgName } =
        inputs;

    if (skipReview || language === "curl") {
        return samples;
    }

    const jobs = host.promiseQueue(Math.ceil(samples.length / 2));
    return jobs.mapAll(samples, async (sample) => {
        const reviewedSample = await reviewSample({
            sample,
            model,
            language,
        });
        return verifySample({
            model,
            sample: reviewedSample,
            clientDist,
            pkgName,
        });
    });
}
