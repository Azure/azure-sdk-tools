import com.azure.core.credential.TokenCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.messaging.servicebus.*;
import com.azure.messaging.servicebus.models.ServiceBusReceiveMode;
import com.azure.messaging.servicebus.models.SubQueue;

import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.*;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.function.Consumer;

public class AzureServiceBusJavaSampleExample {

    private static final String NAMESPACE_ENV = "SERVICE_BUS_NAMESPACE";
    private static final String QUEUE_NAME_ENV = "SERVICE_BUS_QUEUE_NAME";
    private static final String TOPIC_NAME_ENV = "SERVICE_BUS_TOPIC_NAME";
    private static final String SUBSCRIPTION_NAME_ENV = "SERVICE_BUS_SUBSCRIPTION_NAME";
    private static final String SESSION_QUEUE_NAME_ENV = "SERVICE_BUS_SESSION_QUEUE_NAME";

    private static final int MAX_CONCURRENT_SESSIONS = 5;

    public static void main(String[] args) throws InterruptedException {
        String fullyQualifiedNamespace = System.getenv(NAMESPACE_ENV);
        String queueName = System.getenv(QUEUE_NAME_ENV);
        String topicName = System.getenv(TOPIC_NAME_ENV);
        String subscriptionName = System.getenv(SUBSCRIPTION_NAME_ENV);
        String sessionQueueName = System.getenv(SESSION_QUEUE_NAME_ENV);

        if (fullyQualifiedNamespace == null || queueName == null || topicName == null || subscriptionName == null || sessionQueueName == null) {
            System.err.println("Please set all required environment variables: SERVICE_BUS_NAMESPACE, SERVICE_BUS_QUEUE_NAME, SERVICE_BUS_TOPIC_NAME, SERVICE_BUS_SUBSCRIPTION_NAME, SERVICE_BUS_SESSION_QUEUE_NAME");
            System.exit(1);
        }

        TokenCredential credential = new DefaultAzureCredentialBuilder().build();

        // Queue operations
        sendMessagesToQueue(fullyQualifiedNamespace, queueName, credential);
        receiveMessagesFromQueue(fullyQualifiedNamespace, queueName, credential);
        peekMessagesFromQueue(fullyQualifiedNamespace, queueName, credential);

        // Topic/Subscription operations
        sendMessagesToTopic(fullyQualifiedNamespace, topicName, credential);
        receiveMessagesFromSubscription(fullyQualifiedNamespace, topicName, subscriptionName, credential);

        // Session based messaging
        sendMessagesToSessionQueue(fullyQualifiedNamespace, sessionQueueName, credential);
        receiveMessagesFromSessionQueue(fullyQualifiedNamespace, sessionQueueName, credential);

        // Dead letter queue
        receiveMessagesFromDeadLetterQueue(fullyQualifiedNamespace, queueName, credential);
    }

    private static void sendMessagesToQueue(String namespace, String queueName, TokenCredential credential) {
        try (ServiceBusSenderClient sender = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .sender()
                .queueName(queueName)
                .buildClient()) {

            List<ServiceBusMessage> messages = List.of(
                    new ServiceBusMessage("OrderID:12345:Processing order")
                            .setContentType("application/json")
                            .setCorrelationId("order-12345")
                            .setScheduledEnqueueTime(OffsetDateTime.now().plusSeconds(10)) // Schedule for 10 seconds later
                            .setTimeToLive(Duration.ofMinutes(30))
                            .setApplicationProperties(Collections.singletonMap("Priority", "High")),
                    new ServiceBusMessage("UserNotification: Welcome user123!")
                            .setContentType("text/plain")
                            .setCorrelationId("user-123123")
                            .setTimeToLive(Duration.ofMinutes(5))
                            .setApplicationProperties(Collections.singletonMap("NotificationType", "Welcome")));

            // Send messages in batch
            ServiceBusMessageBatch batch = sender.createMessageBatch();
            for (ServiceBusMessage message : messages) {
                if (!batch.tryAddMessage(message)) {
                    sender.sendMessages(batch);
                    batch = sender.createMessageBatch();
                    if (!batch.tryAddMessage(message)) {
                        throw new IllegalArgumentException("Message too large for batch");
                    }
                }
            }
            if (batch.getCount() > 0) {
                sender.sendMessages(batch);
            }

            System.out.println("Sent messages to queue: " + queueName);
        } catch (Exception ex) {
            System.err.println("Error sending messages to queue: " + ex.getMessage());
        }
    }

    private static void receiveMessagesFromQueue(String namespace, String queueName, TokenCredential credential) throws InterruptedException {
        CountDownLatch latch = new CountDownLatch(3);

        Consumer<ServiceBusReceivedMessageContext> processMessage = context -> {
            ServiceBusReceivedMessage message = context.getMessage();
            System.out.printf("Queue Message Received: messageId=%s, body=%s, correlationId=%s, properties=%s%n",
                    message.getMessageId(),
                    message.getBody().toString(),
                    message.getCorrelationId(),
                    message.getApplicationProperties());
            try {
                context.complete();
            } catch (Exception e) {
                System.err.println("Failed to complete message: " + e.getMessage());
            }
            latch.countDown();
        };

        Consumer<ServiceBusErrorContext> processError = errorContext -> {
            System.err.println("Error occurred while receiving queue messages: " + errorContext.getException());
        };

        try (ServiceBusProcessorClient processor = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .processor()
                .queueName(queueName)
                .receiveMode(ServiceBusReceiveMode.PEEK_LOCK)
                .disableAutoComplete()
                .processMessage(processMessage)
                .processError(processError)
                .buildProcessorClient()) {

            processor.start();
            System.out.println("Receiving messages from queue... Waiting to process 3 messages.");
            if (!latch.await(30, TimeUnit.SECONDS)) {
                System.out.println("Timed out waiting for queue messages.");
            }
            processor.stop();
            System.out.println("Stopped receiving from queue.");
        } catch (Exception ex) {
            System.err.println("Error receiving messages from queue: " + ex.getMessage());
        }
    }

    private static void peekMessagesFromQueue(String namespace, String queueName, TokenCredential credential) {
        try (ServiceBusReceiverClient receiver = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .receiver()
                .queueName(queueName)
                .buildClient()) {

            Iterable<ServiceBusReceivedMessage> peekedMessages = receiver.peekMessages(5);
            System.out.println("Peeked messages from queue:");
            for (ServiceBusReceivedMessage msg : peekedMessages) {
                System.out.printf(" messageId=%s, body=%s%n", msg.getMessageId(), msg.getBody().toString());
            }
        } catch (Exception ex) {
            System.err.println("Error peeking messages from queue: " + ex.getMessage());
        }
    }

    private static void sendMessagesToTopic(String namespace, String topicName, TokenCredential credential) {
        try (ServiceBusSenderClient sender = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .sender()
                .topicName(topicName)
                .buildClient()) {

            ServiceBusMessage message1 = new ServiceBusMessage("System Health Alert: CPU Usage High")
                    .setContentType("text/plain")
                    .setApplicationProperties(Collections.singletonMap("AlertLevel", "Critical"));

            ServiceBusMessage message2 = new ServiceBusMessage("System Health Alert: Memory Usage High")
                    .setContentType("text/plain")
                    .setApplicationProperties(Collections.singletonMap("AlertLevel", "Warning"));

            sender.sendMessages(Arrays.asList(message1, message2));

            System.out.println("Sent messages to topic: " + topicName);
        } catch (Exception ex) {
            System.err.println("Error sending messages to topic: " + ex.getMessage());
        }
    }

    private static void receiveMessagesFromSubscription(String namespace, String topicName, String subscriptionName, TokenCredential credential) throws InterruptedException {
        CountDownLatch latch = new CountDownLatch(2);

        Consumer<ServiceBusReceivedMessageContext> processMessage = context -> {
            ServiceBusReceivedMessage message = context.getMessage();
            System.out.printf("Subscription Message Received: messageId=%s, body=%s, applicationProperties=%s%n",
                    message.getMessageId(),
                    message.getBody().toString(),
                    message.getApplicationProperties());
            try {
                context.complete();
            } catch (Exception e) {
                System.err.println("Failed to complete subscription message: " + e.getMessage());
            }
            latch.countDown();
        };

        Consumer<ServiceBusErrorContext> processError = errorContext -> {
            System.err.println("Error occurred while receiving subscription messages: " + errorContext.getException());
        };

        try (ServiceBusProcessorClient processor = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .processor()
                .topicName(topicName)
                .subscriptionName(subscriptionName)
                .receiveMode(ServiceBusReceiveMode.PEEK_LOCK)
                .disableAutoComplete()
                .processMessage(processMessage)
                .processError(processError)
                .buildProcessorClient()) {

            processor.start();
            System.out.println("Receiving messages from subscription...");
            if (!latch.await(30, TimeUnit.SECONDS)) {
                System.out.println("Timed out waiting for subscription messages.");
            }
            processor.stop();
            System.out.println("Stopped receiving from subscription.");
        } catch (Exception ex) {
            System.err.println("Error receiving messages from subscription: " + ex.getMessage());
        }
    }

    private static void sendMessagesToSessionQueue(String namespace, String sessionQueueName, TokenCredential credential) {
        try (ServiceBusSenderClient sender = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .sender()
                .queueName(sessionQueueName)
                .buildClient()) {

            ServiceBusMessage msg1 = new ServiceBusMessage("Session message 1")
                    .setSessionId("session-1")
                    .setApplicationProperties(Map.of("Client", "A"));
            ServiceBusMessage msg2 = new ServiceBusMessage("Session message 2")
                    .setSessionId("session-2")
                    .setApplicationProperties(Map.of("Client", "B"));

            sender.sendMessages(Arrays.asList(msg1, msg2));
            System.out.println("Sent messages to session queue: " + sessionQueueName);
        } catch (Exception ex) {
            System.err.println("Error sending messages to session queue: " + ex.getMessage());
        }
    }

    private static void receiveMessagesFromSessionQueue(String namespace, String sessionQueueName, TokenCredential credential) throws InterruptedException {
        CountDownLatch latch = new CountDownLatch(2);

        Consumer<ServiceBusReceivedMessageContext> processMessage = context -> {
            ServiceBusReceivedMessage message = context.getMessage();
            System.out.printf("Session Message Received: SessionId=%s, MessageId=%s, Body=%s%n",
                    message.getSessionId(),
                    message.getMessageId(),
                    message.getBody().toString());
            try {
                context.complete();
            } catch (Exception e) {
                System.err.println("Failed to complete session message: " + e.getMessage());
            }
            latch.countDown();
        };

        Consumer<ServiceBusErrorContext> processError = errorContext -> {
            System.err.println("Error in session processing: " + errorContext.getException());
        };

        try (ServiceBusProcessorClient sessionProcessor = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .sessionProcessor()
                .queueName(sessionQueueName)
                .maxConcurrentSessions(MAX_CONCURRENT_SESSIONS)
                .receiveMode(ServiceBusReceiveMode.PEEK_LOCK)
                .disableAutoComplete()
                .processMessage(processMessage)
                .processError(processError)
                .buildProcessorClient()) {

            sessionProcessor.start();
            System.out.println("Receiving messages from session queue...");
            if (!latch.await(30, TimeUnit.SECONDS)) {
                System.out.println("Timed out waiting for session messages.");
            }
            sessionProcessor.stop();
            System.out.println("Stopped receiving from session queue.");
        } catch (Exception ex) {
            System.err.println("Error receiving messages from session queue: " + ex.getMessage());
        }
    }

    private static void receiveMessagesFromDeadLetterQueue(String namespace, String queueName, TokenCredential credential) {
        try (ServiceBusReceiverClient receiver = new ServiceBusClientBuilder()
                .credential(namespace, credential)
                .receiver()
                .queueName(queueName)
                .subQueue(SubQueue.DEAD_LETTER_QUEUE)
                .buildClient()) {

            System.out.println("Receiving dead letter messages from queue...");
            Iterable<ServiceBusReceivedMessage> deadLetterMessages = receiver.receiveMessages(10);
            for (ServiceBusReceivedMessage message : deadLetterMessages) {
                System.out.printf("Dead Letter Message: messageId=%s, body=%s, deadLetterReason=%s, deadLetterErrorDescription=%s%n",
                        message.getMessageId(),
                        message.getBody().toString(),
                        message.getDeadLetterReason(),
                        message.getDeadLetterErrorDescription());
            }
            System.out.println("Finished processing dead letter messages.");
        } catch (Exception ex) {
            System.err.println("Error receiving dead letter messages: " + ex.getMessage());
        }
    }
}