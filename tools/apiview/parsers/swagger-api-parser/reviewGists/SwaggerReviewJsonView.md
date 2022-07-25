###  `/deviceupdate/{instanceId}/updates`

<details>
<summary>POST</summary>

``` json
{
  "tags":[
    "Update management"
  ],
  "operationId":"DeviceUpdate_ImportUpdate",
  "x-ms-long-running-operation":true,
  "x-ms-examples":{
    "DeviceUpdate_ImportUpdate":{
      "$ref":"./examples/DeviceUpdate_ImportUpdate.json"
    }
  },
  "description":"Import new update version.",
  "parameters":[
    {
      "$ref":"#/parameters/InstanceIdParameter"
    },
    {
      "$ref":"#/parameters/ImportActionParameter"
    },
    {
      "$ref":"#/parameters/ApiVersionParameter"
    },
    {
      "name":"updateToImport",
      "in":"body",
      "required":true,
      "schema":{
        "$ref":"#/definitions/ImportUpdateInput"
      },
      "description":"The update to be imported."
    }
  ],
  "responses":{
    "202":{
      "description":"Accepted update import request; background operation location to track status is specified in Operation-Location response header.",
      "schema":{
        "$ref":"#/definitions/Update"
      },
      "headers":{
        "Operation-Location":{
          "type":"string",
          "description":"Url to retrieve the import operation status."
        }
      }
    },
    "default":{
      "description":"Default response.",
      "schema":{
        "$ref":"#/definitions/ErrorResponse"
      }
    }
  }
}
```
</details>
<details>
<summary>GET</summary>

``` json
 {
  "tags":[
    "Update management"
  ],
  "description":"Get a list of all updates that have been imported to Device Update for IoT Hub.",
  "operationId":"DeviceUpdate_ListUpdates",
  "x-ms-examples":{
    "DeviceUpdate_ListUpdates":{
      "$ref":"./examples/DeviceUpdate_ListUpdates.json"
    }
  },
  "parameters":[
    {
      "$ref":"#/parameters/InstanceIdParameter"
    },
    {
      "$ref":"#/parameters/ApiVersionParameter"
    },
    {
      "name":"$search",
      "in":"query",
      "required":false,
      "type":"string",
      "description":"Request updates matching a free-text search expression."
    },
    {
      "name":"$filter",
      "in":"query",
      "required":false,
      "type":"string",
      "description":"Filter updates by its properties."
    }
  ],
  "responses":{
    "200":{
      "description":"All imported updates, or empty list if there is none.",
      "schema":{
        "$ref":"#/definitions/UpdateList"
      }
    },
    "default":{
      "description":"Default response.",
      "schema":{
        "$ref":"#/definitions/ErrorResponse"
      }
    }
  },
  "x-ms-odata":"#/definitions/UpdateFilter",
  "x-ms-pageable":{
    "nextLinkName":"nextLink"
  }
}
```

</details>

### `/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions/{version}`

<details>
<summary>GET</summary>

```json
{
  "tags":[
    "Update management"
  ],
  "description":"Get a specific update version.",
  "operationId":"DeviceUpdate_GetUpdate",
  "x-ms-examples":{
    "DeviceUpdate_GetUpdate":{
      "$ref":"./examples/DeviceUpdate_GetUpdate.json"
    }
  },
  "parameters":[
    {
      "$ref":"#/parameters/InstanceIdParameter"
    },
    {
      "$ref":"#/parameters/UpdateProviderParameter"
    },
    {
      "$ref":"#/parameters/UpdateNameParameter"
    },
    {
      "$ref":"#/parameters/UpdateVersionParameter"
    },
    {
      "$ref":"#/parameters/ApiVersionParameter"
    },
    {
      "$ref":"#/parameters/IfNoneMatchParameter"
    }
  ],
  "responses":{
    "200":{
      "description":"The requested update version.",
      "schema":{
        "$ref":"#/definitions/Update"
      }
    },
    "304":{
      "description":"Not modified."
    },
    "default":{
      "description":"Default response.",
      "schema":{
        "$ref":"#/definitions/ErrorResponse"
      }
    }
  }
}
```
</details>

<details>
<summary>DELETE</summary>

```json
{
  "tags":[
    "Update management"
  ],
  "description":"Delete a specific update version.",
  "operationId":"DeviceUpdate_DeleteUpdate",
  "x-ms-long-running-operation":true,
  "x-ms-examples":{
    "DeviceUpdate_DeleteUpdate":{
      "$ref":"./examples/DeviceUpdate_DeleteUpdate.json"
    }
  },
  "parameters":[
    {
      "$ref":"#/parameters/InstanceIdParameter"
    },
    {
      "$ref":"#/parameters/UpdateProviderParameter"
    },
    {
      "$ref":"#/parameters/UpdateNameParameter"
    },
    {
      "$ref":"#/parameters/UpdateVersionParameter"
    },
    {
      "$ref":"#/parameters/ApiVersionParameter"
    }
  ],
  "responses":{
    "202":{
      "description":"Accepted update deletion request; background operation location to track status is specified in Operation-Location response header.",
      "headers":{
        "Operation-Location":{
          "type":"string",
          "description":"Url to retrieve the operation status"
        }
      }
    },
    "default":{
      "description":"Default response.",
      "schema":{
        "$ref":"#/definitions/ErrorResponse"
      }
    }
  }
}
```
</details>


###  Definition

<details>
<summary>Error</summary>
  
  ```json
  {
      "type": "object",
      "properties": {
        "code": {
          "type": "string",
          "description": "Server defined error code."
        },
        "message": {
          "type": "string",
          "description": "A human-readable representation of the error."
        },
        "target": {
          "type": "string",
          "description": "The target of the error."
        },
        "details": {
          "type": "array",
          "items": {
            "$ref": "#/definitions/Error"
          },
          "description": "An array of errors that led to the reported error."
        },
        "innererror": {
          "$ref": "#/definitions/InnerError",
          "description": "An object containing more specific information than the current object about the error."
        },
        "occurredDateTime": {
          "type": "string",
          "description": "Date and time in UTC when the error occurred.",
          "format": "date-time"
        }
      },
      "required": [
        "code",
        "message"
      ],
      "description": "Error details."
    }
  ```
  
</details>

<details>
<summary>DeviceHealthList</summary>

  ``` json
  {
      "description": "Array of Device Health, with server paging support.",
      "type": "object",
      "properties": {
        "value": {
          "description": "The collection of pageable items.",
          "type": "array",
          "items": {
            "$ref": "#/definitions/DeviceHealth"
          }
        },
        "nextLink": {
          "description": "The link to the next page of items.",
          "type": "string"
        }
      },
      "required": [
        "value"
      ]
    },
  
  ```
</details>

<details>
<summary>HealthCheck</summary>
  
  ``` json
  {
      "description": "Health check",
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Health check name"
        },
        "result": {
          "$ref": "#/definitions/HealthCheckResult",
          "description": "Health check result"
        }
      }
    }
  ```
</details>