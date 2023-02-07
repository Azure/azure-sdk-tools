import {
    ClassDeclaration,
    EnumDeclaration,
    InterfaceDeclaration,
    TypeAliasDeclaration
} from "parse-ts-to-ast";
import { IntersectionDeclaration } from "parse-ts-to-ast/build/declarations/IntersectionDeclaration";
import { TypeLiteralDeclaration } from "parse-ts-to-ast/build/declarations/TypeLiteralDeclaration";
import { TSExportedMetaData } from "./extractMetaData";

export class Changelog {
    // features
    public addedOperationGroup: string[] = [];
    public addedOperation: string[] = [];
    public addedInterface: string[] = [];
    public addedClass: string[] = [];
    public addedTypeAlias: string[] = [];
    public interfaceAddOptionalParam: string[] = [];
    public interfaceParamTypeExtended: string[] = [];
    public typeAliasAddInherit: string[] = [];
    public typeAliasAddParam: string[] = [];
    public addedEnum: string[] = [];
    public addedEnumValue: string[] = [];
    public addedFunction: string[] = [];
    // breaking changes
    public removedOperationGroup: string[] = [];
    public removedOperation: string[] = [];
    public operationSignatureChange: string[] = [];
    public deletedClass: string[] = [];
    public classSignatureChange: string[] = [];
    public interfaceParamDelete: string[] = [];
    public interfaceParamAddRequired: string[] = [];
    public interfaceParamTypeChanged: string[] = [];
    public interfaceParamChangeRequired: string[] = [];
    public classParamDelete: string[] = [];
    public classParamChangeRequired: string[] = [];
    public typeAliasDeleteInherit: string[] = [];
    public typeAliasParamDelete: string[] = [];
    public typeAliasAddRequiredParam: string[] = [];
    public typeAliasParamChangeRequired: string[] = [];
    public removedEnum: string[] = [];
    public removedEnumValue: string[] = [];
    public removedFunction: string[] = [];

    public get hasBreakingChange() {
        return this.removedOperationGroup.length > 0 ||
            this.removedOperation.length > 0 ||
            this.operationSignatureChange.length > 0 ||
            this.deletedClass.length > 0 ||
            this.classSignatureChange.length > 0 ||
            this.interfaceParamDelete.length > 0 ||
            this.interfaceParamAddRequired.length > 0 ||
            this.interfaceParamChangeRequired.length > 0 ||
            this.interfaceParamTypeChanged.length > 0 ||
            this.classParamDelete.length > 0 ||
            this.classParamChangeRequired.length > 0 ||
            this.typeAliasDeleteInherit.length > 0 ||
            this.typeAliasParamDelete.length > 0 ||
            this.typeAliasAddRequiredParam.length > 0 ||
            this.typeAliasParamChangeRequired.length > 0 ||
            this.removedEnum.length > 0 ||
            this.removedEnumValue.length > 0;
            this.removedFunction.length > 0;
    }

    public get hasFeature() {
        return this.addedOperationGroup.length > 0 ||
            this.addedOperation.length > 0 ||
            this.addedInterface.length > 0 ||
            this.addedClass.length > 0 ||
            this.addedTypeAlias.length > 0 ||
            this.interfaceAddOptionalParam.length > 0 ||
            this.interfaceParamTypeExtended.length > 0 ||
            this.typeAliasAddInherit.length > 0 ||
            this.typeAliasAddParam.length > 0 ||
            this.addedEnum.length > 0 ||
            this.addedEnumValue.length > 0;
            this.addedFunction.length > 0;
    }

    public getBreakingChangeItems(): string[] {
        let items: string[] = [];
        if (this.hasBreakingChange) {
            this.removedOperationGroup
                .concat(this.removedOperation)
                .concat(this.operationSignatureChange)
                .concat(this.deletedClass)
                .concat(this.classSignatureChange)
                .concat(this.interfaceParamDelete)
                .concat(this.interfaceParamAddRequired)
                .concat(this.interfaceParamChangeRequired)
                .concat(this.interfaceParamTypeChanged)
                .concat(this.classParamDelete)
                .concat(this.classParamChangeRequired)
                .concat(this.typeAliasDeleteInherit)
                .concat(this.typeAliasParamDelete)
                .concat(this.typeAliasAddRequiredParam)
                .concat(this.typeAliasParamChangeRequired)
                .concat(this.removedEnum)
                .concat(this.removedEnumValue)
                .concat(this.removedFunction)
                .forEach(e => {
                    items.push(e);
                });
        }
        return items;
    }

    public displayChangeLog(): string {
        const display: string[] = [];
        if (this.hasFeature) {
            display.push('**Features**');
            display.push('');
            this.addedOperationGroup
                .concat(this.addedOperation)
                .concat(this.addedInterface)
                .concat(this.addedClass)
                .concat(this.addedTypeAlias)
                .concat(this.interfaceAddOptionalParam)
                .concat(this.interfaceParamTypeExtended)
                .concat(this.typeAliasAddInherit)
                .concat(this.typeAliasAddParam)
                .concat(this.addedEnum)
                .concat(this.addedEnumValue)
                .concat(this.addedFunction)
                .forEach(e => {
                    display.push('  - ' + e);
                });
        }

        if (this.hasBreakingChange) {
            if (this.hasFeature) display.push('');
            display.push('**Breaking Changes**');
            display.push('');
            this.removedOperationGroup
                .concat(this.removedOperation)
                .concat(this.operationSignatureChange)
                .concat(this.deletedClass)
                .concat(this.classSignatureChange)
                .concat(this.interfaceParamDelete)
                .concat(this.interfaceParamAddRequired)
                .concat(this.interfaceParamChangeRequired)
                .concat(this.interfaceParamTypeChanged)
                .concat(this.classParamDelete)
                .concat(this.classParamChangeRequired)
                .concat(this.typeAliasDeleteInherit)
                .concat(this.typeAliasParamDelete)
                .concat(this.typeAliasAddRequiredParam)
                .concat(this.typeAliasParamChangeRequired)
                .concat(this.removedEnum)
                .concat(this.removedEnumValue)
                .concat(this.removedFunction)
                .forEach(e => {
                    display.push('  - ' + e);
                });
        }

        return display.join('\n');
    }
}

const findAddedOperationGroup = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addOperationGroup: string[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(operationGroup => {
        if (!metaDataOld.operationInterface[operationGroup]) {
            addOperationGroup.push('Added operation group ' + operationGroup);
        }
    });
    return addOperationGroup;
};

const findAddedOperation = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addOperation: string[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(operationGroup => {
        if (metaDataOld.operationInterface[operationGroup]) {
            const operationGroupFromOld = metaDataOld.operationInterface[operationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[operationGroup] as InterfaceDeclaration;
            operationGroupFromNew.methods.forEach(mNew => {
                let find = false;
                operationGroupFromOld.methods.forEach(mOld => {
                    if (mOld.name === mNew.name) {
                        find = true;
                        return;
                    }
                });
                if (!find) {
                    addOperation.push('Added operation ' + operationGroup + '.' + mNew.name);
                }
            })
        }
    });
    return addOperation;
};

const findAddedInterface = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addInterface: string[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (!metaDataOld.modelInterface[model]) {
            addInterface.push('Added Interface ' + model);
        }
    });
    return addInterface;
};

const findAddedClass = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addClass: string[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (!metaDataOld.classes[model]) {
            addClass.push('Added Class ' + model);
        }
    });
    return addClass;
};

const findAddedTypeAlias = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addModel: string[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (!metaDataOld.typeAlias[typeAlias]) {
            addModel.push('Added Type Alias ' + typeAlias);
        }
    });
    return addModel;
};

const findInterfaceAddOptinalParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceAddedParam: string[] = [];
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
                        interfaceAddedParam.push('Interface ' + model + ' has a new optional parameter ' + pNew.name);
                    }
                }
            });
        }
    });
    return interfaceAddedParam;
};

const findInterfaceParamTypeExtended = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceParamTypeExtended: string[] = [];
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
                                        interfaceParamTypeExtended.push(`Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`);
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

const findTypeAliasAddInherit = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasAddInherit: string[] = [];
    Object.keys(metaDataNew.typeAlias).forEach(typeAlias => {
        if (metaDataOld.typeAlias[typeAlias]) {
            const typeAliasFromOld = metaDataOld.typeAlias[typeAlias] as TypeAliasDeclaration;
            const typeAliasFromNew = metaDataNew.typeAlias[typeAlias] as TypeAliasDeclaration;
            if (typeAliasFromNew.type instanceof IntersectionDeclaration) {
                if (typeAliasFromNew.type.inherits) {
                    typeAliasFromNew.type.inherits.forEach(inherit => {
                        if (typeof inherit === 'string') {
                            if (typeAliasFromOld.type instanceof IntersectionDeclaration) {
                                if (typeAliasFromOld.type.inherits) {
                                    if (!typeAliasFromOld.type.inherits.includes(inherit)) {
                                        typeAliasAddInherit.push('Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias);
                                    }
                                } else {
                                    typeAliasAddInherit.push('Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias);
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                typeAliasAddInherit.push('Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias);
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                if (typeAliasFromOld.type !== inherit) {
                                    typeAliasAddInherit.push('Add parameters of ' + inherit + ' to TypeAlias ' + typeAlias);
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

const findTypeAliasAddParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasAddParam: string[] = [];
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
                                        typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                    }
                                } else {
                                    typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
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
                                    typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else {
                                typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                            }
                        } else if (typeof typeAliasFromOld.type === 'string') {
                            typeAliasAddParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                        }
                    });
                }
            }
        }
    });
    return typeAliasAddParam;
};

const findAddedEnum = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addedEnum: string[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (!metaDataOld.enums[e]) {
            addedEnum.push('Added Enum ' + e);
        }
    });
    return addedEnum;
};

const findAddedEnumValue = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addedEnumValue: string[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (metaDataOld.enums[e]) {
            const enumOld = metaDataOld.enums[e] as EnumDeclaration;
            const enumNew = metaDataNew.enums[e] as EnumDeclaration;
            enumNew.members.forEach(v => {
                if (!enumOld.members.includes(v)) {
                    addedEnumValue.push('Enum ' + e + ' has a new value ' + v);
                }
            });
        }
    });
    return addedEnumValue;
};

const findAddedFunction = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const addedFunction: string[] = [];
    Object.keys(metaDataNew.functions).forEach(e => {
        if (!metaDataOld.functions[e]) {
            addedFunction.push(`Added function ${e}`);
        }
    });
    return addedFunction;
};


const findRemovedOperationGroup = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const removedOperationGroup: string[] = [];
    Object.keys(metaDataOld.operationInterface).forEach(operationGroup => {
        if (!metaDataNew.operationInterface[operationGroup]) {
            removedOperationGroup.push('Removed operation group ' + operationGroup);
        }
    });
    return removedOperationGroup;
};

const findRemovedOperation = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const removedOperation: string[] = [];
    Object.keys(metaDataOld.operationInterface).forEach(operationGroup => {
        if (metaDataNew.operationInterface[operationGroup]) {
            const operationGroupFromOld = metaDataOld.operationInterface[operationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[operationGroup] as InterfaceDeclaration;
            operationGroupFromOld.methods.forEach(mOld => {
                let find = false;
                operationGroupFromNew.methods.forEach(mNew => {
                    if (mOld.name === mNew.name) {
                        find = true;
                        return;
                    }
                });
                if (!find) {
                    removedOperation.push('Removed operation ' + operationGroup + '.' + mOld.name);
                }
            })
        }
    });
    return removedOperation;
};

const findOperationSignatureChange = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const operationSignatureChange: string[] = [];
    Object.keys(metaDataNew.operationInterface).forEach(operationGroup => {
        if (metaDataOld.operationInterface[operationGroup]) {
            const operationGroupFromOld = metaDataOld.operationInterface[operationGroup] as InterfaceDeclaration;
            const operationGroupFromNew = metaDataNew.operationInterface[operationGroup] as InterfaceDeclaration;
            operationGroupFromNew.methods.forEach(mNew => {
                operationGroupFromOld.methods.forEach(mOld => {
                    if (mOld.name === mNew.name) {
                        const parametersOld = mOld.parameters;
                        const parametersNew = mNew.parameters;
                        if (parametersNew.length !== parametersOld.length) {
                            operationSignatureChange.push('Operation ' + operationGroup + '.' + mNew.name + ' has a new signature');
                        } else {
                            for (let index = 0; index < parametersNew.length; index++) {
                                const pOld = parametersOld[index];
                                const pNew = parametersNew[index];
                                if (pOld.type !== pNew.type || pOld.isOptional !== pNew.isOptional) {
                                    operationSignatureChange.push('Operation ' + operationGroup + '.' + mNew.name + ' has a new signature');
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

const findDeletedClass = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const deletedClass: string[] = [];
    Object.keys(metaDataOld.classes).forEach(model => {
        if (!metaDataNew.classes[model]) {
            deletedClass.push('Deleted Class ' + model);
        }
    });
    return deletedClass;
};

const findClassSignatureChange = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const classSignatureChange: string[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (metaDataOld.classes[model]) {
            const modelFromOld = metaDataOld.classes[model] as ClassDeclaration;
            const modelFromNew = metaDataNew.classes[model] as ClassDeclaration;
            const constructorOld = modelFromOld.ctor;
            const constructorNew = modelFromNew.ctor;
            if (constructorOld === undefined && constructorNew === undefined) return;
            if (constructorOld === undefined || constructorNew === undefined) {
                classSignatureChange.push('Class ' + model + ' has a new signature');
                return;
            }
            const parametersOld = constructorOld.parameters;
            const parametersNew = constructorNew.parameters;
            if (parametersNew.length !== parametersOld.length) {
                classSignatureChange.push('Class ' + model + ' has a new signature');
            } else {
                for (let index = 0; index < parametersNew.length; index++) {
                    const pOld = parametersOld[index];
                    const pNew = parametersNew[index];
                    if (pOld.type !== pNew.type || pOld.isOptional !== pNew.isOptional) {
                        classSignatureChange.push('Class ' + model + ' has a new signature');
                        return;
                    }
                }
            }
        }
    });
    return classSignatureChange;
};

const findInterfaceParamDelete = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceDeleteParam: string[] = [];
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
                    interfaceDeleteParam.push('Interface ' + model + ' no longer has parameter ' + pOld.name);
                }
            });
        }
    });
    return interfaceDeleteParam;
};

const findInterfaceParamAddRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceAddedParam: string[] = [];
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
                        interfaceAddedParam.push('Interface ' + model + ' has a new required parameter ' + pNew.name);
                    }
                }
            });
        }
    });
    return interfaceAddedParam;
};

const findInterfaceParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceParamChangeRequired: string[] = [];
    Object.keys(metaDataNew.modelInterface).forEach(model => {
        if (metaDataOld.modelInterface[model]) {
            const modelFromOld = metaDataOld.modelInterface[model] as InterfaceDeclaration;
            const modelFromNew = metaDataNew.modelInterface[model] as InterfaceDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (!pNew.isOptional) {
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            if (pOld.isOptional) {
                                interfaceParamChangeRequired.push('Parameter ' + pNew.name + ' of interface ' + model + ' is now required');
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

const findInterfaceParamTypeChanged = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const interfaceParamTypeChanged: string[] = [];
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
                                            interfaceParamTypeChanged.push(`Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`);
                                            break;
                                        }
                                    }
                                }
                            } else {
                                interfaceParamTypeChanged.push(`Type of parameter ${pNew.name} of interface ${model} is changed from ${pOld.type} to ${pNew.type}`);
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

const findClassParamDelete = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const classDeleteParam: string[] = [];
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
                    classDeleteParam.push('Class ' + model + ' no longer has parameter ' + pOld.name);
                }
            });
        }
    });
    return classDeleteParam;
};

const findClassParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const classParamChangeRequired: string[] = [];
    Object.keys(metaDataNew.classes).forEach(model => {
        if (metaDataOld.classes[model]) {
            const modelFromOld = metaDataOld.classes[model] as ClassDeclaration;
            const modelFromNew = metaDataNew.classes[model] as ClassDeclaration;
            modelFromNew.properties.forEach(pNew => {
                if (!pNew.isOptional) {
                    modelFromOld.properties.forEach(pOld => {
                        if (pNew.name === pOld.name) {
                            if (pOld.isOptional) {
                                classParamChangeRequired.push('Parameter ' + pNew.name + ' of class ' + model + ' is now required');
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

const findTypeAliasDeleteInherit = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasDeleteInherit: string[] = [];
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
                                        typeAliasDeleteInherit.push('Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias);
                                    }
                                } else {
                                    typeAliasDeleteInherit.push('Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias);
                                }
                            } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                                typeAliasDeleteInherit.push('Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias);
                            } else if (typeof typeAliasFromNew.type === 'string') {
                                if (typeAliasFromNew.type !== inherit) {
                                    typeAliasDeleteInherit.push('Delete parameters of ' + inherit + ' in TypeAlias ' + typeAlias);
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

const findTypeAliasDeleteParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasDeleteParam: string[] = [];
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
                                        typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name);
                                    }
                                } else {
                                    typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name);
                                }
                            } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromNew.type.properties.forEach(pNew => {
                                    if (pOld.name === pNew.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name);
                                }
                            } else if (typeof typeAliasFromNew.type === 'string') {
                                typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' no longer has parameter ' + pOld.name);
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
                                    typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name);
                                }
                            } else {
                                typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name);
                            }
                        } else if (typeAliasFromNew.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromNew.type.properties.forEach(pNew => {
                                if (pOld.name === pNew.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name);
                            }
                        } else if (typeof typeAliasFromNew.type === 'string') {
                            typeAliasDeleteParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pOld.name);
                        }
                    });
                }
            }
        }
    });
    return typeAliasDeleteParam;
};

const findTypeAliasAddRequiredParam = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasAddRequiredParam: string[] = [];
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
                                        typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                    }
                                } else {
                                    typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                let find = false;
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name) {
                                        find = true;
                                    }
                                });
                                if (!find) {
                                    typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else if (typeof typeAliasFromOld.type === 'string') {
                                typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
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
                                    typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                                }
                            } else {
                                typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            let find = false;
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name) {
                                    find = true;
                                }
                            });
                            if (!find) {
                                typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                            }
                        } else if (typeof typeAliasFromOld.type === 'string') {
                            typeAliasAddRequiredParam.push('Type Alias ' + typeAlias + ' has a new parameter ' + pNew.name);
                        }
                    });
                }
            }
        }
    });
    return typeAliasAddRequiredParam;
};

const findTypeAliasParamChangeRequired = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const typeAliasParamChangeRequired: string[] = [];
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
                                                typeAliasParamChangeRequired.push('Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required');
                                            }
                                        });
                                    });
                                }
                            } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                                typeAliasFromOld.type.properties.forEach(pOld => {
                                    if (pNew.name === pOld.name && pOld.isOptional) {
                                        typeAliasParamChangeRequired.push('Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required');
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
                                            typeAliasParamChangeRequired.push('Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required');
                                        }
                                    });
                                });
                            }
                        } else if (typeAliasFromOld.type instanceof TypeLiteralDeclaration) {
                            typeAliasFromOld.type.properties.forEach(pOld => {
                                if (pNew.name === pOld.name && pOld.isOptional) {
                                    typeAliasParamChangeRequired.push('Parameter ' + pNew.name + ' of Type Alias ' + typeAlias + ' is now required');
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

const findRemovedEnum = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const removedEnum: string[] = [];
    Object.keys(metaDataOld.enums).forEach(e => {
        if (!metaDataNew.enums[e]) {
            removedEnum.push('Removed Enum ' + e);
        }
    });
    return removedEnum;
};

const findRemovedEnumValue = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const removedEnumValue: string[] = [];
    Object.keys(metaDataNew.enums).forEach(e => {
        if (metaDataOld.enums[e]) {
            const enumOld = metaDataOld.enums[e] as EnumDeclaration;
            const enumNew = metaDataNew.enums[e] as EnumDeclaration;
            enumOld.members.forEach(v => {
                if (!enumNew.members.includes(v)) {
                    removedEnumValue.push('Enum ' + e + ' no longer has value ' + v);
                }
            });
        }
    });
    return removedEnumValue;
};

const findRemovedFunction = (metaDataOld: TSExportedMetaData, metaDataNew: TSExportedMetaData): string[] => {
    const removedFunction: string[] = [];
    Object.keys(metaDataOld.functions).forEach(e => {
        if (!metaDataNew.functions[e]) {
            removedFunction.push('Removed function ' + e);
        }
    });
    return removedFunction;
};


export const changelogGenerator = (metaDataOld: TSExportedMetaData, metadataNew: TSExportedMetaData): Changelog => {
    const changLog = new Changelog();

    // features
    changLog.addedOperationGroup = findAddedOperationGroup(metaDataOld, metadataNew);
    changLog.addedOperation = findAddedOperation(metaDataOld, metadataNew);
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
    changLog.removedOperationGroup = findRemovedOperationGroup(metaDataOld, metadataNew);
    changLog.removedOperation = findRemovedOperation(metaDataOld, metadataNew);
    changLog.operationSignatureChange = findOperationSignatureChange(metaDataOld, metadataNew);
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
