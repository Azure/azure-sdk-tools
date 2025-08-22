import com.azure.cosmos.CosmosClient;
import com.azure.cosmos.CosmosClientBuilder;
import com.azure.cosmos.CosmosContainer;
import com.azure.cosmos.CosmosDatabase;
import com.azure.cosmos.CosmosException;
import com.azure.cosmos.PartitionKey;
import com.azure.cosmos.models.CosmosContainerProperties;
import com.azure.cosmos.models.CosmosDatabaseResponse;
import com.azure.cosmos.models.CosmosItemResponse;
import com.azure.cosmos.models.CosmosQueryRequestOptions;
import com.azure.cosmos.models.CosmosPagedIterable;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.logging.Level;
import java.util.logging.Logger;

public class AzureCosmosDbJavaSampleExample {

    private static final Logger LOGGER = Logger.getLogger(AzureCosmosDbJavaSampleExample.class.getName());

    private static CosmosClient client;
    private static CosmosDatabase database;
    private static CosmosContainer container;

    private static final String DATABASE_NAME = "UserDatabase";
    private static final String CONTAINER_NAME = "Users";
    private static final String PARTITION_KEY_PATH = "/userId";

    public static void main(String[] args) {
        try {
            String endpoint = System.getenv("AZURE_COSMOS_ENDPOINT");
            if (endpoint == null || endpoint.isBlank()) {
                LOGGER.severe("Environment variable AZURE_COSMOS_ENDPOINT is not set or empty.");
                return;
            }

            client = new CosmosClientBuilder()
                    .endpoint(endpoint)
                    .credential(new DefaultAzureCredentialBuilder().build())
                    .buildClient();

            createDatabaseIfNotExists(DATABASE_NAME);
            createContainerIfNotExists(CONTAINER_NAME, PARTITION_KEY_PATH);

            listDatabases();

            User user = new User();
            user.setId(UUID.randomUUID().toString());
            user.setUserId("user123");
            user.setName("John Doe");
            user.setEmail("john.doe@example.com");
            user.setCreatedDate(Instant.now().toString());

            createUser(user);

            User readUser = readUser(user.getId(), new PartitionKey(user.getUserId()));
            if (readUser != null) {
                readUser.setName("John A. Doe");
                updateUser(readUser);
            }

            upsertUser(user);

            queryUsers("SELECT * FROM c WHERE c.userId = @userId", "user123");

            deleteUser(user.getId(), new PartitionKey(user.getUserId()));

        } catch (CosmosException e) {
            LOGGER.log(Level.SEVERE, "CosmosException occurred: " + e.getStatusCode() + " " + e.getMessage(), e);
        } catch (Exception e) {
            LOGGER.log(Level.SEVERE, "Exception occurred: " + e.getMessage(), e);
        } finally {
            if (client != null) {
                client.close();
            }
        }
    }

    private static void createDatabaseIfNotExists(String databaseName) {
        CosmosDatabaseResponse response = client.createDatabaseIfNotExists(databaseName);
        database = client.getDatabase(response.getProperties().getId());
        LOGGER.info("Created or accessed database: " + database.getId());
    }

    private static void createContainerIfNotExists(String containerName, String partitionKeyPath) {
        CosmosContainerProperties containerProperties = new CosmosContainerProperties(containerName, partitionKeyPath);
        database.createContainerIfNotExists(containerProperties);
        container = database.getContainer(containerName);
        LOGGER.info("Created or accessed container: " + container.getId());
    }

    private static void listDatabases() {
        LOGGER.info("Listing databases:");
        client.readAllDatabases().iterableByPage().forEach(page -> {
            page.getResults().forEach(dbProperties -> LOGGER.info("- " + dbProperties.getId()));
        });
    }

    private static void createUser(User user) {
        CosmosItemResponse<User> response = container.createItem(user);
        LOGGER.info("Created user with id: " + response.getItem().getId());
    }

    private static User readUser(String id, PartitionKey partitionKey) {
        try {
            CosmosItemResponse<User> response = container.readItem(id, partitionKey, User.class);
            LOGGER.info("Read user with id: " + response.getItem().getId());
            return response.getItem();
        } catch (CosmosException e) {
            if (e.getStatusCode() == 404) {
                LOGGER.warning("User with id " + id + " not found.");
            } else {
                throw e;
            }
            return null;
        }
    }

    private static void updateUser(User user) {
        CosmosItemResponse<User> response = container.replaceItem(user, user.getId(), new PartitionKey(user.getUserId()));
        LOGGER.info("Updated user with id: " + response.getItem().getId());
    }

    private static void upsertUser(User user) {
        CosmosItemResponse<User> response = container.upsertItem(user);
        LOGGER.info("Upserted user with id: " + response.getItem().getId());
    }

    private static void deleteUser(String id, PartitionKey partitionKey) {
        container.deleteItem(id, partitionKey);
        LOGGER.info("Deleted user with id: " + id);
    }

    private static void queryUsers(String query, String userId) {
        CosmosQueryRequestOptions options = new CosmosQueryRequestOptions();
        options.setPartitionKey(new PartitionKey(userId));
        CosmosPagedIterable<User> pagedIterable = container.queryItems(query, options, User.class);
        List<User> users = new ArrayList<>();
        pagedIterable.forEach(users::add);
        LOGGER.info("Query returned " + users.size() + " users.");
        for (User user : users) {
            LOGGER.info("User: " + user.getId() + ", " + user.getName() + ", " + user.getEmail());
        }
    }

    public static class User {
        private String id;
        private String userId;
        private String name;
        private String email;
        private String createdDate;

        public User() {}

        public String getId() {
            return id;
        }

        public void setId(String id) {
            this.id = id;
        }

        public String getUserId() {
            return userId;
        }

        public void setUserId(String userId) {
            this.userId = userId;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public String getEmail() {
            return email;
        }

        public void setEmail(String email) {
            this.email = email;
        }

        public String getCreatedDate() {
            return createdDate;
        }

        public void setCreatedDate(String createdDate) {
            this.createdDate = createdDate;
        }
    }

}