import com.azure.core.credential.TokenCredential;
import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.models.BlobItem;
import com.azure.storage.blob.models.BlobHttpHeaders;
import com.azure.storage.blob.models.PublicAccessType;
import com.azure.storage.blob.models.BlobRequestConditions;
import com.azure.storage.blob.models.BlobTier;
import com.azure.storage.blob.specialized.BlobLeaseClient;
import com.azure.storage.blob.specialized.BlobLeaseClientBuilder;
import com.azure.storage.blob.options.BlobUploadOptions;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.Path;
import java.nio.charset.StandardCharsets;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.Locale;
import java.util.Map;
import java.util.HashMap;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Java sample demonstrating comprehensive Azure Blob Storage operations
 * using Azure Storage Blob client library for Java.
 */
public class AzureBlobStorageJavaSampleExample {
    private static final Logger LOGGER = LoggerFactory.getLogger(AzureBlobStorageJavaSampleExample.class);

    private static final String ENV_STORAGE_ACCOUNT_NAME = "AZURE_STORAGE_ACCOUNT_NAME";
    private static final String CONTAINER_NAME = "java-demo-container";
    private static final String LOCAL_FILE_NAME = "sample-document.txt";
    private static final String BLOB_NAME = "sample-document.txt";

    public static void main(String[] args) {
        try {
            String accountName = System.getenv(ENV_STORAGE_ACCOUNT_NAME);
            if (accountName == null || accountName.isEmpty()) {
                LOGGER.error("Environment variable {} is not set.", ENV_STORAGE_ACCOUNT_NAME);
                return;
            }

            TokenCredential tokenCredential = new DefaultAzureCredentialBuilder().build();

            String endpoint = String.format(Locale.ROOT, "https://%s.blob.core.windows.net", accountName);

            BlobServiceClient blobServiceClient = createBlobServiceClient(tokenCredential, endpoint);

            BlobContainerClient containerClient = createContainerIfNotExists(blobServiceClient, CONTAINER_NAME);
            Path localFilePath = createLocalTestFile(LOCAL_FILE_NAME);

            Map<String, String> metadata = createMetadata();
            uploadBlobWithMetadataAndContentType(containerClient, BLOB_NAME, localFilePath, metadata);

            Path downloadFilePath = Paths.get("downloaded-" + LOCAL_FILE_NAME);
            downloadBlob(containerClient, BLOB_NAME, downloadFilePath);

            listBlobs(containerClient);

            setBlobTier(containerClient.getBlobClient(BLOB_NAME));

            manageBlobLease(containerClient.getBlobClient(BLOB_NAME));

            conditionalBlobUpdate(containerClient.getBlobClient(BLOB_NAME));

            deleteBlobAndContainer(containerClient, BLOB_NAME);

            Files.deleteIfExists(localFilePath);
            Files.deleteIfExists(downloadFilePath);

            LOGGER.info("Azure Blob Storage Java sample completed successfully.");
        } catch (Exception ex) {
            LOGGER.error("Unexpected exception in main", ex);
        }
    }

    private static BlobServiceClient createBlobServiceClient(TokenCredential credential, String endpoint) {
        try {
            return new BlobServiceClientBuilder()
                .endpoint(endpoint)
                .credential(credential)
                .buildClient();
        } catch (Exception ex) {
            LOGGER.error("Failed to create BlobServiceClient with token credential", ex);
            throw ex;
        }
    }

    private static BlobContainerClient createContainerIfNotExists(BlobServiceClient serviceClient, String containerName) {
        try {
            BlobContainerClient containerClient = serviceClient.getBlobContainerClient(containerName);
            if (!containerClient.exists()) {
                containerClient.createWithResponse(null, PublicAccessType.CONTAINER, null, null);
                LOGGER.info("Created container '{}'.", containerName);
            } else {
                LOGGER.info("Container '{}' already exists.", containerName);
            }
            return containerClient;
        } catch (Exception ex) {
            LOGGER.error("Error creating/getting container.", ex);
            throw ex;
        }
    }

    private static Path createLocalTestFile(String fileName) throws IOException {
        Path path = Paths.get(fileName);
        String content = "Hello Azure Blob Storage from Java!";
        Files.writeString(path, content, StandardCharsets.UTF_8);
        LOGGER.info("Created local test file '{}' with sample content.", fileName);
        return path;
    }

    private static Map<String, String> createMetadata() {
        Map<String, String> metadata = new HashMap<>();
        metadata.put("author", "java-sample");
        metadata.put("purpose", "demo");
        metadata.put("timestamp", OffsetDateTime.now(ZoneOffset.UTC).toString());
        return metadata;
    }

    private static void uploadBlobWithMetadataAndContentType(BlobContainerClient containerClient, String blobName, Path filePath, Map<String, String> metadata) {
        BlobClient blobClient = containerClient.getBlobClient(blobName);
        try {
            String contentType = guessContentType(filePath);
            BlobHttpHeaders headers = new BlobHttpHeaders().setContentType(contentType);

            BlobUploadOptions options = new BlobUploadOptions()
                .setMetadata(metadata)
                .setHeaders(headers)
                .setOverwrite(true);

            blobClient.uploadFromFile(filePath.toString(), options);
            LOGGER.info("Uploaded blob '{}' with metadata and content type '{}'.", blobName, contentType);
        } catch (Exception ex) {
            LOGGER.error("Failed to upload blob.", ex);
        }
    }

    private static String guessContentType(Path filePath) {
        try {
            String contentType = Files.probeContentType(filePath);
            return contentType == null ? "application/octet-stream" : contentType;
        } catch (IOException e) {
            LOGGER.warn("Could not determine content type for file: {}. Default to application/octet-stream.", filePath, e);
            return "application/octet-stream";
        }
    }

    private static void downloadBlob(BlobContainerClient containerClient, String blobName, Path downloadPath) {
        BlobClient blobClient = containerClient.getBlobClient(blobName);
        try {
            blobClient.downloadToFile(downloadPath.toString(), true);
            LOGGER.info("Downloaded blob '{}' to '{}'.", blobName, downloadPath);
        } catch (Exception ex) {
            LOGGER.error("Failed to download blob.", ex);
        }
    }

    private static void listBlobs(BlobContainerClient containerClient) {
        LOGGER.info("Listing blobs in container '{}':", containerClient.getBlobContainerName());
        for (BlobItem blobItem : containerClient.listBlobs()) {
            LOGGER.info("Blob name: {}, Size: {}, Metadata: {}",
                blobItem.getName(),
                blobItem.getProperties().getContentLength(),
                blobItem.getMetadata());
        }
    }

    private static void setBlobTier(BlobClient blobClient) {
        try {
            LOGGER.info("Setting blob tier for '{}' to Cool.", blobClient.getBlobName());
            blobClient.setAccessTier(BlobTier.COOL);
            LOGGER.info("Blob tier set to Cool.");
        } catch (Exception ex) {
            LOGGER.error("Failed to set blob tier.", ex);
        }
    }

    private static void manageBlobLease(BlobClient blobClient) {
        try {
            BlobLeaseClient leaseClient = new BlobLeaseClientBuilder()
                .blobClient(blobClient)
                .buildClient();

            LOGGER.info("Acquiring lease for blob '{}' for 15 seconds.", blobClient.getBlobName());
            String leaseId = leaseClient.acquireLease(15);
            LOGGER.info("Lease acquired. Lease ID: {}", leaseId);

            var properties = blobClient.getProperties();
            LOGGER.info("Lease status: {}, Lease state: {}, Lease duration: {}",
                properties.getLeaseStatus(), properties.getLeaseState(), properties.getLeaseDuration());

            LOGGER.info("Releasing lease.");
            leaseClient.releaseLease();
            LOGGER.info("Lease released.");
        } catch (Exception ex) {
            LOGGER.error("Failed to manage blob lease.", ex);
        }
    }

    private static void conditionalBlobUpdate(BlobClient blobClient) {
        try {
            var properties = blobClient.getProperties();
            String eTag = properties.getETag();
            LOGGER.info("Blob ETag for conditional update: {}", eTag);

            String newContent = "Updated content for concurrency demonstration.";
            byte[] contentBytes = newContent.getBytes(StandardCharsets.UTF_8);

            BlobRequestConditions requestConditions = new BlobRequestConditions().setIfMatch(eTag);
            BlobUploadOptions options = new BlobUploadOptions()
                .setRequestConditions(requestConditions)
                .setHeaders(new BlobHttpHeaders().setContentType("text/plain"))
                .setOverwrite(true);

            try (java.io.InputStream dataStream = new java.io.ByteArrayInputStream(contentBytes)) {
                blobClient.upload(dataStream, contentBytes.length, true);
                LOGGER.info("Conditional update succeeded using ETag match.");
            }
        } catch (Exception ex) {
            LOGGER.error("Conditional update failed. The blob may have been modified by another process.", ex);
        }
    }

    private static void deleteBlobAndContainer(BlobContainerClient containerClient, String blobName) {
        try {
            BlobClient blobClient = containerClient.getBlobClient(blobName);
            if (blobClient.exists()) {
                blobClient.delete();
                LOGGER.info("Deleted blob '{}'.", blobName);
            } else {
                LOGGER.info("Blob '{}' does not exist.", blobName);
            }
            containerClient.delete();
            LOGGER.info("Deleted container '{}'.", containerClient.getBlobContainerName());
        } catch (Exception ex) {
            LOGGER.error("Failed to delete blob or container.", ex);
        }
    }
}