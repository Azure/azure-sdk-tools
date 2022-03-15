import { Config } from './common/config'
import { Container } from 'inversify'
import { Coordinator } from './mid/coordinator'
import { InjectableTypes } from './lib/injectableTypes'
import { ResponseGenerator } from './mid/responser'
import { SpecRetrievalMethod } from './common/environment'
import { SpecRetriever } from './lib/specRetriever'
import { SpecRetrieverFilesystem } from './lib/specRetrieverFilesystem'
import { SpecRetrieverGit } from './lib/specRetrieverGit'
import { config } from './common'

const container = new Container()

function determineSpecRetriever(method: string): any {
    switch (method) {
        case SpecRetrievalMethod.Filesystem:
            return SpecRetrieverFilesystem
        case SpecRetrievalMethod.Git:
            return SpecRetrieverGit
    }
}

function buildContainer() {
    container.bind<Config>(InjectableTypes.Config).toConstantValue(config)
    container
        .bind<SpecRetriever>(InjectableTypes.SpecRetriever)
        .to(determineSpecRetriever(config.specRetrievalMethod))
        .inSingletonScope()
    container
        .bind<ResponseGenerator>(InjectableTypes.ResponseGenerator)
        .to(ResponseGenerator)
        .inSingletonScope()
    container.bind<Coordinator>(InjectableTypes.Coordinator).to(Coordinator).inSingletonScope()
}

export { container, buildContainer }
