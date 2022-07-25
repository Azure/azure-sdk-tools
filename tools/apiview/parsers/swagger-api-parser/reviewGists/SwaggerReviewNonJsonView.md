###  `/deviceupdate/{instanceId}/updates`

<details>
<summary>POST</summary>

``` yaml
  DeviceUpdate_ImportUpdate
  Import new update version.
  x-ms-long-running-operation: true
  
  Parameters:
    PathParameters:
      instanceId: string
    QueryParameters:
      action: string
      api-verison: string
    BodyParameters:
      updateToImport:
        ImportUpdateInput
          array<object>
  Responses:
    202:
      description: Accepted update import request; backgroud operation location to track status is specified in Operation-Location response headers
      Schema:
        Update
          updateId:
            UpdateId
              provider: string
              name: string
              version: string
           description: string
           frendlyName: string
           isDeployable: boolean
           updateType: string
           installedCriteria: string
           compatibility: array<object>
           instructions:
            Instruction
              steps: array<object>
           referencedBy: array<object>
           scanResult: string
           manifestVersion: string
           importedDateTime: string
           createdDateTime: string
           etag: string
   default:
        description: Default response.
        Schema:
          ErrorResponse
            error: 
              Error
                code: string
                message: string
                target: string
                details: array<Error>
                innererror:
                  InnerError
                    code:string
                    message: string
                    errorDetail: string
           

```
</details>

<details>
<summary>GET</summary>

  ``` yaml
    DeviceUpdate_ListUpdates
    Get a list of all updates that have been imported to Device Update for IoT Hub
    
    Parameters:
      PathParameters:
        instanceId: string
      QueryParameters:
        api-version: string
        $search: string
        $filter: string
    Responses:
      200:
        description: All imported updates, or empty list
        Schema:
          UpdateList
            value: array<object>
            nextLink: string
      default:
        description: Default response.
        Schema:
          ErrorResponse
            error: 
              Error
                code: string
                message: string
                target: string
                details: array<Error>
                innererror:
                  InnerError
                    code:string
                    message: string
                    errorDetail: string
              
          

  ```

</details>


### `/deviceupdate/{instanceId}/updates/providers/{provider}/names/{name}/versions/{version}`

<details>
<summary>GET</summary>

``` yaml
  DeviceUpdate_GetUpdate
  Get a specific update version.
  
  Parameters:
    PathParameters:
      instanceId: string
      provider: string
      name: string
      version: string
    QueryParameters:
      api-version: string
  Responses:
    200:
      description: The requeested update version
      Schema:
       Update
          updateId:
            UpdateId
              provider: string
              name: string
              version: string
           description: string
           frendlyName: string
           isDeployable: boolean
           updateType: string
           installedCriteria: string
           compatibility: array<object>
           instructions:
            Instruction
              steps: array<object>
           referencedBy: array<object>
           scanResult: string
           manifestVersion: string
           importedDateTime: string
           createdDateTime: string
           etag: string
    304:
      description: Not modified
    default:
      description: Default response.
        Schema:
          ErrorResponse
            error: 
              Error
                code: string
                message: string
                target: string
                details: array<Error>
                innererror:
                  InnerError
                    code:string
                    message: string
                    errorDetail: string
  

```
</details>

<details>
<summary>DELETE</summary>

``` yaml
  DeviceUpdate_DeleteUpdate
  Delete a specific update version
  x-ms-long-running-operation: true
  
  Parameters:
    PathParameters:
      instanceId: string
      provider: string
      name: string
      version: string
    QueryParameters:
      api-version: string
  Responses:
    202: 
      description: Accepted update deletion request; background operation location to track status is specified in Operation-Location response header.
    default:
      description: Default response.
        Schema:
          ErrorResponse
            error: 
              Error
                code: string
                message: string
                target: string
                details: array<Error>
                innererror:
                  InnerError
                    code:string
                    message: string
                    errorDetail: string
      
```


</details>