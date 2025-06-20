import template from 'string-template';
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
  EnumMemberAdded = 7,
  // NOTE: include function and overload function
  FunctionAdded = 8,

  /** breaking changes */
  OperationGroupRemoved = 1000,
  OperationRemoved = 1001,
  OperationSignatureChanged = 1002,
  ClassRemoved = 1003,
  ClassChanged = 1004,
  ClassPropertyRemoved = 1005,
  ClassPropertyOptionalToRequired = 1006,
  ModelRemoved = 1008,
  ModelRequiredPropertyAdded = 1009,
  ModelPropertyTypeChanged = 1010,
  ModelPropertyRemoved = 1011,
  ModelPropertyOptionalToRequired = 1012,
  ModelPropertyRequiredToOptional = 1013,
  TypeAliasRemoved = 1014,
  TypeAliasTypeChanged = 1015,
  // NOTE: include function and overload function
  FunctionRemoved = 1016,
  EnumRemoved = 1017,
  EnumMemberRemoved = 1018,
}

export interface ChangelogResult {
  hasBreakingChange: boolean;
  hasFeature: boolean;
  changelogItems: ChangelogItems;
  content: string;
  breakingChangeItems?: string[];
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
  private modelPropertyRemovedTemplate = 'Interface {interfaceName} no longer has parameter {propertyName}';
  private modelPropertyOptionalToRequiredTemplate = 'Parameter {propertyName} of interface {interfaceName} is now required';

  /** class */
  private classAddedTemplate = 'Added Class {className}';
  private classRemovedTemplate = 'Deleted Class {className}';
  private classPropertyRemovedTemplate = 'Class {className} no longer has parameter {propertyName}';
  private classPropertyOptionalToRequiredTemplate = 'Parameter {propertyName} of class {className} is now required';
  // NOTE: not detected in v1 except constructor and it's parameters
  private classChangedTemplate = 'Class {className} has a new signature';

  // TODO: add detection for extended type
  /** type alias */
  private typeAliasAddedTemplate = 'Added type alias {typeName}';
  // NOTE: not in v1
  private typeAliasRemovedTemplate = 'Removed type alias {typeName}';
  private typeAliasTypeChangedTemplate = 'Type of type alias {typeName} has been changed';

  /** function */
  private functionAddedTemplate = 'Added function {functionName}';
  private functionRemovedTemplate = 'Removed function {functionName}';

  // TODO: detect enum member's initializer change
  /** enum */
  private enumAddedTemplate = 'Added Enum {enumName}';
  private enumRemovedTemplate = 'Removed Enum {enumName}';
  private enumMemberAddedTemplate = 'Enum {enumName} has a new value {valueName}';
  private enumMemberRemovedTemplate = 'Enum {enumName} no longer has value {valueName}';

  private changelogItems: ChangelogItems = {
    features: new Map<ChangelogItemCategory, string[]>(),
    breakingChanges: new Map<ChangelogItemCategory, string[]>(),
  };

  constructor(
    private detectContext: DetectContext,
    private detectResult: DetectResult
  ) {}

  // TODO: add enum support
  public generate(): ChangelogResult {
    this.detectResult.interfaces.forEach((diffPairs, name) => {
      this.generateForInterfaces(diffPairs, name);
    });
    this.detectResult.classes.forEach((diffPairs, name) => {
      this.generateForClasses(diffPairs, name);
    });
    this.detectResult.typeAliases.forEach((diffPairs, name) => {
      this.generateForTypeAliases(diffPairs, name);
    });
    this.detectResult.functions.forEach((diffPairs, name) => {
      this.generateForFunctions(diffPairs, name);
    });
    this.detectResult.enums.forEach((diffPairs, name) => {
      this.generateForEnums(diffPairs, name);
    });
    const content = this.generateContentCore();
    const hasBreakingChange = this.hasBreakingChange();
    const hasFeature = this.hasFeature();
    const changelogItems = this.getChangelogItems();
    const breakingChangeItems = Array.from(this.changelogItems.breakingChanges).flatMap(([_, items]) => items);
    return { content, changelogItems, hasBreakingChange, hasFeature, breakingChangeItems };
  }

  private hasBreakingChange(): boolean {
    return this.changelogItems.breakingChanges.size > 0;
  }

  private hasFeature(): boolean {
    return this.changelogItems.features.size > 0;
  }

  private getChangelogItems(): ChangelogItems {
    return this.changelogItems;
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
      const messages = this.changelogItems.features.get(category) || [];
      if (!messages.includes(message)) messages.push(message);
      this.changelogItems.features.set(category, messages);
    } else {
      const messages = this.changelogItems.breakingChanges.get(category) || [];
      if (!messages.includes(message)) messages.push(message);
      this.changelogItems.breakingChanges.set(category, messages);
    }
  }

  private generateForFunctions(diffPairs: DiffPair[], functionName: string): void {
    diffPairs.forEach((p) => {
      // function added
      if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.functionAddedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionAdded, message);
      }
      // function removed
      if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.functionRemovedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionRemoved, message);
      }
      // overload function added
      if (p.location === DiffLocation.Signature_Overload && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.functionAddedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionAdded, message);
      }
      // overload function removed
      if (p.location === DiffLocation.Signature_Overload && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.functionRemovedTemplate, { functionName });
        this.addChangelogItem(ChangelogItemCategory.FunctionRemoved, message);
      }
    });
  }

  private generateForEnums(diffPairs: DiffPair[], enumName: string): void {
    diffPairs.forEach((p) => {
      // enum added
      if (p.location === DiffLocation.Enum && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.enumAddedTemplate, { enumName });
        this.addChangelogItem(ChangelogItemCategory.EnumAdded, message);
      }
      // enum removed
      if (p.location === DiffLocation.Enum && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.enumRemovedTemplate, { enumName });
        this.addChangelogItem(ChangelogItemCategory.EnumRemoved, message);
      }
      // overload enum member added
      if (p.location === DiffLocation.EnumMember && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.enumMemberAddedTemplate, {
          enumName,
          valueName: p.source!.node.asKindOrThrow(SyntaxKind.EnumMember).getName(),
        });
        this.addChangelogItem(ChangelogItemCategory.EnumMemberAdded, message);
      }
      // overload enum member removed
      if (p.location === DiffLocation.EnumMember && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.enumMemberRemovedTemplate, {
          enumName,
          valueName: p.target!.node.asKindOrThrow(SyntaxKind.EnumMember).getName(),
        });
        this.addChangelogItem(ChangelogItemCategory.EnumMemberRemoved, message);
      }
    });
  }

  private generateForTypeAliases(diffPairs: DiffPair[], typeName: string): void {
    diffPairs.forEach((p) => {
      // type alias added
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.typeAliasAddedTemplate, { typeName });
        this.addChangelogItem(ChangelogItemCategory.TypeAliasAdded, message);
      }
      // NOTE: not detected in v1
      // type alias removed
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.typeAliasRemovedTemplate, { typeName });
        this.addChangelogItem(ChangelogItemCategory.TypeAliasRemoved, message);
      }
      // type alias type changed
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
        // TODO: improve changelog to tell the most outer impacted declarations
        const oldTypeText = p.target!.node.getText();
        const newTypeText = p.source!.node.getText();
        if (oldTypeText !== newTypeText) {
          const message = template(this.typeAliasTypeChangedTemplate, { typeName });
          this.addChangelogItem(ChangelogItemCategory.TypeAliasTypeChanged, message);
        }
      }
    });
  }

  private generateForClasses(diffPairs: DiffPair[], className: string): void {
    console.log('🚀 ~ ChangelogGenerator ~ diffPairs.forEach ~ diffPairs:', diffPairs);
    diffPairs.forEach((p) => {
      // class added
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = template(this.classAddedTemplate, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassAdded, message);
      }
      // class removed
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.classRemovedTemplate, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassRemoved, message);
      }
      // class type changed
      // NOTE: not detected in v1 except constructor and it's parameters
      if (
        p.location === DiffLocation.Signature &&
        // TODO: from v1, which treat adding constructor as breaking, is it true?
        (this.hasReasons(p.reasons, DiffReasons.Added) || this.hasReasons(p.reasons, DiffReasons.Removed))
      ) {
        const message = template(this.classChangedTemplate, { className });
        this.addChangelogItem(ChangelogItemCategory.ClassChanged, message);
      }
      // class property removed
      if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = template(this.classPropertyRemovedTemplate, {
          className,
          propertyName: p.target!.name,
        });
        this.addChangelogItem(ChangelogItemCategory.ClassPropertyRemoved, message);
      }
      // class property optional to required (baseline to current)
      if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
        const message = template(this.classPropertyOptionalToRequiredTemplate, {
          className,
          propertyName: p.source!.name,
        });
        this.addChangelogItem(ChangelogItemCategory.ClassPropertyOptionalToRequired, message);
      }
    });
  }

  private generateForInterfaces(diffPairs: DiffPair[], interfaceName: string): void {
    const isOperationGroupInterface = () => {
      const source = this.detectContext.context.current.getInterface(interfaceName);
      const target = this.detectContext.context.baseline.getInterface(interfaceName);
      const isOperationGroup =
        (source && this.isOperationGroupInterfaceCore(source, this.detectContext.sdkTypes.source)) ||
        (target && this.isOperationGroupInterfaceCore(target, this.detectContext.sdkTypes.target));
      return isOperationGroup;
    };
    /** operation group and operationchanges */
    diffPairs.forEach((p) => {
      if (isOperationGroupInterface()) {
        /** operation group changes */
        // operation group added
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = template(this.operationGroupAddedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.OperationGroupAdded, message);
        }
        // operation group removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = template(this.operationGroupRemovedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.OperationGroupRemoved, message);
        }

        /** operation changes */
        // operation added
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = template(this.operationAddedTemplate, { interfaceName, signatureName: p.source!.name });
          this.addChangelogItem(ChangelogItemCategory.OperationAdded, message);
        }
        // operation removed
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = template(this.operationRemovedTemplate, { interfaceName, signatureName: p.target!.name });
          this.addChangelogItem(ChangelogItemCategory.OperationRemoved, message);
        }
        // operation signature's parameter type changed
        if (p.location === DiffLocation.Parameter && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const signatureName = () => {
            const parent = p.source!.node.asKindOrThrow(SyntaxKind.Parameter).getParentOrThrow();
            switch (parent.getKind()) {
              case SyntaxKind.MethodSignature:
                return parent.asKindOrThrow(SyntaxKind.MethodSignature).getName();
              case SyntaxKind.FunctionType:
                return parent
                  .asKindOrThrow(SyntaxKind.FunctionType)
                  .getParentOrThrow()
                  .asKindOrThrow(SyntaxKind.PropertySignature)
                  .getName();
            }
          };
          const message = template(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: signatureName(),
          });
          console.log('🚀 ~ operation sig change:', message);
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's parameter list changed
        if (
          p.location === DiffLocation.Signature_ParameterList &&
          this.hasReasons(p.reasons, DiffReasons.CountChanged)
        ) {
          const message = template(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: p.target!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's return type changed
        // NOTE: not detected in v1
        if (p.location === DiffLocation.Signature_ReturnType && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const message = template(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: p.target!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation parameter
        // TODO: v1 make it a breaking change when operation optional/required changed, while v2 only consider required-> optional is breaking change
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
          const message = template(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          this.addChangelogItem(ChangelogItemCategory.OperationSignatureChanged, message);
        }
        // operation signature's parameter required/optional changed
        // NOTE: not detected in v2
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.NotComparable)) {
          const message = template(this.operationSignatureChangedTemplate, {
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
          const message = template(this.modelAddedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.ModelAdded, message);
        }
        // NOTE: not detected in v1
        // model removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = template(this.modelRemovedTemplate, { interfaceName });
          this.addChangelogItem(ChangelogItemCategory.ModelRemoved, message);
        }
        // model's optional property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = template(this.modelOptionalPropertyAddedTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
          this.addChangelogItem(ChangelogItemCategory.ModelOptionalPropertyAdded, message);
        }
        // model's required property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = template(this.modelRequiredPropertyAddedTemplate, {
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
          const oldPropertyTypeText = getTypeText(p.target!.node);
          const newPropertyTypeText = getTypeText(p.source!.node);
          // TODO: improve changelog to tell the most outer impacted declarations
          if (oldPropertyTypeText !== newPropertyTypeText) {
            const message = template(this.modelPropertyTypeChangedTemplate, {
              interfaceName,
              newPropertyName: p.source!.name,
              oldPropertyType: oldPropertyTypeText,
              newPropertyType: newPropertyTypeText,
            });
            this.addChangelogItem(ChangelogItemCategory.ModelPropertyTypeChanged, message);
          }
        }
        // model's property removed
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = template(this.modelPropertyRemovedTemplate, { interfaceName, propertyName: p.target!.name });
          this.addChangelogItem(ChangelogItemCategory.ModelPropertyRemoved, message);
        }
        // model's property optional to required
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
          const message = template(this.modelPropertyOptionalToRequiredTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
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
