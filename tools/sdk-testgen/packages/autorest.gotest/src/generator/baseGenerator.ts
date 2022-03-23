/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as nunjucks from 'nunjucks';
import * as path from 'path';
import { GenerateContext } from './generateContext';
export abstract class BaseDataRender {
    public constructor(public context: GenerateContext) {}

    abstract renderData(): void;
}

export abstract class BaseCodeGenerator {
    public constructor(public context: GenerateContext) {}

    abstract generateCode(extraParam: Record<string, unknown>): void;

    protected renderAndWrite(model: any, templateFileName: string, outputFileName: string, extraParam: Record<string, unknown> = {}, jsFunc: Record<string, unknown> = {}) {
        const tmplPath = path.relative(process.cwd(), path.join(`${__dirname}`, `../../src/template/${templateFileName}`));

        const output = this.render(
            tmplPath,
            {
                ...model,
                config: this.context.testConfig.config,
                ...extraParam,
                imports: this.context.importManager.text(),
                packageName: this.context.packageName,
            },
            jsFunc,
        );
        this.writeToHost(outputFileName, output);
    }

    private writeToHost(fileName: string, output: string) {
        this.context.host.writeFile({
            filename: fileName,
            content: output,
        });
    }

    private render(templatePath: string, data: any, jsFunc: Record<string, unknown>): string {
        nunjucks.configure({ autoescape: false });
        return nunjucks.render(templatePath, {
            ...data,
            jsFunc,
        });
    }

    protected getFilePrefix(configName: string) {
        let filePrefix = this.context.testConfig.getValue(configName);
        if (filePrefix.length > 0 && filePrefix[filePrefix.length - 1] !== '_') {
            filePrefix += '_';
        }
        return filePrefix;
    }
}
