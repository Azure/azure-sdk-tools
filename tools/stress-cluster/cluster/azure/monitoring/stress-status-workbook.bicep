param logAnalyticsResource string
param location string = resourceGroup().location

@description('The friendly name for the workbook that is used in the Gallery or Saved List.  This name must be unique within a resource group.')
param workbookDisplayName string

@description('The gallery that the workbook will been shown under. Supported values include workbook, tsg, etc. Usually, this is \'workbook\'')
param workbookType string = 'workbook'

@description('The id of resource instance to which the workbook will be associated')
param workbookSourceId string = resourceGroup().id

// The guid for this workbook instance (must be deterministic in order to be idempotent)
var workbookId = guid('${resourceGroup().name} ${workbookDisplayName}')

var workbookContent = {
  version: 'Notebook/1.0'
  items: [
    {
      type: 9
      content: {
        version: 'KqlParameterItem/1.0'
        crossComponentResources: [
          logAnalyticsResource
        ]
        parameters: [
          {
            id: '173c3fbc-80a7-4b56-b4cc-b7a982ffe7c9'
            version: 'KqlParameterItem/1.0'
            name: 'TimeRange'
            type: 4
            isRequired: true
            value: {
              durationMs: 604800000
            }
            typeSettings: {
              selectableValues: [
                {
                  durationMs: 43200000
                }
                {
                  durationMs: 86400000
                }
                {
                  durationMs: 172800000
                }
                {
                  durationMs: 259200000
                }
                {
                  durationMs: 604800000
                }
                {
                  durationMs: 1209600000
                }
                {
                  durationMs: 2592000000
                }
                {
                  durationMs: 5184000000
                }
              ]
              allowCustom: true
            }
            timeContext: {
              durationMs: 86400000
            }
          }
          {
            id: '96d5328c-30fa-4fdd-a449-54d560c217f5'
            version: 'KqlParameterItem/1.0'
            name: 'TestNameParameter'
            label: 'TestName'
            type: 2
            multiSelect: true
            quote: '\''
            delimiter: ','
            query: 'KubePodInventory\r\n| where ControllerKind =~ "Job"\r\n        and Namespace !in ("kube-system", "stress-infra")\r\n| summarize arg_max(TimeGenerated, *)\r\n        by extractjson(\'$.controller-uid\', tostring(parse_json(PodLabel)[0]))\r\n| extend TestName = replace_regex(ControllerName,  \'-[[:digit:]]+\', \'\')\r\n| summarize arg_max(TimeGenerated, *) by TestName, Namespace\r\n| sort by Namespace asc, TestName asc\r\n| project value=extractjson(\'$.controller-uid\', tostring(parse_json(PodLabel)[0])),\r\n        label=TestName, group=Namespace\r\n        \r\n'
            crossComponentResources: [
              logAnalyticsResource
            ]
            value: [
              'value::all'
            ]
            typeSettings: {
              additionalResourceOptions: [
                'value::all'
              ]
              showDefault: false
            }
            timeContext: {
              durationMs: 604800000
            }
            queryType: 0
            resourceType: 'microsoft.operationalinsights/workspaces'
          }
          {
            id: '239f3763-8724-46c5-9fe4-03613656131b'
            version: 'KqlParameterItem/1.0'
            name: 'ShowErrorsOnly'
            label: 'Show errors only'
            type: 10
            isRequired: true
            value: 'CompletedPodInitializingErrorImagePullBackOffErrImagePullContainerStatusUnknown'
            typeSettings: {
              additionalResourceOptions: []
              showDefault: false
            }
            jsonData: '[\r\n    { "value":"ErrorImagePullBackOffErrImagePullContainerStatusUnknown", "label":"True" },\r\n    { "value":"CompletedPodInitializingErrorImagePullBackOffErrImagePullContainerStatusUnknown", "label":"False" }\r\n]'
            timeContext: {
              durationMs: 604800000
            }
          }
        ]
        style: 'pills'
        queryType: 0
        resourceType: 'microsoft.operationalinsights/workspaces'
      }
      name: 'parameters - 1'
    }
    {
      type: 12
      content: {
        version: 'NotebookGroup/1.0'
        groupType: 'editable'
        items: [
          {
            type: 1
            content: {
              json: 'For more detailed information on pods with unknown status, execute the following command:\r\n###### kubectl get pods --namespace &lt;namespace&gt; &lt;podname&gt; -o=json'
              style: 'info'
            }
            name: 'text - 1'
          }
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'let StatusTable = KubePodInventory\r\n| where ControllerKind =~ "Job"\r\n        and PodCreationTimeStamp {TimeRange}\r\n        and Namespace !in ("kube-system", "stress-infra")\r\n        and "{ShowErrorsOnly}" contains ContainerStatusReason\r\n| summarize arg_max(TimeGenerated, *)\r\n        by extractjson(\'$.controller-uid\', tostring(parse_json(PodLabel)[0]))\r\n| extend TestName = replace_regex(ControllerName,  \'-[[:digit:]]+\', \'\')\r\n| sort by Namespace asc, TestName asc, PodCreationTimeStamp asc;\r\n\r\nlet FailTrend = StatusTable\r\n| extend IsError=case(ContainerStatusReason in~ ("Completed", "PodInitializing", ""), 0,\r\n                      ContainerStatusReason in~ ("Error", "ImagePullBackOff", "ErrImagePull"), 2,\r\n                      1)\r\n| summarize FailTrend=make_list(IsError, 10) by TestName, Namespace;\r\n\r\nStatusTable\r\n| summarize arg_max(TimeGenerated, *) by TestName, Namespace\r\n| join (FailTrend) on TestName, Namespace\r\n| sort by Namespace asc, TestName asc, PodCreationTimeStamp desc\r\n| extend Duration=strcat(replace_string(replace_string(tostring(format_timespan(TimeGenerated - PodCreationTimeStamp, \'d-HH:mm\')), \'-\', \'d \'), \':\', \'hr \'), \'m\')\r\n| where extractjson(\'$.controller-uid\', tostring(parse_json(PodLabel)[0])) in ({TestNameParameter})\r\n| extend Status = case(\r\n    ContainerStatus =~ "running", ContainerStatus,\r\n    ContainerStatusReason\r\n)\r\n| project TestName,\r\n        Namespace,\r\n        StartTime=format_datetime(PodCreationTimeStamp, \'MM-dd-yyyy HH:mm\'),\r\n        Status,\r\n        Duration,\r\n        FailTrend\r\n'
              size: 3
              title: 'Stress Test Status'
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
              gridSettings: {
                formatters: [
                  {
                    columnMatch: 'Status'
                    formatter: 18
                    formatOptions: {
                      thresholdsOptions: 'colors'
                      thresholdsGrid: [
                        {
                          operator: '=='
                          thresholdValue: 'Completed'
                          representation: 'green'
                          text: 'Completed'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'Error'
                          representation: 'redDark'
                          text: 'Error'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'ErrImagePull'
                          representation: 'redBright'
                          text: 'Error Image Pull'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'ImagePullBackOff'
                          representation: 'red'
                          text: 'Image Pull BackOff'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'PodInitializing'
                          representation: 'green'
                          text: 'Pod Initializing'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'ContainerStatusUnknown'
                          representation: 'yellow'
                          text: 'Status Unknown'
                        }
                        {
                          operator: '=='
                          thresholdValue: 'running'
                          representation: 'lightBlue'
                          text: 'Running'
                        }
                        {
                          operator: 'Default'
                          thresholdValue: null
                          text: 'N/A'
                        }
                      ]
                    }
                    tooltipFormat: {
                      tooltip: 'To get more detailed information on pods with unknown status, execute the following command: kubectl get pods --namespace <namespace> <podname> -o=json'
                    }
                  }
                  {
                    columnMatch: 'Trend'
                    formatter: 10
                    formatOptions: {
                      palette: 'greenRed'
                    }
                  }
                ]
              }
            }
            name: 'query - 0'
            styleSettings: {
              showBorder: true
            }
          }
        ]
      }
      name: 'group - 3'
    }
  ]
  isLocked: false
  fallbackResourceIds: [
    workbookSourceId
  ]
}

resource workbookId_resource 'microsoft.insights/workbooks@2021-03-08' = {
  name: workbookId
  location: location
  kind: 'shared'
  properties: {
    displayName: workbookDisplayName
    serializedData: string(workbookContent)
    version: '1.0'
    sourceId: workbookSourceId
    category: workbookType
  }
  dependsOn: []
}

output id string = workbookId_resource.id
