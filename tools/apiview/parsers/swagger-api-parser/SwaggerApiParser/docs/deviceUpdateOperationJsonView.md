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