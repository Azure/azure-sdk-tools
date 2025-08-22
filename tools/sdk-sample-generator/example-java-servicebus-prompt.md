# Azure Service Bus Java Sample

Create a comprehensive Java sample that demonstrates Azure Service Bus messaging operations using the Azure SDK for Java.

## Requirements

- Use DefaultAzureCredential for authentication
- Show both queue and topic/subscription patterns
- Demonstrate message sending and receiving
- Include session-based messaging
- Show dead letter queue handling
- Implement proper error handling and logging
- Use environment variables for configuration

## Expected Operations

1. **Authentication and Client Setup**
   - Use DefaultAzureCredential
   - Get Service Bus namespace from environment variable

2. **Queue Operations**
   - Send messages to queue
   - Receive messages from queue
   - Peek messages without consuming
   - Handle message properties and metadata

3. **Topic/Subscription Operations**
   - Send messages to topic
   - Receive messages from subscription
   - Handle multiple subscribers
   - Demonstrate message filtering

4. **Message Features**
   - Send messages with custom properties
   - Schedule messages for future delivery
   - Set message TTL (time-to-live)
   - Handle message correlation and session IDs

5. **Advanced Scenarios**
   - Session-based messaging
   - Message deferral and completion
   - Dead letter queue processing
   - Auto-complete vs manual message handling

6. **Batch Operations**
   - Send multiple messages in batch
   - Receive multiple messages at once

7. **Error Handling**
   - Retry policies
   - Exception handling for transient failures
   - Dead letter queue scenarios

8. **Cleanup**
   - Complete/abandon messages properly
   - Clean up test resources

## Sample Data

Use realistic message scenarios:

- Order processing messages
- User notification events
- System health alerts
- Custom message properties and correlation IDs
