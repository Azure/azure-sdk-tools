import Tesseract from "tesseract.js";

export interface ImageInputProcessorOptions {
    languages: string[];
    numWorkers: number;
}

export class ImageInputProcessor {
    // TODO: reuse workwers in entire server
    private readonly scheduler = Tesseract.createScheduler();
    private readonly options: ImageInputProcessorOptions;

    constructor(options: ImageInputProcessorOptions) {
        this.options = options;
    }

    public async init(): Promise<ImageInputProcessor> {
        const tasks = new Array(this.options.numWorkers).fill(0).map(() => {
            return Tesseract.createWorker(this.options.languages);
        });
        const workers = await Promise.all(tasks);
        workers.forEach((worker) => {
            this.scheduler.addWorker(worker);
        });

        return this;
    }

    public async recognize(imageUrls: string[]) {
        const tasks = imageUrls.map((url) => {
            return this.scheduler.addJob("recognize", url);
        });
        const results = await Promise.all(tasks);
        await this.scheduler.terminate();
        return results.map((r) => r.data);
    }

    private async terminate(): Promise<void> {
        await this.scheduler.terminate();
    }
}
