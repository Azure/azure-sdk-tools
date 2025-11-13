import { generateSamples } from "./generateSamples.ts";
import { reviewSamples } from "./reviewSamples.ts";
import { saveSamples } from "./saveSamples.ts";
import type { SampleIdea, Language } from "./types.ts";

export async function run(inputs: {
    sampleIdeas: SampleIdea[];
    spec: WorkspaceFile[];
    language: Language;
    codingModel: string;
    reviewingModel: string;
    skipReview: boolean;
    samplesFolder: string;
    clientDist?: string;
    pkgName?: string;
}) {
    const {
        spec,
        codingModel,
        skipReview,
        language,
        reviewingModel,
        sampleIdeas,
        samplesFolder,
        clientDist,
        pkgName,
    } = inputs;

    const generatedSamples = await generateSamples({
        sampleIdeas,
        spec,
        language,
        model: codingModel,
        clientDist,
        pkgName,
    });

    await saveSamples({
        samples: generatedSamples,
        samplesFolder,
    });

    const reviewedSamples = await reviewSamples({
        samples: generatedSamples,
        language,
        model: reviewingModel,
        skipReview,
    });

    await saveSamples({
        samples: reviewedSamples,
        samplesFolder,
    });
}
