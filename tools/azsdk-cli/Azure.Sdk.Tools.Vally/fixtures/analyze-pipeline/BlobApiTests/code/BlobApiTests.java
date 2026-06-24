// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// FIXTURE (condensed) — azure-sdk-for-java build 6449182, PR #49365.
// Real failing test: BlobApiTests.uploadStreamAccessTierSmart fails in playback
// because its test-proxy session recording is missing (404 NotFound). This is a
// faithful reduction of the captured BlobApiTests.java: the SMART-access-tier
// test and its scaffolding are kept verbatim; the ~3,200 lines of unrelated
// blob tests are elided. Used by pipeline-analysis-java (missing-recording
// analysis) and pipeline-fixer-java (unfixable / re-record classification).

package com.azure.storage.blob;

import com.azure.core.http.rest.Response;
import com.azure.core.test.annotation.RequiredServiceVersion;
import com.azure.core.util.Context;
import com.azure.storage.blob.models.AccessTier;
import com.azure.storage.blob.models.BlobProperties;
import com.azure.storage.blob.options.BlobParallelUploadOptions;
import com.azure.storage.common.implementation.Constants;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

public class BlobApiTests extends BlobTestBase {
    private BlobClient bc;
    private final List<File> createdFiles = new ArrayList<>();

    @BeforeEach
    public void setup() {
        String blobName = generateBlobName();
        bc = cc.getBlobClient(blobName);
        bc.getBlockBlobClient().upload(DATA.getDefaultInputStream(), DATA.getDefaultDataSize());
    }

    @AfterEach
    public void cleanup() {
        createdFiles.forEach(File::delete);
    }

    // ... (unrelated blob API tests elided for fixture brevity) ...

    @RequiredServiceVersion(clazz = BlobServiceVersion.class, min = "2026-02-06")
    @Test
    public void uploadStreamAccessTierSmart() {
        bc = cc.getBlobClient(generateBlobName());
        InputStream data = new ByteArrayInputStream(getRandomByteArray(Constants.KB));

        BlobParallelUploadOptions options = new BlobParallelUploadOptions(data).setTier(AccessTier.SMART);
        bc.uploadWithResponse(options, null, Context.NONE);

        Response<BlobProperties> response = bc.getPropertiesWithResponse(null, null, Context.NONE);
        assertEquals(AccessTier.SMART, response.getValue().getAccessTier());
        assertNotNull(response.getValue().getSmartAccessTier());
    }

}
