import { Connection, createConnection } from 'typeorm';

import { CodeGeneration } from '../../src/types/codeGeneration';
import { CodeGenerationDao } from '../../src/utils/db/codeGenerationDao';
import { CodeGenerationDaoImpl } from '../../src/utils/db/codeGenerationDaoImpl';

let mongoDbConnection: Connection;

async function initDaoTest() {
    mongoDbConnection = await createConnection({
        name: 'mongodb',
        type: 'mongodb',
        host: '127.0.0.1',
        port: 27017,
        username: 'test',
        password: '123456',
        database: 'admin',
        synchronize: true,
        logging: true,
        entities: [CodeGeneration]
    });
}

beforeAll(async (done) => {
    await initDaoTest();
    done();
});

test('dao test submitCodeGeneration and getCodeGenerationByName1', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test1a';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = '';
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.owner = 'SDK';
    cg.type = 'ad-hoc';
    cg.status = 'submit';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);

    const retCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(retCg.service).toBe('msi');
    expect(retCg.serviceType).toBe('resource-manager');
    expect(retCg.tag).toBeNull();
    expect(retCg.sdk).toBe('javascript');
    expect(retCg.swaggerRepo).toBe('{"type": "github", "path":"https://github.com/azure"}');
    expect(retCg.sdkRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.codegenRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.owner).toBe('SDK');
    expect(retCg.type).toBe('ad-hoc');
    expect(retCg.status).toBe('submit');
});

test('dao test submitCodeGeneration and getCodeGenerationByName2', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test1b';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = '';
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.type = 'ad-hoc';
    cg.status = 'submit';
    cg.ignoreFailure = null;
    cg.stages = null;
    cg.swaggerPR = null;
    cg.codePR = null;
    cg.owner = null;

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);

    const retCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(retCg.service).toBe('msi');
    expect(retCg.serviceType).toBe('resource-manager');
    expect(retCg.tag).toBeNull();
    expect(retCg.sdk).toBe('javascript');
    expect(retCg.swaggerRepo).toBe('{"type": "github", "path":"https://github.com/azure"}');
    expect(retCg.sdkRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.codegenRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.owner).toBeNull();
    expect(retCg.type).toBe('ad-hoc');
    expect(retCg.status).toBe('submit');
    expect(retCg.ignoreFailure).toBeNull();
    expect(retCg.stages).toBeNull();
    expect(retCg.swaggerPR).toBeNull();
    expect(retCg.codePR).toBeNull();
});

test('dao test submitCodeGeneration and getCodeGenerationByName3', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test1c';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = '';
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.type = 'ad-hoc';
    cg.status = 'submit';
    cg.ignoreFailure = null;
    cg.stages = null;
    cg.swaggerPR = null;
    cg.codePR = null;
    cg.owner = null;

    const cg2: CodeGeneration = new CodeGeneration();
    cg2.name = 'test1c';
    cg2.service = 'msi';
    cg2.serviceType = 'resource-manager';
    cg2.resourcesToGenerate = '';
    cg2.tag = null;
    cg2.sdk = 'javascript';
    cg2.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg2.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.type = 'ad-hoc';
    cg2.status = 'submit';
    cg2.ignoreFailure = null;
    cg2.stages = null;
    cg2.swaggerPR = null;
    cg2.codePR = null;
    cg2.owner = null;

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);
    // submitCodeGeneration will cover the instance which has the same codegen name
    await codeGenerationDao.submitCodeGeneration(cg2);

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.name = null;

    // one codeGen can't submit twice, unless delete the first one
    // class-validator throw a array(not an error) and jest can't catch it, so use toBeTruthy
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toThrow();
    cg.name = 'test1c';
    // next cases check columns which can't be null
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.service = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.service = 'msi';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.serviceType = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.serviceType = 'resource-manager';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.sdk = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.sdk = 'javascript';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.swaggerRepo = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.sdkRepo = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.codegenRepo = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.type = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.type = 'ad-hoc';

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    cg.status = null;
    await expect(codeGenerationDao.submitCodeGeneration(cg)).rejects.toBeTruthy();
    cg.status = 'submit';
});

test('dao test submitCodeGeneration, updateCodeGenerationValueByName and getCodeGenerationByName', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test2';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = null;
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.owner = 'SDK';
    cg.type = 'ad-hoc';
    cg.status = 'submit';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);
    await codeGenerationDao.updateCodeGenerationValueByName(cg.name, 'owner', 'SWG');
    // check columns which can't be null
    await expect(codeGenerationDao.updateCodeGenerationValueByName(cg.name, 'status', null)).rejects.toBeTruthy();

    const retCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(retCg.service).toBe('msi');
    expect(retCg.serviceType).toBe('resource-manager');
    expect(retCg.tag).toBeNull();
    expect(retCg.sdk).toBe('javascript');
    expect(retCg.swaggerRepo).toBe('{"type": "github", "path":"https://github.com/azure"}');
    expect(retCg.sdkRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.codegenRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.owner).toBe('SWG');
    expect(retCg.type).toBe('ad-hoc');
    expect(retCg.status).toBe('submit');
});

test('dao test submitCodeGeneration, getCodeGenerationByName and deleteCodeGenerationByName', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test3';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = '';
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.owner = 'SDK';
    cg.type = 'ad-hoc';
    cg.status = 'submit';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);

    const retCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(retCg.service).toBe('msi');
    expect(retCg.serviceType).toBe('resource-manager');
    expect(retCg.tag).toBeNull();
    expect(retCg.sdk).toBe('javascript');
    expect(retCg.swaggerRepo).toBe('{"type": "github", "path":"https://github.com/azure"}');
    expect(retCg.sdkRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.codegenRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.owner).toBe('SDK');
    expect(retCg.type).toBe('ad-hoc');
    expect(retCg.status).toBe('submit');

    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    const reqCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(reqCg).toBe(undefined);
});

test('dao test submitCodeGeneration, updateCodeGenerationValuesByName and getCodeGenerationByName', async () => {
    const cg: CodeGeneration = new CodeGeneration();
    cg.name = 'test4';
    cg.service = 'msi';
    cg.serviceType = 'resource-manager';
    cg.resourcesToGenerate = '';
    cg.tag = null;
    cg.sdk = 'javascript';
    cg.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg.owner = 'SDK';
    cg.type = 'ad-hoc';
    cg.status = 'submit';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg.name);
    await codeGenerationDao.submitCodeGeneration(cg);
    await codeGenerationDao.updateCodeGenerationValuesByName(cg.name, { type: 'ad-real', status: 'del' });

    const retCg: CodeGeneration = await codeGenerationDao.getCodeGenerationByName(cg.name);
    expect(retCg.service).toBe('msi');
    expect(retCg.serviceType).toBe('resource-manager');
    expect(retCg.tag).toBeNull();
    expect(retCg.sdk).toBe('javascript');
    expect(retCg.swaggerRepo).toBe('{"type": "github", "path":"https://github.com/azure"}');
    expect(retCg.sdkRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.codegenRepo).toBe('{"type":"github", "path":"https://github.com/azure"}');
    expect(retCg.owner).toBe('SDK');
    expect(retCg.type).toBe('ad-real');
    expect(retCg.status).toBe('del');

    // check columns which can't be null
    await expect(codeGenerationDao.updateCodeGenerationValuesByName(cg.name, { status: null })).rejects.toBeTruthy();
});

test('dao test submitCodeGeneration and listCodeGenerationsByStatus', async () => {
    const cg1: CodeGeneration = new CodeGeneration();
    cg1.name = 'test5a';
    cg1.service = 'msi';
    cg1.serviceType = 'resource-manager';
    cg1.resourcesToGenerate = '';
    cg1.tag = null;
    cg1.sdk = 'javascript';
    cg1.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg1.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg1.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg1.owner = 'SDK';
    cg1.type = 'ad-hoc';
    cg1.status = 'completed';

    const cg2: CodeGeneration = new CodeGeneration();
    cg2.name = 'test5b';
    cg2.service = 'msi';
    cg2.serviceType = 'resource-manager';
    cg2.resourcesToGenerate = '';
    cg2.tag = null;
    cg2.sdk = 'javascript';
    cg2.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg2.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.owner = 'SDK';
    cg2.type = 'ad-hoc';
    cg2.status = 'submit';

    const cg3: CodeGeneration = new CodeGeneration();
    cg3.name = 'test5c';
    cg3.service = 'msi';
    cg3.serviceType = 'resource-manager';
    cg3.resourcesToGenerate = '';
    cg3.tag = null;
    cg3.sdk = 'javascript';
    cg3.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg3.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg3.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg3.owner = 'SDK';
    cg3.type = 'ad-hoc';
    cg3.status = 'completed';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg1.name);
    await codeGenerationDao.deleteCodeGenerationByName(cg2.name);
    await codeGenerationDao.deleteCodeGenerationByName(cg3.name);
    await codeGenerationDao.submitCodeGeneration(cg1);
    await codeGenerationDao.submitCodeGeneration(cg2);
    await codeGenerationDao.submitCodeGeneration(cg3);

    const retCgs: CodeGeneration[] = await codeGenerationDao.listCodeGenerationsByStatus('completed');
    for (const retCg of retCgs) {
        expect(retCg.status).toBe('completed');
    }

    const retCgs2: CodeGeneration[] = await codeGenerationDao.listCodeGenerationsByStatus(null);
    expect(retCgs2.length).toBe(0);
});

test('dao test submitCodeGeneration and listCodeGenerationsByStatus', async () => {
    const cg1: CodeGeneration = new CodeGeneration();
    cg1.name = 'test6a';
    cg1.service = 'msi';
    cg1.serviceType = 'resource-manager';
    cg1.resourcesToGenerate = '';
    cg1.tag = null;
    cg1.sdk = 'javascript';
    cg1.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg1.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg1.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg1.owner = 'SDK';
    cg1.type = 'ad-hoc';
    cg1.status = 'completed';

    const cg2: CodeGeneration = new CodeGeneration();
    cg2.name = 'test6b';
    cg2.service = 'msi';
    cg2.serviceType = 'resource-manager';
    cg2.resourcesToGenerate = '';
    cg2.tag = null;
    cg2.sdk = 'javascript';
    cg2.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg2.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg2.owner = 'SDK';
    cg2.type = 'ad-test';
    cg2.status = 'submit';

    const cg3: CodeGeneration = new CodeGeneration();
    cg3.name = 'test6c';
    cg3.service = 'msi';
    cg3.serviceType = 'resource-manager';
    cg3.resourcesToGenerate = '';
    cg3.tag = null;
    cg3.sdk = 'javascript';
    cg3.swaggerRepo = '{"type": "github", "path":"https://github.com/azure"}';
    cg3.sdkRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg3.codegenRepo = '{"type":"github", "path":"https://github.com/azure"}';
    cg3.owner = 'SDK';
    cg3.type = 'ad-test';
    cg3.status = 'pipelineCompleted';

    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName(cg1.name);
    await codeGenerationDao.deleteCodeGenerationByName(cg2.name);
    await codeGenerationDao.deleteCodeGenerationByName(cg3.name);
    await codeGenerationDao.submitCodeGeneration(cg1);
    await codeGenerationDao.submitCodeGeneration(cg2);
    await codeGenerationDao.submitCodeGeneration(cg3);

    const filters = { type: 'ad-test' };
    const retCgs1: CodeGeneration[] = await codeGenerationDao.listCodeGenerations(filters, true);
    for (const retCg of retCgs1) {
        expect(retCg.type).toBe('ad-test');
    }
    const retCgs2: CodeGeneration[] = await codeGenerationDao.listCodeGenerations(filters, false);
    for (const retCg of retCgs2) {
        expect(retCg.type).toBe('ad-test');
    }
});

test('dao test deleteCodeGenerationByName', async () => {
    const codeGenerationDao: CodeGenerationDao = new CodeGenerationDaoImpl(mongoDbConnection);
    await codeGenerationDao.deleteCodeGenerationByName('');
    await codeGenerationDao.deleteCodeGenerationByName(undefined);
});

async function destroyDaoTest() {
    await mongoDbConnection.close();
}

afterAll(async (done) => {
    await destroyDaoTest();
    done();
});
