import * as _ from 'lodash';
import * as assert from 'assert';
import * as fs from 'fs';
import * as path from 'path';
import { Helper } from '../../src/util/helper';
import { exec } from 'child_process';

async function compare(dir1: string, dir2: string) {
    const cmd = 'diff -r --exclude=gen.zip --strip-trailing-cr -I _filePath -I x-ms-original-file -I file:/// ' + dir1 + ' ' + dir2;
    console.log(cmd);
    return await new Promise<boolean>((resolve, reject) => {
        exec(cmd, (error, stdout) => {
            if (error) {
                console.log('exec error: ' + error + ', ' + stdout);
                // Reject if there is an error:
                return reject(false);
            }
            // Otherwise resolve the promise:
            return resolve(true);
        });
    });
}

async function runAutorest(readmePath: string, extraOption: string[]) {
    const cmd =
        path.join(`${__dirname}`, '..', '..' + '/node_modules', '.bin', 'autorest') +
        ' --version=3.9.3 --use=' +
        path.join(`${__dirname}`, '..', '..') +
        ' ' +
        readmePath +
        ' ' +
        extraOption.join(' ');
    console.log(cmd);
    return await new Promise<boolean>((resolve, reject) => {
        exec(cmd, (error, stdout, stderr) => {
            if (error) {
                console.error('exec error: ' + stderr);
                // Reject if there is an error:
                return reject(false);
            }
            // Otherwise resolve the promise:
            console.log(stdout);
            return resolve(true);
        });
    });
}

async function runSingleTest(swaggerDir: string, rp: string, extraOption: string[], outputFolder: string, tempOutputFolder: string): Promise<boolean> {
    let result = true;
    let msg = '';
    const readmePath = path.join(swaggerDir, 'specification', rp, 'resource-manager/readme.md');
    await runAutorest(readmePath, extraOption)
        .then((res) => {
            if (!res) {
                msg = 'Run autorest not successfully!';
            }
            result = res;
        })
        .catch((e) => {
            msg = `Run autorest failed! ${e}`;
            result = false;
        });
    if (result) {
        await compare(outputFolder, tempOutputFolder)
            .then((res1) => {
                if (res1 === false) {
                    msg = 'The generated files have changed!';
                }
                result = res1;
            })
            .catch((e) => {
                msg = `The diff has some error ${e}`;
                result = false;
            });
    } else {
        console.error(msg);
    }
    return result;
}

const extraOptions: Record<string, string[]> = {
    signalr: ['--testmodeler.add-armtemplate-payload-string'],
};

describe('Run autorest and compare the output', () => {
    beforeAll(async () => {
        //
    });

    afterAll(async () => {
        //
    });

    it('', async () => {
        jest.setTimeout(8 * 60 * 60 * 1000);
        const swaggerDir = path.join(`${__dirname}`, '../../../../swagger');
        const outputDir = path.join(`${__dirname}`, 'output');
        const tempoutputDir = path.join(`${__dirname}`, 'tempoutput');

        let finalResult = true;
        const allTests: Array<Promise<boolean>> = [];
        for (const rp of ['appplatform', 'appplatform-remote', 'compute', 'compute-remote', 'signalr']) {
            console.log('Start Processing: ' + rp);

            // Remove tmpoutput
            const outputFolder = path.join(outputDir, rp, 'model');
            const tempOutputFolder = path.join(tempoutputDir, rp, 'model');
            Helper.deleteFolderRecursive(tempOutputFolder);
            fs.mkdirSync(tempOutputFolder, { recursive: true });

            const flags = [`--output-folder=${tempOutputFolder}`, '--debug', ..._.get(extraOptions, rp, [])];
            if (rp === 'signalr') {
                flags.push('--testmodeler.export-explicit-type');
            }
            const test = runSingleTest(swaggerDir, rp, flags, outputFolder, tempOutputFolder);
            allTests.push(test);
        }
        if ((process.env['PARALELL_TEST'] || 'false').toLowerCase() === 'true') {
            finalResult = (await Promise.all(allTests)).every((x) => x);
        } else {
            for (const test of allTests) {
                if (!(await test)) {
                    assert.fail('Test failed!');
                }
            }
        }

        assert.strictEqual(finalResult, true);
    });
});
