import * as chile_process from 'child_process'
import * as express from 'express'
import * as fs from 'fs'
import * as path from 'path'
import * as process from 'process'
import * as tls from 'tls'
import https = require('https')
import http = require('http')
import { logger } from '../common/utils'

export function getHttpsServer(app: any | express.Application) {
    const certFolder = path.join(__dirname, '..', '..', '..', '.ssh')
    if (
        !fs.existsSync(path.join(certFolder, '127-0-0-1-ca.pem')) ||
        !fs.existsSync(path.join(certFolder, 'localhost-ca.pem'))
    ) {
        if (process.platform === 'win32') {
            chile_process.execSync(`${__dirname}\\..\\..\\..\\script\\renew-ssh.bat`)
        } else {
            chile_process.execSync(`bash ${__dirname}/../../../script/renew-ssh.sh`)
        }
    }
    const certs = {
        '127.0.0.1': {
            key: path.join(certFolder, '127-0-0-1-ca.pem'),
            cert: path.join(certFolder, '127-0-0-1-ca.crt')
        },
        localhost: {
            key: path.join(certFolder, 'localhost-ca.pem'),
            cert: path.join(certFolder, 'localhost-ca.crt')
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
