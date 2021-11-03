import 'dotenv/config'

import { Config } from './config'
import { Environment } from './environment'
import { configSchema } from './configSchema'
import { environmentConfigDevelopment } from './environmentConfigDevelopment'
import { environmentConfigProduction } from './environmentConfigProduction'
import { environmentConfigTest } from './environmentConfigTest'

const env = configSchema.get('serviceEnvironment') as Environment

const environmentOverrides: any = {
    [Environment.Production]: environmentConfigProduction,
    [Environment.Test]: environmentConfigTest,
    [Environment.Development]: environmentConfigDevelopment
}

// Load environment dependent configuration
configSchema.load(environmentOverrides[env])

// Perform validation
configSchema.validate({ allowed: 'strict' })

export const config: Config = configSchema.getProperties()
