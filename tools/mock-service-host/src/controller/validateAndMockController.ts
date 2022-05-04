import * as express from 'express'
import { Config } from '../common/config'
import { Coordinator } from '../mid/coordinator'
import { HttpStatusCode, OperationalError, createErrorBody } from '../common/errors'
import { InjectableTypes } from '../lib/injectableTypes'
import { VirtualServerRequest, VirtualServerResponse } from '../mid/models'
import { container } from '../container'
import { isNullOrUndefined, logger } from '../common/utils'

const profiles: Record<string, any> = {
    httpsPortStateful: {
        stateful: true
    },

    internalErrorPort: {
        alwaysError: 500
    }
}

function getProfileByRequest(req: VirtualServerRequest): Record<string, any> {
    const config = container.get<Config>(InjectableTypes.Config)
    const host = req.headers?.host
    if (isNullOrUndefined(host)) return {}
    const arr = (host as string).split(':')
    let port = config.httpPortStateless.toString()
    if (arr.length > 1) {
        port = arr[1]
    } else if (!isNullOrUndefined(req.localPort)) {
        port = req.localPort.toString()
    }
    if (port === config.httpsPortStateful.toString()) return profiles['httpsPortStateful']
    else if (port === config.internalErrorPort.toString()) return profiles['internalErrorPort']
    return {}
}

function createRequest(req: express.Request): VirtualServerRequest {
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

function createDefaultResponse(): VirtualServerResponse {
    return new VirtualServerResponse(
        HttpStatusCode.INTERNAL_SERVER.toString(),
        createErrorBody(HttpStatusCode.INTERNAL_SERVER, 'Default Response')
    )
}

export async function validateRequestAndMockResponse(
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

    const coordinator = container.get<Coordinator>(InjectableTypes.Coordinator)
    const response = createDefaultResponse()

    try {
        const virtualServerRequest = createRequest(req)
        await coordinator.generateResponse(
            virtualServerRequest,
            response,
            getProfileByRequest(virtualServerRequest)
        )

        for (const name in response.headers) {
            res.setHeader(name, response.headers[name])
        }
        if (req.headers?.accept?.startsWith('text/')) {
            res.status(parseInt(response.statusCode))
                .contentType(req.headers?.accept)
                .send(response.body)
        } else {
            res.status(parseInt(response.statusCode)).json(response.body)
        }
    } catch (err) {
        if (res.locals['result']) {
            res.status(200).json(res.locals['result'])
        } else {
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
