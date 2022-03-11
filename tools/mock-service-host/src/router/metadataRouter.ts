import * as express from 'express'
import { getstatus, mockMetadataEndpoints, shutdown } from '../controller/metadataController'

export const metadataRouter = express.Router()

metadataRouter.get('/metadata/endpoints', mockMetadataEndpoints)
metadataRouter.get('/mock-service-host/status', getstatus)
metadataRouter.post('/mock-service-host/shutdown', shutdown)
