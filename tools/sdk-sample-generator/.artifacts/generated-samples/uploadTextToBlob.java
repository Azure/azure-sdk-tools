import com.azure.identity.DefaultAzureCredentialBuilder;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobContainerClient;
import com.azure.storage.blob.BlobServiceClient;
import com.azure.storage.blob.BlobServiceClientBuilder;

public class uploadTextToBlob {
    public static void main(String[] args) {
        String accountUrl = System.getenv("STORAGE_ACCOUNT_URL");
        if (accountUrl == null) {
            System.err.println("Environment variable STORAGE_ACCOUNT_URL is not set.");
            System.exit(1);
        }

        BlobServiceClient blobServiceClient = new BlobServiceClientBuilder()
            .endpoint(accountUrl)
            .credential(new DefaultAzureCredentialBuilder().build())
            .buildClient();

        BlobContainerClient containerClient = blobServiceClient.getBlobContainerClient("testcontainer");
        BlobClient blobClient = containerClient.getBlobClient("sampleblob.txt");

        String data = "Hello Azure Blob!";
        blobClient.upload(com.azure.core.util.BinaryData.fromString(data), true);

        System.out.println("Uploaded blob 'sampleblob.txt' to container 'testcontainer'.");
    }
}