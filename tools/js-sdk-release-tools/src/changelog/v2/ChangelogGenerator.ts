import { format } from 'string-template';
import { DetectContext, DetectResult } from './DifferenceDetector.js';
import { DiffLocation, DiffPair, DiffReasons } from 'typescript-codegen-breaking-change-detector';
import {
  InterfaceDeclaration,
  Node,
  ParameterDeclaration,
  PropertyDeclaration,
  PropertySignature,
  SyntaxKind,
} from 'ts-morph';
import { SDKType } from '../../common/types.js';

export interface ChangelogItems {
  features: string[];
  breakingChanges: string[];
}

// TODO: use consistent upper/lower case
// TODO: use consistent word: removed/deleted
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
  // NOTE: not detected in v1 except constructor and it's parameters
  private classChanged = 'Class {className} has a new signature';

  /** type alias */
  private typeAliasAddedTemplate = 'Added type alias {typeName}';
  // NOTE: not in v1
  private typeAliasRemovedTemplate = 'Removed type alias {typeName}';

  changelogItems: ChangelogItems = {
    features: [],
    breakingChanges: [],
  };

  constructor(
    private detectContext: DetectContext,
    private detectResult: DetectResult
  ) {}

  public generate(): ChangelogItems {
    this.detectResult.interfaces.forEach((diffPairs, name) => {
      this.generateForInterfaces(name, diffPairs);
      this.generateForClasses(name, diffPairs);
      this.generateForTypeAliases(name, diffPairs);
    });
  }

  private generateForTypeAliases(typeName: string, diffPairs: DiffPair[]): void {
    diffPairs.forEach((p) => {
      // type alias added
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = format(this.typeAliasAddedTemplate, { typeName });
        p.messages.set(DiffReasons.Added, message);
      }
      // NOTE: not detected in v1
      // type alias removed
      if (p.location === DiffLocation.TypeAlias && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.typeAliasRemovedTemplate, { typeName });
        p.messages.set(DiffReasons.Removed, message);
      }
    });
  }

  private generateForClasses(className: string, diffPairs: DiffPair[]): void {
    diffPairs.forEach((p) => {
      // class added
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Added)) {
        const message = format(this.classAddedTemplate, { className });
        p.messages.set(DiffReasons.Added, message);
      }
      // class removed
      if (p.location === DiffLocation.Class && this.hasReasons(p.reasons, DiffReasons.Removed)) {
        const message = format(this.classRemovedTemplate, { className });
        p.messages.set(DiffReasons.Removed, message);
      }
      // class type changed
      // NOTE: not detected in v1 except constructor and it's parameters
      if (
        p.location === DiffLocation.Signature &&
        // TODO: from v1, which treat adding constructor as breaking, is it true?
        (this.hasReasons(p.reasons, DiffReasons.Added) || this.hasReasons(p.reasons, DiffReasons.Removed))
      ) {
        const message = format(this.classChanged, { className });
        // not good, remove it
        p.messages.set(DiffReasons.Added, message);
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
          p.messages.set(DiffReasons.Added, message);
        }
        // operation group removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.operationGroupRemovedTemplate, { interfaceName });
          p.messages.set(DiffReasons.Removed, message);
        }

        /** operation changes */
        // operation added
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.operationAddedTemplate, { interfaceName, signatureName: p.source!.name });
          p.messages.set(DiffReasons.Added, message);
        }
        // operation removed
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.operationRemovedTemplate, { interfaceName, signatureName: p.source!.name });
          p.messages.set(DiffReasons.Removed, message);
        }
        // operation signature's parameter type changed
        if (p.location === DiffLocation.Parameter && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          p.messages.set(DiffReasons.TypeChanged, message);
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
          p.messages.set(DiffReasons.TypeChanged, message);
        }
        // operation signature's return type changed
        // NOTE: not detected in v1
        if (p.location === DiffLocation.Signature_ReturnType && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            signatureName: p.target!.name,
          });
          p.messages.set(DiffReasons.TypeChanged, message);
        }
        // operation parameter
        // TODO: v1 make it a breaking change when operation optional/required changed, while v2 only consider required-> optional is breaking change
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          p.messages.set(DiffReasons.TypeChanged, message);
        }
        // operation signature's parameter required/optional changed
        // NOTE: not detected in v1
        if (p.location === DiffLocation.Signature && this.hasReasons(p.reasons, DiffReasons.RequiredToOptional)) {
          const message = format(this.operationSignatureChangedTemplate, {
            interfaceName,
            // TODO: get signature name
            signatureName: 'TODO',
          });
          p.messages.set(DiffReasons.TypeChanged, message);
        }
      } else {
        /** model changes */
        // model added
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelAddedTemplate, { interfaceName });
          p.messages.set(DiffReasons.Added, message);
        }
        // NOTE: not detected in v1
        // model removed
        if (p.location === DiffLocation.Interface && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.modelRemovedTemplate, { interfaceName });
          p.messages.set(DiffReasons.Removed, message);
        }
        // model's optional property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelOptionalPropertyAddedTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
          p.messages.set(DiffReasons.Added, message);
        }
        // model's required property added
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Added)) {
          const message = format(this.modelRequiredPropertyAddedTemplate, {
            interfaceName,
            propertyName: p.source!.name,
          });
          p.messages.set(DiffReasons.Added, message);
        }
        // model's optional property type changed
        // NOTE: not detected in v1 except for union type
        // NOTE: discuss if extend union type to be breaking or not
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.TypeChanged)) {
          const getTypeText = (node: Node) =>
            node.asKindOrThrow(SyntaxKind.PropertyDeclaration).getTypeNodeOrThrow().getText();
          const message = format(this.modelPropertyTypeChangedTemplate, {
            interfaceName,
            newPropertyName: p.source!.name,
            oldPropertyType: getTypeText(p.target!.node),
            newPropertyType: getTypeText(p.source!.node),
          });
          p.messages.set(DiffReasons.TypeChanged, message);
        }
        // model's property removed
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.Removed)) {
          const message = format(this.modelPropertyRemovedTemplate, { interfaceName, propertyName: p.target!.name });
          p.messages.set(DiffReasons.Removed, message);
        }
        // TODO: not detected in v2
        if (p.location === DiffLocation.Property && this.hasReasons(p.reasons, DiffReasons.OptionalToRequired)) {
          const message = format(this.modelPropertyOptionalToRequired, { interfaceName, propertyName: p.source!.name });
          p.messages.set(DiffReasons.OptionalToRequired, message);
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

  private isModelInterface(node: InterfaceDeclaration, sdkType: SDKType) {
    const memberCount = node.getMembers().length;
    switch (sdkType) {
      case SDKType.HighLevelClient:
        return memberCount > 0 && memberCount > node.getMethods().length;
      case SDKType.ModularClient:
        return memberCount > 0 && memberCount > node.getProperties().filter(this.isFunctionType).length;
      case SDKType.RestLevelClient:
        return false;
      default:
        throw new Error(`Unsupported SDK type ${sdkType} to distingush model interface.`);
    }
  }
}
