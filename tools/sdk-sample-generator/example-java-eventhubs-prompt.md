# Azure Event Hubs Java Sample

Create a comprehensive Java sample that demonstrates Azure Event Hubs operations using the Azure SDK for Java.

## Requirements

- Use DefaultAzureCredential for authentication
- Show event publishing and consuming
- Demonstrate partition handling
- Include event processor host usage
- Show checkpointing and offset management
- Implement proper error handling and logging
- Use environment variables for configuration

## Expected Operations

1. **Authentication and Client Setup**
   - Use DefaultAzureCredential
   - Get Event Hubs namespace and hub name from environment variables

2. **Event Publishing**
   - Send single events
   - Send batch of events
   - Send events to specific partitions
   - Set event properties and metadata

3. **Event Consuming**
   - Receive events from all partitions
   - Receive events from specific partition
   - Handle partition assignment
   - Process events with checkpoint management

4. **Event Data Features**
   - Send events with custom properties
   - Handle event body in different formats (JSON, binary)
   - Set partition key for routing
   - Handle sequence numbers and offsets

5. **Advanced Scenarios**
   - Event processor client usage
   - Load balancing across multiple consumers
   - Checkpoint and offset management
   - Resume processing from specific offset

6. **Batch Processing**
   - Send events in batches
   - Optimise batch size for throughput
   - Handle batch size limits

7. **Error Handling**
   - Retry policies for transient failures
   - Handle partition unavailability
   - Exception handling best practices

8. **Performance Optimisation**
   - Connection pooling
   - Async/await patterns
   - Memory management for large batches

9. **Cleanup**
   - Close producers and consumers properly
   - Clean up test resources

## Sample Data

Use streaming data scenarios:

- IoT sensor telemetry
- Application log events
- User activity tracking
- Real-time metrics data
