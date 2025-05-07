import Tesseract from "tesseract.js";

export interface ImageInputProcessorOptions {
    languages: string[];
    numWorkers: number;
}

export class ImageInputProcessor {
    private readonly scheduler = Tesseract.createScheduler();

    public async init(
        options: ImageInputProcessorOptions
    ): Promise<ImageInputProcessor> {
        const workers = await Promise.all(
            new Array(options.numWorkers).fill(() =>
                Tesseract.createWorker(options.languages)
            )
        );
        workers.forEach((worker) => {
            this.scheduler.addWorker(worker);
        });

        return this;
    }

    public async recognize(imageUrl: string) {}

    private async terminate(): Promise<void> {
        await this.scheduler.terminate();
    }
}
