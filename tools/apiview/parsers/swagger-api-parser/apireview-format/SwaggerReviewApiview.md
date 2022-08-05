###  `/deviceUpdate/{instanceId}/management/deviceClasses/{deviceClassId}`

PATCH


Description: Update device class details.
OperationId: DeviceManagement_UpdateDeviceClass
x-ms-long-running-operation: true
  
### Consumes: application/merge-patch+json

#### Path parameters:

| Name | Type/Format | Required | Description |
| ---- | ----------- | -------- | ----------- |
| `instanceId` | `string` | True | Account instance identifier. |
| `deviceClassId` | `string` | True | Device class identifier. |

#### Query parameters:
| Name | Type/Format | Required | Description |
| ---- | ----------- | -------- | ----------- |
| `api-version` | `string` | True | Version of the API to be used with the client request. |

#### Headers:

#### Body: _PatchBody_

| Model | Field | Type/Format | Required | Description |
| ----- | ----- | ----------- | -------- | ----------- |
| `PatchBody` | | | |Device Class JSON Merge Patch request body |
| | `friendlyName` | `string` | True | The device class friendly name. |

&nbsp;

### Produces: `application/json`

#### 200: _DeviceClass_
| Model | Field | Type/Format | Required | Description |
| ----- | ----- | ----------- | -------- | ----------- |
| `DeviceClass` | | | | Device class metadata. |
| | `deviceClassId` | `string` |True | The device class identifier. |
| | `friendlyName` | `string` | | The device class friendly name. This can be updated by callers after the device class has been automatically created. |
| | `deviceClassProperties` | _DeviceClassProperties_ |True | The device class properties that are used to calculate the device class Id |
| | `bestCompatibleUpdate` | _UpdateInfo_ || Update that is best compatible with this device class. |
| `DeviceClassProperties` | | || The device class properties that are used to calculate the device class Id |
| | `contractModel` | _ContractModel_ | | The Device Update agent contract model. |
| | `deviceClassId` | `string` |True | The device class identifier. |
| `ContractModel` | | || The Device Update agent contract model. |
| | `id` | `string` | True | The Device Update agent contract model Id of the device class. This is also used to calculate the device class Id. |
| | `name` | `string` |True | The Device Update agent contract model name of the device class. Intended to be a more readable form of the contract model Id. |
| `UpdateInfo` | | | | Update information. |
| | `updateId` | _UpdateId_ | True | Update identifier. |
| | `description` | `string` | | Update description. |
| | `friendlyName` | `string` | | Friendly update name. |
| `UpdateId` | | | | Update identifier. |
| | `provider` | `string` | True | Update provider. |
| | `name` | `string` | True | Update name. |
| | `version` | `string` | True | Update version. |

#### default: _ErrorResponse_
| Model | Field | Type/Format | Required | Description |
| ----- | ----- | ----------- | -------- | ----------- |
| `ErrorResponse` | | | | |
| | `error` | _Error_ | True | The error details. |
| `Error` | | | | |
| | `code` | `string` | True | Server defined error code. |
| | `message` | `string` | True | A human-readable representation of the error. |
| | `target` | `string` | | The target of the error. |
| | `details` | _Error[]_ | | An array of errors that led to the reported error. |
| | `innererror` | _InnerError_ | | An object containing more specific information than the current object about the error. |
| | `occurredDateTime` | `string`/`date-time` | | Date and time in UTC when the error occurred. |
| `InnerError` | | | | An object containing more specific information than the current object about the error. |
| | `code` | `string` | True | A more specific error code than what was provided by the containing error. |
| | `message` | `string` | | A human-readable representation of the error. |
| | `errorDetail` | `string` | | The internal error or exception message. |
| | `innererror` | _InnerError_ | | An object containing more specific information than the current object about the error. |
