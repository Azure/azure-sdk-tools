import * as express from 'express'
import {
    BaseHttpController,
    HttpResponseMessage,
    StringContent,
    all,
    controller,
    httpGet,
    httpPost
} from 'inversify-express-utils'
import { Config } from '../common/config'
import { Coordinator } from '../mid/coordinator'
import { HttpStatusCode, OperationalError, createErrorBody } from '../common/errors'
import { InjectableTypes } from '../lib/injectableTypes'
import { VirtualServerRequest, VirtualServerResponse } from '../mid/models'
import { config } from '../common'
import { inject } from 'inversify'
import { isNullOrUndefined, logger } from '../common/utils'

@controller('/')
export class ValidateAndMockController extends BaseHttpController {
    constructor(
        @inject(InjectableTypes.Coordinator) protected coordinator: Coordinator,
        @inject(InjectableTypes.Config) private config: Config
    ) {
        super()
    }

    private profiles: Record<string, any> = {
        httpsPortStateful: {
            stateful: true
        },

        internalErrorPort: {
            alwaysError: 500
        }
    }

    private getProfileByRequest(req: VirtualServerRequest): Record<string, any> {
        const host = req.headers?.host
        if (isNullOrUndefined(host)) return {}
        const arr = (host as string).split(':')
        let port = this.config.httpPortStateless.toString()
        if (arr.length > 1) {
            port = arr[1]
        } else if (!isNullOrUndefined(req.localPort)) {
            port = req.localPort.toString()
        }
        if (port === config.httpsPortStateful.toString()) return this.profiles['httpsPortStateful']
        else if (port === config.internalErrorPort.toString())
            return this.profiles['internalErrorPort']
        return {}
    }

    private createRequest(req: express.Request): VirtualServerRequest {
        return {
            query: req.query,
            url: req.url,
            protocol: req.protocol,
            originalUrl: req.originalUrl,
            method: req.method,
            headers: req.headers,
            body: req.body,
            localPort: req.socket.localPort
        } as VirtualServerRequest
    }

    private createDefaultResponse(): VirtualServerResponse {
        return new VirtualServerResponse(
            HttpStatusCode.INTERNAL_SERVER.toString(),
            createErrorBody(HttpStatusCode.INTERNAL_SERVER, 'Default Response')
        )
    }

    @httpGet('mock-service-host/status')
    public async getstatus() {
        const response = new HttpResponseMessage(200)
        response.content = new StringContent(this.coordinator.ValidatorStatus)
        return response
    }

    @httpPost('mock-service-host/shutdown')
    public async shutdown(req: express.Request, res: express.Response) {
        setTimeout(() => {
            process.exit()
        }, 1000)
        res.status(200).json('shut down')
    }

    @all('*')
    public async validateRequestAndMockResponse(
        req: express.Request,
        res: express.Response,
        next: express.NextFunction
    ) {
        logger.info(
            `[HITTING] ${req.method} ${req.originalUrl}\n  with header: ${JSON.stringify(
                req.headers,
                null,
                4
            )}\n  with body: ${JSON.stringify(req.body, null, 4)}`
        )
        res.on('finish', () => {
            logger.info(`[RESPONSE] code: ${res.statusCode}`)
        })

        const response = this.createDefaultResponse()

        try {
            const virtualServerRequest = this.createRequest(req)
            await this.coordinator.generateResponse(
                virtualServerRequest,
                response,
                this.getProfileByRequest(virtualServerRequest)
            )

            for (const name in response.headers) {
                res.setHeader(name, response.headers[name])
            }
            if (req.headers?.accept?.startsWith('text/')) {
                return res
                    .status(parseInt(response.statusCode))
                    .contentType(req.headers?.accept)
                    .send(response.body)
            } else {
                return res.status(parseInt(response.statusCode)).json(response.body)
            }
        } catch (err) {
            if (err instanceof OperationalError) {
                res.status(err.httpCode).json(err.ToAzureResponse())
            } else {
                res.status(HttpStatusCode.INTERNAL_SERVER).json(
                    createErrorBody(HttpStatusCode.INTERNAL_SERVER, err)
                )
                next(err)
            }
        }
    }
}
