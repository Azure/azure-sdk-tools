import * as express from 'express'
import {
    mockFingerPrint,
    mockGetSubscriptions,
    mockGraphService,
    mockLogin,
    mockProviderService
} from '../controller/authServerController'

export const authServerRouter = express.Router()

authServerRouter.get('/common/.well-known/openid-configuration', mockFingerPrint)
authServerRouter.all('/*/oauth2/token', mockLogin)
authServerRouter.all('/*/oauth2/v2.0/token', mockLogin)
authServerRouter.all('/subscriptions', mockGetSubscriptions)
authServerRouter.all('/*/servicePrincipals', mockGraphService)
authServerRouter.all('/subscriptions/*/providers', mockProviderService)
