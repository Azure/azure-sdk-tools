#!/usr/bin/env node

import { processDailySyncKnowledge } from './DailySyncKnowledge';
import { initConfiguration } from './services/AppConfig';
import { initSecrets } from './services/AppSecret';

/**
 * Main entry point for standalone knowledge sync
 */
async function main(): Promise<void> {
    try {
        console.log('Initialize app configuration');
        await initConfiguration();
        console.log('Initialize app secrets');
        await initSecrets();
        console.log('Starting Azure SDK Knowledge Sync (Standalone)');
        await processDailySyncKnowledge();
        console.log('Azure SDK Knowledge Sync completed successfully');
    } catch (error) {
        console.error('Azure SDK Knowledge Sync failed', error);
        process.exit(1);
    }
}

// Run if called directly
if (require.main === module) {
    main().catch(error => {
        console.error('Unhandled error in knowledge sync:', error);
        process.exit(1);
    });
}

export { main as syncKnowledge };

