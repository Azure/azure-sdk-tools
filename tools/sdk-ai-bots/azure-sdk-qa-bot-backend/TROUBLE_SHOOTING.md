# Azure SDK Q&A Bot Troubleshooting Guide

This guide provides step-by-step instructions for troubleshooting issues with the Azure SDK Q&A Bot across different environments.

## Deployments Overview

The Azure SDK Q&A Bot operates in three environments, each with dedicated resources:

### Development Environment
- **Backend Service**: [azuresdkqabot-dev-server](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-dev/providers/Microsoft.Web/sites/azuresdkqabot-dev-server/appServices)
- **Frontend Service**: [azsdkqabotdev](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-dev/providers/Microsoft.Web/sites/azsdkqabotdev/appServices)
- **Logic APP(For Auto Reply)**: [azuresdkqabot-dev-logicapp](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-dev/providers/Microsoft.Logic/workflows/azuresdkqabot-dev-logicapp/logicApp)
- **Team**: [Azure SDK QA Bot](https://teams.microsoft.com/l/team/19%3A3iefzURPmxhDZJJTtwePbdO1EdI5T0hfK9UFK_59Sbk1%40thread.tacv2/conversations?groupId=7ccc31f0-b371-450b-a73c-48f5a31a9b96&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

### Preview Environment
- **Backend Service**: [azure-sdk-qa-bot-test](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-test/providers/Microsoft.Web/sites/azuresdkqabot-test-server/appServices)
- **Frontend Service**: [azsdkqabottest](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-test/providers/Microsoft.Web/sites/azsdkqabottest/appServices)
- **Logic APP(For Auto Reply)**: [azuresdkqabot-test-logicapp](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot-test/providers/Microsoft.Logic/workflows/azuresdkqabot-test-logicapp/logicApp)
- **Team**: [Azure SDK Q&A Bot Testing](https://teams.microsoft.com/l/team/19%3ArMhMrxg7UjfwZmVoSeVvWvNQIfT_G6ds8napsytWqzw1%40thread.tacv2/conversations?groupId=39910aef-85da-4e30-b5e3-35f04ef38648&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

### Production Environment
- **Backend Service**: [azuresdkqabot-server](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot/providers/Microsoft.Web/sites/azuresdkqabot-server/appServices)
- **Frontend Service**: [azsdkqabot](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot/providers/Microsoft.Web/sites/azsdkqabot/appServices)
- **Logic App(For Auto Reply)**: [azuresdkqabot-logicapp](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/azure-sdk-qa-bot/providers/Microsoft.Logic/workflows/azuresdkqabot-logicapp/logicApp)
- **Team**: [Azure SDK](https://teams.microsoft.com/l/team/19%3Af6d52ac6465c40ea80dc86b8be3825aa%40thread.skype/conversations?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)

## Frequently Issues

### 1. Bot Not Responding

When the bot is not responding to messages in Teams, follow these steps to diagnose and resolve the issue.

#### Step 1: Check Logic App Run History

1. Open the appropriate environment's Logic App link in Azure Portal.
2. Click **Development Tools** > **Run history**.
3. Find the run history entry that matches the exact time the message was posted.
![alt text](images/run_history_of_logic_app.png)

#### Step 2: No Run History Found — Teams Connection Issue

If no matching run history entry is found, the Logic App has failed to detect new messages. This is most likely due to an issue with the **non-people account Teams connection** — the account may be expired or otherwise unavailable.

**Solution:** Change the Teams connection from the non-people account to your personal account. (As long as this account is present in the channel where the bot requires automated replies, message monitoring can be achieved.)

1. Open **Development Tools** > **Logic App Designer**.
![alt text](images/app_designer_of_logic_app.png)
1. Click the first node: **"When a new message is added to a chat or channel"**, and check if the configuration can be loaded.
![alt text](images/channels_of_logic_app.png)
2. If the configuration fails to load, click **Connections** at the top, then click **Reassign** > **Add new**, and follow the sign-in steps.
![alt text](images/reassign_of_logic_app.png)
![alt text](images/add_new_connection_of_logic_app.png)
3. Confirm that the connection has changed to your own account, and recheck if the configuration loads successfully.
4. Waiting for new posts in Teams to see if the bot can respond.

### 2. You got a backend alert or bot answered like 'Unable to establish a connection to the AI service at this time. Please try again later.'

#### Step 1: Disable Auto Reply
You could stop more failure by disabling the auto reply feature temporarily.
1. Navigate to the appropriate environment's Logic App in Azure Portal.
2. Click `Disable` button to avoid more failures. 
![alt text](images/disable_logic_app.png)

#### Step 2: Check Logic App Run History

1. Navigate to the appropriate environment's Logic App in Azure Portal.
2. Click Development Tools/Run history
3. Find if there has any failed record
![alt text](images/logic_app_run_history.png)
4. Click into failed record, find the error log
![alt text](images/logic_app_failed.png)

#### Step 3: Analyze Error Logs

##### 3.1 Timeout Error in "Invoke QA Bot" Node

The error log usually appears in the **"Invoke QA Bot"** node. If the error message is:
```
BadRequest
HTTP request failed: the server did not respond within the timeout limit.
Please see logic app limits at https://aka.ms/logic-apps-limits-and-config#http-limits.
```
This typically happens when the bot response takes too much time. However, the bot should still can reply, you could check it in the Teams channel.

##### 3.2 Other Error Logs — Check Frontend Service

For other error logs:
1. Navigate to the appropriate environment's **Frontend Service** in Azure Portal.
2. Search the logs using keywords from the failed post/request, copy the conversation ID
```kql
AppServiceConsoleLogs 
| where ResultDescription contains "Our service was recently assigned"
```
![alt text](images/search_post_logs_from_frontend.png)

3. Use the conversation ID to filter logs related to this request, analyze the error details.
```kql
AppServiceConsoleLogs 
| where ResultDescription contains "19:906c1efbbec54dc8949ac736633e6bdf@thread.skype;messageid=1770672641211"
```
![alt text](images/search_logs_by_conversation_id.png)

4. If you the logs mention **"Failed to fetch data from RAG backend"**:
   - Navigate to the appropriate environment's **Backend Service** in Azure Portal.
   - Search the logs using keywords from the post/request. (Refer to the [Log Analysis Workflow](#log-analysis-workflow) section for detailed steps.)

### Step 4: Check App Service

1. Navigate to the appropriate environment's backend Service in Azure Portal.
2. Click `Log stream` to check if there has realtime logs(eg. /ping)
3. If there has no logs, which means backend service is breaking down. You could use deploy pipeline to redeploy the backend service. After redeploying, you could test the endpoint reference README.md file or send a test message in Teams channel.
1. If service is working well. It may caused by some internal issue, you could investigate error following Log Analysis Workflow part in this document or just disable the Logic App and wait for the developer to solve this issue.

### Step 5: Enable Auto Reply

If you have retryed the bad case and the bot is available, you could enable the auto reply feature.

1. Navigate to the appropriate environment's Logic App in Azure Portal.
2. Click `Enable` button to avoid more failures. 


## Log Analysis Workflow

### Step 1: Access Azure App Service Logs

1. Navigate to the appropriate environment's App Service in Azure Portal
2. Go to **Log stream** or **Monitoring** > **Logs**
   - Log stream is the realtime logs ![Log stream](images/logstream.png)
   - Logs is the offline logs ![Logs](images/logs.png)
3. In **Logs**, click 'Select a Table' and select 'AppServiceConsoleLogs' ![AppServiceConsoleLogs](images/app_service_console_logs.png)

### Step 2: Specify Request Timeline

Use this workflow to trace a specific user interaction:

#### 2.1 Set Time Range
- Determine when the issue occurred
- Set an appropriate time window (typically 15-30 minutes around the incident)
- Consider timezone differences if reported by users

#### 2.2 Search for Initial Request
Search using the user's original query to locate the request.

Look for log entries containing:
- `Request: {json_request}`
- The user's original question or query

1. KQL:
```kql
AppServiceConsoleLogs 
| where ResultDescription contains "your question"
```

2. UI:
![search initial request](images/search_initial_request.png)

#### 2.3 Extract Request ID
From the initial request log, extract the Request ID:
- Look for pattern: `[RequestID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx]`
- Copy the complete UUID for the next step

#### 2.4 Trace Complete Request Flow
Search using the Request ID to get all related logs, order by time ascending

1. KQL:
```kql
AppServiceConsoleLogs 
| where ResultDescription contains "RequestID: xxx"
```

2. UI:
![alt text](images/search_in_table.png)
![alt text](images/search_in_table_result.png)

### Step 3: Analyze Key Log Components

#### 3.1 Query Processing Analysis
Look for these key log entries in this order:

**Original Request Parsing:**
```
[RequestID: xxx] Request: {"message":{"content":"user query"},...}
```

**Search Query Generation:**
```
[RequestID: xxx] Searching query: processed_query_text
```
- Verify the processed query makes sense
- Check if intent recognition modified the original query
- Look for `Intent Result: category: X, intension: Y`

**Intent Recognition Results:**
```
[RequestID: xxx] Intent Result: category: Branded/Unbranded, intension: refined_question
[RequestID: xxx] Intent recognition took: XXXms
```

#### 3.2 Search Results Analysis

**Vector Search Results:**
Look for entries like:
```
[RequestID: xxx] Vector searched chunk(high score): {document details}
[RequestID: xxx] Vector searched chunk: {document details}
[RequestID: xxx] Vector searched chunk(Q&A): {document details}
```

**Agentic Search Results:**
```
[RequestID: xxx] Agentic search sub queries: [list of generated queries]
[RequestID: xxx] Agentic search took: XXXms
[RequestID: xxx] Agentic searched chunk: 
```
**Key metrics to analyze:**
- **RerankScore**: Should be >=3 for high relevance, >=1.5 for acceptable
- **Chunk**: Check if appropriate sources are being found
- **Search timing**: `XXX Search took: XXXms`

#### 3.3 Performance Metrics
Track these timing metrics:
- Intent recognition: `Intent recognition took: XXXms`
- Search operation: `Vector Search took: XXXms`
- Chunk processing: `Chunk processing took: XXXms`
- Agentic search: `Agentic search took: XXXms`
- OpenAI completion: `OpenAI completion took: XXXms`
- Total time: `Total ChatCompletion time: XXXms`

### Step 4: Quality Assessment

#### 4.1 Search Quality Indicators

**Good Search Results:**
- Multiple chunks with RerankScore >= 2
- Relevant document categories for the query type
- Appropriate mix of documentation sources

**Poor Search Results:**
- All chunks have RerankScore <= 1.5
- No high-relevance chunks found
- Documents from irrelevant categories

#### 4.2 Common Search Issues

**Low Relevance Scores:**
```
[RequestID: xxx] Skipping result with low score: source/document, score: 0.1
```
- Indicates query might be too vague or out of scope
- Consider query preprocessing improvements

**No High-Score Results:**
```
[RequestID: xxx] No results found with high relevance score, supply with normal results
```
- Bot is using fallback results
- May indicate knowledge base gaps

**Complete Documents vs Chunks:**
```
[RequestID: xxx] Complete chunk: source/document
```
- High-relevance results trigger complete document retrieval
- Good indicator of confident matches

## Common Issues and Solutions

### Issue 1: Poor Search Results

**Symptoms:**
- Bot provides generic or irrelevant answers
- Low rerank scores across all results
- User feedback indicates missing information

**Troubleshooting Steps:**
1. Check the "Searching query" vs original user input
2. Verify intent recognition results
3. Examine rerank scores of returned documents
4. Validate search sources are appropriate for query type

**Solutions:**
- Update query preprocessing logic
- Improve intent recognition prompts
- Add missing documentation to knowledge base
- Adjust rerank score thresholds

### Issue 2: Slow Response Times

**Symptoms:**
- Total response time > 10 seconds
- User timeouts or complaints about performance

**Troubleshooting Steps:**
1. Check individual component timings
2. Identify the slowest operation
3. Verify external service health (OpenAI, Search)

**Common Bottlenecks:**
- OpenAI API latency
- Large document processing
- Search index performance
- Network connectivity issues

### Issue 3: Authentication Errors

**Symptoms:**
- 401/403 errors in logs
- "Failed to get token" messages
- Service unavailable responses

**Troubleshooting Steps:**
1. Verify Managed Identity configuration
2. Check token acquisition logs
3. Validate service permissions

### Issue 4: Image Processing Issues

**Symptoms:**
- Images not processed correctly
- "Failed to download attachment" errors

**Troubleshooting Steps:**
1. Check image URL format and accessibility
2. Verify Bot Framework authentication
3. Validate image encoding process

---

For additional assistance, refer to the main README.md or contact the development team.
