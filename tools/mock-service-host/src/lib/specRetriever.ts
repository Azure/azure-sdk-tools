import * as fs from 'fs-extra'
import * as path from 'path'
import { Config } from '../common/config'
import { injectable } from 'inversify'

/**
 * An interface for retrieving specifications.
 */
export interface SpecRetriever {
    /**
     * Retrieve specs and cache them locally.
     * @returns true if the specs have changed since last time they were retrieved.
     */
    retrieveSpecs(): Promise<void>

    /**
     * The full local path where specs are locally cached.
     */
    localPath: string
}

/**
 * Common functionality for spec retrievers.
 */
@injectable()
export abstract class BaseSpecRetriever {
    constructor(protected config: Config) {}

    private localPathValue: string

    public get localPath(): string {
        return (
            this.localPathValue ||
            (this.localPathValue = path.resolve(
                process.cwd(),
                this.config.specRetrievalLocalRelativePath
            ))
        )
    }

    public set localPath(specCachePath: string) {
        this.localPathValue = specCachePath || this.localPathValue
    }

    public async retrieveSpecs(): Promise<void> {
        await this.prepareLocalPath()
        await this.retrieveSpecsImpl()
    }

    /**
     * Determines the full local path for caching specifications and makes sure it exists and is empty.
     */
    protected async prepareLocalPath(): Promise<void> {
        await fs.ensureDir(this.localPath)
    }

    /**
     * Method for concrete implementations to perform retrieval logic.
     */
    protected abstract retrieveSpecsImpl(): Promise<void>
}
