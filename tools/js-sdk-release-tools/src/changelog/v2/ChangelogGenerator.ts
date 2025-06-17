import * as format from 'string-template';
import { DetectContext, DetectResult } from './DifferenceDetector.js';
import { DiffLocation, DiffPair, DiffReasons } from 'typescript-codegen-breaking-change-detector';
import { InterfaceDeclaration, Node, PropertySignature, SyntaxKind } from 'ts-morph';
import { SDKType } from '../../common/types.js';

export enum ChangelogItemCategory {
  /** features */
  OperationGroupAdded = 0,
  OperationAdded = 1,
  ModelAdded = 2,
  ClassAdded = 3,
  TypeAliasAdded = 4,
  ModelOptionalPropertyAdded = 5,
  // TODO: interfaceParamTypeExtended?
  // TODO: type alias type change?
  EnumAdded = 6,
  EnumValueAdded = 7,
  FunctionAdded = 8,
  // TODO
  FunctionOverloadAdded = 9,

  /** breaking changes */
  OperationGroupRemoved = 1000,
  OperationRemoved = 1001,
  OperationSignatureChanged = 1002,
  ClassRemoved = 1003,
  ClassChanged = 1004,
  ClassPropertyRemoved = 1005,
  ClassPropertyOptionalToRequired = 1006,
  ModelRemoved = 1007,
  ModelRequiredPropertyAdded = 1008,
  ModelPropertyTypeChanged = 1009,
  ModelPropertyRemoved = 1010,
  ModelPropertyOptionalToRequired = 1011,
  ModelPropertyRequiredToOptional = 1012,
  TypeAliasRemoved = 1013,
  TypeAliasTypeChanged = 1014,
  FunctionRemoved = 1015,
  FunctionOverloadRemoved = 1016,
  EnumRemoved = 1017,
  EnumValueRemoved = 1018,
}

export interface ChangelogItems {
  features: Map<ChangelogItemCategory, string[]>;
  breakingChanges: Map<ChangelogItemCategory, string[]>;
}

// TODO: use consistent upper/lower case
// TODO: use consistent word: removed/deleted
// TODO: consider enum get wider or narrower
export class ChangelogGenerator {
  /** operation group */
  private operationGroupAddedTemplate = 'Added operation group {interfaceName}';
  private operationGroupRemovedTemplate = 'Removed operation group {interfaceName}';

  /** operation */
  private operationAddedTemplate = 'Added operation {interfaceName}.{signatureName}';
  private operationRemovedTemplate = 'Removed operation {interfaceName}.{signatureName}';
  // TODO: handle high-level-client to modular-client migration
  private operationSignatureChangedTemplate = 'Operation {interfaceName}.{signatureName} has a new signature';

  /** model */
  private modelAddedTemplate = 'Added Interface {interfaceName}';
  // NOTE: not in v1
  private modelRemovedTemplate = 'Removed Interface {interfaceName}';
  private modelOptionalPropertyAddedTemplate = 'Interface {interfaceName} has a new optional parameter {propertyName}';
  private modelRequiredPropertyAddedTemplate = 'Interface {interfaceName} has a new required parameter {propertyName}';
  // NOTE: note in v1 except union type
  // TODO: should be called 'property'
  private modelPropertyTypeChangedTemplate =
    'Type of parameter {newPropertyName} of interface {interfaceName} is changed from {oldPropertyType} to {newPropertyType}';
  private modelPropertyRemovedTemplate = 'Interface {interfaceName} no longer has parameter {parameterName}';
  private modelPropertyOptionalToRequired = 'Parameter {propertyName} of interface {interfaceName} is now required';

  /** class */
  private classAddedTemplate = 'Added Class {className}';
  private classRemovedTemplate = 'Deleted Class {className}';
  private classPropertyRemovedTemplate = 'Class {className} no longer has parameter {propertyName}';
  private classPropertyOptionalToRequired = 'Parameter {propertyName} of class {className} is now required';
  // NOTE: not detected in v1 except constructor and it's parameters
  private classChanged = 'Class {className} has a new signature';

  // TODO: add detection for extended type
  /** type alias */
  private typeAliasAddedTemplate = 'Added type alias {typeName}';
  // NOTE: not in v1
  private typeAliasRemovedTemplate = 'Removed type alias {typeName}';
  private typeAliasTypeChangedTemplate = 'Type of type alias {typeName} has been changed';

  /** function */
  private functionAddedTemplate = 'Added function {functionName}';
  private functionRemovedTemplate = 'Removed function {functionName}';

  // TODO
  /** enum */

  private changelogItems: ChangelogItems = {
    features: new Map<ChangelogItemCategory, string[]>(),
    breakingChanges: new Map<ChangelogItemCategory, string[]>(),
  };

  constructor(
    private detectContext: DetectContext,
    private detectResult: DetectResult
  ) {}

  public generateContent(): string {
    this.detectResult.interfaces.forEach((diffPairs, name) => {
      this.generateForInterfaces(name, diffPairs);
      this.generateForClasses(name, diffPairs);
      this.generateForTypeAliases(name, diffPairs);
      // TODO: add enum support
      //   this.generateForEnums(name, diffPairs);
      this.generateForFunctions(name, diffPairs);
    });
    const content = this.generateContentCore();
    return content;
  }

  public get hasBreakingChange(): boolean {
    return this.changelogItems.breakingChanges.size > 0;
  }

  public get hasFeature(): boolean {
    return this.changelogItems.features.size > 0;
  }

  private getItemsFromCategoryMap(map: Map<ChangelogItemCategory, string[]>): string[] {
    const items: string[] = [];
    [...map.keys()].sort().forEach((category) => {
      const categoryItems = map.get(category);
      if (!categoryItems || categoryItems.length === 0) return;
      categoryItems.forEach((item) => items.push(item));
    });
    return items;
  }

  private generateSection(items: string[], title: string): string {
    if (items.length === 0) return '';
    let content = `### ${title}\n`;
    content += items.map((i) => `  - ${i}`).join('\n');
    content += '\n';
    return content;
  }

  private generateContentCore() {
    let content = '';
    const featureItems = this.getItemsFromCategoryMap(this.changelogItems.features);
    content += this.generateSection(featureItems, 'Features Added');

    const breakingChangeItems = this.getItemsFromCategoryMap(this.changelogItems.breakingChanges);
    content += this.generateSection(breakingChangeItems, 'Breaking Changes');

    return content;
  }

  private addChangelogItem(category: ChangelogItemCategory, message: string) {
    if (category < 1000) {
      this.changelogItems.features.get(category)?.push(message) ||
        this.changelogItems.features.set(category, [message]);
    } else {
      this.changelogItems.breakingChanges.get(category)?.push(message) ||
        this.changelogItems.breakingChanges.set(category, [message]);
    }
  }

  private generateForFunctions(functionName: string, diffPairs: DiffPair[]): void {
    diffPairs.forEach((p) => {
      // function added
      if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = format(this.functionAddedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionAdded, message);
      }
      // function removed
      if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.functionRemovedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionRemoved, message);
      }
      // TODO: add more
    });
  }

  private generateForTypeAliases(typeName: string, diffPairs: DiffPair[]): void {
    diffPairs.forEach((p) => {
      // type alias added
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = format(this.typeAliasAddedTemplate, { typeName });
        this.addChangelogItem(ChangelogItemCategory.TypeAliasAdded, message);
      }
      // NOTE: not detected in v1
      // type alias removed
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.typeAliasRemovedTemplate, { typeName });
        this.addChangelogItem(ChangelogItemCategory.TypeAliasRemoved, message);
      }
      // type alias type changed
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
        const message = format(this.typeAliasTypeChangedTemplate, { typeName });
        this.addChangelogItem(ChangelogItemCategory.TypeAliasTypeChanged, message);
      }
    });
  }

  private generateForClasses(className: string, diffPairs: DiffPair[]): void {
    diffPairs.forEach((p) => {
      // class added
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = format(this.classAddedTemplate, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassAdded, message);
      }
      // class removed
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.classRemovedTemplate, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassRemoved, message);
      }
      // class type changed
      // NOTE: not detected in v1 except constructor and it's parameters
      if (
        p.location === DiffLocation.Signature &&
        // TODO: from v1, which treat adding constructor as breaking, is it true?
        (this.hasReasons(p.reasons, DiffReasons.Added) || this.hasReasons(p.reasons, DiffReasons.Removed))
      ) {
        const message = format(this.classChanged, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassChanged, message);
      }
      // class property removed
      if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.classPropertyRemovedTemplate, {
          className,
          propertyName: p.target!.name,
        });
        this.addChangelogItem(ChangelogItemCategory.ClassPropertyRemoved, message);
      }
      // class property optional to required
      // TODO: add new diff reason for optional to required
      // NOTE: not supported in v2
      if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.NotComparable)) {
        const message = format(this.classPropertyOptionalToRequired, {
          className,
          propertyName: p.source!.name,
        });
        this.addChangelogItem(ChangelogItemCategory.ClassPropertyOptionalToRequired, message);
      }
    });
  }

  private generateForInterfaces(interfaceName: string, diffPairs: DiffPair[]): void {
    const isOperationGroupInterface = (diffPair: DiffPair) => {
      const source = diffPair.source?.node.asKind(SyntaxKind.InterfaceDeclaration);
      const target = diffPair.target?.node.asKind(SyntaxKind.InterfaceDeclaration);
      return (
        (source && this.isOperationGroupInterfaceCore(source, this.detectContext.sdkTypes.source)) ||
        (target && this.isOperationGroupInterfaceCore(target, this.detectContext.sdkTypes.target))
      );
    };
    /** operation group and operationchanges */
    diffPairs.forEach((p) => {
      if (isOperationGroupInterface(p)) {
        /** operation group changes */
        // operation group added
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.operationGroupAddedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.OperationGroupAdded, message);
        }
        // operation group removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.operationGroupRemovedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.OperationGroupRemoved, message);
        }

        /** operation changes */
        // operation added
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.operationAddedTemplate, { interfaceName, signatureName: p.source!.name });
          this.addChangelogItem(ChangelogItemCategory.OperationAdded, message);
        }
        // operation removed
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.operationRemovedTemplate, { interfaceName, signatureName: p.source!.name });
          this.addChangelogItem(ChangelogItemCategory.OperationRemoved, message);
        }
        // operation signature's parameter type changed
        if (p.location === DiffLocation.Parameter && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's parameter list changed
        if (
          p.location === DiffLocation.Signature_ParameterList &&
          this.hasReasons(p.reasons, DiffReasons.CountChanged)
        ) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: p.target!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's return type changed
        // NOTE: not detected in v1
        if (p.location === DiffLocation.Signature_ReturnType && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: p.target!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation parameter
        // TODO: v1 make it a breaking change when operation optional/required changed, while v2 only consider required-> optional is breaking change
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's parameter required/optional changed
        // NOTE: not detected in v2
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.NotComparable)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
      } else {
        /** model changes */
        // model added
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelAddedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.ModelAdded, message);
        }
        // NOTE: not detected in v1
        // model removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.modelRemovedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.ModelRemoved, message);
        }
        // model's optional property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelOptionalPropertyAddedTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.ModelOptionalPropertyAdded, message);
        }
        // model's required property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelRequiredPropertyAddedTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.ModelRequiredPropertyAdded, message);
        }
        // model's property type changed
        // NOTE: not detected in v1 except for union type
        // NOTE: discuss if extend union type to be breaking or not
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const getTypeText = (node: Node) =>
            node.asKindOrThrow(SyntaxKind.PropertySignature).getTypeNodeOrThrow().getText();
          const message = format(this.modelPropertyTypeChangedTemplate, {
            interfaceName,
            newPropertyName: p.source!.name,
            oldPropertyType: getTypeText(p.target!.node),
            newPropertyType: getTypeText(p.source!.node),
          });
          this.addChangelogItem(ChangelogItemCategory.ModelPropertyTypeChanged, message);
        }
        // model's property removed
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.modelPropertyRemovedTemplate, { interfaceName, propertyName: p.target!.name });
          this.addChangelogItem(ChangelogItemCategory.ModelPropertyRemoved, message);
        }
        // TODO: not supported in v2
        // TODO: add new diff reason for optional to required
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.NotComparable)) {
          const message = format(this.modelPropertyOptionalToRequired, { interfaceName, propertyName: p.source!.name });
          this.addChangelogItem(ChangelogItemCategory.ModelPropertyOptionalToRequired, message);
        }
      }
    });
  }

  // TODO: improve, only support one reason in expectedReasons
  private hasReasons(actualReasons: DiffReasons, expectedReasons: DiffReasons) {
    return (actualReasons & expectedReasons) > 0;
  }

  private isFunctionType(property: PropertySignature) {
    const kind = property.getTypeNode()?.getKind();
    return kind === SyntaxKind.FunctionType;
  }

  private isOperationGroupInterfaceCore(node: InterfaceDeclaration, sdkType: SDKType) {
    const memberCount = node.getMembers().length;
    switch (sdkType) {
      case SDKType.HighLevelClient:
        return memberCount > 0 && memberCount === node.getMethods().length;
      case SDKType.ModularClient:
        return memberCount > 0 && memberCount === node.getProperties().filter(this.isFunctionType).length;
      case SDKType.RestLevelClient:
        return false;
      default:
        throw new Error(`Unsupported SDK type ${sdkType} to distingush operation interface.`);
    }
  }
}
