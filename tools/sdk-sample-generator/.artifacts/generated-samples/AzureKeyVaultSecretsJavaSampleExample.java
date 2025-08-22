import com.azure.core.credential.TokenCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.security.keyvault.secrets.SecretClient;
import com.azure.security.keyvault.secrets.SecretClientBuilder;
import com.azure.security.keyvault.secrets.models.KeyVaultSecret;
import com.azure.security.keyvault.secrets.models.SecretProperties;
import com.azure.security.keyvault.secrets.models.DeletedSecret;
import com.azure.core.util.polling.SyncPoller;
import com.azure.core.exception.ResourceNotFoundException;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.HashMap;

public class AzureKeyVaultSecretsJavaSampleExample {

    public static void main(String[] args) {
        String keyVaultUrl = System.getenv("AZURE_KEY_VAULT_URL");
        if (keyVaultUrl == null || keyVaultUrl.isEmpty()) {
            System.err.println("Please set the AZURE_KEY_VAULT_URL environment variable.");
            System.exit(1);
        }

        TokenCredential credential = new DefaultAzureCredentialBuilder().build();

        SecretClient secretClient = new SecretClientBuilder()
            .vaultUrl(keyVaultUrl)
            .credential(credential)
            .buildClient();

        try {
            // 1. Create secrets with metadata
            KeyVaultSecret secret1 = createSecret(secretClient, "database-connection-string", "Server=myserver;Database=mydb;User Id=myuser;Password=mypassword;");
            createSecretWithMetadata(secretClient, "api-key-service-a", "apikey123456", Map.of("environment", "production", "owner", "team-a"));

            // 2. Get secret values
            getSecret(secretClient, "database-connection-string");

            // 3. List all secrets
            listSecrets(secretClient);

            // 4. Update secret metadata
            updateSecretMetadata(secretClient, "api-key-service-a");

            // 5. Secret Versioning
            createSecretVersion(secretClient, "database-connection-string", "Server=myserver;Database=mydb;User Id=myuser;Password=newpassword;");
            listSecretVersions(secretClient, "database-connection-string");
            getSecretVersion(secretClient, "database-connection-string", secret1.getProperties().getVersion());

            // 6. Advanced Operations
            byte[] backup = backupSecret(secretClient, "database-connection-string");
            deleteSecret(secretClient, "database-connection-string");

            // Recover secret
            recoverDeletedSecret(secretClient, "database-connection-string");

            // Purge secret
            purgeDeletedSecret(secretClient, "database-connection-string");

            // Restore secret
            restoreSecret(secretClient, backup);

            // 7. Cleanup
            deleteSecret(secretClient, "api-key-service-a");
            purgeDeletedSecret(secretClient, "api-key-service-a");

        } catch (ResourceNotFoundException e) {
            System.err.println("Resource not found: " + e.getMessage());
        } catch (InterruptedException e) {
            System.err.println("Operation interrupted: " + e.getMessage());
            Thread.currentThread().interrupt();
        } catch (Exception e) {
            System.err.println("Unexpected exception: " + e.getMessage());
        }
    }

    private static KeyVaultSecret createSecret(SecretClient client, String name, String value) {
        System.out.printf("Creating secret '%s' with value '%s'%n", name, value);
        KeyVaultSecret secret = client.setSecret(name, value);
        System.out.printf("Created secret: Name=%s Value=%s Version=%s%n", secret.getName(), secret.getValue(), secret.getProperties().getVersion());
        return secret;
    }

    private static KeyVaultSecret createSecretWithMetadata(SecretClient client, String name, String value, Map<String, String> tags) {
        System.out.printf("Creating secret '%s' with value '%s' and tags %s%n", name, value, tags.toString());
        KeyVaultSecret secret = new KeyVaultSecret(name, value);
        secret.getProperties().setExpiresOn(OffsetDateTime.now().plusMonths(6));
        secret.getProperties().setNotBefore(OffsetDateTime.now());
        secret.getProperties().setEnabled(true);
        secret.getProperties().setContentType("text/plain");
        secret.getProperties().setTags(tags);
        return client.setSecret(secret);
    }

    private static KeyVaultSecret getSecret(SecretClient client, String name) {
        System.out.printf("Retrieving secret '%s'%n", name);
        KeyVaultSecret secret = client.getSecret(name);
        System.out.printf("Retrieved secret: Name=%s Value=%s Version=%s%n", secret.getName(), secret.getValue(), secret.getProperties().getVersion());
        return secret;
    }

    private static void listSecrets(SecretClient client) {
        System.out.println("Listing all secrets:");
        for (SecretProperties prop : client.listPropertiesOfSecrets()) {
            try {
                KeyVaultSecret secret = client.getSecret(prop.getName(), prop.getVersion());
                System.out.printf("- Name: %s, Version: %s, Enabled: %s, ExpiresOn: %s, Tags: %s%n",
                    secret.getName(), secret.getProperties().getVersion(), secret.getProperties().isEnabled(), secret.getProperties().getExpiresOn(), secret.getProperties().getTags());
            } catch (ResourceNotFoundException e) {
                System.err.printf("Secret '%s' version '%s' not found.%n", prop.getName(), prop.getVersion());
            }
        }
    }

    private static void updateSecretMetadata(SecretClient client, String name) {
        System.out.printf("Updating secret metadata for '%s'%n", name);
        KeyVaultSecret secret = client.getSecret(name);
        SecretProperties props = secret.getProperties();
        props.setExpiresOn(OffsetDateTime.now().plusMonths(12));
        props.setEnabled(false);
        if (props.getTags() == null) props.setTags(new HashMap<>());
        Map<String, String> tags = props.getTags();
        tags.put("updated", String.valueOf(System.currentTimeMillis()));
        tags.put("updatedBy", "sampleApp");
        props.setContentType("application/json");
        client.updateSecretProperties(props);
        System.out.printf("Updated secret metadata for '%s': Enabled=%s ExpiresOn=%s Tags=%s ContentType=%s%n",
            name, props.isEnabled(), props.getExpiresOn(), props.getTags(), props.getContentType());
    }

    private static KeyVaultSecret createSecretVersion(SecretClient client, String name, String newValue) {
        System.out.printf("Creating new version of secret '%s' with new value '%s'%n", name, newValue);
        KeyVaultSecret secret = client.setSecret(name, newValue);
        System.out.printf("New version created: Name=%s Version=%s%n", secret.getName(), secret.getProperties().getVersion());
        return secret;
    }

    private static void listSecretVersions(SecretClient client, String name) {
        System.out.printf("Listing versions for secret '%s'%n", name);
        for (SecretProperties version : client.listPropertiesOfSecretVersions(name)) {
            System.out.printf("- Version: %s Enabled: %s ExpiresOn: %s Tags: %s%n",
                version.getVersion(), version.isEnabled(), version.getExpiresOn(), version.getTags());
        }
    }

    private static KeyVaultSecret getSecretVersion(SecretClient client, String name, String version) {
        System.out.printf("Getting secret '%s' version '%s'%n", name, version);
        KeyVaultSecret secret = client.getSecret(name, version);
        System.out.printf("Retrieved secret version: Name=%s Version=%s Value=%s%n",
            secret.getName(), secret.getProperties().getVersion(), secret.getValue());
        return secret;
    }

    private static byte[] backupSecret(SecretClient client, String name) {
        System.out.printf("Backing up secret '%s'%n", name);
        byte[] backup = client.backupSecret(name);
        System.out.printf("Backup size: %d bytes%n", backup.length);
        return backup;
    }

    private static void restoreSecret(SecretClient client, byte[] backup) {
        System.out.println("Restoring secret from backup");
        KeyVaultSecret secret = client.restoreSecretBackup(backup);
        System.out.printf("Restored secret: Name=%s Version=%s%n", secret.getName(), secret.getProperties().getVersion());
    }

    private static void deleteSecret(SecretClient client, String name) {
        System.out.printf("Deleting secret '%s'%n", name);
        SyncPoller<DeletedSecret, Void> deletePoller = client.beginDeleteSecret(name);
        deletePoller.waitForCompletion();
        System.out.printf("Deleted secret '%s'%n", name);
    }

    private static void recoverDeletedSecret(SecretClient client, String name) {
        System.out.printf("Recovering deleted secret '%s'%n", name);
        SyncPoller<KeyVaultSecret, Void> recoverPoller = client.beginRecoverDeletedSecret(name);
        recoverPoller.waitForCompletion();
        System.out.printf("Recovered deleted secret '%s'%n", name);
    }

    private static void purgeDeletedSecret(SecretClient client, String name) {
        System.out.printf("Purging deleted secret '%s'%n", name);
        client.purgeDeletedSecret(name);
        System.out.printf("Purged deleted secret '%s'%n", name);
    }

}