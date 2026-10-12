import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { listConfigurationSettings } = vi.hoisted(() => ({ listConfigurationSettings: vi.fn() }));
vi.mock('@azure/app-configuration', () => ({
    AppConfigurationClient: vi.fn(function () { return { listConfigurationSettings }; })
}));
vi.mock('dotenv', () => ({ config: vi.fn() }));
vi.mock('@azure/identity', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@azure/identity')>();
    return {
        ...actual,
        ManagedIdentityCredential: vi.fn(function (options) { return new actual.ManagedIdentityCredential(options); }),
        AzureCliCredential: vi.fn(function () { return new actual.AzureCliCredential(); }),
        ChainedTokenCredential: vi.fn(function (...credentials) { return new actual.ChainedTokenCredential(...credentials); })
    };
});

import { AzureCliCredential, ChainedTokenCredential, ManagedIdentityCredential } from '@azure/identity';
import { initConfiguration } from '../src/services/AppConfig';

describe('App Configuration credential construction', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        vi.stubEnv('AZURE_APPCONFIG_ENDPOINT', 'https://test.azconfig.io');
        vi.stubEnv('AZURE_CLIENT_ID', '11111111-1111-1111-1111-111111111111');
        vi.spyOn(console, 'log').mockImplementation(() => {});
        listConfigurationSettings.mockImplementation(async function* () {});
    });

    afterEach(() => {
        vi.unstubAllEnvs();
        vi.restoreAllMocks();
    });

    it('constructs only managed identity followed by Azure CLI credentials', async () => {
        await expect(initConfiguration()).resolves.toBeUndefined();
        expect(ManagedIdentityCredential).toHaveBeenCalledWith(expect.objectContaining({ clientId: process.env.AZURE_CLIENT_ID }));
        expect(AzureCliCredential).toHaveBeenCalledOnce();
        const credentials = vi.mocked(ChainedTokenCredential).mock.calls[0];
        expect(credentials).toHaveLength(2);
        expect(credentials[0]).toBe(vi.mocked(ManagedIdentityCredential).mock.results[0].value);
        expect(credentials[1]).toBe(vi.mocked(AzureCliCredential).mock.results[0].value);
        expect(listConfigurationSettings).toHaveBeenCalledOnce();
    });

    it('allows the default managed identity when no client ID is configured', async () => {
        vi.stubEnv('AZURE_CLIENT_ID', undefined);
        await expect(initConfiguration()).resolves.toBeUndefined();
        expect(ManagedIdentityCredential).toHaveBeenCalledWith(expect.objectContaining({ clientId: undefined }));
        expect(vi.mocked(ChainedTokenCredential).mock.calls[0]).toHaveLength(2);
    });
});
