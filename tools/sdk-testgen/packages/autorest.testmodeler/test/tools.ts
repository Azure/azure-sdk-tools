/* eslint-disable @typescript-eslint/consistent-type-assertions */
/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as fs from 'fs';
import * as path from 'path';
import { AllSchemaTypes, ArraySchema, CodeModel, Languages, Parameter, Property, Schema, SchemaType } from '@autorest/codemodel';
import { deserialize } from '@azure-tools/codegen';

export class MockTool {
    public static createSchema(type: AllSchemaTypes = SchemaType.String, others: Record<string, any> = {}): Schema {
        return {
            type: type,
            language: this.createLanguages(),
            protocol: {},
            ...others,
        } as Schema;
    }

    public static createArraySchema(elementType: Schema, others: Record<string, any> = {}): ArraySchema {
        return {
            type: SchemaType.Array,
            elementType: elementType,
            ...others,
        } as ArraySchema;
    }

    public static createProperty(serializedName: string, schema: Schema, others: Record<string, any> = {}): Property {
        return {
            serializedName: serializedName,
            language: this.createLanguages(),
            schema: schema,
            protocol: {},
            ...others,
        } as Property;
    }

    public static createParameter(schema: Schema, others: Record<string, any> = {}): Parameter {
        return {
            schema: schema,
            ...others,
        } as Parameter;
    }

    public static createLanguages(otherLangs: Record<string, any> = {}): Languages {
        return {
            default: {
                name: 'mocked name',
                description: 'mocked description',
            },
            ...otherLangs,
        };
    }

    public static createCodeModel(): CodeModel {
        return deserialize(
            `
info:
  description: APIs documentation for Azure AgFoodPlatform Resource Provider Service.
  title: Azure AgFoodPlatform RP Service
schemas:
  booleans:
    - &ref_62
      type: boolean
      language:
        default:
          name: Boolean
          description: Indicates if the resource name is available.
        go:
          name: bool
          description: Indicates if the resource name is available.
      protocol: {}
    - &ref_66
      type: boolean
      language:
        default:
          name: Boolean
          description: Whether the operation applies to data-plane. This is "true" for data-plane operations and "false" for ARM/control-plane operations.
        go:
          name: bool
          description: Whether the operation applies to data-plane. This is "true" for data-plane operations and "false" for ARM/control-plane operations.
      protocol: {}
  numbers:
    - &ref_153
      type: integer
      apiVersions:
        - version: 2020-05-12-preview
      defaultValue: 50
      maximum: 1000
      minimum: 10
      precision: 32
      language:
        default:
          name: Integer
          description: ''
        go:
          name: int32
          description: ''
      protocol: {}
  strings:
    - &ref_0
      type: string
      language:
        default:
          name: String
          description: simple string
        go:
          name: string
          description: simple string
      protocol: {}
    - &ref_2
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_96
      type: string
      apiVersions:
        - version: '2.0'
      maxLength: 90
      minLength: 1
      pattern: '^[-\\w\\._\\(\\)]+$'
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_93
      type: string
      apiVersions:
        - version: '2.0'
      minLength: 1
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_40
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ResourceId
          description: 'Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}'
        go:
          name: string
          description: 'Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}'
      protocol: {}
    - &ref_41
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ResourceName
          description: The name of the resource
        go:
          name: string
          description: The name of the resource
      protocol: {}
    - &ref_42
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ResourceType
          description: The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or "Microsoft.Storage/storageAccounts"
        go:
          name: string
          description: The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or "Microsoft.Storage/storageAccounts"
      protocol: {}
    - &ref_5
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: SystemDataCreatedBy
          description: The identity that created the resource.
        go:
          name: string
          description: The identity that created the resource.
      protocol: {}
    - &ref_8
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: SystemDataLastModifiedBy
          description: The identity that last modified the resource.
        go:
          name: string
          description: The identity that last modified the resource.
      protocol: {}
    - &ref_10
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: '^[A-za-z]{3,50}[.][A-za-z]{3,100}$'
      language:
        default:
          name: ExtensionPropertiesExtensionId
          description: Extension Id.
        go:
          name: string
          description: Extension Id.
      protocol: {}
    - &ref_11
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: ExtensionPropertiesExtensionCategory
          description: Extension category. e.g. weather/sensor/satellite.
        go:
          name: string
          description: Extension category. e.g. weather/sensor/satellite.
      protocol: {}
    - &ref_12
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: '^([1-9]|10).\\d$'
      language:
        default:
          name: ExtensionPropertiesInstalledExtensionVersion
          description: Installed extension version.
        go:
          name: string
          description: Installed extension version.
      protocol: {}
    - &ref_13
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: ExtensionPropertiesExtensionAuthLink
          description: Extension auth link.
        go:
          name: string
          description: Extension auth link.
      protocol: {}
    - &ref_14
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: ExtensionPropertiesExtensionApiDocsLink
          description: Extension api docs link.
        go:
          name: string
          description: Extension api docs link.
      protocol: {}
    - &ref_15
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: ExtensionETag
          description: The ETag value to implement optimistic concurrency.
        go:
          name: string
          description: The ETag value to implement optimistic concurrency.
      protocol: {}
    - &ref_44
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ErrorDetailCode
          description: The error code.
        go:
          name: string
          description: The error code.
      protocol: {}
    - &ref_45
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ErrorDetailMessage
          description: The error message.
        go:
          name: string
          description: The error message.
      protocol: {}
    - &ref_46
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ErrorDetailTarget
          description: The error target.
        go:
          name: string
          description: The error target.
      protocol: {}
    - &ref_48
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ErrorAdditionalInfoType
          description: The additional info type.
        go:
          name: string
          description: The additional info type.
      protocol: {}
    - &ref_78
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get4ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_79
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get5ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_51
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: ExtensionListResponseNextLink
          description: Continuation link (absolute URI) to the next page of results in the list.
        go:
          name: string
          description: Continuation link (absolute URI) to the next page of results in the list.
      protocol: {}
    - &ref_81
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get0ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_82
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get1ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_83
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get2ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_84
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: Get3ItemsItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_17
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsExtensionPropertiesTargetResourceType
          description: Target ResourceType of the farmBeatsExtension.
        go:
          name: string
          description: Target ResourceType of the farmBeatsExtension.
      protocol: {}
    - &ref_18
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      pattern: '^[A-za-z]{3,50}[.][A-za-z]{3,100}$'
      language:
        default:
          name: FarmBeatsExtensionPropertiesFarmBeatsExtensionId
          description: FarmBeatsExtension ID.
        go:
          name: string
          description: FarmBeatsExtension ID.
      protocol: {}
    - &ref_19
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      language:
        default:
          name: FarmBeatsExtensionPropertiesFarmBeatsExtensionName
          description: FarmBeatsExtension name.
        go:
          name: string
          description: FarmBeatsExtension name.
      protocol: {}
    - &ref_20
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      pattern: '^([1-9]|10).\\d$'
      language:
        default:
          name: FarmBeatsExtensionPropertiesFarmBeatsExtensionVersion
          description: FarmBeatsExtension version.
        go:
          name: string
          description: FarmBeatsExtension version.
      protocol: {}
    - &ref_21
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      language:
        default:
          name: FarmBeatsExtensionPropertiesPublisherId
          description: Publisher ID.
        go:
          name: string
          description: Publisher ID.
      protocol: {}
    - &ref_22
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 500
      minLength: 2
      language:
        default:
          name: FarmBeatsExtensionPropertiesDescription
          description: Textual description.
        go:
          name: string
          description: Textual description.
      protocol: {}
    - &ref_23
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      language:
        default:
          name: FarmBeatsExtensionPropertiesExtensionCategory
          description: Category of the extension. e.g. weather/sensor/satellite.
        go:
          name: string
          description: Category of the extension. e.g. weather/sensor/satellite.
      protocol: {}
    - &ref_24
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsExtensionPropertiesExtensionAuthLink
          description: FarmBeatsExtension auth link.
        go:
          name: string
          description: FarmBeatsExtension auth link.
      protocol: {}
    - &ref_25
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsExtensionPropertiesExtensionApiDocsLink
          description: FarmBeatsExtension api docs link.
        go:
          name: string
          description: FarmBeatsExtension api docs link.
      protocol: {}
    - &ref_26
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: DetailedInformationApiName
          description: ApiName available for the farmBeatsExtension.
        go:
          name: string
          description: ApiName available for the farmBeatsExtension.
      protocol: {}
    - &ref_27
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: DetailedInformationCustomParametersItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_28
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: DetailedInformationPlatformParametersItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_29
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      language:
        default:
          name: UnitSystemsInfoKey
          description: UnitSystem key sent as part of ProviderInput.
        go:
          name: string
          description: UnitSystem key sent as part of ProviderInput.
      protocol: {}
    - &ref_30
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: UnitSystemsInfoValuesItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_31
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: DetailedInformationApiInputParametersItem
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_52
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsExtensionListResponseNextLink
          description: Continuation link (absolute URI) to the next page of results in the list.
        go:
          name: string
          description: Continuation link (absolute URI) to the next page of results in the list.
      protocol: {}
    - &ref_192
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: '^[A-za-z]{3,50}[.][A-za-z]{3,100}$'
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_1
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_39
      type: string
      apiVersions:
        - version: '2.0'
      extensions:
        x-ms-mutability:
          - read
          - create
      language:
        default:
          name: TrackedResourceLocation
          description: The geo-location where the resource lives
        go:
          name: string
          description: The geo-location where the resource lives
      protocol: {}
    - &ref_35
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsPropertiesInstanceUri
          description: Uri of the FarmBeats instance.
        go:
          name: string
          description: Uri of the FarmBeats instance.
      protocol: {}
    - &ref_57
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsUpdateRequestModelLocation
          description: Geo-location where the resource lives.
        go:
          name: string
          description: Geo-location where the resource lives.
      protocol: {}
    - &ref_59
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      language:
        default:
          name: FarmBeatsListResponseNextLink
          description: Continuation link (absolute URI) to the next page of results in the list.
        go:
          name: string
          description: Continuation link (absolute URI) to the next page of results in the list.
      protocol: {}
    - &ref_60
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: CheckNameAvailabilityRequestName
          description: The name of the resource for which availability needs to be checked.
        go:
          name: string
          description: The name of the resource for which availability needs to be checked.
      protocol: {}
    - &ref_61
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: CheckNameAvailabilityRequestType
          description: The resource type.
        go:
          name: string
          description: The resource type.
      protocol: {}
    - &ref_64
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: CheckNameAvailabilityResponseMessage
          description: Detailed reason why the given name is available.
        go:
          name: string
          description: Detailed reason why the given name is available.
      protocol: {}
    - &ref_65
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationName
          description: 'The name of the operation, as per Resource-Based Access Control (RBAC). Examples: "Microsoft.Compute/virtualMachines/write", "Microsoft.Compute/virtualMachines/capture/action"'
        go:
          name: string
          description: 'The name of the operation, as per Resource-Based Access Control (RBAC). Examples: "Microsoft.Compute/virtualMachines/write", "Microsoft.Compute/virtualMachines/capture/action"'
      protocol: {}
    - &ref_67
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationDisplayProvider
          description: 'The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".'
        go:
          name: string
          description: 'The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".'
      protocol: {}
    - &ref_68
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationDisplayResource
          description: The localized friendly name of the resource type related to this operation. E.g. "Virtual Machines" or "Job Schedule Collections".
        go:
          name: string
          description: The localized friendly name of the resource type related to this operation. E.g. "Virtual Machines" or "Job Schedule Collections".
      protocol: {}
    - &ref_69
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationDisplayOperation
          description: 'The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".'
        go:
          name: string
          description: 'The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".'
      protocol: {}
    - &ref_70
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationDisplayDescription
          description: 'The short, localized friendly description of the operation; suitable for tool tips and detailed views.'
        go:
          name: string
          description: 'The short, localized friendly description of the operation; suitable for tool tips and detailed views.'
      protocol: {}
    - &ref_73
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationListResultNextLink
          description: URL to get the next set of operation list results (if there are any).
        go:
          name: string
          description: URL to get the next set of operation list results (if there are any).
      protocol: {}
  choices:
    - &ref_6
      choices:
        - value: User
          language:
            default:
              name: User
              description: ''
            go:
              name: CreatedByTypeUser
              description: ''
        - value: Application
          language:
            default:
              name: Application
              description: ''
            go:
              name: CreatedByTypeApplication
              description: ''
        - value: ManagedIdentity
          language:
            default:
              name: ManagedIdentity
              description: ''
            go:
              name: CreatedByTypeManagedIdentity
              description: ''
        - value: Key
          language:
            default:
              name: Key
              description: ''
            go:
              name: CreatedByTypeKey
              description: ''
      type: choice
      apiVersions:
        - version: '2.0'
      choiceType: *ref_0
      language:
        default:
          name: CreatedByType
          description: The type of identity that created the resource.
        go:
          name: CreatedByType
          description: The type of identity that created the resource.
          possibleValuesFunc: PossibleCreatedByTypeValues
      protocol: {}
    - &ref_36
      choices:
        - value: Succeeded
          language:
            default:
              name: Succeeded
              description: ''
            go:
              name: ProvisioningStateSucceeded
              description: ''
        - value: Failed
          language:
            default:
              name: Failed
              description: ''
            go:
              name: ProvisioningStateFailed
              description: ''
      type: choice
      apiVersions:
        - version: 2020-05-12-preview
      choiceType: *ref_0
      language:
        default:
          name: ProvisioningState
          description: FarmBeats instance provisioning state.
        go:
          name: ProvisioningState
          description: FarmBeats instance provisioning state.
          possibleValuesFunc: PossibleProvisioningStateValues
      protocol: {}
    - &ref_63
      choices:
        - value: Invalid
          language:
            default:
              name: Invalid
              description: ''
            go:
              name: CheckNameAvailabilityReasonInvalid
              description: ''
        - value: AlreadyExists
          language:
            default:
              name: AlreadyExists
              description: ''
            go:
              name: CheckNameAvailabilityReasonAlreadyExists
              description: ''
      type: choice
      apiVersions:
        - version: '2.0'
      choiceType: *ref_0
      language:
        default:
          name: CheckNameAvailabilityReason
          description: The reason why the given name is not available.
        go:
          name: CheckNameAvailabilityReason
          description: The reason why the given name is not available.
          possibleValuesFunc: PossibleCheckNameAvailabilityReasonValues
      protocol: {}
    - &ref_71
      choices:
        - value: user
          language:
            default:
              name: User
              description: ''
            go:
              name: OriginUser
              description: ''
        - value: system
          language:
            default:
              name: System
              description: ''
            go:
              name: OriginSystem
              description: ''
        - value: 'user,system'
          language:
            default:
              name: UserSystem
              description: ''
            go:
              name: OriginUserSystem
              description: ''
      type: choice
      apiVersions:
        - version: '2.0'
      choiceType: *ref_0
      language:
        default:
          name: Origin
          description: 'The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"'
        go:
          name: Origin
          description: 'The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"'
          possibleValuesFunc: PossibleOriginValues
      protocol: {}
    - &ref_72
      choices:
        - value: Internal
          language:
            default:
              name: Internal
              description: ''
            go:
              name: ActionTypeInternal
              description: ''
      type: choice
      apiVersions:
        - version: '2.0'
      choiceType: *ref_0
      language:
        default:
          name: ActionType
          description: Enum. Indicates the action type. "Internal" refers to actions that are for internal only APIs.
        go:
          name: ActionType
          description: Enum. Indicates the action type. "Internal" refers to actions that are for internal only APIs.
          possibleValuesFunc: PossibleActionTypeValues
      protocol: {}
  constants:
    - &ref_94
      type: constant
      value:
        value: 2020-05-12-preview
      valueType: *ref_0
      language:
        default:
          name: ApiVersion20200512Preview
          description: Api Version (2020-05-12-preview)
        go:
          name: string
          description: Api Version (2020-05-12-preview)
      protocol: {}
    - &ref_99
      type: constant
      value:
        value: application/json
      valueType: *ref_0
      language:
        default:
          name: Accept
          description: 'Accept: application/json'
        go:
          name: string
          description: 'Accept: application/json'
      protocol: {}
  dictionaries:
    - &ref_38
      type: dictionary
      elementType: *ref_1
      language:
        default:
          name: TrackedResourceTags
          description: Resource tags.
        go:
          name: 'map[string]*string'
          description: Resource tags.
          elementIsPtr: true
          marshallingFormat: json
      protocol: {}
    - &ref_58
      type: dictionary
      elementType: *ref_2
      language:
        default:
          name: FarmBeatsUpdateRequestModelTags
          description: Resource tags.
        go:
          name: 'map[string]*string'
          description: Resource tags.
          elementIsPtr: true
          marshallingFormat: json
      protocol: {}
  anyObjects:
    - &ref_49
      type: any-object
      language:
        default:
          name: AnyObject
          description: Any object
        go:
          name: 'map[string]interface{}'
          description: Any object
      protocol: {}
  dateTimes:
    - &ref_7
      type: date-time
      format: date-time
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: SystemDataCreatedAt
          description: The timestamp of resource creation (UTC).
        go:
          name: time.Time
          description: The timestamp of resource creation (UTC).
          internalTimeType: timeRFC3339
      protocol: {}
    - &ref_9
      type: date-time
      format: date-time
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: SystemDataLastModifiedAt
          description: The timestamp of resource last modification (UTC)
        go:
          name: time.Time
          description: The timestamp of resource last modification (UTC)
          internalTimeType: timeRFC3339
      protocol: {}
  objects:
    - &ref_4
      type: object
      apiVersions:
        - version: '2.0'
      children:
        all:
          - &ref_3
            type: object
            apiVersions:
              - version: '2.0'
            children:
              all:
                - &ref_32
                  type: object
                  apiVersions:
                    - version: 2020-05-12-preview
                  parents:
                    all:
                      - *ref_3
                      - *ref_4
                    immediate:
                      - *ref_3
                  properties:
                    - schema: &ref_16
                        type: object
                        apiVersions:
                          - version: '2.0'
                        properties:
                          - schema: *ref_5
                            serializedName: createdBy
                            language:
                              default:
                                name: createdBy
                                description: The identity that created the resource.
                              go:
                                name: CreatedBy
                                description: The identity that created the resource.
                            protocol: {}
                          - schema: *ref_6
                            serializedName: createdByType
                            language:
                              default:
                                name: createdByType
                                description: The type of identity that created the resource.
                              go:
                                name: CreatedByType
                                description: The type of identity that created the resource.
                            protocol: {}
                          - schema: *ref_7
                            serializedName: createdAt
                            language:
                              default:
                                name: createdAt
                                description: The timestamp of resource creation (UTC).
                              go:
                                name: CreatedAt
                                description: The timestamp of resource creation (UTC).
                            protocol: {}
                          - schema: *ref_8
                            serializedName: lastModifiedBy
                            language:
                              default:
                                name: lastModifiedBy
                                description: The identity that last modified the resource.
                              go:
                                name: LastModifiedBy
                                description: The identity that last modified the resource.
                            protocol: {}
                          - schema: *ref_6
                            serializedName: lastModifiedByType
                            language:
                              default:
                                name: lastModifiedByType
                                description: The type of identity that last modified the resource.
                              go:
                                name: LastModifiedByType
                                description: The type of identity that last modified the resource.
                            protocol: {}
                          - schema: *ref_9
                            serializedName: lastModifiedAt
                            language:
                              default:
                                name: lastModifiedAt
                                description: The timestamp of resource last modification (UTC)
                              go:
                                name: LastModifiedAt
                                description: The timestamp of resource last modification (UTC)
                            protocol: {}
                        serializationFormats:
                          - json
                        usage:
                          - output
                          - input
                        language:
                          default:
                            name: SystemData
                            description: Metadata pertaining to creation and last modification of the resource.
                            namespace: ''
                          go:
                            name: SystemData
                            description: SystemData - Metadata pertaining to creation and last modification of the resource.
                            marshallingFormat: json
                            namespace: ''
                            needsDateTimeMarshalling: true
                        protocol: {}
                      readOnly: true
                      serializedName: systemData
                      language:
                        default:
                          name: systemData
                          description: Metadata pertaining to creation and last modification of the resource.
                        go:
                          name: SystemData
                          description: READ-ONLY; Metadata pertaining to creation and last modification of the resource.
                      protocol: {}
                    - schema: &ref_43
                        type: object
                        apiVersions:
                          - version: 2020-05-12-preview
                        properties:
                          - schema: *ref_10
                            readOnly: true
                            serializedName: extensionId
                            language:
                              default:
                                name: extensionId
                                description: Extension Id.
                              go:
                                name: ExtensionID
                                description: READ-ONLY; Extension Id.
                            protocol: {}
                          - schema: *ref_11
                            readOnly: true
                            serializedName: extensionCategory
                            language:
                              default:
                                name: extensionCategory
                                description: Extension category. e.g. weather/sensor/satellite.
                              go:
                                name: ExtensionCategory
                                description: READ-ONLY; Extension category. e.g. weather/sensor/satellite.
                            protocol: {}
                          - schema: *ref_12
                            readOnly: true
                            serializedName: installedExtensionVersion
                            language:
                              default:
                                name: installedExtensionVersion
                                description: Installed extension version.
                              go:
                                name: InstalledExtensionVersion
                                description: READ-ONLY; Installed extension version.
                            protocol: {}
                          - schema: *ref_13
                            readOnly: true
                            serializedName: extensionAuthLink
                            language:
                              default:
                                name: extensionAuthLink
                                description: Extension auth link.
                              go:
                                name: ExtensionAuthLink
                                description: READ-ONLY; Extension auth link.
                            protocol: {}
                          - schema: *ref_14
                            readOnly: true
                            serializedName: extensionApiDocsLink
                            language:
                              default:
                                name: extensionApiDocsLink
                                description: Extension api docs link.
                              go:
                                name: ExtensionAPIDocsLink
                                description: READ-ONLY; Extension api docs link.
                            protocol: {}
                        serializationFormats:
                          - json
                        usage:
                          - output
                          - input
                        language:
                          default:
                            name: ExtensionProperties
                            description: Extension resource properties.
                            namespace: ''
                          go:
                            name: ExtensionProperties
                            description: ExtensionProperties - Extension resource properties.
                            marshallingFormat: json
                            namespace: ''
                        protocol: {}
                      serializedName: properties
                      extensions:
                        x-ms-client-flatten: true
                      language:
                        default:
                          name: properties
                          description: Extension resource properties.
                        go:
                          name: Properties
                          description: Extension resource properties.
                      protocol: {}
                    - schema: *ref_15
                      readOnly: true
                      serializedName: eTag
                      language:
                        default:
                          name: eTag
                          description: The ETag value to implement optimistic concurrency.
                        go:
                          name: ETag
                          description: READ-ONLY; The ETag value to implement optimistic concurrency.
                      protocol: {}
                  serializationFormats:
                    - json
                  usage:
                    - output
                    - input
                  extensions:
                    x-ms-azure-resource: true
                  language:
                    default:
                      name: Extension
                      description: Extension resource.
                      namespace: ''
                    go:
                      name: Extension
                      description: Extension resource.
                      marshallingFormat: json
                      namespace: ''
                  protocol: {}
                - &ref_33
                  type: object
                  apiVersions:
                    - version: 2020-05-12-preview
                  parents:
                    all:
                      - *ref_3
                      - *ref_4
                    immediate:
                      - *ref_3
                  properties:
                    - schema: *ref_16
                      readOnly: true
                      serializedName: systemData
                      language:
                        default:
                          name: systemData
                          description: Metadata pertaining to creation and last modification of the resource.
                        go:
                          name: SystemData
                          description: READ-ONLY; Metadata pertaining to creation and last modification of the resource.
                      protocol: {}
                    - schema: &ref_53
                        type: object
                        apiVersions:
                          - version: 2020-05-12-preview
                        properties:
                          - schema: *ref_17
                            readOnly: true
                            serializedName: targetResourceType
                            language:
                              default:
                                name: targetResourceType
                                description: Target ResourceType of the farmBeatsExtension.
                              go:
                                name: TargetResourceType
                                description: READ-ONLY; Target ResourceType of the farmBeatsExtension.
                            protocol: {}
                          - schema: *ref_18
                            readOnly: true
                            serializedName: farmBeatsExtensionId
                            language:
                              default:
                                name: farmBeatsExtensionId
                                description: FarmBeatsExtension ID.
                              go:
                                name: FarmBeatsExtensionID
                                description: READ-ONLY; FarmBeatsExtension ID.
                            protocol: {}
                          - schema: *ref_19
                            readOnly: true
                            serializedName: farmBeatsExtensionName
                            language:
                              default:
                                name: farmBeatsExtensionName
                                description: FarmBeatsExtension name.
                              go:
                                name: FarmBeatsExtensionName
                                description: READ-ONLY; FarmBeatsExtension name.
                            protocol: {}
                          - schema: *ref_20
                            readOnly: true
                            serializedName: farmBeatsExtensionVersion
                            language:
                              default:
                                name: farmBeatsExtensionVersion
                                description: FarmBeatsExtension version.
                              go:
                                name: FarmBeatsExtensionVersion
                                description: READ-ONLY; FarmBeatsExtension version.
                            protocol: {}
                          - schema: *ref_21
                            readOnly: true
                            serializedName: publisherId
                            language:
                              default:
                                name: publisherId
                                description: Publisher ID.
                              go:
                                name: PublisherID
                                description: READ-ONLY; Publisher ID.
                            protocol: {}
                          - schema: *ref_22
                            readOnly: true
                            serializedName: description
                            language:
                              default:
                                name: description
                                description: Textual description.
                              go:
                                name: Description
                                description: READ-ONLY; Textual description.
                            protocol: {}
                          - schema: *ref_23
                            readOnly: true
                            serializedName: extensionCategory
                            language:
                              default:
                                name: extensionCategory
                                description: Category of the extension. e.g. weather/sensor/satellite.
                              go:
                                name: ExtensionCategory
                                description: READ-ONLY; Category of the extension. e.g. weather/sensor/satellite.
                            protocol: {}
                          - schema: *ref_24
                            readOnly: true
                            serializedName: extensionAuthLink
                            language:
                              default:
                                name: extensionAuthLink
                                description: FarmBeatsExtension auth link.
                              go:
                                name: ExtensionAuthLink
                                description: READ-ONLY; FarmBeatsExtension auth link.
                            protocol: {}
                          - schema: *ref_25
                            readOnly: true
                            serializedName: extensionApiDocsLink
                            language:
                              default:
                                name: extensionApiDocsLink
                                description: FarmBeatsExtension api docs link.
                              go:
                                name: ExtensionAPIDocsLink
                                description: READ-ONLY; FarmBeatsExtension api docs link.
                            protocol: {}
                          - schema: &ref_89
                              type: array
                              apiVersions:
                                - version: 2020-05-12-preview
                              elementType: &ref_54
                                type: object
                                apiVersions:
                                  - version: 2020-05-12-preview
                                properties:
                                  - schema: *ref_26
                                    serializedName: apiName
                                    language:
                                      default:
                                        name: apiName
                                        description: ApiName available for the farmBeatsExtension.
                                      go:
                                        name: APIName
                                        description: ApiName available for the farmBeatsExtension.
                                    protocol: {}
                                  - schema: &ref_85
                                      type: array
                                      apiVersions:
                                        - version: 2020-05-12-preview
                                      elementType: *ref_27
                                      language:
                                        default:
                                          name: DetailedInformationCustomParameters
                                          description: List of customParameters.
                                        go:
                                          name: '[]*string'
                                          description: List of customParameters.
                                          elementIsPtr: true
                                          marshallingFormat: json
                                      protocol: {}
                                    serializedName: customParameters
                                    language:
                                      default:
                                        name: customParameters
                                        description: List of customParameters.
                                      go:
                                        name: CustomParameters
                                        description: List of customParameters.
                                        byValue: true
                                    protocol: {}
                                  - schema: &ref_86
                                      type: array
                                      apiVersions:
                                        - version: 2020-05-12-preview
                                      elementType: *ref_28
                                      language:
                                        default:
                                          name: DetailedInformationPlatformParameters
                                          description: List of platformParameters.
                                        go:
                                          name: '[]*string'
                                          description: List of platformParameters.
                                          elementIsPtr: true
                                          marshallingFormat: json
                                      protocol: {}
                                    serializedName: platformParameters
                                    language:
                                      default:
                                        name: platformParameters
                                        description: List of platformParameters.
                                      go:
                                        name: PlatformParameters
                                        description: List of platformParameters.
                                        byValue: true
                                    protocol: {}
                                  - schema: &ref_55
                                      type: object
                                      apiVersions:
                                        - version: 2020-05-12-preview
                                      properties:
                                        - schema: *ref_29
                                          required: true
                                          serializedName: key
                                          language:
                                            default:
                                              name: key
                                              description: UnitSystem key sent as part of ProviderInput.
                                            go:
                                              name: Key
                                              description: REQUIRED; UnitSystem key sent as part of ProviderInput.
                                          protocol: {}
                                        - schema: &ref_87
                                            type: array
                                            apiVersions:
                                              - version: 2020-05-12-preview
                                            elementType: *ref_30
                                            language:
                                              default:
                                                name: UnitSystemsInfoValues
                                                description: List of unit systems supported by this data provider.
                                              go:
                                                name: '[]*string'
                                                description: List of unit systems supported by this data provider.
                                                elementIsPtr: true
                                                marshallingFormat: json
                                            protocol: {}
                                          required: true
                                          serializedName: values
                                          language:
                                            default:
                                              name: values
                                              description: List of unit systems supported by this data provider.
                                            go:
                                              name: Values
                                              description: REQUIRED; List of unit systems supported by this data provider.
                                              byValue: true
                                          protocol: {}
                                      serializationFormats:
                                        - json
                                      usage:
                                        - output
                                        - input
                                      language:
                                        default:
                                          name: UnitSystemsInfo
                                          description: Unit systems info for the data provider.
                                          namespace: ''
                                        go:
                                          name: UnitSystemsInfo
                                          description: UnitSystemsInfo - Unit systems info for the data provider.
                                          hasArrayMap: true
                                          marshallingFormat: json
                                          namespace: ''
                                      protocol: {}
                                    serializedName: unitsSupported
                                    language:
                                      default:
                                        name: unitsSupported
                                        description: Unit systems info for the data provider.
                                      go:
                                        name: UnitsSupported
                                        description: Unit systems info for the data provider.
                                    protocol: {}
                                  - schema: &ref_88
                                      type: array
                                      apiVersions:
                                        - version: 2020-05-12-preview
                                      elementType: *ref_31
                                      language:
                                        default:
                                          name: DetailedInformationApiInputParameters
                                          description: List of apiInputParameters.
                                        go:
                                          name: '[]*string'
                                          description: List of apiInputParameters.
                                          elementIsPtr: true
                                          marshallingFormat: json
                                      protocol: {}
                                    serializedName: apiInputParameters
                                    language:
                                      default:
                                        name: apiInputParameters
                                        description: List of apiInputParameters.
                                      go:
                                        name: APIInputParameters
                                        description: List of apiInputParameters.
                                        byValue: true
                                    protocol: {}
                                serializationFormats:
                                  - json
                                usage:
                                  - output
                                  - input
                                language:
                                  default:
                                    name: DetailedInformation
                                    description: Model to capture detailed information for farmBeatsExtensions.
                                    namespace: ''
                                  go:
                                    name: DetailedInformation
                                    description: DetailedInformation - Model to capture detailed information for farmBeatsExtensions.
                                    hasArrayMap: true
                                    marshallingFormat: json
                                    namespace: ''
                                protocol: {}
                              language:
                                default:
                                  name: FarmBeatsExtensionPropertiesDetailedInformation
                                  description: "Detailed information which shows summary of requested data.\r\nUsed in descriptive get extension metadata call.\r\nInformation for weather category per api included are apisSupported,\r\ncustomParameters, PlatformParameters and Units supported."
                                go:
                                  name: '[]*DetailedInformation'
                                  description: "Detailed information which shows summary of requested data.\r\nUsed in descriptive get extension metadata call.\r\nInformation for weather category per api included are apisSupported,\r\ncustomParameters, PlatformParameters and Units supported."
                                  elementIsPtr: true
                                  marshallingFormat: json
                              protocol: {}
                            readOnly: true
                            serializedName: detailedInformation
                            language:
                              default:
                                name: detailedInformation
                                description: "Detailed information which shows summary of requested data.\r\nUsed in descriptive get extension metadata call.\r\nInformation for weather category per api included are apisSupported,\r\ncustomParameters, PlatformParameters and Units supported."
                              go:
                                name: DetailedInformation
                                description: |-
                                  READ-ONLY; Detailed information which shows summary of requested data. Used in descriptive get extension metadata call. Information for weather category per api included are apisSupported, customParameters,
                                  PlatformParameters and Units supported.
                                byValue: true
                            protocol: {}
                        serializationFormats:
                          - json
                        usage:
                          - output
                          - input
                        language:
                          default:
                            name: FarmBeatsExtensionProperties
                            description: FarmBeatsExtension properties.
                            namespace: ''
                          go:
                            name: FarmBeatsExtensionProperties
                            description: FarmBeatsExtensionProperties - FarmBeatsExtension properties.
                            hasArrayMap: true
                            marshallingFormat: json
                            namespace: ''
                        protocol: {}
                      serializedName: properties
                      extensions:
                        x-ms-client-flatten: true
                      language:
                        default:
                          name: properties
                          description: FarmBeatsExtension properties.
                        go:
                          name: Properties
                          description: FarmBeatsExtension properties.
                      protocol: {}
                  serializationFormats:
                    - json
                  usage:
                    - output
                    - input
                  extensions:
                    x-ms-azure-resource: true
                  language:
                    default:
                      name: FarmBeatsExtension
                      description: FarmBeats extension resource.
                      namespace: ''
                    go:
                      name: FarmBeatsExtension
                      description: FarmBeatsExtension - FarmBeats extension resource.
                      marshallingFormat: json
                      namespace: ''
                  protocol: {}
              immediate:
                - *ref_32
                - *ref_33
            parents:
              all:
                - *ref_4
              immediate:
                - *ref_4
            serializationFormats:
              - json
            summary: Proxy Resource
            usage:
              - output
              - input
            language:
              default:
                name: ProxyResource
                description: The resource model definition for a Azure Resource Manager proxy resource. It will not have tags and a location
                namespace: ''
                summary: Proxy Resource
              go:
                name: ProxyResource
                description: ProxyResource - The resource model definition for a Azure Resource Manager proxy resource. It will not have tags and a location
                marshallingFormat: json
                namespace: ''
                summary: Proxy Resource
            protocol: {}
          - *ref_32
          - *ref_33
          - &ref_34
            type: object
            apiVersions:
              - version: '2.0'
            children:
              all:
                - &ref_37
                  type: object
                  apiVersions:
                    - version: 2020-05-12-preview
                  parents:
                    all:
                      - *ref_34
                      - *ref_4
                    immediate:
                      - *ref_34
                  properties:
                    - schema: *ref_16
                      readOnly: true
                      serializedName: systemData
                      language:
                        default:
                          name: systemData
                          description: Metadata pertaining to creation and last modification of the resource.
                        go:
                          name: SystemData
                          description: READ-ONLY; Metadata pertaining to creation and last modification of the resource.
                      protocol: {}
                    - schema: &ref_56
                        type: object
                        apiVersions:
                          - version: 2020-05-12-preview
                        properties:
                          - schema: *ref_35
                            readOnly: true
                            serializedName: instanceUri
                            language:
                              default:
                                name: instanceUri
                                description: Uri of the FarmBeats instance.
                              go:
                                name: InstanceURI
                                description: READ-ONLY; Uri of the FarmBeats instance.
                            protocol: {}
                          - schema: *ref_36
                            readOnly: true
                            serializedName: provisioningState
                            language:
                              default:
                                name: provisioningState
                                description: FarmBeats instance provisioning state.
                              go:
                                name: ProvisioningState
                                description: READ-ONLY; FarmBeats instance provisioning state.
                            protocol: {}
                        serializationFormats:
                          - json
                        usage:
                          - output
                          - input
                        language:
                          default:
                            name: FarmBeatsProperties
                            description: FarmBeats ARM Resource properties.
                            namespace: ''
                          go:
                            name: FarmBeatsProperties
                            description: FarmBeatsProperties - FarmBeats ARM Resource properties.
                            marshallingFormat: json
                            namespace: ''
                        protocol: {}
                      serializedName: properties
                      extensions:
                        x-ms-client-flatten: true
                      language:
                        default:
                          name: properties
                          description: FarmBeats ARM Resource properties.
                        go:
                          name: Properties
                          description: FarmBeats ARM Resource properties.
                      protocol: {}
                  serializationFormats:
                    - json
                  usage:
                    - output
                    - input
                  extensions:
                    x-ms-azure-resource: true
                  language:
                    default:
                      name: FarmBeats
                      description: FarmBeats ARM Resource.
                      namespace: ''
                    go:
                      name: FarmBeats
                      description: FarmBeats ARM Resource.
                      marshallingFormat: json
                      namespace: ''
                  protocol: {}
              immediate:
                - *ref_37
            parents:
              all:
                - *ref_4
              immediate:
                - *ref_4
            properties:
              - schema: *ref_38
                required: false
                serializedName: tags
                language:
                  default:
                    name: tags
                    description: Resource tags.
                  go:
                    name: Tags
                    description: Resource tags.
                    byValue: true
                protocol: {}
              - schema: *ref_39
                required: true
                serializedName: location
                language:
                  default:
                    name: location
                    description: The geo-location where the resource lives
                  go:
                    name: Location
                    description: REQUIRED; The geo-location where the resource lives
                protocol: {}
            serializationFormats:
              - json
            summary: Tracked Resource
            usage:
              - output
              - input
            language:
              default:
                name: TrackedResource
                description: The resource model definition for an Azure Resource Manager tracked top level resource which has 'tags' and a 'location'
                namespace: ''
                summary: Tracked Resource
              go:
                name: TrackedResource
                description: TrackedResource - The resource model definition for an Azure Resource Manager tracked top level resource which has 'tags' and a 'location'
                hasArrayMap: true
                marshallingFormat: json
                namespace: ''
                summary: Tracked Resource
            protocol: {}
          - *ref_37
        immediate:
          - *ref_3
          - *ref_34
      properties:
        - schema: *ref_40
          readOnly: true
          serializedName: id
          language:
            default:
              name: id
              description: 'Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}'
            go:
              name: ID
              description: 'READ-ONLY; Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}'
          protocol: {}
        - schema: *ref_41
          readOnly: true
          serializedName: name
          language:
            default:
              name: name
              description: The name of the resource
            go:
              name: Name
              description: READ-ONLY; The name of the resource
          protocol: {}
        - schema: *ref_42
          readOnly: true
          serializedName: type
          language:
            default:
              name: type
              description: The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or "Microsoft.Storage/storageAccounts"
            go:
              name: Type
              description: READ-ONLY; The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or "Microsoft.Storage/storageAccounts"
          protocol: {}
      serializationFormats:
        - json
      summary: Resource
      usage:
        - output
        - input
      extensions:
        x-ms-azure-resource: true
      language:
        default:
          name: Resource
          description: Common fields that are returned in the response for all Azure Resource Manager resources
          namespace: ''
          summary: Resource
        go:
          name: Resource
          description: Resource - Common fields that are returned in the response for all Azure Resource Manager resources
          marshallingFormat: json
          namespace: ''
          summary: Resource
      protocol: {}
    - *ref_3
    - *ref_32
    - *ref_16
    - *ref_43
    - &ref_103
      type: object
      apiVersions:
        - version: '2.0'
      properties:
        - schema: &ref_47
            type: object
            apiVersions:
              - version: '2.0'
            properties:
              - schema: *ref_44
                readOnly: true
                serializedName: code
                language:
                  default:
                    name: code
                    description: The error code.
                  go:
                    name: Code
                    description: READ-ONLY; The error code.
                protocol: {}
              - schema: *ref_45
                readOnly: true
                serializedName: message
                language:
                  default:
                    name: message
                    description: The error message.
                  go:
                    name: Message
                    description: READ-ONLY; The error message.
                protocol: {}
              - schema: *ref_46
                readOnly: true
                serializedName: target
                language:
                  default:
                    name: target
                    description: The error target.
                  go:
                    name: Target
                    description: READ-ONLY; The error target.
                protocol: {}
              - schema: &ref_76
                  type: array
                  apiVersions:
                    - version: '2.0'
                  elementType: *ref_47
                  language:
                    default:
                      name: ErrorDetailDetails
                      description: The error details.
                    go:
                      name: '[]*ErrorDetail'
                      description: The error details.
                      elementIsPtr: true
                      marshallingFormat: json
                  protocol: {}
                readOnly: true
                serializedName: details
                language:
                  default:
                    name: details
                    description: The error details.
                  go:
                    name: Details
                    description: READ-ONLY; The error details.
                    byValue: true
                protocol: {}
              - schema: &ref_77
                  type: array
                  apiVersions:
                    - version: '2.0'
                  elementType: &ref_50
                    type: object
                    apiVersions:
                      - version: '2.0'
                    properties:
                      - schema: *ref_48
                        readOnly: true
                        serializedName: type
                        language:
                          default:
                            name: type
                            description: The additional info type.
                          go:
                            name: Type
                            description: READ-ONLY; The additional info type.
                        protocol: {}
                      - schema: *ref_49
                        readOnly: true
                        serializedName: info
                        language:
                          default:
                            name: info
                            description: The additional info.
                          go:
                            name: Info
                            description: READ-ONLY; The additional info.
                            byValue: true
                        protocol: {}
                    serializationFormats:
                      - json
                    usage:
                      - exception
                    language:
                      default:
                        name: ErrorAdditionalInfo
                        description: The resource management error additional info.
                        namespace: ''
                      go:
                        name: ErrorAdditionalInfo
                        description: ErrorAdditionalInfo - The resource management error additional info.
                        marshallingFormat: json
                        namespace: ''
                    protocol: {}
                  language:
                    default:
                      name: ErrorDetailAdditionalInfo
                      description: The error additional info.
                    go:
                      name: '[]*ErrorAdditionalInfo'
                      description: The error additional info.
                      elementIsPtr: true
                      marshallingFormat: json
                  protocol: {}
                readOnly: true
                serializedName: additionalInfo
                language:
                  default:
                    name: additionalInfo
                    description: The error additional info.
                  go:
                    name: AdditionalInfo
                    description: READ-ONLY; The error additional info.
                    byValue: true
                protocol: {}
            serializationFormats:
              - json
            usage:
              - exception
            language:
              default:
                name: ErrorDetail
                description: The error detail.
                namespace: ''
              go:
                name: ErrorDetail
                description: ErrorDetail - The error detail.
                hasArrayMap: true
                marshallingFormat: json
                namespace: ''
            protocol: {}
          serializedName: error
          language:
            default:
              name: error
              description: The error object.
            go:
              name: InnerError
              description: The error object.
          protocol: {}
      serializationFormats:
        - json
      summary: Error response
      usage:
        - exception
      language:
        default:
          name: ErrorResponse
          description: Common error response for all Azure Resource Manager APIs to return error details for failed operations. (This also follows the OData error response format.).
          namespace: ''
          summary: Error response
        go:
          name: ErrorResponse
          description: ErrorResponse - Common error response for all Azure Resource Manager APIs to return error details for failed operations. (This also follows the OData error response format.).
          errorType: true
          marshallingFormat: json
          namespace: ''
          summary: Error response
      protocol: {}
    - *ref_47
    - *ref_50
    - &ref_160
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_80
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_32
            language:
              default:
                name: ExtensionListResponseValue
                description: List of requested objects.
              go:
                name: '[]*Extension'
                description: List of requested objects.
                elementIsPtr: true
                marshallingFormat: json
            protocol: {}
          serializedName: value
          language:
            default:
              name: value
              description: List of requested objects.
            go:
              name: Value
              description: List of requested objects.
              byValue: true
          protocol: {}
        - schema: *ref_51
          readOnly: true
          serializedName: nextLink
          language:
            default:
              name: nextLink
              description: Continuation link (absolute URI) to the next page of results in the list.
            go:
              name: NextLink
              description: READ-ONLY; Continuation link (absolute URI) to the next page of results in the list.
          protocol: {}
      serializationFormats:
        - json
      usage:
        - output
      language:
        default:
          name: ExtensionListResponse
          description: Paged response contains list of requested objects and a URL link to get the next set of results.
          namespace: ''
        go:
          name: ExtensionListResponse
          description: ExtensionListResponse - Paged response contains list of requested objects and a URL link to get the next set of results.
          hasArrayMap: true
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - &ref_182
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_90
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_33
            language:
              default:
                name: FarmBeatsExtensionListResponseValue
                description: List of requested objects.
              go:
                name: '[]*FarmBeatsExtension'
                description: List of requested objects.
                elementIsPtr: true
                marshallingFormat: json
            protocol: {}
          serializedName: value
          language:
            default:
              name: value
              description: List of requested objects.
            go:
              name: Value
              description: List of requested objects.
              byValue: true
          protocol: {}
        - schema: *ref_52
          readOnly: true
          serializedName: nextLink
          language:
            default:
              name: nextLink
              description: Continuation link (absolute URI) to the next page of results in the list.
            go:
              name: NextLink
              description: READ-ONLY; Continuation link (absolute URI) to the next page of results in the list.
          protocol: {}
      serializationFormats:
        - json
      usage:
        - output
      language:
        default:
          name: FarmBeatsExtensionListResponse
          description: Paged response contains list of requested objects and a URL link to get the next set of results.
          namespace: ''
        go:
          name: FarmBeatsExtensionListResponse
          description: FarmBeatsExtensionListResponse - Paged response contains list of requested objects and a URL link to get the next set of results.
          hasArrayMap: true
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - *ref_33
    - *ref_53
    - *ref_54
    - *ref_55
    - *ref_34
    - *ref_37
    - *ref_56
    - &ref_229
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: *ref_57
          serializedName: location
          language:
            default:
              name: location
              description: Geo-location where the resource lives.
            go:
              name: Location
              description: Geo-location where the resource lives.
          protocol: {}
        - schema: *ref_58
          serializedName: tags
          language:
            default:
              name: tags
              description: Resource tags.
            go:
              name: Tags
              description: Resource tags.
              byValue: true
          protocol: {}
      serializationFormats:
        - json
      usage:
        - input
      language:
        default:
          name: FarmBeatsUpdateRequestModel
          description: FarmBeats update request.
          namespace: ''
        go:
          name: FarmBeatsUpdateRequestModel
          description: FarmBeatsUpdateRequestModel - FarmBeats update request.
          hasArrayMap: true
          marshallingFormat: json
          namespace: ''
          needsPatchMarshaller: true
      protocol: {}
    - &ref_255
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_91
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_37
            language:
              default:
                name: FarmBeatsListResponseValue
                description: List of requested objects.
              go:
                name: '[]*FarmBeats'
                description: List of requested objects.
                elementIsPtr: true
                marshallingFormat: json
            protocol: {}
          serializedName: value
          language:
            default:
              name: value
              description: List of requested objects.
            go:
              name: Value
              description: List of requested objects.
              byValue: true
          protocol: {}
        - schema: *ref_59
          readOnly: true
          serializedName: nextLink
          language:
            default:
              name: nextLink
              description: Continuation link (absolute URI) to the next page of results in the list.
            go:
              name: NextLink
              description: READ-ONLY; Continuation link (absolute URI) to the next page of results in the list.
          protocol: {}
      serializationFormats:
        - json
      usage:
        - output
      language:
        default:
          name: FarmBeatsListResponse
          description: Paged response contains list of requested objects and a URL link to get the next set of results.
          namespace: ''
        go:
          name: FarmBeatsListResponse
          description: FarmBeatsListResponse - Paged response contains list of requested objects and a URL link to get the next set of results.
          hasArrayMap: true
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - &ref_280
      type: object
      apiVersions:
        - version: '2.0'
      properties:
        - schema: *ref_60
          serializedName: name
          language:
            default:
              name: name
              description: The name of the resource for which availability needs to be checked.
            go:
              name: Name
              description: The name of the resource for which availability needs to be checked.
          protocol: {}
        - schema: *ref_61
          serializedName: type
          language:
            default:
              name: type
              description: The resource type.
            go:
              name: Type
              description: The resource type.
          protocol: {}
      serializationFormats:
        - json
      usage:
        - input
      language:
        default:
          name: CheckNameAvailabilityRequest
          description: The check availability request body.
          namespace: ''
        go:
          name: CheckNameAvailabilityRequest
          description: CheckNameAvailabilityRequest - The check availability request body.
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - &ref_282
      type: object
      apiVersions:
        - version: '2.0'
      properties:
        - schema: *ref_62
          serializedName: nameAvailable
          language:
            default:
              name: nameAvailable
              description: Indicates if the resource name is available.
            go:
              name: NameAvailable
              description: Indicates if the resource name is available.
          protocol: {}
        - schema: *ref_63
          serializedName: reason
          language:
            default:
              name: reason
              description: The reason why the given name is not available.
            go:
              name: Reason
              description: The reason why the given name is not available.
          protocol: {}
        - schema: *ref_64
          serializedName: message
          language:
            default:
              name: message
              description: Detailed reason why the given name is available.
            go:
              name: Message
              description: Detailed reason why the given name is available.
          protocol: {}
      serializationFormats:
        - json
      usage:
        - output
      language:
        default:
          name: CheckNameAvailabilityResponse
          description: The check availability result.
          namespace: ''
        go:
          name: CheckNameAvailabilityResponse
          description: CheckNameAvailabilityResponse - The check availability result.
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - &ref_293
      type: object
      apiVersions:
        - version: '2.0'
      properties:
        - schema: &ref_92
            type: array
            apiVersions:
              - version: '2.0'
            elementType: &ref_74
              type: object
              apiVersions:
                - version: '2.0'
              properties:
                - schema: *ref_65
                  readOnly: true
                  serializedName: name
                  language:
                    default:
                      name: name
                      description: 'The name of the operation, as per Resource-Based Access Control (RBAC). Examples: "Microsoft.Compute/virtualMachines/write", "Microsoft.Compute/virtualMachines/capture/action"'
                    go:
                      name: Name
                      description: 'READ-ONLY; The name of the operation, as per Resource-Based Access Control (RBAC). Examples: "Microsoft.Compute/virtualMachines/write", "Microsoft.Compute/virtualMachines/capture/action"'
                  protocol: {}
                - schema: *ref_66
                  readOnly: true
                  serializedName: isDataAction
                  language:
                    default:
                      name: isDataAction
                      description: Whether the operation applies to data-plane. This is "true" for data-plane operations and "false" for ARM/control-plane operations.
                    go:
                      name: IsDataAction
                      description: READ-ONLY; Whether the operation applies to data-plane. This is "true" for data-plane operations and "false" for ARM/control-plane operations.
                  protocol: {}
                - schema: &ref_75
                    type: object
                    apiVersions:
                      - version: '2.0'
                    properties:
                      - schema: *ref_67
                        readOnly: true
                        serializedName: provider
                        language:
                          default:
                            name: provider
                            description: 'The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".'
                          go:
                            name: Provider
                            description: 'READ-ONLY; The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".'
                        protocol: {}
                      - schema: *ref_68
                        readOnly: true
                        serializedName: resource
                        language:
                          default:
                            name: resource
                            description: The localized friendly name of the resource type related to this operation. E.g. "Virtual Machines" or "Job Schedule Collections".
                          go:
                            name: Resource
                            description: READ-ONLY; The localized friendly name of the resource type related to this operation. E.g. "Virtual Machines" or "Job Schedule Collections".
                        protocol: {}
                      - schema: *ref_69
                        readOnly: true
                        serializedName: operation
                        language:
                          default:
                            name: operation
                            description: 'The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".'
                          go:
                            name: Operation
                            description: 'READ-ONLY; The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".'
                        protocol: {}
                      - schema: *ref_70
                        readOnly: true
                        serializedName: description
                        language:
                          default:
                            name: description
                            description: 'The short, localized friendly description of the operation; suitable for tool tips and detailed views.'
                          go:
                            name: Description
                            description: 'READ-ONLY; The short, localized friendly description of the operation; suitable for tool tips and detailed views.'
                        protocol: {}
                    serializationFormats:
                      - json
                    usage:
                      - output
                    extensions:
                      x-internal-autorest-anonymous-schema:
                        anonymous: true
                    language:
                      default:
                        name: OperationDisplay
                        description: Localized display information for this particular operation.
                        namespace: ''
                      go:
                        name: OperationDisplay
                        description: OperationDisplay - Localized display information for this particular operation.
                        marshallingFormat: json
                        namespace: ''
                    protocol: {}
                  serializedName: display
                  language:
                    default:
                      name: display
                      description: Localized display information for this particular operation.
                    go:
                      name: Display
                      description: Localized display information for this particular operation.
                  protocol: {}
                - schema: *ref_71
                  readOnly: true
                  serializedName: origin
                  language:
                    default:
                      name: origin
                      description: 'The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"'
                    go:
                      name: Origin
                      description: 'READ-ONLY; The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"'
                  protocol: {}
                - schema: *ref_72
                  readOnly: true
                  serializedName: actionType
                  language:
                    default:
                      name: actionType
                      description: Enum. Indicates the action type. "Internal" refers to actions that are for internal only APIs.
                    go:
                      name: ActionType
                      description: READ-ONLY; Enum. Indicates the action type. "Internal" refers to actions that are for internal only APIs.
                  protocol: {}
              serializationFormats:
                - json
              summary: REST API Operation
              usage:
                - output
              language:
                default:
                  name: Operation
                  description: 'Details of a REST API operation, returned from the Resource Provider Operations API'
                  namespace: ''
                  summary: REST API Operation
                go:
                  name: Operation
                  description: 'Operation - Details of a REST API operation, returned from the Resource Provider Operations API'
                  marshallingFormat: json
                  namespace: ''
                  summary: REST API Operation
              protocol: {}
            language:
              default:
                name: OperationListResultValue
                description: List of operations supported by the resource provider
              go:
                name: '[]*Operation'
                description: List of operations supported by the resource provider
                elementIsPtr: true
                marshallingFormat: json
            protocol: {}
          readOnly: true
          serializedName: value
          language:
            default:
              name: value
              description: List of operations supported by the resource provider
            go:
              name: Value
              description: READ-ONLY; List of operations supported by the resource provider
              byValue: true
          protocol: {}
        - schema: *ref_73
          readOnly: true
          serializedName: nextLink
          language:
            default:
              name: nextLink
              description: URL to get the next set of operation list results (if there are any).
            go:
              name: NextLink
              description: READ-ONLY; URL to get the next set of operation list results (if there are any).
          protocol: {}
      serializationFormats:
        - json
      usage:
        - output
      language:
        default:
          name: OperationListResult
          description: A list of REST API operations supported by an Azure Resource Provider. It contains an URL link to get the next set of results.
          namespace: ''
        go:
          name: OperationListResult
          description: OperationListResult - A list of REST API operations supported by an Azure Resource Provider. It contains an URL link to get the next set of results.
          hasArrayMap: true
          marshallingFormat: json
          namespace: ''
      protocol: {}
    - *ref_74
    - *ref_75
  arrays:
    - *ref_76
    - *ref_77
    - &ref_148
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_78
      language:
        default:
          name: ArrayOfGet4ItemsItem
          description: Array of Get4ItemsItem
        go:
          name: '[]*string'
          description: Array of Get4ItemsItem
          elementIsPtr: true
      protocol: {}
    - &ref_151
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_79
      language:
        default:
          name: ArrayOfGet5ItemsItem
          description: Array of Get5ItemsItem
        go:
          name: '[]*string'
          description: Array of Get5ItemsItem
          elementIsPtr: true
      protocol: {}
    - *ref_80
    - &ref_170
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_81
      language:
        default:
          name: ArrayOfGet0ItemsItem
          description: Array of Get0ItemsItem
        go:
          name: '[]*string'
          description: Array of Get0ItemsItem
          elementIsPtr: true
      protocol: {}
    - &ref_173
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_82
      language:
        default:
          name: ArrayOfGet1ItemsItem
          description: Array of Get1ItemsItem
        go:
          name: '[]*string'
          description: Array of Get1ItemsItem
          elementIsPtr: true
      protocol: {}
    - &ref_175
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_83
      language:
        default:
          name: ArrayOfGet2ItemsItem
          description: Array of Get2ItemsItem
        go:
          name: '[]*string'
          description: Array of Get2ItemsItem
          elementIsPtr: true
      protocol: {}
    - &ref_176
      type: array
      apiVersions:
        - version: 2020-05-12-preview
      elementType: *ref_84
      language:
        default:
          name: ArrayOfGet3ItemsItem
          description: Array of Get3ItemsItem
        go:
          name: '[]*string'
          description: Array of Get3ItemsItem
          elementIsPtr: true
      protocol: {}
    - *ref_85
    - *ref_86
    - *ref_87
    - *ref_88
    - *ref_89
    - *ref_90
    - *ref_91
    - *ref_92
globalParameters:
  - &ref_97
    schema: *ref_93
    implementation: Client
    required: true
    extensions:
      x-ms-priority: 0
    language:
      default:
        name: SubscriptionId
        description: The ID of the target subscription.
        serializedName: subscriptionId
      go:
        name: subscriptionID
        description: The ID of the target subscription.
        serializedName: subscriptionId
    protocol:
      http:
        in: path
  - &ref_95
    schema: *ref_0
    clientDefaultValue: 'https://management.azure.com'
    implementation: Client
    origin: 'modelerfour:synthesized/host'
    required: true
    extensions:
      x-ms-skip-url-encoding: true
    language:
      default:
        name: $host
        description: server parameter
        serializedName: $host
      go:
        name: endpoint
        description: server parameter
        serializedName: $host
    protocol:
      http:
        in: uri
  - &ref_98
    schema: *ref_94
    implementation: Client
    origin: 'modelerfour:synthesized/api-version'
    required: true
    language:
      default:
        name: apiVersion
        description: Api Version
        serializedName: api-version
      go:
        name: apiVersion
        description: Api Version
        serializedName: api-version
    protocol:
      http:
        in: query
operationGroups:
  - $key: Extensions
    operations:
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_100
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: extensionId
                description: Id of extension resource.
                serializedName: extensionId
              go:
                name: extensionID
                description: Id of extension resource.
                serializedName: extensionId
            protocol:
              http:
                in: path
          - &ref_101
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_102
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}'
                method: put
                uri: '{$host}'
        signatureParameters:
          - *ref_100
          - *ref_101
          - *ref_102
        responses:
          - schema: *ref_32
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '201'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Extensions_Create:
              parameters:
                api-version: 2020-05-12-preview
                extensionId: provider.extension
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '201':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: 'https://docs.provider.com/documentation/extension'
                      extensionAuthLink: 'https://www.provider.com/extension/'
                      extensionCategory: Weather
                      installedExtensionVersion: '1.0'
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
        language:
          default:
            name: Create
            description: Install extension.
          go:
            name: Create
            description: |-
              Install extension.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_310
              schema:
                type: object
                language:
                  default: &ref_104
                    name: ExtensionsCreateOptions
                    description: ExtensionsCreateOptions contains the optional parameters for the Extensions.Create method.
                  go: *ref_104
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsCreateOptions
              language:
                default: &ref_105
                  name: ExtensionsCreateOptions
                  description: ExtensionsCreateOptions contains the optional parameters for the Extensions.Create method.
                go: *ref_105
              protocol: {}
            protocolNaming:
              errorMethod: createHandleError
              internalMethod: create
              requestMethod: createCreateRequest
              responseMethod: createHandleResponse
            responseEnv: &ref_321
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_106
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_106
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_107
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_107
                  protocol: {}
                - &ref_112
                  schema:
                    type: object
                    properties:
                      - &ref_110
                        schema: *ref_32
                        serializedName: Extension
                        language:
                          default: &ref_108
                            name: Extension
                            description: Extension resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_108
                        protocol: {}
                    language:
                      default: &ref_109
                        name: ExtensionsCreateResult
                        description: ExtensionsCreateResult contains the result from method Extensions.Create.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_109
                    protocol: {}
                  serializedName: ExtensionsCreateResult
                  language:
                    default: &ref_111
                      name: ExtensionsCreateResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_110
                    go: *ref_111
                  protocol: {}
              language:
                default: &ref_113
                  name: ExtensionsCreateResponse
                  description: ExtensionsCreateResponse contains the response from method Extensions.Create.
                  responseType: true
                  resultEnv: *ref_112
                go: *ref_113
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_114
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: extensionId
                description: Id of extension resource.
                serializedName: extensionId
              go:
                name: extensionID
                description: Id of extension resource.
                serializedName: extensionId
            protocol:
              http:
                in: path
          - &ref_115
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_116
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_114
          - *ref_115
          - *ref_116
        responses:
          - schema: *ref_32
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Extensions_Get:
              parameters:
                api-version: 2020-05-12-preview
                extensionId: provider.extension
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: 'https://docs.provider.com/documentation/extension'
                      extensionAuthLink: 'https://www.provider.com/extension/'
                      extensionCategory: Weather
                      installedExtensionVersion: '1.0'
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
        language:
          default:
            name: Get
            description: Get installed extension details by extension id.
          go:
            name: Get
            description: |-
              Get installed extension details by extension id.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_311
              schema:
                type: object
                language:
                  default: &ref_117
                    name: ExtensionsGetOptions
                    description: ExtensionsGetOptions contains the optional parameters for the Extensions.Get method.
                  go: *ref_117
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsGetOptions
              language:
                default: &ref_118
                  name: ExtensionsGetOptions
                  description: ExtensionsGetOptions contains the optional parameters for the Extensions.Get method.
                go: *ref_118
              protocol: {}
            protocolNaming:
              errorMethod: getHandleError
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_322
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_119
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_119
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_120
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_120
                  protocol: {}
                - &ref_125
                  schema:
                    type: object
                    properties:
                      - &ref_123
                        schema: *ref_32
                        serializedName: Extension
                        language:
                          default: &ref_121
                            name: Extension
                            description: Extension resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_121
                        protocol: {}
                    language:
                      default: &ref_122
                        name: ExtensionsGetResult
                        description: ExtensionsGetResult contains the result from method Extensions.Get.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_122
                    protocol: {}
                  serializedName: ExtensionsGetResult
                  language:
                    default: &ref_124
                      name: ExtensionsGetResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_123
                    go: *ref_124
                  protocol: {}
              language:
                default: &ref_126
                  name: ExtensionsGetResponse
                  description: ExtensionsGetResponse contains the response from method Extensions.Get.
                  responseType: true
                  resultEnv: *ref_125
                go: *ref_126
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_127
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: extensionId
                description: Id of extension resource.
                serializedName: extensionId
              go:
                name: extensionID
                description: Id of extension resource.
                serializedName: extensionId
            protocol:
              http:
                in: path
          - &ref_128
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_129
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}'
                method: patch
                uri: '{$host}'
        signatureParameters:
          - *ref_127
          - *ref_128
          - *ref_129
        responses:
          - schema: *ref_32
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Extensions_Update:
              parameters:
                api-version: 2020-05-12-preview
                extensionId: provider.extension
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: 'https://docs.provider.com/documentation/extension'
                      extensionAuthLink: 'https://www.provider.com/extension/'
                      extensionCategory: Weather
                      installedExtensionVersion: '2.0'
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
        language:
          default:
            name: Update
            description: Upgrade to latest extension.
          go:
            name: Update
            description: |-
              Upgrade to latest extension.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_312
              schema:
                type: object
                language:
                  default: &ref_130
                    name: ExtensionsUpdateOptions
                    description: ExtensionsUpdateOptions contains the optional parameters for the Extensions.Update method.
                  go: *ref_130
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsUpdateOptions
              language:
                default: &ref_131
                  name: ExtensionsUpdateOptions
                  description: ExtensionsUpdateOptions contains the optional parameters for the Extensions.Update method.
                go: *ref_131
              protocol: {}
            protocolNaming:
              errorMethod: updateHandleError
              internalMethod: update
              requestMethod: updateCreateRequest
              responseMethod: updateHandleResponse
            responseEnv: &ref_323
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_132
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_132
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_133
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_133
                  protocol: {}
                - &ref_138
                  schema:
                    type: object
                    properties:
                      - &ref_136
                        schema: *ref_32
                        serializedName: Extension
                        language:
                          default: &ref_134
                            name: Extension
                            description: Extension resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_134
                        protocol: {}
                    language:
                      default: &ref_135
                        name: ExtensionsUpdateResult
                        description: ExtensionsUpdateResult contains the result from method Extensions.Update.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_135
                    protocol: {}
                  serializedName: ExtensionsUpdateResult
                  language:
                    default: &ref_137
                      name: ExtensionsUpdateResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_136
                    go: *ref_137
                  protocol: {}
              language:
                default: &ref_139
                  name: ExtensionsUpdateResponse
                  description: ExtensionsUpdateResponse contains the response from method Extensions.Update.
                  responseType: true
                  resultEnv: *ref_138
                go: *ref_139
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_140
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: extensionId
                description: Id of extension resource.
                serializedName: extensionId
              go:
                name: extensionID
                description: Id of extension resource.
                serializedName: extensionId
            protocol:
              http:
                in: path
          - &ref_141
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_142
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}'
                method: delete
                uri: '{$host}'
        signatureParameters:
          - *ref_140
          - *ref_141
          - *ref_142
        responses:
          - language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                statusCodes:
                  - '200'
          - language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                statusCodes:
                  - '204'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Extensions_Delete:
              parameters:
                api-version: 2020-05-12-preview
                extensionId: provider.extension
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200': {}
                '204': {}
        language:
          default:
            name: Delete
            description: Uninstall extension.
          go:
            name: Delete
            description: |-
              Uninstall extension.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_313
              schema:
                type: object
                language:
                  default: &ref_143
                    name: ExtensionsDeleteOptions
                    description: ExtensionsDeleteOptions contains the optional parameters for the Extensions.Delete method.
                  go: *ref_143
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsDeleteOptions
              language:
                default: &ref_144
                  name: ExtensionsDeleteOptions
                  description: ExtensionsDeleteOptions contains the optional parameters for the Extensions.Delete method.
                go: *ref_144
              protocol: {}
            protocolNaming:
              errorMethod: deleteHandleError
              internalMethod: deleteOperation
              requestMethod: deleteCreateRequest
              responseMethod: deleteHandleResponse
            responseEnv: &ref_324
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_145
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_145
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_146
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_146
                  protocol: {}
              language:
                default: &ref_147
                  name: ExtensionsDeleteResponse
                  description: ExtensionsDeleteResponse contains the response from method Extensions.Delete.
                  responseType: true
                go: *ref_147
              protocol: {}
        protocol: {}
      - &ref_161
        apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_158
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
          - &ref_159
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_150
            schema: *ref_148
            implementation: Method
            language:
              default:
                name: extensionIds
                description: Installed extension ids.
                serializedName: extensionIds
              go:
                name: ExtensionIDs
                description: Installed extension ids.
                byValue: true
                paramGroup: &ref_152
                  schema:
                    type: object
                    language:
                      default: &ref_149
                        name: ExtensionsListByFarmBeatsOptions
                        description: ExtensionsListByFarmBeatsOptions contains the optional parameters for the Extensions.ListByFarmBeats method.
                      go: *ref_149
                    protocol: {}
                  originalParameter:
                    - *ref_150
                    - &ref_155
                      schema: *ref_151
                      implementation: Method
                      language:
                        default:
                          name: extensionCategories
                          description: Installed extension categories.
                          serializedName: extensionCategories
                        go:
                          name: ExtensionCategories
                          description: Installed extension categories.
                          byValue: true
                          paramGroup: *ref_152
                          serializedName: extensionCategories
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_156
                      schema: *ref_153
                      implementation: Method
                      language:
                        default:
                          name: maxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          serializedName: $maxPageSize
                        go:
                          name: MaxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          paramGroup: *ref_152
                          serializedName: $maxPageSize
                      protocol:
                        http:
                          in: query
                    - &ref_157
                      schema: *ref_2
                      implementation: Method
                      language:
                        default:
                          name: skipToken
                          description: Skip token for getting next set of results.
                          serializedName: $skipToken
                        go:
                          name: SkipToken
                          description: Skip token for getting next set of results.
                          paramGroup: *ref_152
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: ExtensionsListByFarmBeatsOptions
                  language:
                    default: &ref_154
                      name: ExtensionsListByFarmBeatsOptions
                      description: ExtensionsListByFarmBeatsOptions contains the optional parameters for the Extensions.ListByFarmBeats method.
                    go: *ref_154
                  protocol: {}
                serializedName: extensionIds
            protocol:
              http:
                explode: true
                in: query
                style: form
          - *ref_155
          - *ref_156
          - *ref_157
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_158
          - *ref_159
          - *ref_150
          - *ref_155
          - *ref_156
          - *ref_157
        responses:
          - schema: *ref_160
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Extensions_ListByFarmBeats:
              parameters:
                api-version: 2020-05-12-preview
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    value:
                      - name: provider.extension
                        type: Microsoft.AgFoodPlatform/farmBeats/extensions
                        eTag: 7200b954-0000-0700-0000-603cbbc40000
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                        properties:
                          extensionApiDocsLink: 'https://docs.provider.com/documentation/extension'
                          extensionAuthLink: 'https://www.provider.com/extension/'
                          extensionCategory: Weather
                          installedExtensionVersion: '1.0'
                        systemData:
                          createdAt: '2020-02-01T01:01:01.1075056Z'
                          createdBy: string
                          createdByType: User
                          lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                          lastModifiedBy: string
                          lastModifiedByType: User
                  headers: {}
          x-ms-pageable:
            nextLinkName: nextLink
        language:
          default:
            name: ListByFarmBeats
            description: Get installed extensions details.
            paging:
              nextLinkName: nextLink
          go:
            name: ListByFarmBeats
            description: |-
              Get installed extensions details.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: *ref_152
            pageableType: &ref_305
              name: ExtensionsListByFarmBeatsPager
              op: *ref_161
            paging:
              nextLinkName: NextLink
            protocolNaming:
              errorMethod: listByFarmBeatsHandleError
              internalMethod: listByFarmBeats
              requestMethod: listByFarmBeatsCreateRequest
              responseMethod: listByFarmBeatsHandleResponse
            responseEnv: &ref_325
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_162
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_162
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_163
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_163
                  protocol: {}
                - &ref_168
                  schema:
                    type: object
                    properties:
                      - &ref_166
                        schema: *ref_160
                        serializedName: ExtensionListResponse
                        language:
                          default: &ref_164
                            name: ExtensionListResponse
                            description: Paged response contains list of requested objects and a URL link to get the next set of results.
                            byValue: true
                            embeddedType: true
                          go: *ref_164
                        protocol: {}
                    language:
                      default: &ref_165
                        name: ExtensionsListByFarmBeatsResult
                        description: ExtensionsListByFarmBeatsResult contains the result from method Extensions.ListByFarmBeats.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_165
                    protocol: {}
                  serializedName: ExtensionsListByFarmBeatsResult
                  language:
                    default: &ref_167
                      name: ExtensionsListByFarmBeatsResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_166
                    go: *ref_167
                  protocol: {}
              language:
                default: &ref_169
                  name: ExtensionsListByFarmBeatsResponse
                  description: ExtensionsListByFarmBeatsResponse contains the response from method Extensions.ListByFarmBeats.
                  responseType: true
                  resultEnv: *ref_168
                go: *ref_169
              protocol: {}
        protocol: {}
    language:
      default:
        name: Extensions
        description: ''
      go:
        name: Extensions
        description: ''
        clientCtorName: NewExtensionsClient
        clientName: ExtensionsClient
        clientParams:
          - *ref_97
    protocol: {}
  - $key: FarmBeatsExtensions
    operations:
      - &ref_183
        apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_172
            schema: *ref_170
            implementation: Method
            language:
              default:
                name: farmBeatsExtensionIds
                description: FarmBeatsExtension ids.
                serializedName: farmBeatsExtensionIds
              go:
                name: FarmBeatsExtensionIDs
                description: FarmBeatsExtension ids.
                byValue: true
                paramGroup: &ref_174
                  schema:
                    type: object
                    language:
                      default: &ref_171
                        name: FarmBeatsExtensionsListOptions
                        description: FarmBeatsExtensionsListOptions contains the optional parameters for the FarmBeatsExtensions.List method.
                      go: *ref_171
                    protocol: {}
                  originalParameter:
                    - *ref_172
                    - &ref_178
                      schema: *ref_173
                      implementation: Method
                      language:
                        default:
                          name: farmBeatsExtensionNames
                          description: FarmBeats extension names.
                          serializedName: farmBeatsExtensionNames
                        go:
                          name: FarmBeatsExtensionNames
                          description: FarmBeats extension names.
                          byValue: true
                          paramGroup: *ref_174
                          serializedName: farmBeatsExtensionNames
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_179
                      schema: *ref_175
                      implementation: Method
                      language:
                        default:
                          name: extensionCategories
                          description: Extension categories.
                          serializedName: extensionCategories
                        go:
                          name: ExtensionCategories
                          description: Extension categories.
                          byValue: true
                          paramGroup: *ref_174
                          serializedName: extensionCategories
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_180
                      schema: *ref_176
                      implementation: Method
                      language:
                        default:
                          name: publisherIds
                          description: Publisher ids.
                          serializedName: publisherIds
                        go:
                          name: PublisherIDs
                          description: Publisher ids.
                          byValue: true
                          paramGroup: *ref_174
                          serializedName: publisherIds
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_181
                      schema: *ref_153
                      implementation: Method
                      language:
                        default:
                          name: maxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          serializedName: $maxPageSize
                        go:
                          name: MaxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          paramGroup: *ref_174
                          serializedName: $maxPageSize
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsExtensionsListOptions
                  language:
                    default: &ref_177
                      name: FarmBeatsExtensionsListOptions
                      description: FarmBeatsExtensionsListOptions contains the optional parameters for the FarmBeatsExtensions.List method.
                    go: *ref_177
                  protocol: {}
                serializedName: farmBeatsExtensionIds
            protocol:
              http:
                explode: true
                in: query
                style: form
          - *ref_178
          - *ref_179
          - *ref_180
          - *ref_181
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: /providers/Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_172
          - *ref_178
          - *ref_179
          - *ref_180
          - *ref_181
        responses:
          - schema: *ref_182
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsExtensions_List:
              parameters:
                api-version: 2020-05-12-preview
              responses:
                '200':
                  body:
                    nextLink: string
                    value:
                      - name: DTN.ContentServices
                        type: Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions
                        id: Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions/DTN.ContentServices
                        properties:
                          detailedInformation:
                            - apiInputParameters:
                                - stationId
                                - lat
                                - lon
                                - days
                                - units
                                - precision
                                - sector
                              apiName: GetDailyObservations
                              customParameters:
                                - stationId
                                - stationLatitude
                                - stationLongitude
                                - timeZone
                                - sunrise
                                - sunset
                                - weatherCode
                                - weatherDescription
                                - maxTemperature
                                - minTemperature
                                - avgHeatIndex
                                - maxHeatIndex
                                - minHeatIndex
                                - maxWindChill
                                - minWindChill
                                - maxFeelsLike
                                - minFeelsLike
                                - avgFeelsLike
                                - maxWindSpeed
                                - avgWetBulbGlobeTemp
                                - maxWetBulbGlobeTemp
                                - minWetBulbGlobeTemp
                                - minutesOfSunshine
                                - cornHeatUnit
                                - evapotranspiration
                              platformParameters:
                                - cloudCover
                                - dewPoint
                                - growingDegreeDay
                                - precipitation
                                - pressure
                                - relativeHumidity
                                - temperature
                                - wetBulbTemperature
                                - dateTime
                                - windChill
                                - windSpeed
                                - windDirection
                              unitsSupported:
                                key: units
                                values:
                                  - us
                                  - si
                            - apiInputParameters:
                                - stationId
                                - lat
                                - lon
                                - hours
                                - units
                                - precision
                                - sector
                              apiName: GetHourlyObservations
                              customParameters:
                                - stationId
                                - stationLatitude
                                - stationLongitude
                                - timeZone
                                - weatherCode
                                - weatherDescription
                                - feelsLike
                                - visibilityWeatherCode
                                - visibilityWeatherDescription
                                - minutesOfSunshine
                              platformParameters:
                                - cloudCover
                                - dewPoint
                                - precipitation
                                - pressure
                                - relativeHumidity
                                - temperature
                                - wetBulbTemperature
                                - dateTime
                                - visibility
                                - windChill
                                - windSpeed
                                - windDirection
                                - windGust
                              unitsSupported:
                                key: units
                                values:
                                  - us
                                  - si
                            - apiInputParameters:
                                - stationId
                                - lat
                                - lon
                                - days
                                - units
                                - precision
                                - sector
                              apiName: GetHourlyForecasts
                              customParameters:
                                - stationId
                                - stationLatitude
                                - stationLongitude
                                - timeZone
                                - weatherCode
                                - weatherDescription
                                - feelsLike
                                - visibilityWeatherCode
                                - visibilityWeatherDescription
                                - minutesOfSunshine
                              platformParameters:
                                - cloudCover
                                - dewPoint
                                - precipitation
                                - pressure
                                - relativeHumidity
                                - temperature
                                - wetBulbTemperature
                                - dateTime
                                - visibility
                                - windChill
                                - windSpeed
                                - windDirection
                                - windGust
                              unitsSupported:
                                key: units
                                values:
                                  - us
                                  - si
                            - apiInputParameters:
                                - stationId
                                - lat
                                - lon
                                - days
                                - units
                                - precision
                                - sector
                              apiName: GetDailyForecasts
                              customParameters:
                                - stationId
                                - stationLatitude
                                - stationLongitude
                                - timeZone
                                - sunrise
                                - sunset
                                - weatherCode
                                - weatherDescription
                                - maxTemperature
                                - minTemperature
                                - avgHeatIndex
                                - maxHeatIndex
                                - minHeatIndex
                                - maxWindChill
                                - minWindChill
                                - maxFeelsLike
                                - minFeelsLike
                                - avgFeelsLike
                                - maxWindSpeed
                                - avgWetBulbGlobeTemp
                                - maxWetBulbGlobeTemp
                                - minWetBulbGlobeTemp
                                - minutesOfSunshine
                                - cornHeatUnit
                                - evapotranspiration
                              platformParameters:
                                - cloudCover
                                - dewPoint
                                - growingDegreeDay
                                - precipitation
                                - pressure
                                - relativeHumidity
                                - temperature
                                - wetBulbTemperature
                                - dateTime
                                - windChill
                                - windSpeed
                                - windDirection
                              unitsSupported:
                                key: units
                                values:
                                  - us
                                  - si
                          extensionApiDocsLink: 'https://cs-docs.dtn.com/api/weather-observations-and-forecasts-rest-api/'
                          extensionAuthLink: 'https://www.dtn.com/dtn-content-integration/'
                          extensionCategory: Weather
                          farmBeatsExtensionId: DTN.ContentServices
                          farmBeatsExtensionName: DTN
                          farmBeatsExtensionVersion: '1.0'
                          publisherId: dtn
                          targetResourceType: FarmBeats
                        systemData:
                          createdAt: '2021-04-12T15:28:06Z'
                          lastModifiedAt: '2021-04-12T15:30:01Z'
                  headers: {}
          x-ms-pageable:
            nextLinkName: nextLink
        language:
          default:
            name: List
            description: Get list of farmBeats extension.
            paging:
              nextLinkName: nextLink
          go:
            name: List
            description: |-
              Get list of farmBeats extension.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsExtensionsClient
            openApiType: arm
            optionalParamGroup: *ref_174
            pageableType: &ref_306
              name: FarmBeatsExtensionsListPager
              op: *ref_183
            paging:
              nextLinkName: NextLink
            protocolNaming:
              errorMethod: listHandleError
              internalMethod: listOperation
              requestMethod: listCreateRequest
              responseMethod: listHandleResponse
            responseEnv: &ref_326
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_184
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_184
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_185
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_185
                  protocol: {}
                - &ref_190
                  schema:
                    type: object
                    properties:
                      - &ref_188
                        schema: *ref_182
                        serializedName: FarmBeatsExtensionListResponse
                        language:
                          default: &ref_186
                            name: FarmBeatsExtensionListResponse
                            description: Paged response contains list of requested objects and a URL link to get the next set of results.
                            byValue: true
                            embeddedType: true
                          go: *ref_186
                        protocol: {}
                    language:
                      default: &ref_187
                        name: FarmBeatsExtensionsListResult
                        description: FarmBeatsExtensionsListResult contains the result from method FarmBeatsExtensions.List.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_187
                    protocol: {}
                  serializedName: FarmBeatsExtensionsListResult
                  language:
                    default: &ref_189
                      name: FarmBeatsExtensionsListResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_188
                    go: *ref_189
                  protocol: {}
              language:
                default: &ref_191
                  name: FarmBeatsExtensionsListResponse
                  description: FarmBeatsExtensionsListResponse contains the response from method FarmBeatsExtensions.List.
                  responseType: true
                  resultEnv: *ref_190
                go: *ref_191
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_193
            schema: *ref_192
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsExtensionId
                description: farmBeatsExtensionId to be queried.
                serializedName: farmBeatsExtensionId
              go:
                name: farmBeatsExtensionID
                description: farmBeatsExtensionId to be queried.
                serializedName: farmBeatsExtensionId
            protocol:
              http:
                in: path
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/providers/Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions/{farmBeatsExtensionId}'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_193
        responses:
          - schema: *ref_33
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsExtensions_Get:
              parameters:
                api-version: 2020-05-12-preview
                farmBeatsExtensionId: DTN.ContentServices
              responses:
                '200':
                  body:
                    name: DTN.ContentServices
                    type: Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions
                    id: Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions/DTN.ContentServices
                    properties:
                      detailedInformation:
                        - apiInputParameters:
                            - stationId
                            - lat
                            - lon
                            - days
                            - units
                            - precision
                            - sector
                          apiName: GetDailyObservations
                          customParameters:
                            - stationId
                            - stationLatitude
                            - stationLongitude
                            - timeZone
                            - sunrise
                            - sunset
                            - weatherCode
                            - weatherDescription
                            - maxTemperature
                            - minTemperature
                            - avgHeatIndex
                            - maxHeatIndex
                            - minHeatIndex
                            - maxWindChill
                            - minWindChill
                            - maxFeelsLike
                            - minFeelsLike
                            - avgFeelsLike
                            - maxWindSpeed
                            - avgWetBulbGlobeTemp
                            - maxWetBulbGlobeTemp
                            - minWetBulbGlobeTemp
                            - minutesOfSunshine
                            - cornHeatUnit
                            - evapotranspiration
                          platformParameters:
                            - cloudCover
                            - dewPoint
                            - growingDegreeDay
                            - precipitation
                            - pressure
                            - relativeHumidity
                            - temperature
                            - wetBulbTemperature
                            - dateTime
                            - windChill
                            - windSpeed
                            - windDirection
                          unitsSupported:
                            key: units
                            values:
                              - us
                              - si
                        - apiInputParameters:
                            - stationId
                            - lat
                            - lon
                            - hours
                            - units
                            - precision
                            - sector
                          apiName: GetHourlyObservations
                          customParameters:
                            - stationId
                            - stationLatitude
                            - stationLongitude
                            - timeZone
                            - weatherCode
                            - weatherDescription
                            - feelsLike
                            - visibilityWeatherCode
                            - visibilityWeatherDescription
                            - minutesOfSunshine
                          platformParameters:
                            - cloudCover
                            - dewPoint
                            - precipitation
                            - pressure
                            - relativeHumidity
                            - temperature
                            - wetBulbTemperature
                            - dateTime
                            - visibility
                            - windChill
                            - windSpeed
                            - windDirection
                            - windGust
                          unitsSupported:
                            key: units
                            values:
                              - us
                              - si
                        - apiInputParameters:
                            - stationId
                            - lat
                            - lon
                            - days
                            - units
                            - precision
                            - sector
                          apiName: GetHourlyForecasts
                          customParameters:
                            - stationId
                            - stationLatitude
                            - stationLongitude
                            - timeZone
                            - weatherCode
                            - weatherDescription
                            - feelsLike
                            - visibilityWeatherCode
                            - visibilityWeatherDescription
                            - minutesOfSunshine
                          platformParameters:
                            - cloudCover
                            - dewPoint
                            - precipitation
                            - pressure
                            - relativeHumidity
                            - temperature
                            - wetBulbTemperature
                            - dateTime
                            - visibility
                            - windChill
                            - windSpeed
                            - windDirection
                            - windGust
                          unitsSupported:
                            key: units
                            values:
                              - us
                              - si
                        - apiInputParameters:
                            - stationId
                            - lat
                            - lon
                            - days
                            - units
                            - precision
                            - sector
                          apiName: GetDailyForecasts
                          customParameters:
                            - stationId
                            - stationLatitude
                            - stationLongitude
                            - timeZone
                            - sunrise
                            - sunset
                            - weatherCode
                            - weatherDescription
                            - maxTemperature
                            - minTemperature
                            - avgHeatIndex
                            - maxHeatIndex
                            - minHeatIndex
                            - maxWindChill
                            - minWindChill
                            - maxFeelsLike
                            - minFeelsLike
                            - avgFeelsLike
                            - maxWindSpeed
                            - avgWetBulbGlobeTemp
                            - maxWetBulbGlobeTemp
                            - minWetBulbGlobeTemp
                            - minutesOfSunshine
                            - cornHeatUnit
                            - evapotranspiration
                          platformParameters:
                            - cloudCover
                            - dewPoint
                            - growingDegreeDay
                            - precipitation
                            - pressure
                            - relativeHumidity
                            - temperature
                            - wetBulbTemperature
                            - dateTime
                            - windChill
                            - windSpeed
                            - windDirection
                          unitsSupported:
                            key: units
                            values:
                              - us
                              - si
                      extensionApiDocsLink: 'https://cs-docs.dtn.com/api/weather-observations-and-forecasts-rest-api/'
                      extensionAuthLink: 'https://www.dtn.com/dtn-content-integration/'
                      extensionCategory: Weather
                      farmBeatsExtensionId: DTN.ContentServices
                      farmBeatsExtensionName: DTN
                      farmBeatsExtensionVersion: '1.0'
                      publisherId: dtn
                      targetResourceType: FarmBeats
                    systemData:
                      createdAt: '2021-04-12T15:28:06Z'
                      lastModifiedAt: '2021-04-12T15:30:01Z'
        language:
          default:
            name: Get
            description: Get farmBeats extension.
          go:
            name: Get
            description: |-
              Get farmBeats extension.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_314
              schema:
                type: object
                language:
                  default: &ref_194
                    name: FarmBeatsExtensionsGetOptions
                    description: FarmBeatsExtensionsGetOptions contains the optional parameters for the FarmBeatsExtensions.Get method.
                  go: *ref_194
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsExtensionsGetOptions
              language:
                default: &ref_195
                  name: FarmBeatsExtensionsGetOptions
                  description: FarmBeatsExtensionsGetOptions contains the optional parameters for the FarmBeatsExtensions.Get method.
                go: *ref_195
              protocol: {}
            protocolNaming:
              errorMethod: getHandleError
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_327
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_196
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_196
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_197
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_197
                  protocol: {}
                - &ref_202
                  schema:
                    type: object
                    properties:
                      - &ref_200
                        schema: *ref_33
                        serializedName: FarmBeatsExtension
                        language:
                          default: &ref_198
                            name: FarmBeatsExtension
                            description: FarmBeats extension resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_198
                        protocol: {}
                    language:
                      default: &ref_199
                        name: FarmBeatsExtensionsGetResult
                        description: FarmBeatsExtensionsGetResult contains the result from method FarmBeatsExtensions.Get.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_199
                    protocol: {}
                  serializedName: FarmBeatsExtensionsGetResult
                  language:
                    default: &ref_201
                      name: FarmBeatsExtensionsGetResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_200
                    go: *ref_201
                  protocol: {}
              language:
                default: &ref_203
                  name: FarmBeatsExtensionsGetResponse
                  description: FarmBeatsExtensionsGetResponse contains the response from method FarmBeatsExtensions.Get.
                  responseType: true
                  resultEnv: *ref_202
                go: *ref_203
              protocol: {}
        protocol: {}
    language:
      default:
        name: FarmBeatsExtensions
        description: ''
      go:
        name: FarmBeatsExtensions
        description: ''
        clientCtorName: NewFarmBeatsExtensionsClient
        clientName: FarmBeatsExtensionsClient
    protocol: {}
  - $key: FarmBeatsModels
    operations:
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_204
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
          - &ref_205
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_204
          - *ref_205
        responses:
          - schema: *ref_37
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_Get:
              parameters:
                api-version: 2020-05-12-preview
                farmBeatsResourceName: examples-farmBeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    name: examples-farmBeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                      provisioningState: Succeeded
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
                    tags:
                      key1: value1
                      key2: value2
        language:
          default:
            name: Get
            description: Get FarmBeats resource.
          go:
            name: Get
            description: |-
              Get FarmBeats resource.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_315
              schema:
                type: object
                language:
                  default: &ref_206
                    name: FarmBeatsModelsGetOptions
                    description: FarmBeatsModelsGetOptions contains the optional parameters for the FarmBeatsModels.Get method.
                  go: *ref_206
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsGetOptions
              language:
                default: &ref_207
                  name: FarmBeatsModelsGetOptions
                  description: FarmBeatsModelsGetOptions contains the optional parameters for the FarmBeatsModels.Get method.
                go: *ref_207
              protocol: {}
            protocolNaming:
              errorMethod: getHandleError
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_328
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_208
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_208
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_209
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_209
                  protocol: {}
                - &ref_214
                  schema:
                    type: object
                    properties:
                      - &ref_212
                        schema: *ref_37
                        serializedName: FarmBeats
                        language:
                          default: &ref_210
                            name: FarmBeats
                            description: FarmBeats ARM Resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_210
                        protocol: {}
                    language:
                      default: &ref_211
                        name: FarmBeatsModelsGetResult
                        description: FarmBeatsModelsGetResult contains the result from method FarmBeatsModels.Get.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_211
                    protocol: {}
                  serializedName: FarmBeatsModelsGetResult
                  language:
                    default: &ref_213
                      name: FarmBeatsModelsGetResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_212
                    go: *ref_213
                  protocol: {}
              language:
                default: &ref_215
                  name: FarmBeatsModelsGetResponse
                  description: FarmBeatsModelsGetResponse contains the response from method FarmBeatsModels.Get.
                  responseType: true
                  resultEnv: *ref_214
                go: *ref_215
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_217
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_218
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - &ref_216
                schema: *ref_37
                implementation: Method
                required: true
                language:
                  default:
                    name: body
                    description: FarmBeats resource create or update request object.
                  go:
                    name: body
                    description: FarmBeats resource create or update request object.
                protocol:
                  http:
                    in: body
                    style: json
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters:
              - *ref_216
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}'
                method: put
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters:
          - *ref_217
          - *ref_218
        responses:
          - schema: *ref_37
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
          - schema: *ref_37
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '201'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_CreateOrUpdate:
              parameters:
                api-version: 2020-05-12-preview
                body:
                  location: eastus2
                  tags:
                    key1: value1
                    key2: value2
                farmBeatsResourceName: examples-farmbeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    name: examples-farmbeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                      provisioningState: Succeeded
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
                    tags:
                      key1: value1
                      key2: value2
                '201':
                  body:
                    name: examples-farmbeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                      provisioningState: Failed
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
                    tags:
                      key1: value1
                      key2: value2
        language:
          default:
            name: CreateOrUpdate
            description: Create or update FarmBeats resource.
          go:
            name: CreateOrUpdate
            description: |-
              Create or update FarmBeats resource.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_316
              schema:
                type: object
                language:
                  default: &ref_219
                    name: FarmBeatsModelsCreateOrUpdateOptions
                    description: FarmBeatsModelsCreateOrUpdateOptions contains the optional parameters for the FarmBeatsModels.CreateOrUpdate method.
                  go: *ref_219
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsCreateOrUpdateOptions
              language:
                default: &ref_220
                  name: FarmBeatsModelsCreateOrUpdateOptions
                  description: FarmBeatsModelsCreateOrUpdateOptions contains the optional parameters for the FarmBeatsModels.CreateOrUpdate method.
                go: *ref_220
              protocol: {}
            protocolNaming:
              errorMethod: createOrUpdateHandleError
              internalMethod: createOrUpdate
              requestMethod: createOrUpdateCreateRequest
              responseMethod: createOrUpdateHandleResponse
            responseEnv: &ref_329
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_221
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_221
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_222
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_222
                  protocol: {}
                - &ref_227
                  schema:
                    type: object
                    properties:
                      - &ref_225
                        schema: *ref_37
                        serializedName: FarmBeats
                        language:
                          default: &ref_223
                            name: FarmBeats
                            description: FarmBeats ARM Resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_223
                        protocol: {}
                    language:
                      default: &ref_224
                        name: FarmBeatsModelsCreateOrUpdateResult
                        description: FarmBeatsModelsCreateOrUpdateResult contains the result from method FarmBeatsModels.CreateOrUpdate.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_224
                    protocol: {}
                  serializedName: FarmBeatsModelsCreateOrUpdateResult
                  language:
                    default: &ref_226
                      name: FarmBeatsModelsCreateOrUpdateResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_225
                    go: *ref_226
                  protocol: {}
              language:
                default: &ref_228
                  name: FarmBeatsModelsCreateOrUpdateResponse
                  description: FarmBeatsModelsCreateOrUpdateResponse contains the response from method FarmBeatsModels.CreateOrUpdate.
                  responseType: true
                  resultEnv: *ref_227
                go: *ref_228
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_231
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
          - &ref_232
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - &ref_230
                schema: *ref_229
                implementation: Method
                required: true
                language:
                  default:
                    name: body
                    description: Request object.
                  go:
                    name: body
                    description: Request object.
                protocol:
                  http:
                    in: body
                    style: json
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters:
              - *ref_230
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}'
                method: patch
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters:
          - *ref_231
          - *ref_232
        responses:
          - schema: *ref_37
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_Update:
              parameters:
                api-version: 2020-05-12-preview
                body:
                  tags:
                    key1: value1
                    key2: value2
                farmBeatsResourceName: examples-farmBeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    name: examples-farmBeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                      provisioningState: Succeeded
                    systemData:
                      createdAt: '2020-02-01T01:01:01.1075056Z'
                      createdBy: string
                      createdByType: User
                      lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                      lastModifiedBy: string
                      lastModifiedByType: User
                    tags:
                      key1: value1
                      key2: value2
        language:
          default:
            name: Update
            description: Update a FarmBeats resource.
          go:
            name: Update
            description: |-
              Update a FarmBeats resource.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_317
              schema:
                type: object
                language:
                  default: &ref_233
                    name: FarmBeatsModelsUpdateOptions
                    description: FarmBeatsModelsUpdateOptions contains the optional parameters for the FarmBeatsModels.Update method.
                  go: *ref_233
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsUpdateOptions
              language:
                default: &ref_234
                  name: FarmBeatsModelsUpdateOptions
                  description: FarmBeatsModelsUpdateOptions contains the optional parameters for the FarmBeatsModels.Update method.
                go: *ref_234
              protocol: {}
            protocolNaming:
              errorMethod: updateHandleError
              internalMethod: update
              requestMethod: updateCreateRequest
              responseMethod: updateHandleResponse
            responseEnv: &ref_330
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_235
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_235
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_236
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_236
                  protocol: {}
                - &ref_241
                  schema:
                    type: object
                    properties:
                      - &ref_239
                        schema: *ref_37
                        serializedName: FarmBeats
                        language:
                          default: &ref_237
                            name: FarmBeats
                            description: FarmBeats ARM Resource.
                            byValue: true
                            embeddedType: true
                          go: *ref_237
                        protocol: {}
                    language:
                      default: &ref_238
                        name: FarmBeatsModelsUpdateResult
                        description: FarmBeatsModelsUpdateResult contains the result from method FarmBeatsModels.Update.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_238
                    protocol: {}
                  serializedName: FarmBeatsModelsUpdateResult
                  language:
                    default: &ref_240
                      name: FarmBeatsModelsUpdateResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_239
                    go: *ref_240
                  protocol: {}
              language:
                default: &ref_242
                  name: FarmBeatsModelsUpdateResponse
                  description: FarmBeatsModelsUpdateResponse contains the response from method FarmBeatsModels.Update.
                  responseType: true
                  resultEnv: *ref_241
                go: *ref_242
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_243
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
          - &ref_244
            schema: *ref_2
            implementation: Method
            required: true
            language:
              default:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
              go:
                name: farmBeatsResourceName
                description: FarmBeats resource name.
                serializedName: farmBeatsResourceName
            protocol:
              http:
                in: path
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}'
                method: delete
                uri: '{$host}'
        signatureParameters:
          - *ref_243
          - *ref_244
        responses:
          - language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                statusCodes:
                  - '200'
          - language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                statusCodes:
                  - '204'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_Delete:
              parameters:
                api-version: 2020-05-12-preview
                farmBeatsResourceName: examples-farmBeatsResourceName
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200': {}
                '204': {}
        language:
          default:
            name: Delete
            description: Delete a FarmBeats resource.
          go:
            name: Delete
            description: |-
              Delete a FarmBeats resource.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_318
              schema:
                type: object
                language:
                  default: &ref_245
                    name: FarmBeatsModelsDeleteOptions
                    description: FarmBeatsModelsDeleteOptions contains the optional parameters for the FarmBeatsModels.Delete method.
                  go: *ref_245
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsDeleteOptions
              language:
                default: &ref_246
                  name: FarmBeatsModelsDeleteOptions
                  description: FarmBeatsModelsDeleteOptions contains the optional parameters for the FarmBeatsModels.Delete method.
                go: *ref_246
              protocol: {}
            protocolNaming:
              errorMethod: deleteHandleError
              internalMethod: deleteOperation
              requestMethod: deleteCreateRequest
              responseMethod: deleteHandleResponse
            responseEnv: &ref_331
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_247
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_247
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_248
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_248
                  protocol: {}
              language:
                default: &ref_249
                  name: FarmBeatsModelsDeleteResponse
                  description: FarmBeatsModelsDeleteResponse contains the response from method FarmBeatsModels.Delete.
                  responseType: true
                go: *ref_249
              protocol: {}
        protocol: {}
      - &ref_256
        apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_251
            schema: *ref_153
            implementation: Method
            language:
              default:
                name: maxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                serializedName: $maxPageSize
              go:
                name: MaxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                paramGroup: &ref_252
                  schema:
                    type: object
                    language:
                      default: &ref_250
                        name: FarmBeatsModelsListBySubscriptionOptions
                        description: FarmBeatsModelsListBySubscriptionOptions contains the optional parameters for the FarmBeatsModels.ListBySubscription method.
                      go: *ref_250
                    protocol: {}
                  originalParameter:
                    - *ref_251
                    - &ref_254
                      schema: *ref_2
                      implementation: Method
                      language:
                        default:
                          name: skipToken
                          description: Skip token for getting next set of results.
                          serializedName: $skipToken
                        go:
                          name: SkipToken
                          description: Skip token for getting next set of results.
                          paramGroup: *ref_252
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsModelsListBySubscriptionOptions
                  language:
                    default: &ref_253
                      name: FarmBeatsModelsListBySubscriptionOptions
                      description: FarmBeatsModelsListBySubscriptionOptions contains the optional parameters for the FarmBeatsModels.ListBySubscription method.
                    go: *ref_253
                  protocol: {}
                serializedName: $maxPageSize
            protocol:
              http:
                in: query
          - *ref_254
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/providers/Microsoft.AgFoodPlatform/farmBeats'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_251
          - *ref_254
        responses:
          - schema: *ref_255
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_ListBySubscription:
              parameters:
                api-version: 2020-05-12-preview
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    value:
                      - name: examples-farmBeatsResourceName
                        type: Microsoft.AgFoodPlatform/farmBeats
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                        location: eastus2
                        properties:
                          instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                          provisioningState: Succeeded
                        systemData:
                          createdAt: '2020-02-01T01:01:01.1075056Z'
                          createdBy: string
                          createdByType: User
                          lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                          lastModifiedBy: string
                          lastModifiedByType: User
                        tags:
                          key1: value1
                          key2: value2
                  headers: {}
          x-ms-pageable:
            nextLinkName: nextLink
        language:
          default:
            name: ListBySubscription
            description: Lists the FarmBeats instances for a subscription.
            paging:
              nextLinkName: nextLink
          go:
            name: ListBySubscription
            description: |-
              Lists the FarmBeats instances for a subscription.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: *ref_252
            pageableType: &ref_307
              name: FarmBeatsModelsListBySubscriptionPager
              op: *ref_256
            paging:
              nextLinkName: NextLink
            protocolNaming:
              errorMethod: listBySubscriptionHandleError
              internalMethod: listBySubscription
              requestMethod: listBySubscriptionCreateRequest
              responseMethod: listBySubscriptionHandleResponse
            responseEnv: &ref_332
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_257
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_257
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_258
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_258
                  protocol: {}
                - &ref_263
                  schema:
                    type: object
                    properties:
                      - &ref_261
                        schema: *ref_255
                        serializedName: FarmBeatsListResponse
                        language:
                          default: &ref_259
                            name: FarmBeatsListResponse
                            description: Paged response contains list of requested objects and a URL link to get the next set of results.
                            byValue: true
                            embeddedType: true
                          go: *ref_259
                        protocol: {}
                    language:
                      default: &ref_260
                        name: FarmBeatsModelsListBySubscriptionResult
                        description: FarmBeatsModelsListBySubscriptionResult contains the result from method FarmBeatsModels.ListBySubscription.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_260
                    protocol: {}
                  serializedName: FarmBeatsModelsListBySubscriptionResult
                  language:
                    default: &ref_262
                      name: FarmBeatsModelsListBySubscriptionResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_261
                    go: *ref_262
                  protocol: {}
              language:
                default: &ref_264
                  name: FarmBeatsModelsListBySubscriptionResponse
                  description: FarmBeatsModelsListBySubscriptionResponse contains the response from method FarmBeatsModels.ListBySubscription.
                  responseType: true
                  resultEnv: *ref_263
                go: *ref_264
              protocol: {}
        protocol: {}
      - &ref_271
        apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_266
            schema: *ref_153
            implementation: Method
            language:
              default:
                name: maxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                serializedName: $maxPageSize
              go:
                name: MaxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                paramGroup: &ref_267
                  schema:
                    type: object
                    language:
                      default: &ref_265
                        name: FarmBeatsModelsListByResourceGroupOptions
                        description: FarmBeatsModelsListByResourceGroupOptions contains the optional parameters for the FarmBeatsModels.ListByResourceGroup method.
                      go: *ref_265
                    protocol: {}
                  originalParameter:
                    - *ref_266
                    - &ref_269
                      schema: *ref_2
                      implementation: Method
                      language:
                        default:
                          name: skipToken
                          description: Continuation token for getting next set of results.
                          serializedName: $skipToken
                        go:
                          name: SkipToken
                          description: Continuation token for getting next set of results.
                          paramGroup: *ref_267
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsModelsListByResourceGroupOptions
                  language:
                    default: &ref_268
                      name: FarmBeatsModelsListByResourceGroupOptions
                      description: FarmBeatsModelsListByResourceGroupOptions contains the optional parameters for the FarmBeatsModels.ListByResourceGroup method.
                    go: *ref_268
                  protocol: {}
                serializedName: $maxPageSize
            protocol:
              http:
                in: query
          - *ref_269
          - &ref_270
            schema: *ref_96
            implementation: Method
            required: true
            language:
              default:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
              go:
                name: resourceGroupName
                description: The name of the resource group. The name is case insensitive.
                serializedName: resourceGroupName
            protocol:
              http:
                in: path
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats'
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_266
          - *ref_269
          - *ref_270
        responses:
          - schema: *ref_255
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            FarmBeatsModels_ListByResourceGroup:
              parameters:
                api-version: 2020-05-12-preview
                resourceGroupName: examples-rg
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    value:
                      - name: examples-farmBeatsResourceName
                        type: Microsoft.AgFoodPlatform/farmBeats
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                        location: eastus2
                        properties:
                          instanceUri: 'https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net'
                          provisioningState: Succeeded
                        systemData:
                          createdAt: '2020-02-01T01:01:01.1075056Z'
                          createdBy: string
                          createdByType: User
                          lastModifiedAt: '2020-02-01T01:01:01.1075056Z'
                          lastModifiedBy: string
                          lastModifiedByType: User
                        tags:
                          key1: value1
                          key2: value2
                  headers: {}
          x-ms-pageable:
            nextLinkName: nextLink
        language:
          default:
            name: ListByResourceGroup
            description: Lists the FarmBeats instances for a resource group.
            paging:
              nextLinkName: nextLink
          go:
            name: ListByResourceGroup
            description: |-
              Lists the FarmBeats instances for a resource group.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: *ref_267
            pageableType: &ref_308
              name: FarmBeatsModelsListByResourceGroupPager
              op: *ref_271
            paging:
              nextLinkName: NextLink
            protocolNaming:
              errorMethod: listByResourceGroupHandleError
              internalMethod: listByResourceGroup
              requestMethod: listByResourceGroupCreateRequest
              responseMethod: listByResourceGroupHandleResponse
            responseEnv: &ref_333
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_272
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_272
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_273
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_273
                  protocol: {}
                - &ref_278
                  schema:
                    type: object
                    properties:
                      - &ref_276
                        schema: *ref_255
                        serializedName: FarmBeatsListResponse
                        language:
                          default: &ref_274
                            name: FarmBeatsListResponse
                            description: Paged response contains list of requested objects and a URL link to get the next set of results.
                            byValue: true
                            embeddedType: true
                          go: *ref_274
                        protocol: {}
                    language:
                      default: &ref_275
                        name: FarmBeatsModelsListByResourceGroupResult
                        description: FarmBeatsModelsListByResourceGroupResult contains the result from method FarmBeatsModels.ListByResourceGroup.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_275
                    protocol: {}
                  serializedName: FarmBeatsModelsListByResourceGroupResult
                  language:
                    default: &ref_277
                      name: FarmBeatsModelsListByResourceGroupResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_276
                    go: *ref_277
                  protocol: {}
              language:
                default: &ref_279
                  name: FarmBeatsModelsListByResourceGroupResponse
                  description: FarmBeatsModelsListByResourceGroupResponse contains the response from method FarmBeatsModels.ListByResourceGroup.
                  responseType: true
                  resultEnv: *ref_278
                go: *ref_279
              protocol: {}
        protocol: {}
    language:
      default:
        name: FarmBeatsModels
        description: ''
      go:
        name: FarmBeatsModels
        description: ''
        clientCtorName: NewFarmBeatsModelsClient
        clientName: FarmBeatsModelsClient
        clientParams:
          - *ref_97
    protocol: {}
  - $key: Locations
    operations:
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - &ref_281
                schema: *ref_280
                implementation: Method
                required: true
                language:
                  default:
                    name: body
                    description: NameAvailabilityRequest object.
                  go:
                    name: body
                    description: NameAvailabilityRequest object.
                protocol:
                  http:
                    in: body
                    style: json
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters:
              - *ref_281
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: '/subscriptions/{subscriptionId}/providers/Microsoft.AgFoodPlatform/checkNameAvailability'
                method: post
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters: []
        responses:
          - schema: *ref_282
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Locations_CheckNameAvailability_AlreadyExists:
              parameters:
                api-version: 2020-05-12-preview
                body:
                  name: existingaccountname
                  type: Microsoft.AgFoodPlatform/farmBeats
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    message: An account named 'existingaccountname' is already in use.
                    nameAvailable: false
                    reason: AlreadyExists
            Locations_CheckNameAvailability_Available:
              parameters:
                api-version: 2020-05-12-preview
                body:
                  name: newaccountname
                  type: Microsoft.AgFoodPlatform/farmBeats
                subscriptionId: 11111111-2222-3333-4444-555555555555
              responses:
                '200':
                  body:
                    nameAvailable: true
        language:
          default:
            name: CheckNameAvailability
            description: Checks the name availability of the resource with requested resource name.
          go:
            name: CheckNameAvailability
            description: |-
              Checks the name availability of the resource with requested resource name.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: LocationsClient
            openApiType: arm
            optionalParamGroup: &ref_319
              schema:
                type: object
                language:
                  default: &ref_283
                    name: LocationsCheckNameAvailabilityOptions
                    description: LocationsCheckNameAvailabilityOptions contains the optional parameters for the Locations.CheckNameAvailability method.
                  go: *ref_283
                protocol: {}
              originalParameter: []
              required: false
              serializedName: LocationsCheckNameAvailabilityOptions
              language:
                default: &ref_284
                  name: LocationsCheckNameAvailabilityOptions
                  description: LocationsCheckNameAvailabilityOptions contains the optional parameters for the Locations.CheckNameAvailability method.
                go: *ref_284
              protocol: {}
            protocolNaming:
              errorMethod: checkNameAvailabilityHandleError
              internalMethod: checkNameAvailability
              requestMethod: checkNameAvailabilityCreateRequest
              responseMethod: checkNameAvailabilityHandleResponse
            responseEnv: &ref_334
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_285
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_285
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_286
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_286
                  protocol: {}
                - &ref_291
                  schema:
                    type: object
                    properties:
                      - &ref_289
                        schema: *ref_282
                        serializedName: CheckNameAvailabilityResponse
                        language:
                          default: &ref_287
                            name: CheckNameAvailabilityResponse
                            description: The check availability result.
                            byValue: true
                            embeddedType: true
                          go: *ref_287
                        protocol: {}
                    language:
                      default: &ref_288
                        name: LocationsCheckNameAvailabilityResult
                        description: LocationsCheckNameAvailabilityResult contains the result from method Locations.CheckNameAvailability.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_288
                    protocol: {}
                  serializedName: LocationsCheckNameAvailabilityResult
                  language:
                    default: &ref_290
                      name: LocationsCheckNameAvailabilityResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_289
                    go: *ref_290
                  protocol: {}
              language:
                default: &ref_292
                  name: LocationsCheckNameAvailabilityResponse
                  description: LocationsCheckNameAvailabilityResponse contains the response from method Locations.CheckNameAvailability.
                  responseType: true
                  resultEnv: *ref_291
                go: *ref_292
              protocol: {}
        protocol: {}
    language:
      default:
        name: Locations
        description: ''
      go:
        name: Locations
        description: ''
        clientCtorName: NewLocationsClient
        clientName: LocationsClient
        clientParams:
          - *ref_97
    protocol: {}
  - $key: Operations
    operations:
      - &ref_296
        apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: 'modelerfour:synthesized/accept'
                required: true
                language:
                  default:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                  go:
                    name: accept
                    description: Accept header
                    serializedName: Accept
                protocol:
                  http:
                    in: header
            signatureParameters: []
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: /providers/Microsoft.AgFoodPlatform/operations
                method: get
                uri: '{$host}'
        signatureParameters: []
        responses:
          - schema: *ref_293
            language:
              default:
                name: ''
                description: Success
              go:
                name: ''
                description: Success
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - '200'
        exceptions:
          - schema: *ref_103
            language:
              default:
                name: ''
                description: Error
              go:
                name: ''
                description: Error
            protocol:
              http:
                knownMediaType: json
                mediaTypes:
                  - application/json
                statusCodes:
                  - default
        extensions:
          x-ms-examples:
            Operations_List:
              parameters:
                api-version: 2020-05-12-preview
              responses:
                '200':
                  body:
                    nextLink: 'https://management.azure.com/providers/Microsoft.AgFoodPlatform/operations?$skiptoken={token}'
                    value:
                      - name: Microsoft.AgFoodPlatform/farmBeats/read
                        display:
                          description: Gets or Lists existing AgFoodPlatform FarmBeats resource(s).
                          operation: Get or List AgFoodPlatform FarmBeats resource(s).
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform FarmBeats
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/farmBeats/write
                        display:
                          description: Creates or Updates AgFoodPlatform FarmBeats.
                          operation: Create or Update AgFoodPlatform FarmBeats.
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform FarmBeats
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/farmBeats/delete
                        display:
                          description: Deletes an existing AgFoodPlatform FarmBeats resource.
                          operation: Delete AgFoodPlatform FarmBeats resource.
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform FarmBeats
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/locations/checkNameAvailability/action
                        display:
                          description: Checks that resource name is valid and is not in use.
                          operation: Check Name Availability
                          provider: Microsoft AgFoodPlatform
                          resource: Locations
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/operations/read
                        display:
                          description: List all operations in Microsoft AgFoodPlatform resource provider.
                          operation: List all operations.
                          provider: Microsoft AgFoodPlatform
                          resource: List all operations in Microsoft AgFoodPlatform resource provider.
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/farmBeats/extensions/read
                        display:
                          description: Gets or Lists existing AgFoodPlatform Extensions resource(s).
                          operation: Get or List AgFoodPlatform Extensions resource(s).
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform Extensions
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/farmBeats/extensions/write
                        display:
                          description: Creates or Updates AgFoodPlatform Extensions.
                          operation: Create or Update AgFoodPlatform Extensions.
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform Extensions
                        isDataAction: false
                      - name: Microsoft.AgFoodPlatform/farmBeats/extensions/delete
                        display:
                          description: Deletes an existing AgFoodPlatform Extensions resource.
                          operation: Delete AgFoodPlatform Extensions resource.
                          provider: Microsoft AgFoodPlatform
                          resource: AgFoodPlatform Extensions
                        isDataAction: false
          x-ms-pageable:
            nextLinkName: nextLink
        language:
          default:
            name: List
            description: Lists the available operations of Microsoft.AgFoodPlatform resource provider.
            paging:
              nextLinkName: nextLink
          go:
            name: List
            description: |-
              Lists the available operations of Microsoft.AgFoodPlatform resource provider.
              If the operation fails it returns the *ErrorResponse error type.
            azureARM: true
            clientName: OperationsClient
            openApiType: arm
            optionalParamGroup: &ref_320
              schema:
                type: object
                language:
                  default: &ref_294
                    name: OperationsListOptions
                    description: OperationsListOptions contains the optional parameters for the Operations.List method.
                  go: *ref_294
                protocol: {}
              originalParameter: []
              required: false
              serializedName: OperationsListOptions
              language:
                default: &ref_295
                  name: OperationsListOptions
                  description: OperationsListOptions contains the optional parameters for the Operations.List method.
                go: *ref_295
              protocol: {}
            pageableType: &ref_309
              name: OperationsListPager
              op: *ref_296
            paging:
              nextLinkName: NextLink
            protocolNaming:
              errorMethod: listHandleError
              internalMethod: listOperation
              requestMethod: listCreateRequest
              responseMethod: listHandleResponse
            responseEnv: &ref_335
              type: object
              properties:
                - schema:
                    type: object
                    language:
                      default: &ref_297
                        name: http.Response
                        description: raw HTTP response
                      go: *ref_297
                    protocol: {}
                  serializedName: RawResponse
                  language:
                    default: &ref_298
                      name: RawResponse
                      description: RawResponse contains the underlying HTTP response.
                    go: *ref_298
                  protocol: {}
                - &ref_303
                  schema:
                    type: object
                    properties:
                      - &ref_301
                        schema: *ref_293
                        serializedName: OperationListResult
                        language:
                          default: &ref_299
                            name: OperationListResult
                            description: A list of REST API operations supported by an Azure Resource Provider. It contains an URL link to get the next set of results.
                            byValue: true
                            embeddedType: true
                          go: *ref_299
                        protocol: {}
                    language:
                      default: &ref_300
                        name: OperationsListResult
                        description: OperationsListResult contains the result from method Operations.List.
                        marshallingFormat: json
                        responseType: true
                      go: *ref_300
                    protocol: {}
                  serializedName: OperationsListResult
                  language:
                    default: &ref_302
                      name: OperationsListResult
                      description: Contains the result of the operation.
                      byValue: true
                      embeddedType: true
                      resultField: *ref_301
                    go: *ref_302
                  protocol: {}
              language:
                default: &ref_304
                  name: OperationsListResponse
                  description: OperationsListResponse contains the response from method Operations.List.
                  responseType: true
                  resultEnv: *ref_303
                go: *ref_304
              protocol: {}
        protocol: {}
    language:
      default:
        name: Operations
        description: ''
      go:
        name: Operations
        description: ''
        clientCtorName: NewOperationsClient
        clientName: OperationsClient
    protocol: {}
security:
  authenticationRequired: true
  schemes:
    - type: AADToken
      scopes:
        - 'https://management.azure.com/.default'
language:
  default:
    name: AzureAgFoodPlatformRPService
    description: ''
  go:
    name: AzureAgFoodPlatformRPService
    description: ''
    azureARM: true
    exportClients: false
    hasTimeRFC3339: true
    headAsBoolean: true
    openApiType: arm
    packageName: armagfood
    pageableTypes:
      - *ref_305
      - *ref_306
      - *ref_307
      - *ref_308
      - *ref_309
    parameterGroups:
      - *ref_310
      - *ref_311
      - *ref_312
      - *ref_313
      - *ref_152
      - *ref_174
      - *ref_314
      - *ref_315
      - *ref_316
      - *ref_317
      - *ref_318
      - *ref_252
      - *ref_267
      - *ref_319
      - *ref_320
    responseEnvelopes:
      - *ref_321
      - *ref_322
      - *ref_323
      - *ref_324
      - *ref_325
      - *ref_326
      - *ref_327
      - *ref_328
      - *ref_329
      - *ref_330
      - *ref_331
      - *ref_332
      - *ref_333
      - *ref_334
      - *ref_335
protocol:
  http: {}

`,
            '',
        );
    }

    public static loadCodeModel(fileName: string): CodeModel {
        return deserialize(fs.readFileSync(path.join(__dirname, `codeModel/${fileName}`), 'utf8'), '');
    }
}
