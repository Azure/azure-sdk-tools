/* eslint-disable @typescript-eslint/consistent-type-assertions */
/* eslint-disable no-useless-escape */
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
            `info:
  description: APIs documentation for Azure AgriFood Resource Provider Service.
  title: Azure AgriFood RP Service
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
    - &ref_136
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
    - &ref_8
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
    - &ref_11
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
    - &ref_37
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: ^[a-zA-Z]{3,50}[.][a-zA-Z]{3,100}$
      language:
        default:
          name: ExtensionPropertiesExtensionId
          description: Extension Id.
        go:
          name: string
          description: Extension Id.
      protocol: {}
    - &ref_38
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
    - &ref_39
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: ^([1-9]|10).\d$
      language:
        default:
          name: ExtensionPropertiesInstalledExtensionVersion
          description: Installed extension version.
        go:
          name: string
          description: Installed extension version.
      protocol: {}
    - &ref_40
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
    - &ref_41
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
    - &ref_42
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
    - &ref_18
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: ResourceId
          description: Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
        go:
          name: string
          description: Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
      protocol: {}
    - &ref_19
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
    - &ref_20
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
    - &ref_22
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
    - &ref_23
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      pattern: ^[a-zA-Z]{3,50}[.][a-zA-Z]{3,100}$
      language:
        default:
          name: FarmBeatsExtensionPropertiesFarmBeatsExtensionId
          description: FarmBeatsExtension ID.
        go:
          name: string
          description: FarmBeatsExtension ID.
      protocol: {}
    - &ref_24
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
    - &ref_25
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      maxLength: 100
      minLength: 2
      pattern: ^([1-9]|10).\d$
      language:
        default:
          name: FarmBeatsExtensionPropertiesFarmBeatsExtensionVersion
          description: FarmBeatsExtension version.
        go:
          name: string
          description: FarmBeatsExtension version.
      protocol: {}
    - &ref_26
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
    - &ref_27
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
    - &ref_28
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
    - &ref_29
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
    - &ref_30
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
    - &ref_31
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
    - &ref_32
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
    - &ref_33
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
    - &ref_34
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
    - &ref_35
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
    - &ref_36
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
    - &ref_163
      type: string
      apiVersions:
        - version: 2020-05-12-preview
      pattern: ^[a-zA-Z]{3,50}[.][a-zA-Z]{3,100}$
      language:
        default:
          name: String
          description: ''
        go:
          name: string
          description: ''
      protocol: {}
    - &ref_13
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
    - &ref_17
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
          description: The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".
        go:
          name: string
          description: The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".
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
          description: The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".
        go:
          name: string
          description: The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".
      protocol: {}
    - &ref_70
      type: string
      apiVersions:
        - version: '2.0'
      language:
        default:
          name: OperationDisplayDescription
          description: The short, localized friendly description of the operation; suitable for tool tips and detailed views.
        go:
          name: string
          description: The short, localized friendly description of the operation; suitable for tool tips and detailed views.
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
    - &ref_9
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
    - &ref_14
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
        - value: user,system
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
          description: The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"
        go:
          name: Origin
          description: The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"
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
    - &ref_16
      type: dictionary
      elementType: *ref_1
      language:
        default:
          name: TrackedResourceTags
          description: Resource tags.
        go:
          name: map[string]*string
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
          name: map[string]*string
          description: Resource tags.
          elementIsPtr: true
          marshallingFormat: json
      protocol: {}
  any:
    - &ref_49
      type: any
      language:
        default:
          name: any
          description: Anything
        go:
          name: interface{}
          description: Anything
      protocol: {}
  dateTimes:
    - &ref_10
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
    - &ref_12
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
    - &ref_3
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      parents:
        all:
          - &ref_4
            type: object
            apiVersions:
              - version: '2.0'
            children:
              all:
                - *ref_3
                - &ref_5
                  type: object
                  apiVersions:
                    - version: 2020-05-12-preview
                  parents:
                    all:
                      - *ref_4
                      - &ref_7
                        type: object
                        apiVersions:
                          - version: '2.0'
                        children:
                          all:
                            - *ref_4
                            - *ref_3
                            - *ref_5
                            - &ref_6
                              type: object
                              apiVersions:
                                - version: '2.0'
                              children:
                                all:
                                  - &ref_15
                                    type: object
                                    apiVersions:
                                      - version: 2020-05-12-preview
                                    parents:
                                      all:
                                        - *ref_6
                                        - *ref_7
                                      immediate:
                                        - *ref_6
                                    properties:
                                      - schema: &ref_21
                                          type: object
                                          apiVersions:
                                            - version: '2.0'
                                          properties:
                                            - schema: *ref_8
                                              serializedName: createdBy
                                              language:
                                                default:
                                                  name: createdBy
                                                  description: The identity that created the resource.
                                                go:
                                                  name: CreatedBy
                                                  description: The identity that created the resource.
                                              protocol: {}
                                            - schema: *ref_9
                                              serializedName: createdByType
                                              language:
                                                default:
                                                  name: createdByType
                                                  description: The type of identity that created the resource.
                                                go:
                                                  name: CreatedByType
                                                  description: The type of identity that created the resource.
                                              protocol: {}
                                            - schema: *ref_10
                                              serializedName: createdAt
                                              language:
                                                default:
                                                  name: createdAt
                                                  description: The timestamp of resource creation (UTC).
                                                go:
                                                  name: CreatedAt
                                                  description: The timestamp of resource creation (UTC).
                                              protocol: {}
                                            - schema: *ref_11
                                              serializedName: lastModifiedBy
                                              language:
                                                default:
                                                  name: lastModifiedBy
                                                  description: The identity that last modified the resource.
                                                go:
                                                  name: LastModifiedBy
                                                  description: The identity that last modified the resource.
                                              protocol: {}
                                            - schema: *ref_9
                                              serializedName: lastModifiedByType
                                              language:
                                                default:
                                                  name: lastModifiedByType
                                                  description: The type of identity that last modified the resource.
                                                go:
                                                  name: LastModifiedByType
                                                  description: The type of identity that last modified the resource.
                                              protocol: {}
                                            - schema: *ref_12
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
                                      - schema: &ref_56
                                          type: object
                                          apiVersions:
                                            - version: 2020-05-12-preview
                                          properties:
                                            - schema: *ref_13
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
                                            - schema: *ref_14
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
                                  - *ref_15
                              parents:
                                all:
                                  - *ref_7
                                immediate:
                                  - *ref_7
                              properties:
                                - schema: *ref_16
                                  required: false
                                  serializedName: tags
                                  extensions:
                                    x-ms-mutability:
                                      - read
                                      - create
                                      - update
                                  language:
                                    default:
                                      name: tags
                                      description: Resource tags.
                                    go:
                                      name: Tags
                                      description: Resource tags.
                                      byValue: true
                                  protocol: {}
                                - schema: *ref_17
                                  required: true
                                  serializedName: location
                                  extensions:
                                    x-ms-mutability:
                                      - read
                                      - create
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
                            - *ref_15
                          immediate:
                            - *ref_4
                            - *ref_6
                        properties:
                          - schema: *ref_18
                            readOnly: true
                            serializedName: id
                            language:
                              default:
                                name: id
                                description: Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
                              go:
                                name: ID
                                description: READ-ONLY; Fully qualified resource ID for the resource. Ex - /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
                            protocol: {}
                          - schema: *ref_19
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
                          - schema: *ref_20
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
                    immediate:
                      - *ref_4
                  properties:
                    - schema: *ref_21
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
                          - schema: *ref_22
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
                          - schema: *ref_23
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
                          - schema: *ref_24
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
                          - schema: *ref_25
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
                          - schema: *ref_26
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
                          - schema: *ref_27
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
                          - schema: *ref_28
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
                          - schema: *ref_29
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
                          - schema: *ref_30
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
                                  - schema: *ref_31
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
                                      elementType: *ref_32
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
                                      elementType: *ref_33
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
                                        - schema: *ref_34
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
                                            elementType: *ref_35
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
                                      elementType: *ref_36
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
                              extensions:
                                x-ms-identifiers:
                                  - apiName
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
                            extensions:
                              x-ms-identifiers:
                                - apiName
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
                - *ref_3
                - *ref_5
            parents:
              all:
                - *ref_7
              immediate:
                - *ref_7
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
          - *ref_7
        immediate:
          - *ref_4
      properties:
        - schema: *ref_21
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
              - schema: *ref_37
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
              - schema: *ref_38
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
              - schema: *ref_39
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
              - schema: *ref_40
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
              - schema: *ref_41
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
        - schema: *ref_42
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
    - *ref_21
    - *ref_43
    - *ref_4
    - *ref_7
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
                  extensions:
                    x-ms-identifiers:
                      - message
                      - target
                  language:
                    default:
                      name: ErrorDetailDetails
                      description: The error details.
                    go:
                      name: '[]*ErrorDetail'
                      description: The error details.
                      elementIsPtr: true
                  protocol: {}
                readOnly: true
                serializedName: details
                extensions:
                  x-ms-identifiers:
                    - message
                    - target
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
                  extensions:
                    x-ms-identifiers: []
                  language:
                    default:
                      name: ErrorDetailAdditionalInfo
                      description: The error additional info.
                    go:
                      name: '[]*ErrorAdditionalInfo'
                      description: The error additional info.
                      elementIsPtr: true
                  protocol: {}
                readOnly: true
                serializedName: additionalInfo
                extensions:
                  x-ms-identifiers: []
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
              name: Error
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
          marshallingFormat: json
          namespace: ''
          summary: Error response
      protocol: {}
    - *ref_47
    - *ref_50
    - &ref_143
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_80
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_3
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
    - &ref_159
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_90
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_5
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
    - *ref_5
    - *ref_53
    - *ref_54
    - *ref_55
    - *ref_15
    - *ref_56
    - *ref_6
    - &ref_185
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
    - &ref_204
      type: object
      apiVersions:
        - version: 2020-05-12-preview
      properties:
        - schema: &ref_91
            type: array
            apiVersions:
              - version: 2020-05-12-preview
            elementType: *ref_15
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
    - &ref_217
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
    - &ref_219
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
    - &ref_225
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
                            description: The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".
                          go:
                            name: Provider
                            description: READ-ONLY; The localized friendly form of the resource provider name, e.g. "Microsoft Monitoring Insights" or "Microsoft Compute".
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
                            description: The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".
                          go:
                            name: Operation
                            description: READ-ONLY; The concise, localized friendly name for the operation; suitable for dropdowns. E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".
                        protocol: {}
                      - schema: *ref_70
                        readOnly: true
                        serializedName: description
                        language:
                          default:
                            name: description
                            description: The short, localized friendly description of the operation; suitable for tool tips and detailed views.
                          go:
                            name: Description
                            description: READ-ONLY; The short, localized friendly description of the operation; suitable for tool tips and detailed views.
                        protocol: {}
                    serializationFormats:
                      - json
                    usage:
                      - output
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
                      description: The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"
                    go:
                      name: Origin
                      description: READ-ONLY; The intended executor of the operation; as in Resource Based Access Control (RBAC) and audit logs UX. Default value is "user,system"
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
                  description: Details of a REST API operation, returned from the Resource Provider Operations API
                  namespace: ''
                  summary: REST API Operation
                go:
                  name: Operation
                  description: Operation - Details of a REST API operation, returned from the Resource Provider Operations API
                  marshallingFormat: json
                  namespace: ''
                  summary: REST API Operation
              protocol: {}
            extensions:
              x-ms-identifiers:
                - name
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
          extensions:
            x-ms-identifiers:
              - name
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
    - &ref_131
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
    - &ref_134
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
    - &ref_147
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
    - &ref_150
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
    - &ref_152
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
    - &ref_153
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
    clientDefaultValue: https://management.azure.com
    implementation: Client
    origin: modelerfour:synthesized/host
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
    origin: modelerfour:synthesized/api-version
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}
                method: put
                uri: '{$host}'
        signatureParameters:
          - *ref_100
          - *ref_101
          - *ref_102
        responses:
          - schema: *ref_3
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Extensions_Create.json
              responses:
                '201':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: https://docs.provider.com/documentation/extension
                      extensionAuthLink: https://www.provider.com/extension/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_231
              schema:
                type: object
                language:
                  default: &ref_104
                    name: ExtensionsClientCreateOptions
                    description: ExtensionsClientCreateOptions contains the optional parameters for the ExtensionsClient.Create method.
                  go: *ref_104
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsClientCreateOptions
              language:
                default: &ref_105
                  name: options
                  description: ExtensionsClientCreateOptions contains the optional parameters for the ExtensionsClient.Create method.
                go: *ref_105
              protocol: {}
            protocolNaming:
              internalMethod: create
              requestMethod: createCreateRequest
              responseMethod: createHandleResponse
            responseEnv: &ref_242
              type: object
              properties:
                - &ref_107
                  schema: *ref_3
                  serializedName: Extension
                  language:
                    default: &ref_106
                      name: Extension
                      description: Extension resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_106
                  protocol: {}
              language:
                default: &ref_108
                  name: ExtensionsClientCreateResponse
                  description: ExtensionsClientCreateResponse contains the response from method ExtensionsClient.Create.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_107
                go: *ref_108
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_109
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
          - &ref_110
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
          - &ref_111
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_109
          - *ref_110
          - *ref_111
        responses:
          - schema: *ref_3
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Extensions_Get.json
              responses:
                '200':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: https://docs.provider.com/documentation/extension
                      extensionAuthLink: https://www.provider.com/extension/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_232
              schema:
                type: object
                language:
                  default: &ref_112
                    name: ExtensionsClientGetOptions
                    description: ExtensionsClientGetOptions contains the optional parameters for the ExtensionsClient.Get method.
                  go: *ref_112
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsClientGetOptions
              language:
                default: &ref_113
                  name: options
                  description: ExtensionsClientGetOptions contains the optional parameters for the ExtensionsClient.Get method.
                go: *ref_113
              protocol: {}
            protocolNaming:
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_243
              type: object
              properties:
                - &ref_115
                  schema: *ref_3
                  serializedName: Extension
                  language:
                    default: &ref_114
                      name: Extension
                      description: Extension resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_114
                  protocol: {}
              language:
                default: &ref_116
                  name: ExtensionsClientGetResponse
                  description: ExtensionsClientGetResponse contains the response from method ExtensionsClient.Get.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_115
                go: *ref_116
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_117
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
          - &ref_118
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
          - &ref_119
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}
                method: patch
                uri: '{$host}'
        signatureParameters:
          - *ref_117
          - *ref_118
          - *ref_119
        responses:
          - schema: *ref_3
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Extensions_Update.json
              responses:
                '200':
                  body:
                    name: provider.extension
                    type: Microsoft.AgFoodPlatform/farmBeats/extensions
                    eTag: 7200b954-0000-0700-0000-603cbbc40000
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                    properties:
                      extensionApiDocsLink: https://docs.provider.com/documentation/extension
                      extensionAuthLink: https://www.provider.com/extension/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_233
              schema:
                type: object
                language:
                  default: &ref_120
                    name: ExtensionsClientUpdateOptions
                    description: ExtensionsClientUpdateOptions contains the optional parameters for the ExtensionsClient.Update method.
                  go: *ref_120
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsClientUpdateOptions
              language:
                default: &ref_121
                  name: options
                  description: ExtensionsClientUpdateOptions contains the optional parameters for the ExtensionsClient.Update method.
                go: *ref_121
              protocol: {}
            protocolNaming:
              internalMethod: update
              requestMethod: updateCreateRequest
              responseMethod: updateHandleResponse
            responseEnv: &ref_244
              type: object
              properties:
                - &ref_123
                  schema: *ref_3
                  serializedName: Extension
                  language:
                    default: &ref_122
                      name: Extension
                      description: Extension resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_122
                  protocol: {}
              language:
                default: &ref_124
                  name: ExtensionsClientUpdateResponse
                  description: ExtensionsClientUpdateResponse contains the response from method ExtensionsClient.Update.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_123
                go: *ref_124
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_125
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
          - &ref_126
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
          - &ref_127
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions/{extensionId}
                method: delete
                uri: '{$host}'
        signatureParameters:
          - *ref_125
          - *ref_126
          - *ref_127
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Extensions_Delete.json
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_234
              schema:
                type: object
                language:
                  default: &ref_128
                    name: ExtensionsClientDeleteOptions
                    description: ExtensionsClientDeleteOptions contains the optional parameters for the ExtensionsClient.Delete method.
                  go: *ref_128
                protocol: {}
              originalParameter: []
              required: false
              serializedName: ExtensionsClientDeleteOptions
              language:
                default: &ref_129
                  name: options
                  description: ExtensionsClientDeleteOptions contains the optional parameters for the ExtensionsClient.Delete method.
                go: *ref_129
              protocol: {}
            protocolNaming:
              internalMethod: deleteOperation
              requestMethod: deleteCreateRequest
              responseMethod: deleteHandleResponse
            responseEnv: &ref_245
              type: object
              language:
                default: &ref_130
                  name: ExtensionsClientDeleteResponse
                  description: ExtensionsClientDeleteResponse contains the response from method ExtensionsClient.Delete.
                  responseType: true
                go: *ref_130
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_141
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
          - &ref_142
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
          - &ref_133
            schema: *ref_131
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
                paramGroup: &ref_135
                  schema:
                    type: object
                    language:
                      default: &ref_132
                        name: ExtensionsClientListByFarmBeatsOptions
                        description: ExtensionsClientListByFarmBeatsOptions contains the optional parameters for the ExtensionsClient.ListByFarmBeats method.
                      go: *ref_132
                    protocol: {}
                  originalParameter:
                    - *ref_133
                    - &ref_138
                      schema: *ref_134
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
                          paramGroup: *ref_135
                          serializedName: extensionCategories
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_139
                      schema: *ref_136
                      implementation: Method
                      language:
                        default:
                          name: maxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          serializedName: $maxPageSize
                        go:
                          name: MaxPageSize
                          description: Maximum number of items needed (inclusive). Minimum = 10, Maximum = 1000, Default value = 50.
                          paramGroup: *ref_135
                          serializedName: $maxPageSize
                      protocol:
                        http:
                          in: query
                    - &ref_140
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
                          paramGroup: *ref_135
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: ExtensionsClientListByFarmBeatsOptions
                  language:
                    default: &ref_137
                      name: options
                      description: ExtensionsClientListByFarmBeatsOptions contains the optional parameters for the ExtensionsClient.ListByFarmBeats method.
                    go: *ref_137
                  protocol: {}
                serializedName: extensionIds
            protocol:
              http:
                explode: true
                in: query
                style: form
          - *ref_138
          - *ref_139
          - *ref_140
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}/extensions
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_141
          - *ref_142
          - *ref_133
          - *ref_138
          - *ref_139
          - *ref_140
        responses:
          - schema: *ref_143
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Extensions_ListByFarmBeats.json
              responses:
                '200':
                  body:
                    value:
                      - name: provider.extension
                        type: Microsoft.AgFoodPlatform/farmBeats/extensions
                        eTag: 7200b954-0000-0700-0000-603cbbc40000
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
                        properties:
                          extensionApiDocsLink: https://docs.provider.com/documentation/extension
                          extensionAuthLink: https://www.provider.com/extension/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: ExtensionsClient
            openApiType: arm
            optionalParamGroup: *ref_135
            paging:
              nextLinkName: NextLink
            protocolNaming:
              internalMethod: listByFarmBeats
              requestMethod: listByFarmBeatsCreateRequest
              responseMethod: listByFarmBeatsHandleResponse
            responseEnv: &ref_246
              type: object
              properties:
                - &ref_145
                  schema: *ref_143
                  serializedName: ExtensionListResponse
                  language:
                    default: &ref_144
                      name: ExtensionListResponse
                      description: Paged response contains list of requested objects and a URL link to get the next set of results.
                      byValue: true
                      embeddedType: true
                    go: *ref_144
                  protocol: {}
              language:
                default: &ref_146
                  name: ExtensionsClientListByFarmBeatsResponse
                  description: ExtensionsClientListByFarmBeatsResponse contains the response from method ExtensionsClient.ListByFarmBeats.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_145
                go: *ref_146
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
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_149
            schema: *ref_147
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
                paramGroup: &ref_151
                  schema:
                    type: object
                    language:
                      default: &ref_148
                        name: FarmBeatsExtensionsClientListOptions
                        description: FarmBeatsExtensionsClientListOptions contains the optional parameters for the FarmBeatsExtensionsClient.List method.
                      go: *ref_148
                    protocol: {}
                  originalParameter:
                    - *ref_149
                    - &ref_155
                      schema: *ref_150
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
                          paramGroup: *ref_151
                          serializedName: farmBeatsExtensionNames
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_156
                      schema: *ref_152
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
                          paramGroup: *ref_151
                          serializedName: extensionCategories
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_157
                      schema: *ref_153
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
                          paramGroup: *ref_151
                          serializedName: publisherIds
                      protocol:
                        http:
                          explode: true
                          in: query
                          style: form
                    - &ref_158
                      schema: *ref_136
                      implementation: Method
                      language:
                        default:
                          name: maxPageSize
                          description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                          serializedName: $maxPageSize
                        go:
                          name: MaxPageSize
                          description: Maximum number of items needed (inclusive). Minimum = 10, Maximum = 1000, Default value = 50.
                          paramGroup: *ref_151
                          serializedName: $maxPageSize
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsExtensionsClientListOptions
                  language:
                    default: &ref_154
                      name: options
                      description: FarmBeatsExtensionsClientListOptions contains the optional parameters for the FarmBeatsExtensionsClient.List method.
                    go: *ref_154
                  protocol: {}
                serializedName: farmBeatsExtensionIds
            protocol:
              http:
                explode: true
                in: query
                style: form
          - *ref_155
          - *ref_156
          - *ref_157
          - *ref_158
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: modelerfour:synthesized/accept
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
          - *ref_149
          - *ref_155
          - *ref_156
          - *ref_157
          - *ref_158
        responses:
          - schema: *ref_159
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_List.json
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
                          extensionApiDocsLink: https://cs-docs.dtn.com/api/weather-observations-and-forecasts-rest-api/
                          extensionAuthLink: https://www.dtn.com/dtn-content-integration/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsExtensionsClient
            openApiType: arm
            optionalParamGroup: *ref_151
            paging:
              nextLinkName: NextLink
            protocolNaming:
              internalMethod: listOperation
              requestMethod: listCreateRequest
              responseMethod: listHandleResponse
            responseEnv: &ref_247
              type: object
              properties:
                - &ref_161
                  schema: *ref_159
                  serializedName: FarmBeatsExtensionListResponse
                  language:
                    default: &ref_160
                      name: FarmBeatsExtensionListResponse
                      description: Paged response contains list of requested objects and a URL link to get the next set of results.
                      byValue: true
                      embeddedType: true
                    go: *ref_160
                  protocol: {}
              language:
                default: &ref_162
                  name: FarmBeatsExtensionsClientListResponse
                  description: FarmBeatsExtensionsClientListResponse contains the response from method FarmBeatsExtensionsClient.List.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_161
                go: *ref_162
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_164
            schema: *ref_163
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
                origin: modelerfour:synthesized/accept
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
                path: /providers/Microsoft.AgFoodPlatform/farmBeatsExtensionDefinitions/{farmBeatsExtensionId}
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_164
        responses:
          - schema: *ref_5
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsExtensions_Get.json
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
                      extensionApiDocsLink: https://cs-docs.dtn.com/api/weather-observations-and-forecasts-rest-api/
                      extensionAuthLink: https://www.dtn.com/dtn-content-integration/
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsExtensionsClient
            openApiType: arm
            optionalParamGroup: &ref_235
              schema:
                type: object
                language:
                  default: &ref_165
                    name: FarmBeatsExtensionsClientGetOptions
                    description: FarmBeatsExtensionsClientGetOptions contains the optional parameters for the FarmBeatsExtensionsClient.Get method.
                  go: *ref_165
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsExtensionsClientGetOptions
              language:
                default: &ref_166
                  name: options
                  description: FarmBeatsExtensionsClientGetOptions contains the optional parameters for the FarmBeatsExtensionsClient.Get method.
                go: *ref_166
              protocol: {}
            protocolNaming:
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_248
              type: object
              properties:
                - &ref_168
                  schema: *ref_5
                  serializedName: FarmBeatsExtension
                  language:
                    default: &ref_167
                      name: FarmBeatsExtension
                      description: FarmBeats extension resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_167
                  protocol: {}
              language:
                default: &ref_169
                  name: FarmBeatsExtensionsClientGetResponse
                  description: FarmBeatsExtensionsClientGetResponse contains the response from method FarmBeatsExtensionsClient.Get.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_168
                go: *ref_169
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
          - &ref_170
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
          - &ref_171
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_170
          - *ref_171
        responses:
          - schema: *ref_15
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_Get.json
              responses:
                '200':
                  body:
                    name: examples-farmBeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_236
              schema:
                type: object
                language:
                  default: &ref_172
                    name: FarmBeatsModelsClientGetOptions
                    description: FarmBeatsModelsClientGetOptions contains the optional parameters for the FarmBeatsModelsClient.Get method.
                  go: *ref_172
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsClientGetOptions
              language:
                default: &ref_173
                  name: options
                  description: FarmBeatsModelsClientGetOptions contains the optional parameters for the FarmBeatsModelsClient.Get method.
                go: *ref_173
              protocol: {}
            protocolNaming:
              internalMethod: get
              requestMethod: getCreateRequest
              responseMethod: getHandleResponse
            responseEnv: &ref_249
              type: object
              properties:
                - &ref_175
                  schema: *ref_15
                  serializedName: FarmBeats
                  language:
                    default: &ref_174
                      name: FarmBeats
                      description: FarmBeats ARM Resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_174
                  protocol: {}
              language:
                default: &ref_176
                  name: FarmBeatsModelsClientGetResponse
                  description: FarmBeatsModelsClientGetResponse contains the response from method FarmBeatsModelsClient.Get.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_175
                go: *ref_176
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_178
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
          - &ref_179
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
              - &ref_177
                schema: *ref_15
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
                origin: modelerfour:synthesized/accept
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
              - *ref_177
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}
                method: put
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters:
          - *ref_178
          - *ref_179
        responses:
          - schema: *ref_15
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
          - schema: *ref_15
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
              x-ms-original-file: >-
                https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_CreateOrUpdate.json
              responses:
                '200':
                  body:
                    name: examples-farmbeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
                      instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_237
              schema:
                type: object
                language:
                  default: &ref_180
                    name: FarmBeatsModelsClientCreateOrUpdateOptions
                    description: FarmBeatsModelsClientCreateOrUpdateOptions contains the optional parameters for the FarmBeatsModelsClient.CreateOrUpdate method.
                  go: *ref_180
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsClientCreateOrUpdateOptions
              language:
                default: &ref_181
                  name: options
                  description: FarmBeatsModelsClientCreateOrUpdateOptions contains the optional parameters for the FarmBeatsModelsClient.CreateOrUpdate method.
                go: *ref_181
              protocol: {}
            protocolNaming:
              internalMethod: createOrUpdate
              requestMethod: createOrUpdateCreateRequest
              responseMethod: createOrUpdateHandleResponse
            responseEnv: &ref_250
              type: object
              properties:
                - &ref_183
                  schema: *ref_15
                  serializedName: FarmBeats
                  language:
                    default: &ref_182
                      name: FarmBeats
                      description: FarmBeats ARM Resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_182
                  protocol: {}
              language:
                default: &ref_184
                  name: FarmBeatsModelsClientCreateOrUpdateResponse
                  description: FarmBeatsModelsClientCreateOrUpdateResponse contains the response from method FarmBeatsModelsClient.CreateOrUpdate.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_183
                go: *ref_184
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_187
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
          - &ref_188
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
              - &ref_186
                schema: *ref_185
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
                origin: modelerfour:synthesized/accept
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
              - *ref_186
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}
                method: patch
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters:
          - *ref_187
          - *ref_188
        responses:
          - schema: *ref_15
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_Update.json
              responses:
                '200':
                  body:
                    name: examples-farmBeatsResourceName
                    type: Microsoft.AgFoodPlatform/farmBeats
                    id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                    location: eastus2
                    properties:
                      instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_238
              schema:
                type: object
                language:
                  default: &ref_189
                    name: FarmBeatsModelsClientUpdateOptions
                    description: FarmBeatsModelsClientUpdateOptions contains the optional parameters for the FarmBeatsModelsClient.Update method.
                  go: *ref_189
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsClientUpdateOptions
              language:
                default: &ref_190
                  name: options
                  description: FarmBeatsModelsClientUpdateOptions contains the optional parameters for the FarmBeatsModelsClient.Update method.
                go: *ref_190
              protocol: {}
            protocolNaming:
              internalMethod: update
              requestMethod: updateCreateRequest
              responseMethod: updateHandleResponse
            responseEnv: &ref_251
              type: object
              properties:
                - &ref_192
                  schema: *ref_15
                  serializedName: FarmBeats
                  language:
                    default: &ref_191
                      name: FarmBeats
                      description: FarmBeats ARM Resource.
                      byValue: true
                      embeddedType: true
                    go: *ref_191
                  protocol: {}
              language:
                default: &ref_193
                  name: FarmBeatsModelsClientUpdateResponse
                  description: FarmBeatsModelsClientUpdateResponse contains the response from method FarmBeatsModelsClient.Update.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_192
                go: *ref_193
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_194
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
          - &ref_195
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats/{farmBeatsResourceName}
                method: delete
                uri: '{$host}'
        signatureParameters:
          - *ref_194
          - *ref_195
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_Delete.json
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: &ref_239
              schema:
                type: object
                language:
                  default: &ref_196
                    name: FarmBeatsModelsClientDeleteOptions
                    description: FarmBeatsModelsClientDeleteOptions contains the optional parameters for the FarmBeatsModelsClient.Delete method.
                  go: *ref_196
                protocol: {}
              originalParameter: []
              required: false
              serializedName: FarmBeatsModelsClientDeleteOptions
              language:
                default: &ref_197
                  name: options
                  description: FarmBeatsModelsClientDeleteOptions contains the optional parameters for the FarmBeatsModelsClient.Delete method.
                go: *ref_197
              protocol: {}
            protocolNaming:
              internalMethod: deleteOperation
              requestMethod: deleteCreateRequest
              responseMethod: deleteHandleResponse
            responseEnv: &ref_252
              type: object
              language:
                default: &ref_198
                  name: FarmBeatsModelsClientDeleteResponse
                  description: FarmBeatsModelsClientDeleteResponse contains the response from method FarmBeatsModelsClient.Delete.
                  responseType: true
                go: *ref_198
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_200
            schema: *ref_136
            implementation: Method
            language:
              default:
                name: maxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                serializedName: $maxPageSize
              go:
                name: MaxPageSize
                description: Maximum number of items needed (inclusive). Minimum = 10, Maximum = 1000, Default value = 50.
                paramGroup: &ref_201
                  schema:
                    type: object
                    language:
                      default: &ref_199
                        name: FarmBeatsModelsClientListBySubscriptionOptions
                        description: FarmBeatsModelsClientListBySubscriptionOptions contains the optional parameters for the FarmBeatsModelsClient.ListBySubscription method.
                      go: *ref_199
                    protocol: {}
                  originalParameter:
                    - *ref_200
                    - &ref_203
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
                          paramGroup: *ref_201
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsModelsClientListBySubscriptionOptions
                  language:
                    default: &ref_202
                      name: options
                      description: FarmBeatsModelsClientListBySubscriptionOptions contains the optional parameters for the FarmBeatsModelsClient.ListBySubscription method.
                    go: *ref_202
                  protocol: {}
                serializedName: $maxPageSize
            protocol:
              http:
                in: query
          - *ref_203
          - *ref_97
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/providers/Microsoft.AgFoodPlatform/farmBeats
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_200
          - *ref_203
        responses:
          - schema: *ref_204
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
              x-ms-original-file: >-
                https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_ListBySubscription.json
              responses:
                '200':
                  body:
                    value:
                      - name: examples-farmBeatsResourceName
                        type: Microsoft.AgFoodPlatform/farmBeats
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                        location: eastus2
                        properties:
                          instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: *ref_201
            paging:
              nextLinkName: NextLink
            protocolNaming:
              internalMethod: listBySubscription
              requestMethod: listBySubscriptionCreateRequest
              responseMethod: listBySubscriptionHandleResponse
            responseEnv: &ref_253
              type: object
              properties:
                - &ref_206
                  schema: *ref_204
                  serializedName: FarmBeatsListResponse
                  language:
                    default: &ref_205
                      name: FarmBeatsListResponse
                      description: Paged response contains list of requested objects and a URL link to get the next set of results.
                      byValue: true
                      embeddedType: true
                    go: *ref_205
                  protocol: {}
              language:
                default: &ref_207
                  name: FarmBeatsModelsClientListBySubscriptionResponse
                  description: FarmBeatsModelsClientListBySubscriptionResponse contains the response from method FarmBeatsModelsClient.ListBySubscription.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_206
                go: *ref_207
              protocol: {}
        protocol: {}
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - &ref_209
            schema: *ref_136
            implementation: Method
            language:
              default:
                name: maxPageSize
                description: "Maximum number of items needed (inclusive).\r\nMinimum = 10, Maximum = 1000, Default value = 50."
                serializedName: $maxPageSize
              go:
                name: MaxPageSize
                description: Maximum number of items needed (inclusive). Minimum = 10, Maximum = 1000, Default value = 50.
                paramGroup: &ref_210
                  schema:
                    type: object
                    language:
                      default: &ref_208
                        name: FarmBeatsModelsClientListByResourceGroupOptions
                        description: FarmBeatsModelsClientListByResourceGroupOptions contains the optional parameters for the FarmBeatsModelsClient.ListByResourceGroup method.
                      go: *ref_208
                    protocol: {}
                  originalParameter:
                    - *ref_209
                    - &ref_212
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
                          paramGroup: *ref_210
                          serializedName: $skipToken
                      protocol:
                        http:
                          in: query
                  required: false
                  serializedName: FarmBeatsModelsClientListByResourceGroupOptions
                  language:
                    default: &ref_211
                      name: options
                      description: FarmBeatsModelsClientListByResourceGroupOptions contains the optional parameters for the FarmBeatsModelsClient.ListByResourceGroup method.
                    go: *ref_211
                  protocol: {}
                serializedName: $maxPageSize
            protocol:
              http:
                in: query
          - *ref_212
          - &ref_213
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
                origin: modelerfour:synthesized/accept
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
                path: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AgFoodPlatform/farmBeats
                method: get
                uri: '{$host}'
        signatureParameters:
          - *ref_209
          - *ref_212
          - *ref_213
        responses:
          - schema: *ref_204
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
              x-ms-original-file: >-
                https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/FarmBeatsModels_ListByResourceGroup.json
              responses:
                '200':
                  body:
                    value:
                      - name: examples-farmBeatsResourceName
                        type: Microsoft.AgFoodPlatform/farmBeats
                        id: /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName
                        location: eastus2
                        properties:
                          instanceUri: https://examples-farmbeatsResourceName.eastus2.farmbeats.azure.net
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: FarmBeatsModelsClient
            openApiType: arm
            optionalParamGroup: *ref_210
            paging:
              nextLinkName: NextLink
            protocolNaming:
              internalMethod: listByResourceGroup
              requestMethod: listByResourceGroupCreateRequest
              responseMethod: listByResourceGroupHandleResponse
            responseEnv: &ref_254
              type: object
              properties:
                - &ref_215
                  schema: *ref_204
                  serializedName: FarmBeatsListResponse
                  language:
                    default: &ref_214
                      name: FarmBeatsListResponse
                      description: Paged response contains list of requested objects and a URL link to get the next set of results.
                      byValue: true
                      embeddedType: true
                    go: *ref_214
                  protocol: {}
              language:
                default: &ref_216
                  name: FarmBeatsModelsClientListByResourceGroupResponse
                  description: FarmBeatsModelsClientListByResourceGroupResponse contains the response from method FarmBeatsModelsClient.ListByResourceGroup.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_215
                go: *ref_216
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
              - &ref_218
                schema: *ref_217
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
                origin: modelerfour:synthesized/accept
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
              - *ref_218
            language:
              default:
                name: ''
                description: ''
              go:
                name: ''
                description: ''
            protocol:
              http:
                path: /subscriptions/{subscriptionId}/providers/Microsoft.AgFoodPlatform/checkNameAvailability
                method: post
                knownMediaType: json
                mediaTypes:
                  - application/json
                uri: '{$host}'
        signatureParameters: []
        responses:
          - schema: *ref_219
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
              x-ms-original-file: >-
                https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Locations_CheckNameAvailability_AlreadyExists.json
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
              x-ms-original-file: >-
                https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Locations_CheckNameAvailability_Available.json
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: LocationsClient
            openApiType: arm
            optionalParamGroup: &ref_240
              schema:
                type: object
                language:
                  default: &ref_220
                    name: LocationsClientCheckNameAvailabilityOptions
                    description: LocationsClientCheckNameAvailabilityOptions contains the optional parameters for the LocationsClient.CheckNameAvailability method.
                  go: *ref_220
                protocol: {}
              originalParameter: []
              required: false
              serializedName: LocationsClientCheckNameAvailabilityOptions
              language:
                default: &ref_221
                  name: options
                  description: LocationsClientCheckNameAvailabilityOptions contains the optional parameters for the LocationsClient.CheckNameAvailability method.
                go: *ref_221
              protocol: {}
            protocolNaming:
              internalMethod: checkNameAvailability
              requestMethod: checkNameAvailabilityCreateRequest
              responseMethod: checkNameAvailabilityHandleResponse
            responseEnv: &ref_255
              type: object
              properties:
                - &ref_223
                  schema: *ref_219
                  serializedName: CheckNameAvailabilityResponse
                  language:
                    default: &ref_222
                      name: CheckNameAvailabilityResponse
                      description: The check availability result.
                      byValue: true
                      embeddedType: true
                    go: *ref_222
                  protocol: {}
              language:
                default: &ref_224
                  name: LocationsClientCheckNameAvailabilityResponse
                  description: LocationsClientCheckNameAvailabilityResponse contains the response from method LocationsClient.CheckNameAvailability.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_223
                go: *ref_224
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
      - apiVersions:
          - version: 2020-05-12-preview
        parameters:
          - *ref_95
          - *ref_98
        requests:
          - parameters:
              - schema: *ref_99
                implementation: Method
                origin: modelerfour:synthesized/accept
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
          - schema: *ref_225
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
              x-ms-original-file: https://github.com/Azure/azure-rest-api-specs/blob/d045209326d1de6e0d30f0341825526adfad5a55/specification/agrifood/resource-manager/Microsoft.AgFoodPlatform/preview/2020-05-12-preview/examples/Operations_List.json
              responses:
                '200':
                  body:
                    nextLink: https://management.azure.com/providers/Microsoft.AgFoodPlatform/operations?$skiptoken={token}
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
              If the operation fails it returns an *azcore.ResponseError type.
            azureARM: true
            clientName: OperationsClient
            openApiType: arm
            optionalParamGroup: &ref_241
              schema:
                type: object
                language:
                  default: &ref_226
                    name: OperationsClientListOptions
                    description: OperationsClientListOptions contains the optional parameters for the OperationsClient.List method.
                  go: *ref_226
                protocol: {}
              originalParameter: []
              required: false
              serializedName: OperationsClientListOptions
              language:
                default: &ref_227
                  name: options
                  description: OperationsClientListOptions contains the optional parameters for the OperationsClient.List method.
                go: *ref_227
              protocol: {}
            paging:
              nextLinkName: NextLink
            protocolNaming:
              internalMethod: listOperation
              requestMethod: listCreateRequest
              responseMethod: listHandleResponse
            responseEnv: &ref_256
              type: object
              properties:
                - &ref_229
                  schema: *ref_225
                  serializedName: OperationListResult
                  language:
                    default: &ref_228
                      name: OperationListResult
                      description: A list of REST API operations supported by an Azure Resource Provider. It contains an URL link to get the next set of results.
                      byValue: true
                      embeddedType: true
                    go: *ref_228
                  protocol: {}
              language:
                default: &ref_230
                  name: OperationsClientListResponse
                  description: OperationsClientListResponse contains the response from method OperationsClient.List.
                  marshallingFormat: json
                  responseType: true
                  resultProp: *ref_229
                go: *ref_230
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
        - https://management.azure.com/.default
language:
  default:
    name: AzureAgriFoodRPService
    description: ''
  go:
    name: AzureAgriFoodRPService
    description: ''
    azureARM: true
    exportClients: false
    groupParameters: true
    hasTimeRFC3339: true
    headAsBoolean: true
    openApiType: arm
    packageName: armagrifood
    parameterGroups:
      - *ref_231
      - *ref_232
      - *ref_233
      - *ref_234
      - *ref_135
      - *ref_151
      - *ref_235
      - *ref_236
      - *ref_237
      - *ref_238
      - *ref_239
      - *ref_201
      - *ref_210
      - *ref_240
      - *ref_241
    responseEnvelopes:
      - *ref_242
      - *ref_243
      - *ref_244
      - *ref_245
      - *ref_246
      - *ref_247
      - *ref_248
      - *ref_249
      - *ref_250
      - *ref_251
      - *ref_252
      - *ref_253
      - *ref_254
      - *ref_255
      - *ref_256
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
