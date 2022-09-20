import * as express from 'express'
import { Coordinator } from '../mid/coordinator'
import { InjectableTypes } from '../lib/injectableTypes'
import { container } from '../container'
import { logger } from '../common/utils'

export function mockMetadataEndpoints(req: express.Request, res: express.Response) {
    logger.info('fetching metadata')
    res.status(200).json({
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
    })
}

export function getstatus(req: express.Request, res: express.Response) {
    const coordinator = container.get<Coordinator>(InjectableTypes.Coordinator)
    res.status(200).json({ status: coordinator.ValidatorStatus })
}

export function shutdown(req: express.Request, res: express.Response) {
    setTimeout(() => {
        process.exit()
    }, 1000)
    res.status(200).json({ result: 'shut down' })
}
