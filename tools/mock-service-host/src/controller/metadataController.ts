import * as express from 'express'
import { Coordinator } from '../mid/coordinator'
import { InjectableTypes } from '../lib/injectableTypes'
import { container } from '../container'
import { logger } from '../common/utils'

export function mockMetadataEndpoints(
    req: express.Request,
    res: express.Response,
    next: express.NextFunction
) {
    logger.info('fetching metadata')
    res.locals['result'] = {
        galleryEndpoint: '',
        graphEndpoint: 'https://graph.chinacloudapi.cn/',
        // "graphEndpoint": "https://localhost:8443",
        portalEndpoint: '',
        authentication: {
            // "loginEndpoint": "https://localhost:8443", // "https://login.chinacloudapi.cn/",
            loginEndpoint: 'https://login.chinacloudapi.cn/',
            audiences: [
                // "http://localhost:8081",
                'https://management.core.chinacloudapi.cn/',
                'https://management.chinacloudapi.cn/'
            ]
        }
    }
    next()
}

export function getstatus(req: express.Request, res: express.Response, next: express.NextFunction) {
    const coordinator = container.get<Coordinator>(InjectableTypes.Coordinator)
    res.locals['result'] = coordinator.ValidatorStatus
    next()
}

export function shutdown(req: express.Request, res: express.Response, next: express.NextFunction) {
    setTimeout(() => {
        process.exit()
    }, 1000)
    res.locals['result'] = 'shut down'
    next()
}
