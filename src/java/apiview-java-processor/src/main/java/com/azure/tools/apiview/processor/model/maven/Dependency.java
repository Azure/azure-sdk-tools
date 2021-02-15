package com.azure.tools.apiview.processor.model.maven;

public class Dependency implements MavenGAV {
    private String groupId;
    private String artifactId;
    private String version;
    private String scope = "";

    public Dependency(String groupId, String artifactId, String version, String scope) {
        this.groupId = groupId;
        this.artifactId = artifactId;
        this.version = version;
        this.scope = scope;
    }

    @Override
    public String getGroupId() {
        return groupId;
    }

    @Override
    public String getArtifactId() {
        return artifactId;
    }

    @Override
    public String getVersion() {
        return version;
    }

    public String getScope() {
        return scope;
    }
}
