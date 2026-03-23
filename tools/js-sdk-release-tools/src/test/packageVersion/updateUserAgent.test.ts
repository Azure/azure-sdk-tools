import { describe, expect, test, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { SDKType, ModularSDKType } from '../../common/types.js';

vi.mock('../../common/utils.js', () => ({
    getSDKType: vi.fn(),
}));
vi.mock('../../utils/generateInputUtils.js', () => ({
    getModularSDKType: vi.fn(),
}));
vi.mock('../../xlc/apiVersion/apiVersionTypeExtractor.js', () => ({
    isModelOnly: vi.fn(),
}));
vi.mock('../../utils/logger.js', () => ({
    logger: {
        info: vi.fn(),
        warn: vi.fn(),
    },
}));

import { updateUserAgent } from '../../xlc/codeUpdate/updateUserAgent.js';
import { getSDKType } from '../../common/utils.js';
import { getModularSDKType } from '../../utils/generateInputUtils.js';
import { isModelOnly } from '../../xlc/apiVersion/apiVersionTypeExtractor.js';
import { logger } from '../../utils/logger.js';

describe('updateUserAgent', () => {
    let tmpDir: string;

    beforeEach(() => {
        vi.resetAllMocks();
        tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'updateUserAgent-test-'));
    });

    afterEach(() => {
        fs.rmSync(tmpDir, { recursive: true, force: true });
    });

    function setupPackageJson(name: string) {
        fs.writeFileSync(
            path.join(tmpDir, 'package.json'),
            JSON.stringify({ name }),
        );
    }

    function setupSrcFile(relativePath: string, content: string) {
        const fullPath = path.join(tmpDir, relativePath);
        fs.mkdirSync(path.dirname(fullPath), { recursive: true });
        fs.writeFileSync(fullPath, content, 'utf8');
    }

    function readSrcFile(relativePath: string): string {
        return fs.readFileSync(path.join(tmpDir, relativePath), 'utf8');
    }

    describe('HighLevelClient', () => {
        test('updates packageDetails in src .ts files', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/testClient.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0-beta.1`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/testClient.ts')).toBe(
                'const packageDetails = `azsdk-js-arm-test/2.0.0`;');
        });

        test('does not update non-.ts files', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/testClient.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
            setupSrcFile('src/readme.md',
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/testClient.ts')).toContain('2.0.0');
            expect(readSrcFile('src/readme.md')).toContain('1.0.0');
        });

        test('updates multiple .ts files', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/a.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
            setupSrcFile('src/b.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/a.ts')).toContain('2.0.0');
            expect(readSrcFile('src/b.ts')).toContain('2.0.0');
        });

        test('handles @azure/ prefix removal for package name', async () => {
            setupPackageJson('@azure/arm-compute');
            setupSrcFile('src/client.ts',
                'const packageDetails = `azsdk-js-arm-compute/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '3.0.0');

            expect(readSrcFile('src/client.ts')).toBe(
                'const packageDetails = `azsdk-js-arm-compute/3.0.0`;');
        });
    });

    describe('RestLevelClient', () => {
        test('updates userAgentInfo in src .ts files', async () => {
            setupPackageJson('@azure-rest/test');
            setupSrcFile('src/testClient.ts',
                'const userAgentInfo = `azsdk-js-test-rest/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.RestLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/testClient.ts')).toBe(
                'const userAgentInfo = `azsdk-js-test-rest/2.0.0`;');
        });

        test('transforms @azure-rest/ prefix to -rest suffix in package name', async () => {
            setupPackageJson('@azure-rest/my-service');
            setupSrcFile('src/client.ts',
                'const userAgentInfo = `azsdk-js-my-service-rest/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.RestLevelClient);

            await updateUserAgent(tmpDir, '1.0.1');

            expect(readSrcFile('src/client.ts')).toBe(
                'const userAgentInfo = `azsdk-js-my-service-rest/1.0.1`;');
        });

        test('does not update non-.ts files', async () => {
            setupPackageJson('@azure-rest/test');
            setupSrcFile('src/client.ts',
                'const userAgentInfo = `azsdk-js-test-rest/1.0.0`;');
            setupSrcFile('src/other.js',
                'const userAgentInfo = `azsdk-js-test-rest/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.RestLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/client.ts')).toContain('2.0.0');
            expect(readSrcFile('src/other.js')).toContain('1.0.0');
        });
    });

    describe('ModularClient - ManagementPlane', () => {
        test('updates userAgentInfo in src/api/*Context.ts files', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/api/testContext.ts',
                'const userAgentInfo = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.ManagementPlane);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/api/testContext.ts')).toBe(
                'const userAgentInfo = `azsdk-js-arm-test/2.0.0`;');
        });

        test('does not call isModelOnly for ManagementPlane', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/api/testContext.ts',
                'const userAgentInfo = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.ManagementPlane);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(isModelOnly).not.toHaveBeenCalled();
        });

        test('only updates files ending with Context.ts', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/api/testContext.ts',
                'const userAgentInfo = `azsdk-js-arm-test/1.0.0`;');
            setupSrcFile('src/api/helpers.ts',
                'const userAgentInfo = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.ManagementPlane);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/api/testContext.ts')).toContain('2.0.0');
            expect(readSrcFile('src/api/helpers.ts')).toContain('1.0.0');
        });
    });

    describe('ModularClient - DataPlane', () => {
        test('updates userAgentInfo when not model-only', async () => {
            setupPackageJson('@azure/my-service');
            setupSrcFile('src/api/myServiceContext.ts',
                'const userAgentInfo = `azsdk-js-my-service/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.DataPlane);
            vi.mocked(isModelOnly).mockResolvedValue(false);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(isModelOnly).toHaveBeenCalledWith(tmpDir);
            expect(readSrcFile('src/api/myServiceContext.ts')).toBe(
                'const userAgentInfo = `azsdk-js-my-service/2.0.0`;');
        });

        test('skips update when model-only', async () => {
            setupPackageJson('@azure/my-service');
            setupSrcFile('src/api/myServiceContext.ts',
                'const userAgentInfo = `azsdk-js-my-service/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.DataPlane);
            vi.mocked(isModelOnly).mockResolvedValue(true);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(isModelOnly).toHaveBeenCalledWith(tmpDir);
            // File should remain unchanged
            expect(readSrcFile('src/api/myServiceContext.ts')).toBe(
                'const userAgentInfo = `azsdk-js-my-service/1.0.0`;');
        });

        test('logs info message when skipping model-only package', async () => {
            setupPackageJson('@azure/my-service');
            setupSrcFile('src/api/myServiceContext.ts',
                'const userAgentInfo = `azsdk-js-my-service/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.ModularClient);
            vi.mocked(getModularSDKType).mockReturnValue(ModularSDKType.DataPlane);
            vi.mocked(isModelOnly).mockResolvedValue(true);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(logger.info).toHaveBeenCalledWith(
                expect.stringContaining('model-only package'));
        });
    });

    describe('Package name handling', () => {
        test('package name without known prefix uses name as-is', async () => {
            setupPackageJson('my-custom-package');
            setupSrcFile('src/client.ts',
                'const packageDetails = `azsdk-js-my-custom-package/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(readSrcFile('src/client.ts')).toBe(
                'const packageDetails = `azsdk-js-my-custom-package/2.0.0`;');
        });
    });

    describe('Unsupported SDK type', () => {
        test('logs warning for unsupported SDK type', async () => {
            setupPackageJson('@azure/some-package');
            vi.mocked(getSDKType).mockReturnValue('UnknownType' as SDKType);

            await updateUserAgent(tmpDir, '2.0.0');

            expect(logger.warn).toHaveBeenCalledWith(
                expect.stringContaining('Unsupported SDK type'));
        });
    });

    describe('Version formats', () => {
        test('replaces beta version with stable version', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/client.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0-beta.1`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '1.0.0');

            expect(readSrcFile('src/client.ts')).toBe(
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
        });

        test('replaces stable version with beta version', async () => {
            setupPackageJson('@azure/arm-test');
            setupSrcFile('src/client.ts',
                'const packageDetails = `azsdk-js-arm-test/1.0.0`;');
            vi.mocked(getSDKType).mockReturnValue(SDKType.HighLevelClient);

            await updateUserAgent(tmpDir, '2.0.0-beta.1');

            expect(readSrcFile('src/client.ts')).toBe(
                'const packageDetails = `azsdk-js-arm-test/2.0.0-beta.1`;');
        });
    });
});
