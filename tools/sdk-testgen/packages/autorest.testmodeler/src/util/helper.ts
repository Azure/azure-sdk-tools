import * as child_process from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { AutorestExtensionHost, Session } from '@autorest/extension-base';
import { ChoiceSchema, CodeModel, ComplexSchema, ObjectSchema, Operation, Parameter, Property, codeModelSchema, isVirtualParameter } from '@autorest/codemodel';
import { JSONPath } from 'jsonpath-plus';
import { comment, serialize } from '@azure-tools/codegen';

export class Helper {
    static dumpBuf: Record<string, any> = {};
    public static async outputToModelerfour(
        host: AutorestExtensionHost,
        session: Session<CodeModel>,
        exportExplicitTypes: boolean,
        explicitTypes: string[] = undefined,
    ): Promise<void> {
        // write the final result first which is hardcoded in the Session class to use to build the model..
        // overwrite the modelerfour which should be fine considering our change is backward compatible
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const modelerfourOptions = await session.getValue('modelerfour', {});
        if (modelerfourOptions['emit-yaml-tags'] !== false) {
            if (exportExplicitTypes) {
                codeModelSchema.explicit = codeModelSchema.explicit.concat(codeModelSchema.implicit.filter((t) => Helper.isExplicitTypes(t.tag, explicitTypes)));
                codeModelSchema.implicit = codeModelSchema.implicit.filter((t) => !Helper.isExplicitTypes(t.tag, explicitTypes));
                codeModelSchema.compiledExplicit = codeModelSchema.compiledExplicit.concat(
                    codeModelSchema.compiledImplicit.filter((t) => Helper.isExplicitTypes(t.tag, explicitTypes)),
                );
                codeModelSchema.compiledImplicit = codeModelSchema.compiledImplicit.filter((t) => !Helper.isExplicitTypes(t.tag, explicitTypes));
            }
            host.writeFile({
                filename: 'code-model-v4.yaml',
                content: serialize(session.model, { schema: codeModelSchema }),
                artifactType: 'code-model-v4',
            });
        }
        if (modelerfourOptions['emit-yaml-tags'] !== true) {
            host.writeFile({
                filename: 'code-model-v4-no-tags.yaml',
                content: serialize(session.model),
                artifactType: 'code-model-v4-no-tags',
            });
        }
    }

    private static isExplicitTypes(tag: string, explicitTypes: string[] = undefined): boolean {
        return tag && (explicitTypes || []).some((t) => tag.endsWith(t));
    }

    public static addCodeModelDump(session: Session<CodeModel>, fileName: string, withTags: boolean, debugOnly = true) {
        this.dumpBuf[(debugOnly ? '__debug/' : '') + fileName] = withTags ? serialize(session.model, { schema: codeModelSchema }) : serialize(session.model);
    }

    public static async dump(host: AutorestExtensionHost): Promise<void> {
        for (const [filename, content] of Object.entries(this.dumpBuf)) {
            host.writeFile({
                filename: filename,
                content: content,
            });
        }
        this.dumpBuf = {};
    }

    public static allParameters(operation: Operation) {
        const ret: Parameter[] = [];
        if (operation.parameters) {
            ret.push(...operation.parameters);
        }
        if (operation.requests[0]?.parameters) {
            ret.push(...operation.requests[0].parameters);
        }
        return ret;
    }

    public static getParameterSerializedName(parameter: Parameter) {
        return parameter?.language?.default?.['serializedName'] || parameter?.language?.default?.['name'];
    }

    public static getFlattenedNames(parameter: Parameter): string[] {
        let ret = undefined;
        if (isVirtualParameter(parameter)) {
            ret = parameter.targetProperty.flattenedNames;
            if (!ret) {
                ret = [parameter.originalParameter.language.default.name, parameter.language.default.name];
            }
        }
        if (!ret) {
            ret = [this.getParameterSerializedName(parameter)];
        }
        return ret;
    }

    public static findInDescents(schema: ObjectSchema, value: Record<string, any>): ComplexSchema {
        if (schema.discriminator) {
            const discriminatorKey = schema.discriminator.property.serializedName;
            if (Object.prototype.hasOwnProperty.call(value, discriminatorKey) && Object.prototype.hasOwnProperty.call(schema.discriminator.all, value[discriminatorKey])) {
                return schema.discriminator.all[value[discriminatorKey]];
            }
        }

        //TODO: find most matched child by properties if no discriminator
        return schema;
    }

    public static getAllProperties(schema: ComplexSchema, withParents = false): Property[] {
        const ret: Property[] = [];
        if (!schema) {
            return ret;
        }
        if (Object.prototype.hasOwnProperty.call(schema, 'properties')) {
            ret.push(...(schema as ObjectSchema).properties);
        }
        if (withParents && Object.prototype.hasOwnProperty.call(schema, 'parents')) {
            for (const parent of (schema as ObjectSchema).parents.immediate) {
                ret.push(...this.getAllProperties(parent, withParents));
            }
        }
        return ret;
    }

    public static escapeString(str: string): string {
        return str.split('\\').join('\\\\').split('"').join('\\"').replace(/\n/g, '\\n').replace(/\r/g, '\\r');
    }

    public static quotedEscapeString(str: string, quoter = '"'): string {
        return `${quoter}${Helper.escapeString(str + '')}${quoter}`;
    }

    public static nextVariableName(name: string) {
        const match = name.match(/\d+$/);
        if (match === undefined || match === null) {
            return name + '2';
        }
        const tailNumber = parseInt(match[0], 10);
        return `${name.slice(0, -match[0].length)}${tailNumber + 1}`;
    }

    public static findChoiceValue(schema: ChoiceSchema, rawValue: any) {
        for (const choiceValue of schema.choices) {
            if (choiceValue.value === rawValue) {
                return choiceValue;
            }
        }
        throw new Error(`${rawValue} is NOT a valid ${schema.language.default.name} value`);
    }

    public static execSync(command: string) {
        child_process.execSync(command);
    }

    public static deleteFolderRecursive(target) {
        if (fs.existsSync(target)) {
            fs.readdirSync(target).forEach((file, _) => {
                const curPath = path.join(target, file);
                if (fs.lstatSync(curPath).isDirectory()) {
                    // recurse
                    this.deleteFolderRecursive(curPath);
                } else {
                    // delete file
                    fs.unlinkSync(curPath);
                }
            });
            fs.rmdirSync(target);
        }
    }

    public static toKebabCase(v: string): string {
        return v
            .replace(/([a-z](?=[A-Z]))/g, '$1 ')
            .split(' ')
            .join('-')
            .toLowerCase();
    }

    public static toSnakeCase(v: string): string {
        return v
            .replace(/([a-z](?=[A-Z]))/g, '$1 ')
            .split(' ')
            .join('_')
            .toLowerCase();
    }

    public static toCamelCase(v: string): string {
        v = v
            .toLowerCase()
            .replace(/[^A-Za-z0-9]/g, ' ')
            .split(' ')
            .reduce((result, word) => result + this.capitalize(word.toLowerCase()));
        return v.charAt(0).toLowerCase() + v.slice(1);
    }

    public static capitalize(v: string): string {
        return v.charAt(0).toUpperCase() + v.slice(1);
    }

    public static uncapitalize(v: string) {
        return v.charAt(0).toLowerCase() + v.slice(1);
    }

    public static async getCopyright(session: Session<CodeModel>): Promise<string> {
        return comment(await session.getValue('header-text', 'MISSING LICENSE HEADER'), '// ');
    }

    public static queryByPath(obj: any, path: string[]): any[] {
        const jsonPath = '$' + path.map((x) => `['${x}']`).join('');
        return JSONPath({ path: jsonPath, json: obj });
    }

    public static queryBodyParameter(obj: any, path: string[]): any[] {
        let i = 0;
        let cur = obj;
        while (path.length > i) {
            const realKey =
                i === 0 ? Object.keys(cur).find((key) => key.replace(/[^A-Za-z$0-9]/g, '').toLowerCase() === path[i].replace(/[^A-Za-z$0-9]/g, '').toLowerCase()) : path[i];
            if (realKey) {
                cur = cur[realKey];
            } else {
                return [];
            }
            i++;
        }
        return [cur];
    }

    public static getExampleRelativePath(src: string): string {
        src = src ? src : '';
        const dst = src.match('specification/.*/examples/.*\\.json');
        if (dst !== null) {
            return dst[0];
        } else {
            return src;
        }
    }

    public static pathIsIncluded(paths: Set<any[]>, path: any[]): boolean {
        for (const t of paths) {
            if (t.length > path.length) {
                continue;
            }
            let isDiff = false;
            for (let i = 0; i < t.length; i++) {
                if (t[i] !== path[i]) {
                    isDiff = true;
                    break;
                }
            }
            if (!isDiff) {
                return true;
            }
        }
        return false;
    }

    public static filterPathsByPrefix(paths: Set<string[]>, prefix: string[]): Set<string[]> {
        return new Set(
            Array.from(paths)
                .filter((x) => x.length >= prefix.length && x.slice(0, prefix.length).join(',') === prefix.join(','))
                .map((x) => x.slice(prefix.length)),
        );
    }
}
