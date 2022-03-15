param logAnalyticsResource string

@description('The friendly name for the workbook that is used in the Gallery or Saved List.  This name must be unique within a resource group.')
param workbookDisplayName string

@description('The gallery that the workbook will been shown under. Supported values include workbook, tsg, etc. Usually, this is \'workbook\'')
param workbookType string = 'workbook'

@description('The id of resource instance to which the workbook will be associated')
param workbookSourceId string = 'azure monitor'

// The guid for this workbook instance (must be deterministic in order to be idempotent)
var workbookId = guid(resourceGroup().name)

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
            id: '8f132ca2-e11d-4ec4-b2cf-e3d33a7cca0b'
            version: 'KqlParameterItem/1.0'
            name: 'PodUidParameter'
            label: 'Pod'
            type: 2
            description: 'Pod+Container name for metrics'
            isRequired: true
            multiSelect: true
            quote: '\''
            delimiter: ','
            query: 'Perf \r\n| where ObjectName == "K8SContainer"\r\n| distinct InstanceName\r\n| extend ResourceId = split(InstanceName, \'/\')\r\n| extend PodUid = strcat(ResourceId[-2])\r\n| extend ContainerName = strcat(ResourceId[-1])\r\n| distinct ContainerName, PodUid\r\n// Exclude init containers\r\n| where ContainerName !startswith "init-"\r\n| join kind=inner (\r\n  KubePodInventory\r\n  | where Namespace != "kube-system" and Namespace != "stress-infra"\r\n  | distinct Namespace, Name, PodUid\r\n) on PodUid\r\n| project value = PodUid, label = Name, group = Namespace\r\n'
            crossComponentResources: [
              logAnalyticsResource
            ]
            typeSettings: {
              additionalResourceOptions: [
                'value::all'
              ]
              showDefault: false
            }
            timeContext: {
              durationMs: 3600000
            }
            queryType: 0
            resourceType: 'microsoft.operationalinsights/workspaces'
          }
          {
            id: '7f1f155e-7592-404a-a809-10d06ba6eaf7'
            version: 'KqlParameterItem/1.0'
            name: 'TimeRange'
            type: 4
            isRequired: true
            value: {
              durationMs: 14400000
            }
            typeSettings: {
              selectableValues: [
                {
                  durationMs: 300000
                }
                {
                  durationMs: 1800000
                }
                {
                  durationMs: 3600000
                }
                {
                  durationMs: 14400000
                }
                {
                  durationMs: 43200000
                }
                {
                  durationMs: 86400000
                }
                {
                  durationMs: 259200000
                }
                {
                  durationMs: 604800000
                }
              ]
              allowCustom: true
            }
            timeContext: {
              durationMs: 3600000
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
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'Perf\r\n| where ObjectName == "K8SContainer"\r\n| where CounterName == "cpuUsageNanoCores"\r\n| extend Container = split(InstanceName, \'/\')\r\n| extend PodUid = tostring(Container[-2])\r\n| extend ContainerName = tostring(Container[-1])\r\n| where ContainerName !startswith "init-"\r\n| where PodUid in ({PodUidParameter})\r\n| join kind=inner (\r\n  KubePodInventory\r\n  | where Namespace != "kube-system" and Namespace != "stress-infra"\r\n  | distinct Namespace, Name, PodUid\r\n) on PodUid\r\n| project TimeGenerated, strcat(Name, \'/\', ContainerName), CounterValue / 1000000\r\n| render timechart\r\n'
              size: 0
              aggregation: 3
              title: 'CPU (Millicores)'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
            }
            customWidth: '50'
            name: 'CPU millicores'
            styleSettings: {
              maxWidth: '50'
            }
          }
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'Perf\r\n| where ObjectName == "K8SContainer"\r\n| where CounterName == "memoryWorkingSetBytes"\r\n\r\n| extend Container = split(InstanceName, \'/\')\r\n| extend PodUid = tostring(Container[-2])\r\n| extend ContainerName = tostring(Container[-1])\r\n| where ContainerName !startswith "init-"\r\n| where PodUid in ({PodUidParameter})\r\n| join kind=inner (\r\n  KubePodInventory\r\n  | where Namespace != "kube-system" and Namespace != "stress-infra"\r\n  | distinct Namespace, Name, PodUid\r\n) on PodUid\r\n| project TimeGenerated, strcat(Name, \'/\', ContainerName), CounterValue / 1000000\r\n| render timechart\r\n'
              size: 0
              aggregation: 3
              title: 'Memory (MB)'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
            }
            customWidth: '50'
            name: 'Memory MB'
            styleSettings: {
              maxWidth: '50'
            }
          }
        ]
      }
      name: 'container stats'
    }
    {
      type: 12
      content: {
        version: 'NotebookGroup/1.0'
        groupType: 'editable'
        title: 'Events'
        items: [
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'KubePodInventory\r\n| extend ContainerId = split(ContainerName, \'/\')\r\n| extend PodUid = ContainerId[0]\r\n| where PodUid in ({PodUidParameter})\r\n| extend ContainerHumanName = ContainerId[1]\r\n// Ensure window function (prev()) checks state changes against correct groupings\r\n| sort by Name, ContainerName, TimeGenerated\r\n// Only show container state changes and the first element of each ContainerName group\r\n| where prev(ContainerStatus) != ContainerStatus or prev(ContainerName) != ContainerName\r\n| sort by TimeGenerated, ContainerCreationTimeStamp\r\n| project-reorder TimeGenerated, Name, PodStatus, ContainerHumanName, ContainerStatus, Computer, PodCreationTimeStamp, PodStartTime, ContainerCreationTimeStamp, * asc\r\n'
              size: 1
              showAnalytics: true
              title: 'Pod/Container Events'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
              gridSettings: {
                rowLimit: 50
              }
            }
            customWidth: '50'
            name: 'KubeEvents'
            styleSettings: {
              maxWidth: '50'
            }
          }
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'let containerData = KubePodInventory\r\n| where Namespace != "kube-system" and Namespace != "stress-infra"\r\n| where ContainerName !startswith "init-"\r\n| where PodUid in ({PodUidParameter})\r\n| extend Container = tostring(split(ContainerName, \'/\')[1])\r\n| extend TargetContainerID = ContainerID\r\n| distinct Name, Container, TargetContainerID;\r\nlet containerIDs = containerData | project TargetContainerID;\r\nContainerLog\r\n| where LogEntry has "chaos-daemon-server"\r\n| extend TargetContainerID = extract("containerd://(.*)\\\\\\\\", 1, LogEntry)\r\n| where isnotempty(TargetContainerID) and TargetContainerID in (containerIDs)\r\n| project TargetContainerID, LogEntry\r\n| join kind=inner (containerData) on TargetContainerID\r\n| extend Pod=Name\r\n| project Pod, Container, LogEntry'
              size: 1
              title: 'Chaos Daemon Events'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              showRefreshButton: true
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
            }
            customWidth: '50'
            name: 'ChaosEvents'
            styleSettings: {
              maxWidth: '50'
            }
          }
        ]
      }
      name: 'events'
    }
    {
      type: 12
      content: {
        version: 'NotebookGroup/1.0'
        groupType: 'editable'
        title: 'App Insights Telemetry'
        items: [
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'AppEvents\r\n| where TimeGenerated {TimeRange}\r\n| extend Name = AppRoleInstance\r\n| join kind=inner (\r\n  KubePodInventory\r\n  | where TimeGenerated {TimeRange}\r\n  | where Namespace != "kube-system" and Namespace != "stress-infra"\r\n  | distinct Name, PodUid\r\n) on Name\r\n| where PodUid in ({PodUidParameter})\r\n| project-away TenantId'
              size: 0
              title: 'Stress Test Events'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
            }
            customWidth: '50'
            name: 'StressTestEvents'
            styleSettings: {
              maxWidth: '50'
            }
          }
          {
            type: 3
            content: {
              version: 'KqlItem/1.0'
              query: 'AppExceptions\r\n| where TimeGenerated {TimeRange}\r\n| extend Name = AppRoleInstance\r\n| join kind=inner (\r\n  KubePodInventory\r\n  | where Namespace != "kube-system" and Namespace != "stress-infra"\r\n  | where TimeGenerated {TimeRange}\r\n  | distinct Name, PodUid\r\n) on Name\r\n| where PodUid in ({PodUidParameter})\r\n| project-reorder TimeGenerated, ProblemId, ExceptionType, OuterMessage, Message, Details, Assembly, OuterAssembly, * asc'
              size: 1
              title: 'Exceptions'
              timeContext: {
                durationMs: 14400000
              }
              timeContextFromParameter: 'TimeRange'
              queryType: 0
              resourceType: 'microsoft.operationalinsights/workspaces'
              crossComponentResources: [
                logAnalyticsResource
              ]
              gridSettings: {
                rowLimit: 500
              }
            }
            customWidth: '50'
            name: 'AppExceptions'
            styleSettings: {
              maxWidth: '50'
            }
          }
        ]
      }
      name: 'AppInsightsTelemetry'
    }
    {
      type: 3
      content: {
        version: 'KqlItem/1.0'
        query: 'let containerData = KubePodInventory\r\n| where Namespace != "kube-system" and Namespace != "stress-infra"\r\n| where PodUid in ({PodUidParameter})\r\n| extend Container = tostring(split(ContainerName, \'/\')[1])\r\n| distinct Name, Container, ContainerID;\r\nlet containerIDs = containerData | project ContainerID;\r\nContainerLog\r\n| project ContainerID, LogEntry, TimeGenerated\r\n| join kind=inner (containerData) on ContainerID\r\n| extend Pod=Name\r\n| project TimeGenerated, Pod, Container, LogEntry\r\n| sort by TimeGenerated desc'
        size: 0
        showAnalytics: true
        title: 'Container Logs'
        timeContext: {
          durationMs: 14400000
        }
        timeContextFromParameter: 'TimeRange'
        queryType: 0
        resourceType: 'microsoft.operationalinsights/workspaces'
        crossComponentResources: [
          logAnalyticsResource
        ]
      }
      name: 'ContainerLogs'
    }
  ]
  isLocked: false
  fallbackResourceIds: [
    'azure monitor'
  ]
}

resource workbookId_resource 'microsoft.insights/workbooks@2021-03-08' = {
  name: workbookId
  location: resourceGroup().location
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
