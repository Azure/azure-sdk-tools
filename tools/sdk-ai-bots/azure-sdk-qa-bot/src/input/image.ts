import Tesseract from "tesseract.js";

export class OCRPool {
    private scheduler = Tesseract.createScheduler();

    public static async create(
        languages: string[],
        numWorkers: number
    ): Promise<OCRPool> {
        const pool = new OCRPool();
        const workers = await Promise.all(
            new Array(numWorkers).fill(() => Tesseract.createWorker(languages))
        );
        workers.forEach((worker) => {
            pool.scheduler.addWorker(worker);
        });
        return pool;
    }

    public async terminate(): Promise<void> {
        await this.scheduler.terminate();
    }
}
