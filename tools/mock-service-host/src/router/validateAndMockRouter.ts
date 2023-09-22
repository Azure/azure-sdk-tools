import * as express from 'express'
import { validateRequestAndMockResponse } from '../controller/validateAndMockController'

export const validateAndMockRouter = express.Router()

validateAndMockRouter.all('/*', validateRequestAndMockResponse)
