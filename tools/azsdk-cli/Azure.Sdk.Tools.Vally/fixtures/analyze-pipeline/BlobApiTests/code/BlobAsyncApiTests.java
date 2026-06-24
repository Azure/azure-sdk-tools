// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// FIXTURE (condensed) — azure-sdk-for-java build 6449182, PR #49365.
// Real failing test: BlobAsyncApiTests.uploadStreamAccessTierSmart fails in
// playback because its test-proxy session recording is missing (404 NotFound).
// Faithful reduction of the captured BlobAsyncApiTests.java: the SMART-access-
// tier test and scaffolding are kept verbatim; ~3,000 lines of unrelated async
// blob tests are elided. Used by pipeline-analysis-java and pipeline-fixer-java.

package com.azure.storage.blob;

import com.azure.core.http.rest.Response;
import com.azure.core.test.annotation.RequiredServiceVersion;
import com.azure.storage.blob.models.AccessTier;
import com.azure.storage.blob.models.BlobProperties;
import com.azure.storage.blob.options.BlobParallelUploadOptions;

import reactor.core.publisher.Mono;
import reactor.test.StepVerifier;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

public class BlobAsyncApiTests extends BlobTestBase {
    private BlobAsyncClient bc;
    private final List<File> createdFiles = new ArrayList<>();

    @BeforeEach
    public void setup() {
        String blobName = generateBlobName();
        bc = ccAsync.getBlobAsyncClient(blobName);
        bc.getBlockBlobAsyncClient().upload(DATA.getDefaultFlux(), DATA.getDefaultDataSize()).block();
    }

    @AfterEach
    public void cleanup() {
        createdFiles.forEach(File::delete);
    }

    // ... (unrelated async blob API tests elided for fixture brevity) ...

    @RequiredServiceVersion(clazz = BlobServiceVersion.class, min = "2026-02-06")
    @Test
    public void uploadStreamAccessTierSmart() {
        bc = ccAsync.getBlobAsyncClient(generateBlobName());
        BlobParallelUploadOptions options
            = new BlobParallelUploadOptions(DATA.getDefaultFlux()).setTier(AccessTier.SMART);
        Mono<Response<BlobProperties>> response
            = bc.uploadWithResponse(options).then(bc.getPropertiesWithResponse(null));

        StepVerifier.create(response).assertNext(r -> {
            assertEquals(AccessTier.SMART, r.getValue().getAccessTier());
            assertNotNull(r.getValue().getSmartAccessTier());
        }).verifyComplete();
    }

}
