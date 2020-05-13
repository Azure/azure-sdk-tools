# Pipeline Witness

Pipeline Witness is an Azure Function that processes webhook notifications from Azure Pipelines (via [Webhook Router](../webhook-router/README.md). The purpose of the service is to process _Run State Changed_ notifications and extract detailed build information in an easily queryable format for analysis in our health metrics reporting for the Azure SDK Engineering System.

## Requirements

Our health metrics engineering system needs to handle evolution in the underlying tools that we use and the types of analysis that we need to perform. For example we need to handle transitioning from Azure Pipelines to GitHub Actions, or running them side by side. We also need to be able to add logic to extract additional data from our data store that we might not have considered up front.

## Core principles

At a high level here are some of the core principles that are going into this design:

1. Assume source data is volatile, may be changed, may be deleted.
2. Assume source tools may change over time.
3. Assume we don't know all the questions we want to ask yet.
4. Assume that push notifications are unreliable (lost messages).
5. Assume that polling isn't scalable (rate limiting).
6. Assume that despite our best efforts, we'll be missing data.

## Approach

With the requirements and core principles in mind, here is the high level architecture for Pipeline Witness.

### Store everything

Whenever we receive a webhook we should store the webhook payload. If the payload doesn't represent the entirety of the data that we need enrich the data. For example _Run State Changed_ events from Azure Pipelines contain just minimal information about the run. So when we receive that event we would then go and pull the full run details. This would include pulling the timeline records and associated log entries.

### Record locators

Every JSON webhook payload, every REST API JSON result and every log file should be stored, and identified with a URI so that it can be addressed later.

The URI sceheme that we use needs to assume that our tools will change over time so they either need to be agnostic or bake in hints that the record is specifically associated with a particular tool (so that it can later be processed and normalized).

For example, the _Run State Changed_ notifications from Azure Pipelines might be identified the following way:

```pipeline-witness:///providers/azurepipelines/projects/{projectId}/pipelines/{pipelineId}/runs/{runId}/events/{eventId}```

This record locator would be inserted into our graph/table data store along with key values from the payload itself. The JSON payload would be stored as a blob in Azure Storage.

An example of another record might be an Azure Pipelines run record itself:

```pipeline-witness:///providers/azurepipelines/projects/{projectId}/pipelines/{pipelineId}/runs/{runId}/attempts/{attemptCount}```

Notice how the URI captures the fact a run can be attempted multiple times? The URI namespace for GitHub actions would likely be different (despite some obvious similarities).

```pipeline-witness:///providers/githubactions/repositories/{repositoryId}/runs/{runId}```

### Record expansion

The intention is that as we receive events we'll create records, and some records will result in us needing to fetch more records and so on. When we receive an event we will will bootstrap the creation of a record (each record will be represented by a type). That record will be passed into a reactor where multiple "subscribers" can evaluate whether they can enrich the data associate with that record which creates more records and so on.

If every record is represented as a type, then the logic to enrich records can also be represented as types which process records. For example we might have the following type to represent a RunStateChangedEvent:

```csharp
public class RunStateChangedEvent : Record
{
}
```

Then we would have a base class that knows how to expand records:

```csharp
public class RecordEvaluator<T> where T: Record
{
    public Task EvaluateAsync(RecordEvaluationContext context, T record)
    {
    }
}
```

Inside the ```EvaluateAsync``` method we would scan the record to determine whether the record is up to date (we might change what key values we extract over time) and whether other related records can be created. The evaluator would make calls against the ```context``` variable to queue up the creation and processing of those records.

Because the way we want to process records can change over time, each record type has an integer that specifies the "version" of the record format. Over time the system will process all records multiple times and when it encounters a record that does not match the latest format it will upgrade the format.

### Records to insights

The record expansion process is primarily concerned with harvesting and maintaining the records to get insights into the health of our engineering system. But another important function of record expansion is extracting out key data that is used to drive reporting.

For example, when a pipeline run record is fully expanded we should have sufficient detail to answer questions like how much wait time a particular run and/or job experienced. This insight would be persisted along with the record so that it is easily queryable with Power BI (we aren't building our own visualization and reporting layer).