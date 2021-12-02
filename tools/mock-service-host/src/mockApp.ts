import bodyParser = require('body-parser')
import { Config } from './common/config'
import { Container } from 'inversify'
import { Coordinator } from './mid/coordinator'
import { InjectableTypes } from './lib/injectableTypes'
import { InversifyExpressServer } from 'inversify-express-utils'
import { ResponseGenerator } from './mid/responser'
import { SpecRetrievalMethod } from './common/environment'
import { SpecRetriever } from './lib/specRetriever'
import { SpecRetrieverFilesystem } from './lib/specRetrieverFilesystem'
import { SpecRetrieverGit } from './lib/specRetrieverGit'
import { config } from './common'
import { getHttpServer, getHttpsServer } from './webserver/httpServerConstructor'
import { logger } from './common/utils'

/*eslint-disable */
import './webserver/validateAndMockController'
import './webserver/authServerController'
import './webserver/metadataController'
/*eslint-enable */

class MockApp {
    private container: Container

    private buildContainer(): void {
        this.container = new Container()
        this.container.bind<Config>(InjectableTypes.Config).toConstantValue(config)
        this.container
            .bind<SpecRetriever>(InjectableTypes.SpecRetriever)
            .to(this.determineSpecRetriever(config.specRetrievalMethod))
            .inSingletonScope()
        this.container
            .bind<ResponseGenerator>(InjectableTypes.ResponseGenerator)
            .to(ResponseGenerator)
            .inSingletonScope()
        this.container
            .bind<Coordinator>(InjectableTypes.Coordinator)
            .to(Coordinator)
            .inSingletonScope()
    }

    private initializeCoordinator(): void {
        const validator = this.container.get<Coordinator>(InjectableTypes.Coordinator)
        validator.initialize()
    }

    private logResponseBody(req: any, res: any, next: any) {
        const oldWrite = res.write
        const oldEnd = res.end
        const chunks: any = []

        res.write = function (...chunk: any) {
            if (chunk && Buffer.isBuffer(chunk[0])) chunks.push(chunk[0])
            return oldWrite.apply(res, chunk)
        }

        res.end = function (...chunk: any) {
            if (chunk && Buffer.isBuffer(chunk[0])) chunks.push(chunk[0])
            let body = Buffer.concat(chunks).toString('utf8')
            try {
                body = JSON.stringify(JSON.parse(body), null, 4)
            } catch {
                // keep origin body value if is not JSON
            }
            logger.info(
                `[RESPONSE]\n  header: ${JSON.stringify(
                    res.getHeaders(),
                    null,
                    4
                )}\n  body: ${body}`
            )
            oldEnd.apply(res, chunk)
        }

        next()
    }

    private buildExpress(): void {
        const server = new InversifyExpressServer(this.container)

        server.setErrorConfig((app) => {
            app.use((err: Error) => {
                logger.error(`[Internal Error] ${err.stack}`)
            })
        })
        server.setConfig((app) => {
            app.use(
                bodyParser.json({
                    limit: '20mb',
                    inflate: true,
                    strict: false
                })
            )

            app.use(bodyParser.urlencoded({ extended: false }))
            app.use(bodyParser.json())
            app.use(this.logResponseBody)
        })
        const serverInstance = server.build()

        const httpsServer = getHttpsServer(serverInstance)
        httpsServer.listen(config.httpsPortStateful, () => {
            console.log(`Listening https on port: ${config.httpsPortStateful}`)
        })

        const httpServer = getHttpServer(serverInstance)
        httpServer.listen(config.httpPortStateless, () => {
            console.log(`Listening http on port: ${config.httpPortStateless}`)
        })

        const statelessServer = getHttpsServer(serverInstance)
        statelessServer.listen(config.httpsPortStateless, () => {
            console.log(`Listening https on port: ${config.httpsPortStateless}`)
        })

        const internalErrorServer = getHttpsServer(serverInstance)
        internalErrorServer.listen(config.internalErrorPort, () => {
            console.log(`Listening https on port: ${config.internalErrorPort}`)
        })
    }

    private determineSpecRetriever(method: string): any {
        switch (method) {
            case SpecRetrievalMethod.Filesystem:
                return SpecRetrieverFilesystem
            case SpecRetrievalMethod.Git:
                return SpecRetrieverGit
        }
    }

    public start(): void {
        this.buildContainer()
        this.buildExpress()
        this.initializeCoordinator()
    }
}

export default new MockApp()
