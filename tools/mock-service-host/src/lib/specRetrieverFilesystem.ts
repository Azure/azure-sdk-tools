import { BaseSpecRetriever, SpecRetriever } from './specRetriever'
import { Config } from '../common/config'
import { InjectableTypes } from './injectableTypes'
import { inject, injectable } from 'inversify'
import { logger } from '../common/utils'

/**
 * Copies specifications from a specified location on the filesystem and caches them locally.
 */
@injectable()
export class SpecRetrieverFilesystem extends BaseSpecRetriever implements SpecRetriever {
    constructor(@inject(InjectableTypes.Config) config: Config) {
        super(config)
    }

    protected async retrieveSpecsImpl(): Promise<void> {
        logger.info(
            `SpecRetrieverFilesystem: Set spec cache path as ${this.config.specRetrievalLocalRelativePath}`
        )
        this.localPath = this.config.specRetrievalLocalRelativePath
    }
}
