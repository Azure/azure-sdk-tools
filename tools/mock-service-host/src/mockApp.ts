import { Coordinator } from './mid/coordinator'
import { InjectableTypes } from './lib/injectableTypes'
import { authServerRouter } from './router/authServerRouter'
import { buildContainer, container } from './container'
import { config } from './common'
import { getHttpServer, getHttpsServer } from './controller/httpServerConstructor'
import { logger } from './common/utils'
import { metadataRouter } from './router/metadataRouter'
import { validateAndMockRouter } from './router/validateAndMockRouter'
import express from 'express'

class MockApp {
    private initializeCoordinator(): void {
        const validator = container.get<Coordinator>(InjectableTypes.Coordinator)
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
        const app = express()

        app.use(
            express.json({
                limit: '20mb',
                inflate: true,
                strict: false
            })
        )
        app.use(express.urlencoded({ extended: false }))
        app.use(express.json())
        app.use(this.logResponseBody)

        app.use(authServerRouter)
        app.use(metadataRouter)
        app.use(validateAndMockRouter)

        app.use((err: Error) => {
            logger.error(`[Internal Error] ${err.stack}`)
        })

        const httpsServer = getHttpsServer(app)
        httpsServer.listen(config.httpsPortStateful, () => {
            console.log(`Listening https on port: ${config.httpsPortStateful}`)
        })

        const httpServer = getHttpServer(app)
        httpServer.listen(config.httpPortStateless, () => {
            console.log(`Listening http on port: ${config.httpPortStateless}`)
        })

        const statelessServer = getHttpsServer(app)
        statelessServer.listen(config.httpsPortStateless, () => {
            console.log(`Listening https on port: ${config.httpsPortStateless}`)
        })

        const internalErrorServer = getHttpsServer(app)
        internalErrorServer.listen(config.internalErrorPort, () => {
            console.log(`Listening https on port: ${config.internalErrorPort}`)
        })
    }

    public start(): void {
        buildContainer()
        this.buildExpress()
        this.initializeCoordinator()
    }
}

export default new MockApp()
