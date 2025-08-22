import com.azure.core.credential.TokenCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.messaging.eventhubs.EventData;
import com.azure.messaging.eventhubs.EventDataBatch;
import com.azure.messaging.eventhubs.EventHubClientBuilder;
import com.azure.messaging.eventhubs.EventHubConsumerClient;
import com.azure.messaging.eventhubs.EventHubProducerClient;
import com.azure.messaging.eventhubs.EventProcessorClient;
import com.azure.messaging.eventhubs.EventProcessorClientBuilder;
import com.azure.messaging.eventhubs.models.EventPosition;
import com.azure.messaging.eventhubs.models.PartitionEvent;
import com.azure.messaging.eventhubs.models.SendOptions;
import com.azure.messaging.eventhubs.checkpointstore.blob.BlobCheckpointStore;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobContainerClientBuilder;

import java.time.Duration;
import java.util.Collections;
import java.util.List;
import java.util.logging.Level;
import java.util.logging.Logger;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

public class AzureEventHubsJavaSampleExample {

    private static final Logger LOGGER = Logger.getLogger(AzureEventHubsJavaSampleExample.class.getName());

    public static void main(String[] args) throws InterruptedException {
        String fullyQualifiedNamespace = System.getenv("EVENT_HUBS_NAMESPACE");
        String eventHubName = System.getenv("EVENT_HUB_NAME");
        String consumerGroup = System.getenv().getOrDefault("EVENT_HUBS_CONSUMER_GROUP", EventHubClientBuilder.DEFAULT_CONSUMER_GROUP_NAME);
        String blobStorageConnectionString = System.getenv("BLOB_STORAGE_CONNECTION_STRING");
        String blobContainerName = System.getenv("BLOB_CONTAINER_NAME");

        if (fullyQualifiedNamespace == null || fullyQualifiedNamespace.isEmpty() || eventHubName == null || eventHubName.isEmpty()
                || blobStorageConnectionString == null || blobStorageConnectionString.isEmpty() || blobContainerName == null || blobContainerName.isEmpty()) {
            LOGGER.severe("Required environment variables: EVENT_HUBS_NAMESPACE, EVENT_HUB_NAME, BLOB_STORAGE_CONNECTION_STRING, BLOB_CONTAINER_NAME");
            return;
        }

        TokenCredential credential = new DefaultAzureCredentialBuilder().build();

        try (EventHubProducerClient producer = new EventHubClientBuilder()
                .credential(fullyQualifiedNamespace, eventHubName, credential)
                .buildProducerClient();
             EventHubConsumerClient consumer = new EventHubClientBuilder()
                     .credential(fullyQualifiedNamespace, eventHubName, credential)
                     .consumerGroup(consumerGroup)
                     .buildConsumerClient()) {

            BlobContainerClient blobContainerClient = new BlobContainerClientBuilder()
                    .connectionString(blobStorageConnectionString)
                    .containerName(blobContainerName)
                    .buildClient();

            BlobCheckpointStore checkpointStore = new BlobCheckpointStore(blobContainerClient);

            publishEvents(producer);

            String partitionId = consumer.getPartitionIds().iterator().next();
            consumeEventsFromPartition(consumer, partitionId);

            CountDownLatch processingCompleteLatch = new CountDownLatch(1);

            EventProcessorClient eventProcessorClient = new EventProcessorClientBuilder()
                    .credential(fullyQualifiedNamespace, eventHubName, credential)
                    .consumerGroup(consumerGroup)
                    .checkpointStore(checkpointStore)
                    .processEvent(eventContext -> {
                        LOGGER.info(String.format("Partition %s - Sequence #: %d - Event Body: %s",
                                eventContext.getPartitionContext().getPartitionId(),
                                eventContext.getEventData().getSequenceNumber(),
                                eventContext.getEventData().getBodyAsString()));
                    })
                    .processError(errorContext -> {
                        LOGGER.log(Level.SEVERE, String.format("Error on partition %s",
                                errorContext.getPartitionContext().getPartitionId()), errorContext.getThrowable());
                    })
                    .buildEventProcessorClient();

            eventProcessorClient.start();

            boolean completed = processingCompleteLatch.await(30, TimeUnit.SECONDS);
            if (!completed) {
                LOGGER.info("Finished processing events with EventProcessorClient after timeout.");
            }

            eventProcessorClient.stop();

        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Encountered exception while running Event Hubs sample.", e);
        }
    }

    private static void publishEvents(EventHubProducerClient producer) {
        EventData event = new EventData("Single event body");
        event.getProperties().put("source", "singleEvent");
        try {
            producer.send(Collections.singletonList(event));
            LOGGER.info("Sent single event.");
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Failed to send single event.", e);
        }

        EventDataBatch batch = producer.createBatch();
        for (int i = 0; i < 5; i++) {
            EventData batchEvent = new EventData("Batch event " + i);
            batchEvent.getProperties().put("batchIndex", String.valueOf(i));
            if (!batch.tryAdd(batchEvent)) {
                LOGGER.warning("Event too large for batch, sending current batch and creating new one.");
                try {
                    producer.send(batch);
                } catch (Exception e) {
                    LOGGER.log(Level.SEVERE, "Failed to send batch.", e);
                }
                batch = producer.createBatch();
                if (!batch.tryAdd(batchEvent)) {
                    throw new IllegalArgumentException("Event is too large even for empty batch.");
                }
            }
        }
        if (batch.getCount() > 0) {
            try {
                producer.send(batch);
                LOGGER.info("Sent batch events.");
            } catch (Exception e) {
                LOGGER.log(Level.SEVERE, "Failed to send batch.", e);
            }
        }

        List<EventData> keyedEvents = List.of(
                new EventData("Event with key A1"),
                new EventData("Event with key A2"));
        SendOptions sendOptions = new SendOptions().setPartitionKey("keyA");
        try {
            producer.send(keyedEvents, sendOptions);
            LOGGER.info("Sent events with partition key.");
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Failed to send events with partition key.", e);
        }
    }

    private static void consumeEventsFromPartition(EventHubConsumerClient consumer, String partitionId) {
        EventPosition startingPosition = EventPosition.latest();
        try {
            consumer.receiveFromPartition(partitionId, 10, startingPosition, Duration.ofSeconds(15))
                    .forEach(event -> LOGGER.info(String.format("Received event from partition %s with sequence number %d and body: %s",
                            partitionId, event.getData().getSequenceNumber(), event.getData().getBodyAsString())));
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Failed to receive events from partition.", e);
        }
    }
}