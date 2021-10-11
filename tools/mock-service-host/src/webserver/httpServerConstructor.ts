import * as express from 'express'
import * as fs from 'fs'
import * as tls from 'tls'
import https = require('https')
import http = require('http')
import { logger } from '../common/utils'

export function getHttpsServer(app: any | express.Application) {
    const certs = {
        '127.0.0.1': {
            key: '.ssh/127-0-0-1-ca.pem',
            cert: '.ssh/127-0-0-1-ca.cer'
        },
        localhost: {
            key: '.ssh/localhost-ca.pem',
            cert: '.ssh/localhost-ca.crt'
        },
        'login.microsoftonline.com': {
            key: '.ssh/login-microsoftonline-com-ca.pem',
            cert: '.ssh/login-microsoftonline-com-ca.crt'
        }
    }
    const secureContexts = getSecureContexts(certs)
    const options = {
        // A function that will be called if the client supports SNI TLS extension.
        SNICallback: (servername: any, cb: any) => {
            const ctx = secureContexts[servername]

            if (!ctx) {
                logger.error('Not found SSL certificate for host: ' + servername)
            } else {
                logger.info('SSL certificate has been found and assigned to ' + servername)
            }

            if (cb) {
                cb(null, ctx)
            } else {
                return ctx
            }
        }
    }
    const httpsServer = https.createServer(options, app)
    return httpsServer
}

export function getHttpServer(app: any | express.Application) {
    return http.createServer(app)
}

function getSecureContexts(certs: any) {
    if (!certs || Object.keys(certs).length === 0) {
        throw new Error("Any certificate wasn't found.")
    }

    const certsToReturn: any = {}

    for (const serverName of Object.keys(certs)) {
        const appCert = certs[serverName]

        certsToReturn[serverName] = tls.createSecureContext({
            key: fs.readFileSync(appCert.key),
            cert: fs.readFileSync(appCert.cert)
        })
    }
    return certsToReturn
}
