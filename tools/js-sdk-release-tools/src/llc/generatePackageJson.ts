import * as fs from "fs";
import * as path from "path";
import {getPackageFolderName, getRelativePackagePath} from "./utils";

export function generatePackageJson(packagePath, packageName, sdkRepo) {
    let description = 'Sample description.';
    let metadata = {
        "constantPaths": [
            {
                "path": "swagger/README.md",
                "prefix": "package-version"
            }
        ]
    };
    let sampleConfiguration = {};
    if (fs.existsSync(path.join(packagePath, 'package.json'))) {
        const originalContent = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
        if (!!originalContent['description']) description = originalContent['description'];
        if (!!originalContent['//metadata']) metadata = originalContent['//metadata'];
        if (!!originalContent['//sampleConfiguration']) sampleConfiguration = originalContent['//sampleConfiguration'];
    }
    const content = {
        "name": packageName,
        "sdk-type": "client",
        "author": "Microsoft Corporation",
        "description": description,
        "version": "1.0.0-beta.1",
        "keywords": [
            "node",
            "azure",
            "cloud",
            "typescript",
            "browser",
            "isomorphic"
        ],
        "license": "MIT",
        "main": "./dist/index.js",
        "module": "./dist-esm/src/index.js",
        "types": `./types/${getPackageFolderName(packageName)}.d.ts`,
        "homepage": `https://github.com/Azure/azure-sdk-for-js/tree/main/${getRelativePackagePath(packagePath)}/README.md`,
        "repository": "github:Azure/azure-sdk-for-js",
        "bugs": {
            "url": "https://github.com/Azure/azure-sdk-for-js/issues"
        },
        "files": [
            "dist/",
            "dist-esm/src/",
            `types/${getPackageFolderName(packageName)}.d.ts`,
            "README.md",
            "LICENSE"
        ],
        "engines": {
            "node": ">=12.0.0"
        },
        "//metadata": metadata,
        "//sampleConfiguration": sampleConfiguration,
        "browser": {
            "./dist-esm/test/public/utils/env.js": "./dist-esm/test/public/utils/env.browser.js"
        },
        "scripts": {
            "audit": "node ../../../common/scripts/rush-audit.js && rimraf node_modules package-lock.json && npm i --package-lock-only 2>&1 && npm audit",
            "build:browser": "tsc -p . && cross-env ONLY_BROWSER=true rollup -c 2>&1",
            "build:node": "tsc -p . && cross-env ONLY_NODE=true rollup -c 2>&1",
            "build:samples": "echo Obsolete.",
            "build:test": "tsc -p . && rollup -c 2>&1",
            "build": "npm run clean && tsc -p . && rollup -c 2>&1 && mkdirp ./review && api-extractor run --local",
            "build:debug": "tsc -p . && rollup -c 2>&1 && api-extractor run --local",
            "check-format": "prettier --list-different --config ../../../.prettierrc.json --ignore-path ../../../.prettierignore \"src/**/*.ts\" \"test/**/*.ts\" \"samples-dev/**/*.ts\" \"*.{js,json}\"",
            "clean": "rimraf dist dist-browser dist-esm test-dist dist-test temp types *.tgz *.log",
            "execute:samples": "dev-tool samples run samples-dev",
            "extract-api": "rimraf review && mkdirp ./review && api-extractor run --local",
            "format": "prettier --write --config ../../../.prettierrc.json --ignore-path ../../../.prettierignore \"src/**/*.ts\" \"test/**/*.ts\" \"samples-dev/**/*.ts\" \"*.{js,json}\"",
            "generate:client": "autorest --typescript swagger/README.md && npm run format",
            "integration-test:browser": "karma start --single-run",
            "integration-test:node": "nyc mocha -r esm --require source-map-support/register --reporter ../../../common/tools/mocha-multi-reporter.js --timeout 5000000 --full-trace \"dist-esm/test/{,!(browser)/**/}*.spec.js\"",
            "integration-test": "npm run integration-test:node && npm run integration-test:browser",
            "lint:fix": "eslint package.json api-extractor.json src test --ext .ts --fix --fix-type [problem,suggestion]",
            "lint": "eslint package.json api-extractor.json src test --ext .ts",
            "pack": "npm pack 2>&1",
            "test:browser": "npm run clean && npm run build:test && npm run unit-test:browser",
            "test:node": "npm run clean && npm run build:test && npm run unit-test:node",
            "test": "npm run clean && npm run build:test && npm run unit-test",
            "unit-test:browser": "cross-env karma start --single-run",
            "unit-test:node": "cross-env mocha -r esm --require ts-node/register --reporter ../../../common/tools/mocha-multi-reporter.js --timeout 1200000 --full-trace \"test/{,!(browser)/**/}*.spec.ts\"",
            "unit-test": "npm run unit-test:node && npm run unit-test:browser",
            "docs": "typedoc --excludePrivate --excludeExternals  --out ./dist/docs ./src",
            "browser": {
                "./dist-esm/test/public/utils/env.js": "./dist-esm/test/public/utils/env.browser.js"
            }
        },
        "sideEffects": false,
        "autoPublish": false,
        "dependencies": {
            "@azure/core-auth": "^1.3.0",
            "@azure-rest/core-client": "1.0.0-beta.7",
            "@azure/core-rest-pipeline": "^1.1.0",
            "@azure/logger": "^1.0.0",
            "tslib": "^2.2.0"
        },
        "devDependencies": {
            "@azure/dev-tool": "^1.0.0",
            "@azure/eslint-plugin-azure-sdk": "^3.0.0",
            "@azure/identity": "^2.0.1",
            "@azure-tools/test-recorder": "^1.0.0",
            "@microsoft/api-extractor": "^7.18.11",
            "@types/chai": "^4.1.6",
            "@types/mocha": "^7.0.2",
            "@types/node": "^12.0.0",
            "chai": "^4.2.0",
            "cross-env": "^7.0.2",
            "dotenv": "^8.2.0",
            "eslint": "^7.15.0",
            "karma-chrome-launcher": "^3.0.0",
            "karma-coverage": "^2.0.0",
            "karma-edge-launcher": "^0.4.2",
            "karma-env-preprocessor": "^0.1.1",
            "karma-firefox-launcher": "^1.1.0",
            "karma-ie-launcher": "^1.0.0",
            "karma-json-preprocessor": "^0.3.3",
            "karma-json-to-file-reporter": "^1.0.1",
            "karma-junit-reporter": "^2.0.1",
            "karma-mocha-reporter": "^2.2.5",
            "karma-mocha": "^2.0.1",
            "karma-source-map-support": "~1.4.0",
            "karma-sourcemap-loader": "^0.3.8",
            "karma": "^6.2.0",
            "mkdirp": "^1.0.4",
            "mocha-junit-reporter": "^1.18.0",
            "mocha": "^7.1.1",
            "nyc": "^14.0.0",
            "prettier": "2.2.1",
            "rimraf": "^3.0.0",
            "rollup": "^1.16.3",
            "source-map-support": "^0.5.9",
            "typedoc": "0.15.2",
            "typescript": "~4.2.0"
        }
    };
    const keyVaultAdminPackageJson = JSON.parse(fs.readFileSync(path.join(sdkRepo, 'sdk', 'keyvault', 'keyvault-admin', 'package.json'), {encoding: 'utf-8'}));
    if (fs.existsSync(path.join(packagePath, 'src', 'paginateHelper.ts'))) {
        const paginateHelper = fs.readFileSync(path.join(packagePath, 'src', 'paginateHelper.ts'));
        if (paginateHelper.includes('@azure/core-paging')) {
            content['dependencies']['@azure/core-paging'] = keyVaultAdminPackageJson['dependencies']['@azure/core-paging'];
        }
        if (paginateHelper.includes('@azure-rest/core-client-paging')) {
            if (packageName !== '@azure-rest/agrifood-farming') {
                const agrifoodPackageJson = JSON.parse(fs.readFileSync(path.join(sdkRepo, 'sdk', 'agrifood', 'agrifood-farming-rest', 'package.json'), {encoding: 'utf-8'}));
                content['dependencies']['@azure-rest/core-client-paging'] = agrifoodPackageJson['dependencies']['@azure-rest/core-client-paging'];
            } else {
                content['dependencies']['@azure-rest/core-client-paging'] = '1.0.0-beta.1';
            }
        }
    }
    if (fs.existsSync(path.join(packagePath, 'src', 'pollingHelper.ts'))) {
        content['dependencies']['@azure/core-lro'] = keyVaultAdminPackageJson['dependencies']['@azure/core-lro'];
    }
    const readme = fs.readFileSync(path.join(packagePath, 'swagger', 'README.md'), {encoding: 'utf-8'});
    const match = /package-version: "*([0-9a-z-.]+)/.exec(readme);
    if (!!match && match.length === 2) {
        content.version = match[1];
    }
    fs.writeFileSync(path.join(packagePath, 'package.json'), JSON.stringify(content, undefined, '  '));
}
