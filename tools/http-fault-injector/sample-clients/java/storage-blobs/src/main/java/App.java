// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import com.azure.core.util.BinaryData;
import com.azure.core.util.Configuration;
import com.azure.storage.blob.BlobClient;
import com.azure.storage.blob.BlobClientBuilder;
import com.azure.storage.common.policy.RequestRetryOptions;
import com.azure.storage.common.policy.RetryPolicyType;

import java.time.Duration;

public class App {
    public static void main(String[] args) {
        // You must either add the .NET developer certiifcate to the Java cacerts keystore, or uncomment the following
        // lines to disable SSL validation.
        //
        // io.netty.handler.ssl.SslContext sslContext = io.netty.handler.ssl.SslContextBuilder
        //     .forClient().trustManager(io.netty.handler.ssl.util.InsecureTrustManagerFactory.INSTANCE).build();
        // httpClient = httpClient.secure(sslContextBuilder -> sslContextBuilder.sslContext(sslContext));

        BlobClient blobClient = new BlobClientBuilder()
            .connectionString(Configuration.getGlobalConfiguration().get("STORAGE_CONNECTION_STRING"))
            .containerName("sample")
            .blobName("sample.txt")
            .retryOptions(new RequestRetryOptions(RetryPolicyType.FIXED, 3, Duration.ofMinutes(1),
                Duration.ofSeconds(1), Duration.ofSeconds(1), null))
            .addPolicy(new FaultInjectorUrlRewriterPolicy())
            .buildClient();

        System.out.println("Sending request...");
        BinaryData content = blobClient.downloadContent();
        System.out.printf("Content: %s%n", content);
    }
}
