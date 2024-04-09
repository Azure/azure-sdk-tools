package com.azure.tools.apiview.processor.model;

import com.azure.tools.apiview.processor.model.maven.Dependency;
import com.fasterxml.jackson.annotation.JsonProperty;

public enum Flavor {
    @JsonProperty("azure")
    AZURE("com.azure"),

    @JsonProperty("generic")
    GENERIC("io.clientcore"),

    UNKNOWN(null);

    private final String packagePrefix;

    private Flavor(String packagePrefix) {
        this.packagePrefix = packagePrefix;
    }

    public String getPackagePrefix() {
        return packagePrefix;
    }

    public static Flavor getFlavor(APIListing apiListing) {
        Flavor flavor = apiListing.getApiViewProperties().getFlavor();
        if (flavor != null) {
            return flavor;
        }

        // we are reviewing a library that does not have a flavor metadata in its apiview_properties.json file,
        // so we will use alternate means to determine the flavor.

        // Firstly, check the package name - does it start with one of the known package prefixes?
        if (apiListing.getPackageName().startsWith(AZURE.getPackagePrefix())) {
            return AZURE;
        } else if (apiListing.getPackageName().startsWith(GENERIC.getPackagePrefix())) {
            return GENERIC;
        }

        // we still don't know the flavor, so the next thing we can do is look at the dependencies of the library
        // to see if it brings in com.azure or io.clientcore libraries.
        int azureCount = 0;
        int genericCount = 0;
        for (Dependency dependency : apiListing.getMavenPom().getDependencies()) {
            if (dependency.getGroupId().equals(AZURE.getPackagePrefix())) {
                // if we have azure-core, then we are an azure library and we bail
                if (dependency.getArtifactId().equals("azure-core")) {
                    return AZURE;
                }
                azureCount++;
            } else if (dependency.getGroupId().equals(GENERIC.getPackagePrefix())) {
                // if we have 'core', then we are a clientcore library and we bail
                if (dependency.getArtifactId().equals("core")) {
                    return GENERIC;
                }
                genericCount++;
            }
        }

        // see which count is greatest (and non-zero), and return that flavour. If equal, return unknown
        return azureCount > genericCount ? AZURE : genericCount > azureCount ? GENERIC : UNKNOWN;
    }


}