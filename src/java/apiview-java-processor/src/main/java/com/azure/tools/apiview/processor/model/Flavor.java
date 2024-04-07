package com.azure.tools.apiview.processor.model;

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
        if (apiListing.getPackageName().startsWith(AZURE.packagePrefix)) {
            return AZURE;
        } else if (apiListing.getPackageName().startsWith(GENERIC.packagePrefix)) {
            return GENERIC;
        }

        // TODO we still don't know the flavor, so the next thing we can do is look at the dependencies of the library
        // to see if it brings in com.azure or io.clientcore libraries.

        // we've failed - return the unknown flavor
        return UNKNOWN;
    }


}