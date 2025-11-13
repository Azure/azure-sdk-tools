import type { SampleIdea } from "./types.ts";

export async function selectSampleIdeas(inputs: {
    sampleIdeas: SampleIdea[];
    interactive: boolean;
    samplesCount: number;
}): Promise<SampleIdea[]> {
    const { sampleIdeas, interactive, samplesCount } = inputs;
    if (interactive) {
        const answer = await host.select("Select a sample idea:", [
            "all",
            "cancel",
            ...sampleIdeas.map(
                (sample: SampleIdea, i: number) =>
                    `${(i + 1).toString()}. ${sample.name}`,
            ),
        ]);
        const match = answer.match(/^(\d+)\./);
        const selectedIndex = match ? parseInt(match[1], 10) - 1 : -1;
        if (selectedIndex >= 0 && selectedIndex < sampleIdeas.length) {
            return [sampleIdeas[selectedIndex]];
        } else if (answer !== "all") {
            cancel("No sample idea selected.");
        }
    }
    return sampleIdeas.slice(0, samplesCount);
}
