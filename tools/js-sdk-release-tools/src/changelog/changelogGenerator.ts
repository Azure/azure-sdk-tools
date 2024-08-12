import {
    ClassDeclaration,
    EnumDeclaration,
    InterfaceDeclaration,
    TypeAliasDeclaration
} from "parse-ts-to-ast";
import { IntersectionDeclaration } from "parse-ts-to-ast/build/declarations/IntersectionDeclaration";
import { TypeLiteralDeclaration } from "parse-ts-to-ast/build/declarations/TypeLiteralDeclaration";
import { TSExportedMetaData } from "./extractMetaData";
import { SDKType } from "../common/types";
import { logger } from "../utils/logger";
// TODO: add detection for routes and overloads in base detector
import { detectBreakingChangesBetweenPackages, InlineDeclarationNameSetMessage, RuleMessage, RuleMessageKind } from "typescript-codegen-breaking-change-detector";
import { RestLevelClientChangelogPostProcessor } from "./RestLevelClientChangelogPostProcessor";

export interface ChangelogItem {
    line: string;
    oldName?: string;
    newName?: string;
    toDelete?: boolean;
}

export class Changelog {
    // features
    public addedOperationGroup: ChangelogItem[] = [];
    public addedOperation: ChangelogItem[] = [];
    public addedInterface: ChangelogItem[] = [];
    public addedClass: ChangelogItem[] = [];
    public addedTypeAlias: ChangelogItem[] = [];
    public interfaceAddOptionalParam: ChangelogItem[] = [];
    public interfaceParamTypeExtended: ChangelogItem[] = [];
    public typeAliasAddInherit: ChangelogItem[] = [];
    public typeAliasAddParam: ChangelogItem[] = [];
    public addedEnum: ChangelogItem[] = [];
    public addedEnumValue: ChangelogItem[] = [];
    public addedFunction: ChangelogItem[] = [];
    // breaking changes
    public removedOperationGroup: ChangelogItem[] = [];
    public removedOperation: ChangelogItem[] = [];
    public operationSignatureChange: ChangelogItem[] = [];
    public deletedClass: ChangelogItem[] = [];
    public classSignatureChange: ChangelogItem[] = [];
    public interfaceParamDelete: ChangelogItem[] = [];
    public interfaceParamAddRequired: ChangelogItem[] = [];
    public interfaceParamTypeChanged: ChangelogItem[] = [];
    public interfaceParamChangeRequired: ChangelogItem[] = [];
    public classParamDelete: ChangelogItem[] = [];
    public classParamChangeRequired: ChangelogItem[] = [];
    public typeAliasDeleteInherit: ChangelogItem[] = [];
    public typeAliasParamDelete: ChangelogItem[] = [];
    public typeAliasAddRequiredParam: ChangelogItem[] = [];
    public typeAliasParamChangeRequired: ChangelogItem[] = [];
    public removedEnum: ChangelogItem[] = [];
    public removedEnumValue: ChangelogItem[] = [];
    public removedFunction: ChangelogItem[] = [];

    public get hasBreakingChange() {
        return this.removedOperationGroup.filter(i => !i.toDelete).length > 0 ||
            this.removedOperation.filter(i => !i.toDelete).length > 0 ||
            this.operationSignatureChange.filter(i => !i.toDelete).length > 0 ||
            this.deletedClass.filter(i => !i.toDelete).length > 0 ||
            this.classSignatureChange.filter(i => !i.toDelete).length > 0 ||
            this.interfaceParamDelete.filter(i => !i.toDelete).length > 0 ||
            this.interfaceParamAddRequired.filter(i => !i.toDelete).length > 0 ||
            this.interfaceParamChangeRequired.filter(i => !i.toDelete).length > 0 ||
            this.interfaceParamTypeChanged.filter(i => !i.toDelete).length > 0 ||
            this.classParamDelete.filter(i => !i.toDelete).length > 0 ||
            this.classParamChangeRequired.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasDeleteInherit.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasParamDelete.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasAddRequiredParam.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasParamChangeRequired.filter(i => !i.toDelete).length > 0 ||
            this.removedEnum.filter(i => !i.toDelete).length > 0 ||
            this.removedEnumValue.filter(i => !i.toDelete).length > 0;
            this.removedFunction.filter(i => !i.toDelete).length > 0;
    }

    public get hasFeature() {
        return this.addedOperationGroup.filter(i => !i.toDelete).length > 0 ||
            this.addedOperation.filter(i => !i.toDelete).length > 0 ||
            this.addedInterface.filter(i => !i.toDelete).length > 0 ||
            this.addedClass.filter(i => !i.toDelete).length > 0 ||
            this.addedTypeAlias.filter(i => !i.toDelete).length > 0 ||
            this.interfaceAddOptionalParam.filter(i => !i.toDelete).length > 0 ||
            this.interfaceParamTypeExtended.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasAddInherit.filter(i => !i.toDelete).length > 0 ||
            this.typeAliasAddParam.filter(i => !i.toDelete).length > 0 ||
            this.addedEnum.filter(i => !i.toDelete).length > 0 ||
            this.addedEnumValue.filter(i => !i.toDelete).length > 0;
        this.addedFunction.filter(i => !i.toDelete).length > 0;
    }

    public getBreakingChangeItems(): string[] {
        let items: string[] = [];
        if (this.hasBreakingChange) {
            this.removedOperationGroup.filter(i => !i.toDelete).map(i => i.line)
                .concat(this.removedOperation.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.operationSignatureChange.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.deletedClass.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classSignatureChange.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamAddRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamTypeChanged.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasDeleteInherit.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasAddRequiredParam.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedEnum.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedEnumValue.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedFunction.filter(i => !i.toDelete).map(i => i.line))
                .forEach(e => {
                    items.push(e);
                });
        }
        return items;
    }

    public displayChangeLog(): string {
        const display: string[] = [];
        if (this.hasFeature) {
            display.push('### Features Added');
            display.push('');
            this.addedOperationGroup.filter(i => !i.toDelete).map(i => i.line)
                .concat(this.addedOperation.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedInterface.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedClass.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedTypeAlias.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceAddOptionalParam.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamTypeExtended.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasAddInherit.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasAddParam.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedEnum.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedEnumValue.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.addedFunction.filter(i => !i.toDelete).map(i => i.line))
                .forEach(e => {
                    display.push('  - ' + e);
                });
        }

        if (this.hasBreakingChange) {
            if (this.hasFeature) display.push('');
            display.push('### Breaking Changes');
            display.push('');
            this.removedOperationGroup.filter(i => !i.toDelete).map(i => i.line)
                .concat(this.removedOperation.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.operationSignatureChange.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.deletedClass.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classSignatureChange.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamAddRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.interfaceParamTypeChanged.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.classParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasDeleteInherit.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasParamDelete.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasAddRequiredParam.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.typeAliasParamChangeRequired.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedEnum.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedEnumValue.filter(i => !i.toDelete).map(i => i.line))
                .concat(this.removedFunction.filter(i => !i.toDelete).map(i => i.line))
                .forEach(e => {
                    display.push('  - ' + e);
                });
        }

        return display.join('\n');
    }

    // TODO: add modular
    public async postProcess(baselinePackageRoot: string, currentPackageRoot: string, sdkType: SDKType): Promise<void> {
        if (sdkType !== SDKType.RestLevelClient) {
            logger.logWarn(`No need to post process changelog for ${sdkType}.`)
            return;
        }
        try {
            const messageMap = await detectBreakingChangesBetweenPackages(baselinePackageRoot, currentPackageRoot, baselinePackageRoot, true);
            
            switch (sdkType) {
                case SDKType.RestLevelClient:
                    await this.postProcessForRestLevelClient(messageMap);
                    break;
            }
        } catch (err) {
            throw new Error(`Failed to apply special breaking change rules to ${sdkType}`);
        }
    }

    private processInlineMessage(messages: InlineDeclarationNameSetMessage[]) {
        logger.logInfo('Before post process rename messages in changelog')
        logger.logGreen(this.displayChangeLog());
        
        if (messages.length !== 1) {
            throw new Error(`Multiple inline messages are unsupported`)
        }
        const postProcesser = new RestLevelClientChangelogPostProcessor(this, messages[0]);
        postProcesser.run();

        logger.logInfo('After post process rename messages in changelog')
        logger.logGreen(this.displayChangeLog());
    }

    private async postProcessForRestLevelClient(messageMap: Map<string, RuleMessage[] | undefined>) {
        // RLC only has 1 api view
        const key = Array.from(messageMap.keys())[0]
        const messages = messageMap.get(key)!
        const inlineMessages = messages.filter(m => m.kind === RuleMessageKind.InlineDeclarationNameSetMessage).map(m => m as InlineDeclarationNameSetMessage)
        this.processInlineMessage(inlineMessages)
    }
}

// todo: special rules for HLC convert to Modular, will use a more generic method to replace
function getRenamedOperationGroupFromToMap(from: TSExportedMetaData): { [id: string]: string } {
    const map: { [id: string]: string } = {};
    for (const fromName in from.operationInterface) {
        const operationIndex = fromName.lastIndexOf("Operations");
        const toName = operationIndex >= 1 ? fromName.substring(0, operationIndex) : fromName + "Operations";
        map[fromName] = toName;
    }
    return map;
}

const findAddedOperationGroup = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType
): ChangelogItem[] => {
    const newToOldMap = getRenamedOperationGroupFromToMap(metaDataNew);
    const addOperationGroup: ChangelogItem[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(operationGroup => {
        const oldName = oldSdkType === newSdkType ? operationGroup : newToOldMap[operationGroup];
        if (!metaDataOld.operationInterface[oldName]) {
            addOperationGroup.push({ line: 'Added operation group ' + operationGroup, newName: operationGroup });
        }
    });
    return addOperationGroup;
};

function getAllMethodNameInInterface(interface_: InterfaceDeclaration): Array<string> {
    const nameFromMethods = interface_.methods.map(m => m.name);
    const nameFromProperties = interface_.properties.map(m => m.name);
    return [...nameFromMethods, ...nameFromProperties];
}

const findAddedOperation = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType
): ChangelogItem[] => {
    const newToOldMap = getRenamedOperationGroupFromToMap(metaDataNew);
    const addOperation: ChangelogItem[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(newOperationGroup => {
        const oldOperationGroup = oldSdkType === newSdkType ? newOperationGroup : newToOldMap[newOperationGroup];
        if(!metaDataOld.operationInterface[oldOperationGroup]) {
            return;
        }
        const newInterfaceMethodNames = getAllMethodNameInInterface(metaDataNew.operationInterface[newOperationGroup]);
        const oldInterfaceMethodNames = getAllMethodNameInInterface(metaDataOld.operationInterface[oldOperationGroup]);
        newInterfaceMethodNames
            .filter(newOpName => !oldInterfaceMethodNames.includes(newOpName))
            .forEach(newOpName => { addOperation.push({ line: 'Added operation ' + newOperationGroup + '.' + newOpName, newName: newOperationGroup }); });
        return;
    });
    return addOperation;
};

const findAddedInterface = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addInterface: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (!metaDataOld.modelInterface[model]) {
            addInterface.push({ line: 'Added Interface ' + model, newName: model });
        }
    });
    return addInterface;
};

const findAddedClass = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addClass: ChangelogItem[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (!metaDataOld.classes[model]) {
            addClass.push({ line: 'Added Class ' + model, newName: model });
        }
    });
    return addClass;
};

const findAddedTypeAlias = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addModel: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (!metaDataOld.typeAlias[typeAlias]) {
            addModel.push({ line: 'Added Type Alias ' + typeAlias, newName: typeAlias });
        }
    });
    return addModel;
};

const findInterfaceAddOptinalParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceAddedParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (pNew.isOptional) {
                    let find = false;
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            find = true;
                            return;
                        }
                    });
                    if (!find) {
                        interfaceAddedParam.push({ line: 'Interface ' + model + ' has a new optional parameter ' + pNew.name });
                    }
                }
            });
        }
    });
    return interfaceAddedParam;
};

const findInterfaceParamTypeExtended = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceParamTypeExtended: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                modelFromOld.properties.forEach(pOld => {
                    if (pNew.name === pOld.name) {
                        if (pNew.type !== pOld.type) {
                            if (pNew.type?.includes('|')) { // is union
                                const newTypes = pNew.type?.split('|').map(e => e.toString().trim());
                                const oldTypes = pOld.type?.split('|').map(e => e.toString().trim());
                                if (!!newTypes && !!oldTypes) {
                                    let allFind = true;
                                    for (const t of oldTypes) {
                                        if (!newTypes.includes(t)) {
                                            allFind = false;
                                            break;
                                        }
                                    }
                                    if (allFind) {
                                        interfaceParamTypeExtended.push({ line: `Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`, newName: pNew.type, oldName: pOld.type });
                                    }
                                }
                            }
                        }
                        return;
                    }
                });
            });
        }
    });
    return interfaceParamTypeExtended;
};

const findTypeAliasAddInherit = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasAddInherit: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                if (typeAliasFromNew.type.inherits) {
                    typeAliasFromNew.type.inherits.forEach(inherit => {
                        if (typeof inherit === 'string') {
                            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                                // strange behavior, 'ClustersUpdateMediaTypesParam_CCC' is in intersection type, but it's in 'inherits'
                                // export type ClustersUpdateParameters = ClustersUpdateMediaTypesParam_CCC & ClustersUpdateBodyParam & RequestParameters;
                                if (typeAliasFromOld.type.inherits) {
                                    if (!typeAliasFromOld.type.inherits.includes(inherit)) {
                                        typeAliasAddInherit.push({ line: 'Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias, newName: inherit });
                                    }
                                } else {
                                    typeAliasAddInherit.push({ line: 'Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias, newName: inherit });
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                typeAliasAddInherit.push({ line: 'Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias, newName: inherit });
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                if (typeAliasFromOld.type !== inherit) {
                                    typeAliasAddInherit.push({ line: 'Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias, newName: inherit });
                                }
                            }
                        }
                    })
                }
            }
        }
    });
    return typeAliasAddInherit;
};

const findTypeAliasAddParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasAddParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                if (typeAliasFromNew.type.typeLiteralDeclarations) {
                    typeAliasFromNew.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationNew => {
                        typeLiteralDeclarationNew.properties.forEach(pNew => {
                            if (!pNew.isOptional) return;
                            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                    let find = false;
                                    typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                        typeLiteralDeclarationOld.properties.forEach(pOld => {
                                            if (pNew.name === pOld.name) {
                                                find = true;
                                            }
                                        });
                                    });
                                    if (!find) {
                                        typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                    }
                                } else {
                                    typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        });
                    });

                } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                    typeAliasFromNew.type.properties.forEach(pNew => {
                        if (!pNew.isOptional) return;
                        if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                            if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                let find = false;
                                typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                    typeLiteralDeclarationOld.properties.forEach(pOld => {
                                        if (pNew.name === pOld.name) {
                                            find = true;
                                        }
                                    });
                                });
                                if (!find) {
                                    typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else {
                                typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        } else if (typeof typeAliasFromOld.type === 'string') {
                            typeAliasAddParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                        }
                    });
                }
            }
        }
    });
    return typeAliasAddParam;
};

const findAddedEnum = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addedEnum: ChangelogItem[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (!metaDataOld.enums[e]) {
            addedEnum.push({ line: 'Added Enum ' + e, newName: e });
        }
    });
    return addedEnum;
};

const findAddedEnumValue = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addedEnumValue: ChangelogItem[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (metaDataOld.enums[e]) {
            const enumOld = metaDataOld.enums[e] as EnumDeclaration;
            const enumNew = metaDataNew.enums[e] as EnumDeclaration;
            enumNew.members.forEach(v => {
                if (!enumOld.members.includes(v)) {
                    addedEnumValue.push({ line: 'Enum ' + e + ' has a new value ' + v, newName: e });
                }
            });
        }
    });
    return addedEnumValue;
};

const findAddedFunction = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const addedFunction: ChangelogItem[] = [];
    Object.keys(metaDataNew.functions).forEach(e => {
        if (!metaDataOld.functions[e]) {
            addedFunction.push({ line: `Added function ${e}`, newName: e });
        }
    });
    return addedFunction;
};


const findRemovedOperationGroup = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType
): ChangelogItem[] => {
    const oldToNew = getRenamedOperationGroupFromToMap(metaDataOld);
    const removedOperationGroup: ChangelogItem[] = [];
    Object.keys(metaDataOld.operationInterface).forEach(oldOperationGroup => {
        const newOperationGroup = oldSdkType === newSdkType ? oldOperationGroup : oldToNew[oldOperationGroup];
        if (!metaDataNew.operationInterface[newOperationGroup]) {
            removedOperationGroup.push({ line: 'Removed operation group ' + oldOperationGroup, oldName: oldOperationGroup });
        }
    });
    return removedOperationGroup;
};

const findRemovedOperation = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType
): ChangelogItem[] => {
    const oldToNew = getRenamedOperationGroupFromToMap(metaDataOld);
    const removedOperation: ChangelogItem[] = [];

    Object.keys(metaDataOld.operationInterface).forEach(oldOperationGroup => {
        const newOperationGroup = oldSdkType === newSdkType ? oldOperationGroup : oldToNew[oldOperationGroup];
        if(!metaDataNew.operationInterface[newOperationGroup]) {
            return;
        }
        const newInterfaceMethodNames = getAllMethodNameInInterface(metaDataNew.operationInterface[newOperationGroup]);
        const oldInterfaceMethodNames = getAllMethodNameInInterface(metaDataOld.operationInterface[oldOperationGroup]);
        oldInterfaceMethodNames
            .filter(oldOpName => !newInterfaceMethodNames.includes(oldOpName))
            .forEach(oldOpName => { removedOperation.push({ line: 'Removed operation ' + oldOperationGroup + '.' + oldOpName, oldName: oldOpName }); });
        return true;
    });
    return removedOperation;
};

const findOperationSignatureChange = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType): ChangelogItem[] => {
    const newToOld = getRenamedOperationGroupFromToMap(metaDataNew);
    const operationSignatureChange: ChangelogItem[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(newOperationGroup => {
        const oldOperationGroup = oldSdkType === newSdkType ? newOperationGroup : newToOld[newOperationGroup];
        if (!metaDataOld.operationInterface[oldOperationGroup]) {
            return;
        }
        if (oldSdkType === SDKType.HighLevelClient && newSdkType === SDKType.ModularClient) {
            const operationGroupFromOld = metaDataOld.operationInterface[oldOperationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[newOperationGroup] as InterfaceDeclaration;
            const oldOpNames = operationGroupFromOld.properties.map(p => p.name);
            const newOpNames = operationGroupFromNew.methods.map(m => m.name);
            const newOpNameSet = new Set<string>(newOpNames);
            const unchangeOperationNames = oldOpNames.filter(opName => newOpNameSet.has(opName)).map(opName => opName);
            logger.logWarn(`${unchangeOperationNames} operation names aren't changed, but signature may change, please check manually.`);
            return;
        }
        
        if (oldSdkType === SDKType.ModularClient && newSdkType === SDKType.ModularClient) {
            const operationGroupFromOld = metaDataOld.operationInterface[oldOperationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[newOperationGroup] as InterfaceDeclaration;
            const oldOpNames = operationGroupFromOld.properties.map(p => p.name);
            const newOpNames = operationGroupFromNew.properties.map(m => m.name);
            const newOpNameSet = new Set<string>(newOpNames);
            const unchangeOperationNames = oldOpNames.filter(opName => newOpNameSet.has(opName)).map(opName => opName);
            logger.logWarn(`${unchangeOperationNames} operation names aren't changed, but signature may change, please check manually.`);
            return;
        }

        // oldSdkType === SDKType.HighLevelClient && newSdkType === SDKType.HighLevelClient
        if (metaDataOld.operationInterface[oldOperationGroup]) {
            const operationGroupFromOld = metaDataOld.operationInterface[oldOperationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[newOperationGroup] as InterfaceDeclaration;
            operationGroupFromNew.methods.forEach(mNew => {
                operationGroupFromOld.methods.forEach(mOld => {
                    if (mOld.name === mNew.name) {
                        const parametersOld = mOld.parameters;
                        const parametersNew = mNew.parameters;
                        if (parametersNew.length !== parametersOld.length) {
                            operationSignatureChange.push({ line:'Operation ' + newOperationGroup + '.' + mNew.name + ' has a new signature', newName: mNew.type });
                        } else {
                            for (let index = 0; index < parametersNew.length; index++) {
                                const pOld = parametersOld[index];
                                const pNew = parametersNew[index];
                                if (pOld.type !== pNew.type || pOld.isOptional !== pNew.isOptional) {
                                    operationSignatureChange.push({ line:'Operation ' + newOperationGroup + '.' + mNew.name + ' has a new signature', newName: mNew.type });
                                    return;
                                }
                            }
                        }
                        return;
                    }
                });
            })
        }
    });
    return operationSignatureChange;
};

const findDeletedClass = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const deletedClass: ChangelogItem[] = [];
    Object.keys(metaDataOld.classes).forEach(model => {
        if (!metaDataNew.classes[model]) {
            deletedClass.push({ line: 'Deleted Class ' + model, oldName: model });
        }
    });
    return deletedClass;
};

const findClassSignatureChange = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const classSignatureChange: ChangelogItem[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (metaDataOld.classes[model]) {
            const modelFromOld = metaDataOld.classes[model] as ClassDeclaration;
            const modelFromNew = metaDataNew.classes[model] as ClassDeclaration;
            const constructorOld = modelFromOld.ctor;
            const constructorNew = modelFromNew.ctor;
            if (constructorOld === undefined && constructorNew === undefined) return;
            if (constructorOld === undefined || constructorNew === undefined) {
                classSignatureChange.push({ line: 'Class ' + model + ' has a new signature' });
                return;
            }
            const parametersOld = constructorOld.parameters;
            const parametersNew = constructorNew.parameters;
            if (parametersNew.length !== parametersOld.length) {
                classSignatureChange.push({ line: 'Class ' + model + ' has a new signature' });
            } else {
                for (let index = 0; index < parametersNew.length; index++) {
                    const pOld = parametersOld[index];
                    const pNew = parametersNew[index];
                    if (pOld.type !== pNew.type || pOld.isOptional !== pNew.isOptional) {
                        classSignatureChange.push({ line: 'Class ' + model + ' has a new signature' });
                        return;
                    }
                }
            }
        }
    });
    return classSignatureChange;
};

const findInterfaceParamDelete = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceDeleteParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromOld.properties.forEach(pOld => {
                let find = false;
                modelFromNew.properties.forEach(pNew => {
                    if (pNew.name === pOld.name) {
                        find = true;
                        return;
                    }
                });
                if (modelFromNew.extends?.length > 0) {
                    modelFromNew.extends.forEach(parentInterfaceName => {
                        const parentInterface = metaDataNew.modelInterface[parentInterfaceName];
                        if (!!parentInterfaceName && parentInterface instanceof InterfaceDeclaration) {
                            parentInterface.properties.forEach(pNew => {
                                if (pNew.name === pOld.name) {
                                    find = true;
                                    return;
                                }
                            })
                        }
                    });
                }
                if (!find) {
                    interfaceDeleteParam.push({ line: 'Interface ' + model + ' no longer has parameter ' + pOld.name, oldName: pOld.name});
                }
            });
        }
    });
    return interfaceDeleteParam;
};

const findInterfaceParamAddRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceAddedParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (!pNew.isOptional) {
                    let find = false;
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            find = true;
                            return;
                        }
                    });
                    if (!find) {
                        interfaceAddedParam.push({ line: 'Interface ' + model + ' has a new required parameter ' + pNew.name, newName: '?????' });
                    }
                }
            });
        }
    });
    return interfaceAddedParam;
};

const findInterfaceParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceParamChangeRequired: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (!pNew.isOptional) {
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            if (pOld.isOptional) {
                                interfaceParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of interface ' + model + ' is now required' });
                            }
                            return;
                        }
                    });
                }
            });
        }
    });
    return interfaceParamChangeRequired;
};

const findInterfaceParamTypeChanged = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const interfaceParamTypeChanged: ChangelogItem[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                modelFromOld.properties.forEach(pOld => {
                    if (pNew.name === pOld.name) {
                        if (pNew.type !== pOld.type) {
                            if (pNew.type?.includes('|')) { // is union
                                const newTypes = pNew.type?.split('|').map(e => e.toString().trim());
                                const oldTypes = pOld.type?.split('|').map(e => e.toString().trim());
                                if (!!newTypes && !!oldTypes) {
                                    for (const t of oldTypes) {
                                        if (!newTypes.includes(t)) {
                                            interfaceParamTypeChanged.push({ line: `Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`, oldName: pOld.type, newName: pNew.type });
                                            break;
                                        }
                                    }
                                }
                            } else {
                                interfaceParamTypeChanged.push({ line: `Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`, oldName: pOld.type, newName: pNew.type });
                            }
                        }
                        return;
                    }
                });
            });
        }
    });
    return interfaceParamTypeChanged;
};

const findClassParamDelete = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const classDeleteParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (metaDataOld.classes[model]) {
            const modelFromOld = metaDataOld.classes[model] as ClassDeclaration;
            const modelFromNew = metaDataNew.classes[model] as ClassDeclaration;
            modelFromOld.properties.forEach(pOld => {
                let find = false;
                modelFromNew.properties.forEach(pNew => {
                    if (pNew.name === pOld.name) {
                        find = true;
                        return;
                    }
                });
                if (!find) {
                    classDeleteParam.push({ line: 'Class ' + model + ' no longer has parameter ' + pOld.name });
                }
            });
        }
    });
    return classDeleteParam;
};

const findClassParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const classParamChangeRequired: ChangelogItem[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (metaDataOld.classes[model]) {
            const modelFromOld = metaDataOld.classes[model] as ClassDeclaration;
            const modelFromNew = metaDataNew.classes[model] as ClassDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (!pNew.isOptional) {
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            if (pOld.isOptional) {
                                classParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of class ' + model + ' is now required' });
                            }
                            return;
                        }
                    });
                }
            });
        }
    });
    return classParamChangeRequired;
};

const findTypeAliasDeleteInherit = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasDeleteInherit: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                if (typeAliasFromOld.type.inherits) {
                    typeAliasFromOld.type.inherits.forEach(inherit => {
                        if (typeof inherit === 'string') {
                            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromNew.type.inherits) {
                                    if (!typeAliasFromNew.type.inherits.includes(inherit)) {
                                        typeAliasDeleteInherit.push({ line: 'Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias, oldName: inherit });
                                    }
                                } else {
                                    typeAliasDeleteInherit.push({ line: 'Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias, oldName: inherit });
                                }
                            } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                                typeAliasDeleteInherit.push({ line: 'Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias, oldName: inherit });
                            } else if (typeof typeAliasFromNew.type === 'string') {
                                if (typeAliasFromNew.type !== inherit) {
                                    typeAliasDeleteInherit.push({ line: 'Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias, oldName: inherit });
                                }
                            }
                        }
                    })
                }
            }
        }
    });
    return typeAliasDeleteInherit;
};

const findTypeAliasDeleteParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasDeleteParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                if (typeAliasFromOld.type.typeLiteralDeclarations) {
                    typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                        typeLiteralDeclarationOld.properties.forEach(pOld => {
                            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromNew.type.typeLiteralDeclarations) {
                                    let find = false;
                                    typeAliasFromNew.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationNew => {
                                        typeLiteralDeclarationNew.properties.forEach(pNew => {
                                            if (pOld.name === pNew.name) {
                                                find = true;
                                            }
                                        });
                                    });
                                    if (typeAliasFromNew.type.inherits?.length > 0) {
                                        typeAliasFromNew.type.inherits.forEach(modelInterfaceName => {
                                            const modelInterface = metaDataNew.modelInterface?.[modelInterfaceName];
                                            if (modelInterface) {
                                                modelInterface.properties?.forEach(pNew => {
                                                    if (pOld.name === pNew.name) {
                                                        find = true;
                                                    }
                                                });
                                            }
                                        })
                                    }
                                    if (!find) {
                                        typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name });
                                    }
                                } else {
                                    typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name });
                                }
                            } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromNew.type.properties.forEach(pNew => {
                                    if (pOld.name === pNew.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name });
                                }
                            } else if (typeof typeAliasFromNew.type === 'string') {
                                typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name });
                            }
                        });
                    });

                } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                    typeAliasFromOld.type.properties.forEach(pOld => {
                        if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                            if (typeAliasFromNew.type.typeLiteralDeclarations) {
                                let find = false;
                                typeAliasFromNew.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationNew => {
                                    typeLiteralDeclarationNew.properties.forEach(pNew => {
                                        if (pOld.name === pNew.name) {
                                            find = true;
                                        }
                                    });
                                });
                                if (!find) {
                                    typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name });
                                }
                            } else {
                                typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name });
                            }
                        } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromNew.type.properties.forEach(pNew => {
                                if (pOld.name === pNew.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name });
                            }
                        } else if (typeof typeAliasFromNew.type === 'string') {
                            typeAliasDeleteParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name });
                        }
                    });
                }
            }
        }
    });
    return typeAliasDeleteParam;
};

const findTypeAliasAddRequiredParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasAddRequiredParam: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                if (typeAliasFromNew.type.typeLiteralDeclarations) {
                    typeAliasFromNew.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationNew => {
                        typeLiteralDeclarationNew.properties.forEach(pNew => {
                            if (pNew.isOptional) return;
                            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                    let find = false;
                                    typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                        typeLiteralDeclarationOld.properties.forEach(pOld => {
                                            if (pNew.name === pOld.name) {
                                                find = true;
                                            }
                                        });
                                    });
                                    if (!find) {
                                        typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                    }
                                } else {
                                    typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        });
                    });

                } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                    typeAliasFromNew.type.properties.forEach(pNew => {
                        if (pNew.isOptional) return;
                        if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                            if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                let find = false;
                                typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                    typeLiteralDeclarationOld.properties.forEach(pOld => {
                                        if (pNew.name === pOld.name) {
                                            find = true;
                                        }
                                    });
                                });
                                if (!find) {
                                    typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                                }
                            } else {
                                typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                            }
                        } else if (typeof typeAliasFromOld.type === 'string') {
                            typeAliasAddRequiredParam.push({ line: 'Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name });
                        }
                    });
                }
            }
        }
    });
    return typeAliasAddRequiredParam;
};

const findTypeAliasParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const typeAliasParamChangeRequired: ChangelogItem[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                if (typeAliasFromNew.type.typeLiteralDeclarations) {
                    typeAliasFromNew.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationNew => {
                        typeLiteralDeclarationNew.properties.forEach(pNew => {
                            if (pNew.isOptional) return;
                            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                    typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                        typeLiteralDeclarationOld.properties.forEach(pOld => {
                                            if (pNew.name === pOld.name && pOld.isOptional) {
                                                typeAliasParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required' });
                                            }
                                        });
                                    });
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name && pOld.isOptional) {
                                        typeAliasParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required' });
                                    }
                                });
                            }
                        });
                    });
                } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                    typeAliasFromNew.type.properties.forEach(pNew => {
                        if (pNew.isOptional) return;
                        if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                            if (typeAliasFromOld.type.typeLiteralDeclarations) {
                                typeAliasFromOld.type.typeLiteralDeclarations.forEach(typeLiteralDeclarationOld => {
                                    typeLiteralDeclarationOld.properties.forEach(pOld => {
                                        if (pNew.name === pOld.name && pOld.isOptional) {
                                            typeAliasParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required' });
                                        }
                                    });
                                });
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name && pOld.isOptional) {
                                    typeAliasParamChangeRequired.push({ line: 'Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required' });
                                }
                            });
                        }
                    });
                }
            }
        }
    });
    return typeAliasParamChangeRequired;
};

const findRemovedEnum = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const removedEnum: ChangelogItem[] = [];
    Object.keys(metaDataOld.enums).forEach(e => {
        if (!metaDataNew.enums[e]) {
            removedEnum.push({ line: 'Removed Enum ' + e });
        }
    });
    return removedEnum;
};

const findRemovedEnumValue = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const removedEnumValue: ChangelogItem[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (metaDataOld.enums[e]) {
            const enumOld = metaDataOld.enums[e] as EnumDeclaration;
            const enumNew = metaDataNew.enums[e] as EnumDeclaration;
            enumOld.members.forEach(v => {
                if (!enumNew.members.includes(v)) {
                    removedEnumValue.push({ line: 'Enum ' + e + ' no longer has value ' + v });
                }
            });
        }
    });
    return removedEnumValue;
};

const findRemovedFunction = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): ChangelogItem[] => {
    const removedFunction: ChangelogItem[] = [];
    Object.keys(metaDataOld.functions).forEach(e => {
        if (!metaDataNew.functions[e]) {
            removedFunction.push({ line: 'Removed function ' + e, oldName: e});
        }
    });
    return removedFunction;
};


export const changelogGenerator = (
    metaDataOld: TSExportedMetaData, metadataNew: TSExportedMetaData,
    oldSdkType: SDKType, newSdkType: SDKType): Changelog => {
    if (!oldSdkType || !newSdkType) {
        throw new Error(`SDK type is not valid. Old SDK type: ${oldSdkType}, New SDK type: ${newSdkType}`);
    }

    const changLog = new Changelog();

    // features
    changLog.addedOperationGroup = findAddedOperationGroup(metaDataOld, metadataNew, oldSdkType, newSdkType);
    changLog.addedOperation = findAddedOperation(metaDataOld, metadataNew, oldSdkType, newSdkType);
    changLog.addedInterface = findAddedInterface(metaDataOld, metadataNew);
    changLog.addedClass = findAddedClass(metaDataOld, metadataNew);
    changLog.addedTypeAlias = findAddedTypeAlias(metaDataOld, metadataNew);
    changLog.interfaceAddOptionalParam = findInterfaceAddOptinalParam(metaDataOld, metadataNew);
    changLog.interfaceParamTypeExtended = findInterfaceParamTypeExtended(metaDataOld, metadataNew);
    changLog.typeAliasAddInherit = findTypeAliasAddInherit(metaDataOld, metadataNew);
    changLog.typeAliasAddParam = findTypeAliasAddParam(metaDataOld, metadataNew);
    changLog.addedEnum = findAddedEnum(metaDataOld, metadataNew);
    changLog.addedEnumValue = findAddedEnumValue(metaDataOld, metadataNew);
    changLog.addedFunction = findAddedFunction(metaDataOld, metadataNew);

    // breaking changes
    changLog.removedOperationGroup = findRemovedOperationGroup(metaDataOld, metadataNew, oldSdkType, newSdkType);
    changLog.removedOperation = findRemovedOperation(metaDataOld, metadataNew, oldSdkType, newSdkType);
    changLog.operationSignatureChange = findOperationSignatureChange(metaDataOld, metadataNew, oldSdkType, newSdkType);
    changLog.deletedClass = findDeletedClass(metaDataOld, metadataNew);
    changLog.classSignatureChange = findClassSignatureChange(metaDataOld, metadataNew);
    changLog.interfaceParamDelete = findInterfaceParamDelete(metaDataOld, metadataNew);
    changLog.interfaceParamAddRequired = findInterfaceParamAddRequired(metaDataOld, metadataNew);
    changLog.interfaceParamChangeRequired = findInterfaceParamChangeRequired(metaDataOld, metadataNew);
    changLog.interfaceParamTypeChanged = findInterfaceParamTypeChanged(metaDataOld, metadataNew);
    changLog.classParamDelete = findClassParamDelete(metaDataOld, metadataNew);
    changLog.classParamChangeRequired = findClassParamChangeRequired(metaDataOld, metadataNew);
    changLog.typeAliasDeleteInherit = findTypeAliasDeleteInherit(metaDataOld, metadataNew);
    changLog.typeAliasParamDelete = findTypeAliasDeleteParam(metaDataOld, metadataNew);
    changLog.typeAliasAddRequiredParam = findTypeAliasAddRequiredParam(metaDataOld, metadataNew);
    changLog.typeAliasParamChangeRequired = findTypeAliasParamChangeRequired(metaDataOld, metadataNew);
    changLog.removedEnum = findRemovedEnum(metaDataOld, metadataNew);
    changLog.removedEnumValue = findRemovedEnumValue(metaDataOld, metadataNew);
    changLog.removedFunction = findRemovedFunction(metaDataOld, metadataNew);
    return changLog;
};
