```java
maven { 
    parent : com.azure:azure-client-sdk-parent:1.7.0 
    properties : com.azure:azure-core:1.57.0 
    configuration { 
        jacoco { 
            min-line-coverage : 0.6 
            min-branch-coverage : 0.6 
        } 
    } 
    name : Microsoft Azure Java Core Library 
    description : This package contains core types for Azure Java clients. 
    dependencies { 
        // compile scope 
        com.azure:azure-json 1.5.0 
        com.azure:azure-xml 1.2.0 
        com.fasterxml.jackson.core:jackson-annotations 2.18.4 
        com.fasterxml.jackson.core:jackson-core 2.18.4.1 
        com.fasterxml.jackson.core:jackson-databind 2.18.4 
        com.fasterxml.jackson.datatype:jackson-datatype-jsr310 2.18.4 
        org.slf4j:slf4j-api 1.7.36 
        io.projectreactor:reactor-core 3.7.11 
        // provided scope 
        com.google.code.findbugs:jsr305 3.0.2 
    } 
} 
module com.azure.core { 
    requires transitive com.azure.json;
    requires transitive com.azure.xml;
    requires transitive reactor.core;
    requires transitive org.reactivestreams;
    requires transitive org.slf4j;
    requires transitive com.fasterxml.jackson.annotation;
    requires transitive com.fasterxml.jackson.core;
    requires transitive com.fasterxml.jackson.databind;
    requires transitive com.fasterxml.jackson.datatype.jsr310;
    exports com.azure.core.annotation;
    exports com.azure.core.client.traits;
    exports com.azure.core.credential;
    exports com.azure.core.cryptography;
    exports com.azure.core.exception;
    exports com.azure.core.http;
    exports com.azure.core.http.policy;
    exports com.azure.core.http.rest;
    exports com.azure.core.models;
    exports com.azure.core.util;
    exports com.azure.core.util.builder;
    exports com.azure.core.util.io;
    exports com.azure.core.util.logging;
    exports com.azure.core.util.paging;
    exports com.azure.core.util.polling;
    exports com.azure.core.util.serializer;
    exports com.azure.core.util.tracing;
    exports com.azure.core.util.metrics;
    exports com.azure.core.implementation to com.azure.core.serializer.json.jackson, com.azure.core.serializer.json.gson, com.azure.core.experimental, com.azure.core.http.vertx;
    exports com.azure.core.implementation.jackson to com.azure.core.management, com.azure.core.serializer.json.jackson;
    exports com.azure.core.implementation.util to com.azure.http.netty, com.azure.core.http.okhttp, com.azure.core.http.jdk.httpclient, com.azure.core.http.vertx, com.azure.core.serializer.json.jackson;
    exports com.azure.core.util.polling.implementation to com.azure.core.experimental;
    opens com.azure.core.credential to com.fasterxml.jackson.databind;
    opens com.azure.core.http to com.fasterxml.jackson.databind;
    opens com.azure.core.models to com.fasterxml.jackson.databind;
    opens com.azure.core.util to com.fasterxml.jackson.databind;
    opens com.azure.core.util.logging to com.fasterxml.jackson.databind;
    opens com.azure.core.util.polling to com.fasterxml.jackson.databind;
    opens com.azure.core.util.polling.implementation to com.fasterxml.jackson.databind;
    opens com.azure.core.util.serializer to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation.logging to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation.serializer to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation.jackson to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation.util to com.fasterxml.jackson.databind;
    opens com.azure.core.implementation.http.rest to com.fasterxml.jackson.databind;
    opens com.azure.core.http.rest to com.fasterxml.jackson.databind;
    uses com.azure.core.http.HttpClientProvider;
    uses com.azure.core.http.policy.BeforeRetryPolicyProvider;
    uses com.azure.core.http.policy.AfterRetryPolicyProvider;
    uses com.azure.core.util.serializer.JsonSerializerProvider;
    uses com.azure.core.util.serializer.MemberNameConverterProvider;
    uses com.azure.core.util.tracing.Tracer;
    uses com.azure.core.util.metrics.MeterProvider;
    uses com.azure.core.util.tracing.TracerProvider;
} 
/** 
 * <p>This package contains annotations for client-side methods that map to REST APIs in the Azure SDK.</p> 
 * 
 * <p>These annotations are used to define the HTTP method (GET, POST, PUT, DELETE, etc.) and the relative path for the 
 * REST endpoint. They also provide a way to specify path parameters, query parameters, and the request body.</p> 
 * 
 * <p>Here are some of the key annotations included in this package:</p> 
 * 
 * <ul> 
 *     <li>{@link com.azure.core.annotation.Get}: Annotation for HTTP GET method.</li> 
 *     <li>{@link com.azure.core.annotation.Put}: Annotation for HTTP PUT method.</li> 
 *     <li>{@link com.azure.core.annotation.QueryParam}: Annotation for query parameters to be appended to a REST API 
 *     Request URI.</li> 
 * </ul> 
 */ 
package com.azure.core.annotation { 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Annotation to annotate a parameter to send to a REST endpoint as HTTP Request content. 
     * 
     * <p> 
     * If the parameter type extends <code>InputStream</code>, this payload is streamed to server through 
     * "application/octet-stream". Otherwise, the body is serialized first and sent as "application/json" or 
     * "application/xml", based on the serializer. 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Put JSON</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.BodyParam.class1 --> 
     * <pre> 
     * @Put("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * VirtualMachine createOrUpdate(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") VirtualMachine vm); 
     * </pre> 
     * <!-- end com.azure.core.annotation.BodyParam.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Stream</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.BodyParam.class2 --> 
     * <pre> 
     * @Post("formdata/stream/uploadfile") 
     * void uploadFileViaBody(@BodyParam("application/octet-stream") FileInputStream fileContent); 
     * </pre> 
     * <!-- end com.azure.core.annotation.BodyParam.class2 --> 
     */ 
    public @annotation BodyParam { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP DELETE method annotation describing the parameterized relative path to a REST endpoint for resource deletion. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Delete.class1 --> 
     * <pre> 
     * @Delete("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * void delete(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Delete.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Delete.class2 --> 
     * <pre> 
     * @Delete("{vaultBaseUrl}/secrets/{secretName}") 
     * void delete(@PathParam(value = "vaultBaseUrl", encoded = true) String vaultBaseUrl, 
     *     @PathParam("secretName") String secretName); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Delete.class2 --> 
     */ 
    public @annotation Delete { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * Annotation to annotate list of HTTP status codes that are expected in response from a REST endpoint. 
     * 
     * <p> 
     * <strong>Example:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.ExpectedResponses.class --> 
     * <pre> 
     * @ExpectedResponses({200, 201}) 
     * @Post("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.CustomerInsights/" 
     *     + "hubs/{hubName}/images/getEntityTypeImageUploadUrl") 
     * void getUploadUrlForEntityType(@PathParam("resourceGroupName") String resourceGroupName, 
     *     @PathParam("hubName") String hubName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") RequestBody parameters); 
     * </pre> 
     * <!-- end com.azure.core.annotation.ExpectedResponses.class --> 
     */ 
    public @annotation ExpectedResponses { 
        int[] value()
    } 
    @Retention(SOURCE)
    @Target(TYPE)
    /** 
     * Annotation given to all classes that are expected to provide a fluent API to end users. If a class has this 
     * annotation, checks can be made to ensure all API meets this expectation. Similarly, classes that are not annotated 
     * with this annotation should not have fluent APIs. 
     */ 
    public @annotation Fluent { 
        // This annotation does not declare any members. 
    } 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Annotation for form parameters to be sent to a REST API Request URI. 
     * 
     * <p> 
     * <strong>Example:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.FormParam.class --> 
     * <pre> 
     * @Post("spellcheck") 
     * Mono<Response<ResponseBody>> spellChecker(@HeaderParam("X-BingApis-SDK") String xBingApisSDK, 
     *     @QueryParam("UserId") String userId, 
     *     @FormParam("Text") String text); 
     * </pre> 
     * <!-- end com.azure.core.annotation.FormParam.class --> 
     * 
     * <p> 
     * The value of parameter text will be encoded and encoded value will be added to the form data sent to the API. 
     * </p> 
     */ 
    public @annotation FormParam { 
        String value()
        boolean encoded() default false
    } 
    @Retention(SOURCE)
    @Target({ METHOD, CONSTRUCTOR, FIELD })
    /** 
     * Annotation given to all methods that are generated by AutoRest. This annotation is intended to be used by the code 
     * generation tool only to identify methods that are generated. The purpose of this annotation is to find and replace 
     * all methods in a class that are generated. Methods not annotated with this annotation will not be updated when code 
     * is regenerated. 
     * <p> 
     * This annotation is expected to be used in classes that are annotated with {@link ServiceClient} only. 
     * </p> 
     */ 
    public @annotation Generated { 
        // This annotation does not declare any members. 
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP GET method annotation describing the parameterized relative path to a REST endpoint for resource retrieval. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Get.class1 --> 
     * <pre> 
     * @Get("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * VirtualMachine getByResourceGroup(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Get.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Get.class2 --> 
     * <pre> 
     * @Get("{nextLink}") 
     * List<VirtualMachine> listNext(@PathParam("nextLink") String nextLink); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Get.class2 --> 
     */ 
    public @annotation Get { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP HEAD method annotation describing the parameterized relative path to a REST endpoint. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (otherwise the parse cannot tell if it's absolute or relative) 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Head.class1 --> 
     * <pre> 
     * @Head("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * boolean checkNameAvailability(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Head.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Head.class2 --> 
     * <pre> 
     * @Head("{storageAccountId}") 
     * boolean checkNameAvailability(@PathParam("storageAccountId") String storageAccountId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Head.class2 --> 
     */ 
    public @annotation Head { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(FIELD)
    /** 
     * Annotation on a deserialized header type that indicates that the property should be treated as a header collection 
     * with the provided prefix. 
     */ 
    public @annotation HeaderCollection { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Replaces the header with the value of its target. The value specified here replaces headers specified statically in 
     * the {@link Headers}. If the parameter this annotation is attached to is a Map type, then this will be treated as a 
     * header collection. In that case each of the entries in the argument's map will be individual header values that use 
     * the value of this annotation as a prefix to their key/header name. 
     * 
     * <p> 
     * <strong>Example 1:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HeaderParam.class1 --> 
     * <pre> 
     * @Put("{functionId}") 
     * Mono<ResponseBase<ResponseHeaders, ResponseBody>> createOrReplace( 
     *     @PathParam(value = "functionId", encoded = true) String functionId, 
     *     @BodyParam("application/json") RequestBody function, 
     *     @HeaderParam("If-Match") String ifMatch); 
     * 
     * // "If-Match: user passed value" will show up as one of the headers. 
     * </pre> 
     * <!-- end com.azure.core.annotation.HeaderParam.class1 --> 
     * 
     * <p> 
     * <strong>Example 2:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HeaderParam.class2 --> 
     * <pre> 
     * @Get("subscriptions/{subscriptionId}/providers/Microsoft.ServiceBus/namespaces") 
     * Mono<ResponseBase<ResponseHeaders, ResponseBody>> list(@PathParam("subscriptionId") String subscriptionId, 
     *     @HeaderParam("accept-language") String acceptLanguage, 
     *     @HeaderParam("User-Agent") String userAgent); 
     * 
     * // "accept-language" generated by the HTTP client will be overwritten by the user passed value. 
     * </pre> 
     * <!-- end com.azure.core.annotation.HeaderParam.class2 --> 
     * 
     * <p> 
     * <strong>Example 3:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HeaderParam.class3 --> 
     * <pre> 
     * @Get("subscriptions/{subscriptionId}/providers/Microsoft.ServiceBus/namespaces") 
     * Mono<ResponseBase<ResponseHeaders, ResponseBody>> list(@PathParam("subscriptionId") String subscriptionId, 
     *     @HeaderParam("Authorization") String token); 
     * 
     * // The token parameter will replace the effect of any credentials in the HttpPipeline. 
     * </pre> 
     * <!-- end com.azure.core.annotation.HeaderParam.class3 --> 
     * 
     * <p> 
     * <strong>Example 4:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HeaderParam.class4 --> 
     * <pre> 
     * @Put("{containerName}/{blob}") 
     * @ExpectedResponses({200}) 
     * Mono<ResponseBase<ResponseHeaders, Void>> setMetadata(@PathParam("containerName") String containerName, 
     *     @PathParam("blob") String blob, 
     *     @HeaderParam("x-ms-meta-") Map<String, String> metadata); 
     * 
     * // The metadata parameter will be expanded out so that each entry becomes 
     * // "x-ms-meta-{@literal <entryKey>}: {@literal <entryValue>}". 
     * </pre> 
     * <!-- end com.azure.core.annotation.HeaderParam.class4 --> 
     */ 
    public @annotation HeaderParam { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * Annotation to annotate list of static headers sent to a REST endpoint. 
     * 
     * <p> 
     * Headers are comma separated strings, with each in the format of "header name: header value1,header value2". 
     * </p> 
     * 
     * <p> 
     * <strong>Examples:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Headers.class --> 
     * <pre> 
     * @Headers({"Content-Type: application/json; charset=utf-8", "accept-language: en-US"}) 
     * @Post("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.CustomerInsights/" 
     *     + "hubs/{hubName}/images/getEntityTypeImageUploadUrl") 
     * void getUploadUrlForEntityType(@PathParam("resourceGroupName") String resourceGroupName, 
     *     @PathParam("hubName") String hubName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") RequestBody parameters); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Headers.class --> 
     */ 
    public @annotation Headers { 
        String[] value()
    } 
    @Retention(RUNTIME)
    @Target(TYPE)
    /** 
     * Annotation for parameterized host name targeting a REST service. 
     * 
     * <p> 
     * This is the 'host' field or 'x-ms-parameterized-host.hostTemplate' field in a Swagger document. parameters are 
     * enclosed in {}s, e.g. {accountName}. An HTTP client must accept the parameterized host as the base URL for the 
     * request, replacing the parameters during runtime with the actual values users provide. 
     * </p> 
     * 
     * <p> 
     * For parameterized hosts, parameters annotated with {@link HostParam} must be provided. See Java docs in 
     * {@link HostParam} for directions for host parameters. 
     * </p> 
     * 
     * <p> 
     * The host's value must contain the scheme/protocol and the host. The host's value may contain the 
     * port number. 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Static annotation</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Host.class1 --> 
     * <pre> 
     * @Host("https://management.azure.com") 
     * interface VirtualMachinesService { 
     *     @Get("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *         + "virtualMachines/{vmName}") 
     *     VirtualMachine getByResourceGroup(@PathParam("resourceGroupName") String rgName, 
     *         @PathParam("vmName") String vmName, 
     *         @PathParam("subscriptionId") String subscriptionId); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.annotation.Host.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Dynamic annotation</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Host.class2 --> 
     * <pre> 
     * @Host("https://{vaultName}.vault.azure.net:443") 
     * interface KeyVaultService { 
     *     @Get("secrets/{secretName}") 
     *     Secret get(@HostParam("vaultName") String vaultName, @PathParam("secretName") String secretName); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.annotation.Host.class2 --> 
     */ 
    public @annotation Host { 
        String value() default ""
    } 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Annotation to annotate replacement of parameterized segments in a dynamic {@link Host}. 
     * 
     * <p> 
     * You provide the value, which should be the same (case sensitive) with the parameterized segments in '{}' in the 
     * host, unless there's only one parameterized segment, then you can leave the value empty. This is extremely useful 
     * when the designer of the API interface doesn't know about the named parameters in the host. 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Named parameters</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HostParam.class1 --> 
     * <pre> 
     * @Host("{accountName}.{suffix}") 
     * interface DatalakeService { 
     *     @Get("jobs/{jobIdentity}") 
     *     Job getJob(@HostParam("accountName") String accountName, 
     *         @HostParam("suffix") String suffix, 
     *         @PathParam("jobIdentity") String jobIdentity); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.annotation.HostParam.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Unnamed parameter</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.HostParam.class2 --> 
     * <pre> 
     * String KEY_VAULT_ENDPOINT = "{vaultName}"; 
     * 
     * @Host(KEY_VAULT_ENDPOINT) 
     * interface KeyVaultService { 
     *     @Get("secrets/{secretName}") 
     *     Secret get(@HostParam("vaultName") String vaultName, @PathParam("secretName") String secretName); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.annotation.HostParam.class2 --> 
     */ 
    public @annotation HostParam { 
        String value()
        boolean encoded() default true
    } 
    @Retention(SOURCE)
    @Target(TYPE)
    /** 
     * Annotation given to all immutable classes. If a class has this annotation, checks can be made to ensure all fields in 
     * this class are final. 
     */ 
    public @annotation Immutable { 
        // This annotation does not declare any members. 
    } 
    @Retention(RUNTIME)
    @Target({ ElementType.ANNOTATION_TYPE, ElementType.TYPE, ElementType.FIELD })
    /** 
     * Annotation used for flattening properties separated by '.'. E.g. a property with JsonProperty value 
     * "properties.value" will have "value" property under the "properties" tree on the wire. This annotation when used on a 
     * class, all JSON fields will be checked for '.' and be flattened appropriately. 
     */ 
    public @annotation JsonFlatten { 
        // This annotation does not declare any members. 
    } 
    @Retention(RetentionPolicy.RUNTIME)
    @Target(ElementType.METHOD)
    /** 
     * HTTP OPTIONS method annotation describing the parameterized relative path to a REST endpoint for retrieving options. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (Otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Options.class1 --> 
     * <pre> 
     * @Options("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * ResponseBase<ResponseHeaders, ResponseBody> options(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Options.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Options.class2 --> 
     * <pre> 
     * @Options("{vaultBaseUrl}/secrets/{secretName}") 
     * ResponseBase<ResponseHeaders, ResponseBody> options( 
     *     @PathParam(value = "vaultBaseUrl", encoded = true) String vaultBaseUrl, 
     *     @PathParam("secretName") String secretName); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Options.class2 --> 
     */ 
    public @annotation Options { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP PATCH method annotation describing the parameterized relative path to a REST endpoint for resource update. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (Otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <pre> 
     * {@literal @}Patch("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/ 
     * Microsoft.Compute/virtualMachines/{vmName}") 
     * VirtualMachine patch(@PathParam("resourceGroupName") String rgName, @PathParam("vmName") String 
     * vmName, @PathParam("subscriptionId") String subscriptionId, @BodyParam VirtualMachineUpdateParameters 
     * updateParameters); </pre> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <pre> 
     * {@literal @}Patch({vaultBaseUrl}/secrets/{secretName}) 
     * Secret patch(@PathParam("vaultBaseUrl" encoded = true) String vaultBaseUrl, @PathParam("secretName") String 
     * secretName, @BodyParam SecretUpdateParameters updateParameters); </pre> 
     */ 
    public @annotation Patch { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Annotation to annotate replacement for a named path segment in REST endpoint URL. 
     * 
     * <p> 
     * A parameter that is annotated with PathParam will be ignored if the "uri template" does not contain a path 
     * segment variable with name {@link PathParam#value()}. 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.PathParam.class1 --> 
     * <pre> 
     * @Get("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/") 
     * VirtualMachine getByResourceGroup(@PathParam("subscriptionId") String subscriptionId, 
     *     @PathParam("resourceGroupName") String rgName, 
     *     @PathParam("foo") String bar); 
     * 
     * // The value of parameters subscriptionId, resourceGroupName will be encoded and used to replace the 
     * // corresponding path segments {subscriptionId}, {resourceGroupName} respectively. 
     * </pre> 
     * <!-- end com.azure.core.annotation.PathParam.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: (A use case where PathParam.encoded=true will be used)</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.PathParam.class2 --> 
     * <pre> 
     * // It is possible that a path segment variable can be used to represent sub path: 
     * 
     * @Get("http://wq.com/foo/{subpath}/value") 
     * String getValue(@PathParam("subpath") String param1); 
     * 
     * // In this case, if consumer pass "a/b" as the value for param1 then the resolved url looks like: 
     * // "http://wq.com/foo/a%2Fb/value". 
     * </pre> 
     * <!-- end com.azure.core.annotation.PathParam.class2 --> 
     * 
     * <!-- src_embed com.azure.core.annotation.PathParam.class3 --> 
     * <pre> 
     * // For such cases the encoded attribute can be used: 
     * 
     * @Get("http://wq.com/foo/{subpath}/values") 
     * List<String> getValues(@PathParam(value = "subpath", encoded = true) String param1); 
     * 
     * // In this case, if consumer pass "a/b" as the value for param1 then the resolved url looks as expected: 
     * // "http://wq.com/foo/a/b/values". 
     * </pre> 
     * <!-- end com.azure.core.annotation.PathParam.class3 --> 
     */ 
    public @annotation PathParam { 
        String value()
        boolean encoded() default false
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP POST method annotation describing the parameterized relative path to a REST endpoint for an action. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (Otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Post.class1 --> 
     * <pre> 
     * @Post("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}/restart") 
     * void restart(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Post.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Post.class2 --> 
     * <pre> 
     * @Post("https://{functionApp}.azurewebsites.net/admin/functions/{name}/keys/{keyName}") 
     * KeyValuePair generateFunctionKey(@PathParam("functionApp") String functionApp, 
     *     @PathParam("name") String name, 
     *     @PathParam("keyName") String keyName); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Post.class2 --> 
     */ 
    public @annotation Post { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * HTTP PUT method annotation describing the parameterized relative path to a REST endpoint for resource creation or 
     * update. 
     * 
     * <p> 
     * The required value can be either a relative path or an absolute path. When it's an absolute path, it must start 
     * with a protocol or a parameterized segment (Otherwise the parse cannot tell if it's absolute or relative). 
     * </p> 
     * 
     * <p> 
     * <strong>Example 1: Relative path segments</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Put.class1 --> 
     * <pre> 
     * @Put("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/" 
     *     + "virtualMachines/{vmName}") 
     * VirtualMachine createOrUpdate(@PathParam("resourceGroupName") String rgName, 
     *     @PathParam("vmName") String vmName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") VirtualMachine vm); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Put.class1 --> 
     * 
     * <p> 
     * <strong>Example 2: Absolute path segment</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.Put.class2 --> 
     * <pre> 
     * @Put("{vaultBaseUrl}/secrets/{secretName}") 
     * Secret createOrUpdate(@PathParam(value = "vaultBaseUrl", encoded = true) String vaultBaseUrl, 
     *     @PathParam("secretName") String secretName, 
     *     @BodyParam("application/json") Secret secret); 
     * </pre> 
     * <!-- end com.azure.core.annotation.Put.class2 --> 
     */ 
    public @annotation Put { 
        String value()
    } 
    @Retention(RUNTIME)
    @Target(PARAMETER)
    /** 
     * Annotation for query parameters to be appended to a REST API Request URI. 
     * 
     * <p> 
     * <strong>Example 1:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.QueryParam.class1 --> 
     * <pre> 
     * @Get("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/resources") 
     * Mono<ResponseBase<ResponseHeaders, ResponseBody>> listByResourceGroup( 
     *     @PathParam("resourceGroupName") String resourceGroupName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @QueryParam("$filter") String filter, 
     *     @QueryParam("$expand") String expand, 
     *     @QueryParam("$top") Integer top, 
     *     @QueryParam("api-version") String apiVersion); 
     * 
     * // The value of parameters filter, expand, top, apiVersion will be encoded and will be used to set the query 
     * // parameters {$filter}, {$expand}, {$top}, {api-version} on the HTTP URL. 
     * </pre> 
     * <!-- end com.azure.core.annotation.QueryParam.class1 --> 
     * 
     * <p> 
     * <strong>Example 2:</strong> (A use case where PathParam.encoded=true will be used) 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.QueryParam.class2 --> 
     * <pre> 
     * // It is possible that a query parameter will need to be encoded: 
     * @Get("http://wq.com/foo/{subpath}/value") 
     * String getValue(@PathParam("subpath") String param, 
     *     @QueryParam("query") String query); 
     * 
     * // In this case, if consumer pass "a=b" as the value for 'query' then the resolved url looks like: 
     * // "http://wq.com/foo/subpath/value?query=a%3Db" 
     * </pre> 
     * <!-- end com.azure.core.annotation.QueryParam.class2 --> 
     * 
     * <p> 
     * For such cases the encoded attribute can be used: 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.QueryParam.class3 --> 
     * <pre> 
     * @Get("http://wq.com/foo/{subpath}/values") 
     * List<String> getValues(@PathParam("subpath") String param, 
     *     @QueryParam(value = "query", encoded = true) String query); 
     * 
     * // In this case, if consumer pass "a=b" as the value for 'query' then the resolved url looks like: 
     * // "http://wq.com/foo/subpath/values?connectionString=a=b" 
     * </pre> 
     * <!-- end com.azure.core.annotation.QueryParam.class3 --> 
     * 
     * <p> 
     * <strong>Example 3:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.QueryParam.class4 --> 
     * <pre> 
     * @Get("http://wq.com/foo/multiple/params") 
     * String multipleParams(@QueryParam(value = "query", multipleQueryParams = true) List<String> query); 
     * 
     * // The value of parameter avoid would look like this: 
     * // "http://wq.com/foo/multiple/params?avoid%3Dtest1&avoid%3Dtest2&avoid%3Dtest3" 
     * </pre> 
     * <!-- end com.azure.core.annotation.QueryParam.class4 --> 
     */ 
    public @annotation QueryParam { 
        String value()
        boolean encoded() default false
        boolean multipleQueryParams() default false
    } 
    @Deprecated
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * Annotation for method representing continuation operation. 
     * 
     * @deprecated This interface is no longer used, or respected, in code. 
     */ 
    public @annotation ResumeOperation { 
        // This annotation does not declare any members. 
    } 
    /** 
     * Enumeration of return types used with {@link ServiceMethod} annotation to indicate if a method is expected to return 
     * a single item or a collection 
     */ 
    public enum ReturnType { 
        SINGLE, 
            /** 
             * Single value return type. 
             */ 
        COLLECTION, 
            /** 
             * Simple collection, enumeration, return type. 
             */ 
        LONG_RUNNING_OPERATION; 
            /** 
             * Long-running operation return type. 
             */ 
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * Annotation for the type that will be used to deserialize the return value of a REST API response. Supported values 
     * are: 
     * 
     * <ol> 
     * <li>{@link Base64Url}</li> 
     * <li>{@link DateTimeRfc1123}</li> 
     * <li>{@link Page}</li> 
     * <li>{@link List List<T>} where {@code T} can be one of the four values above.</li> 
     * </ol> 
     */ 
    public @annotation ReturnValueWireType { 
        Class<?> value()
    } 
    @Retention(CLASS)
    @Target(TYPE)
    /** 
     * Annotation given to all service client classes. 
     */ 
    public @annotation ServiceClient { 
        Class<?> builder()
        boolean isAsync() default false
        Class<?>[] serviceInterfaces() default {  }
    } 
    @Retention(RUNTIME)
    @Target(TYPE)
    /** 
     * Annotation given to all service client builder classes. 
     */ 
    public @annotation ServiceClientBuilder { 
        Class<?>[] serviceClients()
        ServiceClientProtocol protocol() default ServiceClientProtocol.HTTP
    } 
    /** 
     * Enumeration of protocols available for setting the {@link ServiceClientBuilder#protocol() protocol} property of 
     * {@link ServiceClientBuilder} annotation. 
     */ 
    public enum ServiceClientProtocol { 
        HTTP, 
            /** 
             * HTTP protocol. 
             */ 
        AMQP; 
            /** 
             * AMQP protocol. 
             */ 
    } 
    @Retention(RUNTIME)
    @Target(TYPE)
    /** 
     * Annotation to give the service interfaces a name that correlates to the service that is usable in a programmatic 
     * way. 
     */ 
    public @annotation ServiceInterface { 
        String name()
    } 
    @Retention(CLASS)
    @Target(METHOD)
    /** 
     * Annotation given to all service client methods that perform network operations. All methods with this annotation 
     * should be contained in class annotated with {@link ServiceClient} 
     */ 
    public @annotation ServiceMethod { 
        ReturnType returns()
    } 
    @Repeatable(UnexpectedResponseExceptionTypes)
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * The exception type that is thrown or returned when one of the status codes is returned from a REST API. Multiple 
     * annotations can be used. When no codes are listed that exception is always thrown or returned if it is reached 
     * during evaluation, this should be treated as a default case. If no default case is annotated the fall through 
     * exception is {@link HttpResponseException}. 
     * 
     * <p> 
     * <strong>Example:</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.annotation.UnexpectedResponseExceptionType.class --> 
     * <pre> 
     * // Set it so that all response exceptions use a custom exception type. 
     * 
     * @UnexpectedResponseExceptionType(MyCustomExceptionHttpResponseException.class) 
     * @Post("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/" 
     *     + "Microsoft.CustomerInsights/hubs/{hubName}/images/getEntityTypeImageUploadUrl") 
     * void singleExceptionType(@PathParam("resourceGroupName") String resourceGroupName, 
     *     @PathParam("hubName") String hubName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") RequestBody parameters); 
     * 
     * 
     * // Set it so 404 uses a specific exception type while others use a generic exception type. 
     * 
     * @UnexpectedResponseExceptionType(code = {404}, value = ResourceNotFoundException.class) 
     * @UnexpectedResponseExceptionType(HttpResponseException.class) 
     * @Post("subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/" 
     *     + "Microsoft.CustomerInsights/hubs/{hubName}/images/getEntityTypeImageUploadUrl") 
     * void multipleExceptionTypes(@PathParam("resourceGroupName") String resourceGroupName, 
     *     @PathParam("hubName") String hubName, 
     *     @PathParam("subscriptionId") String subscriptionId, 
     *     @BodyParam("application/json") RequestBody parameters); 
     * 
     * // If multiple annotations share the same HTTP status code or there is multiple default annotations the 
     * // exception, the last annotation in the top to bottom order will be used (so the bottom most annotation). 
     * </pre> 
     * <!-- end com.azure.core.annotation.UnexpectedResponseExceptionType.class --> 
     */ 
    public @annotation UnexpectedResponseExceptionType { 
        Class<? extends HttpResponseException> value()
        int[] code() default {  }
    } 
    @Retention(RUNTIME)
    @Target(METHOD)
    /** 
     * The {@link Repeatable} container annotation for {@link UnexpectedResponseExceptionType}. This allows methods to have 
     * different exceptions to be thrown or returned based on the response status codes returned from a REST API. 
     */ 
    public @annotation UnexpectedResponseExceptionTypes { 
        UnexpectedResponseExceptionType[] value()
    } 
} 
/** 
 * This package contains interfaces that represent common cross-cutting aspects of functionality offered by libraries 
 * in the Azure SDK for Java. Each interface is referred to as a 'trait', and classes that implement the interface are 
 * said to have that trait. There are additional traits related to AMQP use cases in the 
 * {@code com.azure.core.amqp.client.traits} package. 
 * 
 * <p> 
 * The particular focus of traits in the Azure SDK for Java is to enable higher-level 
 * libraries the ability to more abstractly configure client libraries as part of their builders, prior to the client 
 * itself being instantiated. By doing this, these high-level libraries are able to reason about functionality more 
 * simply. It is important to appreciate that despite the availability of these cross-cutting traits, there is no 
 * promise that configuration of each builder can simply be a matter of providing the same arguments for all builders! 
 * Each builder must be configured appropriately for its requirements, or else runtime failures may occur when the 
 * builder is asked to create the associated client. 
 * </p> 
 */ 
package com.azure.core.client.traits { 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link AzureKeyCredential}. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
     * documentation for more details on proper usage of the {@link AzureKeyCredential} type. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see AzureKeyCredential 
     */ 
    public interface AzureKeyCredentialTrait<T extends AzureKeyCredentialTrait<T>> { 
        /** 
         * Sets the {@link AzureKeyCredential} used for authentication. Refer to the Azure SDK for Java 
         * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
         * documentation for more details on proper usage of the {@link AzureKeyCredential} type. 
         * 
         * @param credential the {@link AzureKeyCredential} to be used for authentication. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T credential(AzureKeyCredential credential) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link AzureNamedKeyCredential}. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
     * documentation for more details on proper usage of the {@link AzureNamedKeyCredential} type. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see AzureNamedKeyCredential 
     */ 
    public interface AzureNamedKeyCredentialTrait<T extends AzureNamedKeyCredentialTrait<T>> { 
        /** 
         * Sets the {@link AzureNamedKeyCredential} used for authentication. Refer to the Azure SDK for Java 
         * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
         * documentation for more details on proper usage of the {@link AzureNamedKeyCredential} type. 
         * 
         * @param credential the {@link AzureNamedKeyCredential} to be used for authentication. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T credential(AzureNamedKeyCredential credential) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link AzureSasCredential}. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
     * documentation for more details on proper usage of the {@link AzureSasCredential} type. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see AzureSasCredential 
     */ 
    public interface AzureSasCredentialTrait<T extends AzureSasCredentialTrait<T>> { 
        /** 
         * Sets the {@link AzureSasCredential} used for authentication. Refer to the Azure SDK for Java 
         * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
         * documentation for more details on proper usage of the {@link AzureSasCredential} type. 
         * 
         * @param credential the {@link AzureSasCredential} to be used for authentication. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T credential(AzureSasCredential credential) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link Configuration}. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see Configuration 
     */ 
    public interface ConfigurationTrait<T extends ConfigurationTrait<T>> { 
        /** 
         * Sets the client-specific configuration used to retrieve client or global configuration properties 
         * when building a client. 
         * 
         * @param configuration Configuration store used to retrieve client configurations. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T configuration(Configuration configuration) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for 
     * setting connection strings. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     */ 
    public interface ConnectionStringTrait<T extends ConnectionStringTrait<T>> { 
        /** 
         * Sets the connection string to connect to the service. 
         * 
         * @param connectionString Connection string of the service. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T connectionString(String connectionString) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * service endpoints. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     */ 
    public interface EndpointTrait<T extends EndpointTrait<T>> { 
        /** 
         * Sets the service endpoint that will be connected to by clients. 
         * 
         * @param endpoint The URL of the service endpoint. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         * @throws NullPointerException If {@code endpoint} is {@code null}. 
         * @throws IllegalArgumentException If {@code endpoint} isn't a valid URL. 
         */ 
        T endpoint(String endpoint) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for configuration 
     * of HTTP-specific settings. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/http-client-pipeline">HTTP clients and pipelines</a> 
     * documentation for more details on proper usage and configuration of the Azure SDK for Java HTTP clients. 
     * 
     * <p> 
     * It is important to understand the precedence order of the HttpTrait APIs. In particular, if a 
     * {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and they will be ignored. 
     * If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally based on the settings 
     * provided to this trait. Additionally, there may be other APIs in types that implement this 
     * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
     * documentation of types that implement this trait to understand the full set of implications. 
     * </p> 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see HttpClient 
     * @see HttpPipeline 
     * @see HttpPipelinePolicy 
     * @see RetryOptions 
     * @see HttpLogOptions 
     * @see HttpClientOptions 
     */ 
    public interface HttpTrait<T extends HttpTrait<T>> { 
        /** 
         * Adds a {@link HttpPipelinePolicy pipeline policy} to apply on each request sent. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param pipelinePolicy A {@link HttpPipelinePolicy pipeline policy}. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         * @throws NullPointerException If {@code pipelinePolicy} is {@code null}. 
         */ 
        T addPolicy(HttpPipelinePolicy pipelinePolicy) 
        /** 
         * Allows for setting common properties such as application ID, headers, proxy configuration, etc. Note that it is 
         * recommended that this method be called with an instance of the {@link HttpClientOptions} 
         * class (a subclass of the {@link ClientOptions} base class). The HttpClientOptions subclass provides more 
         * configuration options suitable for HTTP clients, which is applicable for any class that implements this HttpTrait 
         * interface. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param clientOptions A configured instance of {@link HttpClientOptions}. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         * @see HttpClientOptions 
         */ 
        T clientOptions(ClientOptions clientOptions) 
        /** 
         * Sets the {@link HttpClient} to use for sending and receiving requests to and from the service. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param httpClient The {@link HttpClient} to use for requests. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T httpClient(HttpClient httpClient) 
        /** 
         * Sets the {@link HttpLogOptions logging configuration} to use when sending and receiving requests to and from 
         * the service. If a {@code logLevel} is not provided, default value of {@link HttpLogDetailLevel#NONE} is set. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param logOptions The {@link HttpLogOptions logging configuration} to use when sending and receiving requests to 
         * and from the service. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T httpLogOptions(HttpLogOptions logOptions) 
        /** 
         * Sets the {@link HttpPipeline} to use for the service client. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param pipeline {@link HttpPipeline} to use for sending service requests and receiving responses. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T pipeline(HttpPipeline pipeline) 
        /** 
         * Sets the {@link RetryOptions} for all the requests made through the client. 
         * 
         * <p><strong>Note:</strong> It is important to understand the precedence order of the HttpTrait APIs. In 
         * particular, if a {@link HttpPipeline} is specified, this takes precedence over all other APIs in the trait, and 
         * they will be ignored. If no {@link HttpPipeline} is specified, a HTTP pipeline will be constructed internally 
         * based on the settings provided to this trait. Additionally, there may be other APIs in types that implement this 
         * trait that are also ignored if an {@link HttpPipeline} is specified, so please be sure to refer to the 
         * documentation of types that implement this trait to understand the full set of implications.</p> 
         * 
         * @param retryOptions The {@link RetryOptions} to use for all the requests made through the client. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T retryOptions(RetryOptions retryOptions) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link KeyCredential}. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
     * documentation for more details on proper usage of the {@link KeyCredential} type. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see KeyCredential 
     */ 
    public interface KeyCredentialTrait<T> { 
        /** 
         * Sets the {@link KeyCredential} used for authentication. Refer to the Azure SDK for Java 
         * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
         * documentation for more details on proper usage of the {@link KeyCredential} type. 
         * 
         * @param credential the {@link KeyCredential} to be used for authentication. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T credential(KeyCredential credential) 
    } 
    /** 
     * An {@link com.azure.core.client.traits Azure SDK for Java trait} providing a consistent interface for setting 
     * {@link TokenCredential}. Refer to the Azure SDK for Java 
     * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
     * documentation for more details on proper usage of the {@link TokenCredential} type. 
     * 
     * @param <T> The concrete type that implements the trait. This is required so that fluent operations can continue 
     * to return the concrete type, rather than the trait type. 
     * @see com.azure.core.client.traits 
     * @see TokenCredential 
     */ 
    public interface TokenCredentialTrait<T extends TokenCredentialTrait<T>> { 
        /** 
         * Sets the {@link TokenCredential} used to authorize requests sent to the service. Refer to the Azure SDK for Java 
         * <a href="https://aka.ms/azsdk/java/docs/identity">identity and authentication</a> 
         * documentation for more details on proper usage of the {@link TokenCredential} type. 
         * 
         * @param credential {@link TokenCredential} used to authorize requests sent to the service. 
         * @return Returns the same concrete type with the appropriate properties updated, to allow for fluent chaining of 
         *      operations. 
         */ 
        T credential(TokenCredential credential) 
    } 
} 
/** 
 * <p> 
 * Azure Core Credential library is designed to simplify the process of authenticating and authorizing access 
 * to Azure services from Java applications. The SDK provides a set of classes and methods that handle authentication 
 * and credential management, allowing developers to securely connect to Azure services without dealing with the 
 * low-level details of authentication protocols. 
 * </p> 
 * 
 * <p> 
 * The library provides a unified way to obtain credentials for various Azure authentication 
 * mechanisms, such as Azure Active Directory (AAD), shared access signatures, and API keys. It abstracts the 
 * complexities of authentication and provides a consistent programming model for accessing Azure services. 
 * </p> 
 * 
 * <p> 
 * By using the library, users can easily integrate Azure authentication into their applications, retrieve the 
 * required credentials based on the desired authentication method, and use those credentials to authenticate 
 * requests to Azure services like Azure Storage, Azure Key Vault, Azure Service Bus, and more. 
 * </p> 
 * 
 * <p> 
 * The library offers several authentication types for authenticating with Azure services. Here are some of the 
 * authentication mechanisms supported by the library: 
 * </p> 
 * <ul> 
 * <li>Azure Active Directory (AAD) Authentication</li> 
 * <li>Shared Access Signature (SAS) Authentication</li> 
 * <li>Key Based Authentication</li> 
 * </ul> 
 * 
 * <h2>Azure Active Directory (AAD) Authentication</h2> 
 * 
 * <p> 
 * This type of authentication allows you to authenticate using Azure Active Directory and obtain a token to access 
 * Azure resources. You can authenticate with AAD using client secrets, client certificates, or user credentials. 
 * The library offers {@link com.azure.core.credential.TokenCredential} interface which is accepted as an argument 
 * on the client builders in Azure SDKs where AAD authentication is supported. 
 * You can refer to and include our 
 * <a href="https://learn.microsoft.com/java/api/overview/azure/identity-readme?view=azure-java-stable">Azure 
 * Identity</a> 
 * library in your application as it offers pluggable implementation of 
 * {@link com.azure.core.credential.TokenCredential} for various AAD based authentication mechanism including 
 * service principal, managed identity, and more. 
 * </p> 
 * 
 * <br> 
 * 
 * <hr> 
 * 
 * <h2>Shared Access Signature (SAS) Authentication</h2> 
 * 
 * <p> 
 * Shared Access Signatures enable you to grant time-limited access to Azure resources. The library offers 
 * {@link com.azure.core.credential.AzureSasCredential} which allows you to authenticate using a shared access 
 * signature, which is a string-based token that grants access to specific resources for a specific period. 
 * </p> 
 * 
 * <p> 
 * <strong>Sample: Azure SAS Authentication</strong> 
 * </p> 
 * 
 * <p> 
 * The following code sample demonstrates the creation of a {@link com.azure.core.credential.AzureSasCredential}, 
 * using the sas token to configure it. 
 * </p> 
 * 
 * <!-- src_embed com.azure.core.credential.azureSasCredential --> 
 * <pre> 
 * AzureSasCredential azureSasCredential = 
 *     new AzureSasCredential("AZURE-SERVICE-SAS-KEY"); 
 * </pre> 
 * <!-- end com.azure.core.credential.azureSasCredential --> 
 * 
 * <br> 
 * 
 * <hr> 
 * 
 * <h2>Key Based Authentication</h2> 
 * 
 * <p> 
 * A key is a unique identifier or token that is associated with a specific user or application. It serves as a 
 * simple form of authentication to ensure that only authorized clients can access the protected resources or APIs. 
 * This authentication is commonly used for accessing certain services, such as Azure Cognitive Services, Azure Search, 
 * or Azure Management APIs. Each service may have its own specific way of using API keys, but the general concept 
 * remains the same. The library offers {@link com.azure.core.credential.AzureKeyCredential} and 
 * {@link com.azure.core.credential.AzureNamedKeyCredential} which can allows you to authenticate using a key. 
 * </p> 
 * 
 * 
 * <p> 
 * <strong>Sample: Azure Key Authentication</strong> 
 * </p> 
 * 
 * <p> 
 * The following code sample demonstrates the creation of a {@link com.azure.core.credential.AzureKeyCredential}, 
 * using the Azure service key to configure it. 
 * </p> 
 * 
 * <!-- src_embed com.azure.core.credential.azureKeyCredential --> 
 * <pre> 
 * AzureKeyCredential azureKeyCredential = new AzureKeyCredential("AZURE-SERVICE-KEY"); 
 * </pre> 
 * <!-- end com.azure.core.credential.azureKeyCredential --> 
 * 
 * @see com.azure.core.credential.AzureKeyCredential 
 * @see com.azure.core.credential.AzureNamedKeyCredential 
 * @see com.azure.core.credential.AzureSasCredential 
 * @see com.azure.core.credential.TokenCredential 
 */ 
package com.azure.core.credential { 
    /** 
     * <p> 
     * Represents an immutable access token with a token string and an expiration time. 
     * </p> 
     * 
     * <p> 
     * An Access Token is a security token that is issued by an authentication source, such as 
     * Azure Active Directory (AAD), and it represents the authorization to access a specific resource or service. 
     * It is typically used to authenticate and authorize requests made to Azure services. 
     * </p> 
     * 
     * <p> 
     * Access Tokens are obtained through the authentication process, where the user or application presents valid 
     * credentials (such as a client ID, client secret, username/password, or certificate) to the authentication source. 
     * The authentication source then verifies the credentials and issues an Access Token, which is a time-limited token 
     * that grants access to the requested resource. 
     * </p> 
     * 
     * <p> 
     * Once an Access Token is obtained, it can be included in the Authorization header of HTTP requests to 
     * authenticate and authorize requests to Azure services. 
     * </p> 
     * 
     * @see com.azure.core.credential 
     * @see com.azure.core.credential.TokenCredential 
     */ 
    public class AccessToken { 
        /** 
         * Creates an access token instance. 
         * 
         * @param token the token string. 
         * @param expiresAt the expiration time. 
         */ 
        public AccessToken(String token, OffsetDateTime expiresAt) 
        /** 
         * Creates an access token instance. 
         * 
         * @param token the token string. 
         * @param expiresAt the expiration time. 
         * @param refreshAt the next token refresh time. 
         */ 
        public AccessToken(String token, OffsetDateTime expiresAt, OffsetDateTime refreshAt) 
        /** 
         * Creates an access token instance. 
         * 
         * @param token the token string. 
         * @param expiresAt the expiration time. 
         * @param refreshAt the next token refresh time. 
         * @param tokenType the type of token. 
         */ 
        public AccessToken(String token, OffsetDateTime expiresAt, OffsetDateTime refreshAt, String tokenType) 
        /** 
         * Gets the {@link Duration} until the {@link AccessToken} expires. 
         * <p> 
         * The {@link Duration} is based on the {@link OffsetDateTime#now() current time} and may return a negative 
         * {@link Duration}, indicating that the {@link AccessToken} has expired. 
         * 
         * @return The {@link Duration} until the {@link AccessToken} expires. 
         */ 
        public Duration getDurationUntilExpiration() 
        /** 
         * Whether the token has expired. 
         * 
         * @return Whether the token has expired. 
         */ 
        public boolean isExpired() 
        /** 
         * Gets the time when the token expires, in UTC. 
         * 
         * @return The time when the token expires, in UTC. 
         */ 
        public OffsetDateTime getExpiresAt() 
        /** 
         * Gets the time when the token should refresh next, in UTC. 
         * 
         * <p>Note: This value can be null as it is not always provided by the service. When it is provided, 
         * it overrides the default refresh offset used by the 
         * {@link com.azure.core.http.policy.BearerTokenAuthenticationPolicy} to proactively refresh the token.</p> 
         * 
         * @return The time when the token should refresh next, in UTC. 
         */ 
        public OffsetDateTime getRefreshAt() 
        /** 
         * Gets the token. 
         * 
         * @return The token. 
         */ 
        public String getToken() 
        /** 
         * Gets the token type. 
         * 
         * @return A string representing the token type. It can be "Bearer" or "Pop". 
         */ 
        public String getTokenType() 
    } 
    /** 
     * <p> 
     * The {@link AzureKeyCredential} is used to authenticate and authorize requests made to Azure services. 
     * It is specifically designed for scenarios where you need to authenticate using a key. 
     * </p> 
     * 
     * <p> 
     * A key is a unique identifier or token that is associated with a specific user or application. It serves as a 
     * simple form of authentication to ensure that only authorized clients can access the protected resources or APIs. 
     * This authentication is commonly used for accessing certain services, such as Azure Cognitive Services, Azure Search, 
     * or Azure Management APIs. Each service may have its own specific way of using API keys, but the general concept 
     * remains the same. The {@link com.azure.core.credential.AzureKeyCredential} allows you to authenticate 
     * using a key. 
     * </p> 
     * 
     * <p> 
     * <strong>Code Samples</strong> 
     * </p> 
     * 
     * <p> 
     * Create a key credential for a service key. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.credential.azureKeyCredential --> 
     * <pre> 
     * AzureKeyCredential azureKeyCredential = new AzureKeyCredential("AZURE-SERVICE-KEY"); 
     * </pre> 
     * <!-- end com.azure.core.credential.azureKeyCredential --> 
     * 
     * @see com.azure.core.credential 
     */ 
    public final class AzureKeyCredential extends KeyCredential { 
        /** 
         * Creates a credential that authorizes request with the given key. 
         * 
         * @param key The key used to authorize requests. 
         * @throws NullPointerException If {@code key} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} is an empty string. 
         */ 
        public AzureKeyCredential(String key) 
        /** 
         * Rotates the key associated to this credential. 
         * 
         * @param key The new key to associated with this credential. 
         * @return The updated {@code AzureKeyCredential} object. 
         * @throws NullPointerException If {@code key} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} is an empty string. 
         */ 
        @Override public AzureKeyCredential update(String key) 
    } 
    @Immutable
    /** 
     * Represents a credential bag containing the key and the name of the key. 
     * 
     * @see AzureNamedKeyCredential 
     */ 
    public final class AzureNamedKey { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Retrieves the key. 
         * 
         * @return The key. 
         */ 
        public String getKey() 
        /** 
         * Retrieves the name associated with the key. 
         * 
         * @return The name of the key. 
         */ 
        public String getName() 
    } 
    /** 
     * <p> 
     * The {@link AzureNamedKeyCredential} is used to authenticate and authorize requests made to Azure services. 
     * It is specifically designed for scenarios where you need to authenticate using a key with a name identifier 
     * associated with it. 
     * </p> 
     * 
     * <p> 
     * A key is a unique identifier or token that is associated with a specific user or application. It serves as a 
     * simple form of authentication to ensure that only authorized clients can access the protected resources or APIs. 
     * This authentication is commonly used for accessing certain services, such as Azure Tables and Azure Event Hubs. 
     * Each service may have its own specific way of using API keys, but the general concept remains the same. 
     * </p> 
     * 
     * <p> 
     * The {@link com.azure.core.credential.AzureNamedKeyCredential} can be created for keys which have a name 
     * identifier associated with them. 
     * </p> 
     * 
     * <p> 
     * <strong>Code Samples</strong> 
     * </p> 
     * 
     * <p> 
     * Create a named credential for a service specific sas key. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.credential.azureNamedKeyCredentialSasKey --> 
     * <pre> 
     * AzureNamedKeyCredential azureNamedKeyCredential = 
     *     new AzureNamedKeyCredential("AZURE-SERVICE-SAS-KEY-NAME", "AZURE-SERVICE-SAS-KEY"); 
     * </pre> 
     * <!-- end com.azure.core.credential.azureNamedKeyCredentialSasKey --> 
     * 
     * @see com.azure.core.credential 
     */ 
    public final class AzureNamedKeyCredential { 
        /** 
         * Creates a credential with specified {@code name} that authorizes request with the given {@code key}. 
         * 
         * @param name The name of the key credential. 
         * @param key The key used to authorize requests. 
         * @throws NullPointerException If {@code key} or {@code name} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} or {@code name} is an empty string. 
         */ 
        public AzureNamedKeyCredential(String name, String key) 
        /** 
         * Retrieves the {@link AzureNamedKey} containing the name and key associated with this credential. 
         * 
         * @return The {@link AzureNamedKey} containing the name and key . 
         */ 
        public AzureNamedKey getAzureNamedKey() 
        /** 
         * Rotates the {@code name} and  {@code key} associated to this credential. 
         * 
         * @param name The new name of the key credential. 
         * @param key The new key to be associated with this credential. 
         * @return The updated {@code AzureNamedKeyCredential} object. 
         * @throws NullPointerException If {@code key} or {@code name} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} or {@code name} is an empty string. 
         */ 
        public AzureNamedKeyCredential update(String name, String key) 
    } 
    /** 
     * <p> 
     * Represents a credential that uses a shared access signature to authenticate to an Azure Service. 
     * It is used for authenticating and authorizing access to Azure services using a shared access signature. 
     * </p> 
     * 
     * <p> 
     * A shared access signature is a string-based token that grants limited permissions and access to specific 
     * resources within an Azure service for a specified period. It allows you to provide time-limited access to your 
     * resources without sharing your account key or other sensitive credentials. 
     * </p> 
     * 
     * <p> 
     * The {@link AzureSasCredential} enables you to authenticate and access Azure services that 
     * support shared access signatures. By creating an instance of the {@link AzureSasCredential} class and providing the 
     * SAS token as a parameter, you can use this credential to authenticate requests to Azure services. 
     * </p> 
     * 
     * <p> 
     * To use the Credential, you typically pass it to the appropriate Azure client or service client 
     * builder during instantiation. The library internally handles the authentication process and includes the 
     * SAS token in the HTTP requests made to the Azure service, allowing you to access the resources specified in 
     * the SAS token. 
     * </p> 
     * 
     * <p> 
     * The {@link AzureSasCredential} is particularly useful when you need to grant temporary and limited access to 
     * specific resources, such as Azure Storage containers, blobs, queues, or files, without exposing 
     * your account key. 
     * </p> 
     * 
     * <p> 
     * It's important to note that the availability and usage of the {@link AzureSasCredential} may depend on the 
     * specific Azure service and its support for shared access signatures. Additionally, the format and content of the 
     * SAS token may vary depending on the service and resource you are targeting. 
     * </p> 
     * 
     * <p> 
     * <strong>Sample: Azure SAS Authentication</strong> 
     * </p> 
     * 
     * <p> 
     * The following code sample demonstrates the creation of a {@link com.azure.core.credential.AzureSasCredential}, 
     * using the sas token to configure it. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.credential.azureSasCredential --> 
     * <pre> 
     * AzureSasCredential azureSasCredential = 
     *     new AzureSasCredential("AZURE-SERVICE-SAS-KEY"); 
     * </pre> 
     * <!-- end com.azure.core.credential.azureSasCredential --> 
     * 
     * @see com.azure.core.credential 
     */ 
    public final class AzureSasCredential { 
        /** 
         * Creates a credential that authorizes request with the given shared access signature. 
         * <p> 
         * The {@code signature} passed is assumed to be encoded. This constructor is effectively the same as calling {@link 
         * #AzureSasCredential(String, Function) new AzureSasCredential(signature, null))}. 
         * 
         * @param signature The shared access signature used to authorize requests. 
         * @throws NullPointerException If {@code signature} is {@code null}. 
         * @throws IllegalArgumentException If {@code signature} is an empty string. 
         */ 
        public AzureSasCredential(String signature) 
        /** 
         * Creates a credential that authorizes request within the given shared access signature. 
         * <p> 
         * If {@code signatureEncoder} is non-null the {@code signature}, and all {@link #update(String) updated 
         * signatures}, will be encoded using the function. {@code signatureEncoder} should be as idempotent as possible to 
         * reduce the chance of double encoding errors. 
         * 
         * @param signature The shared access signature used to authorize requests. 
         * @param signatureEncoder An optional function which encodes the {@code signature}. 
         * @throws NullPointerException If {@code signature} is {@code null}. 
         * @throws IllegalArgumentException If {@code signature} is an empty string. 
         */ 
        public AzureSasCredential(String signature, Function<String, String> signatureEncoder) 
        /** 
         * Retrieves the shared access signature associated to this credential. 
         * 
         * @return The shared access signature being used to authorize requests. 
         */ 
        public String getSignature() 
        /** 
         * Rotates the shared access signature associated to this credential. 
         * 
         * @param signature The new shared access signature to be associated with this credential. 
         * @return The updated {@code AzureSasCredential} object. 
         * @throws NullPointerException If {@code signature} is {@code null}. 
         * @throws IllegalArgumentException If {@code signature} is an empty string. 
         */ 
        public AzureSasCredential update(String signature) 
    } 
    /** 
     * <p> 
     * The {@link BasicAuthenticationCredential} is used to authenticate and authorize requests made to 
     * Azure services using the Basic authentication scheme. Basic Authentication is a simple authentication scheme 
     * that uses a combination of a username and password. 
     * </p> 
     * 
     * <p> 
     * Note that Basic Authentication is generally considered less secure than other authentication methods, 
     * such as Azure Active Directory (AAD) authentication. It is recommended to use 
     * <a href="https://learn.microsoft.com/azure/active-directory/fundamentals/">Azure Active Directory (Azure AD)</a> 
     * authentication via {@link TokenCredential} whenever possible, especially for production environments. 
     * </p> 
     * 
     * <p> 
     * <strong>Sample: Azure SAS Authentication</strong> 
     * </p> 
     * 
     * <p> 
     * The following code sample demonstrates the creation of a 
     * {@link com.azure.core.credential.BasicAuthenticationCredential}, using username and password 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.credential.basicAuthenticationCredential --> 
     * <pre> 
     * BasicAuthenticationCredential basicAuthenticationCredential = 
     *     new BasicAuthenticationCredential("<username>", "<password>"); 
     * </pre> 
     * <!-- end com.azure.core.credential.basicAuthenticationCredential --> 
     * 
     * @see com.azure.core.credential 
     * @see com.azure.core.credential.TokenCredential 
     */ 
    public class BasicAuthenticationCredential implements TokenCredential { 
        /** 
         * Creates a basic authentication credential. 
         * 
         * @param username basic auth username 
         * @param password basic auth password 
         */ 
        public BasicAuthenticationCredential(String username, String password) 
        @Override public Mono<AccessToken> getToken(TokenRequestContext request) 
        @Override public AccessToken getTokenSync(TokenRequestContext request) 
    } 
    /** 
     * Represents a credential that uses a key to authenticate. 
     */ 
    public class KeyCredential { 
        /** 
         * Creates a credential that authorizes request with the given key. 
         * 
         * @param key The key used to authorize requests. 
         * @throws NullPointerException If {@code key} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} is an empty string. 
         */ 
        public KeyCredential(String key) 
        /** 
         * Retrieves the key associated to this credential. 
         * 
         * @return The key being used to authorize requests. 
         */ 
        public String getKey() 
        /** 
         * Rotates the key associated to this credential. 
         * 
         * @param key The new key to associated with this credential. 
         * @return The updated {@code KeyCredential} object. 
         * @throws NullPointerException If {@code key} is {@code null}. 
         * @throws IllegalArgumentException If {@code key} is an empty string. 
         */ 
        public KeyCredential update(String key) 
    } 
    /** 
     * Specifies Options for Pop Token authentication. 
     */ 
    public class ProofOfPossessionOptions { 
        /** 
         * Creates a new instance of {@link ProofOfPossessionOptions}. 
         */ 
        public ProofOfPossessionOptions() 
        /** 
         * Gets the proof of possession nonce. 
         * 
         * @return A string representing the proof of possession nonce. 
         */ 
        public String getProofOfPossessionNonce() 
        /** 
         * Sets the proof of possession nonce. 
         * 
         * @param proofOfPossessionNonce A string representing the proof of possession nonce. 
         * @return The updated instance of ProofOfPossessionOptions. 
         */ 
        public ProofOfPossessionOptions setProofOfPossessionNonce(String proofOfPossessionNonce) 
        /** 
         * Gets the request method. 
         * 
         * @return An HttpMethod representing the request method. 
         */ 
        public HttpMethod getRequestMethod() 
        /** 
         * Sets the request method. 
         * 
         * @param requestMethod An HttpMethod representing the request method. 
         * @return The updated instance of ProofOfPossessionOptions. 
         */ 
        public ProofOfPossessionOptions setRequestMethod(HttpMethod requestMethod) 
        /** 
         * Gets the request URL. 
         * 
         * @return A URL representing the request URL. 
         */ 
        public URL getRequestUrl() 
        /** 
         * Sets the request URL. 
         * 
         * @param requestUrl A URL representing the request URL. 
         * @return The updated instance of ProofOfPossessionOptions. 
         */ 
        public ProofOfPossessionOptions setRequestUrl(URL requestUrl) 
    } 
    /** 
     * <p> 
     * The Simple Token Cache offers a basic in-memory token caching mechanism. It is designed to help improve 
     * performance and reduce the number of token requests made to Azure services during application runtime. 
     * </p> 
     * 
     * <p> 
     * When using Azure services that require authentication, such as Azure Storage or Azure Key Vault, the library 
     * handles the acquisition and management of access tokens. By default, each request made to an Azure service triggers 
     * a token request, which involves authentication and token retrieval from the authentication provider 
     * (e.g., Azure Active Directory). 
     * </p> 
     * 
     * <p> 
     * The Simple Token Cache feature caches the access tokens retrieved from the authentication provider in memory 
     * for a certain period. This caching mechanism helps reduce the overhead of repeated token requests, especially when 
     * multiple requests are made within a short time frame. 
     * </p> 
     * 
     * <p> 
     * The Simple Token Cache is designed for simplicity and ease of use. It automatically handles token expiration 
     * and refreshing. When a cached token is about to expire, the SDK automatically attempts to refresh it by requesting 
     * a new token from the authentication provider. The cached tokens are associated with a specific Azure resource or 
     * scope and are used for subsequent requests to that resource. 
     * </p> 
     * 
     * <p> 
     * <strong>Sample: Azure SAS Authentication</strong> 
     * </p> 
     * 
     * <p> 
     * The following code sample demonstrates the creation of a {@link com.azure.core.credential.SimpleTokenCache}. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.credential.simpleTokenCache --> 
     * <pre> 
     * SimpleTokenCache simpleTokenCache = 
     *     new SimpleTokenCache(() -> { 
     *         // Your logic to retrieve access token goes here. 
     *         return Mono.just(new AccessToken("dummy-token", OffsetDateTime.now().plusHours(2))); 
     *     }); 
     * </pre> 
     * <!-- end com.azure.core.credential.simpleTokenCache --> 
     * 
     * @see com.azure.core.credential 
     * @see com.azure.core.credential.TokenCredential 
     */ 
    public class SimpleTokenCache { 
        /** 
         * Creates an instance of RefreshableTokenCredential with default scheme "Bearer". 
         * 
         * @param tokenSupplier a method to get a new token 
         */ 
        public SimpleTokenCache(Supplier<Mono<AccessToken>> tokenSupplier) 
        /** 
         * Asynchronously get a token from either the cache or replenish the cache with a new token. 
         * @return a Publisher that emits an AccessToken 
         */ 
        public Mono<AccessToken> getToken() 
    } 
    @FunctionalInterface
    /** 
     * <p> 
     * Token Credential interface serves as a fundamental component for managing and providing access tokens required for 
     * <a href="https://learn.microsoft.com/azure/active-directory/fundamentals/">Azure Active Directory (Azure AD)</a> 
     * authentication when making requests to Azure services. 
     * </p> 
     * 
     * <p> 
     * The {@link TokenCredential} interface, offers {@link TokenCredential#getToken(TokenRequestContext)} 
     * and {@link TokenCredential#getTokenSync(TokenRequestContext)} methods. These methods are responsible for 
     * retrieving an access token that can be used to authenticate requests to Azure services. The scopes parameter 
     * specified as part of {@link TokenRequestContext} represents the resources or permissions required for the 
     * token. 
     * </p> 
     * 
     * <p> 
     * The Token Credential interface is implemented by various credential classes in the 
     * <a href="https://learn.microsoft.com/java/api/overview/azure/identity-readme?view=azure-java-stable">Azure 
     * Identity</a> 
     * library. These credential classes handle the authentication process and provide the necessary access tokens based on 
     * the specified scopes and any additional configuration. 
     * </p> 
     * 
     * <p> 
     * By utilizing the Token Credential interface, you can abstract the authentication logic away from your 
     * application code. This allows for flexibility in choosing authentication mechanisms and simplifies the management 
     * of access tokens, including token caching and refreshing. It provides a consistent approach to authenticate requests 
     * across different Azure services and libraries. 
     * </p> 
     * 
     * <p> 
     * Here are some examples of credential classes that implement the Token Credential interface: 
     * </p> 
     * 
     * <ul> 
     * <li><a href= 
     * "https://learn.microsoft.com/java/api/com.azure.identity.defaultazurecredential?view=azure-java-stable">DefaultAzureCredential</a>: 
     * Represents a credential that tries a series of authentication methods to 
     * authenticate requests automatically. It simplifies the process by automatically selecting an appropriate 
     * authentication mechanism based on the environment, such as environment variables, managed identities, and 
     * developer tool credentials.</li> 
     * 
     * <li><a href= 
     * "https://learn.microsoft.com/java/api/com.azure.identity.clientsecretcredential?view=azure-java-stable">ClientSecretCredential</a>: 
     * Represents a credential that uses a client ID, client secret, and tenant 
     * ID to authenticate. It is suitable for scenarios where you have a client application that needs to authenticate 
     * with Azure services using a client secret.</li> 
     * 
     * <li><a href= 
     * "https://learn.microsoft.com/java/api/com.azure.identity.clientcertificatecredential?view=azure-java-stable">ClientCertificateCredential</a>: 
     * Represents a credential that uses a client ID, client certificate, and 
     * tenant ID for authentication. This credential is useful when your client application has a client certificate 
     * available for authentication.</li> 
     * 
     * <li><a href= 
     * "https://learn.microsoft.com/java/api/com.azure.identity.interactivebrowsercredential?view=azure-java-stable">InteractiveBrowserCredential</a>: 
     * Represents a credential that performs an interactive authentication 
     * flow with the user in a browser. It is useful for scenarios where the user needs to provide consent or 
     * multi-factor authentication is required.</li> 
     * </ul> 
     * 
     * <p> 
     * You can find more credential classes that implement the {@link TokenCredential} interface in our 
     * <a href="https://learn.microsoft.com/java/api/overview/azure/identity-readme?view=azure-java-stable">Azure 
     * Identity</a> 
     * library. 
     * </p> 
     * 
     * <p> 
     * These credential classes can be used in combination with various Azure client libraries to authenticate requests 
     * and access Azure services without the need to manage access tokens manually. The Token Credential interface provides 
     * a consistent way to handle Azure Active Directory (AAD) authentication across different Azure services and SDKs in 
     * a secure and efficient manner. 
     * </p> 
     * 
     * @see com.azure.core.credential 
     */ 
    public interface TokenCredential { 
        /** 
         * Asynchronously get a token for a given resource/audience. 
         * <p> 
         * This method is called automatically by Azure SDK client libraries. 
         * <p> 
         * You may call this method directly, but you must also handle token caching and token refreshing. 
         * 
         * @param request the details of the token request 
         * @return a Publisher that emits a single access token 
         */ 
        Mono<AccessToken> getToken(TokenRequestContext request) 
        /** 
         * Synchronously get a token for a given resource/audience. 
         * <p> 
         * This method is called automatically by Azure SDK client libraries. 
         * <p> 
         * You may call this method directly, but you must also handle token caching and token refreshing. 
         * 
         * @param request the details of the token request 
         * @return The Access Token 
         */ 
        default AccessToken getTokenSync(TokenRequestContext request) 
    } 
    /** 
     * <p> 
     * The {@link TokenRequestContext} is a class used to provide additional information and context when requesting an 
     * access token from an authentication source. It allows you to customize the token request and specify additional 
     * parameters, such as scopes, claims, or authentication options. 
     * </p> 
     * 
     * <p> 
     * The {@link TokenRequestContext} is typically used with authentication mechanisms that require more advanced 
     * configurations or options, such as 
     * <a href="https://learn.microsoft.com/azure/active-directory/fundamentals/">Azure Active Directory (Azure AD)</a> 
     * authentication. 
     * </p> 
     * 
     * <p> 
     * Here's a high-level overview of how you can use the {@link TokenRequestContext}: 
     * </p> 
     * 
     * <ol> 
     * <li>Create an instance of the {@link TokenRequestContext} class and configure the required properties. 
     * The {@link TokenRequestContext} class allows you to specify the scopes or resources for which you want to request 
     * an access token, as well as any additional claims or options.</li> 
     * 
     * <li>Pass the TokenRequestContext instance to the appropriate authentication client or mechanism when 
     * requesting an access token. The specific method or API to do this will depend on the authentication mechanism 
     * you are using. For example, if you are using Azure Identity for AAD authentication, you would pass the 
     * TokenRequestContext instance to the getToken method of the {@link TokenCredential} implementation.</li> 
     * 
     * <li>The authentication client or mechanism will handle the token request and return an access token that can 
     * be used to authenticate and authorize requests to Azure services.</li> 
     * </ol> 
     * 
     * @see com.azure.core.credential 
     * @see com.azure.core.credential.TokenCredential 
     */ 
    public class TokenRequestContext { 
        /** 
         * Creates a token request instance. 
         */ 
        public TokenRequestContext() 
        /** 
         * Adds one or more scopes to the request scopes. 
         * @param scopes one or more scopes to add 
         * @return the TokenRequestContext itself 
         */ 
        public TokenRequestContext addScopes(String... scopes) 
        /** 
         * Get the status indicating whether Continuous Access Evaluation (CAE) is enabled for the requested token. 
         * 
         * @return the flag indicating whether CAE authentication should be used or not. 
         */ 
        public boolean isCaeEnabled() 
        /** 
         * Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token. 
         * 
         * <p> If a resource API implements CAE and your application declares it can handle CAE, your app receives 
         * CAE tokens for that resource. For this reason, if you declare your app CAE ready, your application must handle 
         * the CAE claim challenge for all resource APIs that accept Microsoft Identity access tokens. If you don't handle 
         * CAE responses in these API calls, your app could end up in a loop retrying an API call with a token that is 
         * still in the returned lifespan of the token but has been revoked due to CAE.</p> 
         * 
         * @param enableCae the flag indicating whether to enable Continuous Access Evaluation (CAE) for 
         * the requested token. 
         * @return the updated TokenRequestContext. 
         */ 
        public TokenRequestContext setCaeEnabled(boolean enableCae) 
        /** 
         * Get the additional claims to be included in the token. 
         * 
         * @see <a href="https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter"> 
         *     https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter</a> 
         * 
         * @return the additional claims to be included in the token. 
         */ 
        public String getClaims() 
        /** 
         * Set the additional claims to be included in the token. 
         * 
         * @see <a href="https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter"> 
         *     https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter</a> 
         * 
         * @param claims the additional claims to be included in the token. 
         * @return the updated TokenRequestContext itself 
         */ 
        public TokenRequestContext setClaims(String claims) 
        /** 
         * Gets the proof of possession options. 
         * 
         * @return The current instance of ProofOfPossessionOptions. 
         */ 
        public ProofOfPossessionOptions getProofOfPossessionOptions() 
        /** 
         * Sets the proof of possession options. 
         * 
         * @param proofOfPossessionOptions An instance of ProofOfPossessionOptions to be set. 
         * @return The updated instance of TokenRequestContext. 
         */ 
        public TokenRequestContext setProofOfPossessionOptions(ProofOfPossessionOptions proofOfPossessionOptions) 
        /** 
         * Gets the scopes required for the token. 
         * @return the scopes required for the token 
         */ 
        public List<String> getScopes() 
        /** 
         * Sets the scopes required for the token. 
         * @param scopes the scopes required for the token 
         * @return the TokenRequestContext itself 
         */ 
        public TokenRequestContext setScopes(List<String> scopes) 
        /** 
         * Get the tenant id to be used for the authentication request. 
         * 
         * @return the configured tenant id. 
         */ 
        public String getTenantId() 
        /** 
         * Set the tenant id to be used for the authentication request. 
         * 
         * @param tenantId the tenant to be used when requesting the token. 
         * @return the updated TokenRequestContext itself 
         */ 
        public TokenRequestContext setTenantId(String tenantId) 
    } 
} 
/** 
 * <p>This package contains cryptography interfaces for Azure SDK client libraries. These interfaces allow client 
 * libraries to perform cryptographic operations using asymmetric and symmetric keys, such as encrypting, decrypting, 
 * signing, verifying, wrapping, and unwrapping keys. The package also provides classes that can resolve key 
 * encryption keys from a given key identifier.</p> 
 * 
 * <p>Some of the key concepts and features of the cryptography package are:</p> 
 * 
 * <ul> 
 *     <li><strong>Async Key Encryption Key and Key Encryption Key interfaces</strong>: These interfaces define the 
 *     methods for encrypting and decrypting keys, also known as key wrapping and unwrapping. They also support signing 
 *     and verifying data using the configured key.</li> 
 * 
 *     <li><strong>Async Key Encryption Key Resolver and Key Encryption Key Resolver interfaces</strong>: These 
 *     interfaces define the methods for resolving key encryption keys from a given key identifier. They can be used 
 *     to create instances of CryptographyClient.</li> 
 * </ul> 
 * 
 * @see com.azure.core.cryptography.KeyEncryptionKey 
 * @see com.azure.core.cryptography.AsyncKeyEncryptionKey 
 * @see com.azure.core.cryptography.KeyEncryptionKeyResolver 
 * @see com.azure.core.cryptography.AsyncKeyEncryptionKeyResolver 
 */ 
package com.azure.core.cryptography { 
    /** 
     * The AsyncKeyEncryptionKey defines asynchronous methods for encrypting and decrypting keys, also 
     * known as key wrapping and unwrapping. It also supports signing and verifying data using the configured key. 
     */ 
    public interface AsyncKeyEncryptionKey { 
        /** 
         * Retrieves the key identifier. 
         * 
         * @return A {@link Mono} containing key identifier. 
         */ 
        Mono<String> getKeyId() 
        /** 
         * Decrypts the specified encrypted key using the specified algorithm. 
         * 
         * @param algorithm The key wrap algorithm which was used to encrypt the specified encrypted key. 
         * @param encryptedKey The encrypted key content to be decrypted. 
         * @return A {@link Mono} containing the decrypted key bytes. 
         */ 
        Mono<byte[]> unwrapKey(String algorithm, byte[] encryptedKey) 
        /** 
         * Encrypts the specified key using the specified algorithm. 
         * 
         * @param algorithm The key wrap algorithm used to encrypt the specified key. 
         * @param key The key content to be encrypted. 
         * @return A {@link Mono} containing the encrypted key bytes. 
         */ 
        Mono<byte[]> wrapKey(String algorithm, byte[] key) 
    } 
    /** 
     * An object capable of asynchronously retrieving key encryption keys from a provided key identifier. 
     */ 
    public interface AsyncKeyEncryptionKeyResolver { 
        /** 
         * Retrieves the {@link AsyncKeyEncryptionKey} corresponding to the specified {@code keyId} 
         * 
         * @param keyId The key identifier of the key encryption key to retrieve 
         * @return The key encryption key corresponding to the specified {@code keyId} 
         */ 
        Mono<? extends AsyncKeyEncryptionKey> buildAsyncKeyEncryptionKey(String keyId) 
    } 
    /** 
     * A KeyEncryptionKey defines synchronous methods for encrypting and decrypting keys, also 
     * known as key wrapping and unwrapping. It also supports signing and verifying data using the configured key. 
     */ 
    public interface KeyEncryptionKey { 
        /** 
         * Retrieves the key identifier. 
         * 
         * @return The key identifier. 
         */ 
        String getKeyId() 
        /** 
         * Decrypts the specified encrypted key using the specified algorithm. 
         * 
         * @param algorithm The key wrap algorithm which was used to encrypt the specified encrypted key. 
         * @param encryptedKey The encrypted key content to be decrypted. 
         * @return The decrypted key bytes. 
         */ 
        byte[] unwrapKey(String algorithm, byte[] encryptedKey) 
        /** 
         * Encrypts the specified key using the specified algorithm. 
         * 
         * @param algorithm The key wrap algorithm used to encrypt the specified key. 
         * @param key The key content to be encrypted. 
         * @return The encrypted key bytes. 
         */ 
        byte[] wrapKey(String algorithm, byte[] key) 
    } 
    /** 
     * An object capable of synchronously retrieving key encryption keys from a provided key identifier. 
     */ 
    public interface KeyEncryptionKeyResolver { 
        /** 
         * Retrieves the {@link KeyEncryptionKey} corresponding to the specified {@code keyId} 
         * 
         * @param keyId The key identifier of the key encryption key to retrieve 
         * @return The key encryption key corresponding to the specified {@code keyId} 
         */ 
        KeyEncryptionKey buildKeyEncryptionKey(String keyId) 
    } 
} 
/** 
 * <p>This package contains the core exception classes used throughout the Azure SDKs.</p> 
 * 
 * <p>These exceptions are typically thrown in response to errors that occur when interacting with Azure services. 
 * For example, if a network request to an Azure service fails an exception from this package is thrown. 
 * The specific exception that is thrown depends on the nature of the error.</p> 
 * 
 * <p>Here are some of the key exceptions included in this package:</p> 
 * <ul> 
 *     <li>{@link com.azure.core.exception.AzureException}: The base class for all exceptions thrown by Azure SDKs.</li> 
 * 
 *     <li>{@link com.azure.core.exception.HttpRequestException}: Represents an exception thrown when an HTTP request 
 *     fails.</li> 
 * 
 *     <li>{@link com.azure.core.exception.HttpResponseException}: Represents an exception thrown when an unsuccessful 
 *     HTTP response is received from a service request.</li> 
 * 
 *     <li>{@link com.azure.core.exception.ResourceExistsException}: Represents an exception thrown when an HTTP request 
 *     attempts to create a resource that already exists.</li> 
 * 
 *     <li>{@link com.azure.core.exception.ResourceNotFoundException}: Represents an exception thrown when an HTTP 
 *     request attempts to access a resource that does not exist.</li> 
 * </ul> 
 * 
 * <p>Some exceptions (noted in their documentation) include the HTTP request or response that led to the exception.</p> 
 */ 
package com.azure.core.exception { 
    /** 
     * <p>The {@code AzureException} class is the base class for all exceptions thrown by Azure SDKs. 
     * This class extends the {@code RuntimeException} class, which means that it is an unchecked exception.</p> 
     * 
     * <p>Instances of this class or its subclasses are typically thrown in response to errors that occur when interacting 
     * with Azure services. For example, if a network request to an Azure service fails, an {@code AzureException} might be 
     * thrown. The specific subclass of {@code AzureException} that is thrown depends on the nature of the error.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpRequestException 
     * @see com.azure.core.exception.ServiceResponseException 
     * @see com.azure.core.exception.HttpResponseException 
     */ 
    public class AzureException extends RuntimeException { 
        /** 
         * Initializes a new instance of the AzureException class. 
         */ 
        public AzureException() 
        /** 
         * Initializes a new instance of the AzureException class. 
         * 
         * @param message The exception message. 
         */ 
        public AzureException(String message) 
        /** 
         * Initializes a new instance of the AzureException class. 
         * 
         * @param cause The {@link Throwable} which caused the creation of this AzureException. 
         */ 
        public AzureException(Throwable cause) 
        /** 
         * Initializes a new instance of the AzureException class. 
         * 
         * @param message The exception message. 
         * @param cause The {@link Throwable} which caused the creation of this AzureException. 
         */ 
        public AzureException(String message, Throwable cause) 
        /** 
         * Initializes a new instance of the AzureException class. 
         * 
         * @param message The exception message. 
         * @param cause The {@link Throwable} which caused the creation of this AzureException. 
         * @param enableSuppression Whether suppression is enabled or disabled. 
         * @param writableStackTrace Whether the exception stack trace will be filled in. 
         */ 
        public AzureException(String message, Throwable cause, boolean enableSuppression, boolean writableStackTrace) 
    } 
    /** 
     * <p>The {@code ClientAuthenticationException} represents an exception thrown when client authentication fails with 
     * a status code of 4XX, typically 401 unauthorized.</p> 
     * 
     * <p>This exception is thrown in the following scenarios:</p> 
     * 
     * <ul> 
     *     <li>The client did not send the required authorization credentials to access the requested resource, i.e., the 
     *     Authorization HTTP header is missing in the request.</li> 
     * 
     *     <li>The request contains the HTTP Authorization header, but authorization has been refused for the credentials 
     *     contained in the request header.</li> 
     * </ul> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     */ 
    public class ClientAuthenticationException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the {@link ClientAuthenticationException} class. 
         * 
         * @param message The exception message or the response content if a message is not available. 
         * @param response The HTTP response with the authorization failure. 
         */ 
        public ClientAuthenticationException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the {@link ClientAuthenticationException} class. 
         * 
         * @param message The exception message or the response content if a message is not available. 
         * @param response The HTTP response with the authorization failure. 
         * @param value The deserialized HTTP response value. 
         */ 
        public ClientAuthenticationException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the {@link ClientAuthenticationException} class. 
         * 
         * @param message The exception message or the response content if a message is not available. 
         * @param response The HTTP response with the authorization failure. 
         * @param cause The {@link Throwable} which caused the creation of this exception. 
         */ 
        public ClientAuthenticationException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code DecodeException} represents an exception thrown when the HTTP response could not be decoded during 
     * the deserialization process.</p> 
     * 
     * <p>This exception is thrown when the HTTP response received from Azure service is not in the expected format 
     * or structure, causing the deserialization process to fail.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     */ 
    public class DecodeException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the DecodeException class. 
         * 
         * @param message The exception message or the response content if a message is not available. 
         * @param response The HTTP response received from Azure service. 
         */ 
        public DecodeException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the DecodeException class. 
         * 
         * @param message The exception message. 
         * @param response The HTTP response received from Azure service. 
         * @param value The deserialized response value. 
         */ 
        public DecodeException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the DecodeException class. 
         * 
         * @param message The exception message or the response content if a message is not available. 
         * @param response The HTTP response received from Azure service. 
         * @param cause The {@link Throwable} which caused the creation of this exception. 
         */ 
        public DecodeException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code HttpRequestException} that represents an exception thrown when an HTTP request fails.</p> 
     * 
     * <p>This exception is typically thrown when the client sends an HTTP request to the Azure service, but the service 
     * is unable to process the request.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.AzureException 
     * @see com.azure.core.http.HttpRequest 
     */ 
    public class HttpRequestException extends AzureException { 
        /** 
         * Initializes a new instance of the HttpRequestException class. 
         * 
         * @param request The {@link HttpRequest} being sent when the exception occurred. 
         */ 
        public HttpRequestException(HttpRequest request) 
        /** 
         * Initializes a new instance of the HttpRequestException class. 
         * 
         * @param message The exception message. 
         * @param request the HTTP request sends to the Azure service 
         */ 
        public HttpRequestException(String message, HttpRequest request) 
        /** 
         * Initializes a new instance of the HttpRequestException class. 
         * 
         * @param request The {@link HttpRequest} being sent when the exception occurred. 
         * @param cause The {@link Throwable} which caused the creation of this HttpRequestException. 
         */ 
        public HttpRequestException(HttpRequest request, Throwable cause) 
        /** 
         * Initializes a new instance of the HttpRequestException class. 
         * 
         * @param message The exception message. 
         * @param request The {@link HttpRequest} being sent when the exception occurred. 
         * @param cause The {@link Throwable} which caused the creation of this HttpRequestException. 
         */ 
        public HttpRequestException(String message, HttpRequest request, Throwable cause) 
        /** 
         * Initializes a new instance of the HttpRequestException class. 
         * 
         * @param message The exception message. 
         * @param request The {@link HttpRequest} being sent when the exception occurred. 
         * @param cause The {@link Throwable} which caused the creation of this HttpRequestException. 
         * @param enableSuppression Whether suppression is enabled or disabled. 
         * @param writableStackTrace Whether the exception stack trace will be filled in. 
         */ 
        public HttpRequestException(String message, HttpRequest request, Throwable cause, boolean enableSuppression, boolean writableStackTrace) 
        /** 
         * Gets the {@link HttpRequest} being sent when the exception occurred. 
         * 
         * @return The {@link HttpRequest} being sent when the exception occurred. 
         */ 
        public HttpRequest getRequest() 
    } 
    /** 
     * <p>The {@code HttpResponseException} represents an exception thrown when an unsuccessful HTTP response is received 
     * from a service request.</p> 
     * 
     * <p>This exception is typically thrown when the service responds with a non-success status code 
     * (e.g., 3XX, 4XX, 5XX).</p> 
     * 
     * <p>This class also provides methods to get the {@link HttpResponse} that was received when the exception occurred and 
     * the deserialized HTTP response value.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.AzureException 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class HttpResponseException extends AzureException { 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         */ 
        public HttpResponseException(HttpResponse response) 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param message The exception message. 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         */ 
        public HttpResponseException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         * @param cause The {@link Throwable} which caused the creation of this exception. 
         */ 
        public HttpResponseException(HttpResponse response, Throwable cause) 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param message The exception message. 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         * @param value The deserialized response value. 
         */ 
        public HttpResponseException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param message The exception message. 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         * @param cause The {@link Throwable} which caused the creation of this exception. 
         */ 
        public HttpResponseException(String message, HttpResponse response, Throwable cause) 
        /** 
         * Initializes a new instance of the HttpResponseException class. 
         * 
         * @param message The exception message. 
         * @param response The {@link HttpResponse} received that is associated to the exception. 
         * @param cause The {@link Throwable} which caused the creation of this exception. 
         * @param enableSuppression Whether suppression is enabled or disabled. 
         * @param writableStackTrace Whether the exception stack trace will be filled in. 
         */ 
        public HttpResponseException(String message, HttpResponse response, Throwable cause, boolean enableSuppression, boolean writableStackTrace) 
        /** 
         * Gets the {@link HttpResponse} received that is associated to the exception. 
         * 
         * @return The {@link HttpResponse} received that is associated to the exception. 
         */ 
        public HttpResponse getResponse() 
        /** 
         * Gets the deserialized HTTP response value. 
         * 
         * @return The deserialized HTTP response value. 
         */ 
        public Object getValue() 
    } 
    /** 
     * <p>The {@code ResourceExistsException} represents an exception thrown when an HTTP request attempts to create a 
     * resource that already exists.</p> 
     * 
     * <p>This exception is typically thrown when the service responds with a status code of 4XX, 
     * typically 412 conflict.</p> 
     * 
     * <p>This class also provides methods to get the {@link HttpResponse} that was received when the exception occurred and 
     * the deserialized HTTP response value.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ResourceExistsException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the ResourceExistsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         */ 
        public ResourceExistsException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the ResourceExistsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param value the deserialized response value 
         */ 
        public ResourceExistsException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the ResourceExistsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param cause the Throwable which caused the creation of this ResourceExistsException 
         */ 
        public ResourceExistsException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code ResourceModifiedException} represents an exception thrown when an HTTP request attempts to modify a 
     * resource in a way that is not allowed.</p> 
     * 
     * <p>This exception is typically thrown when the service responds with a status code of 4XX, typically 409 Conflict. 
     * This can occur when trying to modify a resource that has been updated by another process, resulting in a conflict.</p> 
     * 
     * <p>This class also provides methods to get the {@link HttpResponse} that was received when the exception occurred and 
     * the deserialized HTTP response value.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ResourceModifiedException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the ResourceModifiedException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         */ 
        public ResourceModifiedException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the ResourceModifiedException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param value the deserialized response value 
         */ 
        public ResourceModifiedException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the ResourceModifiedException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param cause the Throwable which caused the creation of this ResourceModifiedException 
         */ 
        public ResourceModifiedException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code ResourceNotFoundException} represents an exception thrown when an HTTP request attempts to access a 
     * resource that does not exist.</p> 
     * 
     * <p>This exception is typically thrown when the service responds with a status code of 4XX, 
     * typically 404 Not Found.</p> 
     * 
     * <p>This class also provides methods to get the {@link HttpResponse} that was received when the exception occurred and 
     * the deserialized HTTP response value.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ResourceNotFoundException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the ResourceNotFoundException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         */ 
        public ResourceNotFoundException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the ResourceNotFoundException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param value the deserialized response value 
         */ 
        public ResourceNotFoundException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the ResourceNotFoundException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param cause the Throwable which caused the creation of this ResourceNotFoundException 
         */ 
        public ResourceNotFoundException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code ServiceResponseException} represents an exception thrown when the client fails to understand the 
     * service response or the connection times out.</p> 
     * 
     * <p>This exception is typically thrown in the following scenarios:</p> 
     * 
     * <ul> 
     *     <li>The request was sent, but the client failed to understand the response. This could be due to the response 
     *     not being in the expected format, or only a partial response was received.</li> 
     * 
     *     <li>The connection may have timed out. These errors can be retried for idempotent or safe operations.</li> 
     * </ul> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.AzureException 
     */ 
    public class ServiceResponseException extends AzureException { 
        /** 
         * Initializes a new instance of the ServiceResponseException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         */ 
        public ServiceResponseException(String message) 
        /** 
         * Initializes a new instance of the ServiceResponseException class. 
         * 
         * @param message the exception message. 
         * @param cause the Throwable which caused the creation of this ServiceResponseException. 
         */ 
        public ServiceResponseException(String message, Throwable cause) 
    } 
    /** 
     * <p>The {@code TooManyRedirectsException} represents an exception thrown when an HTTP request has reached the 
     * maximum number of redirect attempts.</p> 
     * 
     * <p>This exception is typically thrown when the service responds with a status code of 3XX, indicating multiple 
     * redirections, and the client has exhausted its limit of redirection attempts.</p> 
     * 
     * <p>This class also provides methods to get the {@link HttpResponse} that was received when the exception occurred and 
     * the deserialized HTTP response value.</p> 
     * 
     * @see com.azure.core.exception 
     * @see com.azure.core.exception.HttpResponseException 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class TooManyRedirectsException extends HttpResponseException { 
        /** 
         * Initializes a new instance of the TooManyRedirectsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         */ 
        public TooManyRedirectsException(String message, HttpResponse response) 
        /** 
         * Initializes a new instance of the TooManyRedirectsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param value the deserialized response value 
         */ 
        public TooManyRedirectsException(String message, HttpResponse response, Object value) 
        /** 
         * Initializes a new instance of the TooManyRedirectsException class. 
         * 
         * @param message the exception message or the response content if a message is not available 
         * @param response the HTTP response 
         * @param cause the Throwable which caused the creation of this TooManyRedirectsException 
         */ 
        public TooManyRedirectsException(String message, HttpResponse response, Throwable cause) 
    } 
    /** 
     * <p>The {@code UnexpectedLengthException} represents an exception thrown when the specified input length doesn't 
     * match the actual data length.</p> 
     * 
     * <p>This exception is typically thrown when the number of bytes read from an input source does not match the 
     * expected number of bytes. This could occur when reading data from a file or a network connection.</p> 
     * 
     * <p>This class also provides methods to get the number of bytes read from the input and the number of bytes that were 
     * expected to be read from the input.</p> 
     * 
     * @see com.azure.core.exception 
     * @see java.lang.IllegalStateException 
     */ 
    public final class UnexpectedLengthException extends IllegalStateException { 
        /** 
         * Constructor of the UnexpectedLengthException. 
         * @param message The message for the exception. 
         * @param bytesRead The number of bytes read from resource. 
         * @param bytesExpected The number of bytes expected from the receiver. 
         */ 
        public UnexpectedLengthException(String message, long bytesRead, long bytesExpected) 
        /** 
         * Gets the number of bytes that were expected to be read from the input. 
         * 
         * @return the number of bytes that were expected to be read from the input 
         */ 
        public long getBytesExpected() 
        /** 
         * Gets the number of bytes read from the input. 
         * 
         * @return the number of bytes read from the input 
         */ 
        public long getBytesRead() 
    } 
} 
/** 
 * <p>This package provides HTTP abstractions for Azure SDK client libraries. It serves as a bridge between the 
 * AnnotationParser, RestProxy, and the HTTP client.</p> 
 * 
 * <p>Key features:</p> 
 * <ul> 
 *     <li>AnnotationParser: Interprets annotations on interface definitions and methods to construct HTTP requests.</li> 
 *     <li>RestProxy: Transforms interface definitions into live implementations that convert method invocations into 
 *     network calls.</li> 
 *     <li>HTTP client: Sends HTTP requests and receives responses.</li> 
 * </ul> 
 * 
 * <p>The HTTP pipeline is a series of policies that are invoked to handle an HTTP request. Each policy is a piece of 
 * code that takes an HTTP request, does some processing, and passes the request to the next policy in the pipeline. 
 * The last policy in the pipeline would then actually send the HTTP request.</p> 
 * 
 * <p>Users can create a custom pipeline by creating their own policies and adding them to the pipeline. 
 * Here's a code sample:</p> 
 * 
 * <pre> 
 * HttpPipeline pipeline = new HttpPipelineBuilder() 
 *     .policies(new UserAgentPolicy(), new RetryPolicy()) 
 *     .build(); 
 * </pre> 
 * 
 * <p>This package is crucial for the communication between Azure SDK client libraries and Azure services. 
 * It provides a layer of abstraction over the HTTP protocol, allowing client libraries to focus on service-specific 
 * logic. Custom pipelines can be helpful when you want to customize the behavior of HTTP requests and responses in 
 * some way, such as, to add a custom header to all requests.</p> 
 * 
 * @see com.azure.core.http.HttpClient 
 * @see com.azure.core.http.HttpRequest 
 * @see com.azure.core.http.HttpResponse 
 * @see com.azure.core.http.HttpPipeline 
 * @see com.azure.core.http.HttpHeaders 
 * @see com.azure.core.http.HttpMethod 
 */ 
package com.azure.core.http { 
    /** 
     * <p>This class provides constants for commonly used Content-Type header values in HTTP requests and responses.</p> 
     * 
     * <p>This class is useful when you need to specify the Content-Type header in an HTTP request or check the 
     * Content-Type header in an HTTP response.</p> 
     */ 
    public final class ContentType { 
        /** 
         * the default JSON Content-Type header. 
         */ 
        public static final String APPLICATION_JSON = "application/json"; 
        /** 
         * the default binary Content-Type header. 
         */ 
        public static final String APPLICATION_OCTET_STREAM = "application/octet-stream"; 
        /** 
         * The default form data Content-Type header. 
         */ 
        public static final String APPLICATION_X_WWW_FORM_URLENCODED = "application/x-www-form-urlencoded"; 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
    } 
    @Immutable
    /** 
     * Represents the value of an HTTP Authorization header. 
     * 
     * <p>This class encapsulates the scheme and parameter of an HTTP Authorization header. The scheme represents the 
     * type of authorization being used, and the parameter represents the credentials used for the authorization.</p> 
     * 
     * <p>It provides methods to access these properties. For example, you can use {@link #getScheme()} to get the 
     * scheme of the authorization header, and {@link #getParameter()} to get the credentials of the authorization header.</p> 
     * 
     * <p>This class is useful when you want to work with the Authorization header of an HTTP request or response.</p> 
     */ 
    public final class HttpAuthorization { 
        /** 
         * Constructs a new HttpAuthorization instance. 
         * 
         * @param scheme Scheme component of an authorization header value. 
         * @param parameter The credentials used for the authorization header value. 
         * @throws NullPointerException If either {@code scheme} or {@code parameter} is null. 
         * @throws IllegalArgumentException If either {@code scheme} or {@code parameter} are an empty string. 
         */ 
        public HttpAuthorization(String scheme, String parameter) 
        /** 
         * Gets the credential of the authorization header. 
         * 
         * @return Credential of the authorization header. 
         */ 
        public String getParameter() 
        /** 
         * Gets the scheme of the authorization header. 
         * 
         * @return Scheme of the authorization header. 
         */ 
        public String getScheme() 
        @Override public String toString() 
    } 
    /** 
     * A generic interface for sending HTTP requests and getting responses. 
     */ 
    public interface HttpClient { 
        /** 
         * Creates a new {@link HttpClient} instance. 
         * 
         * @return A new {@link HttpClient} instance. 
         */ 
        static HttpClient createDefault() 
        /** 
         * Creates a new {@link HttpClient} instance. 
         * 
         * @param clientOptions Configuration options applied to the created {@link HttpClient}. 
         * @return A new {@link HttpClient} instance. 
         */ 
        static HttpClient createDefault(HttpClientOptions clientOptions) 
        /** 
         * Send the provided request asynchronously. 
         * 
         * @param request The HTTP request to send. 
         * @return A {@link Mono} that emits the response asynchronously. 
         */ 
        Mono<HttpResponse> send(HttpRequest request) 
        /** 
         * Sends the provided request asynchronously with contextual information. 
         * 
         * @param request The HTTP request to send. 
         * @param context Contextual information about the request. 
         * @return A {@link Mono} that emits the response asynchronously. 
         */ 
        default Mono<HttpResponse> send(HttpRequest request, Context context) 
        /** 
         * Sends the provided request synchronously with contextual information. 
         * 
         * @param request The HTTP request to send. 
         * @param context Contextual information about the request. 
         * @return The response. 
         */ 
        default HttpResponse sendSync(HttpRequest request, Context context) 
    } 
    @FunctionalInterface
    /** 
     * An interface to be implemented by any azure-core plugin that wishes to provide an alternate {@link HttpClient} 
     * implementation. 
     */ 
    public interface HttpClientProvider { 
        /** 
         * Creates a new instance of the {@link HttpClient} that this HttpClientProvider is configured to create. 
         * 
         * @return A new {@link HttpClient} instance, entirely unrelated to all other instances that were created 
         * previously. 
         */ 
        HttpClient createInstance() 
        /** 
         * Creates a new instance of the {@link HttpClient} that this HttpClientProvider is configured to create. 
         * 
         * @param clientOptions Configuration options applied to the created {@link HttpClient}. 
         * @return A new {@link HttpClient} instance, entirely unrelated to all other instances that were created 
         * previously. 
         */ 
        default HttpClient createInstance(HttpClientOptions clientOptions) 
    } 
    /** 
     * Represents a single header within an HTTP request or response. 
     * 
     * <p>This class encapsulates the name and value(s) of an HTTP header. If multiple values are associated with the same 
     * header name, they are stored in a single HttpHeader instance with values separated by commas.</p> 
     * 
     * <p>It provides constructors to create an HttpHeader instance with a single value {@link #HttpHeader(String, String)} 
     * or multiple values {@link #HttpHeader(String, List)}.</p> 
     * 
     * <p>This class is useful when you want to work with individual headers of an HTTP request or response.</p> 
     * 
     * <p>Note: Header names are case-insensitive.</p> 
     */ 
    public class HttpHeader extends Header { 
        /** 
         * Create an HttpHeader instance using the provided name and value. 
         * 
         * @param name the name 
         * @param value the value 
         */ 
        public HttpHeader(String name, String value) 
        /** 
         * Create an HttpHeader instance using the provided name and values, resulting in a single HttpHeader instance with 
         * a single name and multiple values set within it. 
         * 
         * @param name the name 
         * @param values the values 
         */ 
        public HttpHeader(String name, List<String> values) 
    } 
    /** 
     * <p>Represents HTTP header names for multiple versions of HTTP.</p> 
     * 
     * <p>This class encapsulates the name of an HTTP header in a case-insensitive manner. It provides methods to access the 
     * case-sensitive and case-insensitive versions of the header name.</p> 
     * 
     * <p>It also provides constants for commonly used HTTP header names. For example, you can use {@link #CONTENT_TYPE} to get 
     * the Content-Type header name, and {@link #AUTHORIZATION} to get the Authorization header name.</p> 
     * 
     * <p>This class is useful when you want to work with the names of HTTP headers in a case-insensitive manner, or when you 
     * want to use the predefined constants for commonly used HTTP header names.</p> 
     * 
     * <p>Note: This class extends {@link ExpandableStringEnum}, so it can be used in the same way as other expandable string 
     * enums. For example, you can use the {@link #fromString(String)} method to get an instance of this class from a string.</p> 
     */ 
    public final class HttpHeaderName extends ExpandableStringEnum<HttpHeaderName> { 
        /** 
         * {@code Accept}/{@code accept} 
         */ 
        public static final HttpHeaderName ACCEPT = fromString("Accept"); 
        /** 
         * {@code Accept-Charset}/{@code accept-charset} 
         */ 
        public static final HttpHeaderName ACCEPT_CHARSET = fromString("Accept-Charset"); 
        /** 
         * {@code Access-Control-Allow-Credentials}/{@code access-control-allow-credentials} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_ALLOW_CREDENTIALS = fromString("Access-Control-Allow-Credentials"); 
        /** 
         * {@code Access-Control-Allow-Headers}/{@code access-control-allow-headers} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_ALLOW_HEADERS = fromString("Access-Control-Allow-Headers"); 
        /** 
         * {@code Access-Control-Allow-Methods}/{@code access-control-allow-methods} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_ALLOW_METHODS = fromString("Access-Control-Allow-Methods"); 
        /** 
         * {@code Access-Control-Allow-Origin}/{@code access-control-allow-origin} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_ALLOW_ORIGIN = fromString("Access-Control-Allow-Origin"); 
        /** 
         * {@code Access-Control-Expose-Headers}/{@code access-control-expose-headers} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_EXPOSE_HEADERS = fromString("Access-Control-Expose-Headers"); 
        /** 
         * {@code Access-Control-Max-Age}/{@code access-control-max-age} 
         */ 
        public static final HttpHeaderName ACCESS_CONTROL_MAX_AGE = fromString("Access-Control-Max-Age"); 
        /** 
         * {@code Accept-Datetime}/{@code accept-datetime} 
         */ 
        public static final HttpHeaderName ACCEPT_DATETIME = fromString("Accept-Datetime"); 
        /** 
         * {@code Accept-Encoding}/{@code accept-encoding} 
         */ 
        public static final HttpHeaderName ACCEPT_ENCODING = fromString("Accept-Encoding"); 
        /** 
         * {@code Accept-Language}/{@code accept-language} 
         */ 
        public static final HttpHeaderName ACCEPT_LANGUAGE = fromString("Accept-Language"); 
        /** 
         * {@code Accept-Patch}/{@code accept-patch} 
         */ 
        public static final HttpHeaderName ACCEPT_PATCH = fromString("Accept-Patch"); 
        /** 
         * {@code Accept-Ranges}/{@code accept-ranges} 
         */ 
        public static final HttpHeaderName ACCEPT_RANGES = fromString("Accept-Ranges"); 
        /** 
         * {@code Age}/{@code age} 
         */ 
        public static final HttpHeaderName AGE = fromString("Age"); 
        /** 
         * {@code Allow}/{@code allow} 
         */ 
        public static final HttpHeaderName ALLOW = fromString("Allow"); 
        /** 
         * {@code Authorization}/{@code authorization} 
         */ 
        public static final HttpHeaderName AUTHORIZATION = fromString("Authorization"); 
        /** 
         * {@code Azure-AsyncOperation}/{@code azure-azyncoperation} 
         */ 
        public static final HttpHeaderName AZURE_ASYNCOPERATION = fromString("Azure-AsyncOperation"); 
        /** 
         * {@code Cache-Control}/{@code cache-control} 
         */ 
        public static final HttpHeaderName CACHE_CONTROL = fromString("Cache-Control"); 
        /** 
         * {@code Connection}/{@code connection} 
         */ 
        public static final HttpHeaderName CONNECTION = fromString("Connection"); 
        /** 
         * {@code Content-Disposition}/{@code content-disposition} 
         */ 
        public static final HttpHeaderName CONTENT_DISPOSITION = fromString("Content-Disposition"); 
        /** 
         * {@code Content-Encoding}/{@code content-encoding} 
         */ 
        public static final HttpHeaderName CONTENT_ENCODING = fromString("Content-Encoding"); 
        /** 
         * {@code Content-Language}/{@code content-language} 
         */ 
        public static final HttpHeaderName CONTENT_LANGUAGE = fromString("Content-Language"); 
        /** 
         * {@code Content-Length}/{@code content-length} 
         */ 
        public static final HttpHeaderName CONTENT_LENGTH = fromString("Content-Length"); 
        /** 
         * {@code Content-Location}/{@code content-location} 
         */ 
        public static final HttpHeaderName CONTENT_LOCATION = fromString("Content-Location"); 
        /** 
         * {@code Content-MD5}/{@code content-md5} 
         */ 
        public static final HttpHeaderName CONTENT_MD5 = fromString("Content-MD5"); 
        /** 
         * {@code Content-Range}/{@code content-range} 
         */ 
        public static final HttpHeaderName CONTENT_RANGE = fromString("Content-Range"); 
        /** 
         * {@code Content-Type}/{@code content-type} 
         */ 
        public static final HttpHeaderName CONTENT_TYPE = fromString("Content-Type"); 
        /** 
         * {@code Cookie}/{@code cookie} 
         */ 
        public static final HttpHeaderName COOKIE = fromString("Cookie"); 
        /** 
         * {@code Date}/{@code date} 
         */ 
        public static final HttpHeaderName DATE = fromString("Date"); 
        /** 
         * {@code ETag}/{@code etag} 
         */ 
        public static final HttpHeaderName ETAG = fromString("ETag"); 
        /** 
         * {@code Expect}/{@code expect} 
         */ 
        public static final HttpHeaderName EXPECT = fromString("Expect"); 
        /** 
         * {@code Expires}/{@code expires} 
         */ 
        public static final HttpHeaderName EXPIRES = fromString("Expires"); 
        /** 
         * {@code Forwarded}/{@code forwarded} 
         */ 
        public static final HttpHeaderName FORWARDED = fromString("Forwarded"); 
        /** 
         * {@code From}/{@code from} 
         */ 
        public static final HttpHeaderName FROM = fromString("From"); 
        /** 
         * {@code Host}/{@code host} 
         */ 
        public static final HttpHeaderName HOST = fromString("Host"); 
        /** 
         * {@code HTTP2-Settings}/{@code http2-settings} 
         */ 
        public static final HttpHeaderName HTTP2_SETTINGS = fromString("HTTP2-Settings"); 
        /** 
         * {@code If-Match}/{@code if-match} 
         */ 
        public static final HttpHeaderName IF_MATCH = fromString("If-Match"); 
        /** 
         * {@code If-Modified-Since}/{@code if-modified-since} 
         */ 
        public static final HttpHeaderName IF_MODIFIED_SINCE = fromString("If-Modified-Since"); 
        /** 
         * {@code If-None-Match}/{@code if-none-match} 
         */ 
        public static final HttpHeaderName IF_NONE_MATCH = fromString("If-None-Match"); 
        /** 
         * {@code If-Range}/{@code if-range} 
         */ 
        public static final HttpHeaderName IF_RANGE = fromString("If-Range"); 
        /** 
         * {@code If-Unmodified-Since}/{@code if-unmodified-since} 
         */ 
        public static final HttpHeaderName IF_UNMODIFIED_SINCE = fromString("If-Unmodified-Since"); 
        /** 
         * {@code Last-Modified}/{@code last-modified} 
         */ 
        public static final HttpHeaderName LAST_MODIFIED = fromString("Last-Modified"); 
        /** 
         * {@code Link}/{@code link} 
         */ 
        public static final HttpHeaderName LINK = fromString("Link"); 
        /** 
         * {@code Location}/{@code location} 
         */ 
        public static final HttpHeaderName LOCATION = fromString("Location"); 
        /** 
         * {@code Max-Forwards}/{@code max-forwards} 
         */ 
        public static final HttpHeaderName MAX_FORWARDS = fromString("Max-Forwards"); 
        /** 
         * {@code Operation-Location}/{@code operation-location} 
         */ 
        public static final HttpHeaderName OPERATION_LOCATION = fromString("Operation-Location"); 
        /** 
         * {@code Origin}/{@code origin} 
         */ 
        public static final HttpHeaderName ORIGIN = fromString("Origin"); 
        /** 
         * {@code Pragma}/{@code pragma} 
         */ 
        public static final HttpHeaderName PRAGMA = fromString("Pragma"); 
        /** 
         * {@code Prefer}/{@code prefer} 
         */ 
        public static final HttpHeaderName PREFER = fromString("Prefer"); 
        /** 
         * {@code Preference-Applied}/{@code preference-applied} 
         */ 
        public static final HttpHeaderName PREFERENCE_APPLIED = fromString("Preference-Applied"); 
        /** 
         * {@code Proxy-Authenticate}/{@code proxy-authenticate} 
         */ 
        public static final HttpHeaderName PROXY_AUTHENTICATE = fromString("Proxy-Authenticate"); 
        /** 
         * {@code Proxy-Authorization}/{@code proxy-authorization} 
         */ 
        public static final HttpHeaderName PROXY_AUTHORIZATION = fromString("Proxy-Authorization"); 
        /** 
         * {@code Range}/{@code range} 
         */ 
        public static final HttpHeaderName RANGE = fromString("Range"); 
        /** 
         * {@code Referer}/{@code referer} 
         */ 
        public static final HttpHeaderName REFERER = fromString("Referer"); 
        /** 
         * {@code Retry-After}/{@code retry-after} 
         */ 
        public static final HttpHeaderName RETRY_AFTER = fromString("Retry-After"); 
        /** 
         * {@code retry-after-ms} 
         */ 
        public static final HttpHeaderName RETRY_AFTER_MS = fromString("retry-after-ms"); 
        /** 
         * {@code Server}/{@code server} 
         */ 
        public static final HttpHeaderName SERVER = fromString("Server"); 
        /** 
         * {@code Set-Cookie}/{@code set-cookie} 
         */ 
        public static final HttpHeaderName SET_COOKIE = fromString("Set-Cookie"); 
        /** 
         * {@code Strict-Transport-Security}/{@code strict-transport-security} 
         */ 
        public static final HttpHeaderName STRICT_TRANSPORT_SECURITY = fromString("Strict-Transport-Security"); 
        /** 
         * {@code TE}/{@code te} 
         */ 
        public static final HttpHeaderName TE = fromString("TE"); 
        /** 
         * {@code Trailer}/{@code trailer} 
         */ 
        public static final HttpHeaderName TRAILER = fromString("Trailer"); 
        /** 
         * {@code Transfer-Encoding}/{@code transfer-encoding} 
         */ 
        public static final HttpHeaderName TRANSFER_ENCODING = fromString("Transfer-Encoding"); 
        /** 
         * {@code User-Agent}/{@code user-agent} 
         */ 
        public static final HttpHeaderName USER_AGENT = fromString("User-Agent"); 
        /** 
         * {@code Upgrade}/{@code upgrade} 
         */ 
        public static final HttpHeaderName UPGRADE = fromString("Upgrade"); 
        /** 
         * {@code Vary}/{@code vary} 
         */ 
        public static final HttpHeaderName VARY = fromString("Vary"); 
        /** 
         * {@code Via}/{@code via} 
         */ 
        public static final HttpHeaderName VIA = fromString("Via"); 
        /** 
         * {@code Warning}/{@code warning} 
         */ 
        public static final HttpHeaderName WARNING = fromString("Warning"); 
        /** 
         * {@code WWW-Authenticate}/{@code www-authenticate} 
         */ 
        public static final HttpHeaderName WWW_AUTHENTICATE = fromString("WWW-Authenticate"); 
        /** 
         * {@code x-ms-client-request-id} 
         */ 
        public static final HttpHeaderName X_MS_CLIENT_ID = fromString("x-ms-client-id"); 
        /** 
         * {@code x-ms-client-request-id} 
         */ 
        public static final HttpHeaderName X_MS_CLIENT_REQUEST_ID = fromString("x-ms-client-request-id"); 
        /** 
         * {@code x-ms-date} 
         */ 
        public static final HttpHeaderName X_MS_DATE = fromString("x-ms-date"); 
        /** 
         * {@code x-ms-request-id} 
         */ 
        public static final HttpHeaderName X_MS_REQUEST_ID = fromString("x-ms-request-id"); 
        /** 
         * {@code x-ms-retry-after-ms} 
         */ 
        public static final HttpHeaderName X_MS_RETRY_AFTER_MS = fromString("x-ms-retry-after-ms"); 
        /** 
         * {@code traceparent} 
         */ 
        public static final HttpHeaderName TRACEPARENT = fromString("traceparent"); 
        /** 
         * Creates a new instance of {@link HttpHeaderName} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link HttpHeaderName} which doesn't have a String enum 
         * value. 
         * 
         * @deprecated Use one of the constants or the {@link #fromString(String)} factory method. 
         */ 
        @Deprecated public HttpHeaderName() 
        /** 
         * Gets the HTTP header name lower cased. 
         * 
         * @return The HTTP header name lower cased. 
         */ 
        public String getCaseInsensitiveName() 
        /** 
         * Gets the HTTP header name based on the name passed into {@link #fromString(String)}. 
         * 
         * @return The HTTP header name based on the construction of this {@link HttpHeaderName}. 
         */ 
        public String getCaseSensitiveName() 
        @Override public boolean equals(Object obj) 
        /** 
         * Gets or creates the {@link HttpHeaderName} for the passed {@code name}. 
         * <p> 
         * null will be returned if {@code name} is null. 
         * 
         * @param name The name. 
         * @return The HttpHeaderName of the passed name, or null if name was null. 
         */ 
        public static HttpHeaderName fromString(String name) 
        @Override public int hashCode() 
    } 
    /** 
     * <p>Represents a collection of headers on an HTTP request or response.</p> 
     * 
     * <p>This class encapsulates the headers of an HTTP request or response. It provides methods to add, set, get, and 
     * remove headers. It also provides methods to convert the headers to a Map, and to get a Stream representation of the 
     * headers.</p> 
     * 
     * <p>Each header is represented by an {@link HttpHeader} instance, which encapsulates the name and value(s) of a header. 
     * If multiple values are associated with the same header name, they are stored in a single HttpHeader instance 
     * with values separated by commas.</p> 
     * 
     * <p>Note: Header names are case-insensitive.</p> 
     */ 
    public class HttpHeaders implements Iterable<HttpHeader> { 
        /** 
         * Create an empty HttpHeaders instance. 
         */ 
        public HttpHeaders() 
        /** 
         * Create a HttpHeaders instance with the provided initial headers. 
         * 
         * @param headers the map of initial headers 
         */ 
        public HttpHeaders(Map<String, String> headers) 
        /** 
         * Create a HttpHeaders instance with the provided initial headers. 
         * 
         * @param headers the collection of initial headers 
         */ 
        public HttpHeaders(Iterable<HttpHeader> headers) 
        /** 
         * Create a HttpHeaders instance with an initial {@code size} empty headers 
         * 
         * @param initialCapacity the initial capacity of headers map. 
         */ 
        public HttpHeaders(int initialCapacity) 
        /** 
         * Gets the {@link HttpHeader header} for the provided header name. null is returned if the header isn't 
         * found. 
         * 
         * @param name the name of the header to find. 
         * @return the header if found, null otherwise. 
         * @deprecated Use {@link #get(HttpHeaderName)} as it provides better performance. 
         */ 
        @Deprecated public HttpHeader get(String name) 
        /** 
         * Gets the {@link HttpHeader header} for the provided header name. null is returned if the header isn't 
         * found. 
         * 
         * @param name the name of the header to find. 
         * @return the header if found, null otherwise. 
         */ 
        public HttpHeader get(HttpHeaderName name) 
        /** 
         * Sets a {@link HttpHeader header} with the given name and value. If a header with same name already exists then 
         * the value will be overwritten. If the given value is null, the header with the given name will be removed. 
         * 
         * @param name the name to set in the header. If it is null, this method will return with no changes to the 
         * headers. 
         * @param value the value 
         * @return The updated HttpHeaders object 
         * @deprecated Use {@link #set(HttpHeaderName, String)} as it provides better performance. 
         */ 
        @Deprecated public HttpHeaders set(String name, String value) 
        /** 
         * Sets a {@link HttpHeader header} with the given name and value. If a header with same name already exists then 
         * the value will be overwritten. If the given value is null, the header with the given name will be removed. 
         * 
         * @param name the name to set in the header. If it is null, this method will return with no changes to the 
         * headers. 
         * @param value the value 
         * @return The updated HttpHeaders object 
         */ 
        public HttpHeaders set(HttpHeaderName name, String value) 
        /** 
         * Sets a {@link HttpHeader header} with the given name and the list of values provided, such that the given values 
         * will be comma-separated when necessary. If a header with same name already exists then the values will be 
         * overwritten. If the given values list is null, the header with the given name will be removed. 
         * 
         * @param name the name 
         * @param values the values that will be comma-separated as appropriate 
         * @return The updated HttpHeaders object 
         * @deprecated Use {@link #set(HttpHeaderName, List)} as it provides better performance. 
         */ 
        @Deprecated public HttpHeaders set(String name, List<String> values) 
        /** 
         * Sets a {@link HttpHeader header} with the given name and the list of values provided, such that the given values 
         * will be comma-separated when necessary. If a header with same name already exists then the values will be 
         * overwritten. If the given values list is null, the header with the given name will be removed. 
         * 
         * @param name the name 
         * @param values the values that will be comma-separated as appropriate 
         * @return The updated HttpHeaders object 
         */ 
        public HttpHeaders set(HttpHeaderName name, List<String> values) 
        /** 
         * Adds a {@link HttpHeader header} with the given name and value if a header with that name doesn't already exist, 
         * otherwise adds the {@code value} to the existing header. 
         * 
         * @param name The name of the header. 
         * @param value The value of the header. 
         * @return The updated HttpHeaders object. 
         * @deprecated Use {@link #add(HttpHeaderName, String)} as it provides better performance. 
         */ 
        @Deprecated public HttpHeaders add(String name, String value) 
        /** 
         * Adds a {@link HttpHeader header} with the given name and value if a header with that name doesn't already exist, 
         * otherwise adds the {@code value} to the existing header. 
         * 
         * @param name The name of the header. 
         * @param value The value of the header. 
         * @return The updated HttpHeaders object. 
         */ 
        public HttpHeaders add(HttpHeaderName name, String value) 
        /** 
         * Sets all provided header key/values pairs into this HttpHeaders instance. This is equivalent to calling {@code 
         * headers.forEach(this::set)}, and therefore the behavior is as specified in {@link #set(String, List)}. In other 
         * words, this will create a header for each key in the provided map, replacing or removing an existing one, 
         * depending on the value. If the given values list is null, the header with the given name will be removed. If the 
         * given name is already a header, it will be removed and replaced with the headers provided. 
         * <p> 
         * Use {@link #setAllHttpHeaders(HttpHeaders)} if you already have an instance of {@link HttpHeaders} as it provides better 
         * performance. 
         * 
         * @param headers a map containing keys representing header names, and keys representing the associated values. 
         * @return The updated HttpHeaders object 
         * @throws NullPointerException If {@code headers} is null. 
         */ 
        public HttpHeaders setAll(Map<String, List<String>> headers) 
        /** 
         * Sets all headers from the passed {@code headers} into this {@link HttpHeaders}. 
         * <p> 
         * This is the equivalent to calling {@code headers.forEach(header -> set(header.getName(), header.getValuesList())} 
         * and therefore the behavior is as specified in {@link #set(String, List)}. 
         * <p> 
         * If {@code headers} is null this is a no-op. 
         * 
         * @param headers The headers to add into this {@link HttpHeaders}. 
         * @return The updated HttpHeaders object. 
         */ 
        public HttpHeaders setAllHttpHeaders(HttpHeaders headers) 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public Iterator<HttpHeader> iterator() 
        /** 
         * Sets a {@link HttpHeader header} with the given name and value. 
         * 
         * <p>If header with same name already exists then the value will be overwritten.</p> 
         * 
         * @param name the name 
         * @param value the value 
         * @return The updated HttpHeaders object 
         * @deprecated Use {@link #set(HttpHeaderName, String)} instead. 
         */ 
        @Deprecated public HttpHeaders put(String name, String value) 
        /** 
         * Removes the {@link HttpHeader header} with the provided header name. null is returned if the header isn't 
         * found. 
         * 
         * @param name the name of the header to remove. 
         * @return the header if removed, null otherwise. 
         * @deprecated Use {@link #remove(HttpHeaderName)} as it provides better performance. 
         */ 
        @Deprecated public HttpHeader remove(String name) 
        /** 
         * Removes the {@link HttpHeader header} with the provided header name. null is returned if the header isn't 
         * found. 
         * 
         * @param name the name of the header to remove. 
         * @return the header if removed, null otherwise. 
         */ 
        public HttpHeader remove(HttpHeaderName name) 
        /** 
         * Gets the number of headers in the collection. 
         * 
         * @return the number of headers in this collection. 
         */ 
        public int getSize() 
        /** 
         * Get a {@link Stream} representation of the HttpHeader values in this instance. 
         * 
         * @return A {@link Stream} of all header values in this instance. 
         */ 
        public Stream<HttpHeader> stream() 
        /** 
         * Returns a copy of the http headers as an unmodifiable {@link Map} representation of the state of the headers at 
         * the time of the toMap call. This map will not change as the underlying http headers change, and nor will 
         * modifying the key or values contained in the map have any effect on the state of the http headers. 
         * 
         * <p>Note that there may be performance implications of using Map APIs on the returned Map. It is highly 
         * recommended that users prefer to use alternate APIs present on the HttpHeaders class, over using APIs present on 
         * the returned Map class. For example, use the {@link #get(String)} API, rather than {@code 
         * httpHeaders.toMap().get(name)}.</p> 
         * 
         * @return the headers in a copied and unmodifiable form. 
         */ 
        public Map<String, String> toMap() 
        @Override public String toString() 
        /** 
         * Get the value for the provided header name. null is returned if the header name isn't found. 
         * 
         * @param name the name of the header whose value is being retrieved. 
         * @return the value of the header, or null if the header isn't found 
         * @deprecated Use {@link #getValue(HttpHeaderName)} as it provides better performance. 
         */ 
        @Deprecated public String getValue(String name) 
        /** 
         * Get the value for the provided header name. null is returned if the header name isn't found. 
         * 
         * @param name the name of the header whose value is being retrieved. 
         * @return the value of the header, or null if the header isn't found 
         */ 
        public String getValue(HttpHeaderName name) 
        /** 
         * Get the values for the provided header name. null is returned if the header name isn't found. 
         * 
         * <p>This returns {@link #getValue(String) getValue} split by {@code comma}.</p> 
         * 
         * @param name the name of the header whose value is being retrieved. 
         * @return the values of the header, or null if the header isn't found 
         * @deprecated Use {@link #getValue(HttpHeaderName)} as it provides better performance. 
         */ 
        @Deprecated public String[] getValues(String name) 
        /** 
         * Get the values for the provided header name. null is returned if the header name isn't found. 
         * 
         * <p>This returns {@link #getValue(String) getValue} split by {@code comma}.</p> 
         * 
         * @param name the name of the header whose value is being retrieved. 
         * @return the values of the header, or null if the header isn't found 
         */ 
        public String[] getValues(HttpHeaderName name) 
    } 
    /** 
     * Represents the HTTP methods that can be used in a request. 
     * 
     * <p>This enum encapsulates the HTTP methods that can be used in a request, such as GET, PUT, POST, PATCH, DELETE, 
     * HEAD, OPTIONS, TRACE, and CONNECT.</p> 
     * 
     * <p>This enum is useful when you want to specify the HTTP method of a request. For example, you can use it when 
     * creating an instance of {@link HttpRequest}.</p> 
     * 
     * <p>Note: The HTTP methods are defined by the HTTP/1.1 specification (RFC 2616) and 
     * the HTTP/2 specification (RFC 7540).</p> 
     */ 
    public enum HttpMethod { 
        GET, 
            /** 
             * The HTTP GET method. 
             */ 
        PUT, 
            /** 
             * The HTTP PUT method. 
             */ 
        POST, 
            /** 
             * The HTTP POST method. 
             */ 
        PATCH, 
            /** 
             * The HTTP PATCH method. 
             */ 
        DELETE, 
            /** 
             * The HTTP DELETE method. 
             */ 
        HEAD, 
            /** 
             * The HTTP HEAD method. 
             */ 
        OPTIONS, 
            /** 
             * The HTTP OPTIONS method. 
             */ 
        TRACE, 
            /** 
             * The HTTP TRACE method. 
             */ 
        CONNECT; 
            /** 
             * The HTTP CONNECT method. 
             */ 
    } 
    /** 
     * <p>The HTTP pipeline through which HTTP requests and responses flow.</p> 
     * 
     * <p>This class encapsulates the HTTP pipeline that applies a set of {@link HttpPipelinePolicy HttpPipelinePolicies} 
     * to the request before it is sent and on the response as it is being returned.</p> 
     * 
     * <p>It provides methods to get the policy at a specific index in the pipeline, get the count of policies in the 
     * pipeline, get the associated {@link HttpClient}, and send the HTTP request through the pipeline.</p> 
     * 
     * <p>This class is useful when you want to send an HTTP request and apply a set of policies to the request and 
     * response.</p> 
     * 
     * @see HttpPipelinePolicy 
     * @see HttpClient 
     */ 
    public final class HttpPipeline { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Get the {@link HttpClient} associated with the pipeline. 
         * 
         * @return the {@link HttpClient} associated with the pipeline 
         */ 
        public HttpClient getHttpClient() 
        /** 
         * Get the policy at the passed index in the pipeline. 
         * 
         * @param index index of the policy to retrieve. 
         * @return the policy stored at that index. 
         */ 
        public HttpPipelinePolicy getPolicy(int index) 
        /** 
         * Get the count of policies in the pipeline. 
         * 
         * @return count of policies. 
         */ 
        public int getPolicyCount() 
        /** 
         * Wraps the {@code request} in a context and sends it through pipeline. 
         * 
         * @param request The HTTP request to send. 
         * @return A publisher upon subscription flows the context through policies, sends the request, and emits response 
         * upon completion. 
         */ 
        public Mono<HttpResponse> send(HttpRequest request) 
        /** 
         * Sends the context (containing an HTTP request) through pipeline. 
         * 
         * @param context The request context. 
         * @return A publisher upon subscription flows the context through policies, sends the request and emits response 
         * upon completion. 
         */ 
        public Mono<HttpResponse> send(HttpPipelineCallContext context) 
        /** 
         * Wraps the request in a context with additional metadata and sends it through the pipeline. 
         * 
         * @param request THe HTTP request to send. 
         * @param data Additional metadata to pass along with the request. 
         * @return A publisher upon subscription flows the context through policies, sends the request, and emits response 
         * upon completion. 
         */ 
        public Mono<HttpResponse> send(HttpRequest request, Context data) 
        /** 
         * Wraps the request in a context with additional metadata and sends it through the pipeline. 
         * 
         * @param request THe HTTP request to send. 
         * @param data Additional metadata to pass along with the request. 
         * @return A publisher upon subscription flows the context through policies, sends the request, and emits response 
         * upon completion. 
         */ 
        public HttpResponse sendSync(HttpRequest request, Context data) 
        /** 
         * Get the {@link Tracer} associated with the pipeline. 
         * 
         * @return the {@link Tracer} associated with the pipeline 
         */ 
        public Tracer getTracer() 
    } 
    /** 
     * This class provides a fluent builder API to help aid the configuration and instantiation of the {@link HttpPipeline}, 
     * calling {@link HttpPipelineBuilder#build() build} constructs an instance of the pipeline. 
     * 
     * <p> 
     * A pipeline is configured with a HttpClient that sends the request, if no client is set a default is used. 
     * A pipeline may be configured with a list of policies that are applied to each request. 
     * </p> 
     * 
     * <p> 
     * <strong>Code Samples</strong> 
     * </p> 
     * 
     * <p> 
     * Create a pipeline without configuration 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.HttpPipelineBuilder.noConfiguration --> 
     * <pre> 
     * HttpPipeline pipeline = new HttpPipelineBuilder().build(); 
     * </pre> 
     * <!-- end com.azure.core.http.HttpPipelineBuilder.noConfiguration --> 
     * 
     * <p> 
     * Create a pipeline using the default HTTP client and a retry policy 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.HttpPipelineBuilder.defaultHttpClientWithRetryPolicy --> 
     * <pre> 
     * HttpPipeline pipeline = new HttpPipelineBuilder() 
     *     .httpClient(HttpClient.createDefault()) 
     *     .policies(new RetryPolicy()) 
     *     .build(); 
     * </pre> 
     * <!-- end com.azure.core.http.HttpPipelineBuilder.defaultHttpClientWithRetryPolicy --> 
     * 
     * @see HttpPipeline 
     */ 
    public class HttpPipelineBuilder { 
        /** 
         * Creates a new instance of HttpPipelineBuilder that can configure options for the {@link HttpPipeline} before 
         * creating an instance of it. 
         */ 
        public HttpPipelineBuilder() 
        /** 
         * Sets the ClientOptions that will configure the pipeline. 
         * 
         * @param clientOptions The ClientOptions that will configure the pipeline. 
         * @return The updated HttpPipelineBuilder object. 
         */ 
        public HttpPipelineBuilder clientOptions(ClientOptions clientOptions) 
        /** 
         * Sets the HttpClient that the pipeline will use to send requests. 
         * 
         * @param httpClient The HttpClient the pipeline will use when sending requests. 
         * @return The updated HttpPipelineBuilder object. 
         */ 
        public HttpPipelineBuilder httpClient(HttpClient httpClient) 
        /** 
         * Adds {@link HttpPipelinePolicy policies} to the set of policies that the pipeline will use when sending 
         * requests. 
         * 
         * @param policies Policies to add to the policy set. 
         * @return The updated HttpPipelineBuilder object. 
         */ 
        public HttpPipelineBuilder policies(HttpPipelinePolicy... policies) 
        /** 
         * Sets the Tracer to trace logical and HTTP calls. 
         * 
         * @param tracer The Tracer instance. 
         * @return The updated HttpPipelineBuilder object. 
         */ 
        public HttpPipelineBuilder tracer(Tracer tracer) 
        /** 
         * Creates an {@link HttpPipeline} based on options set in the builder. Every time {@code build()} is called, a new 
         * instance of {@link HttpPipeline} is created. 
         * <p> 
         * If HttpClient is not set then a default HttpClient is used. 
         * 
         * @return A HttpPipeline with the options set from the builder. 
         */ 
        public HttpPipeline build() 
    } 
    /** 
     * <p>Represents the context for a single HTTP request in the HTTP pipeline.</p> 
     * 
     * <p>This class encapsulates the HTTP request and the associated context data. The context data is a key-value store 
     * that can be used to pass additional information along with the HTTP request.</p> 
     * 
     * <p>It provides methods to get and set the HTTP request, get the context data, and get and set data in the context 
     * using a key.</p> 
     * 
     * <p>This class is useful when you want to send an HTTP request through the HTTP pipeline and need to associate 
     * additional data with the request.</p> 
     * 
     * @see HttpRequest 
     * @see Context 
     */ 
    public final class HttpPipelineCallContext { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         *  Gets the context associated to the HTTP call. 
         * 
         *  <p> 
         *  The returned context is a snapshot of the data stored in this http pipeline call context. 
         *  </p> 
         * 
         * @return The context associated to the HTTP call. 
         */ 
        public Context getContext() 
        /** 
         * Gets a value with the given key stored in the context. 
         * 
         * @param key The key to find in the context. 
         * @return The value associated with the key. 
         */ 
        public Optional<Object> getData(String key) 
        /** 
         * Stores a key-value data in the context. 
         * 
         * @param key The key to add. 
         * @param value The value to associate with that key. 
         */ 
        public void setData(String key, Object value) 
        /** 
         * Gets the HTTP request. 
         * 
         * @return The HTTP request. 
         */ 
        public HttpRequest getHttpRequest() 
        /** 
         * Sets the HTTP request object in the context. 
         * 
         * @param request The HTTP request. 
         * @return The updated HttpPipelineCallContext object. 
         */ 
        public HttpPipelineCallContext setHttpRequest(HttpRequest request) 
    } 
    /** 
     * <p>A class that invokes the next policy in the HTTP pipeline.</p> 
     * 
     * <p>This class encapsulates the state of the HTTP pipeline call and provides a method to process the next policy in 
     * the pipeline.</p> 
     * 
     * <p>It provides methods to process the next policy and clone the current instance of the next pipeline policy.</p> 
     * 
     * <p>This class is useful when you want to send an HTTP request through the HTTP pipeline and need to process the 
     * next policy in the pipeline.</p> 
     * 
     * @see HttpPipelinePolicy 
     * @see HttpPipelineCallState 
     */ 
    public class HttpPipelineNextPolicy { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates a new instance of this instance. 
         * 
         * @return A new instance of this next pipeline policy. 
         */ 
        @Override public HttpPipelineNextPolicy clone() 
        /** 
         * Invokes the next {@link HttpPipelinePolicy}. 
         * 
         * @return A publisher which upon subscription invokes next policy and emits response from the policy. 
         */ 
        public Mono<HttpResponse> process() 
    } 
    /** 
     * <p>A class that invokes the next policy in the HTTP pipeline in a synchronous manner.</p> 
     * 
     * <p>This class encapsulates the state of the HTTP pipeline call and provides a method to process the next policy in 
     * the pipeline synchronously.</p> 
     * 
     * <p>It provides methods to process the next policy and clone the current instance of the next pipeline policy.</p> 
     * 
     * <p>This class is useful when you want to send an HTTP request through the HTTP pipeline and need to process the 
     * next policy in the pipeline in a synchronous manner.</p> 
     * 
     * @see HttpPipelinePolicy 
     * @see HttpPipelineCallState 
     */ 
    public class HttpPipelineNextSyncPolicy { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates a new instance of this instance. 
         * 
         * @return A new instance of this next pipeline sync policy. 
         */ 
        @Override public HttpPipelineNextSyncPolicy clone() 
        /** 
         * Invokes the next {@link HttpPipelinePolicy}. 
         * 
         * @return The response. 
         */ 
        public HttpResponse processSync() 
    } 
    /** 
     * Enum representing the position in an {@link HttpPipeline} to place an {@link HttpPipelinePolicy}. 
     * 
     * <p>This enum encapsulates the positions where an HTTP pipeline policy can be placed in the HTTP pipeline. The positions 
     * are before or after a {@link RetryPolicy}.</p> 
     * 
     * <p>Each position is represented by an enum constant. For example, you can use {@link #PER_CALL} to represent the position 
     * before a RetryPolicy, and {@link #PER_RETRY} to represent the position after a RetryPolicy.</p> 
     * 
     * <p>The PER_CALL position means that the policy will only be 
     * invoked once per pipeline invocation (service call), and the PER_RETRY position means that the policy will be invoked 
     * every time a request is sent (including retries).</p> 
     */ 
    public enum HttpPipelinePosition { 
        PER_CALL, 
            /** 
             * Policy is placed before a {@link RetryPolicy} and will only be invoked once per pipeline invocation (service 
             * call). 
             */ 
        PER_RETRY; 
            /** 
             * Policy is placed after a {@link RetryPolicy} and will be invoked every time a request is sent. 
             * <p> 
             * The policy will be invoked at least once for the initial service call and each time the request is retried. 
             */ 
    } 
    @Immutable
    /** 
     * <p>Represents a range of bytes within an HTTP resource.</p> 
     * 
     * <p>This class encapsulates a range of bytes that can be requested from an HTTP resource. The range starts at the 
     * {@link #getOffset()} inclusively and ends at {@link #getOffset()} + {@link #getLength()} exclusively, or offset + length - 1.</p> 
     * 
     * <p>If {@link #getLength()} is unspecified or null, the range extends to the end of the HTTP resource.</p> 
     * 
     * <p>This class is useful when you want to request a specific range of bytes from an HTTP resource, such as a part of a file. 
     * For example, you can use it to download a part of a file, to resume a download, or to stream a video from a specific point.</p> 
     * 
     * @see HttpRequest 
     */ 
    public final class HttpRange { 
        /** 
         * Creates an instance of {@link HttpRange}. 
         * <p> 
         * This creates a range which has an unbounded length starting at the specified {@code offset}. 
         * 
         * @param offset The offset to begin the range. 
         * @throws IllegalArgumentException If {@code offset} is less than 0. 
         */ 
        public HttpRange(long offset) 
        /** 
         * Creates an instance of {@link HttpRange}. 
         * 
         * @param offset The offset to begin the range. 
         * @param length The length of the range. 
         * @throws IllegalArgumentException If {@code offset} is less than 0 or {@code length} is non-null and is less than 
         * or equal to 0. 
         */ 
        public HttpRange(long offset, Long length) 
        @Override public boolean equals(Object obj) 
        @Override public int hashCode() 
        /** 
         * Gets the length of the range. 
         * <p> 
         * If the length is null the range continues to the end of the HTTP resource. 
         * 
         * @return Length of the range or null if range continues to the end of the HTTP resource. 
         */ 
        public Long getLength() 
        /** 
         * Gets the offset of the range. 
         * 
         * @return Offset of the range. 
         */ 
        public long getOffset() 
        /** 
         * Gets the string representation of the range. 
         * <p> 
         * If length is null the returned string will be {@code "bytes=<offset>-"}, if length is not null the returned 
         * string will be {@code "bytes=<offset>-<offset + length - 1>"}. 
         * 
         * @return The string representation of the range. 
         */ 
        @Override public String toString() 
    } 
    /** 
     * Represents an outgoing HTTP request. 
     * 
     * <p>This class encapsulates an HTTP request, including the HTTP method, URL, headers, and body. It provides methods 
     * to set and get these properties.</p> 
     * 
     * <p>This class is useful when you want to create an HTTP request to send to a server. For example, you can use it to 
     * create a GET request to retrieve a resource, a POST request to create a resource, a PUT request to update a resource, 
     * or a DELETE request to delete a resource.</p> 
     * 
     * <p>Note: This class provides a {@link #copy()} method to create a copy of the HTTP request. This is useful when you 
     * want to modify the HTTP request without affecting the original request.</p> 
     */ 
    public class HttpRequest { 
        /** 
         * Create a new HttpRequest instance. 
         * 
         * @param httpMethod the HTTP request method 
         * @param url the target address to send the request to 
         */ 
        public HttpRequest(HttpMethod httpMethod, URL url) 
        /** 
         * Create a new HttpRequest instance. 
         * 
         * @param httpMethod the HTTP request method 
         * @param url the target address to send the request to 
         * @throws IllegalArgumentException if {@code url} is null or it cannot be parsed into a valid URL. 
         */ 
        public HttpRequest(HttpMethod httpMethod, String url) 
        /** 
         * Create a new HttpRequest instance. 
         * 
         * @param httpMethod the HTTP request method 
         * @param url the target address to send the request to 
         * @param headers the HTTP headers to use with this request 
         */ 
        public HttpRequest(HttpMethod httpMethod, URL url, HttpHeaders headers) 
        /** 
         * Create a new HttpRequest instance. 
         * 
         * @param httpMethod the HTTP request method 
         * @param url the target address to send the request to 
         * @param headers the HTTP headers to use with this request 
         * @param body the request content 
         */ 
        public HttpRequest(HttpMethod httpMethod, URL url, HttpHeaders headers, Flux<ByteBuffer> body) 
        /** 
         * Create a new HttpRequest instance. 
         * 
         * @param httpMethod the HTTP request method 
         * @param url the target address to send the request to 
         * @param headers the HTTP headers to use with this request 
         * @param body the request content 
         */ 
        public HttpRequest(HttpMethod httpMethod, URL url, HttpHeaders headers, BinaryData body) 
        /** 
         * Get the request content. 
         * 
         * @return the content to be sent 
         */ 
        public Flux<ByteBuffer> getBody() 
        /** 
         * Set the request content. 
         * <p> 
         * The Content-Length header will be set based on the given content's length. 
         * 
         * @param content the request content 
         * @return this HttpRequest 
         */ 
        public HttpRequest setBody(String content) 
        /** 
         * Set the request content. 
         * <p> 
         * The Content-Length header will be set based on the given content's length. 
         * 
         * @param content the request content 
         * @return this HttpRequest 
         */ 
        public HttpRequest setBody(byte[] content) 
        /** 
         * Set request content. 
         * <p> 
         * Caller must set the Content-Length header to indicate the length of the content, or use Transfer-Encoding: 
         * chunked. 
         * 
         * @param content the request content 
         * @return this HttpRequest 
         */ 
        public HttpRequest setBody(Flux<ByteBuffer> content) 
        /** 
         * Set request content. 
         * <p> 
         * If provided content has known length, i.e. {@link BinaryData#getLength()} returns non-null then 
         * Content-Length header is updated. Otherwise, 
         * if provided content has unknown length, i.e. {@link BinaryData#getLength()} returns null then 
         * the caller must set the Content-Length header to indicate the length of the content, or use Transfer-Encoding: 
         * chunked. 
         * 
         * @param content the request content 
         * @return this HttpRequest 
         */ 
        public HttpRequest setBody(BinaryData content) 
        /** 
         * Get the request content. 
         * 
         * @return the content to be sent 
         */ 
        public BinaryData getBodyAsBinaryData() 
        /** 
         * Creates a copy of the request. 
         * <p> 
         * The main purpose of this is so that this HttpRequest can be changed and the resulting HttpRequest can be a 
         * backup. This means that the cloned HttpHeaders and body must not be able to change from side effects of this 
         * HttpRequest. 
         * 
         * @return a new HTTP request instance with cloned instances of all mutable properties. 
         */ 
        public HttpRequest copy() 
        /** 
         * Set a request header, replacing any existing value. A null for {@code value} will remove the header if one with 
         * matching name exists. 
         * 
         * @param name the header name 
         * @param value the header value 
         * @return this HttpRequest 
         * @deprecated Use {@link #setHeader(HttpHeaderName, String)} instead as is offers better performance. 
         */ 
        @Deprecated public HttpRequest setHeader(String name, String value) 
        /** 
         * Set a request header, replacing any existing value. A null for {@code value} will remove the header if one with 
         * matching name exists. 
         * 
         * @param headerName the header name 
         * @param value the header value 
         * @return this HttpRequest 
         */ 
        public HttpRequest setHeader(HttpHeaderName headerName, String value) 
        /** 
         * Get the request headers. 
         * 
         * @return headers to be sent 
         */ 
        public HttpHeaders getHeaders() 
        /** 
         * Set the request headers. 
         * 
         * @param headers the set of headers 
         * @return this HttpRequest 
         */ 
        public HttpRequest setHeaders(HttpHeaders headers) 
        /** 
         * Get the request method. 
         * 
         * @return the request method 
         */ 
        public HttpMethod getHttpMethod() 
        /** 
         * Set the request method. 
         * 
         * @param httpMethod the request method 
         * @return this HttpRequest 
         */ 
        public HttpRequest setHttpMethod(HttpMethod httpMethod) 
        /** 
         * Get the target address. 
         * 
         * @return the target address 
         */ 
        public URL getUrl() 
        /** 
         * Set the target address to send the request to. 
         * 
         * @param url target address as {@link URL} 
         * @return this HttpRequest 
         */ 
        public HttpRequest setUrl(URL url) 
        /** 
         * Set the target address to send the request to. 
         * 
         * @param url target address as a String 
         * @return this HttpRequest 
         * @throws IllegalArgumentException if {@code url} is null or it cannot be parsed into a valid URL. 
         */ 
        public HttpRequest setUrl(String url) 
    } 
    /** 
     * <p>Represents an incoming HTTP response.</p> 
     * 
     * <p>This class encapsulates an HTTP response, including the HTTP status code, headers, and body. It provides methods 
     * to get these properties.</p> 
     * 
     * <p>This class is useful when you want to process an HTTP response received from a server. For example, you can use it to 
     * get the status code to check if the request was successful, get the headers to check for any additional information, 
     * and get the body to process the content of the response.</p> 
     * 
     * <p>Note: This class implements {@link Closeable}, so you should call the {@link #close()} method when you're done 
     * with the HTTP response to free any resources associated with it.</p> 
     * 
     * @see HttpRequest 
     * @see HttpHeaders 
     * @see HttpHeader 
     */ 
    public abstract class HttpResponse implements Closeable { 
        /** 
         * Creates an instance of {@link HttpResponse}. 
         * 
         * @param request The {@link HttpRequest} that resulted in this {@link HttpResponse}. 
         */ 
        protected HttpResponse(HttpRequest request) 
        /** 
         * Get the publisher emitting response content chunks. 
         * <p> 
         * Returns a stream of the response's body content. Emissions may occur on Reactor threads which should not be 
         * blocked. Blocking should be avoided as much as possible/practical in reactive programming but if you do use 
         * methods like {@code block()} on the stream then be sure to use {@code publishOn} before the blocking call. 
         * 
         * @return The response's content as a stream of {@link ByteBuffer}. 
         */ 
        public abstract Flux<ByteBuffer> getBody() 
        /** 
         * Gets the {@link BinaryData} that represents the body of the response. 
         * <p> 
         * Subclasses should override this method. 
         * 
         * @return The {@link BinaryData} response body. 
         */ 
        public BinaryData getBodyAsBinaryData() 
        /** 
         * Gets the response content as a {@code byte[]}. 
         * 
         * @return The response content as a {@code byte[]}. 
         */ 
        public abstract Mono<byte[]> getBodyAsByteArray() 
        /** 
         * Gets the response content as an {@link InputStream}. 
         * 
         * @return The response content as an {@link InputStream}. 
         */ 
        public Mono<InputStream> getBodyAsInputStream() 
        /** 
         * Gets the response content as a {@link String}. 
         * <p> 
         * By default, this method will inspect the response body for containing a byte order mark (BOM) to determine the 
         * encoding of the string (UTF-8, UTF-16, etc.). If a BOM isn't found this will default to using UTF-8 as the 
         * encoding, if a specific encoding is required use {@link #getBodyAsString(Charset)}. 
         * 
         * @return The response content as a {@link String}. 
         */ 
        public abstract Mono<String> getBodyAsString() 
        /** 
         * Gets the response content as a {@link String}. 
         * 
         * @param charset The {@link Charset} to use as the string encoding. 
         * @return The response content as a {@link String}. 
         */ 
        public abstract Mono<String> getBodyAsString(Charset charset) 
        /** 
         * Gets a new {@link HttpResponse response} object wrapping this response with its content buffered into memory. 
         * 
         * @return A new {@link HttpResponse response} with the content buffered. 
         */ 
        public HttpResponse buffer() 
        /** 
         * Closes the response content stream, if any. 
         */ 
        @Override public void close() 
        /** 
         * Get all response headers. 
         * 
         * @return the response headers 
         */ 
        public abstract HttpHeaders getHeaders() 
        /** 
         * Lookup a response header with the provided name. 
         * 
         * @param name the name of the header to lookup. 
         * @return the value of the header, or null if the header doesn't exist in the response. 
         * @deprecated Use {@link #getHeaderValue(HttpHeaderName)} as it provides better performance. 
         */ 
        @Deprecated public abstract String getHeaderValue(String name) 
        /** 
         * Lookup a response header with the provider {@link HttpHeaderName}. 
         * 
         * @param headerName the name of the header to lookup. 
         * @return the value of the header, or null if the header doesn't exist in the response. 
         */ 
        public String getHeaderValue(HttpHeaderName headerName) 
        /** 
         * Gets the {@link HttpRequest request} which resulted in this response. 
         * 
         * @return The {@link HttpRequest request} which resulted in this response. 
         */ 
        public final HttpRequest getRequest() 
        /** 
         * Get the response status code. 
         * 
         * @return The response status code 
         */ 
        public abstract int getStatusCode() 
        /** 
         * Transfers body bytes to the {@link WritableByteChannel}. 
         * @param channel The destination {@link WritableByteChannel}. 
         * @throws IOException When I/O operation fails. 
         * @throws NullPointerException When {@code channel} is null. 
         */ 
        public void writeBodyTo(WritableByteChannel channel) throws IOException
        /** 
         * Transfers body bytes to the {@link AsynchronousByteChannel}. 
         * @param channel The destination {@link AsynchronousByteChannel}. 
         * @return A {@link Mono} that completes when transfer is completed. 
         * @throws NullPointerException When {@code channel} is null. 
         */ 
        public Mono<Void> writeBodyToAsync(AsynchronousByteChannel channel) 
    } 
    @Fluent
    /** 
     * <p>Specifies HTTP options for conditional requests based on ETag matching.</p> 
     * 
     * <p>This class encapsulates the ETag conditions that can be used in a request, such as If-Match and If-None-Match.</p> 
     * 
     * <p>This class is useful when you want to create an HTTP request with conditional headers based on ETag matching. For example, 
     * you can use it to create a GET request that only retrieves the resource if it has not been modified (based on the ETag), or a 
     * PUT request that only updates the resource if it has not been modified by another client (based on the ETag).</p> 
     * 
     * @see HttpRequest 
     */ 
    public class MatchConditions { 
        /** 
         * Creates a new instance of {@link MatchConditions}. 
         */ 
        public MatchConditions() 
        /** 
         * Gets the ETag that resources must match. 
         * 
         * @return The ETag that resources must match. 
         */ 
        public String getIfMatch() 
        /** 
         * Optionally limit requests to resources that match the passed ETag. 
         * 
         * @param ifMatch ETag that resources must match. 
         * @return The updated MatchConditions object. 
         */ 
        public MatchConditions setIfMatch(String ifMatch) 
        /** 
         * Gets the ETag that resources must not match. 
         * 
         * @return The ETag that resources must not match. 
         */ 
        public String getIfNoneMatch() 
        /** 
         * Optionally limit requests to resources that do not match the passed ETag. 
         * 
         * @param ifNoneMatch ETag that resources must not match. 
         * @return The updated MatchConditions object. 
         */ 
        public MatchConditions setIfNoneMatch(String ifNoneMatch) 
    } 
    /** 
     * <p>Represents the proxy configuration to be used in HTTP clients.</p> 
     * 
     * <p>This class encapsulates the proxy settings, including the proxy type, address, and optional credentials. It 
     * provides methods to set and get these properties.</p> 
     * 
     * <p>This class is useful when you want to configure a proxy for an HTTP client. For example, you can use it to 
     * create a proxy with specific credentials, or to specify hosts that should bypass the proxy.</p> 
     * 
     * <p>Note: This class provides a {@link Type} enum to represent the proxy type, which can be HTTP, SOCKS4, or SOCKS5.</p> 
     * 
     * @see Type 
     * @see InetSocketAddress 
     */ 
    public class ProxyOptions { 
        /** 
         * Creates ProxyOptions. 
         * 
         * @param type the proxy type 
         * @param address the proxy address (ip and port number) 
         */ 
        public ProxyOptions(Type type, InetSocketAddress address) 
        /** 
         * Gets the address of the proxy. 
         * 
         * @return the address of the proxy. 
         */ 
        public InetSocketAddress getAddress() 
        /** 
         * Set the proxy credentials. 
         * 
         * @param username proxy user name 
         * @param password proxy password 
         * @return the updated ProxyOptions object 
         */ 
        public ProxyOptions setCredentials(String username, String password) 
        /** 
         * Attempts to load a proxy from the configuration. 
         * <p> 
         * If a proxy is found and loaded the proxy address is DNS resolved. 
         * <p> 
         * Environment configurations are loaded in this order: 
         * <ol> 
         *     <li>Azure HTTPS</li> 
         *     <li>Azure HTTP</li> 
         *     <li>Java HTTPS</li> 
         *     <li>Java HTTP</li> 
         * </ol> 
         * 
         * Azure proxy configurations will be preferred over Java proxy configurations as they are more closely scoped to 
         * the purpose of the SDK. Additionally, more secure protocols, HTTPS vs HTTP, will be preferred. 
         * 
         * <p> 
         * {@code null} will be returned if no proxy was found in the environment. 
         * 
         * @param configuration The {@link Configuration} that is used to load proxy configurations from the environment. If 
         * {@code null} is passed then {@link Configuration#getGlobalConfiguration()} will be used. 
         * @return A {@link ProxyOptions} reflecting a proxy loaded from the environment, if no proxy is found {@code null} 
         * will be returned. 
         */ 
        public static ProxyOptions fromConfiguration(Configuration configuration) 
        /** 
         * Attempts to load a proxy from the environment. 
         * <p> 
         * If a proxy is found and loaded, the proxy address is DNS resolved based on {@code createUnresolved}. When {@code 
         * createUnresolved} is true resolving {@link #getAddress()} may be required before using the address in network 
         * calls. 
         * <p> 
         * Environment configurations are loaded in this order: 
         * <ol> 
         *     <li>Azure HTTPS</li> 
         *     <li>Azure HTTP</li> 
         *     <li>Java HTTPS</li> 
         *     <li>Java HTTP</li> 
         * </ol> 
         * 
         * Azure proxy configurations will be preferred over Java proxy configurations as they are more closely scoped to 
         * the purpose of the SDK. Additionally, more secure protocols, HTTPS vs HTTP, will be preferred. 
         * <p> 
         * {@code null} will be returned if no proxy was found in the environment. 
         * 
         * @param configuration The {@link Configuration} that is used to load proxy configurations from the environment. If 
         * {@code null} is passed then {@link Configuration#getGlobalConfiguration()} will be used. If {@link 
         * Configuration#NONE} is passed {@link IllegalArgumentException} will be thrown. 
         * @param createUnresolved Flag determining whether the returned {@link ProxyOptions} is unresolved. 
         * @return A {@link ProxyOptions} reflecting a proxy loaded from the environment, if no proxy is found {@code null} 
         * will be returned. 
         */ 
        public static ProxyOptions fromConfiguration(Configuration configuration, boolean createUnresolved) 
        /** 
         * Gets the host that bypass the proxy. 
         * 
         * @return the hosts that bypass the proxy. 
         */ 
        public String getNonProxyHosts() 
        /** 
         * Sets the hosts which bypass the proxy. 
         * <p> 
         * The expected format of the passed string is a {@code '|'} delimited list of hosts which should bypass the proxy. 
         * Individual host strings may contain regex characters such as {@code '*'}. 
         * 
         * @param nonProxyHosts Hosts that bypass the proxy. 
         * @return the updated ProxyOptions object 
         */ 
        public ProxyOptions setNonProxyHosts(String nonProxyHosts) 
        /** 
         * Gets the proxy password. 
         * 
         * @return the proxy password. 
         */ 
        public String getPassword() 
        /** 
         * Gets the type of the proxy. 
         * 
         * @return the type of the proxy. 
         */ 
        public Type getType() 
        /** 
         * Gets the proxy username. 
         * 
         * @return the proxy username. 
         */ 
        public String getUsername() 
        /** 
         * The type of the proxy. 
         */ 
        public enum Type { 
            HTTP(Proxy.Type.HTTP), 
                /** 
                 * HTTP proxy type. 
                 */ 
            SOCKS4(Proxy.Type.SOCKS), 
                /** 
                 * SOCKS4 proxy type. 
                 */ 
            SOCKS5(Proxy.Type.SOCKS); 
                /** 
                 * SOCKS5 proxy type. 
                 */ 
            /** 
             * Get the {@link Proxy.Type} equivalent of this type. 
             * 
             * @return the proxy type 
             */ 
            public Proxy.Type toProxyType() 
        } 
    } 
    @Fluent
    /** 
     * <p>Specifies HTTP options for conditional requests based on modification time and ETag matching.</p> 
     * 
     * <p>This class extends {@link MatchConditions} and adds conditions based on the modification time of the resource. 
     * It encapsulates conditions such as If-Modified-Since and If-Unmodified-Since, in addition to If-Match and 
     * If-None-Match from {@link MatchConditions}.</p> 
     * 
     * <p>This class is useful when you want to create an HTTP request with conditional headers based on the modification 
     * time of the resource and ETag matching. For example, you can use it to create a GET request that only retrieves the 
     * resource if it has been modified since a specific time, or a PUT request that only updates the resource if it 
     * has not been modified by another client since a specific time.</p> 
     * 
     * @see MatchConditions 
     * @see OffsetDateTime 
     */ 
    public class RequestConditions extends MatchConditions { 
        /** 
         * Creates a new instance of {@link RequestConditions}. 
         */ 
        public RequestConditions() 
        /** 
         * Optionally limit requests to resources that match the passed ETag. 
         * 
         * @param ifMatch ETag that resources must match. 
         * @return The updated ResourceConditions object. 
         */ 
        @Override public RequestConditions setIfMatch(String ifMatch) 
        /** 
         * Gets the {@link OffsetDateTime datetime} that resources must have been modified since. 
         * 
         * @return The datetime that resources must have been modified since. 
         */ 
        public OffsetDateTime getIfModifiedSince() 
        /** 
         * Optionally limit requests to resources that have only been modified since the passed 
         * {@link OffsetDateTime datetime}. 
         * 
         * @param ifModifiedSince The datetime that resources must have been modified since. 
         * @return The updated ResourceConditions object. 
         */ 
        public RequestConditions setIfModifiedSince(OffsetDateTime ifModifiedSince) 
        /** 
         * Optionally limit requests to resources that do not match the passed ETag. 
         * 
         * @param ifNoneMatch ETag that resources must not match. 
         * @return The updated ResourceConditions object. 
         */ 
        @Override public RequestConditions setIfNoneMatch(String ifNoneMatch) 
        /** 
         * Gets the {@link OffsetDateTime datetime} that resources must have remained unmodified since. 
         * 
         * @return The datetime that resources must have remained unmodified since. 
         */ 
        public OffsetDateTime getIfUnmodifiedSince() 
        /** 
         * Optionally limit requests to resources that have remained unmodified since the passed 
         * {@link OffsetDateTime datetime}. 
         * 
         * @param ifUnmodifiedSince The datetime that resources must have remained unmodified since. 
         * @return The updated ResourceConditions object. 
         */ 
        public RequestConditions setIfUnmodifiedSince(OffsetDateTime ifUnmodifiedSince) 
    } 
} 
/** 
 * This package contains the HttpPipelinePolicy interface and its implementations. These policies are used to form an 
 * HTTP pipeline, which is a series of policies that are invoked to handle an HTTP request. 
 * 
 * <p>The HttpPipelinePolicy interface defines process and processSync methods. These 
 * methods transform an HTTP request into an HttpResponse asynchronously and synchronously respectively. 
 * Implementations of this interface can modify the request, pass it to the next policy, and then modify the response.</p> 
 * 
 * <p><strong>Code Sample:</strong></p> 
 * 
 * <p>In this example, the UserAgentPolicy, RetryPolicy, and CustomPolicy are added to the pipeline. The pipeline is 
 * then used to send an HTTP request, and the response is retrieved.</p> 
 * 
 * <pre> 
 * {@code 
 * HttpPipeline pipeline = new HttpPipelineBuilder() 
 *     .policies(new UserAgentPolicy(), new RetryPolicy(), new CustomPolicy()) 
 *     .build(); 
 * 
 * HttpRequest request = new HttpRequest(HttpMethod.GET, new URL("http://example.com")); 
 * HttpResponse response = pipeline.send(request).block(); 
 * } 
 * </pre> 
 * 
 * <p>This package is crucial for the communication between Azure SDK client libraries and Azure services. It provides 
 * a layer of abstraction over the HTTP protocol, allowing client libraries to focus on service-specific logic.</p> 
 * 
 * @see com.azure.core.http.policy.HttpPipelinePolicy 
 * @see com.azure.core.http.policy.HttpLogDetailLevel 
 * @see com.azure.core.http.policy.HttpLogOptions 
 * @see com.azure.core.http.policy.HttpLoggingPolicy 
 * @see com.azure.core.http.policy.HttpPipelinePolicy 
 * @see com.azure.core.http.policy.RetryPolicy 
 * @see com.azure.core.http.policy.UserAgentPolicy 
 */ 
package com.azure.core.http.policy { 
    /** 
     * <p>The {@code AddDatePolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is 
     * used to add a "Date" header in RFC 1123 format when sending an HTTP request.</p> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class AddDatePolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a new instance of {@link AddDatePolicy}. 
         */ 
        public AddDatePolicy() 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The pipeline policy that override or add {@link HttpHeaders} in {@link HttpRequest} by reading values from 
     * {@link Context} with key 'azure-http-headers-key'. The value for this key should be of type {@link HttpHeaders} for 
     * it to be added in {@link HttpRequest}. 
     * 
     * <p> 
     * <strong>Code Sample: Add multiple HttpHeader in Context and call client</strong> 
     * </p> 
     * <pre> 
     * // Create ConfigurationClient for example 
     * ConfigurationClient configurationClient = new ConfigurationClientBuilder() 
     * .connectionString("endpoint={endpoint_value};id={id_value};secret={secret_value}") 
     * .buildClient(); 
     * // Add your headers 
     * HttpHeaders headers = new HttpHeaders(); 
     * headers.put("my-header1", "my-header1-value"); 
     * headers.put("my-header2", "my-header2-value"); 
     * headers.put("my-header3", "my-header3-value"); 
     * // Call API by passing headers in Context. 
     * configurationClient.addConfigurationSettingWithResponse( 
     * new ConfigurationSetting().setKey("key").setValue("value"), 
     * new Context(AddHeadersFromContextPolicy.AZURE_REQUEST_HTTP_HEADERS_KEY, headers)); 
     * // Above three HttpHeader will be added in outgoing HttpRequest. 
     * </pre> 
     * 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class AddHeadersFromContextPolicy implements HttpPipelinePolicy { 
        /** 
         *Key used to override headers in HttpRequest. The Value for this key should be {@link HttpHeaders}. 
         */ 
        public static final String AZURE_REQUEST_HTTP_HEADERS_KEY = "azure-http-headers-key"; 
        /** 
         * Creates a new instance of {@link AddHeadersFromContextPolicy}. 
         */ 
        public AddHeadersFromContextPolicy() 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * <p>The {@code AddHeadersPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy 
     * is used to add a set of headers to all outgoing HTTP requests.</p> 
     * 
     * <p>This class is useful when there are certain headers that should be included in all requests. For example, you 
     * might want to include a "User-Agent" header in all requests to identify your application, or a "Content-Type" header 
     * to specify the format of the request body.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, the {@code AddHeadersPolicy} is created from the specified headers. The policy can be added to 
     * the pipeline and the requests sent will include the headers specified in the {@code AddHeadersPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.AddHeaderPolicy.constructor --> 
     * <pre> 
     * HttpHeaders headers = new HttpHeaders(); 
     * headers.put("User-Agent", "MyApp/1.0"); 
     * headers.put("Content-Type", "application/json"); 
     * 
     * new AddHeadersPolicy(headers); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.AddHeaderPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.HttpHeaders 
     */ 
    public class AddHeadersPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a AddHeadersPolicy. 
         * 
         * @param headers The headers to add to outgoing requests. 
         */ 
        public AddHeadersPolicy(HttpHeaders headers) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * Implementing classes are automatically added as policies after the retry policy. 
     */ 
    public interface AfterRetryPolicyProvider extends HttpPolicyProvider { 
        // This interface does not declare any API. 
    } 
    /** 
     * The {@code AzureKeyCredentialPolicy} class is an implementation of the {@link KeyCredentialPolicy} interface. This 
     * policy uses an {@link AzureKeyCredential} to set the authorization key for a request. 
     * 
     * <p>This class is useful when you need to authorize requests with a key from Azure.</p> 
     * 
     * <p>Requests sent with this pipeline policy are required to use {@code HTTPS}. If the request isn't using 
     * {@code HTTPS} an exception will be thrown to prevent leaking the key.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code AzureKeyCredentialPolicy} is created with a key and a header name. The policy 
     * can be added to a pipeline. The requests sent by the pipeline will then include the specified header with the 
     * key as its value.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.AzureKeyCredentialPolicy.constructor --> 
     * <pre> 
     * AzureKeyCredential credential = new AzureKeyCredential("my_key"); 
     * AzureKeyCredentialPolicy policy = new AzureKeyCredentialPolicy("my_header", credential); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.AzureKeyCredentialPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.KeyCredentialPolicy 
     * @see com.azure.core.credential.AzureKeyCredential 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public final class AzureKeyCredentialPolicy extends KeyCredentialPolicy { 
        /** 
         * Creates a policy that uses the passed {@link AzureKeyCredential} to set the specified header name. 
         * 
         * @param name The name of the key header that will be set to {@link AzureKeyCredential#getKey()}. 
         * @param credential The {@link AzureKeyCredential} containing the authorization key to use. 
         * @throws NullPointerException If {@code name} or {@code credential} is {@code null}. 
         * @throws IllegalArgumentException If {@code name} is empty. 
         */ 
        public AzureKeyCredentialPolicy(String name, AzureKeyCredential credential) 
        /** 
         * Creates a policy that uses the passed {@link AzureKeyCredential} to set the specified header name. 
         * <p> 
         * The {@code prefix} will be applied before the {@link AzureKeyCredential#getKey()} when setting the header. A 
         * space will be inserted between {@code prefix} and credential. 
         * 
         * @param name The name of the key header that will be set to {@link AzureKeyCredential#getKey()}. 
         * @param credential The {@link AzureKeyCredential} containing the authorization key to use. 
         * @param prefix The prefix to apply before the credential, for example "SharedAccessKey credential". 
         * @throws NullPointerException If {@code name} or {@code credential} is {@code null}. 
         * @throws IllegalArgumentException If {@code name} is empty. 
         */ 
        public AzureKeyCredentialPolicy(String name, AzureKeyCredential credential, String prefix) 
    } 
    /** 
     * The {@code AzureSasCredentialPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This 
     * policy uses an {@link AzureSasCredential} to append a shared access signature (SAS) to the query string of a request. 
     * 
     * <p>This class is useful when you need to authorize requests with a SAS from Azure. It ensures that the requests are 
     * sent over HTTPS to prevent the SAS from being leaked.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code AzureSasCredentialPolicy} is created with a SAS. The policy can then added to the 
     * pipeline. The request sent by the pipeline will then include the SAS appended to its query string.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.AzureSasCredentialPolicy.constructor --> 
     * <pre> 
     * AzureSasCredential credential = new AzureSasCredential("my_sas"); 
     * AzureSasCredentialPolicy policy = new AzureSasCredentialPolicy(credential); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.AzureSasCredentialPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.credential.AzureSasCredential 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public final class AzureSasCredentialPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a policy that uses the passed {@link AzureSasCredential} to append sas to query string. 
         * <p> 
         * Requests sent with this pipeline policy are required to use {@code HTTPS}. 
         * If the request isn't using {@code HTTPS} 
         * an exception will be thrown to prevent leaking the shared access signature. 
         * 
         * @param credential The {@link AzureSasCredential} containing the shared access signature to use. 
         * @throws NullPointerException If {@code credential} is {@code null}. 
         */ 
        public AzureSasCredentialPolicy(AzureSasCredential credential) 
        /** 
         * Creates a policy that uses the passed {@link AzureSasCredential} to append sas to query string. 
         * 
         * @param credential The {@link AzureSasCredential} containing the shared access signature to use. 
         * @param requireHttps A flag indicating whether {@code HTTPS} is required. 
         * @throws NullPointerException If {@code credential} is {@code null}. 
         */ 
        public AzureSasCredentialPolicy(AzureSasCredential credential, boolean requireHttps) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * <p>The {@code BearerTokenAuthenticationPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. 
     * This policy uses a {@link TokenCredential} to authenticate the request with a bearer token.</p> 
     * 
     * <p>This class is useful when you need to authorize requests with a bearer token from Azure. It ensures that the 
     * requests are sent over HTTPS to prevent the token from being leaked.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code BearerTokenAuthenticationPolicy} is created with a {@link TokenCredential} and a scope. 
     * The policy can then added to the pipeline. The request sent via the pipeline will then include the 
     * Authorization header with the bearer token.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.BearerTokenAuthenticationPolicy.constructor --> 
     * <pre> 
     * TokenCredential credential = new BasicAuthenticationCredential("username", "password"); 
     * BearerTokenAuthenticationPolicy policy = new BearerTokenAuthenticationPolicy(credential, 
     *     "https://management.azure.com/.default"); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.BearerTokenAuthenticationPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.credential.TokenCredential 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class BearerTokenAuthenticationPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates BearerTokenAuthenticationPolicy. 
         * 
         * @param credential the token credential to authenticate the request 
         * @param scopes the scopes of authentication the credential should get token for 
         */ 
        public BearerTokenAuthenticationPolicy(TokenCredential credential, String... scopes) 
        /** 
         * Authorizes the request with the bearer token acquired using the specified {@code tokenRequestContext} 
         * 
         * @param context the HTTP pipeline context. 
         * @param tokenRequestContext the token request context to be used for token acquisition. 
         * @return a {@link Mono} containing {@link Void} 
         */ 
        public Mono<Void> setAuthorizationHeader(HttpPipelineCallContext context, TokenRequestContext tokenRequestContext) 
        /** 
         * Authorizes the request with the bearer token acquired using the specified {@code tokenRequestContext} 
         * 
         * @param context the HTTP pipeline context. 
         * @param tokenRequestContext the token request context to be used for token acquisition. 
         */ 
        public void setAuthorizationHeaderSync(HttpPipelineCallContext context, TokenRequestContext tokenRequestContext) 
        /** 
         * Executed before sending the initial request and authenticates the request. 
         * 
         * @param context The request context. 
         * @return A {@link Mono} containing {@link Void} 
         */ 
        public Mono<Void> authorizeRequest(HttpPipelineCallContext context) 
        /** 
         * Handles the authentication challenge in the event a 401 response with a WWW-Authenticate authentication challenge 
         * header is received after the initial request and returns appropriate {@link TokenRequestContext} to be used for 
         * re-authentication. 
         * <p> 
         * The default implementation will attempt to handle Continuous Access Evaluation (CAE) challenges. 
         * </p> 
         * 
         * @param context The request context. 
         * @param response The Http Response containing the authentication challenge header. 
         * @return A {@link Mono} containing {@link TokenRequestContext} 
         */ 
        public Mono<Boolean> authorizeRequestOnChallenge(HttpPipelineCallContext context, HttpResponse response) 
        /** 
         * Handles the authentication challenge in the event a 401 response with a WWW-Authenticate authentication challenge 
         * header is received after the initial request and returns appropriate {@link TokenRequestContext} to be used for 
         * re-authentication. 
         * <p> 
         * The default implementation will attempt to handle Continuous Access Evaluation (CAE) challenges. 
         * </p> 
         * 
         * @param context The request context. 
         * @param response The Http Response containing the authentication challenge header. 
         * @return A boolean indicating if containing the {@link TokenRequestContext} for re-authentication 
         */ 
        public boolean authorizeRequestOnChallengeSync(HttpPipelineCallContext context, HttpResponse response) 
        /** 
         * Synchronously executed before sending the initial request and authenticates the request. 
         * 
         * @param context The request context. 
         */ 
        public void authorizeRequestSync(HttpPipelineCallContext context) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * Implementing classes are automatically added as policies before the retry policy. 
     */ 
    public interface BeforeRetryPolicyProvider extends HttpPolicyProvider { 
        // This interface does not declare any API. 
    } 
    /** 
     * <p>The {@code CookiePolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is 
     * used to handle cookies in HTTP requests and responses.</p> 
     * 
     * <p>This class stores cookies from the "Set-Cookie" header of the HTTP response and adds them to subsequent HTTP 
     * requests. This is useful for maintaining session information or other stateful information across multiple requests 
     * to the same server.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code CookiePolicy} is constructed. The policy can then be added to a pipeline. 
     * Any cookies set by the server in the response to a request by the pipeline will be stored by the {@code CookiePolicy} 
     * and added to subsequent requests to the same server.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.CookiePolicy.constructor --> 
     * <pre> 
     * CookiePolicy cookiePolicy = new CookiePolicy(); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.CookiePolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class CookiePolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a new instance of {@link CookiePolicy}. 
         */ 
        public CookiePolicy() 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code DefaultRedirectStrategy} class is an implementation of the {@link RedirectStrategy} interface. This 
     * strategy uses the provided maximum retry attempts, header name to look up redirect URL value for, HTTP methods and 
     * a known set of redirect status response codes (301, 302, 307, 308) to determine if a request should be redirected. 
     * 
     * <p>This class is useful when you need to handle HTTP redirects. It ensures that the requests are redirected 
     * correctly based on the response status code and the maximum number of redirect attempts.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code DefaultRedirectStrategy} is created with a maximum of 3 redirect attempts, 
     * "Location" as the header name to locate the redirect URL, and GET and HEAD as the allowed methods for performing 
     * the redirect. The strategy is then used in a {@code RedirectPolicy} which can be added to the pipeline. For a request 
     * sent by the pipeline, if the server responds with a redirect status code and provides a "Location" header, 
     * the request will be redirected up to 3 times as needed.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.DefaultRedirectStrategy.constructor --> 
     * <pre> 
     * DefaultRedirectStrategy redirectStrategy = new DefaultRedirectStrategy(3, "Location", 
     *     EnumSet.of(HttpMethod.GET, HttpMethod.HEAD)); 
     * RedirectPolicy redirectPolicy = new RedirectPolicy(redirectStrategy); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.DefaultRedirectStrategy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.RedirectStrategy 
     * @see com.azure.core.http.policy.RedirectPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public final class DefaultRedirectStrategy implements RedirectStrategy { 
        /** 
         * Creates an instance of {@link DefaultRedirectStrategy} with a maximum number of redirect attempts 3, 
         * header name "Location" to locate the redirect url in the response headers and {@link HttpMethod#GET} 
         * and {@link HttpMethod#HEAD} as allowed methods for performing the redirect. 
         */ 
        public DefaultRedirectStrategy() 
        /** 
         * Creates an instance of {@link DefaultRedirectStrategy} with the provided number of redirect attempts and 
         * default header name "Location" to locate the redirect url in the response headers and {@link HttpMethod#GET} 
         * and {@link HttpMethod#HEAD} as allowed methods for performing the redirect. 
         * 
         * @param maxAttempts The max number of redirect attempts that can be made. 
         * @throws IllegalArgumentException if {@code maxAttempts} is less than 0. 
         */ 
        public DefaultRedirectStrategy(int maxAttempts) 
        /** 
         * Creates an instance of {@link DefaultRedirectStrategy}. 
         * 
         * @param maxAttempts The max number of redirect attempts that can be made. 
         * @param locationHeader The header name containing the redirect URL. 
         * @param allowedMethods The set of {@link HttpMethod} that are allowed to be redirected. 
         * @throws IllegalArgumentException if {@code maxAttempts} is less than 0. 
         */ 
        public DefaultRedirectStrategy(int maxAttempts, String locationHeader, Set<HttpMethod> allowedMethods) 
        @Override public HttpRequest createRedirectRequest(HttpResponse httpResponse) 
        @Override public int getMaxAttempts() 
        @Override public boolean shouldAttemptRedirect(HttpPipelineCallContext context, HttpResponse httpResponse, int tryCount, Set<String> attemptedRedirectUrls) 
    } 
    /** 
     * <p>The {@code ExponentialBackoff} class is an implementation of the {@link RetryStrategy} interface. This strategy uses 
     * a delay duration that exponentially increases with each retry attempt until an upper bound is reached, after which 
     * every retry attempt is delayed by the provided max delay duration.</p> 
     * 
     * <p>This class is useful when you need to handle retries for operations that may transiently fail. It ensures that 
     * the retries are performed with an increasing delay to avoid overloading the system.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code ExponentialBackoff} is created and used in a {@code RetryPolicy} which can be added to 
     * a pipeline. For a request sent by the pipeline, if the server responds with a transient error, the request will be 
     * retried with an exponentially increasing delay.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.ExponentialBackoff.constructor --> 
     * <pre> 
     * ExponentialBackoff retryStrategy = new ExponentialBackoff(); 
     * RetryPolicy policy = new RetryPolicy(retryStrategy); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.ExponentialBackoff.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.RetryStrategy 
     * @see com.azure.core.http.policy.RetryPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ExponentialBackoff implements RetryStrategy { 
        /** 
         * Creates an instance of {@link ExponentialBackoff} with a maximum number of retry attempts configured by the 
         * environment property {@link Configuration#PROPERTY_AZURE_REQUEST_RETRY_COUNT}, or three if it isn't configured or 
         * is less than or equal to 0. This strategy starts with a delay of 800 milliseconds and exponentially increases 
         * with each additional retry attempt to a maximum of 8 seconds. 
         */ 
        public ExponentialBackoff() 
        /** 
         * Creates an instance of {@link ExponentialBackoff}. 
         * 
         * @param options The {@link ExponentialBackoffOptions}. 
         * @throws NullPointerException if {@code options} is {@code null}. 
         */ 
        public ExponentialBackoff(ExponentialBackoffOptions options) 
        /** 
         * Creates an instance of {@link ExponentialBackoff}. 
         * 
         * @param maxRetries The max retry attempts that can be made. 
         * @param baseDelay The base delay duration for retry. 
         * @param maxDelay The max delay duration for retry. 
         * @throws IllegalArgumentException if {@code maxRetries} is less than 0 or {@code baseDelay} is less than or equal 
         * to 0 or {@code maxDelay} is less than {@code baseDelay}. 
         */ 
        public ExponentialBackoff(int maxRetries, Duration baseDelay, Duration maxDelay) 
        @Override public Duration calculateRetryDelay(int retryAttempts) 
        @Override public int getMaxRetries() 
        @Override public boolean shouldRetryCondition(RequestRetryCondition requestRetryCondition) 
    } 
    /** 
     * <p>The {@code ExponentialBackoffOptions} class provides configuration options for the {@link ExponentialBackoff} 
     * retry strategy. This strategy uses a delay duration that exponentially increases with each retry attempt until an 
     * upper bound is reached. After reaching the upper bound, every retry attempt is delayed by the provided max delay 
     * duration.</p> 
     * 
     * <p>This class is useful when you need to customize the behavior of the exponential backoff strategy. It allows you 
     * to specify the maximum number of retry attempts, the base delay duration, and the maximum delay duration.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code ExponentialBackoffOptions} is created and used to configure an 
     * {@code ExponentialBackoff} retry strategy. The strategy is then used in a {@code RetryPolicy} which can then be added to 
     * a pipeline. For a request then sent by the pipeline, if the server responds with a transient error, the request 
     * will be retried with an exponentially increasing delay.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.ExponentialBackoffOptions.constructor --> 
     * <pre> 
     * ExponentialBackoffOptions options = new ExponentialBackoffOptions().setMaxRetries(5) 
     *     .setBaseDelay(Duration.ofSeconds(1)) 
     *     .setMaxDelay(Duration.ofSeconds(10)); 
     * 
     * ExponentialBackoff retryStrategy = new ExponentialBackoff(options); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.ExponentialBackoffOptions.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.ExponentialBackoff 
     * @see com.azure.core.http.policy.RetryPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ExponentialBackoffOptions { 
        /** 
         * Creates a new instance of {@link ExponentialBackoffOptions}. 
         */ 
        public ExponentialBackoffOptions() 
        /** 
         * Gets the base delay duration for retry. 
         * 
         * @return The base delay duration for retry. 
         */ 
        public Duration getBaseDelay() 
        /** 
         * Sets the base delay duration for retry. 
         * 
         * @param baseDelay the base delay duration for retry. 
         * @throws IllegalArgumentException if {@code baseDelay} is less than or equal 
         * to 0 or {@code maxDelay} has been set and is less than {@code baseDelay}. 
         * @return The updated {@link ExponentialBackoffOptions} 
         */ 
        public ExponentialBackoffOptions setBaseDelay(Duration baseDelay) 
        /** 
         * Gets the max delay duration for retry. 
         * 
         * @return The max delay duration for retry. 
         */ 
        public Duration getMaxDelay() 
        /** 
         * Sets the max delay duration for retry. 
         * 
         * @param maxDelay the max delay duration for retry. 
         * @throws IllegalArgumentException if {@code maxDelay} is less than or equal 
         * to 0 or {@code baseDelay} has been set and is more than {@code maxDelay}. 
         * @return The updated {@link ExponentialBackoffOptions} 
         */ 
        public ExponentialBackoffOptions setMaxDelay(Duration maxDelay) 
        /** 
         * Gets the max retry attempts that can be made. 
         * 
         * @return The max retry attempts that can be made. 
         */ 
        public Integer getMaxRetries() 
        /** 
         * Sets the max retry attempts that can be made. 
         * 
         * @param maxRetries the max retry attempts that can be made. 
         * @throws IllegalArgumentException if {@code maxRetries} is less than 0. 
         * @return The updated {@link ExponentialBackoffOptions} 
         */ 
        public ExponentialBackoffOptions setMaxRetries(Integer maxRetries) 
    } 
    /** 
     * The {@code FixedDelay} class is an implementation of the {@link RetryStrategy} interface. This strategy uses a 
     * fixed delay duration between each retry attempt. 
     * 
     * <p>This class is useful when you need to handle retries for operations that may transiently fail. It ensures that 
     * the retries are performed with a fixed delay to provide a consistent delay between retries.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code FixedDelay} is created with a maximum of 3 retry attempts and a delay of 1 second 
     * between each attempt. The strategy is then used in a {@code RetryPolicy} which can then be added to the pipeline. 
     * For a request then sent by the pipeline, if the server responds with a transient error, the request will be retried 
     * with a fixed delay of 1 second between each attempt.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.FixedDelay.constructor --> 
     * <pre> 
     * FixedDelay retryStrategy = new FixedDelay(3, Duration.ofSeconds(1)); 
     * RetryPolicy policy = new RetryPolicy(retryStrategy); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.FixedDelay.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.RetryStrategy 
     * @see com.azure.core.http.policy.RetryPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class FixedDelay implements RetryStrategy { 
        /** 
         * Creates an instance of {@link FixedDelay}. 
         * 
         * @param fixedDelayOptions The {@link FixedDelayOptions}. 
         */ 
        public FixedDelay(FixedDelayOptions fixedDelayOptions) 
        /** 
         * Creates an instance of {@link FixedDelay}. 
         * 
         * @param maxRetries The max number of retry attempts that can be made. 
         * @param delay The fixed delay duration between retry attempts. 
         * @throws IllegalArgumentException If {@code maxRetries} is negative. 
         * @throws NullPointerException If {@code delay} is {@code null}. 
         */ 
        public FixedDelay(int maxRetries, Duration delay) 
        @Override public Duration calculateRetryDelay(int retryAttempts) 
        @Override public int getMaxRetries() 
        @Override public boolean shouldRetryCondition(RequestRetryCondition requestRetryCondition) 
    } 
    /** 
     * The {@code FixedDelayOptions} class provides configuration options for the {@link FixedDelay} retry strategy. 
     * This strategy uses a fixed delay duration between each retry attempt. 
     * 
     * <p>This class is useful when you need to customize the behavior of the fixed delay retry strategy. It allows you 
     * to specify the maximum number of retry attempts and the delay duration between each attempt.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code FixedDelayOptions} is created and used to configure a {@code FixedDelay} retry strategy. 
     * The strategy is then used in a {@code RetryPolicy} which can then be added to the pipeline. For a request then sent 
     * by the pipeline, if the server responds with a transient error, the request will be retried with a fixed delay 
     * between each attempt.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.FixedDelayOptions.constructor --> 
     * <pre> 
     * FixedDelayOptions options = new FixedDelayOptions(3, Duration.ofSeconds(1)); 
     * FixedDelay retryStrategy = new FixedDelay(options); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.FixedDelayOptions.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.FixedDelay 
     * @see com.azure.core.http.policy.RetryPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class FixedDelayOptions { 
        /** 
         * Creates an instance of {@link FixedDelayOptions}. 
         * 
         * @param maxRetries The max number of retry attempts that can be made. 
         * @param delay The fixed delay duration between retry attempts. 
         * @throws IllegalArgumentException If {@code maxRetries} is negative. 
         * @throws NullPointerException If {@code delay} is {@code null}. 
         */ 
        public FixedDelayOptions(int maxRetries, Duration delay) 
        /** 
         * Gets the max retry attempts that can be made. 
         * 
         * @return The max retry attempts that can be made. 
         */ 
        public Duration getDelay() 
        /** 
         * Gets the max retry attempts that can be made. 
         * 
         * @return The max retry attempts that can be made. 
         */ 
        public int getMaxRetries() 
    } 
    /** 
     * The {@code HostPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is used 
     * to add a specific host to each HTTP request. 
     * 
     * <p>This class is useful when you need to set a specific host for all requests in a pipeline. It ensures that the 
     * host is set correctly for each request.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code HostPolicy} is created with a host of "www.example.com". Once added to the pipeline, 
     * all requests will have their host set to "www.example.com" by the {@code HostPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.HostPolicy.constructor --> 
     * <pre> 
     * HostPolicy hostPolicy = new HostPolicy("www.example.com"); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.HostPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class HostPolicy implements HttpPipelinePolicy { 
        /** 
         * Create HostPolicy. 
         * 
         * @param host The host to set on every HttpRequest. 
         */ 
        public HostPolicy(String host) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code HttpLogDetailLevel} class is an enumeration of the levels of detail to log on HTTP messages. 
     * 
     * <p>This class is useful when you need to control the amount of information that is logged during the execution 
     * of HTTP requests. It provides several levels of detail, ranging from no logging at all to logging of headers and 
     * body content.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code HttpLogOptions} is created and the log level is set to 
     * {@code HttpLogDetailLevel.BODY_AND_HEADERS}. This means that the URL, HTTP method, headers, and body content of 
     * each request and response will be logged. The {@code HttpLogOptions} is then used to create an 
     * {@code HttpLoggingPolicy}, which can then be added to the pipeline.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.HttpLogDetailLevel.constructor --> 
     * <pre> 
     * HttpLogOptions logOptions = new HttpLogOptions(); 
     * logOptions.setLogLevel(HttpLogDetailLevel.BODY_AND_HEADERS); 
     * HttpLoggingPolicy loggingPolicy = new HttpLoggingPolicy(logOptions); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.HttpLogDetailLevel.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpLoggingPolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public enum HttpLogDetailLevel { 
        NONE, 
            /** 
             * Logging is turned off. 
             */ 
        BASIC, 
            /** 
             * Logs only URLs, HTTP methods, and time to finish the request. 
             */ 
        HEADERS, 
            /** 
             * Logs everything in BASIC, plus all the request and response headers. 
             * <p> 
             * Values of the headers will be logged only for allowed headers. 
             * See {@link HttpLogOptions#getAllowedHttpHeaderNames()}. 
             */ 
        BODY, 
            /** 
             * Logs everything in BASIC, plus all the request and response body. 
             * <p> 
             * <b>NOTE:</b> Only payloads in plain text or plain text encoded in GZIP will be logged. 
             * <p> 
             * The response body will be buffered into memory even if it is never consumed by an application, possibly impacting 
             * performance. 
             */ 
        BODY_AND_HEADERS; 
            /** 
             * Logs everything in HEADERS and BODY. 
             * <p> 
             * Values of the headers will be logged only for allowed headers. 
             * See {@link HttpLogOptions#getAllowedHttpHeaderNames()}. 
             * <p> 
             * The response body will be buffered into memory even if it is never consumed by an application, possibly impacting 
             * performance. 
             */ 
        /** 
         * Whether a body should be logged. 
         * 
         * @return Whether a body should be logged. 
         */ 
        public boolean shouldLogBody() 
        /** 
         * Whether headers should be logged. 
         * 
         * @return Whether headers should be logged. 
         */ 
        public boolean shouldLogHeaders() 
        /** 
         * Whether a URL should be logged. 
         * 
         * @return Whether a URL should be logged. 
         */ 
        public boolean shouldLogUrl() 
    } 
    /** 
     * The {@code HttpLogOptions} class provides configuration options for HTTP logging. This includes setting the log level, 
     * specifying allowed header names and query parameters for logging, and controlling whether to pretty print the body 
     * of HTTP messages. 
     * 
     * <p>This class is useful when you need to control the amount of information that is logged during the execution of 
     * HTTP requests and responses. It allows you to specify the log level, which determines the amount of detail included 
     * in the logs (such as the URL, headers, and body of requests and responses).</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, the {@code HttpLogOptions} is created and the log level is set to {@code HttpLogDetailLevel.BODY_AND_HEADERS}. 
     * This means that the URL, HTTP method, headers, and body content of each request and response will be logged. 
     * The allowed header names and query parameters for logging are also specified, and pretty printing of the body is enabled. 
     * The {@code HttpLogOptions} is then used to create an {@code HttpLoggingPolicy}, which can then be added to the pipeline.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.HttpLogOptions.constructor --> 
     * <pre> 
     * HttpLogOptions logOptions = new HttpLogOptions(); 
     * logOptions.setLogLevel(HttpLogDetailLevel.BODY_AND_HEADERS); 
     * logOptions.setAllowedHttpHeaderNames(new HashSet<>(Arrays.asList(HttpHeaderName.DATE, 
     *     HttpHeaderName.X_MS_REQUEST_ID))); 
     * logOptions.setAllowedQueryParamNames(new HashSet<>(Arrays.asList("api-version"))); 
     * HttpLoggingPolicy loggingPolicy = new HttpLoggingPolicy(logOptions); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.HttpLogOptions.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpLoggingPolicy 
     * @see com.azure.core.http.policy.HttpLogDetailLevel 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class HttpLogOptions { 
        /** 
         * Creates a new instance that does not log any information about HTTP requests or responses. 
         */ 
        public HttpLogOptions() 
        /** 
         * Sets the given allowed header to the default header set that should be logged. 
         * <p> 
         * With the deprecation of this method, the passed {@code allowedHeaderName} will be converted to an 
         * {@link HttpHeaderName} using {@link HttpHeaderName#fromString(String)}. 
         * 
         * @param allowedHeaderName The allowed header name. 
         * @return The updated HttpLogOptions object. 
         * @throws NullPointerException If {@code allowedHeaderName} is {@code null}. 
         * @deprecated Use {@link #addAllowedHttpHeaderName(HttpHeaderName)} instead. 
         */ 
        @Deprecated public HttpLogOptions addAllowedHeaderName(String allowedHeaderName) 
        /** 
         * Sets the given allowed {@link HttpHeaderName} to the default header set that should be logged. 
         * 
         * @param allowedHeaderName The allowed {@link HttpHeaderName}. 
         * @return The updated HttpLogOptions object. 
         * @throws NullPointerException If {@code allowedHeaderName} is {@code null}. 
         */ 
        public HttpLogOptions addAllowedHttpHeaderName(HttpHeaderName allowedHeaderName) 
        /** 
         * Sets the given allowed query param that should be logged. 
         * 
         * @param allowedQueryParamName The allowed query param name from the user. 
         * @return The updated HttpLogOptions object. 
         * @throws NullPointerException If {@code allowedQueryParamName} is {@code null}. 
         */ 
        public HttpLogOptions addAllowedQueryParamName(String allowedQueryParamName) 
        /** 
         * Gets the allowed headers that should be logged. 
         * <p> 
         * With the deprecation of this method, this will now return a new {@link HashSet} each time called where the values 
         * are mapped from {@link HttpHeaderName#getCaseSensitiveName()}. 
         * 
         * @return The list of allowed headers. 
         * @deprecated Use {@link #getAllowedHttpHeaderNames()} instead. 
         */ 
        @Deprecated public Set<String> getAllowedHeaderNames() 
        /** 
         * Sets the given allowed headers that should be logged. 
         * <p> 
         * This method sets the provided header names to be the allowed header names which will be logged for all HTTP 
         * requests and responses, overwriting any previously configured headers. Additionally, users can use {@link 
         * HttpLogOptions#addAllowedHeaderName(String)} or {@link HttpLogOptions#getAllowedHeaderNames()} to add or remove 
         * more headers names to the existing set of allowed header names. 
         * <p> 
         * With the deprecation of this method, if {@code allowedHeaderNames} is non-null, this will map the passed 
         * {@code allowedHeaderNames} to a set of {@link HttpHeaderName} using {@link HttpHeaderName#fromString(String)} 
         * on each value in the set. 
         * 
         * @param allowedHeaderNames The list of allowed header names. 
         * @return The updated HttpLogOptions object. 
         * @deprecated Use {@link #setAllowedHttpHeaderNames(Set)} instead. 
         */ 
        @Deprecated public HttpLogOptions setAllowedHeaderNames(Set<String> allowedHeaderNames) 
        /** 
         * Gets the allowed {@link HttpHeaderName HttpHeaderNames} that should be logged. 
         * 
         * @return The list of allowed {@link HttpHeaderName HttpHeaderNames}. 
         */ 
        public Set<HttpHeaderName> getAllowedHttpHeaderNames() 
        /** 
         * Sets the given allowed {@link HttpHeaderName HttpHeaderNames} that should be logged. 
         * <p> 
         * This method sets the provided header names to be the allowed header names which will be logged for all HTTP 
         * requests and responses, overwriting any previously configured headers. Additionally, users can use {@link 
         * HttpLogOptions#addAllowedHeaderName(String)} or {@link HttpLogOptions#getAllowedHeaderNames()} to add or remove 
         * more headers names to the existing set of allowed header names. 
         * 
         * @param allowedHttpHeaderNames The list of allowed {@link HttpHeaderName HttpHeaderNames}. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions setAllowedHttpHeaderNames(Set<HttpHeaderName> allowedHttpHeaderNames) 
        /** 
         * Gets the allowed query parameters. 
         * 
         * @return The list of allowed query parameters. 
         */ 
        public Set<String> getAllowedQueryParamNames() 
        /** 
         * Sets the given allowed query params to be displayed in the logging info. 
         * 
         * @param allowedQueryParamNames The list of allowed query params from the user. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions setAllowedQueryParamNames(Set<String> allowedQueryParamNames) 
        /** 
         * Gets the application specific id. 
         * 
         * @return The application specific id. 
         * @deprecated Use {@link ClientOptions} to configure {@code applicationId}. 
         */ 
        @Deprecated public String getApplicationId() 
        /** 
         * Sets the custom application specific id supplied by the user of the client library. 
         * 
         * @param applicationId The user specified application id. 
         * @return The updated HttpLogOptions object. 
         * @throws IllegalArgumentException If {@code applicationId} contains spaces or is larger than 24 characters in 
         * length. 
         * @deprecated Use {@link ClientOptions} to configure {@code applicationId}. 
         */ 
        @Deprecated public HttpLogOptions setApplicationId(String applicationId) 
        /** 
         * Sets the flag that controls if header names which value is redacted should be logged. 
         * <p> 
         * Applies only if logging request and response headers is enabled. See {@link #setLogLevel(HttpLogDetailLevel)} for 
         * details. Defaults to false - redacted header names are logged. 
         * 
         * @param disableRedactedHeaderLogging If true, redacted header names are not logged. 
         * Otherwise, they are logged as a comma separated list under redactedHeaders property. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions disableRedactedHeaderLogging(boolean disableRedactedHeaderLogging) 
        /** 
         * Gets the level of detail to log on HTTP messages. 
         * 
         * @return The {@link HttpLogDetailLevel}. 
         */ 
        public HttpLogDetailLevel getLogLevel() 
        /** 
         * Sets the level of detail to log on Http messages. 
         * 
         * <p>If logLevel is not provided, default value of {@link HttpLogDetailLevel#NONE} is set.</p> 
         * 
         * @param logLevel The {@link HttpLogDetailLevel}. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions setLogLevel(HttpLogDetailLevel logLevel) 
        /** 
         * Gets flag to allow pretty printing of message bodies. 
         * 
         * @return true if pretty printing of message bodies is allowed. 
         * @deprecated Use {@link #setRequestLogger(HttpRequestLogger)} and {@link #setResponseLogger(HttpResponseLogger)} 
         * to configure how requests and responses should be logged at a granular level instead. 
         */ 
        @Deprecated public boolean isPrettyPrintBody() 
        /** 
         * Sets flag to allow pretty printing of message bodies. 
         * 
         * @param prettyPrintBody If true, pretty prints message bodies when logging. If the detailLevel does not include 
         * body logging, this flag does nothing. 
         * @return The updated HttpLogOptions object. 
         * @deprecated Use {@link #setRequestLogger(HttpRequestLogger)} and {@link #setResponseLogger(HttpResponseLogger)} 
         * to configure how requests and responses should be logged at a granular level instead. 
         */ 
        @Deprecated public HttpLogOptions setPrettyPrintBody(boolean prettyPrintBody) 
        /** 
         * Gets the flag that controls if header names with redacted values should be logged. 
         * 
         * @return true if header names with redacted values should be logged. 
         */ 
        public boolean isRedactedHeaderLoggingDisabled() 
        /** 
         * Gets the {@link HttpRequestLogger} that will be used to log HTTP requests. 
         * <p> 
         * A default {@link HttpRequestLogger} will be used if one isn't supplied. 
         * 
         * @return The {@link HttpRequestLogger} that will be used to log HTTP requests. 
         */ 
        public HttpRequestLogger getRequestLogger() 
        /** 
         * Sets the {@link HttpRequestLogger} that will be used to log HTTP requests. 
         * <p> 
         * A default {@link HttpRequestLogger} will be used if one isn't supplied. 
         * 
         * @param requestLogger The {@link HttpRequestLogger} that will be used to log HTTP requests. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions setRequestLogger(HttpRequestLogger requestLogger) 
        /** 
         * Gets the {@link HttpResponseLogger} that will be used to log HTTP responses. 
         * <p> 
         * A default {@link HttpResponseLogger} will be used if one isn't supplied. 
         * 
         * @return The {@link HttpResponseLogger} that will be used to log HTTP responses. 
         */ 
        public HttpResponseLogger getResponseLogger() 
        /** 
         * Sets the {@link HttpResponseLogger} that will be used to log HTTP responses. 
         * <p> 
         * A default {@link HttpResponseLogger} will be used if one isn't supplied. 
         * 
         * @param responseLogger The {@link HttpResponseLogger} that will be used to log HTTP responses. 
         * @return The updated HttpLogOptions object. 
         */ 
        public HttpLogOptions setResponseLogger(HttpResponseLogger responseLogger) 
    } 
    /** 
     * The {@code HttpLoggingPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. 
     * This policy handles logging of HTTP requests and responses based on the provided {@link HttpLogOptions}. 
     * 
     * <p>This class is useful when you need to log HTTP traffic for debugging or auditing purposes. It allows you to 
     * control the amount of information that is logged, including the URL, headers, and body of requests and responses.</p> 
     * 
     * <p><b>NOTE:</b> Enabling body logging (using the {@link HttpLogDetailLevel#BODY BODY} or 
     * {@link HttpLogDetailLevel#BODY_AND_HEADERS BODY_AND_HEADERS} levels) will buffer the response body into memory even 
     * if it is never consumed by your application, possibly impacting performance.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an {@code HttpLogOptions} is created and the log level is set to 
     * {@code HttpLogDetailLevel.BODY_AND_HEADERS}. This means that the URL, HTTP method, headers, and body content of 
     * each request and response will be logged. The {@code HttpLogOptions} is then used to create an 
     * {@code HttpLoggingPolicy}, which can then added to the pipeline.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.HttpLoggingPolicy.constructor --> 
     * <pre> 
     * HttpLogOptions logOptions = new HttpLogOptions(); 
     * logOptions.setLogLevel(HttpLogDetailLevel.BODY_AND_HEADERS); 
     * HttpLoggingPolicy loggingPolicy = new HttpLoggingPolicy(logOptions); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.HttpLoggingPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.policy.HttpLogOptions 
     * @see com.azure.core.http.policy.HttpLogDetailLevel 
     */ 
    public class HttpLoggingPolicy implements HttpPipelinePolicy { 
        /** 
         * Key for {@link Context} to pass request retry count metadata for logging. 
         */ 
        public static final String RETRY_COUNT_CONTEXT = "requestRetryCount"; 
        /** 
         * Creates an HttpLoggingPolicy with the given log configurations. 
         * 
         * @param httpLogOptions The HTTP logging configuration options. 
         */ 
        public HttpLoggingPolicy(HttpLogOptions httpLogOptions) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    @FunctionalInterface
    /** 
     * A policy within the {@link HttpPipeline}. 
     * 
     * @see HttpPipeline 
     */ 
    public interface HttpPipelinePolicy { 
        /** 
         * Gets the position to place the policy. 
         * <p> 
         * By default pipeline policies are positioned {@link HttpPipelinePosition#PER_RETRY}. 
         * 
         * @return The position to place the policy. 
         */ 
        default HttpPipelinePosition getPipelinePosition() 
        /** 
         * Processes provided request context and invokes the next policy. 
         * 
         * @param context The request context. 
         * @param next The next policy to invoke. 
         * @return A publisher that initiates the request upon subscription and emits a response on completion. 
         */ 
        Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        /** 
         * Processes provided request context and invokes the next policy synchronously. 
         * 
         * @param context The request context. 
         * @param next The next policy to invoke. 
         * @return A publisher that initiates the request upon subscription and emits a response on completion. 
         */ 
        default HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code HttpPipelineSyncPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This 
     * policy represents a synchronous operation within the HTTP pipeline, meaning it doesn't perform any asynchronous or 
     * synchronously blocking operations. 
     * 
     * <p>This class is useful when you need to perform operations in the HTTP pipeline that don't require 
     * asynchronous processing or blocking. It provides hooks to perform actions before the request is sent and after the 
     * response is received.</p> 
     * 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.HttpPipelineCallContext 
     */ 
    public class HttpPipelineSyncPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a new instance of {@link HttpPipelineSyncPolicy}. 
         */ 
        public HttpPipelineSyncPolicy() 
        /** 
         * Method is invoked after the response is received. 
         * 
         * @param context The request context. 
         * @param response The response received. 
         * @return The transformed response. 
         */ 
        protected HttpResponse afterReceivedResponse(HttpPipelineCallContext context, HttpResponse response) 
        /** 
         * Method is invoked before the request is sent. 
         * 
         * @param context The request context. 
         */ 
        protected void beforeSendingRequest(HttpPipelineCallContext context) 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public final Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public final HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * Implementing classes automatically provide policies. 
     */ 
    public interface HttpPolicyProvider { 
        /** 
         * Creates the policy. 
         * @return the policy that was created. 
         */ 
        HttpPipelinePolicy create() 
    } 
    /** 
     * The {@code HttpPolicyProviders} class is responsible for adding Service Provider Interface (SPI) pluggable policies 
     * to an HTTP pipeline automatically. 
     * 
     * <p>This class is useful when you need to add custom policies to the HTTP pipeline that are loaded using Java's 
     * {@link ServiceLoader}. It provides methods to add policies before and after the retry policy in the pipeline.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, an empty list of policies is created. Then, the 
     * {@code HttpPolicyProviders.addBeforeRetryPolicies} method is used to add policies that should be executed before 
     * the retry policy. The {@code HttpPolicyProviders.addAfterRetryPolicies} method is used to add policies that should 
     * be executed after the retry policy. The list of policies can then be used to build an HTTP pipeline.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.HttpPolicyProviders.usage --> 
     * <pre> 
     * List<HttpPipelinePolicy> policies = new ArrayList<>(); 
     * // Add policies that should be executed before the retry policy 
     * HttpPolicyProviders.addBeforeRetryPolicies(policies); 
     * // Add the retry policy 
     * policies.add(new RetryPolicy()); 
     * // Add policies that should be executed after the retry policy 
     * HttpPolicyProviders.addAfterRetryPolicies(policies); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.HttpPolicyProviders.usage --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.policy.BeforeRetryPolicyProvider 
     * @see com.azure.core.http.policy.AfterRetryPolicyProvider 
     */ 
    public final class HttpPolicyProviders { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Adds SPI policies that implement {@link AfterRetryPolicyProvider}. 
         * 
         * @param policies Policy list to append the policies. 
         */ 
        public static void addAfterRetryPolicies(List<HttpPipelinePolicy> policies) 
        /** 
         * Adds SPI policies that implement {@link BeforeRetryPolicyProvider}. 
         * 
         * @param policies Policy list to append the policies. 
         */ 
        public static void addBeforeRetryPolicies(List<HttpPipelinePolicy> policies) 
    } 
    @FunctionalInterface
    /** 
     * Manages logging HTTP requests in {@link HttpLoggingPolicy}. 
     */ 
    public interface HttpRequestLogger { 
        /** 
         * Gets the {@link LogLevel} used to log the HTTP request. 
         * <p> 
         * By default, this will return {@link LogLevel#INFORMATIONAL}. 
         * 
         * @param loggingOptions The information available during request logging. 
         * @return The {@link LogLevel} used to log the HTTP request. 
         */ 
        default LogLevel getLogLevel(HttpRequestLoggingContext loggingOptions) 
        /** 
         * Logs the HTTP request. 
         * <p> 
         * To get the {@link LogLevel} used to log the HTTP request use {@link #getLogLevel(HttpRequestLoggingContext)}. 
         * 
         * @param logger The {@link ClientLogger} used to log the HTTP request. 
         * @param loggingOptions The information available during request logging. 
         * @return A reactive response that indicates that the HTTP request has been logged. 
         */ 
        Mono<Void> logRequest(ClientLogger logger, HttpRequestLoggingContext loggingOptions) 
        /** 
         * Logs the HTTP request. 
         * To get the {@link LogLevel} used to log the HTTP request use {@link #getLogLevel(HttpRequestLoggingContext)}. 
         * 
         * @param logger The {@link ClientLogger} used to log the HTTP request. 
         * @param loggingOptions The information available during request logging. 
         */ 
        default void logRequestSync(ClientLogger logger, HttpRequestLoggingContext loggingOptions) 
    } 
    /** 
     * The {@code HttpRequestLoggingContext} class provides contextual information available during HTTP request logging. 
     * 
     * <p>This class is useful when you need to access information about an HTTP request during logging. It provides 
     * access to the HTTP request being sent, the contextual information about the request, and the try count for the 
     * request.</p> 
     * 
     * 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.util.Context 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     */ 
    public final class HttpRequestLoggingContext { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets the contextual information about the HTTP request. 
         * 
         * @return The contextual information. 
         */ 
        public Context getContext() 
        /** 
         * Gets the HTTP request being sent. 
         * 
         * @return The HTTP request. 
         */ 
        public HttpRequest getHttpRequest() 
        /** 
         * Gets the try count for the HTTP request. 
         * 
         * @return The HTTP request try count. 
         */ 
        public Integer getTryCount() 
    } 
    @FunctionalInterface
    /** 
     * Manages logging HTTP responses in {@link HttpLoggingPolicy}. 
     */ 
    public interface HttpResponseLogger { 
        /** 
         * Gets the {@link LogLevel} used to log the HTTP response. 
         * <p> 
         * By default, this will return {@link LogLevel#INFORMATIONAL}. 
         * 
         * @param loggingOptions The information available during response logging. 
         * @return The {@link LogLevel} used to log the HTTP response. 
         */ 
        default LogLevel getLogLevel(HttpResponseLoggingContext loggingOptions) 
        /** 
         * Logs the HTTP response. 
         * <p> 
         * To get the {@link LogLevel} used to log the HTTP response use {@link #getLogLevel(HttpResponseLoggingContext)}. 
         * 
         * @param logger The {@link ClientLogger} used to log the response. 
         * @param loggingOptions The information available during response logging. 
         * @return A reactive response that returns the HTTP response that was logged. 
         */ 
        Mono<HttpResponse> logResponse(ClientLogger logger, HttpResponseLoggingContext loggingOptions) 
        /** 
         * Logs the HTTP response. 
         * To get the {@link LogLevel} used to log the HTTP response use {@link #getLogLevel(HttpResponseLoggingContext)} . 
         * 
         * @param logger The {@link ClientLogger} used to log the response. 
         * @param loggingOptions The information available during response logging. 
         * @return A response that returns the HTTP response that was logged. 
         */ 
        default HttpResponse logResponseSync(ClientLogger logger, HttpResponseLoggingContext loggingOptions) 
    } 
    /** 
     * The {@code HttpResponseLoggingContext} class provides contextual information available during HTTP response logging. 
     * 
     * <p>This class is useful when you need to access information about an HTTP response during logging. It provides 
     * access to the HTTP response being received, the duration between the HTTP request being sent and the HTTP response 
     * being received, the contextual information about the response, and the try count for the request.</p> 
     * 
     * @see com.azure.core.http.HttpResponse 
     * @see java.time.Duration 
     * @see com.azure.core.util.Context 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     */ 
    public final class HttpResponseLoggingContext { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets the contextual information about the HTTP response. 
         * 
         * @return The contextual information. 
         */ 
        public Context getContext() 
        /** 
         * Gets the HTTP response being received. 
         * 
         * @return The HTTP response being received. 
         */ 
        public HttpResponse getHttpResponse() 
        /** 
         * Gets the duration between the HTTP request being sent and the HTTP response being received. 
         * 
         * @return The duration between the HTTP request being sent and the HTTP response being received. 
         */ 
        public Duration getResponseDuration() 
        /** 
         * Gets the try count for the HTTP request associated to the HTTP response. 
         * 
         * @return The HTTP request try count. 
         */ 
        public Integer getTryCount() 
    } 
    /** 
     * The {@code KeyCredentialPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy 
     * uses a {@link KeyCredential} to set the authorization key for a request in the form of a header. 
     * 
     * <p>This class is useful when you need to authorize requests with a key. It ensures that the requests are sent over 
     * HTTPS to prevent the key from being leaked. The key is set in the header of the HTTP request.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code KeyCredentialPolicy} is created with a key and a header name. The policy can then be 
     * added to the pipeline. The request sent by the pipeline will then include the specified header with the key as its 
     * value.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.KeyCredentialPolicy.constructor --> 
     * <pre> 
     * KeyCredential credential = new KeyCredential("my_key"); 
     * KeyCredentialPolicy policy = new KeyCredentialPolicy("my_header", credential); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.KeyCredentialPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.credential.KeyCredential 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.HttpHeaders 
     */ 
    public class KeyCredentialPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a policy that uses the passed {@link KeyCredential} to set the specified header name. 
         * 
         * @param name The name of the key header that will be set to {@link KeyCredential#getKey()}. 
         * @param credential The {@link KeyCredential} containing the authorization key to use. 
         * @throws NullPointerException If {@code name} or {@code credential} is {@code null}. 
         * @throws IllegalArgumentException If {@code name} is empty. 
         */ 
        public KeyCredentialPolicy(String name, KeyCredential credential) 
        /** 
         * Creates a policy that uses the passed {@link KeyCredential} to set the specified header name. 
         * <p> 
         * The {@code prefix} will be applied before the {@link KeyCredential#getKey()} when setting the header. A 
         * space will be inserted between {@code prefix} and credential. 
         * 
         * @param name The name of the key header that will be set to {@link KeyCredential#getKey()}. 
         * @param credential The {@link KeyCredential} containing the authorization key to use. 
         * @param prefix The prefix to apply before the credential, for example "SharedAccessKey credential". 
         * @throws NullPointerException If {@code name} or {@code credential} is {@code null}. 
         * @throws IllegalArgumentException If {@code name} is empty. 
         */ 
        public KeyCredentialPolicy(String name, KeyCredential credential, String prefix) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code PortPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is used 
     * to add a specific port to each {@link HttpRequest}. 
     * 
     * <p>This class is useful when you need to set a specific port for all requests in a pipeline. It ensures that the 
     * port is set correctly for each request.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code PortPolicy} is created with a port of 8080 and an overwrite flag set to true. The 
     * policy can then be added to the pipeline. Once added to the pipeline, all requests will have their port set to 8080 
     * by the {@code PortPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.PortPolicy.constructor --> 
     * <pre> 
     * PortPolicy portPolicy = new PortPolicy(8080, true); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.PortPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class PortPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a new PortPolicy object. 
         * 
         * @param port The port to set. 
         * @param overwrite Whether to overwrite a {@link HttpRequest HttpRequest's} port if it already has one. 
         */ 
        public PortPolicy(int port, boolean overwrite) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code ProtocolPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is 
     * used to add a specific protocol to each {@code HttpRequest}. 
     * 
     * <p>This class is useful when you need to set a specific protocol for all requests in a pipeline. It ensures that the 
     * protocol is set correctly for each request.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code ProtocolPolicy} is created with a protocol of "https" and an overwrite flag set to 
     * true. The policy can then be added to the pipeline. Once added to the pipeline, requests have their protocol set to 
     * "https" by the {@code ProtocolPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.ProtocolPolicy.constructor --> 
     * <pre> 
     * ProtocolPolicy protocolPolicy = new ProtocolPolicy("https", true); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.ProtocolPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     */ 
    public class ProtocolPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a new ProtocolPolicy. 
         * 
         * @param protocol The protocol to set. 
         * @param overwrite Whether to overwrite a HttpRequest's protocol if it already has one. 
         */ 
        public ProtocolPolicy(String protocol, boolean overwrite) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The {@code RedirectPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy 
     * handles HTTP redirects by determining if an HTTP request should be redirected based on the received 
     * {@link HttpResponse}. 
     * 
     * <p>This class is useful when you need to handle HTTP redirects in a pipeline. It uses a {@link RedirectStrategy} to 
     * decide if a request should be redirected. By default, it uses the {@link DefaultRedirectStrategy}, which redirects 
     * the request based on the HTTP status code of the response.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code RedirectPolicy} is constructed and can be added to a pipeline. For a request sent by the 
     * pipeline, if the server responds with a redirect status code, the request will be redirected according 
     * to the {@link RedirectStrategy} used by the {@code RedirectPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.RedirectPolicy.constructor --> 
     * <pre> 
     * RedirectPolicy redirectPolicy = new RedirectPolicy(); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.RedirectPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.policy.RedirectStrategy 
     * @see com.azure.core.http.policy.DefaultRedirectStrategy 
     */ 
    public final class RedirectPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates {@link RedirectPolicy} with default {@link DefaultRedirectStrategy} as {@link RedirectStrategy} and 
         * uses the redirect status response code (301, 302, 307, 308) to determine if this request should be redirected. 
         */ 
        public RedirectPolicy() 
        /** 
         * Creates {@link RedirectPolicy} with the provided {@code redirectStrategy} as {@link RedirectStrategy} 
         * to determine if this request should be redirected. 
         * 
         * @param redirectStrategy The {@link RedirectStrategy} used for redirection. 
         * @throws NullPointerException When {@code redirectStrategy} is {@code null}. 
         */ 
        public RedirectPolicy(RedirectStrategy redirectStrategy) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The interface for determining the {@link RedirectStrategy redirect strategy} used in {@link RedirectPolicy}. 
     */ 
    public interface RedirectStrategy { 
        /** 
         * Creates an {@link HttpRequest request} for the redirect attempt. 
         * 
         * @param httpResponse the {@link HttpResponse} containing the redirect url present in the response headers 
         * @return the modified {@link HttpRequest} to redirect the incoming request. 
         */ 
        HttpRequest createRedirectRequest(HttpResponse httpResponse) 
        /** 
         * Max number of redirect attempts to be made. 
         * 
         * @return The max number of redirect attempts. 
         */ 
        int getMaxAttempts() 
        /** 
         * Determines if the url should be redirected between each try. 
         * 
         * @param context the {@link HttpPipelineCallContext HTTP pipeline context}. 
         * @param httpResponse the {@link HttpRequest} containing the redirect url present in the response headers 
         * @param tryCount redirect attempts so far 
         * @param attemptedRedirectUrls attempted redirect locations used so far. 
         * @return {@code true} if the request should be redirected, {@code false} otherwise 
         */ 
        boolean shouldAttemptRedirect(HttpPipelineCallContext context, HttpResponse httpResponse, int tryCount, Set<String> attemptedRedirectUrls) 
    } 
    /** 
     * The {@code RequestIdPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is 
     * used to add a unique identifier to each {@link HttpRequest} in the form of a UUID in the request header. Azure 
     * uses the request id as the unique identifier for the request. 
     * 
     * <p>This class is useful when you need to track HTTP requests for debugging or auditing purposes. It allows you to 
     * specify a custom header name for the request id, or use the default header name 'x-ms-client-request-id'.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code RequestIdPolicy} is created with a custom header name. Once added to the pipeline 
     * requests will have their request id set in the 'x-ms-my-custom-request-id' header by the {@code RequestIdPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.RequestIdPolicy.constructor --> 
     * <pre> 
     * // Using the default header name 
     * RequestIdPolicy defaultPolicy = new RequestIdPolicy(); 
     * // Using a custom header name 
     * RequestIdPolicy customRequestIdPolicy = new RequestIdPolicy("x-ms-my-custom-request-id"); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.RequestIdPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.HttpHeaders 
     */ 
    public class RequestIdPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates default {@link RequestIdPolicy} with default header name 'x-ms-client-request-id'. 
         */ 
        public RequestIdPolicy() 
        /** 
         * Creates  {@link RequestIdPolicy} with provided {@code requestIdHeaderName}. 
         * @param requestIdHeaderName to be used to set in {@link HttpRequest}. 
         */ 
        public RequestIdPolicy(String requestIdHeaderName) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * Information about the request that failed, used to determine whether a retry should be attempted. 
     */ 
    public final class RequestRetryCondition { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets the HTTP response of the request that failed. 
         * <p> 
         * This may be null if the request failed with a throwable and no response was received. 
         * 
         * @return The HTTP response of the request that failed. 
         */ 
        public HttpResponse getResponse() 
        /** 
         * Gets the unmodifiable list of throwables that have been encountered during retries. 
         * 
         * @return The unmodifiable list of throwables that have been encountered during retries. 
         */ 
        public List<Throwable> getRetriedThrowables() 
        /** 
         * Gets the throwable of the request that failed. 
         * <p> 
         * This may be null if the request failed with a response and no throwable was received. 
         * 
         * @return The throwable of the request that failed. 
         */ 
        public Throwable getThrowable() 
        /** 
         * Gets the number of tries that have been attempted. 
         * 
         * @return The number of tries that have been attempted. 
         */ 
        public int getTryCount() 
    } 
    /** 
     * The {@code RetryOptions} class provides configuration options for retry strategies. It supports both 
     * {@link ExponentialBackoffOptions} and {@link FixedDelayOptions}. 
     * 
     * <p>This class is useful when you need to customize the behavior of retries in the HTTP pipeline. It allows you to 
     * specify the type of retry strategy and its options.</p> 
     * 
     * <p>Here's a code sample of how to use this class:</p> 
     * 
     * <p>In these examples, {@code RetryOptions} is created with either {@code ExponentialBackoffOptions} or 
     * {@code FixedDelayOptions}. These options can then be used to configure a retry policy in the HTTP pipeline.</p> 
     * 
     * <pre> 
     * {@code 
     * // Using ExponentialBackoffOptions 
     * ExponentialBackoffOptions exponentialOptions = new ExponentialBackoffOptions() 
     *     .setMaxRetries(5) 
     *     .setBaseDelay(Duration.ofSeconds(1)) 
     *     .setMaxDelay(Duration.ofSeconds(10)); 
     * RetryOptions retryOptions = new RetryOptions(exponentialOptions); 
     * 
     * // Using FixedDelayOptions 
     * FixedDelayOptions fixedOptions = new FixedDelayOptions(3, Duration.ofSeconds(1)); 
     * RetryOptions retryOptions = new RetryOptions(fixedOptions); 
     * } 
     * </pre> 
     * 
     * @see com.azure.core.http.policy.RetryPolicy 
     * @see com.azure.core.http.policy.ExponentialBackoffOptions 
     * @see com.azure.core.http.policy.FixedDelayOptions 
     */ 
    public class RetryOptions { 
        /** 
         * Creates a new instance that uses {@link ExponentialBackoffOptions}. 
         * 
         * @param exponentialBackoffOptions The {@link ExponentialBackoffOptions}. 
         */ 
        public RetryOptions(ExponentialBackoffOptions exponentialBackoffOptions) 
        /** 
         * Creates a new instance that uses {@link FixedDelayOptions}. 
         * 
         * @param fixedDelayOptions The {@link FixedDelayOptions}. 
         */ 
        public RetryOptions(FixedDelayOptions fixedDelayOptions) 
        /** 
         * Gets the configuration for exponential backoff if configured. 
         * 
         * @return The {@link ExponentialBackoffOptions}. 
         */ 
        public ExponentialBackoffOptions getExponentialBackoffOptions() 
        /** 
         * Gets the configuration for exponential backoff if configured. 
         * 
         * @return The {@link FixedDelayOptions}. 
         */ 
        public FixedDelayOptions getFixedDelayOptions() 
        /** 
         * Gets the predicate that determines if a retry should be attempted. 
         * <p> 
         * If null, the default behavior is to retry HTTP responses with status codes 408, 429, and any 500 status code that 
         * isn't 501 or 505. And to retry any {@link Exception}. 
         * 
         * @return The predicate that determines if a retry should be attempted. 
         */ 
        public Predicate<RequestRetryCondition> getShouldRetryCondition() 
        /** 
         * Sets the predicate that determines if a retry should be attempted. 
         * <p> 
         * If null, the default behavior is to retry HTTP responses with status codes 408, 429, and any 500 status code that 
         * isn't 501 or 505. And to retry any {@link Exception}. 
         * 
         * @param shouldRetryCondition The predicate that determines if a retry should be attempted for the given 
         * {@link HttpResponse}. 
         * @return The updated {@link RetryOptions} object. 
         */ 
        public RetryOptions setShouldRetryCondition(Predicate<RequestRetryCondition> shouldRetryCondition) 
    } 
    /** 
     * The {@code RetryPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy handles 
     * HTTP retries by determining if an HTTP request should be retried based on the received {@link HttpResponse}. 
     * 
     * <p>This class is useful when you need to handle HTTP retries in a pipeline. It uses a {@link RetryStrategy} to 
     * decide if a request should be retried. By default, it uses the {@link ExponentialBackoff} strategy, which uses 
     * a delay duration that exponentially increases with each retry attempt until an upper bound is reached.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code RetryPolicy} is created which can then be added to the pipeline. For the request then 
     * sent by the pipeline, if the server responds with a status code that indicates a transient error, the request will be 
     * retried according to the {@link RetryStrategy} used by the {@code RetryPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.RetryPolicy.constructor --> 
     * <pre> 
     * RetryPolicy retryPolicy = new RetryPolicy(); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.RetryPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.policy.RetryStrategy 
     * @see com.azure.core.http.policy.DefaultRedirectStrategy 
     */ 
    public class RetryPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates {@link RetryPolicy} using {@link ExponentialBackoff#ExponentialBackoff()} as the {@link RetryStrategy}. 
         */ 
        public RetryPolicy() 
        /** 
         * Creates a {@link RetryPolicy} with the provided {@link RetryStrategy}. 
         * 
         * @param retryStrategy The {@link RetryStrategy} used for retries. 
         * @throws NullPointerException If {@code retryStrategy} is null. 
         */ 
        public RetryPolicy(RetryStrategy retryStrategy) 
        /** 
         * Creates a {@link RetryPolicy} with the provided {@link RetryOptions}. 
         * 
         * @param retryOptions The {@link RetryOptions} used to configure this {@link RetryPolicy}. 
         * @throws NullPointerException If {@code retryOptions} is null. 
         */ 
        public RetryPolicy(RetryOptions retryOptions) 
        /** 
         * Creates {@link RetryPolicy} using {@link ExponentialBackoff#ExponentialBackoff()} as the {@link RetryStrategy} 
         * and uses {@code retryAfterHeader} to look up the wait period in the returned {@link HttpResponse} to calculate 
         * the retry delay when a recoverable HTTP error is returned. 
         * 
         * @param retryAfterHeader The HTTP header, such as {@code Retry-After} or {@code x-ms-retry-after-ms}, to lookup 
         * for the retry delay. If the value is null, {@link RetryStrategy#calculateRetryDelay(RequestRetryCondition)} will 
         * compute the delay and ignore the delay provided in response header. 
         * @param retryAfterTimeUnit The time unit to use when applying the retry delay. Null is valid if, and only if, 
         * {@code retryAfterHeader} is null. 
         * @throws NullPointerException When {@code retryAfterTimeUnit} is null and {@code retryAfterHeader} is not null. 
         */ 
        public RetryPolicy(String retryAfterHeader, ChronoUnit retryAfterTimeUnit) 
        /** 
         * Creates {@link RetryPolicy} with the provided {@link RetryStrategy} and default {@link ExponentialBackoff} as 
         * {@link RetryStrategy}. It will use provided {@code retryAfterHeader} in {@link HttpResponse} headers for 
         * calculating retry delay. 
         * 
         * @param retryStrategy The {@link RetryStrategy} used for retries. 
         * @param retryAfterHeader The HTTP header, such as 'Retry-After' or 'x-ms-retry-after-ms', to lookup for the retry 
         * delay. If the value is null, {@link RetryPolicy} will use the retry strategy to compute the delay and ignore the 
         * delay provided in response header. 
         * @param retryAfterTimeUnit The time unit to use when applying the retry delay. null is valid if, and only if, 
         * {@code retryAfterHeader} is null. 
         * @throws NullPointerException If {@code retryStrategy} is null or when {@code retryAfterTimeUnit} is null and 
         * {@code retryAfterHeader} is not null. 
         */ 
        public RetryPolicy(RetryStrategy retryStrategy, String retryAfterHeader, ChronoUnit retryAfterTimeUnit) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
    /** 
     * The interface for determining the retry strategy used in {@link RetryPolicy}. 
     */ 
    public interface RetryStrategy { 
        /** 
         * HTTP response status code for {@code Too Many Requests}. 
         */ 
        int HTTP_STATUS_TOO_MANY_REQUESTS = 429; 
        /** 
         * Computes the delay between each retry. 
         * <p> 
         * If both this method and {@link #calculateRetryDelay(RequestRetryCondition)} are overridden, this method is 
         * ignored. 
         * 
         * @param retryAttempts The number of retry attempts completed so far. 
         * @return The delay duration before the next retry. 
         */ 
        Duration calculateRetryDelay(int retryAttempts) 
        /** 
         * Computes the delay between each retry based on the {@link RequestRetryCondition}. 
         * <p> 
         * If this method is not overridden, the {@link #calculateRetryDelay(int)} method is called with 
         * {@link RequestRetryCondition#getTryCount()}. 
         * <p> 
         * If both this method and {@link #calculateRetryDelay(int)} are overridden, this method is used. 
         * 
         * @param requestRetryCondition The {@link RequestRetryCondition} containing information that can be used to 
         * determine the delay. 
         * @return The delay duration before the next retry. 
         * @throws NullPointerException If {@code requestRetryCondition} is null. 
         */ 
        default Duration calculateRetryDelay(RequestRetryCondition requestRetryCondition) 
        /** 
         * Max number of retry attempts to be made. 
         * 
         * @return The max number of retry attempts. 
         */ 
        int getMaxRetries() 
        /** 
         * This method is consulted to determine if a retry attempt should be made for the given {@link HttpResponse} if the 
         * retry attempts are less than {@link #getMaxRetries()}. 
         * 
         * @param httpResponse The response from the previous attempt. 
         * @return Whether a retry should be attempted. 
         */ 
        default boolean shouldRetry(HttpResponse httpResponse) 
        /** 
         * This method is consulted to determine if a retry attempt should be made for the given 
         * {@link RequestRetryCondition}. 
         * <p> 
         * By default, if the {@link RequestRetryCondition} contains a non-null {@link HttpResponse}, then the 
         * {@link #shouldRetry(HttpResponse)} method is called, otherwise the {@link #shouldRetryException(Throwable)} 
         * method is called. 
         * 
         * @param requestRetryCondition The {@link RequestRetryCondition} containing information that can be used to 
         * determine if the request should be retried. 
         * @return Whether a retry should be attempted. 
         */ 
        default boolean shouldRetryCondition(RequestRetryCondition requestRetryCondition) 
        /** 
         * This method is consulted to determine if a retry attempt should be made for the given {@link Throwable} 
         * propagated when the request failed to send. 
         * 
         * @param throwable The {@link Throwable} thrown during the previous attempt. 
         * @return Whether a retry should be attempted. 
         */ 
        default boolean shouldRetryException(Throwable throwable) 
    } 
    @Deprecated
    /** 
     * The pipeline policy that limits the time allowed between sending a request and receiving the response. 
     * 
     * @deprecated Consider configuring timeouts with {@link com.azure.core.util.HttpClientOptions}. 
     */ 
    public class TimeoutPolicy implements HttpPipelinePolicy { 
        /** 
         * Creates a TimeoutPolicy. 
         * 
         * @param timeoutDuration the timeout duration 
         */ 
        public TimeoutPolicy(Duration timeoutDuration) 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
    } 
    /** 
     * The {@code UserAgentPolicy} class is an implementation of the {@link HttpPipelinePolicy} interface. This policy is 
     * used to add a "User-Agent" header to each {@code HttpRequest}. 
     * 
     * <p>This class is useful when you need to add a specific "User-Agent" header for all requests in a pipeline. 
     * It ensures that the "User-Agent" header is set correctly for each request. The "User-Agent" header is used to 
     * provide the server with information about the software used by the client.</p> 
     * 
     * <p><strong>Code sample:</strong></p> 
     * 
     * <p>In this example, a {@code UserAgentPolicy} is created with a "User-Agent" header value of "MyApp/1.0". 
     * Once added to the pipeline, requests will have their "User-Agent" header set to "MyApp/1.0" by the 
     * {@code UserAgentPolicy}.</p> 
     * 
     * <!-- src_embed com.azure.core.http.policy.UserAgentPolicy.constructor --> 
     * <pre> 
     * UserAgentPolicy userAgentPolicy = new UserAgentPolicy("MyApp/1.0"); 
     * </pre> 
     * <!-- end com.azure.core.http.policy.UserAgentPolicy.constructor --> 
     * 
     * @see com.azure.core.http.policy 
     * @see com.azure.core.http.policy.HttpPipelinePolicy 
     * @see com.azure.core.http.HttpPipeline 
     * @see com.azure.core.http.HttpRequest 
     * @see com.azure.core.http.HttpResponse 
     * @see com.azure.core.http.HttpHeaderName 
     */ 
    public class UserAgentPolicy implements HttpPipelinePolicy { 
        /** 
         * Key for {@link Context} to add a value which will override the User-Agent supplied in this policy in an ad-hoc 
         * manner. 
         */ 
        public static final String OVERRIDE_USER_AGENT_CONTEXT_KEY = "Override-User-Agent"; 
        /** 
         * Key for {@link Context} to add a value which will be appended to the User-Agent supplied in this policy in an 
         * ad-hoc manner. 
         */ 
        public static final String APPEND_USER_AGENT_CONTEXT_KEY = "Append-User-Agent"; 
        /** 
         * Creates a {@link UserAgentPolicy} with a default user agent string. 
         */ 
        public UserAgentPolicy() 
        /** 
         * Creates a UserAgentPolicy with {@code userAgent} as the header value. If {@code userAgent} is {@code null}, then 
         * the default user agent value is used. 
         * 
         * @param userAgent The user agent string to add to request headers. 
         */ 
        public UserAgentPolicy(String userAgent) 
        /** 
         * Creates a UserAgentPolicy with the {@code sdkName} and {@code sdkVersion} in the User-Agent header value. 
         * 
         * <p>If the passed configuration contains true for AZURE_TELEMETRY_DISABLED the platform information won't be 
         * included in the user agent.</p> 
         * 
         * @param applicationId User specified application Id. 
         * @param sdkName Name of the client library. 
         * @param sdkVersion Version of the client library. 
         * @param configuration Configuration store that will be checked for {@link 
         * Configuration#PROPERTY_AZURE_TELEMETRY_DISABLED}. If {@code null} is passed the {@link 
         * Configuration#getGlobalConfiguration() global configuration} will be checked. 
         */ 
        public UserAgentPolicy(String applicationId, String sdkName, String sdkVersion, Configuration configuration) 
        /** 
         * Creates a UserAgentPolicy with the {@code sdkName} and {@code sdkVersion} in the User-Agent header value. 
         * 
         * <p>If the passed configuration contains true for AZURE_TELEMETRY_DISABLED the platform information won't be 
         * included in the user agent.</p> 
         * 
         * @param sdkName Name of the client library. 
         * @param sdkVersion Version of the client library. 
         * @param version {@link ServiceVersion} of the service to be used when making requests. 
         * @param configuration Configuration store that will be checked for {@link 
         * Configuration#PROPERTY_AZURE_TELEMETRY_DISABLED}. If {@code null} is passed the {@link 
         * Configuration#getGlobalConfiguration() global configuration} will be checked. 
         * @deprecated Use {@link UserAgentPolicy#UserAgentPolicy(String, String, String, Configuration)} instead. 
         */ 
        @Deprecated public UserAgentPolicy(String sdkName, String sdkVersion, Configuration configuration, ServiceVersion version) 
        /** 
         * Updates the "User-Agent" header with the value supplied in the policy. 
         * 
         * <p>The {@code context} will be checked for {@code Override-User-Agent} and {@code Append-User-Agent}. 
         * {@code Override-User-Agent} will take precedence over the value supplied in the policy, 
         * {@code Append-User-Agent} will be appended to the value supplied in the policy.</p> 
         * 
         * @param context request context 
         * @param next The next policy to invoke. 
         * @return A publisher that initiates the request upon subscription and emits a response on completion. 
         */ 
        @Override public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) 
        /** 
         * Updates the "User-Agent" header with the value supplied in the policy synchronously. 
         * 
         * <p>The {@code context} will be checked for {@code Override-User-Agent} and {@code Append-User-Agent}. 
         * {@code Override-User-Agent} will take precedence over the value supplied in the policy, 
         * {@code Append-User-Agent} will be appended to the value supplied in the policy.</p> 
         * 
         * @param context request context 
         * @param next The next policy to invoke. 
         * @return A response. 
         */ 
        @Override public HttpResponse processSync(HttpPipelineCallContext context, HttpPipelineNextSyncPolicy next) 
    } 
} 
/** 
 * <p>This package contains classes and interfaces that provide RESTful HTTP functionality for Azure SDKs.</p> 
 * 
 * <p>The classes in this package allow you to send HTTP requests to Azure services and handle the responses. They also 
 * provide functionality for handling paged responses from Azure services, which is useful when dealing with large 
 * amounts of data.</p> 
 * 
 * <p>Here are some of the key classes included in this package:</p> 
 * 
 * <ul> 
 *     <li>{@link com.azure.core.http.rest.ResponseBase}: The base class for all responses of a REST request.</li> 
 *     <li>{@link com.azure.core.http.rest.PagedIterable}: Provides utility to iterate over 
 *     {@link com.azure.core.http.rest.PagedResponse} using 
 *     {@link java.util.stream.Stream} and {@link java.lang.Iterable} interfaces.</li> 
 *     <li>{@link com.azure.core.http.rest.PagedFlux}: Provides utility to iterate over {@link com.azure.core.http.rest.PagedResponse} using 
 *     {@link reactor.core.publisher.Flux} and {@link java.lang.Iterable} interfaces.</li> 
 *     <li>{@link com.azure.core.http.rest.SimpleResponse}: Represents a REST response with a strongly-typed content 
 *     deserialized from the response body.</li> 
 * </ul> 
 * 
 * <p>Each class provides useful methods and functionality for dealing with HTTP requests and responses. For example, 
 * the {@link com.azure.core.http.rest.PagedIterable} class provides methods for iterating over paged responses from 
 * Azure services.</p> 
 */ 
package com.azure.core.http.rest { 
    /** 
     * Represents a paginated REST response from the service. 
     * 
     * @param <T> Type of items in the page response. 
     */ 
    public interface Page<T> extends ContinuablePage<String, T> { 
        /** 
         * Get list of elements in the page. 
         * 
         * @return the page elements 
         * 
         * @deprecated use {@link #getElements()}. 
         */ 
        @Deprecated default List<T> getItems() 
    } 
    /** 
     * PagedFlux is a Flux that provides the ability to operate on paginated REST responses of type {@link PagedResponse} 
     * and individual items in such pages. When processing the response by page each response will contain the items in the 
     * page as well as the REST response details such as status code and headers. 
     * 
     * <p> 
     * To process one item at a time, simply subscribe to this flux as shown below 
     * </p> 
     * <p> 
     * <strong>Code sample</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedflux.items --> 
     * <pre> 
     * // Subscribe to process one item at a time 
     * pagedFlux 
     *     .log() 
     *     .subscribe(item -> System.out.println("Processing item with value: " + item), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedflux.items --> 
     * 
     * <p> 
     * To process one page at a time, use {@link #byPage()} method as shown below 
     * </p> 
     * <p> 
     * <strong>Code sample</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedflux.pages --> 
     * <pre> 
     * // Subscribe to process one page at a time from the beginning 
     * pagedFlux 
     *     .byPage() 
     *     .log() 
     *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
     *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedflux.pages --> 
     * 
     * <p> 
     * To process items one page at a time starting from any page associated with a continuation token, 
     * use {@link #byPage(String)} as shown below 
     * </p> 
     * <p> 
     * <strong>Code sample</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedflux.pagesWithContinuationToken --> 
     * <pre> 
     * // Subscribe to process one page at a time starting from a page associated with 
     * // a continuation token 
     * String continuationToken = getContinuationToken(); 
     * pagedFlux 
     *     .byPage(continuationToken) 
     *     .log() 
     *     .doOnSubscribe(ignored -> System.out.println( 
     *         "Subscribed to paged flux processing pages starting from: " + continuationToken)) 
     *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
     *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedflux.pagesWithContinuationToken --> 
     * 
     * @param <T> The type of items in a {@link PagedResponse} 
     * @see PagedResponse 
     * @see Page 
     * @see Flux 
     */ 
    public class PagedFlux<T> extends PagedFluxBase<T, PagedResponse<T>> { 
        /** 
         * Creates an instance of {@link PagedFlux} that consists of only a single page. This constructor takes a {@code 
         * Supplier} that return the single page of {@code T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.pagedflux.singlepage.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<Mono<PagedResponse<Integer>>> firstPageRetrieverFunction = () -> getFirstPage(); 
         * 
         * PagedFlux<Integer> pagedFluxInstance = new PagedFlux<>(firstPageRetrieverFunction, 
         *     nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedflux.singlepage.instantiation --> 
         * 
         * @param firstPageRetriever Supplier that retrieves the first page. 
         */ 
        public PagedFlux(Supplier<Mono<PagedResponse<T>>> firstPageRetriever) 
        /** 
         * Creates an instance of {@link PagedFlux} that consists of only a single page with a given element count. 
         * 
         * <p><strong>Code sample</strong></p> 
         * 
         * <!-- src_embed com.azure.core.http.rest.PagedFlux.singlepage.instantiationWithPageSize --> 
         * <pre> 
         * // A function that fetches the single page of data from a source/service. 
         * Function<Integer, Mono<PagedResponse<Integer>>> singlePageRetriever = pageSize -> 
         *     getFirstPageWithSize(pageSize); 
         * 
         * PagedFlux<Integer> singlePageFluxWithPageSize = new PagedFlux<Integer>(singlePageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedFlux.singlepage.instantiationWithPageSize --> 
         * 
         * @param firstPageRetriever Function that retrieves the first page. 
         */ 
        public PagedFlux(Function<Integer, Mono<PagedResponse<T>>> firstPageRetriever) 
        /** 
         * Creates an instance of {@link PagedFlux}. The constructor takes a {@code Supplier} and {@code Function}. The 
         * {@code Supplier} returns the first page of {@code T}, the {@code Function} retrieves subsequent pages of {@code 
         * T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * 
         * <!-- src_embed com.azure.core.http.rest.pagedflux.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<Mono<PagedResponse<Integer>>> firstPageRetriever = () -> getFirstPage(); 
         * 
         * // A function that fetches subsequent pages of data from source/service given a continuation token 
         * Function<String, Mono<PagedResponse<Integer>>> nextPageRetriever = 
         *     continuationToken -> getNextPage(continuationToken); 
         * 
         * PagedFlux<Integer> pagedFlux = new PagedFlux<>(firstPageRetriever, 
         *     nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedflux.instantiation --> 
         * 
         * @param firstPageRetriever Supplier that retrieves the first page 
         * @param nextPageRetriever Function that retrieves the next page given a continuation token 
         */ 
        public PagedFlux(Supplier<Mono<PagedResponse<T>>> firstPageRetriever, Function<String, Mono<PagedResponse<T>>> nextPageRetriever) 
        /** 
         * Creates an instance of {@link PagedFlux} that is capable of retrieving multiple pages with of a given page size. 
         * 
         * <p><strong>Code sample</strong></p> 
         * 
         * <!-- src_embed com.azure.core.http.rest.PagedFlux.instantiationWithPageSize --> 
         * <pre> 
         * // A function that fetches the first page of data from a source/service. 
         * Function<Integer, Mono<PagedResponse<Integer>>> firstPageRetriever = pageSize -> getFirstPageWithSize(pageSize); 
         * 
         * // A function that fetches subsequent pages of data from a source/service given a continuation token. 
         * BiFunction<String, Integer, Mono<PagedResponse<Integer>>> nextPageRetriever = (continuationToken, pageSize) -> 
         *     getNextPageWithSize(continuationToken, pageSize); 
         * 
         * PagedFlux<Integer> pagedFluxWithPageSize = new PagedFlux<>(firstPageRetriever, nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedFlux.instantiationWithPageSize --> 
         * 
         * @param firstPageRetriever Function that retrieves the first page. 
         * @param nextPageRetriever BiFunction that retrieves the next page given a continuation token and page size. 
         */ 
        public PagedFlux(Function<Integer, Mono<PagedResponse<T>>> firstPageRetriever, BiFunction<String, Integer, Mono<PagedResponse<T>>> nextPageRetriever) 
        /** 
         * Creates an instance of {@link PagedFlux} backed by a Page Retriever Supplier (provider). When invoked provider 
         * should return {@link PageRetriever}. The provider will be called for each Subscription to the PagedFlux instance. 
         * The Page Retriever can get called multiple times in serial fashion, each time after the completion of the Flux 
         * returned from the previous invocation. The final completion signal will be send to the Subscriber when the last 
         * Page emitted by the Flux returned by Page Retriever has {@code null} continuation token. 
         * 
         * The provider is useful mainly in two scenarios: 
         * <ul> 
         * <li> To manage state across multiple call to Page Retrieval within the same Subscription. 
         * <li> To decorate a PagedFlux to produce new PagedFlux. 
         * </ul> 
         * 
         * <p><strong>Decoration sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.pagedflux.create.decoration --> 
         * <pre> 
         * 
         * // Transform a PagedFlux with Integer items to PagedFlux of String items. 
         * final PagedFlux<Integer> intPagedFlux = createAnInstance(); 
         * 
         * // PagedResponse<Integer> to PagedResponse<String> mapper 
         * final Function<PagedResponse<Integer>, PagedResponse<String>> responseMapper 
         *     = intResponse -> new PagedResponseBase<Void, String>(intResponse.getRequest(), 
         *     intResponse.getStatusCode(), 
         *     intResponse.getHeaders(), 
         *     intResponse.getValue() 
         *         .stream() 
         *         .map(intValue -> Integer.toString(intValue)).collect(Collectors.toList()), 
         *     intResponse.getContinuationToken(), 
         *     null); 
         * 
         * final Supplier<PageRetriever<String, PagedResponse<String>>> provider = () -> 
         *     (continuationToken, pageSize) -> { 
         *         Flux<PagedResponse<Integer>> flux = (continuationToken == null) 
         *             ? intPagedFlux.byPage() 
         *             : intPagedFlux.byPage(continuationToken); 
         *         return flux.map(responseMapper); 
         *     }; 
         * PagedFlux<String> strPagedFlux = PagedFlux.create(provider); 
         * 
         * // Create a PagedFlux from a PagedFlux with all exceptions mapped to a specific exception. 
         * final PagedFlux<Integer> pagedFlux = createAnInstance(); 
         * final Supplier<PageRetriever<String, PagedResponse<Integer>>> exceptionProvider = () -> 
         *     (continuationToken, pageSize) -> { 
         *         Flux<PagedResponse<Integer>> flux = (continuationToken == null) 
         *             ? pagedFlux.byPage() 
         *             : pagedFlux.byPage(continuationToken); 
         *         return flux.onErrorMap(Exception.class, PaginationException::new); 
         *     }; 
         * final PagedFlux<Integer> exceptionMappedPagedFlux = PagedFlux.create(exceptionProvider); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedflux.create.decoration --> 
         * 
         * @param provider the Page Retrieval Provider 
         * @param <T> The type of items in a {@link PagedResponse} 
         * @return PagedFlux backed by the Page Retriever Function Supplier 
         */ 
        public static <T> PagedFlux<T> create(Supplier<PageRetriever<String, PagedResponse<T>>> provider) 
        /** 
         * Maps this PagedFlux instance of T to a PagedFlux instance of type S as per the provided mapper function. 
         * 
         * @param mapper The mapper function to convert from type T to type S. 
         * @param <S> The mapped type. 
         * @return A PagedFlux of type S. 
         * @deprecated refer the decoration samples for {@link PagedFlux#create(Supplier)}. 
         */ 
        @Deprecated public <S> PagedFlux<S> mapPage(Function<T, S> mapper) 
    } 
    @Deprecated
    /** 
     * This class is a flux that can operate on any type that extends {@link PagedResponse} and also provides the ability to 
     * operate on individual items. When processing the response by page, each response will contain the items in the page 
     * as well as the request details like status code and headers. 
     * 
     * <p> 
     * <strong>Process each item in Flux</strong> 
     * </p> 
     * <p> 
     * To process one item at a time, simply subscribe to this Flux. 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.items --> 
     * <pre> 
     * pagedFluxBase 
     *     .log() 
     *     .subscribe(item -> System.out.println("Processing item with value: " + item), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedfluxbase.items --> 
     * 
     * <p> 
     * <strong>Process one page at a time</strong> 
     * </p> 
     * <p> 
     * To process one page at a time, starting from the beginning, use {@link #byPage() byPage()} method. 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.pages --> 
     * <pre> 
     * pagedFluxBase 
     *     .byPage() 
     *     .log() 
     *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
     *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedfluxbase.pages --> 
     * 
     * <p> 
     * <strong>Process items starting from a continuation token</strong> 
     * </p> 
     * <p> 
     * To process items one page at a time starting from any page associated with a continuation token, use 
     * {@link #byPage(String)}. 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.pagesWithContinuationToken --> 
     * <pre> 
     * String continuationToken = getContinuationToken(); 
     * pagedFluxBase 
     *     .byPage(continuationToken) 
     *     .log() 
     *     .doOnSubscribe(ignored -> System.out.println( 
     *         "Subscribed to paged flux processing pages starting from: " + continuationToken)) 
     *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
     *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
     *         error -> System.err.println("An error occurred: " + error), 
     *         () -> System.out.println("Processing complete.")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedfluxbase.pagesWithContinuationToken --> 
     * 
     * @param <T> The type of items in {@code P}. 
     * @param <P> The {@link PagedResponse} holding items of type {@code T}. 
     * @see PagedResponse 
     * @see Page 
     * @see Flux 
     * @deprecated use {@link ContinuablePagedFluxCore}. 
     */ 
    public class PagedFluxBase<T, P extends PagedResponse<T>> extends ContinuablePagedFluxCore<String, T, P> { 
        /** 
         * Creates an instance of {@link PagedFluxBase} that consists of only a single page. This constructor takes a {@code 
         * Supplier} that return the single page of {@code T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.singlepage.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<Mono<PagedResponse<Integer>>> firstPageRetrieverFunction = () -> getFirstPage(); 
         * 
         * PagedFluxBase<Integer, PagedResponse<Integer>> pagedFluxBaseInstance = 
         *     new PagedFluxBase<>(firstPageRetrieverFunction, 
         *         nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedfluxbase.singlepage.instantiation --> 
         * 
         * @param firstPageRetriever Supplier that retrieves the first page. 
         */ 
        public PagedFluxBase(Supplier<Mono<P>> firstPageRetriever) 
        /** 
         * Creates an instance of {@link PagedFluxBase}. The constructor takes a {@code Supplier} and {@code Function}. The 
         * {@code Supplier} returns the first page of {@code T}, the {@code Function} retrieves subsequent pages of {@code 
         * T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<Mono<PagedResponse<Integer>>> firstPageRetriever = () -> getFirstPage(); 
         * 
         * // A function that fetches subsequent pages of data from source/service given a continuation token 
         * Function<String, Mono<PagedResponse<Integer>>> nextPageRetriever = 
         *     continuationToken -> getNextPage(continuationToken); 
         * 
         * PagedFluxBase<Integer, PagedResponse<Integer>> pagedFluxBase = new PagedFluxBase<>(firstPageRetriever, 
         *     nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedfluxbase.instantiation --> 
         * 
         * @param firstPageRetriever Supplier that retrieves the first page 
         * @param nextPageRetriever Function that retrieves the next page given a continuation token 
         */ 
        public PagedFluxBase(Supplier<Mono<P>> firstPageRetriever, Function<String, Mono<P>> nextPageRetriever) 
        /** 
         * Creates a Flux of {@link PagedResponse} starting from the first page. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedFluxBase.byPage --> 
         * <pre> 
         * // Start processing the results from first page 
         * pagedFluxBase.byPage() 
         *     .log() 
         *     .doOnSubscribe(ignoredVal -> System.out.println( 
         *         "Subscribed to paged flux processing pages starting from first page")) 
         *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
         *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
         *         error -> System.err.println("An error occurred: " + error), 
         *         () -> System.out.println("Processing complete.")); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedFluxBase.byPage --> 
         * 
         * @return A {@link PagedFluxBase} starting from the first page 
         */ 
        public Flux<P> byPage() 
        /** 
         * Creates a Flux of {@link PagedResponse} starting from the next page associated with the given continuation token. 
         * To start from first page, use {@link #byPage()} instead. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedFluxBase.byPage#String --> 
         * <pre> 
         * // Start processing the results from a page associated with the continuation token 
         * String continuationToken = getContinuationToken(); 
         * pagedFluxBase.byPage(continuationToken) 
         *     .log() 
         *     .doOnSubscribe(ignoredVal -> System.out.println( 
         *         "Subscribed to paged flux processing page starting from " + continuationToken)) 
         *     .subscribe(page -> System.out.printf("Processing page containing item values: %s%n", 
         *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", "))), 
         *         error -> System.err.println("An error occurred: " + error), 
         *         () -> System.out.println("Processing complete.")); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedFluxBase.byPage#String --> 
         * 
         * @param continuationToken The continuation token used to fetch the next page 
         * @return A {@link PagedFluxBase} starting from the page associated with the continuation token 
         */ 
        public Flux<P> byPage(String continuationToken) 
        /** 
         * Subscribe to consume all items of type {@code T} in the sequence respectively. This is recommended for most 
         * common scenarios. This will seamlessly fetch next page when required and provide with a {@link Flux} of items. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.pagedfluxbase.subscribe --> 
         * <pre> 
         * pagedFluxBase.subscribe(new BaseSubscriber<Integer>() { 
         *     @Override 
         *     protected void hookOnSubscribe(Subscription subscription) { 
         *         System.out.println("Subscribed to paged flux processing items"); 
         *         super.hookOnSubscribe(subscription); 
         *     } 
         * 
         *     @Override 
         *     protected void hookOnNext(Integer value) { 
         *         System.out.println("Processing item with value: " + value); 
         *     } 
         * 
         *     @Override 
         *     protected void hookOnComplete() { 
         *         System.out.println("Processing complete."); 
         *     } 
         * }); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.pagedfluxbase.subscribe --> 
         * 
         * @param coreSubscriber The subscriber for this {@link PagedFluxBase} 
         */ 
        @Override public void subscribe(CoreSubscriber<? super T> coreSubscriber) 
    } 
    /** 
     * This class provides utility to iterate over {@link PagedResponse} using {@link Stream} and {@link Iterable} 
     * interfaces. 
     * 
     * <p> 
     * <strong>Code sample using {@link Stream} by page</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.PagedIterable.streamByPage --> 
     * <pre> 
     * // process the streamByPage 
     * pagedIterableResponse.streamByPage().forEach(resp -> { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %d %n", value)); 
     * }); 
     * 
     * </pre> 
     * <!-- end com.azure.core.http.rest.PagedIterable.streamByPage --> 
     * 
     * <p> 
     * <strong>Code sample using {@link Iterable} by page</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.PagedIterable.iterableByPage --> 
     * <pre> 
     * // process the iterableByPage 
     * pagedIterableResponse.iterableByPage().forEach(resp -> { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %d %n", value)); 
     * }); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.PagedIterable.iterableByPage --> 
     * 
     * <p> 
     * <strong>Code sample using {@link Iterable} by page and while loop</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.PagedIterable.iterableByPage.while --> 
     * <pre> 
     * // iterate over each page 
     * for (PagedResponse<Integer> resp : pagedIterableResponse.iterableByPage()) { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %d %n", value)); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.http.rest.PagedIterable.iterableByPage.while --> 
     * 
     * <p> 
     * <strong>Code sample using {@link Iterable} by page and continuation token</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.PagedIterable.pagesWithContinuationToken --> 
     * <pre> 
     * String continuationToken = getContinuationToken(); 
     * pagedIterable 
     *     .iterableByPage(continuationToken) 
     *     .forEach(page -> System.out.printf("Processing page containing item values: %s%n", 
     *         page.getElements().stream().map(String::valueOf).collect(Collectors.joining(", ")))); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.PagedIterable.pagesWithContinuationToken --> 
     * 
     * @param <T> The type of value contained in this {@link IterableStream}. 
     * @see PagedResponse 
     * @see IterableStream 
     */ 
    public class PagedIterable<T> extends PagedIterableBase<T, PagedResponse<T>> { 
        /** 
         * Creates instance given {@link PagedFlux}. 
         * @param pagedFlux to use as iterable 
         */ 
        public PagedIterable(PagedFlux<T> pagedFlux) 
        /** 
         * Creates an instance of {@link PagedIterable} that consists of only a single page. This constructor takes a {@code 
         * Supplier} that return the single page of {@code T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedIterable.singlepage.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<PagedResponse<Integer>> firstPageRetrieverFunction = () -> getFirstPage(); 
         * 
         * PagedIterable<Integer> pagedIterableInstance = new PagedIterable<>(firstPageRetrieverFunction, 
         *     nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedIterable.singlepage.instantiation --> 
         * @param firstPageRetriever Supplier that retrieves the first page. 
         */ 
        public PagedIterable(Supplier<PagedResponse<T>> firstPageRetriever) 
        /** 
         * Creates an instance of {@link PagedIterable} that consists of only a single page with a given element count. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedFlux.singlepage.instantiationWithPageSize --> 
         * <pre> 
         * // A function that fetches the single page of data from a source/service. 
         * Function<Integer, Mono<PagedResponse<Integer>>> singlePageRetriever = pageSize -> 
         *     getFirstPageWithSize(pageSize); 
         * 
         * PagedFlux<Integer> singlePageFluxWithPageSize = new PagedFlux<Integer>(singlePageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedFlux.singlepage.instantiationWithPageSize --> 
         * @param firstPageRetriever Function that retrieves the first page. 
         */ 
        public PagedIterable(Function<Integer, PagedResponse<T>> firstPageRetriever) 
        /** 
         * Creates an instance of {@link PagedIterable}. The constructor takes a {@code Supplier} and {@code Function}. The 
         * {@code Supplier} returns the first page of {@code T}, the {@code Function} retrieves subsequent pages of {@code 
         * T}. 
         * 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedIterable.instantiation --> 
         * <pre> 
         * // A supplier that fetches the first page of data from source/service 
         * Supplier<PagedResponse<Integer>> firstPageRetriever = () -> getFirstPage(); 
         * 
         * // A function that fetches subsequent pages of data from source/service given a continuation token 
         * Function<String, PagedResponse<Integer>> nextPageRetriever = 
         *     continuationToken -> getNextPage(continuationToken); 
         * 
         * PagedIterable<Integer> pagedIterable = new PagedIterable<>(firstPageRetriever, 
         *     nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedIterable.instantiation --> 
         * @param firstPageRetriever Supplier that retrieves the first page 
         * @param nextPageRetriever Function that retrieves the next page given a continuation token 
         */ 
        public PagedIterable(Supplier<PagedResponse<T>> firstPageRetriever, Function<String, PagedResponse<T>> nextPageRetriever) 
        /** 
         * Creates an instance of {@link PagedIterable} that is capable of retrieving multiple pages with of a given page size. 
         * <p><strong>Code sample</strong></p> 
         * <!-- src_embed com.azure.core.http.rest.PagedIterable.instantiationWithPageSize --> 
         * <pre> 
         * // A function that fetches the first page of data from a source/service. 
         * Function<Integer, PagedResponse<Integer>> firstPageRetriever = pageSize -> getPage(pageSize); 
         * 
         * // A function that fetches subsequent pages of data from a source/service given a continuation token. 
         * BiFunction<String, Integer, PagedResponse<Integer>> nextPageRetriever = (continuationToken, pageSize) -> 
         *     getPage(continuationToken, pageSize); 
         * 
         * PagedIterable<Integer> pagedIterableWithPageSize = new PagedIterable<>(firstPageRetriever, nextPageRetriever); 
         * </pre> 
         * <!-- end com.azure.core.http.rest.PagedIterable.instantiationWithPageSize --> 
         * @param firstPageRetriever Function that retrieves the first page. 
         * @param nextPageRetriever BiFunction that retrieves the next page given a continuation token and page size. 
         */ 
        public PagedIterable(Function<Integer, PagedResponse<T>> firstPageRetriever, BiFunction<String, Integer, PagedResponse<T>> nextPageRetriever) 
        /** 
         * Maps this PagedIterable instance of T to a PagedIterable instance of type S as per the provided mapper function. 
         * 
         * @param mapper The mapper function to convert from type T to type S. 
         * @param <S> The mapped type. 
         * @return A PagedIterable of type S. 
         */ 
        public <S> PagedIterable<S> mapPage(Function<T, S> mapper) 
    } 
    /** 
     * This class provides utility to iterate over responses that extend {@link PagedResponse} using {@link Stream} and 
     * {@link Iterable} interfaces. 
     * 
     * <p> 
     * <strong>Code sample using {@link Stream} by page</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.pagedIterableBase.streamByPage --> 
     * <pre> 
     * // process the streamByPage 
     * CustomPagedFlux<String> customPagedFlux = createCustomInstance(); 
     * PagedIterableBase<String, PagedResponse<String>> customPagedIterableResponse = 
     *     new PagedIterableBase<>(customPagedFlux); 
     * customPagedIterableResponse.streamByPage().forEach(resp -> { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %s %n", value)); 
     * }); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedIterableBase.streamByPage --> 
     * 
     * <p> 
     * <strong>Code sample using {@link Iterable} by page</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.pagedIterableBase.iterableByPage --> 
     * <pre> 
     * // process the iterableByPage 
     * customPagedIterableResponse.iterableByPage().forEach(resp -> { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %s %n", value)); 
     * }); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedIterableBase.iterableByPage --> 
     * 
     * <p> 
     * <strong>Code sample using {@link Iterable} by page and while loop</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.http.rest.pagedIterableBase.iterableByPage.while --> 
     * <pre> 
     * // iterate over each page 
     * for (PagedResponse<String> resp : customPagedIterableResponse.iterableByPage()) { 
     *     System.out.printf("Response headers are %s. Url %s  and status code %d %n", resp.getHeaders(), 
     *         resp.getRequest().getUrl(), resp.getStatusCode()); 
     *     resp.getElements().forEach(value -> System.out.printf("Response value is %s %n", value)); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.http.rest.pagedIterableBase.iterableByPage.while --> 
     * 
     * @param <T> The type of value contained in this {@link IterableStream}. 
     * @param <P> The response extending from {@link PagedResponse} 
     * @see PagedResponse 
     * @see IterableStream 
     */ 
    public class PagedIterableBase<T, P extends PagedResponse<T>> extends ContinuablePagedIterable<String, T, P> { 
        /** 
         * Creates instance given {@link PagedFluxBase}. 
         * 
         * @param pagedFluxBase to use as iterable 
         */ 
        public PagedIterableBase(PagedFluxBase<T, P> pagedFluxBase) 
        /** 
         * Creates instance given the {@link PageRetrieverSync page retriever} {@link Supplier}. 
         * 
         * @param provider The page retriever {@link Supplier}. 
         */ 
        public PagedIterableBase(Supplier<PageRetrieverSync<String, P>> provider) 
    } 
    /** 
     * Response of a REST API that returns page. 
     * 
     * @see Page 
     * @see Response 
     * 
     * @param <T> The type of items in the page. 
     */ 
    public interface PagedResponse<T> extends Page<T> , Response<List<T>> , Closeable { 
        /** 
         * Returns the items in the page. 
         * 
         * @return The items in the page. 
         */ 
        default List<T> getValue() 
    } 
    /** 
     * Represents an HTTP response that contains a list of items deserialized into a {@link Page}. 
     * 
     * @param <H> The HTTP response headers 
     * @param <T> The type of items contained in the {@link Page} 
     * @see com.azure.core.http.rest.PagedResponse 
     */ 
    public class PagedResponseBase<H, T> implements PagedResponse<T> { 
        /** 
         * Creates a new instance of the PagedResponseBase type. 
         * 
         * @param request The HttpRequest that was sent to the service whose response resulted in this response. 
         * @param statusCode The status code from the response. 
         * @param headers The headers from the response. 
         * @param page The page of content returned from the service within the response. 
         * @param deserializedHeaders The headers, deserialized into an instance of type H. 
         */ 
        public PagedResponseBase(HttpRequest request, int statusCode, HttpHeaders headers, Page<T> page, H deserializedHeaders) 
        /** 
         * Creates a new instance of the PagedResponseBase type. 
         * 
         * @param request The HttpRequest that was sent to the service whose response resulted in this response. 
         * @param statusCode The status code from the response. 
         * @param headers The headers from the response. 
         * @param items The items returned from the service within the response. 
         * @param continuationToken The continuation token returned from the service, to enable future requests to pick up 
         *      from the same place in the paged iteration. 
         * @param deserializedHeaders The headers, deserialized into an instance of type H. 
         */ 
        public PagedResponseBase(HttpRequest request, int statusCode, HttpHeaders headers, List<T> items, String continuationToken, H deserializedHeaders) 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public void close() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public String getContinuationToken() 
        /** 
         * Get the headers from the HTTP response, transformed into the header type H. 
         * 
         * @return an instance of header type H, containing the HTTP response headers. 
         */ 
        public H getDeserializedHeaders() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public IterableStream<T> getElements() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public HttpHeaders getHeaders() 
        /** 
         * @return the request which resulted in this PagedRequestResponse. 
         */ 
        @Override public HttpRequest getRequest() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public int getStatusCode() 
    } 
    /** 
     * This class contains the options to customize an HTTP request. {@link RequestOptions} can be used to configure the 
     * request headers, query params, the request body, or add a callback to modify all aspects of the HTTP request. 
     * 
     * <p> 
     * An instance of fully configured {@link RequestOptions} can be passed to a service method that preconfigures known 
     * components of the request like URL, path params etc, further modifying both un-configured, or preconfigured 
     * components. 
     * </p> 
     * 
     * <p> 
     * To demonstrate how this class can be used to construct a request, let's use a Pet Store service as an example. The 
     * list of APIs available on this service are <a href="https://petstore.swagger.io/#/pet">documented in the swagger 
     * definition.</a> 
     * </p> 
     * 
     * <p> 
     * <strong>Creating an instance of RequestOptions</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.http.rest.requestoptions.instantiation --> 
     * <pre> 
     * RequestOptions options = new RequestOptions() 
     *     .setBody(BinaryData.fromString("{\"name\":\"Fluffy\"}")) 
     *     .addHeader("x-ms-pet-version", "2021-06-01"); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.requestoptions.instantiation --> 
     * 
     * <p> 
     * <strong>Configuring the request with JSON body and making a HTTP POST request</strong> 
     * </p> 
     * To <a href="https://petstore.swagger.io/#/pet/addPet">add a new pet to the pet store</a>, an HTTP POST call should be 
     * made to the service with the details of the pet that is to be added. The details of the pet are included as the 
     * request body in JSON format. 
     * 
     * The JSON structure for the request is defined as follows: 
     * <pre>{@code 
     * { 
     *   "id": 0, 
     *   "category": { 
     *     "id": 0, 
     *     "name": "string" 
     * }, 
     * "name": "doggie", 
     * "photoUrls": [ 
     * "string" 
     * ], 
     * "tags": [ 
     * { 
     * "id": 0, 
     * "name": "string" 
     * } 
     * ], 
     * "status": "available" 
     * } 
     * }</pre> 
     * 
     * To create a concrete request, Json builder provided in javax package is used here for demonstration. However, any 
     * other Json building library can be used to achieve similar results. 
     * 
     * <!-- src_embed com.azure.core.http.rest.requestoptions.createjsonrequest --> 
     * <pre> 
     * JsonArray photoUrls = new JsonArray() 
     *     .addElement(new JsonString("https://imgur.com/pet1")) 
     *     .addElement(new JsonString("https://imgur.com/pet2")); 
     * 
     * JsonArray tags = new JsonArray() 
     *     .addElement(new JsonObject() 
     *         .setProperty("id", new JsonNumber(0)) 
     *         .setProperty("name", new JsonString("Labrador"))) 
     *     .addElement(new JsonObject() 
     *         .setProperty("id", new JsonNumber(1)) 
     *         .setProperty("name", new JsonString("2021"))); 
     * 
     * JsonObject requestBody = new JsonObject() 
     *     .setProperty("id", new JsonNumber(0)) 
     *     .setProperty("name", new JsonString("foo")) 
     *     .setProperty("status", new JsonString("available")) 
     *     .setProperty("category", new JsonObject() 
     *         .setProperty("id", new JsonNumber(0)) 
     *         .setProperty("name", new JsonString("dog"))) 
     *     .setProperty("photoUrls", photoUrls) 
     *     .setProperty("tags", tags); 
     * 
     * BinaryData requestBodyData = BinaryData.fromObject(requestBody); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.requestoptions.createjsonrequest --> 
     * 
     * Now, this string representation of the JSON request can be set as body of RequestOptions 
     * 
     * <!-- src_embed com.azure.core.http.rest.requestoptions.postrequest --> 
     * <pre> 
     * RequestOptions options = new RequestOptions() 
     *     .addRequestCallback(request -> request 
     *         // may already be set if request is created from a client 
     *         .setUrl("https://petstore.example.com/pet") 
     *         .setHttpMethod(HttpMethod.POST) 
     *         .setBody(requestBodyData) 
     *         .setHeader(HttpHeaderName.CONTENT_TYPE, "application/json")); 
     * </pre> 
     * <!-- end com.azure.core.http.rest.requestoptions.postrequest --> 
     */ 
    public final class RequestOptions { 
        /** 
         * Creates a new instance of {@link RequestOptions}. 
         */ 
        public RequestOptions() 
        /** 
         * Adds a header to the HTTP request. 
         * <p> 
         * If a header with the given name exists the {@code value} is added to the existing header (comma-separated), 
         * otherwise a new header is created. 
         * 
         * @param header the header key 
         * @param value the header value 
         * @return the modified RequestOptions object 
         * @deprecated Use {@link #addHeader(HttpHeaderName, String)} as it provides better performance. 
         */ 
        @Deprecated public RequestOptions addHeader(String header, String value) 
        /** 
         * Adds a header to the HTTP request. 
         * <p> 
         * If a header with the given name exists the {@code value} is added to the existing header (comma-separated), 
         * otherwise a new header is created. 
         * 
         * @param header the header key 
         * @param value the header value 
         * @return the modified RequestOptions object 
         */ 
        public RequestOptions addHeader(HttpHeaderName header, String value) 
        /** 
         * Adds a query parameter to the request URL. The parameter name and value will be URL encoded. To use an already 
         * encoded parameter name and value, call {@code addQueryParam("name", "value", true)}. 
         * 
         * @param parameterName the name of the query parameter 
         * @param value the value of the query parameter 
         * @return the modified RequestOptions object 
         */ 
        public RequestOptions addQueryParam(String parameterName, String value) 
        /** 
         * Adds a query parameter to the request URL, specifying whether the parameter is already encoded. A value true for 
         * this argument indicates that value of {@link QueryParam#value()} is already encoded hence engine should not 
         * encode it, by default value will be encoded. 
         * 
         * @param parameterName the name of the query parameter 
         * @param value the value of the query parameter 
         * @param encoded whether this query parameter is already encoded 
         * @return the modified RequestOptions object 
         */ 
        public RequestOptions addQueryParam(String parameterName, String value, boolean encoded) 
        /** 
         * Adds a custom request callback to modify the HTTP request before it's sent by the HttpClient. The modifications 
         * made on a RequestOptions object is applied in order on the request. 
         * 
         * @param requestCallback the request callback 
         * @return the modified RequestOptions object 
         * @throws NullPointerException If {@code requestCallback} is null. 
         */ 
        public RequestOptions addRequestCallback(Consumer<HttpRequest> requestCallback) 
        /** 
         * Sets the body to send as part of the HTTP request. 
         * 
         * @param requestBody the request body data 
         * @return the modified RequestOptions object 
         * @throws NullPointerException If {@code requestBody} is null. 
         */ 
        public RequestOptions setBody(BinaryData requestBody) 
        /** 
         * Gets the additional context on the request that is passed during the service call. 
         * 
         * @return The additional context that is passed during the service call. 
         */ 
        public Context getContext() 
        /** 
         * Sets the additional context on the request that is passed during the service call. 
         * 
         * @param context Additional context that is passed during the service call. 
         * @return the modified RequestOptions object 
         */ 
        public RequestOptions setContext(Context context) 
        /** 
         * Sets a header on the HTTP request. 
         * <p> 
         * If a header with the given name exists it is overridden by the new {@code value}. 
         * 
         * @param header the header key 
         * @param value the header value 
         * @return the modified RequestOptions object 
         * @deprecated Use {@link #setHeader(HttpHeaderName, String)} as it provides better performance. 
         */ 
        @Deprecated public RequestOptions setHeader(String header, String value) 
        /** 
         * Sets a header on the HTTP request. 
         * <p> 
         * If a header with the given name exists it is overridden by the new {@code value}. 
         * 
         * @param header the header key 
         * @param value the header value 
         * @return the modified RequestOptions object 
         */ 
        public RequestOptions setHeader(HttpHeaderName header, String value) 
    } 
    /** 
     * REST response with a strongly-typed content specified. 
     * 
     * @param <T> The deserialized type of the response content, available from {@link #getValue()}. 
     * @see ResponseBase 
     */ 
    public interface Response<T> { 
        /** 
         * Gets the headers from the HTTP response. 
         * 
         * @return The HTTP response headers. 
         */ 
        HttpHeaders getHeaders() 
        /** 
         * Gets the HTTP request which resulted in this response. 
         * 
         * @return The HTTP request. 
         */ 
        HttpRequest getRequest() 
        /** 
         * Gets the HTTP response status code. 
         * 
         * @return The status code of the HTTP response. 
         */ 
        int getStatusCode() 
        /** 
         * Gets the deserialized value of the HTTP response. 
         * 
         * @return The deserialized value of the HTTP response. 
         */ 
        T getValue() 
    } 
    /** 
     * The response of a REST request. 
     * 
     * @param <H> The deserialized type of the response headers. 
     * @param <T> The deserialized type of the response value, available from {@link Response#getValue()}. 
     */ 
    public class ResponseBase<H, T> implements Response<T> { 
        /** 
         * Creates a {@link ResponseBase}. 
         * 
         * @param request The HTTP request which resulted in this response. 
         * @param statusCode The status code of the HTTP response. 
         * @param headers The headers of the HTTP response. 
         * @param deserializedHeaders The deserialized headers of the HTTP response. 
         * @param value The deserialized value of the HTTP response. 
         */ 
        public ResponseBase(HttpRequest request, int statusCode, HttpHeaders headers, T value, H deserializedHeaders) 
        /** 
         * Get the headers from the HTTP response, transformed into the header type, {@code H}. 
         * 
         * @return An instance of header type {@code H}, deserialized from the HTTP response headers. 
         */ 
        public H getDeserializedHeaders() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public HttpHeaders getHeaders() 
        /** 
         * Gets The request which resulted in this {@link ResponseBase}. 
         * 
         * @return The request which resulted in this {@link ResponseBase}. 
         */ 
        @Override public HttpRequest getRequest() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public int getStatusCode() 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public T getValue() 
    } 
    /** 
     * <p>RestProxy is a type that creates a proxy implementation for an interface describing REST API methods. 
     * It can create proxy implementations for interfaces with methods that return deserialized Java objects as well 
     * as asynchronous Single objects that resolve to a deserialized Java object.</p> 
     * 
     * <p>RestProxy uses the provided HttpPipeline and SerializerAdapter to send HTTP requests and convert response bodies 
     * to POJOs.</p> 
     * 
     * <p>It also provides methods to send the provided request asynchronously, applying any request policies provided to 
     * the HttpClient instance.</p> 
     * 
     * <p>RestProxy is useful when you want to create a proxy implementation for an interface describing REST API methods.</p> 
     */ 
    public final class RestProxy implements InvocationHandler { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Create a proxy implementation of the provided Swagger interface. 
         * 
         * @param swaggerInterface the Swagger interface to provide a proxy implementation for 
         * @param <A> the type of the Swagger interface 
         * @return a proxy implementation of the provided Swagger interface 
         */ 
        public static <A> A create(Class<A> swaggerInterface) 
        /** 
         * Create a proxy implementation of the provided Swagger interface. 
         * 
         * @param swaggerInterface the Swagger interface to provide a proxy implementation for 
         * @param httpPipeline the HttpPipelinePolicy and HttpClient pipeline that will be used to send Http requests 
         * @param <A> the type of the Swagger interface 
         * @return a proxy implementation of the provided Swagger interface 
         */ 
        public static <A> A create(Class<A> swaggerInterface, HttpPipeline httpPipeline) 
        /** 
         * Create a proxy implementation of the provided Swagger interface. 
         * 
         * @param swaggerInterface the Swagger interface to provide a proxy implementation for 
         * @param httpPipeline the HttpPipelinePolicy and HttpClient pipline that will be used to send Http requests 
         * @param serializer the serializer that will be used to convert POJOs to and from request and response bodies 
         * @param <A> the type of the Swagger interface. 
         * @return a proxy implementation of the provided Swagger interface 
         */ 
        public static <A> A create(Class<A> swaggerInterface, HttpPipeline httpPipeline, SerializerAdapter serializer) 
        @Override public Object invoke(Object proxy, Method method, Object[] args) 
        /** 
         * Send the provided request asynchronously, applying any request policies provided to the HttpClient instance. 
         * 
         * @param request the HTTP request to send 
         * @param contextData the context 
         * @return a {@link Mono} that emits HttpResponse asynchronously 
         */ 
        public Mono<HttpResponse> send(HttpRequest request, Context contextData) 
    } 
    /** 
     * This class represents a simple HTTP response with a strongly-typed content. 
     * It encapsulates the HTTP request that resulted in the response, the status code of the HTTP response, 
     * the headers of the HTTP response, and the deserialized value of the HTTP response. 
     * 
     * <p>This class is useful when you want to work with the response of an HTTP request where the body of the response 
     * is expected to be in a specific format (the generic type {@code T}).</p> 
     * 
     * @param <T> The type of the deserialized response content. 
     */ 
    public class SimpleResponse<T> implements Response<T> { 
        /** 
         * Creates a {@link SimpleResponse} from a response and a value. 
         * 
         * @param response The response that needs to be mapped. 
         * @param value The value to put into the new response. 
         */ 
        public SimpleResponse(Response<?> response, T value) 
        /** 
         * Creates a {@link SimpleResponse}. 
         * 
         * @param request The request which resulted in this response. 
         * @param statusCode The status code of the HTTP response. 
         * @param headers The headers of the HTTP response. 
         * @param value The deserialized value of the HTTP response. 
         */ 
        public SimpleResponse(HttpRequest request, int statusCode, HttpHeaders headers, T value) 
        /** 
         * {@inheritDoc} 
         */ 
        @Override public HttpHeaders getHeaders() 
        /** 
         * Gets the request which resulted in this {@link SimpleResponse}. 
         * 
         * @return The request which resulted in this {@link SimpleResponse}. 
         */ 
        @Override public HttpRequest getRequest() 
        /** 
         * Gets the status code of the HTTP response. 
         * 
         * @return The status code of the HTTP response. 
         */ 
        @Override public int getStatusCode() 
        /** 
         * Gets the deserialized value of the HTTP response. 
         * 
         * @return The deserialized value of the HTTP response. 
         */ 
        @Override public T getValue() 
    } 
    /** 
     * <p>This class represents a REST response with a streaming content. 
     * It encapsulates the HTTP request that resulted in the response, the status code of the HTTP response, 
     * the headers of the HTTP response, and the content of the HTTP response as a stream of 
     * {@link ByteBuffer byte buffers}.</p> 
     * 
     * <p>It also provides methods to write the content of the HTTP response to a {@link AsynchronousByteChannel} or a 
     * {@link WritableByteChannel}, and to dispose the connection associated with the response.</p> 
     */ 
    public final class StreamResponse extends SimpleResponse<Flux<ByteBuffer>> implements Closeable { 
        /** 
         * Creates a {@link StreamResponse}. 
         * 
         * @param response The HTTP response. 
         */ 
        public StreamResponse(HttpResponse response) 
        /** 
         * Creates a {@link StreamResponse}. 
         * 
         * @param request The request which resulted in this response. 
         * @param statusCode The status code of the HTTP response. 
         * @param headers The headers of the HTTP response. 
         * @param value The content of the HTTP response. 
         * @deprecated Use {@link #StreamResponse(HttpResponse)} 
         */ 
        @Deprecated public StreamResponse(HttpRequest request, int statusCode, HttpHeaders headers, Flux<ByteBuffer> value) 
        /** 
         * Disposes the connection associated with this {@link StreamResponse}. 
         */ 
        @Override public void close() 
        /** 
         * The content of the HTTP response as a stream of {@link ByteBuffer byte buffers}. 
         * 
         * @return The content of the HTTP response as a stream of {@link ByteBuffer byte buffers}. 
         */ 
        @Override public Flux<ByteBuffer> getValue() 
        /** 
         * Transfers content bytes to the {@link WritableByteChannel}. 
         * @param channel The destination {@link WritableByteChannel}. 
         * @throws UncheckedIOException When I/O operation fails. 
         */ 
        public void writeValueTo(WritableByteChannel channel) 
        /** 
         * Transfers content bytes to the {@link AsynchronousByteChannel}. 
         * @param channel The destination {@link AsynchronousByteChannel}. 
         * @return A {@link Mono} that completes when transfer is completed. 
         */ 
        public Mono<Void> writeValueToAsync(AsynchronousByteChannel channel) 
    } 
} 
/** 
 * This package contains the core model classes used across the Azure SDK. 
 * 
 * <p>These classes provide common structures and functionality for working with Azure services. They include 
 * representations for various types of data, such GeoJSON objects, and JSON Patch documents.</p> 
 * 
 * <p>Classes in this package are typically used as base classes or utility classes, and are extended or used by other 
 * classes in the Azure SDK to provide service-specific functionality.</p> 
 * 
 * <p>Some of the key classes in this package include:</p> 
 * <ul> 
 *     <li>{@link com.azure.core.models.GeoObject}: Represents an abstract geometric object in GeoJSON format.</li> 
 *     <li>{@link com.azure.core.models.GeoPolygonCollection}: Represents a collection of 
 *     {@link com.azure.core.models.GeoPolygon GeoPolygons} in GeoJSON format.</li> 
 *     <li>{@link com.azure.core.models.JsonPatchDocument}: Represents a JSON Patch document.</li> 
 *     <li>{@link com.azure.core.models.ResponseError}: Represents the error details of an HTTP response.</li> 
 *     <li>{@link com.azure.core.models.ResponseInnerError}: Represents the inner error details of a 
 *     {@link com.azure.core.models.ResponseError}.</li> 
 *     <li>{@link com.azure.core.models.MessageContent}: Represents a message with a specific content type and data.</li> 
 * </ul> 
 */ 
package com.azure.core.models { 
    /** 
     * An expandable enum that describes Azure cloud environment. 
     */ 
    public final class AzureCloud extends ExpandableStringEnum<AzureCloud> { 
        /** 
         * Azure public cloud. 
         */ 
        public static final AzureCloud AZURE_PUBLIC_CLOUD = fromString("AZURE_PUBLIC_CLOUD"); 
        /** 
         * Azure China cloud. 
         */ 
        public static final AzureCloud AZURE_CHINA_CLOUD = fromString("AZURE_CHINA_CLOUD"); 
        /** 
         * Azure US government cloud. 
         */ 
        public static final AzureCloud AZURE_US_GOVERNMENT_CLOUD = fromString("AZURE_US_GOVERNMENT"); 
        /** 
         * Creates a new instance of {@link AzureCloud} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link AzureCloud} which doesn't have a 
         * String enum value. 
         * 
         * @deprecated Use one of the constants or the {@link #fromString(String)} factory method. 
         */ 
        @Deprecated public AzureCloud() 
        /** 
         * Creates or finds an AzureCloud from its string representation. 
         * 
         * @param cloudName cloud name to look for 
         * @return the corresponding AzureCloud 
         */ 
        public static AzureCloud fromString(String cloudName) 
    } 
    @Fluent
    /** 
     * Represents the CloudEvent conforming to the 1.0 schema defined by the 
     * <a href="https://github.com/cloudevents/spec/blob/v1.0.1/spec.md">Cloud Native Computing Foundation</a>. 
     * 
     * <p> 
     * CloudEvents is a specification for describing event data in common formats to provide interoperability across 
     * services, platforms and systems. 
     * </p> 
     * 
     * <p> 
     * Some Azure services, for instance, EventGrid, are compatible with this specification. You can use this class to 
     * communicate with these Azure services. 
     * </p> 
     * <p> 
     * Depending on your scenario, you can either use the constructor 
     * {@link #CloudEvent(String, String, BinaryData, CloudEventDataFormat, String)} to create a CloudEvent, or use the 
     * factory method {@link #fromString(String)} to deserialize CloudEvent instances from a Json String representation of 
     * CloudEvents. 
     * </p> 
     * 
     * <p> 
     * If you have the data payload of a CloudEvent and want to send it out, use the constructor 
     * {@link #CloudEvent(String, String, BinaryData, CloudEventDataFormat, String)} to create it. Then you can serialize 
     * the CloudEvent into its Json String representation and send it. 
     * </p> 
     * 
     * <p> 
     * <strong>Create CloudEvent Samples</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.model.CloudEvent#constructor --> 
     * <pre> 
     * // Use BinaryData.fromBytes() to create data in format CloudEventDataFormat.BYTES 
     * byte[] exampleBytes = "Hello World".getBytes(StandardCharsets.UTF_8); 
     * CloudEvent cloudEvent = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromBytes(exampleBytes), CloudEventDataFormat.BYTES, "application/octet-stream"); 
     * 
     * // Use BinaryData.fromObject() to create CloudEvent data in format CloudEventDataFormat.JSON 
     * // From a model class 
     * User user = new User("Stephen", "James"); 
     * CloudEvent cloudEventDataObject = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromObject(user), CloudEventDataFormat.JSON, "application/json"); 
     * 
     * // From a String 
     * CloudEvent cloudEventDataStr = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromObject("Hello World"), CloudEventDataFormat.JSON, "text/plain"); 
     * 
     * // From an Integer 
     * CloudEvent cloudEventDataInt = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromObject(1), CloudEventDataFormat.JSON, "int"); 
     * 
     * // From a Boolean 
     * CloudEvent cloudEventDataBool = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromObject(true), CloudEventDataFormat.JSON, "bool"); 
     * 
     * // From null 
     * CloudEvent cloudEventDataNull = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromObject(null), CloudEventDataFormat.JSON, "null"); 
     * 
     * // Use BinaryData.fromString() if you have a Json String for the CloudEvent data. 
     * String jsonStringForData = "\"Hello World\"";  // A json String. 
     * CloudEvent cloudEventDataJsonStr = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
     *     BinaryData.fromString(jsonStringForData), CloudEventDataFormat.JSON, "text/plain"); 
     * </pre> 
     * <!-- end com.azure.core.model.CloudEvent#constructor --> 
     * 
     * <p> 
     * On the contrary, if you receive CloudEvents and have the Json string representation of one or more of 
     * CloudEvents, use {@link #fromString(String)} to deserialize them from the Json string. 
     * </p> 
     * 
     * <p> 
     * <strong>Deserialize CloudEvent Samples</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.model.CloudEvent.fromString --> 
     * <pre> 
     * List<CloudEvent> cloudEventList = CloudEvent.fromString(cloudEventJsonString); 
     * CloudEvent cloudEvent = cloudEventList.get(0); 
     * BinaryData cloudEventData = cloudEvent.getData(); 
     * 
     * byte[] bytesValue = cloudEventData.toBytes();  // If data payload is in bytes (data_base64 is not null). 
     * User objectValue = cloudEventData.toObject(User.class);  // If data payload is a User object. 
     * int intValue = cloudEventData.toObject(Integer.class);  // If data payload is an int. 
     * boolean boolValue = cloudEventData.toObject(Boolean.class);  // If data payload is boolean. 
     * String stringValue = cloudEventData.toObject(String.class);  // If data payload is String. 
     * String jsonStringValue = cloudEventData.toString();  // The data payload represented in Json String. 
     * </pre> 
     * <!-- end com.azure.core.model.CloudEvent.fromString --> 
     */ 
    public final class CloudEvent implements JsonSerializable<CloudEvent> { 
        /** 
         * Create an instance of {@link CloudEvent}. 
         * 
         * <p>{@code source}, {@code type}, {@code id}, and {@code specversion} are required attributes according to the 
         * <a href="https://github.com/cloudevents/spec/blob/v1.0.1/spec.md">CNCF CloudEvent spec</a>. 
         * You must set the {@code source} and {@code type} when using this constructor. For convenience, {@code id} and 
         * {@code specversion} are automatically assigned. You can change the {@code id} by using {@link #setId(String)} 
         * after you create a CloudEvent. But you can not change {@code specversion} because this class is specifically for 
         * CloudEvent 1.0 schema.</p> 
         * 
         * <p>For the CloudEvent data payload, this constructor accepts {@code data} of {@link BinaryData} as the 
         * CloudEvent payload. The {@code data} can be created from objects of type String, bytes, boolean, null, array or 
         * other types. A CloudEvent will be serialized to its Json String representation to be sent out. Use param 
         * {@code format} to indicate whether the {@code data} will be serialized as bytes, or Json. When 
         * {@link CloudEventDataFormat#BYTES} is used, the data payload will be serialized to base64 bytes and stored in 
         * attribute <em>data_base64</em> of the CloudEvent's Json representation. When {@link CloudEventDataFormat#JSON} is 
         * used, the data payload will be serialized as Json data and stored in attribute <em>data</em> of the CloudEvent's 
         * Json representation.</p> 
         * 
         * <p><strong>Create CloudEvent Samples</strong></p> 
         * <!-- src_embed com.azure.core.model.CloudEvent#constructor --> 
         * <pre> 
         * // Use BinaryData.fromBytes() to create data in format CloudEventDataFormat.BYTES 
         * byte[] exampleBytes = "Hello World".getBytes(StandardCharsets.UTF_8); 
         * CloudEvent cloudEvent = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromBytes(exampleBytes), CloudEventDataFormat.BYTES, "application/octet-stream"); 
         * 
         * // Use BinaryData.fromObject() to create CloudEvent data in format CloudEventDataFormat.JSON 
         * // From a model class 
         * User user = new User("Stephen", "James"); 
         * CloudEvent cloudEventDataObject = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromObject(user), CloudEventDataFormat.JSON, "application/json"); 
         * 
         * // From a String 
         * CloudEvent cloudEventDataStr = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromObject("Hello World"), CloudEventDataFormat.JSON, "text/plain"); 
         * 
         * // From an Integer 
         * CloudEvent cloudEventDataInt = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromObject(1), CloudEventDataFormat.JSON, "int"); 
         * 
         * // From a Boolean 
         * CloudEvent cloudEventDataBool = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromObject(true), CloudEventDataFormat.JSON, "bool"); 
         * 
         * // From null 
         * CloudEvent cloudEventDataNull = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromObject(null), CloudEventDataFormat.JSON, "null"); 
         * 
         * // Use BinaryData.fromString() if you have a Json String for the CloudEvent data. 
         * String jsonStringForData = "\"Hello World\"";  // A json String. 
         * CloudEvent cloudEventDataJsonStr = new CloudEvent("/cloudevents/example/source", "Example.EventType", 
         *     BinaryData.fromString(jsonStringForData), CloudEventDataFormat.JSON, "text/plain"); 
         * </pre> 
         * <!-- end com.azure.core.model.CloudEvent#constructor --> 
         * 
         * @param source Identifies the context in which an event happened. The combination of id and source must be unique 
         * for each distinct event. 
         * @param type Type of event related to the originating occurrence. 
         * @param data A {@link BinaryData} that wraps the original data, which can be a String, byte[], or model class. 
         * @param format Set to {@link CloudEventDataFormat#BYTES} to serialize the data to base64 format, or 
         * {@link CloudEventDataFormat#JSON} to serialize the data to JSON value. 
         * @param dataContentType The content type of the data. It has no impact on how the data is serialized but tells the 
         * event subscriber how to use the data. Typically, the value is of MIME types such as "application/json", 
         * "text/plain", "text/xml", "avro/binary", etc. It can be null. 
         * @throws NullPointerException If source or type is null or format is null while data isn't null. 
         * @throws IllegalArgumentException if format is {@link CloudEventDataFormat#JSON} but the data isn't in a correct 
         * JSON format. 
         */ 
        public CloudEvent(String source, String type, BinaryData data, CloudEventDataFormat format, String dataContentType) 
        /** 
         * Add/Overwrite a single extension attribute to the cloud event. 
         * 
         * @param name the name of the attribute. It must contain only lower-case alphanumeric characters and not be any 
         * CloudEvent reserved attribute names. 
         * @param value the value to associate with the name. 
         * @return the cloud event itself. 
         * @throws NullPointerException if name or value is null. 
         * @throws IllegalArgumentException if name format isn't correct. 
         */ 
        public CloudEvent addExtensionAttribute(String name, Object value) 
        /** 
         * Get the data associated with this event as a {@link BinaryData}, which has API to deserialize the data into a 
         * String, an Object, or a byte[]. 
         * 
         * @return A {@link BinaryData} that wraps the event's data payload. 
         */ 
        public BinaryData getData() 
        /** 
         * Get the content MIME type that the data is in. 
         * 
         * @return the content type the data is in, or null it is not set. 
         */ 
        public String getDataContentType() 
        /** 
         * Get the schema that the data adheres to. 
         * 
         * @return a URI of the data schema, or null if it is not set. 
         */ 
        public String getDataSchema() 
        /** 
         * Set the schema that the data adheres to. 
         * 
         * @param dataSchema a String identifying the schema of the data. The <a 
         * href="https://github.com/cloudevents/spec/blob/v1.0.1/spec.md#dataschema"> CNCF CloudEvent spec dataschema</a> is 
         * defined as a URI. For compatibility with legacy system, this class accepts any String. But for interoperability, 
         * you should use a URI format string. 
         * @return the cloud event itself. 
         */ 
        public CloudEvent setDataSchema(String dataSchema) 
        /** 
         * Get a map of the additional user-defined attributes associated with this event. 
         * 
         * @return an unmodifiable map of the extension attributes. 
         */ 
        public Map<String, Object> getExtensionAttributes() 
        /** 
         * Reads a JSON stream into a {@link CloudEvent}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link CloudEvent} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IOException If a {@link CloudEvent} fails to be read from the {@code jsonReader}. 
         */ 
        public static CloudEvent fromJson(JsonReader jsonReader) throws IOException
        /** 
         * Deserialize {@link CloudEvent} JSON string representation that has one CloudEvent object or an array of 
         * CloudEvent objects into a list of CloudEvents, and validate whether any CloudEvents have null {@code id}, 
         * {@code source}, or {@code type}. If you want to skip this validation, use {@link #fromString(String, boolean)}. 
         * 
         * <p><strong>Deserialize CloudEvent Samples</strong></p> 
         * <!-- src_embed com.azure.core.model.CloudEvent.fromString --> 
         * <pre> 
         * List<CloudEvent> cloudEventList = CloudEvent.fromString(cloudEventJsonString); 
         * CloudEvent cloudEvent = cloudEventList.get(0); 
         * BinaryData cloudEventData = cloudEvent.getData(); 
         * 
         * byte[] bytesValue = cloudEventData.toBytes();  // If data payload is in bytes (data_base64 is not null). 
         * User objectValue = cloudEventData.toObject(User.class);  // If data payload is a User object. 
         * int intValue = cloudEventData.toObject(Integer.class);  // If data payload is an int. 
         * boolean boolValue = cloudEventData.toObject(Boolean.class);  // If data payload is boolean. 
         * String stringValue = cloudEventData.toObject(String.class);  // If data payload is String. 
         * String jsonStringValue = cloudEventData.toString();  // The data payload represented in Json String. 
         * </pre> 
         * <!-- end com.azure.core.model.CloudEvent.fromString --> 
         * 
         * @param cloudEventsJson the JSON payload containing one or more events. 
         * @return all the events in the payload deserialized as {@link CloudEvent CloudEvents}. 
         * @throws NullPointerException if cloudEventsJson is null. 
         * @throws IllegalArgumentException if the input parameter isn't a correct JSON string for a CloudEvent or an array 
         * of CloudEvents, or any deserialized CloudEvents have null {@code id}, {@code source}, or {@code type}. 
         */ 
        public static List<CloudEvent> fromString(String cloudEventsJson) 
        /** 
         * Deserialize {@link CloudEvent CloudEvents} JSON string representation that has one CloudEvent object or an array 
         * of CloudEvent objects into a list of CloudEvents. 
         * 
         * @param cloudEventsJson the JSON payload containing one or more events. 
         * @param skipValidation set to true if you'd like to skip the validation for the deserialized CloudEvents. A valid 
         * CloudEvent should have 'id', 'source' and 'type' not null. 
         * @return all the events in the payload deserialized as {@link CloudEvent CloudEvents}. 
         * @throws NullPointerException if cloudEventsJson is null. 
         * @throws IllegalArgumentException if the input parameter isn't a JSON string for a CloudEvent or an array of 
         * CloudEvents, or skipValidation is false and any CloudEvents have null id', 'source', or 'type'. 
         */ 
        public static List<CloudEvent> fromString(String cloudEventsJson, boolean skipValidation) 
        /** 
         * Get the id of the cloud event. 
         * 
         * @return the id. 
         */ 
        public String getId() 
        /** 
         * Set a custom id. Note that a random id is already set by default. 
         * 
         * @param id the id to set. 
         * @return the cloud event itself. 
         * @throws NullPointerException if id is null. 
         * @throws IllegalArgumentException if id is empty. 
         */ 
        public CloudEvent setId(String id) 
        /** 
         * Get the source of the event. 
         * 
         * @return the source. 
         */ 
        public String getSource() 
        /** 
         * Get the subject associated with this event. 
         * 
         * @return the subject, or null if it is not set. 
         */ 
        public String getSubject() 
        /** 
         * Set the subject of the event. 
         * 
         * @param subject the subject to set. 
         * @return the cloud event itself. 
         */ 
        public CloudEvent setSubject(String subject) 
        /** 
         * Get the time associated with the occurrence of the event. 
         * 
         * @return the event time, or null if the time is not set. 
         */ 
        public OffsetDateTime getTime() 
        /** 
         * Set the time associated with the occurrence of the event. 
         * <p> 
         * At creation, the time is set to the current UTC time. It can be unset by setting it to null. 
         * 
         * @param time the time to set. 
         * @return the cloud event itself. 
         */ 
        public CloudEvent setTime(OffsetDateTime time) 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        /** 
         * Get the type of event, e.g. "Contoso.Items.ItemReceived". 
         * 
         * @return the type of the event. 
         */ 
        public String getType() 
    } 
    /** 
     * Representation of the data format for a {@link CloudEvent}. 
     * <p> 
     * When constructing a {@link CloudEvent} this is passed to determine the serialized format of the event's data. 
     * If {@link #BYTES} is used the data will be stored as a Base64 encoded string, 
     * otherwise it will be stored as a JSON serialized object. 
     * 
     * @see CloudEvent#CloudEvent(String, String, BinaryData, com.azure.core.models.CloudEventDataFormat, String) 
     */ 
    public final class CloudEventDataFormat extends ExpandableStringEnum<CloudEventDataFormat> { 
        /** 
         * Bytes format. 
         */ 
        public static final CloudEventDataFormat BYTES = fromString("BYTES", CloudEventDataFormat.class); 
        /** 
         * JSON format. 
         */ 
        public static final CloudEventDataFormat JSON = fromString("JSON", CloudEventDataFormat.class); 
        /** 
         * Creates a new instance of {@link CloudEventDataFormat} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link CloudEventDataFormat} which doesn't 
         * have a String enum value. 
         * 
         * @deprecated Use one of the constants or the {@link #fromString(String)} factory method. 
         */ 
        @Deprecated public CloudEventDataFormat() 
        /** 
         * Creates or gets a CloudEventDataFormat from its string representation. 
         * 
         * @param name Name of the CloudEventDataFormat. 
         * @return The corresponding CloudEventDataFormat. 
         */ 
        public static CloudEventDataFormat fromString(String name) 
    } 
    @Immutable
    /** 
     * Represents a geometric bounding box. 
     * 
     * <p>This class encapsulates a bounding box defined by west, south, east, and north coordinates, and optionally 
     * minimum and maximum altitude. It provides methods to access these properties.</p> 
     * 
     * <p>This class is useful when you want to work with a bounding box in a geographic context. For example, you can use 
     * it to define the area of interest for a map view, or to specify the spatial extent of a geographic dataset.</p> 
     * 
     * @see JsonSerializable 
     */ 
    public final class GeoBoundingBox implements JsonSerializable<GeoBoundingBox> { 
        /** 
         * Constructs a bounding box. 
         * 
         * @param west West longitudinal boundary. 
         * @param south South latitudinal boundary. 
         * @param east East longitudinal boundary. 
         * @param north North latitudinal boundary. 
         */ 
        public GeoBoundingBox(double west, double south, double east, double north) 
        /** 
         * Constructs a bounding box. 
         * 
         * @param west West longitudinal boundary. 
         * @param south South latitudinal boundary. 
         * @param east East longitudinal boundary. 
         * @param north North latitudinal boundary. 
         * @param minAltitude Minimum altitude boundary. 
         * @param maxAltitude Maximum altitude boundary. 
         */ 
        public GeoBoundingBox(double west, double south, double east, double north, double minAltitude, double maxAltitude) 
        /** 
         * The east longitudinal boundary of the bounding box. 
         * 
         * @return The east longitudinal boundary. 
         */ 
        public double getEast() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoBoundingBox}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoBoundingBox} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@link GeoBoundingBox} doesn't have four or six positions in the array. 
         * @throws IOException If a {@link GeoBoundingBox} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoBoundingBox fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * The maximum altitude boundary of the bounding box. 
         * 
         * @return The maximum altitude boundary. 
         */ 
        public Double getMaxAltitude() 
        /** 
         * The minimum altitude boundary of the bounding box. 
         * 
         * @return The minimum altitude boundary. 
         */ 
        public Double getMinAltitude() 
        /** 
         * The north latitudinal boundary of the bounding box. 
         * 
         * @return The north latitudinal boundary. 
         */ 
        public double getNorth() 
        /** 
         * The south latitudinal boundary of the bounding box. 
         * 
         * @return The south latitudinal boundary. 
         */ 
        public double getSouth() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public String toString() 
        /** 
         * The west longitudinal boundary of the bounding box. 
         * 
         * @return The west longitudinal boundary. 
         */ 
        public double getWest() 
    } 
    @Immutable
    /** 
     * <p>Represents a heterogeneous collection of {@link GeoObject GeoObjects}.</p> 
     * 
     * <p>This class encapsulates a list of geometry objects and provides methods to access these objects. 
     * The objects can be of any type that extends {@link GeoObject}.</p> 
     * 
     * <p>This class is useful when you want to work with a collection of geometry objects in a read-only manner. For 
     * example, you can use it to represent a complex geographic feature that is composed of multiple simple geographic 
     * features.</p> 
     * 
     * @see GeoObject 
     * @see GeoBoundingBox 
     */ 
    public final class GeoCollection extends GeoObject { 
        /** 
         * Constructs a {@link GeoCollection}. 
         * 
         * @param geometries The geometries in the collection. 
         * @throws NullPointerException If {@code geometries} is {@code null}. 
         */ 
        public GeoCollection(List<GeoObject> geometries) 
        /** 
         * Constructs a {@link GeoCollection}. 
         * 
         * @param geometries The geometries in the collection. 
         * @param boundingBox Bounding box for the {@link GeoCollection}. 
         * @param customProperties Additional properties of the {@link GeoCollection}. 
         * @throws NullPointerException If {@code geometries} is {@code null}. 
         */ 
        public GeoCollection(List<GeoObject> geometries, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoCollection}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoCollection} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code GeometryCollection}. 
         * @throws IOException If a {@link GeoCollection} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoCollection fromJson(JsonReader jsonReader) throws IOException
        /** 
         * Unmodifiable representation of the {@link GeoObject geometries} contained in this collection. 
         * 
         * @return An unmodifiable representation of the {@link GeoObject geometries} in this collection. 
         */ 
        public List<GeoObject> getGeometries() 
        @Override public int hashCode() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a geometric line string.</p> 
     * 
     * <p>This class encapsulates a list of {@link GeoPosition} instances that form a line string. A line string is a 
     * curve with linear interpolation between points.</p> 
     * 
     * <p>This class is useful when you want to work with a line string in a geographic context. For example, you can use 
     * it to represent a route on a map or the shape of a geographic feature.</p> 
     * 
     * <p>Note: A line string requires at least 2 coordinates.</p> 
     * 
     * @see GeoPosition 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoLineString extends GeoObject { 
        /** 
         * Constructs a geometric line. 
         * 
         * @param positions Geometric positions that define the line. 
         * @throws NullPointerException If {@code positions} is {@code null}. 
         */ 
        public GeoLineString(List<GeoPosition> positions) 
        /** 
         * Constructs a geometric line. 
         * 
         * @param positions Geometric positions that define the line. 
         * @param boundingBox Bounding box for the line. 
         * @param customProperties Additional properties of the geometric line. 
         * @throws NullPointerException If {@code positions} is {@code null}. 
         */ 
        public GeoLineString(List<GeoPosition> positions, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        /** 
         * Unmodifiable representation of the {@link GeoPosition geometric positions} representing this line. 
         * 
         * @return An unmodifiable representation of the {@link GeoPosition geometric positions} representing this line. 
         */ 
        public List<GeoPosition> getCoordinates() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoLineString}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoLineString} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code LineString}. 
         * @throws IOException If a {@link GeoLineString} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoLineString fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a collection of {@link GeoLineString GeoLineStrings}.</p> 
     * 
     * <p>This class encapsulates a list of {@link GeoLineString} instances that form a collection of lines. Each line 
     * string is a curve with linear interpolation between points.</p> 
     * 
     * <p>This class is useful when you want to work with a collection of line strings in a geographic context. 
     * For example, you can use it to represent a complex route on a map that is composed of multiple line strings.</p> 
     * 
     * <p>Note: A line string collection requires at least 2 coordinates for each line string.</p> 
     * 
     * @see GeoLineString 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoLineStringCollection extends GeoObject { 
        /** 
         * Constructs a {@link GeoLineStringCollection}. 
         * 
         * @param lines The geometric lines that define the multi-line. 
         * @throws NullPointerException If {@code lines} is {@code null}. 
         */ 
        public GeoLineStringCollection(List<GeoLineString> lines) 
        /** 
         * Constructs a {@link GeoLineStringCollection}. 
         * 
         * @param lines The geometric lines that define the multi-line. 
         * @param boundingBox Bounding box for the multi-line. 
         * @param customProperties Additional properties of the multi-line. 
         * @throws NullPointerException If {@code lines} is {@code null}. 
         */ 
        public GeoLineStringCollection(List<GeoLineString> lines, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoLineStringCollection}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoLineStringCollection} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code MultiLineString}. 
         * @throws IOException If a {@link GeoLineStringCollection} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoLineStringCollection fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * Unmodifiable representation of the {@link GeoLineString geometric lines} representing this multi-line. 
         * 
         * @return An unmodifiable representation of the {@link GeoLineString geometric lines} representing this multi-line. 
         */ 
        public List<GeoLineString> getLines() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a linear ring that is part of a {@link GeoPolygon}.</p> 
     * 
     * <p>This class encapsulates a list of {@link GeoPosition} instances that form a closed loop, which is a component 
     * of a {@link GeoPolygon}. The first and last positions of the loop are the same, forming a closed ring.</p> 
     * 
     * <p>This class is useful when you want to work with a linear ring in a geographic context. For example, you can 
     * use it to define the boundary of a geographic area in a {@link GeoPolygon}.</p> 
     * 
     * <p>Note: A linear ring requires at least 4 coordinates, and the first and last coordinates must be the same.</p> 
     * 
     * @see GeoPosition 
     * @see GeoPolygon 
     * @see JsonSerializable 
     */ 
    public final class GeoLinearRing implements JsonSerializable<GeoLinearRing> { 
        /** 
         * Constructs a new linear ring with the passed coordinates. 
         * 
         * @param coordinates The coordinates of the linear ring. 
         * @throws NullPointerException If {@code coordinates} is null. 
         * @throws IllegalArgumentException If {@code coordinates} has less than 4 elements or the first and last elements 
         * aren't equivalent. 
         */ 
        public GeoLinearRing(List<GeoPosition> coordinates) 
        /** 
         * Unmodifiable representation of the {@link GeoPosition geometric positions} representing this linear ring. 
         * 
         * @return An unmodifiable representation of the {@link GeoPosition geometric positions} representing this linear 
         * ring. 
         */ 
        public List<GeoPosition> getCoordinates() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoLinearRing}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoLinearRing} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IOException If a {@link GeoLinearRing} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoLinearRing fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
    } 
    @Immutable
    /** 
     * Represents an abstract geometric object in GeoJSON format. 
     * 
     * <p>This class encapsulates the common properties of a geometric object, including the bounding box and additional 
     * custom properties. It provides methods to access these properties.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the geometric object to JSON, 
     * and a {@link #fromJson(JsonReader)} method to deserialize a geometric object from JSON.</p> 
     * 
     * @see GeoBoundingBox 
     * @see GeoPosition 
     * @see GeoPoint 
     * @see GeoLineString 
     * @see GeoPolygon 
     * @see GeoPointCollection 
     * @see GeoLineStringCollection 
     * @see GeoPolygonCollection 
     * @see GeoCollection 
     * @see JsonSerializable 
     */ 
    public abstract class GeoObject implements JsonSerializable<GeoObject> { 
        /** 
         * Creates a {@link GeoObject} instance. 
         * 
         * @param boundingBox Optional bounding box of the {@link GeoObject}. 
         * @param customProperties Optional additional properties to associate to the {@link GeoObject}. 
         */ 
        protected GeoObject(GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        /** 
         * Bounding box for this {@link GeoObject}. 
         * 
         * @return The bounding box for this {@link GeoObject}. 
         */ 
        public final GeoBoundingBox getBoundingBox() 
        /** 
         * Additional properties about this {@link GeoObject}. 
         * 
         * @return An unmodifiable representation of the additional properties associated with this {@link GeoObject}. 
         */ 
        public final Map<String, Object> getCustomProperties() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads an instance of {@link GeoObject} from the JsonReader. 
         * 
         * @param jsonReader The JsonReader being read. 
         * @return An instance of {@link GeoObject} if the JsonReader was pointing to an instance of it, or null if it was 
         * pointing to JSON null. 
         * @throws IllegalStateException If the deserialized JSON object was missing any required properties or the 
         * polymorphic discriminator. 
         * @throws IOException If an error occurs while reading the {@link GeoObject}. 
         */ 
        public static GeoObject fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        /** 
         * Gets the GeoJSON type for this object. 
         * 
         * @return The GeoJSON type for this object. 
         */ 
        public abstract GeoObjectType getType() 
    } 
    /** 
     * <p>Represents the type of a GeoJSON object.</p> 
     * 
     * <p>This class encapsulates the type of a GeoJSON object. It provides constants for the different types of 
     * GeoJSON objects, such as {@link #POINT}, {@link #MULTI_POINT}, {@link #POLYGON}, {@link #MULTI_POLYGON}, 
     * {@link #LINE_STRING}, {@link #MULTI_LINE_STRING}, and {@link #GEOMETRY_COLLECTION}.</p> 
     * 
     * <p>Each GeoJSON object type is represented by an instance of this class. You can use the 
     * {@link #fromString(String)} method to create or get a GeoObjectType from its string representation, 
     * and the {@link #values()} method to get all known GeoObjectType values.</p> 
     * 
     * <p>This class is useful when you want to work with GeoJSON objects and need to specify or check the type of a 
     * GeoJSON object.</p> 
     * 
     * @see ExpandableStringEnum 
     */ 
    public final class GeoObjectType extends ExpandableStringEnum<GeoObjectType> { 
        /** 
         * GeoJSON point. 
         */ 
        public static final GeoObjectType POINT = fromString("Point"); 
        /** 
         * GeoJSON multi-point. 
         */ 
        public static final GeoObjectType MULTI_POINT = fromString("MultiPoint"); 
        /** 
         * GeoJSON polygon. 
         */ 
        public static final GeoObjectType POLYGON = fromString("Polygon"); 
        /** 
         * GeoJSON multi-polygon. 
         */ 
        public static final GeoObjectType MULTI_POLYGON = fromString("MultiPolygon"); 
        /** 
         * GeoJSON line string. 
         */ 
        public static final GeoObjectType LINE_STRING = fromString("LineString"); 
        /** 
         * GeoJSON multi-line string. 
         */ 
        public static final GeoObjectType MULTI_LINE_STRING = fromString("MultiLineString"); 
        /** 
         * GeoJSON geometry collection. 
         */ 
        public static final GeoObjectType GEOMETRY_COLLECTION = fromString("GeometryCollection"); 
        /** 
         * Creates a new instance of {@link GeoObjectType} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link GeoObjectType} which doesn't 
         * have a String enum value. 
         * 
         * @deprecated Use one of the constants or the {@link #fromString(String)} factory method. 
         */ 
        @Deprecated public GeoObjectType() 
        /** 
         * Creates or gets a GeoObjectType from its string representation. 
         * 
         * @param name Name of the GeoObjectType. 
         * @return The corresponding GeoObjectType. 
         */ 
        public static GeoObjectType fromString(String name) 
        /** 
         * Gets all known GeoObjectType values. 
         * 
         * @return All known GeoObjectType values. 
         */ 
        public static Collection<GeoObjectType> values() 
    } 
    @Immutable
    /** 
     * <p>Represents a geometric point in GeoJSON format.</p> 
     * 
     * <p>This class encapsulates a point defined by a {@link GeoPosition} which includes the longitude, latitude, and 
     * optionally the altitude of the point.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the geometric point to JSON, and 
     * a {@link #fromJson(JsonReader)} method to deserialize a geometric point from JSON.</p> 
     * 
     * @see GeoPosition 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoPoint extends GeoObject { 
        /** 
         * Constructs a geometric point. 
         * 
         * @param position The {@link GeoPosition geometric position} of the point. 
         * @throws NullPointerException If {@code position} is {@code null}. 
         */ 
        public GeoPoint(GeoPosition position) 
        /** 
         * Constructs a {@link GeoPoint}. 
         * 
         * @param longitude The longitudinal position of the point. 
         * @param latitude The latitudinal position of the point. 
         */ 
        public GeoPoint(double longitude, double latitude) 
        /** 
         * Constructs a {@link GeoPoint}. 
         * 
         * @param longitude The longitudinal position of the point. 
         * @param latitude The latitudinal position of the point. 
         * @param altitude The altitude of the point. 
         */ 
        public GeoPoint(double longitude, double latitude, Double altitude) 
        /** 
         * Constructs a geometric point. 
         * 
         * @param position The {@link GeoPosition geometric position} of the point. 
         * @param boundingBox Bounding box for the point. 
         * @param customProperties Additional properties of the geometric point. 
         * @throws NullPointerException If {@code position} is {@code null}. 
         */ 
        public GeoPoint(GeoPosition position, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        /** 
         * The {@link GeoPosition geometric position} of the point. 
         * 
         * @return The {@link GeoPosition geometric position} of the point. 
         */ 
        public GeoPosition getCoordinates() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoPoint}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoPoint} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code Point}. 
         * @throws IOException If a {@link GeoPoint} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoPoint fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a collection of {@link GeoPoint GeoPoints} in GeoJSON format.</p> 
     * 
     * <p>This class encapsulates a list of {@link GeoPoint} instances that form a collection of points. Each point is 
     * defined by a {@link GeoPosition} which includes the longitude, latitude, and optionally the altitude.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the collection of points to JSON, 
     * and a {@link #fromJson(JsonReader)} method to deserialize a collection of points from JSON.</p> 
     * 
     * <p>Note:A point collection requires at least 2 coordinates for each point.</p> 
     * 
     * @see GeoPoint 
     * @see GeoPosition 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoPointCollection extends GeoObject { 
        /** 
         * Constructs a {@link GeoPointCollection}. 
         * 
         * @param points The points that define the multi-point. 
         * @throws NullPointerException If {@code points} is {@code null}. 
         */ 
        public GeoPointCollection(List<GeoPoint> points) 
        /** 
         * Constructs a {@link GeoPointCollection}. 
         * 
         * @param points The points that define the multi-point. 
         * @param boundingBox Bounding box for the multi-point. 
         * @param customProperties Additional properties of the multi-point. 
         * @throws NullPointerException If {@code points} is {@code null}. 
         */ 
        public GeoPointCollection(List<GeoPoint> points, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoPointCollection}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoPointCollection} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code MultiPoint}. 
         * @throws IOException If a {@link GeoPointCollection} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoPointCollection fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * Unmodifiable representation of the {@link GeoPoint geometric points} representing this multi-point. 
         * 
         * @return An unmodifiable representation of the {@link GeoPoint geometric points} representing this multi-point. 
         */ 
        public List<GeoPoint> getPoints() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a geometric polygon in GeoJSON format.</p> 
     * 
     * <p>This class encapsulates a polygon defined by a list of {@link GeoLinearRing} instances. Each ring represents a 
     * closed loop of coordinates forming the boundary of the polygon.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the geometric polygon to JSON, and a 
     * {@link #fromJson(JsonReader)} method to deserialize a geometric polygon from JSON.</p> 
     * 
     * <p>This class is useful when you want to work with a polygon in a geographic context. For example, you can use it 
     * to represent a geographic area on a map.</p> 
     * 
     * <p>Note: A polygon requires at least one ring, and each ring requires at least 4 coordinates 
     * (with the first and last coordinates being the same to form a closed loop).</p> 
     * 
     * @see GeoLinearRing 
     * @see GeoPosition 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoPolygon extends GeoObject { 
        /** 
         * Constructs a geometric polygon. 
         * 
         * @param ring The {@link GeoLinearRing ring} that defines the polygon. 
         * @throws NullPointerException If {@code ring} is {@code null}. 
         */ 
        public GeoPolygon(GeoLinearRing ring) 
        /** 
         * Constructs a geometric polygon. 
         * 
         * @param rings The {@link GeoLinearRing rings} that define the polygon. 
         * @throws NullPointerException If {@code rings} is {@code null}. 
         */ 
        public GeoPolygon(List<GeoLinearRing> rings) 
        /** 
         * Constructs a geometric polygon. 
         * 
         * @param ring The {@link GeoLinearRing ring} that defines the polygon. 
         * @param boundingBox Bounding box for the polygon. 
         * @param customProperties Additional properties of the polygon. 
         * @throws NullPointerException If {@code ring} is {@code null}. 
         */ 
        public GeoPolygon(GeoLinearRing ring, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        /** 
         * Constructs a geometric polygon. 
         * 
         * @param rings The {@link GeoLinearRing rings} that define the polygon. 
         * @param boundingBox Bounding box for the polygon. 
         * @param customProperties Additional properties of the polygon. 
         * @throws NullPointerException If {@code rings} is {@code null}. 
         */ 
        public GeoPolygon(List<GeoLinearRing> rings, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoPolygon}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoPolygon} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code Polygon}. 
         * @throws IOException If a {@link GeoPolygon} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoPolygon fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * Gets the outer ring of the polygon. 
         * 
         * @return Outer ring of the polygon. 
         */ 
        public GeoLinearRing getOuterRing() 
        /** 
         * Unmodifiable representation of the {@link GeoLinearRing geometric rings} representing this polygon. 
         * 
         * @return An unmodifiable representation of the {@link GeoLinearRing geometric rings} representing this polygon. 
         */ 
        public List<GeoLinearRing> getRings() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a collection of {@link GeoPolygon GeoPolygons} in GeoJSON format.</p> 
     * 
     * <p>This class encapsulates a list of {@link GeoPolygon} instances that form a collection of polygons. Each polygon 
     * is defined by a list of {@link GeoLinearRing} instances that form the boundary of the polygon.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the collection of polygons to JSON, 
     * and a {@link #fromJson(JsonReader)} method to deserialize a collection of polygons from JSON.</p> 
     * 
     * <p>This class is useful when you want to work with a collection of polygons in a geographic context. For example, 
     * you can use it to represent a complex geographic area on a map that is composed of multiple polygons.</p> 
     * 
     * <p>Note: A polygon collection requires at least one ring for each polygon, and each ring requires at least 
     * 4 coordinates (with the first and last coordinates being the same to form a closed loop).</p> 
     * 
     * @see GeoPolygon 
     * @see GeoLinearRing 
     * @see GeoPosition 
     * @see GeoObject 
     * @see JsonSerializable 
     */ 
    public final class GeoPolygonCollection extends GeoObject { 
        /** 
         * Constructs a {@link GeoPolygonCollection}. 
         * 
         * @param polygons The polygons that define the multi-polygon. 
         * @throws NullPointerException If {@code polygons} is {@code null}. 
         */ 
        public GeoPolygonCollection(List<GeoPolygon> polygons) 
        /** 
         * Constructs a {@link GeoPolygonCollection}. 
         * 
         * @param polygons The polygons that define the multi-polygon. 
         * @param boundingBox Bounding box for the multi-polygon. 
         * @param customProperties Additional properties of the multi-polygon. 
         * @throws NullPointerException If {@code polygons} is {@code null}. 
         */ 
        public GeoPolygonCollection(List<GeoPolygon> polygons, GeoBoundingBox boundingBox, Map<String, Object> customProperties) 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoPolygonCollection}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoPolygonCollection} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@code type} node exists and isn't equal to {@code MultiPolygon}. 
         * @throws IOException If a {@link GeoPolygonCollection} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoPolygonCollection fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * Unmodifiable representation of the {@link GeoPolygon geometric polygons} representing this multi-polygon. 
         * 
         * @return An unmodifiable representation of the {@link GeoPolygon geometric polygons} representing this 
         * multi-polygon. 
         */ 
        public List<GeoPolygon> getPolygons() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public GeoObjectType getType() 
    } 
    @Immutable
    /** 
     * <p>Represents a geographic position in GeoJSON format.</p> 
     * 
     * <p>This class encapsulates a geographic position defined by longitude, latitude, and optionally altitude. It 
     * provides methods to access these properties.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the geographic position to JSON, 
     * and a {@link #fromJson(JsonReader)} method to deserialize a geographic position from JSON.</p> 
     * 
     * <p>This class is useful when you want to work with a geographic position in a geographic context. For example, 
     * you can use it to represent a location on a map or a point in a geographic dataset.</p> 
     * 
     * @see JsonSerializable 
     */ 
    public final class GeoPosition implements JsonSerializable<GeoPosition> { 
        /** 
         * Constructs a geo position. 
         * 
         * @param longitude Longitudinal position. 
         * @param latitude Latitudinal position. 
         */ 
        public GeoPosition(double longitude, double latitude) 
        /** 
         * Constructs a geo position. 
         * 
         * @param longitude Longitudinal position. 
         * @param latitude Latitudinal position. 
         * @param altitude Altitude position. 
         */ 
        public GeoPosition(double longitude, double latitude, Double altitude) 
        /** 
         * The altitude of the geometric position. 
         * 
         * @return The altitude. 
         */ 
        public Double getAltitude() 
        /** 
         * Gets the number of coordinates used to compose the position. 
         * <p> 
         * This will return either 2 or 3 depending on whether {@link #getAltitude() altitude is set}. 
         * 
         * @return The number of coordinates used to compose the position. 
         */ 
        public int count() 
        @Override public boolean equals(Object obj) 
        /** 
         * Reads a JSON stream into a {@link GeoPosition}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link GeoPosition} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the {@link GeoPosition} has less than two or more than three positions in the 
         * array. 
         * @throws IOException If a {@link GeoPosition} fails to be read from the {@code jsonReader}. 
         */ 
        public static GeoPosition fromJson(JsonReader jsonReader) throws IOException
        @Override public int hashCode() 
        /** 
         * The latitudinal position of the geometric position. 
         * 
         * @return The latitudinal position. 
         */ 
        public double getLatitude() 
        /** 
         * The longitudinal position of the geometric position. 
         * 
         * @return The longitudinal position. 
         */ 
        public double getLongitude() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        @Override public String toString() 
    } 
    /** 
     * <p>Represents a JSON Patch document.</p> 
     * 
     * <p>This class encapsulates a list of {@link JsonPatchOperation} instances that form a JSON Patch document. 
     * It provides methods to add various types of operations (add, replace, copy, move, remove, test) to the document.</p> 
     * 
     * <p>Each operation in the document is represented by a {@link JsonPatchOperation} instance, which encapsulates the 
     * operation kind, path, and optional from and value.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the JSON Patch document to JSON, 
     * and a {@link #fromJson(JsonReader)} method to deserialize a JSON Patch document from JSON.</p> 
     * 
     * <p>This class is useful when you want to create a JSON Patch document to express a sequence of operations to 
     * apply to a JSON document.</p> 
     * 
     * @see JsonPatchOperation 
     * @see JsonPatchOperationKind 
     * @see JsonSerializable 
     */ 
    public final class JsonPatchDocument implements JsonSerializable<JsonPatchDocument> { 
        /** 
         * Creates a new JSON Patch document. 
         */ 
        public JsonPatchDocument() 
        /** 
         * Creates a new JSON Patch document. 
         * <p> 
         * If {@code serializer} isn't specified {@link JacksonAdapter} will be used. 
         * 
         * @param serializer The {@link JsonSerializer} that will be used to serialize patch operation values. 
         */ 
        public JsonPatchDocument(JsonSerializer serializer) 
        /** 
         * Appends an "add" operation to this JSON Patch document. 
         * <p> 
         * If the {@code path} doesn't exist a new member is added to the object. If the {@code path} does exist the 
         * previous value is replaced. If the {@code path} specifies an array index the value is inserted at the specified. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.1">JSON Patch Add</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendAdd#String-Object --> 
         * <pre> 
         * /* 
         *  * Add an object member to the JSON document { "foo" : "bar" } to get the JSON document 
         *  * { "bar": "foo", "foo": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendAdd("/bar", "foo"); 
         * 
         * /* 
         *  * Add an array element to the JSON document { "foo": [ "fizz", "fizzbuzz" ] } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ] }. 
         *  */ 
         * jsonPatchDocument.appendAdd("/foo/1", "buzz"); 
         * 
         * /* 
         *  * Add a nested member to the JSON document { "foo": "bar" } to get the JSON document 
         *  * { "foo": "bar", "child": { "grandchild": { } } }. 
         *  */ 
         * jsonPatchDocument.appendAdd("/child", Collections.singletonMap("grandchild", Collections.emptyMap())); 
         * 
         * /* 
         *  * Add an array element to the JSON document { "foo": [ "fizz", "buzz" ] } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ] }. 
         *  */ 
         * jsonPatchDocument.appendAdd("/foo/-", "fizzbuzz"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendAdd#String-Object --> 
         * 
         * @param path The path to apply the addition. 
         * @param value The value that will be serialized and added to the path. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendAdd(String path, Object value) 
        /** 
         * Appends an "add" operation to this JSON Patch document. 
         * <p> 
         * If the {@code path} doesn't exist a new member is added to the object. If the {@code path} does exist the 
         * previous value is replaced. If the {@code path} specifies an array index the value is inserted at the specified. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.1">JSON Patch Add</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendAddRaw#String-String --> 
         * <pre> 
         * /* 
         *  * Add an object member to the JSON document { "foo" : "bar" } to get the JSON document 
         *  * { "bar": "foo", "foo": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendAddRaw("/bar", "\"foo\""); 
         * 
         * /* 
         *  * Add an array element to the JSON document { "foo": [ "fizz", "fizzbuzz" ] } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ] }. 
         *  */ 
         * jsonPatchDocument.appendAddRaw("/foo/1", "\"buzz\""); 
         * 
         * /* 
         *  * Add a nested member to the JSON document { "foo": "bar" } to get the JSON document 
         *  * { "foo": "bar", "child": { "grandchild": { } } }. 
         *  */ 
         * jsonPatchDocument.appendAddRaw("/child", "\"child\": { \"grandchild\": { } }"); 
         * 
         * /* 
         *  * Add an array element to the JSON document { "foo": [ "fizz", "buzz" ] } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ] }. 
         *  */ 
         * jsonPatchDocument.appendAddRaw("/foo/-", "\"fizzbuzz\""); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendAddRaw#String-String --> 
         * 
         * @param path The path to apply the addition. 
         * @param rawJson The raw JSON value that will be added to the path. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendAddRaw(String path, String rawJson) 
        /** 
         * Appends a "copy" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.5">JSON Patch copy</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendCopy#String-String --> 
         * <pre> 
         * /* 
         *  * Copy an object member in the JSON document { "foo": "bar" } to get the JSON document 
         *  * { "foo": "bar", "copy": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendCopy("/foo", "/copy"); 
         * 
         * /* 
         *  * Copy an object member in the JSON document { "foo": { "bar": "baz" } } to get the JSON document 
         *  * { "foo": { "bar": "baz" }, "bar": "baz" }. 
         *  */ 
         * jsonPatchDocument.appendCopy("/foo/bar", "/bar"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an invalid copy operation as the 
         *  * target from doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendCopy("/baz", "/fizz"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendCopy#String-String --> 
         * 
         * @param from The path to copy from. 
         * @param path The path to copy to. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code from} or {@code path} is null. 
         */ 
        public JsonPatchDocument appendCopy(String from, String path) 
        /** 
         * Appends a "move" operation to this JSON Patch document. 
         * <p> 
         * For the operation to be successful {@code path} cannot be a child node of {@code from}. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.4">JSON Patch move</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendMove#String-String --> 
         * <pre> 
         * /* 
         *  * Move an object member in the JSON document { "foo": "bar", "bar": "foo" } to get the JSON document 
         *  * { "bar": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendMove("/foo", "/bar"); 
         * 
         * /* 
         *  * Move an object member in the JSON document { "foo": { "bar": "baz" } } to get the JSON document 
         *  * { "foo": "baz" }. 
         *  */ 
         * jsonPatchDocument.appendMove("/foo/bar", "/foo"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": { "bar": "baz" } } the following is an example of an invalid move operation 
         *  * as the target path is a child of the target from. 
         *  */ 
         * jsonPatchDocument.appendMove("/foo", "/foo/bar"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an invalid move operation as the 
         *  * target from doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendMove("/baz", "/fizz"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendMove#String-String --> 
         * 
         * @param from The path to move from. 
         * @param path The path to move to. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code from} or {@code path} is null. 
         */ 
        public JsonPatchDocument appendMove(String from, String path) 
        /** 
         * Appends a "remove" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.2">JSON Patch remove</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendRemove#String --> 
         * <pre> 
         * /* 
         *  * Remove an object member in the JSON document { "foo": "bar", "bar": "foo" } to get the JSON document 
         *  * { "foo": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendRemove("/bar"); 
         * 
         * /* 
         *  * Remove an object member in the JSON document { "foo": { "bar": "baz" } } to get the JSON document 
         *  * { "foo": { } }. 
         *  */ 
         * jsonPatchDocument.appendRemove("/foo/bar"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an invalid remove operation as the 
         *  * target from doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendRemove("/baz"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendRemove#String --> 
         * 
         * @param path The path to remove. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendRemove(String path) 
        /** 
         * Appends a "replace" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.3">JSON Patch replace</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendReplace#String-Object --> 
         * <pre> 
         * /* 
         *  * Replace an object member in the JSON document { "bar": "qux", "foo": "bar" } to get the JSON document 
         *  * { "bar": "foo", "foo": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendReplace("/bar", "foo"); 
         * 
         * /* 
         *  * Replace an object member in the JSON document { "foo": "fizz" } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ]  }. 
         *  */ 
         * jsonPatchDocument.appendReplace("/foo", new String[] {"fizz", "buzz", "fizzbuzz"}); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an invalid replace operation as the 
         *  * target path doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendReplace("/baz", "foo"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendReplace#String-Object --> 
         * 
         * @param path The path to replace. 
         * @param value The value will be serialized and used as the replacement. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendReplace(String path, Object value) 
        /** 
         * Appends a "replace" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.3">JSON Patch replace</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendReplaceRaw#String-String --> 
         * <pre> 
         * /* 
         *  * Replace an object member in the JSON document { "bar": "qux", "foo": "bar" } to get the JSON document 
         *  * { "bar": "foo", "foo": "bar" }. 
         *  */ 
         * jsonPatchDocument.appendReplaceRaw("/bar", "\"foo\""); 
         * 
         * /* 
         *  * Replace an object member in the JSON document { "foo": "fizz" } to get the JSON document 
         *  * { "foo": [ "fizz", "buzz", "fizzbuzz" ]  }. 
         *  */ 
         * jsonPatchDocument.appendReplaceRaw("/foo", "[ \"fizz\", \"buzz\", \"fizzbuzz\" ]"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an invalid replace operation as the 
         *  * target path doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendReplaceRaw("/baz", "\"foo\""); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendReplaceRaw#String-String --> 
         * 
         * @param path The path to replace. 
         * @param rawJson The raw JSON value that will be used as the replacement. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendReplaceRaw(String path, String rawJson) 
        /** 
         * Appends a "test" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.6">JSON Patch test</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendTest#String-Object --> 
         * <pre> 
         * /* 
         *  * Test an object member in the JSON document { "foo": "bar" } to get a successful operation. 
         *  */ 
         * jsonPatchDocument.appendTest("/foo", "bar"); 
         * 
         * /* 
         *  * Test an object member in the JSON document { "foo": "bar" } to get a unsuccessful operation. 
         *  */ 
         * jsonPatchDocument.appendTest("/foo", 42); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an unsuccessful test operation as 
         *  * the target path doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendTest("/baz", "bar"); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendTest#String-Object --> 
         * 
         * @param path The path to test. 
         * @param value The value that will be serialized and used to test against. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendTest(String path, Object value) 
        /** 
         * Appends a "test" operation to this JSON Patch document. 
         * <p> 
         * See <a href="https://tools.ietf.org/html/rfc6902#section-4.6">JSON Patch test</a> for more information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.JsonPatchDocument.appendTestRaw#String-String --> 
         * <pre> 
         * /* 
         *  * Test an object member in the JSON document { "foo": "bar" } to get a successful operation. 
         *  */ 
         * jsonPatchDocument.appendTestRaw("/foo", "\"bar\""); 
         * 
         * /* 
         *  * Test an object member in the JSON document { "foo": "bar" } to get a unsuccessful operation. 
         *  */ 
         * jsonPatchDocument.appendTestRaw("/foo", "42"); 
         * 
         * /* 
         *  * Given the JSON document { "foo": "bar" } the following is an example of an unsuccessful test operation as 
         *  * the target path doesn't exist in the document. 
         *  */ 
         * jsonPatchDocument.appendTestRaw("/baz", "\"bar\""); 
         * </pre> 
         * <!-- end com.azure.core.util.JsonPatchDocument.appendTestRaw#String-String --> 
         * 
         * @param path The path to test. 
         * @param rawJson The raw JSON value that will be used to test against. 
         * @return The updated JsonPatchDocument object. 
         * @throws NullPointerException If {@code path} is null. 
         */ 
        public JsonPatchDocument appendTestRaw(String path, String rawJson) 
        /** 
         * Reads a JSON stream into a {@link JsonPatchDocument}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link JsonPatchDocument} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the deserialized JSON object was missing any required properties. 
         * @throws IOException If a {@link JsonPatchDocument} fails to be read from the {@code jsonReader}. 
         */ 
        public static JsonPatchDocument fromJson(JsonReader jsonReader) throws IOException
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
        /** 
         * Gets a formatted JSON string representation of this JSON Patch document. 
         * 
         * @return The formatted JSON String representing this JSON Patch document. 
         */ 
        @Override public String toString() 
    } 
    @Fluent
    /** 
     * <p>Represents a message with a specific content type and data.</p> 
     * 
     * <p>This class encapsulates a message that includes a content type and its corresponding data. The data is 
     * represented as a {@link BinaryData} object, and the content type is a string.</p> 
     * 
     * <p>This class is useful when you want to work with a message that includes a specific type of content and its 
     * corresponding data. For example, you can use it to represent a message with JSON data, XML data, 
     * or plain text data.</p> 
     * 
     * @see BinaryData 
     */ 
    public class MessageContent { 
        /** 
         * Creates a new instance of {@link MessageContent}. 
         */ 
        public MessageContent() 
        /** 
         * Gets the message body. 
         * 
         * @return The message body. 
         */ 
        public BinaryData getBodyAsBinaryData() 
        /** 
         * Sets the message body. 
         * 
         * @param binaryData The message body. 
         * @return The updated {@link MessageContent} object. 
         */ 
        public MessageContent setBodyAsBinaryData(BinaryData binaryData) 
        /** 
         * Gets the content type. 
         * 
         * @return The content type. 
         */ 
        public String getContentType() 
        /** 
         * Sets the content type. 
         * 
         * @param contentType The content type. 
         * @return The updated {@link MessageContent} object. 
         */ 
        public MessageContent setContentType(String contentType) 
    } 
    /** 
     * <p>Represents the error details of an HTTP response.</p> 
     * 
     * <p>This class encapsulates the details of an HTTP error response, including the error code, message, target, 
     * inner error, and additional error details. It provides methods to access these properties.</p> 
     * 
     * <p>This class also provides a {@link #toJson(JsonWriter)} method to serialize the error details to JSON, and 
     * a {@link #fromJson(JsonReader)} method to deserialize the error details from JSON.</p> 
     * 
     * @see JsonSerializable 
     * @see JsonReader 
     * @see JsonWriter 
     */ 
    public final class ResponseError implements JsonSerializable<ResponseError> { 
        /** 
         * Creates an instance of {@link ResponseError}. 
         * 
         * @param code the error code of this error. 
         * @param message the error message of this error. 
         */ 
        public ResponseError(String code, String message) 
        /** 
         * Returns the error code of this error. 
         * 
         * @return the error code of this error. 
         */ 
        public String getCode() 
        /** 
         * Reads a JSON stream into a {@link ResponseError}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link ResponseError} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the deserialized JSON object was missing any required properties. 
         * @throws IOException If a {@link ResponseError} fails to be read from the {@code jsonReader}. 
         */ 
        public static ResponseError fromJson(JsonReader jsonReader) throws IOException
        /** 
         * Returns the error message of this error. 
         * 
         * @return the error message of this error. 
         */ 
        public String getMessage() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
    } 
} 
/** 
 * Package containing core utility classes. 
 */ 
package com.azure.core.util { 
    /** 
     * Interface for close operations that are asynchronous. 
     * 
     * <p> 
     * <strong>Asynchronously closing a class</strong> 
     * </p> 
     * <p> 
     * In the snippet below, we have a long-lived {@code NetworkResource} class. There are some operations such 
     * as closing {@literal I/O}. Instead of returning a sync {@code close()}, we use {@code closeAsync()} so users' 
     * programs don't block waiting for this operation to complete. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.AsyncCloseable.closeAsync --> 
     * <pre> 
     * NetworkResource resource = new NetworkResource(); 
     * resource.longRunningDownload("https://longdownload.com") 
     *     .subscribe( 
     *         byteBuffer -> System.out.println("Buffer received: " + byteBuffer), 
     *         error -> System.err.printf("Error occurred while downloading: %s%n", error), 
     *         () -> System.out.println("Completed download operation.")); 
     * 
     * System.out.println("Press enter to stop downloading."); 
     * System.in.read(); 
     * 
     * // We block here because it is the end of the main Program function. A real-life program may chain this 
     * // with some other close operations like save download/program state, etc. 
     * resource.closeAsync().block(); 
     * </pre> 
     * <!-- end com.azure.core.util.AsyncCloseable.closeAsync --> 
     */ 
    public interface AsyncCloseable { 
        /** 
         * Begins the close operation. If one is in progress, will return that existing close operation. If the close 
         * operation is unsuccessful, the Mono completes with an error. 
         * 
         * @return A Mono representing the close operation. If the close operation is unsuccessful, the Mono completes with 
         *     an error. 
         */ 
        Mono<Void> closeAsync() 
    } 
    /** 
     * An authenticate challenge. 
     * <p> 
     * This challenge can be from any source, but will primarily be from parsing {@link HttpHeaderName#WWW_AUTHENTICATE} or 
     * {@link HttpHeaderName#PROXY_AUTHENTICATE} headers using {@link CoreUtils#parseAuthenticateHeader(String)}. 
     * <p> 
     * Some challenge information may be optional, meaning the getters may return null or an empty collection. 
     */ 
    public final class AuthenticateChallenge { 
        /** 
         * Creates an instance of the AuthenticateChallenge. 
         * 
         * @param scheme The scheme of the challenge. 
         * @throws IllegalArgumentException If the scheme is null or empty. 
         */ 
        public AuthenticateChallenge(String scheme) 
        /** 
         * Creates an instance of the AuthenticateChallenge. 
         * 
         * @param scheme The scheme of the challenge. 
         * @param token68 The token68 of the challenge. 
         * @throws IllegalArgumentException If the scheme is null or empty. 
         */ 
        public AuthenticateChallenge(String scheme, String token68) 
        /** 
         * Creates an instance of the AuthenticateChallenge. 
         * 
         * @param scheme The scheme of the challenge. 
         * @param parameters The parameters of the challenge. 
         * @throws IllegalArgumentException If the scheme is null or empty. 
         */ 
        public AuthenticateChallenge(String scheme, Map<String, String> parameters) 
        /** 
         * Gets the parameters of the challenge as a read-only map. 
         * <p> 
         * This map will be empty if the challenge does not have any parameters. 
         * 
         * @return The parameters of the challenge. 
         */ 
        public Map<String, String> getParameters() 
        /** 
         * Gets the scheme of the challenge. 
         * 
         * @return The scheme of the challenge. 
         */ 
        public String getScheme() 
        /** 
         * Gets the token68 of the challenge. 
         * <p> 
         * This may be null if the challenge does not have a token68. 
         * 
         * @return The token68 of the challenge, or null if the challenge does not have a token68. 
         */ 
        public String getToken68() 
    } 
    /** 
     * This class handles Basic and Digest authorization challenges, complying to RFC 2617 and RFC 7616. 
     */ 
    public class AuthorizationChallengeHandler { 
        /** 
         * Header representing a server requesting authentication. 
         */ 
        public static final String WWW_AUTHENTICATE = "WWW-Authenticate"; 
        /** 
         * Header representing a proxy server requesting authentication. 
         */ 
        public static final String PROXY_AUTHENTICATE = "Proxy-Authenticate"; 
        /** 
         * Header representing the authorization the client is presenting to a server. 
         */ 
        public static final String AUTHORIZATION = "Authorization"; 
        /** 
         * Header representing the authorization the client is presenting to a proxy server. 
         */ 
        public static final String PROXY_AUTHORIZATION = "Proxy-Authorization"; 
        /** 
         * Header representing additional information a server is expecting during future authentication requests. 
         */ 
        public static final String AUTHENTICATION_INFO = "Authentication-Info"; 
        /** 
         * Header representing additional information a proxy server is expecting during future authentication requests. 
         */ 
        public static final String PROXY_AUTHENTICATION_INFO = "Proxy-Authentication-Info"; 
        /** 
         * Creates an {@link AuthorizationChallengeHandler} using the {@code username} and {@code password} to respond to 
         * authentication challenges. 
         * 
         * @param username Username used to response to authorization challenges. 
         * @param password Password used to respond to authorization challenges. 
         * @throws NullPointerException If {@code username} or {@code password} are {@code null}. 
         */ 
        public AuthorizationChallengeHandler(String username, String password) 
        /** 
         * Attempts to pipeline requests by applying the most recent authorization type used to create an authorization 
         * header. 
         * 
         * @param method HTTP method being used in the request. 
         * @param uri Relative URI for the request. 
         * @param entityBodySupplier Supplies the request entity body, used to compute the hash of the body when using 
         * {@code "qop=auth-int"}. 
         * @return A preemptive authorization header for a potential Digest authentication challenge. 
         */ 
        public final String attemptToPipelineAuthorization(String method, String uri, Supplier<byte[]> entityBodySupplier) 
        /** 
         * Consumes either the 'Authentication-Info' or 'Proxy-Authentication-Info' header returned in a response from a 
         * server. This header is used by the server to communicate information about the successful authentication of the 
         * client, this header may be returned at any time by the server. 
         * 
         * <p>See <a href="https://tools.ietf.org/html/rfc7615">RFC 7615</a> for more information about these headers.</p> 
         * 
         * @param authenticationInfoMap Either 'Authentication-Info' or 'Proxy-Authentication-Info' header returned from the 
         * server split into its key-value pair pieces. 
         */ 
        public final void consumeAuthenticationInfoHeader(Map<String, String> authenticationInfoMap) 
        /** 
         * Handles Basic authentication challenges. 
         * 
         * @return Authorization header for Basic authentication challenges. 
         */ 
        public final String handleBasic() 
        /** 
         * Handles Digest authentication challenges. 
         * 
         * @param method HTTP method being used in the request. 
         * @param uri Relative URI for the request. 
         * @param challenges List of challenges that the server returned for the client to choose from and use when creating 
         * the authorization header. 
         * @param entityBodySupplier Supplies the request entity body, used to compute the hash of the body when using 
         * {@code "qop=auth-int"}. 
         * @return Authorization header for Digest authentication challenges. 
         */ 
        public final String handleDigest(String method, String uri, List<Map<String, String>> challenges, Supplier<byte[]> entityBodySupplier) 
        /** 
         * Parses the {@code Authorization} or {@code Authentication} header into its key-value pairs. 
         * <p> 
         * This will remove quotes on quoted string values. 
         * 
         * @param header Authorization or Authentication header. 
         * @return The Authorization or Authentication header split into its key-value pairs. 
         */ 
        public static Map<String, String> parseAuthenticationOrAuthorizationHeader(String header) 
    } 
    /** 
     * Encodes and decodes using Base64 URL encoding. 
     */ 
    public final class Base64Url { 
        /** 
         * Creates a new Base64Url object with the specified encoded string. 
         * 
         * @param string The encoded string. 
         */ 
        public Base64Url(String string) 
        /** 
         * Creates a new Base64Url object with the specified encoded bytes. 
         * 
         * @param bytes The encoded bytes. 
         */ 
        public Base64Url(byte[] bytes) 
        /** 
         * Decode the bytes and returns its value. 
         * 
         * @return The decoded byte array. 
         */ 
        public byte[] decodedBytes() 
        /** 
         * Encodes a byte array into Base64Url encoded bytes. 
         * 
         * @param bytes The byte array to encode. 
         * @return A new Base64Url instance. 
         */ 
        public static Base64Url encode(byte[] bytes) 
        /** 
         * Returns the underlying encoded byte array. 
         * 
         * @return The underlying encoded byte array. 
         */ 
        public byte[] encodedBytes() 
        @Override public boolean equals(Object obj) 
        @Override public int hashCode() 
        @Override public String toString() 
    } 
    /** 
     * Utility type exposing Base64 encoding and decoding methods. 
     */ 
    public final class Base64Util { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Decodes a base64 encoded byte array. 
         * @param encoded the byte array to decode 
         * @return the decoded byte array 
         */ 
        public static byte[] decode(byte[] encoded) 
        /** 
         * Decodes a base64 encoded string. 
         * @param encoded the string to decode 
         * @return the decoded byte array 
         */ 
        public static byte[] decodeString(String encoded) 
        /** 
         * Decodes a byte array in base64 URL format. 
         * @param src the byte array to decode 
         * @return the decoded byte array 
         */ 
        public static byte[] decodeURL(byte[] src) 
        /** 
         * Encodes a byte array to base64. 
         * @param src the byte array to encode 
         * @return the base64 encoded bytes 
         */ 
        public static byte[] encode(byte[] src) 
        /** 
         * Encodes a byte array to a base 64 string. 
         * @param src the byte array to encode 
         * @return the base64 encoded string 
         */ 
        public static String encodeToString(byte[] src) 
        /** 
         * Encodes a byte array to base64 URL format. 
         * @param src the byte array to encode 
         * @return the base64 URL encoded bytes 
         */ 
        public static byte[] encodeURLWithoutPadding(byte[] src) 
    } 
    /** 
     * BinaryData is a convenient data interchange class for use throughout the Azure SDK for Java. Put simply, BinaryData 
     * enables developers to bring data in from external sources, and read it back from Azure services, in formats that 
     * appeal to them. This leaves BinaryData, and the Azure SDK for Java, the task of converting this data into appropriate 
     * formats to be transferred to and from these external services. This enables developers to focus on their business 
     * logic, and enables the Azure SDK for Java to optimize operations for best performance. 
     * <p> 
     * BinaryData in its simplest form can be thought of as a container for content. Often this content is already in-memory 
     * as a String, byte array, or an Object that can be serialized into a String or byte[]. When the BinaryData is about to 
     * be sent to an Azure Service, this in-memory content is copied into the network request and sent to the service. 
     * </p> 
     * <p> 
     * In more performance critical scenarios, where copying data into memory results in increased memory pressure, it is 
     * possible to create a BinaryData instance from a stream of data. From this, BinaryData can be connected directly to 
     * the outgoing network connection so that the stream is read directly to the network, without needing to first be read 
     * into memory on the system. Similarly, it is possible to read a stream of data from a BinaryData returned from an 
     * Azure Service without it first being read into memory. In many situations, these streaming operations can drastically 
     * reduce the memory pressure in applications, and so it is encouraged that all developers very carefully consider their 
     * ability to use the most appropriate API in BinaryData whenever they encounter an client library that makes use of 
     * BinaryData. 
     * </p> 
     * <p> 
     * Refer to the documentation of each method in the BinaryData class to better understand its performance 
     * characteristics, and refer to the samples below to understand the common usage scenarios of this class. 
     * </p> 
     * 
     * {@link BinaryData} can be created from an {@link InputStream}, a {@link Flux} of {@link ByteBuffer}, a 
     * {@link String}, an {@link Object}, a {@link Path file}, or a byte array. 
     * 
     * <p> 
     * <strong>A note on data mutability</strong> 
     * </p> 
     * 
     * {@link BinaryData} does not copy data on construction. BinaryData keeps a reference to the source content and is 
     * accessed when a read request is made. So, any modifications to the underlying source before the content is read can 
     * result in undefined behavior. 
     * <p> 
     * To create an instance of {@link BinaryData}, use the various static factory methods available. They all start with 
     * {@code 'from'} prefix, for example {@link BinaryData#fromBytes(byte[])}. 
     * </p> 
     * 
     * <p> 
     * <strong>Create an instance from a byte array</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromBytes#byte --> 
     * <pre> 
     * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
     * BinaryData binaryData = BinaryData.fromBytes(data); 
     * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromBytes#byte --> 
     * 
     * <p> 
     * <strong>Create an instance from a String</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromString#String --> 
     * <pre> 
     * final String data = "Some Data"; 
     * // Following will use default character set as StandardCharsets.UTF_8 
     * BinaryData binaryData = BinaryData.fromString(data); 
     * System.out.println(binaryData.toString()); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromString#String --> 
     * 
     * <p> 
     * <strong>Create an instance from an InputStream</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromStream#InputStream --> 
     * <pre> 
     * final ByteArrayInputStream inputStream = new ByteArrayInputStream("Some Data".getBytes(StandardCharsets.UTF_8)); 
     * BinaryData binaryData = BinaryData.fromStream(inputStream); 
     * System.out.println(binaryData); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromStream#InputStream --> 
     * 
     * <p> 
     * <strong>Create an instance from an Object</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromObject#Object --> 
     * <pre> 
     * final Person data = new Person().setName("John"); 
     * 
     * // Provide your custom serializer or use Azure provided serializers. 
     * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
     * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
     * BinaryData binaryData = BinaryData.fromObject(data); 
     * 
     * System.out.println(binaryData); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromObject#Object --> 
     * 
     * <p> 
     * <strong>Create an instance from {@code Flux<ByteBuffer>}</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromFlux#Flux --> 
     * <pre> 
     * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
     * final Flux<ByteBuffer> dataFlux = Flux.just(ByteBuffer.wrap(data)); 
     * 
     * Mono<BinaryData> binaryDataMono = BinaryData.fromFlux(dataFlux); 
     * 
     * Disposable subscriber = binaryDataMono 
     *     .map(binaryData -> { 
     *         System.out.println(binaryData.toString()); 
     *         return true; 
     *     }) 
     *     .subscribe(); 
     * 
     * // So that your program wait for above subscribe to complete. 
     * TimeUnit.SECONDS.sleep(5); 
     * subscriber.dispose(); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromFlux#Flux --> 
     * 
     * <p> 
     * <strong>Create an instance from a file</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.BinaryData.fromFile --> 
     * <pre> 
     * BinaryData binaryData = BinaryData.fromFile(new File("path/to/file").toPath()); 
     * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
     * </pre> 
     * <!-- end com.azure.core.util.BinaryData.fromFile --> 
     * 
     * @see ObjectSerializer 
     * @see JsonSerializer 
     * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
     */ 
    public final class BinaryData { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link ByteBuffer}. 
         * <p> 
         * If the {@link ByteBuffer} is zero length an empty {@link BinaryData} will be returned. Note that the input 
         * {@link ByteBuffer} is used as a reference by this instance of {@link BinaryData} and any changes to the 
         * {@link ByteBuffer} outside of this instance will result in the contents of this BinaryData instance being updated 
         * as well. To safely update the {@link ByteBuffer} without impacting the BinaryData instance, perform an array copy 
         * first. 
         * </p> 
         * 
         * <p><strong>Create an instance from a ByteBuffer</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromByteBuffer#ByteBuffer --> 
         * <pre> 
         * final ByteBuffer data = ByteBuffer.wrap("Some Data".getBytes(StandardCharsets.UTF_8)); 
         * BinaryData binaryData = BinaryData.fromByteBuffer(data); 
         * System.out.println(binaryData); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromByteBuffer#ByteBuffer --> 
         * 
         * @param data The {@link ByteBuffer} that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the {@link ByteBuffer}. 
         * @throws NullPointerException If {@code data} is null. 
         */ 
        public static BinaryData fromByteBuffer(ByteBuffer data) 
        /** 
         * Creates an instance of {@link BinaryData} from the given byte array. 
         * <p> 
         * If the byte array is zero length an empty {@link BinaryData} will be returned. Note that the input byte array is 
         * used as a reference by this instance of {@link BinaryData} and any changes to the byte array outside of this 
         * instance will result in the contents of this BinaryData instance being updated as well. To safely update the byte 
         * array without impacting the BinaryData instance, perform an array copy first. 
         * </p> 
         * 
         * <p><strong>Create an instance from a byte array</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromBytes#byte --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * BinaryData binaryData = BinaryData.fromBytes(data); 
         * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromBytes#byte --> 
         * 
         * @param data The byte array that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the byte array. 
         * @throws NullPointerException If {@code data} is null. 
         */ 
        public static BinaryData fromBytes(byte[] data) 
        /** 
         * Creates a {@link BinaryData} that uses the content of the file at {@link Path} as its data. This method checks 
         * for the existence of the file at the time of creating an instance of {@link BinaryData}. The file, however, is 
         * not read until there is an attempt to read the contents of the returned BinaryData instance. 
         * 
         * <p><strong>Create an instance from a file</strong></p> 
         * 
         * <p>The {@link BinaryData} returned from this method uses 8KB chunk size when reading file content.</p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFile --> 
         * <pre> 
         * BinaryData binaryData = BinaryData.fromFile(new File("path/to/file").toPath()); 
         * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFile --> 
         * 
         * @param file The {@link Path} that will be the {@link BinaryData} data. 
         * @return A new {@link BinaryData}. 
         * @throws NullPointerException If {@code file} is null. 
         */ 
        public static BinaryData fromFile(Path file) 
        /** 
         * Creates a {@link BinaryData} that uses the content of the file at {@link Path file} as its data. This method 
         * checks for the existence of the file at the time of creating an instance of {@link BinaryData}. The file, 
         * however, is not read until there is an attempt to read the contents of the returned BinaryData instance. 
         * 
         * <p><strong>Create an instance from a file</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFile#Path-int --> 
         * <pre> 
         * BinaryData binaryData = BinaryData.fromFile(new File("path/to/file").toPath(), 8092); 
         * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFile#Path-int --> 
         * 
         * @param file The {@link Path} that will be the {@link BinaryData} data. 
         * @param chunkSize The requested size for each read of the path. 
         * @return A new {@link BinaryData}. 
         * @throws NullPointerException If {@code file} is null. 
         * @throws IllegalArgumentException If {@code offset} or {@code length} are negative or {@code offset} plus 
         * {@code length} is greater than the file size or {@code chunkSize} is less than or equal to 0. 
         * @throws UncheckedIOException if the file does not exist. 
         */ 
        public static BinaryData fromFile(Path file, int chunkSize) 
        /** 
         * Creates a {@link BinaryData} that uses the content of the file at {@link Path file} as its data. This method 
         * checks for the existence of the file at the time of creating an instance of {@link BinaryData}. The file, 
         * however, is not read until there is an attempt to read the contents of the returned BinaryData instance. 
         * 
         * <p><strong>Create an instance from a file</strong></p> 
         * 
         * <p>The {@link BinaryData} returned from this method uses 8KB chunk size when reading file content.</p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFile#Path-Long-Long --> 
         * <pre> 
         * long position = 1024; 
         * long length = 100 * 1048; 
         * BinaryData binaryData = BinaryData.fromFile( 
         *     new File("path/to/file").toPath(), position, length); 
         * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFile#Path-Long-Long --> 
         * 
         * @param file The {@link Path} that will be the {@link BinaryData} data. 
         * @param position Position, or offset, within the path where reading begins. 
         * @param length Maximum number of bytes to be read from the path. 
         * @return A new {@link BinaryData}. 
         * @throws NullPointerException If {@code file} is null. 
         * @throws IllegalArgumentException If {@code offset} or {@code length} are negative or {@code offset} plus 
         * {@code length} is greater than the file size or {@code chunkSize} is less than or equal to 0. 
         * @throws UncheckedIOException if the file does not exist. 
         */ 
        public static BinaryData fromFile(Path file, Long position, Long length) 
        /** 
         * Creates a {@link BinaryData} that uses the content of the file at {@link Path file} as its data. This method 
         * checks for the existence of the file at the time of creating an instance of {@link BinaryData}. The file, 
         * however, is not read until there is an attempt to read the contents of the returned BinaryData instance. 
         * 
         * <p><strong>Create an instance from a file</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFile#Path-Long-Long-int --> 
         * <pre> 
         * long position = 1024; 
         * long length = 100 * 1048; 
         * int chunkSize = 8092; 
         * BinaryData binaryData = BinaryData.fromFile( 
         *     new File("path/to/file").toPath(), position, length, chunkSize); 
         * System.out.println(new String(binaryData.toBytes(), StandardCharsets.UTF_8)); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFile#Path-Long-Long-int --> 
         * 
         * @param file The {@link Path} that will be the {@link BinaryData} data. 
         * @param position Position, or offset, within the path where reading begins. 
         * @param length Maximum number of bytes to be read from the path. 
         * @param chunkSize The requested size for each read of the path. 
         * @return A new {@link BinaryData}. 
         * @throws NullPointerException If {@code file} is null. 
         * @throws IllegalArgumentException If {@code offset} or {@code length} are negative or {@code offset} plus 
         * {@code length} is greater than the file size or {@code chunkSize} is less than or equal to 0. 
         * @throws UncheckedIOException if the file does not exist. 
         */ 
        public static BinaryData fromFile(Path file, Long position, Long length, int chunkSize) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link Flux} of {@link ByteBuffer}. 
         * 
         * <p><strong>Create an instance from a Flux of ByteBuffer</strong></p> 
         * 
         * <p>This method aggregates data into single byte array.</p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFlux#Flux --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * final Flux<ByteBuffer> dataFlux = Flux.just(ByteBuffer.wrap(data)); 
         * 
         * Mono<BinaryData> binaryDataMono = BinaryData.fromFlux(dataFlux); 
         * 
         * Disposable subscriber = binaryDataMono 
         *     .map(binaryData -> { 
         *         System.out.println(binaryData.toString()); 
         *         return true; 
         *     }) 
         *     .subscribe(); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFlux#Flux --> 
         * 
         * @param data The {@link Flux} of {@link ByteBuffer} that {@link BinaryData} will represent. 
         * @return A {@link Mono} of {@link BinaryData} representing the {@link Flux} of {@link ByteBuffer}. 
         * @throws NullPointerException If {@code data} is null. 
         */ 
        public static Mono<BinaryData> fromFlux(Flux<ByteBuffer> data) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link Flux} of {@link ByteBuffer}. 
         * 
         * <p><strong>Create an instance from a Flux of ByteBuffer</strong></p> 
         * 
         * <p>This method aggregates data into single byte array.</p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFlux#Flux-Long --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * final long length = data.length; 
         * final Flux<ByteBuffer> dataFlux = Flux.just(ByteBuffer.wrap(data)); 
         * 
         * Mono<BinaryData> binaryDataMono = BinaryData.fromFlux(dataFlux, length); 
         * 
         * Disposable subscriber = binaryDataMono 
         *     .map(binaryData -> { 
         *         System.out.println(binaryData.toString()); 
         *         return true; 
         *     }) 
         *     .subscribe(); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFlux#Flux-Long --> 
         * 
         * @param data The {@link Flux} of {@link ByteBuffer} that {@link BinaryData} will represent. 
         * @param length The length of {@code data} in bytes. 
         * @return A {@link Mono} of {@link BinaryData} representing the {@link Flux} of {@link ByteBuffer}. 
         * @throws IllegalArgumentException if the length is less than zero. 
         * @throws NullPointerException if {@code data} is null. 
         */ 
        public static Mono<BinaryData> fromFlux(Flux<ByteBuffer> data, Long length) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link Flux} of {@link ByteBuffer}. 
         * <p> 
         * If {@code bufferContent} is true and {@code length} is null the length of the returned {@link BinaryData} will be 
         * based on the length calculated by buffering. If {@code length} is non-null it will always be used as the 
         * {@link BinaryData} length even if buffering determines a different length. 
         * 
         * <p><strong>Create an instance from a Flux of ByteBuffer</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromFlux#Flux-Long-boolean --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * final long length = data.length; 
         * final boolean shouldAggregateData = false; 
         * final Flux<ByteBuffer> dataFlux = Flux.just(ByteBuffer.wrap(data)); 
         * 
         * Mono<BinaryData> binaryDataMono = BinaryData.fromFlux(dataFlux, length, shouldAggregateData); 
         * 
         * Disposable subscriber = binaryDataMono 
         *     .map(binaryData -> { 
         *         System.out.println(binaryData.toString()); 
         *         return true; 
         *     }) 
         *     .subscribe(); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromFlux#Flux-Long-boolean --> 
         * 
         * @param data The {@link Flux} of {@link ByteBuffer} that {@link BinaryData} will represent. 
         * @param length The length of {@code data} in bytes. 
         * @param bufferContent A flag indicating whether {@link Flux} should be buffered eagerly or consumption deferred. 
         * @return A {@link Mono} of {@link BinaryData} representing the {@link Flux} of {@link ByteBuffer}. 
         * @throws IllegalArgumentException if the length is less than zero. 
         * @throws NullPointerException if {@code data} is null. 
         */ 
        public static Mono<BinaryData> fromFlux(Flux<ByteBuffer> data, Long length, boolean bufferContent) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link List} of {@link ByteBuffer}. 
         * 
         * <p> 
         * The input {@link ByteBuffer} instances are used as a reference by this instance of {@link BinaryData} and any 
         * changes to a {@link ByteBuffer} outside of this instance will result in the contents of this BinaryData instance 
         * being updated as well. To safely update the byte array without impacting the BinaryData instance, perform an 
         * array copy first. 
         * </p> 
         * 
         * <p><strong>Create an instance from a List<ByteBuffer></strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromListByteBuffer#List --> 
         * <pre> 
         * final List<ByteBuffer> data = Stream.of("Some ", "data") 
         *     .map(s -> ByteBuffer.wrap(s.getBytes(StandardCharsets.UTF_8))) 
         *     .collect(Collectors.toList()); 
         * BinaryData binaryData = BinaryData.fromListByteBuffer(data); 
         * System.out.println(binaryData); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromListByteBuffer#List --> 
         * 
         * @param data The {@link List} of {@link ByteBuffer} that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the {@link List} of {@link ByteBuffer}. 
         */ 
        public static BinaryData fromListByteBuffer(List<ByteBuffer> data) 
        /** 
         * Creates an instance of {@link BinaryData} by serializing the {@link Object} using the default 
         * {@link JsonSerializer}. 
         * 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to serialize the object. 
         * </p> 
         * <p><strong>Creating an instance from an Object</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromObject#Object --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * BinaryData binaryData = BinaryData.fromObject(data); 
         * 
         * System.out.println(binaryData); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromObject#Object --> 
         * 
         * @param data The object that will be JSON serialized that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the JSON serialized object. 
         * @throws NullPointerException If {@code data} is null. 
         * @see JsonSerializer 
         */ 
        public static BinaryData fromObject(Object data) 
        /** 
         * Creates an instance of {@link BinaryData} by serializing the {@link Object} using the passed 
         * {@link ObjectSerializer}. 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * </p> 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Create an instance from an Object</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromObject#Object-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(data, serializer); 
         * 
         * System.out.println(binaryData.toString()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromObject#Object-ObjectSerializer --> 
         * 
         * @param data The object that will be serialized that {@link BinaryData} will represent. The {@code serializer} 
         * determines how {@code null} data is serialized. 
         * @param serializer The {@link ObjectSerializer} used to serialize object. 
         * @return A {@link BinaryData} representing the serialized object. 
         * @throws NullPointerException If {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public static BinaryData fromObject(Object data, ObjectSerializer serializer) 
        /** 
         * Creates an instance of {@link BinaryData} by serializing the {@link Object} using the default 
         * {@link JsonSerializer}. 
         * 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to serialize the object. 
         * </p> 
         * <p><strong>Creating an instance from an Object</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromObjectAsync#Object --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * Disposable subscriber = BinaryData.fromObjectAsync(data) 
         *     .subscribe(binaryData -> System.out.println(binaryData.toString())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromObjectAsync#Object --> 
         * 
         * @param data The object that will be JSON serialized that {@link BinaryData} will represent. 
         * @return A {@link Mono} of {@link BinaryData} representing the JSON serialized object. 
         * @see JsonSerializer 
         */ 
        public static Mono<BinaryData> fromObjectAsync(Object data) 
        /** 
         * Creates an instance of {@link BinaryData} by serializing the {@link Object} using the passed 
         * {@link ObjectSerializer}. 
         * 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * </p> 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Create an instance from an Object</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromObjectAsync#Object-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * Disposable subscriber = BinaryData.fromObjectAsync(data, serializer) 
         *     .subscribe(binaryData -> System.out.println(binaryData.toString())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromObjectAsync#Object-ObjectSerializer --> 
         * 
         * @param data The object that will be serialized that {@link BinaryData} will represent. The {@code serializer} 
         * determines how {@code null} data is serialized. 
         * @param serializer The {@link ObjectSerializer} used to serialize object. 
         * @return A {@link Mono} of {@link BinaryData} representing the serialized object. 
         * @throws NullPointerException If {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public static Mono<BinaryData> fromObjectAsync(Object data, ObjectSerializer serializer) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link InputStream}. Depending on the type of 
         * inputStream, the BinaryData instance created may or may not allow reading the content more than once. The stream 
         * content is not cached if the stream is not read into a format that requires the content to be fully read into 
         * memory. 
         * <p> 
         * <b>NOTE:</b> The {@link InputStream} is not closed by this function. 
         * </p> 
         * 
         * <p><strong>Create an instance from an InputStream</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromStream#InputStream --> 
         * <pre> 
         * final ByteArrayInputStream inputStream = new ByteArrayInputStream("Some Data".getBytes(StandardCharsets.UTF_8)); 
         * BinaryData binaryData = BinaryData.fromStream(inputStream); 
         * System.out.println(binaryData); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromStream#InputStream --> 
         * 
         * @param inputStream The {@link InputStream} that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the {@link InputStream}. 
         * @throws UncheckedIOException If any error happens while reading the {@link InputStream}. 
         * @throws NullPointerException If {@code inputStream} is null. 
         */ 
        public static BinaryData fromStream(InputStream inputStream) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link InputStream}. Depending on the type of 
         * inputStream, the BinaryData instance created may or may not allow reading the content more than once. The stream 
         * content is not cached if the stream is not read into a format that requires the content to be fully read into 
         * memory. 
         * <p> 
         * <b>NOTE:</b> The {@link InputStream} is not closed by this function. 
         * </p> 
         * 
         * <p><strong>Create an instance from an InputStream</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromStream#InputStream-Long --> 
         * <pre> 
         * byte[] bytes = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * final ByteArrayInputStream inputStream = new ByteArrayInputStream(bytes); 
         * BinaryData binaryData = BinaryData.fromStream(inputStream, (long) bytes.length); 
         * System.out.println(binaryData); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromStream#InputStream-Long --> 
         * 
         * @param inputStream The {@link InputStream} that {@link BinaryData} will represent. 
         * @param length The length of {@code data} in bytes. 
         * @return A {@link BinaryData} representing the {@link InputStream}. 
         * @throws UncheckedIOException If any error happens while reading the {@link InputStream}. 
         * @throws NullPointerException If {@code inputStream} is null. 
         */ 
        public static BinaryData fromStream(InputStream inputStream, Long length) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link InputStream}. 
         * <b>NOTE:</b> The {@link InputStream} is not closed by this function. 
         * 
         * <p><strong>Create an instance from an InputStream</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromStreamAsync#InputStream --> 
         * <pre> 
         * final ByteArrayInputStream inputStream = new ByteArrayInputStream("Some Data".getBytes(StandardCharsets.UTF_8)); 
         * 
         * Mono<BinaryData> binaryDataMono = BinaryData.fromStreamAsync(inputStream); 
         * 
         * Disposable subscriber = binaryDataMono 
         *     .map(binaryData -> { 
         *         System.out.println(binaryData.toString()); 
         *         return true; 
         *     }) 
         *     .subscribe(); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromStreamAsync#InputStream --> 
         * 
         * @param inputStream The {@link InputStream} that {@link BinaryData} will represent. 
         * @return A {@link Mono} of {@link BinaryData} representing the {@link InputStream}. 
         * @throws UncheckedIOException If any error happens while reading the {@link InputStream}. 
         * @throws NullPointerException If {@code inputStream} is null. 
         */ 
        public static Mono<BinaryData> fromStreamAsync(InputStream inputStream) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link InputStream}. 
         * <b>NOTE:</b> The {@link InputStream} is not closed by this function. 
         * 
         * <p><strong>Create an instance from an InputStream</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromStreamAsync#InputStream-Long --> 
         * <pre> 
         * byte[] bytes = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * final ByteArrayInputStream inputStream = new ByteArrayInputStream(bytes); 
         * 
         * Mono<BinaryData> binaryDataMono = BinaryData.fromStreamAsync(inputStream, (long) bytes.length); 
         * 
         * Disposable subscriber = binaryDataMono 
         *     .map(binaryData -> { 
         *         System.out.println(binaryData.toString()); 
         *         return true; 
         *     }) 
         *     .subscribe(); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromStreamAsync#InputStream-Long --> 
         * 
         * @param inputStream The {@link InputStream} that {@link BinaryData} will represent. 
         * @param length The length of {@code data} in bytes. 
         * @return A {@link Mono} of {@link BinaryData} representing the {@link InputStream}. 
         * @throws UncheckedIOException If any error happens while reading the {@link InputStream}. 
         * @throws NullPointerException If {@code inputStream} is null. 
         */ 
        public static Mono<BinaryData> fromStreamAsync(InputStream inputStream, Long length) 
        /** 
         * Creates an instance of {@link BinaryData} from the given {@link String}. 
         * <p> 
         * The {@link String} is converted into bytes using {@link String#getBytes(Charset)} passing 
         * {@link StandardCharsets#UTF_8}. 
         * </p> 
         * <p><strong>Create an instance from a String</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.fromString#String --> 
         * <pre> 
         * final String data = "Some Data"; 
         * // Following will use default character set as StandardCharsets.UTF_8 
         * BinaryData binaryData = BinaryData.fromString(data); 
         * System.out.println(binaryData.toString()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.fromString#String --> 
         * 
         * @param data The {@link String} that {@link BinaryData} will represent. 
         * @return A {@link BinaryData} representing the {@link String}. 
         * @throws NullPointerException If {@code data} is null. 
         */ 
        public static BinaryData fromString(String data) 
        /** 
         * Returns the length of the content, if it is known. The length can be {@code null} if the source did not specify 
         * the length or the length cannot be determined without reading the whole content. 
         * 
         * @return the length of the content, if it is known. 
         */ 
        public Long getLength() 
        /** 
         * Returns a flag indicating whether the content can be repeatedly consumed using all accessors including 
         * {@link #toStream()} and {@link #toFluxByteBuffer()} 
         * 
         * <p> 
         * Replayability does not imply thread-safety. The caller must not use data accessors simultaneously regardless of 
         * what this method returns. 
         * </p> 
         * 
         * <!-- src_embed com.azure.util.BinaryData.replayability --> 
         * <pre> 
         * BinaryData binaryData = binaryDataProducer(); 
         * 
         * if (!binaryData.isReplayable()) { 
         *     binaryData = binaryData.toReplayableBinaryData(); 
         * } 
         * 
         * streamConsumer(binaryData.toStream()); 
         * streamConsumer(binaryData.toStream()); 
         * </pre> 
         * <!-- end com.azure.util.BinaryData.replayability --> 
         * 
         * <!-- src_embed com.azure.util.BinaryData.replayabilityAsync --> 
         * <pre> 
         * Mono.fromCallable(this::binaryDataProducer) 
         *     .flatMap(binaryData -> { 
         *         if (binaryData.isReplayable()) { 
         *             return Mono.just(binaryData); 
         *         } else  { 
         *             return binaryData.toReplayableBinaryDataAsync(); 
         *         } 
         *     }) 
         *     .flatMap(replayableBinaryData -> 
         *         fluxConsumer(replayableBinaryData.toFluxByteBuffer()) 
         *             .then(fluxConsumer(replayableBinaryData.toFluxByteBuffer()))) 
         *     .subscribe(); 
         * </pre> 
         * <!-- end com.azure.util.BinaryData.replayabilityAsync --> 
         * 
         * @return a flag indicating whether the content can be repeatedly consumed using all accessors. 
         */ 
        public boolean isReplayable() 
        /** 
         * Returns a read-only {@link ByteBuffer} representation of this {@link BinaryData}. 
         * <p> 
         * Attempting to mutate the returned {@link ByteBuffer} will throw a {@link ReadOnlyBufferException}. 
         * 
         * <p><strong>Get a read-only ByteBuffer from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.util.BinaryData.toByteBuffer --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * BinaryData binaryData = BinaryData.fromBytes(data); 
         * final byte[] bytes = new byte[data.length]; 
         * binaryData.toByteBuffer().get(bytes, 0, data.length); 
         * System.out.println(new String(bytes)); 
         * </pre> 
         * <!-- end com.azure.util.BinaryData.toByteBuffer --> 
         * 
         * @return A read-only {@link ByteBuffer} representing the {@link BinaryData}. 
         */ 
        public ByteBuffer toByteBuffer() 
        /** 
         * Returns a byte array representation of this {@link BinaryData}. 
         * <p> 
         * This method returns a reference to the underlying byte array. Modifying the contents of the returned byte array 
         * may change the content of this BinaryData instance. If the content source of this BinaryData instance is a file, 
         * an {@link InputStream}, or a {@code Flux<ByteBuffer>} the source is not modified. To safely update the byte 
         * array, it is recommended to make a copy of the contents first. 
         * <p> 
         * If the {@link BinaryData} is larger than the maximum size allowed for a {@code byte[]} this will throw an 
         * {@link IllegalStateException}. 
         * 
         * @return A byte array representing this {@link BinaryData}. 
         * @throws IllegalStateException If the {@link BinaryData} is larger than the maximum size allowed for a 
         * {@code byte[]}. 
         */ 
        public byte[] toBytes() 
        /** 
         * Returns the content of this {@link BinaryData} instance as a flux of {@link ByteBuffer ByteBuffers}. The content 
         * is not read from the underlying data source until the {@link Flux} is subscribed to. 
         * 
         * @return the content of this {@link BinaryData} instance as a flux of {@link ByteBuffer ByteBuffers}. 
         */ 
        public Flux<ByteBuffer> toFluxByteBuffer() 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the default 
         * {@link JsonSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link Class}, should be a non-generic class, for generic classes use 
         * {@link #toObject(TypeReference)}. 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to deserialize the object. 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#Class --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Ensure your classpath have the Serializer to serialize the object which implement implement 
         * // com.azure.core.util.serializer.JsonSerializer interface. 
         * // Or use Azure provided libraries for this. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * BinaryData binaryData = BinaryData.fromObject(data); 
         * 
         * Person person = binaryData.toObject(Person.class); 
         * System.out.println(person.getName()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#Class --> 
         * 
         * @param <T> Type of the deserialized Object. 
         * @param clazz The {@link Class} representing the Object's type. 
         * @return An {@link Object} representing the JSON deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code clazz} is null. 
         * @see JsonSerializer 
         */ 
        public <T> T toObject(Class<T> clazz) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the default 
         * {@link JsonSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link TypeReference}, can either be a generic or non-generic type. If the type is 
         * generic create a sub-type of {@link TypeReference}, if the type is non-generic use 
         * {@link TypeReference#createInstance(Class)}. 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to deserialize the object. 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#TypeReference --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Ensure your classpath have the Serializer to serialize the object which implement implement 
         * // com.azure.core.util.serializer.JsonSerializer interface. 
         * // Or use Azure provided libraries for this. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * BinaryData binaryData = BinaryData.fromObject(data); 
         * 
         * Person person = binaryData.toObject(TypeReference.createInstance(Person.class)); 
         * System.out.println(person.getName()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#TypeReference --> 
         * 
         * <p><strong>Get a generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#TypeReference-generic --> 
         * <pre> 
         * final Person person1 = new Person().setName("John"); 
         * final Person person2 = new Person().setName("Jack"); 
         * 
         * List<Person> personList = new ArrayList<>(); 
         * personList.add(person1); 
         * personList.add(person2); 
         * 
         * // Ensure your classpath have the Serializer to serialize the object which implement implement 
         * // com.azure.core.util.serializer.JsonSerializer interface. 
         * // Or use Azure provided libraries for this. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * 
         * BinaryData binaryData = BinaryData.fromObject(personList); 
         * 
         * List<Person> persons = binaryData.toObject(new TypeReference<List<Person>>() { }); 
         * persons.forEach(person -> System.out.println(person.getName())); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#TypeReference-generic --> 
         * 
         * @param typeReference The {@link TypeReference} representing the Object's type. 
         * @param <T> Type of the deserialized Object. 
         * @return An {@link Object} representing the JSON deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code typeReference} is null. 
         * @see JsonSerializer 
         */ 
        public <T> T toObject(TypeReference<T> typeReference) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the passed 
         * {@link ObjectSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link Class}, should be a non-generic class, for generic classes use 
         * {@link #toObject(TypeReference, ObjectSerializer)}. 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#Class-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(data, serializer); 
         * 
         * Person person = binaryData.toObject(Person.class, serializer); 
         * System.out.println("Name : " + person.getName()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#Class-ObjectSerializer --> 
         * 
         * @param clazz The {@link Class} representing the Object's type. 
         * @param serializer The {@link ObjectSerializer} used to deserialize object. 
         * @param <T> Type of the deserialized Object. 
         * @return An {@link Object} representing the deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code clazz} or {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public <T> T toObject(Class<T> clazz, ObjectSerializer serializer) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the passed 
         * {@link ObjectSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link TypeReference}, can either be a generic or non-generic type. If the type is 
         * generic create a sub-type of {@link TypeReference}, if the type is non-generic use 
         * {@link TypeReference#createInstance(Class)}. 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#TypeReference-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(data, serializer); 
         * 
         * Person person = binaryData.toObject(TypeReference.createInstance(Person.class), serializer); 
         * System.out.println("Name : " + person.getName()); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#TypeReference-ObjectSerializer --> 
         * 
         * <p><strong>Get a generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObject#TypeReference-ObjectSerializer-generic --> 
         * <pre> 
         * final Person person1 = new Person().setName("John"); 
         * final Person person2 = new Person().setName("Jack"); 
         * 
         * List<Person> personList = new ArrayList<>(); 
         * personList.add(person1); 
         * personList.add(person2); 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(personList, serializer); 
         * 
         * // Retains the type of the list when deserializing 
         * List<Person> persons = binaryData.toObject(new TypeReference<List<Person>>() { }, serializer); 
         * persons.forEach(person -> System.out.println("Name : " + person.getName())); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObject#TypeReference-ObjectSerializer-generic --> 
         * 
         * @param typeReference The {@link TypeReference} representing the Object's type. 
         * @param serializer The {@link ObjectSerializer} used to deserialize object. 
         * @param <T> Type of the deserialized Object. 
         * @return An {@link Object} representing the deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code typeReference} or {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public <T> T toObject(TypeReference<T> typeReference, ObjectSerializer serializer) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the default 
         * {@link JsonSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link Class}, should be a non-generic class, for generic classes use 
         * {@link #toObject(TypeReference)}. 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to deserialize the object. 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#Class --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Ensure your classpath have the Serializer to serialize the object which implement implement 
         * // com.azure.core.util.serializer.JsonSerializer interface. 
         * // Or use Azure provided libraries for this. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * BinaryData binaryData = BinaryData.fromObject(data); 
         * 
         * Disposable subscriber = binaryData.toObjectAsync(Person.class) 
         *     .subscribe(person -> System.out.println(person.getName())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#Class --> 
         * 
         * @param clazz The {@link Class} representing the Object's type. 
         * @param <T> Type of the deserialized Object. 
         * @return A {@link Mono} of {@link Object} representing the JSON deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code clazz} is null. 
         * @see JsonSerializer 
         */ 
        public <T> Mono<T> toObjectAsync(Class<T> clazz) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the default 
         * {@link JsonSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link TypeReference}, can either be a generic or non-generic type. If the type is 
         * generic create a sub-type of {@link TypeReference}, if the type is non-generic use 
         * {@link TypeReference#createInstance(Class)}. 
         * <p> 
         * <b>Note:</b> This method first looks for a {@link JsonSerializerProvider} implementation on the classpath. If no 
         * implementation is found, a default Jackson-based implementation will be used to deserialize the object. 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#TypeReference --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Ensure your classpath have the Serializer to serialize the object which implement implement 
         * // com.azure.core.util.serializer.JsonSerializer interface. 
         * // Or use Azure provided libraries for this. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * BinaryData binaryData = BinaryData.fromObject(data); 
         * 
         * Disposable subscriber = binaryData.toObjectAsync(TypeReference.createInstance(Person.class)) 
         *     .subscribe(person -> System.out.println(person.getName())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#TypeReference --> 
         * 
         * <p><strong>Get a generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#TypeReference-generic --> 
         * <pre> 
         * final Person person1 = new Person().setName("John"); 
         * final Person person2 = new Person().setName("Jack"); 
         * 
         * List<Person> personList = new ArrayList<>(); 
         * personList.add(person1); 
         * personList.add(person2); 
         * 
         * BinaryData binaryData = BinaryData.fromObject(personList); 
         * 
         * Disposable subscriber = binaryData.toObjectAsync(new TypeReference<List<Person>>() { }) 
         *     .subscribe(persons -> persons.forEach(person -> System.out.println(person.getName()))); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#TypeReference-generic --> 
         * 
         * @param typeReference The {@link TypeReference} representing the Object's type. 
         * @param <T> Type of the deserialized Object. 
         * @return A {@link Mono} of {@link Object} representing the JSON deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code typeReference} is null. 
         * @see JsonSerializer 
         */ 
        public <T> Mono<T> toObjectAsync(TypeReference<T> typeReference) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the passed 
         * {@link ObjectSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link Class}, should be a non-generic class, for generic classes use 
         * {@link #toObject(TypeReference, ObjectSerializer)}. 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#Class-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(data, serializer); 
         * 
         * Disposable subscriber = binaryData.toObjectAsync(Person.class, serializer) 
         *     .subscribe(person -> System.out.println(person.getName())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#Class-ObjectSerializer --> 
         * 
         * @param clazz The {@link Class} representing the Object's type. 
         * @param serializer The {@link ObjectSerializer} used to deserialize object. 
         * @param <T> Type of the deserialized Object. 
         * @return A {@link Mono} of {@link Object} representing the deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code clazz} or {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public <T> Mono<T> toObjectAsync(Class<T> clazz, ObjectSerializer serializer) 
        /** 
         * Returns an {@link Object} representation of this {@link BinaryData} by deserializing its data using the passed 
         * {@link ObjectSerializer}. Each time this method is called, the content is deserialized and a new instance of type 
         * {@code T} is returned. So, calling this method repeatedly to convert the underlying data source into the same 
         * type is not recommended. 
         * <p> 
         * The type, represented by {@link TypeReference}, can either be a generic or non-generic type. If the type is 
         * generic create a sub-type of {@link TypeReference}, if the type is non-generic use 
         * {@link TypeReference#createInstance(Class)}. 
         * <p> 
         * The passed {@link ObjectSerializer} can either be one of the implementations offered by the Azure SDKs or your 
         * own implementation. 
         * 
         * <p><strong>Azure SDK implementations</strong></p> 
         * <ul> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson" target="_blank">Jackson JSON serializer</a></li> 
         * <li><a href="https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson" target="_blank">GSON JSON serializer</a></li> 
         * </ul> 
         * 
         * <p><strong>Get a non-generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#TypeReference-ObjectSerializer --> 
         * <pre> 
         * final Person data = new Person().setName("John"); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://central.sonatype.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(data, serializer); 
         * 
         * Disposable subscriber = binaryData 
         *     .toObjectAsync(TypeReference.createInstance(Person.class), serializer) 
         *     .subscribe(person -> System.out.println(person.getName())); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#TypeReference-ObjectSerializer --> 
         * 
         * <p><strong>Get a generic Object from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toObjectAsync#TypeReference-ObjectSerializer-generic --> 
         * <pre> 
         * final Person person1 = new Person().setName("John"); 
         * final Person person2 = new Person().setName("Jack"); 
         * 
         * List<Person> personList = new ArrayList<>(); 
         * personList.add(person1); 
         * personList.add(person2); 
         * 
         * // Provide your custom serializer or use Azure provided serializers. 
         * // https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-jackson or 
         * // https://mvnrepository.com/artifact/com.azure/azure-core-serializer-json-gson 
         * 
         * final ObjectSerializer serializer = new MyJsonSerializer(); // Replace this with your Serializer 
         * BinaryData binaryData = BinaryData.fromObject(personList, serializer); 
         * 
         * Disposable subscriber = binaryData 
         *     .toObjectAsync(new TypeReference<List<Person>>() { }, serializer) // retains the generic type information 
         *     .subscribe(persons -> persons.forEach(person -> System.out.println(person.getName()))); 
         * 
         * // So that your program wait for above subscribe to complete. 
         * TimeUnit.SECONDS.sleep(5); 
         * subscriber.dispose(); 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toObjectAsync#TypeReference-ObjectSerializer-generic --> 
         * 
         * @param typeReference The {@link TypeReference} representing the Object's type. 
         * @param serializer The {@link ObjectSerializer} used to deserialize object. 
         * @param <T> Type of the deserialized Object. 
         * @return A {@link Mono} of {@link Object} representing the deserialized {@link BinaryData}. 
         * @throws NullPointerException If {@code typeReference} or {@code serializer} is null. 
         * @see ObjectSerializer 
         * @see JsonSerializer 
         * @see <a href="https://aka.ms/azsdk/java/docs/serialization" target="_blank">More about serialization</a> 
         */ 
        public <T> Mono<T> toObjectAsync(TypeReference<T> typeReference, ObjectSerializer serializer) 
        /** 
         * Converts the {@link BinaryData} into a {@link BinaryData} that is replayable, i.e. content can be consumed 
         * repeatedly using all accessors including {@link #toStream()} and {@link #toFluxByteBuffer()} 
         * 
         * <p> 
         * A {@link BinaryData} that is already replayable is returned as is. Otherwise techniques like marking and 
         * resetting a stream or buffering in memory are employed to assure replayability. 
         * </p> 
         * 
         * <p> 
         * Replayability does not imply thread-safety. The caller must not use data accessors of returned {@link BinaryData} 
         * simultaneously. 
         * </p> 
         * 
         * <!-- src_embed com.azure.util.BinaryData.replayability --> 
         * <pre> 
         * BinaryData binaryData = binaryDataProducer(); 
         * 
         * if (!binaryData.isReplayable()) { 
         *     binaryData = binaryData.toReplayableBinaryData(); 
         * } 
         * 
         * streamConsumer(binaryData.toStream()); 
         * streamConsumer(binaryData.toStream()); 
         * </pre> 
         * <!-- end com.azure.util.BinaryData.replayability --> 
         * 
         * @return Replayable {@link BinaryData}. 
         */ 
        public BinaryData toReplayableBinaryData() 
        /** 
         * Converts the {@link BinaryData} into a {@link BinaryData} that is replayable, i.e. content can be consumed 
         * repeatedly using all accessors including {@link #toStream()} and {@link #toFluxByteBuffer()} 
         * 
         * <p> 
         * A {@link BinaryData} that is already replayable is returned as is. Otherwise techniques like marking and 
         * resetting a stream or buffering in memory are employed to assure replayability. 
         * </p> 
         * 
         * <p> 
         * Replayability does not imply thread-safety. The caller must not use data accessors of returned {@link BinaryData} 
         * simultaneously. 
         * </p> 
         * 
         * <!-- src_embed com.azure.util.BinaryData.replayabilityAsync --> 
         * <pre> 
         * Mono.fromCallable(this::binaryDataProducer) 
         *     .flatMap(binaryData -> { 
         *         if (binaryData.isReplayable()) { 
         *             return Mono.just(binaryData); 
         *         } else  { 
         *             return binaryData.toReplayableBinaryDataAsync(); 
         *         } 
         *     }) 
         *     .flatMap(replayableBinaryData -> 
         *         fluxConsumer(replayableBinaryData.toFluxByteBuffer()) 
         *             .then(fluxConsumer(replayableBinaryData.toFluxByteBuffer()))) 
         *     .subscribe(); 
         * </pre> 
         * <!-- end com.azure.util.BinaryData.replayabilityAsync --> 
         * 
         * @return A {@link Mono} of {@link BinaryData} representing the replayable {@link BinaryData}. 
         */ 
        public Mono<BinaryData> toReplayableBinaryDataAsync() 
        /** 
         * Returns an {@link InputStream} representation of this {@link BinaryData}. 
         * 
         * <p><strong>Get an InputStream from the BinaryData</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.BinaryData.toStream --> 
         * <pre> 
         * final byte[] data = "Some Data".getBytes(StandardCharsets.UTF_8); 
         * BinaryData binaryData = BinaryData.fromStream(new ByteArrayInputStream(data), (long) data.length); 
         * final byte[] bytes = new byte[data.length]; 
         * try (InputStream inputStream = binaryData.toStream()) { 
         *     inputStream.read(bytes, 0, data.length); 
         *     System.out.println(new String(bytes)); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.BinaryData.toStream --> 
         * 
         * @return An {@link InputStream} representing the {@link BinaryData}. 
         */ 
        public InputStream toStream() 
        /** 
         * Returns a {@link String} representation of this {@link BinaryData} by converting its data using the UTF-8 
         * character set. A new instance of String is created each time this method is called. 
         * <p> 
         * If the {@link BinaryData} is larger than the maximum size allowed for a {@link String} this will throw an 
         * {@link IllegalStateException}. 
         * 
         * @return A {@link String} representing this {@link BinaryData}. 
         * @throws IllegalStateException If the {@link BinaryData} is larger than the maximum size allowed for a 
         * {@link String}. 
         */ 
        public String toString() 
        /** 
         * Writes the contents of this {@link BinaryData} to the given {@link OutputStream}. 
         * <p> 
         * This method does not close the {@link OutputStream}. 
         * <p> 
         * The contents of this {@link BinaryData} will be written without buffering. If the underlying data source isn't 
         * {@link #isReplayable()}, after this method is called the {@link BinaryData} will be consumed and can't be read 
         * again. If it needs to be read again, use {@link #toReplayableBinaryData()} to create a replayable copy. 
         * 
         * @param outputStream The {@link OutputStream} to write the contents of this {@link BinaryData} to. 
         * @throws NullPointerException If {@code outputStream} is null. 
         * @throws IOException If an I/O error occurs. 
         */ 
        public void writeTo(OutputStream outputStream) throws IOException
        /** 
         * Writes the contents of this {@link BinaryData} to the given {@link WritableByteChannel}. 
         * <p> 
         * This method does not close the {@link WritableByteChannel}. 
         * <p> 
         * The contents of this {@link BinaryData} will be written without buffering. If the underlying data source isn't 
         * {@link #isReplayable()}, after this method is called the {@link BinaryData} will be consumed and can't be read 
         * again. If it needs to be read again, use {@link #toReplayableBinaryData()} to create a replayable copy. 
         * 
         * @param channel The {@link WritableByteChannel} to write the contents of this {@link BinaryData} to. 
         * @throws NullPointerException If {@code channel} is null. 
         * @throws IOException If an I/O error occurs. 
         */ 
        public void writeTo(WritableByteChannel channel) throws IOException
        /** 
         * Writes the contents of this {@link BinaryData} to the given {@link AsynchronousByteChannel}. 
         * <p> 
         * This method does not close the {@link AsynchronousByteChannel}. 
         * <p> 
         * The contents of this {@link BinaryData} will be written without buffering. If the underlying data source isn't 
         * {@link #isReplayable()}, after this method is called the {@link BinaryData} will be consumed and can't be read 
         * again. If it needs to be read again, use {@link #toReplayableBinaryDataAsync()} to create a replayable copy. 
         * 
         * @param channel The {@link AsynchronousByteChannel} to write the contents of this {@link BinaryData} to. 
         * @return A {@link Mono} the completes once content has been written or had an error writing. 
         * @throws NullPointerException If {@code channel} is null. 
         */ 
        public Mono<Void> writeTo(AsynchronousByteChannel channel) 
        /** 
         * Writes the contents of this {@link BinaryData} to the given {@link JsonWriter}. 
         * <p> 
         * This method does not close or flush the {@link JsonWriter}. 
         * <p> 
         * The contents of this {@link BinaryData} will be written without buffering. If the underlying data source isn't 
         * {@link #isReplayable()}, after this method is called the {@link BinaryData} will be consumed and can't be read 
         * again. If it needs to be read again, use {@link #toReplayableBinaryData()} to create a replayable copy. 
         * 
         * @param jsonWriter The {@link JsonWriter} to write the contents of this {@link BinaryData} to. 
         * @throws NullPointerException If {@code jsonWriter} is null. 
         * @throws IOException If an I/O error occurs during writing. 
         */ 
        public void writeTo(JsonWriter jsonWriter) throws IOException
    } 
    @Fluent
    /** 
     * General configuration options for clients. 
     */ 
    public class ClientOptions { 
        /** 
         * Creates a new instance of {@link ClientOptions}. 
         */ 
        public ClientOptions() 
        /** 
         * Gets the application ID. 
         * 
         * @return The application ID. 
         */ 
        public String getApplicationId() 
        /** 
         * Sets the application ID. 
         * <p> 
         * The {@code applicationId} is used to configure {@link UserAgentPolicy} for telemetry/monitoring purposes. 
         * <p> 
         * See <a href="https://azure.github.io/azure-sdk/general_azurecore.html#telemetry-policy">Azure Core: Telemetry 
         * policy</a> for additional information. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <p>Create ClientOptions with application ID 'myApplicationId'</p> 
         * 
         * <!-- src_embed com.azure.core.util.ClientOptions.setApplicationId#String --> 
         * <pre> 
         * ClientOptions clientOptions = new ClientOptions() 
         *     .setApplicationId("myApplicationId"); 
         * </pre> 
         * <!-- end com.azure.core.util.ClientOptions.setApplicationId#String --> 
         * 
         * @param applicationId The application ID. 
         * @return The updated ClientOptions object. 
         * @throws IllegalArgumentException If {@code applicationId} contains spaces. 
         */ 
        public ClientOptions setApplicationId(String applicationId) 
        /** 
         * Gets the {@link Header Headers}. 
         * 
         * @return The {@link Header Headers}, if headers weren't set previously an empty list is returned. 
         */ 
        public Iterable<Header> getHeaders() 
        /** 
         * Sets the {@link Header Headers}. 
         * <p> 
         * The passed headers are applied to each request sent with the client. 
         * <p> 
         * This overwrites all previously set headers. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <p>Create ClientOptions with Header 'myCustomHeader':'myStaticValue'</p> 
         * 
         * <!-- src_embed com.azure.core.util.ClientOptions.setHeaders#Iterable --> 
         * <pre> 
         * ClientOptions clientOptions = new ClientOptions() 
         *     .setHeaders(Collections.singletonList(new Header("myCustomHeader", "myStaticValue"))); 
         * </pre> 
         * <!-- end com.azure.core.util.ClientOptions.setHeaders#Iterable --> 
         * 
         * @param headers The headers. 
         * @return The updated {@link ClientOptions} object. 
         */ 
        public ClientOptions setHeaders(Iterable<Header> headers) 
        /** 
         * Gets {@link MetricsOptions} 
         * 
         * @return The {@link MetricsOptions} instance, if metrics options weren't set previously, {@code null} is returned. 
         */ 
        public MetricsOptions getMetricsOptions() 
        /** 
         * Sets {@link MetricsOptions} that are applied to each metric reported by the client. 
         * Use metrics options to enable and disable metrics or pass implementation-specific configuration. 
         * 
         * @param metricsOptions instance of {@link MetricsOptions} to set. 
         * @return The updated {@link ClientOptions} object. 
         */ 
        public ClientOptions setMetricsOptions(MetricsOptions metricsOptions) 
        /** 
         * Gets {@link TracingOptions} 
         * 
         * @return The {@link TracingOptions} instance, if tracing options weren't set previously, {@code null} is returned. 
         */ 
        public TracingOptions getTracingOptions() 
        /** 
         * Sets {@link TracingOptions} that are applied to each tracing reported by the client. 
         * Use tracing options to enable and disable tracing or pass implementation-specific configuration. 
         * 
         * @param tracingOptions instance of {@link TracingOptions} to set. 
         * @return The updated {@link ClientOptions} object. 
         */ 
        public ClientOptions setTracingOptions(TracingOptions tracingOptions) 
    } 
    /** 
     * Contains configuration information that is used during construction of client libraries. 
     * 
     * <!-- src_embed com.azure.core.util.Configuration --> 
     * <pre> 
     * Configuration configuration = new ConfigurationBuilder(new SampleSource(properties)) 
     *     .root("azure.sdk") 
     *     .buildSection("client-name"); 
     * 
     * ConfigurationProperty<String> proxyHostnameProperty = ConfigurationPropertyBuilder.ofString("http.proxy.hostname") 
     *     .shared(true) 
     *     .build(); 
     * System.out.println(configuration.get(proxyHostnameProperty)); 
     * </pre> 
     * <!-- end com.azure.core.util.Configuration --> 
     */ 
    public class Configuration implements Cloneable { 
        /** 
         * URL of the proxy for HTTP connections. 
         */ 
        public static final String PROPERTY_HTTP_PROXY = "HTTP_PROXY"; 
        /** 
         * URL of the proxy for HTTPS connections. 
         */ 
        public static final String PROPERTY_HTTPS_PROXY = "HTTPS_PROXY"; 
        /** 
         * Endpoint to connect to when using Azure Active Directory managed service identity (MSI). 
         */ 
        public static final String PROPERTY_IDENTITY_ENDPOINT = "IDENTITY_ENDPOINT"; 
        /** 
         * Header when connecting to Azure Active Directory using managed service identity (MSI). 
         */ 
        public static final String PROPERTY_IDENTITY_HEADER = "IDENTITY_HEADER"; 
        /** 
         * A list of hosts or CIDR to not use proxy HTTP/HTTPS connections through. 
         */ 
        public static final String PROPERTY_NO_PROXY = "NO_PROXY"; 
        /** 
         * Endpoint to connect to when using Azure Active Directory managed service identity (MSI). 
         */ 
        public static final String PROPERTY_MSI_ENDPOINT = "MSI_ENDPOINT"; 
        /** 
         * Secret when connecting to Azure Active Directory using managed service identity (MSI). 
         */ 
        public static final String PROPERTY_MSI_SECRET = "MSI_SECRET"; 
        /** 
         * Subscription id to use when connecting to Azure resources. 
         */ 
        public static final String PROPERTY_AZURE_SUBSCRIPTION_ID = "AZURE_SUBSCRIPTION_ID"; 
        /** 
         * Username to use when performing username/password authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_USERNAME = "AZURE_USERNAME"; 
        /** 
         * Username to use when performing username/password authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_PASSWORD = "AZURE_PASSWORD"; 
        /** 
         * Client id to use when performing service principal authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_CLIENT_ID = "AZURE_CLIENT_ID"; 
        /** 
         * Client secret to use when performing service principal authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_CLIENT_SECRET = "AZURE_CLIENT_SECRET"; 
        /** 
         * Tenant id for the Azure resources. 
         */ 
        public static final String PROPERTY_AZURE_TENANT_ID = "AZURE_TENANT_ID"; 
        /** 
         * Path of a PFX/PEM certificate file to use when performing service principal authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_CLIENT_CERTIFICATE_PATH = "AZURE_CLIENT_CERTIFICATE_PATH"; 
        /** 
         * Password for a PFX/PEM certificate used when performing service principal authentication with Azure. 
         */ 
        public static final String PROPERTY_AZURE_CLIENT_CERTIFICATE_PASSWORD = "AZURE_CLIENT_CERTIFICATE_PASSWORD"; 
        /** 
         * Flag to enable sending the certificate chain in x5c header to support subject name / issuer based authentication. 
         */ 
        public static final String PROPERTY_AZURE_CLIENT_SEND_CERTIFICATE_CHAIN = "AZURE_CLIENT_SEND_CERTIFICATE_CHAIN"; 
        /** 
         * Flag to disable the CP1 client capabilities in Azure Identity Token credentials. 
         */ 
        public static final String PROPERTY_AZURE_IDENTITY_DISABLE_CP1 = "AZURE_IDENTITY_DISABLE_CP1"; 
        /** 
         * URL used by Bridge To Kubernetes to redirect IMDS calls in the development environment. 
         */ 
        public static final String PROPERTY_AZURE_POD_IDENTITY_TOKEN_URL = "AZURE_POD_IDENTITY_TOKEN_URL"; 
        /** 
         * Name of Azure AAD regional authority. 
         */ 
        public static final String PROPERTY_AZURE_REGIONAL_AUTHORITY_NAME = "AZURE_REGIONAL_AUTHORITY_NAME"; 
        /** 
         * Name of the Azure resource group. 
         */ 
        public static final String PROPERTY_AZURE_RESOURCE_GROUP = "AZURE_RESOURCE_GROUP"; 
        /** 
         * Name of the Azure cloud to connect to. 
         */ 
        public static final String PROPERTY_AZURE_CLOUD = "AZURE_CLOUD"; 
        /** 
         * The Azure Active Directory endpoint to connect to. 
         */ 
        public static final String PROPERTY_AZURE_AUTHORITY_HOST = "AZURE_AUTHORITY_HOST"; 
        /** 
         * Disables telemetry collection. 
         */ 
        public static final String PROPERTY_AZURE_TELEMETRY_DISABLED = "AZURE_TELEMETRY_DISABLED"; 
        /** 
         * Enables logging by setting a log level. 
         */ 
        public static final String PROPERTY_AZURE_LOG_LEVEL = "AZURE_LOG_LEVEL"; 
        /** 
         * Enables HTTP request/response logging by setting an HTTP log detail level. 
         */ 
        public static final String PROPERTY_AZURE_HTTP_LOG_DETAIL_LEVEL = "AZURE_HTTP_LOG_DETAIL_LEVEL"; 
        /** 
         * Disables tracing. 
         */ 
        public static final String PROPERTY_AZURE_TRACING_DISABLED = "AZURE_TRACING_DISABLED"; 
        /** 
         * Sets the name of the {@link TracerProvider} implementation that should be used to construct instances of 
         * {@link Tracer}. 
         * <p> 
         * The name must be the full class name, e.g. {@code com.azure.core.tracing.opentelemetry.OpenTelemetryTracerProvider} and not 
         * {@code OpenTelemetryTracerProvider}. 
         * <p> 
         * If the value isn't set or is an empty string the first {@link TracerProvider} resolved by {@link java.util.ServiceLoader} will be 
         * used to create an instance of {@link Tracer}. If the value is set and doesn't match any 
         * {@link TracerProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         * attempting to create an instance of {@link TracerProvider}. 
         */ 
        public static final String PROPERTY_AZURE_TRACING_IMPLEMENTATION = "AZURE_TRACING_IMPLEMENTATION"; 
        /** 
         * Disables metrics. 
         */ 
        public static final String PROPERTY_AZURE_METRICS_DISABLED = "AZURE_METRICS_DISABLED"; 
        /** 
         * Sets the name of the {@link MeterProvider} implementation that should be used to construct instances of 
         * {@link Meter}. 
         * <p> 
         * The name must be the full class name, e.g. {@code com.azure.core.tracing.opentelemetry.OpenTelemetryMeterProvider} and not 
         * {@code OpenTelemetryMeterProvider}. 
         * <p> 
         * If the value isn't set or is an empty string the first {@link MeterProvider} resolved by {@link java.util.ServiceLoader} will be 
         * used to create an instance of {@link Meter}. If the value is set and doesn't match any 
         * {@link MeterProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         * attempting to create an instance of {@link MeterProvider}. 
         */ 
        public static final String PROPERTY_AZURE_METRICS_IMPLEMENTATION = "AZURE_METRICS_IMPLEMENTATION"; 
        /** 
         * Sets the default number of times a request will be retried, if it passes the conditions for retrying, before it 
         * fails. 
         */ 
        public static final String PROPERTY_AZURE_REQUEST_RETRY_COUNT = "AZURE_REQUEST_RETRY_COUNT"; 
        /** 
         * Sets the default timeout, in milliseconds, for a request to connect to the remote host. 
         * <p> 
         * If the configured value is equal to or less than 0 no timeout will be applied. 
         */ 
        public static final String PROPERTY_AZURE_REQUEST_CONNECT_TIMEOUT = "AZURE_REQUEST_CONNECT_TIMEOUT"; 
        /** 
         * Sets the default timeout interval, in milliseconds, allowed between each byte written by a request. 
         * <p> 
         * If the configured value is equal to or less than 0 no timeout will be applied. 
         */ 
        public static final String PROPERTY_AZURE_REQUEST_WRITE_TIMEOUT = "AZURE_REQUEST_WRITE_TIMEOUT"; 
        /** 
         * Sets the default timeout, in milliseconds, for a request to receive a response from the remote host. 
         * <p> 
         * If the configured value is equal to or less than 0 no timeout will be applied. 
         */ 
        public static final String PROPERTY_AZURE_REQUEST_RESPONSE_TIMEOUT = "AZURE_REQUEST_RESPONSE_TIMEOUT"; 
        /** 
         * Sets the default timeout interval, in milliseconds, allowed between each byte read in a response. 
         * <p> 
         * If the configured value is equal to or less than 0 no timeout will be applied. 
         */ 
        public static final String PROPERTY_AZURE_REQUEST_READ_TIMEOUT = "AZURE_REQUEST_READ_TIMEOUT"; 
        /** 
         * Sets the name of the {@link HttpClientProvider} implementation that should be used to construct instances of 
         * {@link HttpClient}. 
         * <p> 
         * The name must be the full class name, ex {@code com.azure.core.http.netty.NettyAsyncHttpClientProvider} and not 
         * {@code NettyAsyncHttpClientProvider}, to disambiguate multiple providers with the same name but from different 
         * packages. 
         * <p> 
         * If the value isn't set or is an empty string the first {@link HttpClientProvider} resolved by {@link java.util.ServiceLoader} will be 
         * used to create an instance of {@link HttpClient}. If the value is set and doesn't match any 
         * {@link HttpClientProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         * attempting to create an instance of {@link HttpClient}. 
         */ 
        public static final String PROPERTY_AZURE_HTTP_CLIENT_IMPLEMENTATION = "AZURE_HTTP_CLIENT_IMPLEMENTATION"; 
        /** 
         * No-op {@link Configuration} object used to opt out of using global configurations when constructing client 
         * libraries. 
         */ 
        public static final Configuration NONE = new NoopConfiguration ( /* Elided */ ) ; 
        /** 
         * Constructs a configuration containing the known Azure properties constants. 
         * 
         * @deprecated Use {@link ConfigurationBuilder} and {@link ConfigurationSource} that allow to provide all properties 
         * before creating configuration and keep it immutable. 
         */ 
        @Deprecated public Configuration() 
        /** 
         * Gets the value of system property or environment variable. Use {@link Configuration#get(ConfigurationProperty)} 
         * overload to get explicit configuration or environment configuration from specific source. 
         * 
         * <p> 
         * This method first checks the values previously loaded from the environment, if the configuration is found there 
         * it will be returned. Otherwise, this will attempt to load the value from the environment. 
         * 
         * @param name Name of the configuration. 
         * @return Value of the configuration if found, otherwise null. 
         */ 
        public String get(String name) 
        /** 
         * Gets property value from all available sources in the following order: 
         * 
         * <ul> 
         *     <li>Explicit configuration from given {@link ConfigurationSource} by property name</li> 
         *     <li>Explicit configuration by property aliases in the order they were provided in {@link ConfigurationProperty}</li> 
         *     <li>Explicit configuration by property name in the shared section (if {@link ConfigurationProperty} is shared)</li> 
         *     <li>Explicit configuration by property aliases in the shared section (if {@link ConfigurationProperty} is shared)</li> 
         *     <li>System property (if set)</li> 
         *     <li>Environment variable (if set)</li> 
         * </ul> 
         * 
         * <p> 
         * Property value is converted to specified type. If property value is missing and not required, default value is returned. 
         * 
         * <!-- src_embed com.azure.core.util.Configuration.get#ConfigurationProperty --> 
         * <pre> 
         * ConfigurationProperty<String> property = ConfigurationPropertyBuilder.ofString("http.proxy.hostname") 
         *     .shared(true) 
         *     .logValue(true) 
         *     .systemPropertyName("http.proxyHost") 
         *     .build(); 
         * 
         * // attempts to get local `azure.sdk.<client-name>.http.proxy.host` property and falls back to 
         * // shared azure.sdk.http.proxy.port 
         * System.out.println(configuration.get(property)); 
         * </pre> 
         * <!-- end com.azure.core.util.Configuration.get#ConfigurationProperty --> 
         * 
         * @param property instance. 
         * @param <T> Type that the configuration is converted to if found. 
         * @return The value of the property if it exists, otherwise the default value of the property. 
         * @throws NullPointerException when property instance is null. 
         * @throws IllegalArgumentException when required property is missing. 
         * @throws RuntimeException when property value conversion (and validation) throws. 
         */ 
        public <T> T get(ConfigurationProperty<T> property) 
        /** 
         * Gets the value of system property or environment variable converted to given primitive {@code T} using 
         * corresponding {@code parse} method on this type. 
         * 
         * Use {@link Configuration#get(ConfigurationProperty)} overload to get explicit configuration or environment 
         * configuration from specific source. 
         * 
         * <p> 
         * This method first checks the values previously loaded from the environment, if the configuration is found there 
         * it will be returned. Otherwise, this will attempt to load the value from the environment. 
         * <p> 
         * If no configuration is found, the {@code defaultValue} is returned. 
         * 
         * <p><b>Following types are supported:</b></p> 
         * <ul> 
         * <li>{@link Byte}</li> 
         * <li>{@link Short}</li> 
         * <li>{@link Integer}</li> 
         * <li>{@link Long}</li> 
         * <li>{@link Float}</li> 
         * <li>{@link Double}</li> 
         * <li>{@link Boolean}</li> 
         * </ul> 
         * 
         * @param name Name of the configuration. 
         * @param defaultValue Value to return if the configuration isn't found. 
         * @param <T> Type that the configuration is converted to if found. 
         * @return The converted configuration if found, otherwise the default value is returned. 
         */ 
        public <T> T get(String name, T defaultValue) 
        /** 
         * Gets the value of system property or environment variable and converts it with the {@code converter}. 
         * <p> 
         * This method first checks the values previously loaded from the environment, if the configuration is found there 
         * it will be returned. Otherwise, this will attempt to load the value from the environment. 
         * <p> 
         * If no configuration is found the {@code converter} won't be called and null will be returned. 
         * 
         * @param name Name of the configuration. 
         * @param converter Converter used to map the configuration to {@code T}. 
         * @param <T> Type that the configuration is converted to if found. 
         * @return The converted configuration if found, otherwise null. 
         */ 
        public <T> T get(String name, Function<String, T> converter) 
        /** 
         * Clones this Configuration object. 
         * 
         * @return A clone of the Configuration object. 
         * @deprecated Use {@link ConfigurationBuilder} and {@link ConfigurationSource} to create configuration. 
         */ 
        @Deprecated public Configuration clone() 
        /** 
         * Determines if the system property or environment variable is defined. 
         * <p> 
         * Use {@link Configuration#contains(ConfigurationProperty)} overload to get explicit configuration or environment 
         * configuration from specific source. 
         * 
         * <p> 
         * This only checks against values previously loaded into the Configuration object, this won't inspect the 
         * environment for containing the value. 
         * 
         * @param name Name of the configuration. 
         * @return True if the configuration exists, otherwise false. 
         */ 
        public boolean contains(String name) 
        /** 
         * Checks if configuration contains the property. If property can be shared between clients, checks this 
         * {@code Configuration} and falls back to shared section. If property has aliases, system property or environment 
         * variable defined, checks them as well. 
         * <p> 
         * Value is not validated. 
         * 
         * @param property instance. 
         * @return true if property is available, false otherwise. 
         */ 
        public boolean contains(ConfigurationProperty<?> property) 
        /** 
         * Gets the global configuration store shared by all client libraries. 
         * 
         * @return The global configuration store. 
         */ 
        public static Configuration getGlobalConfiguration() 
        /** 
         * Adds a configuration with the given value. 
         * <p> 
         * This will overwrite the previous configuration value if it existed. 
         * 
         * @param name Name of the configuration. 
         * @param value Value of the configuration. 
         * @return The updated Configuration object. 
         * @deprecated Use {@link ConfigurationBuilder} and {@link ConfigurationSource} to provide all properties before 
         * creating configuration. 
         */ 
        @Deprecated public Configuration put(String name, String value) 
        /** 
         * Removes the configuration. 
         * <p> 
         * This returns the value of the configuration if it previously existed. 
         * 
         * @param name Name of the configuration. 
         * @return The configuration if it previously existed, otherwise null. 
         * @deprecated Use {@link ConfigurationBuilder} and {@link ConfigurationSource} to provide all properties before 
         * creating configuration. 
         */ 
        @Deprecated public String remove(String name) 
    } 
    @Fluent
    /** 
     * Builds {@link Configuration} with external source. 
     */ 
    public final class ConfigurationBuilder { 
        /** 
         * Creates {@code ConfigurationBuilder}. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationBuilder#putProperty --> 
         * <pre> 
         * configuration = new ConfigurationBuilder() 
         *     .putProperty("azure.sdk.client-name.connection-string", "...") 
         *     .root("azure.sdk") 
         *     .buildSection("client-name"); 
         * 
         * ConfigurationProperty<String> connectionStringProperty = ConfigurationPropertyBuilder.ofString("connection-string") 
         *     .build(); 
         * 
         * System.out.println(configuration.get(connectionStringProperty)); 
         * </pre> 
         * <!-- end com.com.azure.core.util.ConfigurationBuilder#putProperty --> 
         */ 
        public ConfigurationBuilder() 
        /** 
         * Creates {@code ConfigurationBuilder} with configuration source. 
         * 
         * <!-- src_embed com.azure.core.util.Configuration --> 
         * <pre> 
         * Configuration configuration = new ConfigurationBuilder(new SampleSource(properties)) 
         *     .root("azure.sdk") 
         *     .buildSection("client-name"); 
         * 
         * ConfigurationProperty<String> proxyHostnameProperty = ConfigurationPropertyBuilder.ofString("http.proxy.hostname") 
         *     .shared(true) 
         *     .build(); 
         * System.out.println(configuration.get(proxyHostnameProperty)); 
         * </pre> 
         * <!-- end com.com.azure.core.util.Configuration --> 
         * 
         * @param source Custom {@link ConfigurationSource} containing known Azure SDK configuration properties. 
         */ 
        public ConfigurationBuilder(ConfigurationSource source) 
        /** 
         * Creates {@code ConfigurationBuilder} with configuration sources for explicit configuration, system properties and 
         * environment configuration sources. Use this constructor to customize known Azure SDK system properties and 
         * environment variables retrieval. 
         * 
         * @param source Custom {@link ConfigurationSource} containing known Azure SDK configuration properties 
         * @param systemPropertiesConfigurationSource {@link ConfigurationSource} containing known Azure SDK system 
         * properties. 
         * @param environmentConfigurationSource {@link ConfigurationSource} containing known Azure SDK environment 
         * variables. 
         */ 
        public ConfigurationBuilder(ConfigurationSource source, ConfigurationSource systemPropertiesConfigurationSource, ConfigurationSource environmentConfigurationSource) 
        /** 
         * Adds property to the configuration source. In case the source already contains property with the same name, the 
         * value will be overwritten with the new value passed. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationBuilder#putProperty --> 
         * <pre> 
         * configuration = new ConfigurationBuilder() 
         *     .putProperty("azure.sdk.client-name.connection-string", "...") 
         *     .root("azure.sdk") 
         *     .buildSection("client-name"); 
         * 
         * ConfigurationProperty<String> connectionStringProperty = ConfigurationPropertyBuilder.ofString("connection-string") 
         *     .build(); 
         * 
         * System.out.println(configuration.get(connectionStringProperty)); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationBuilder#putProperty --> 
         * 
         * @param name Property name. 
         * @param value Property value. 
         * @return {@code ConfigurationBuilder} instance for chaining. 
         */ 
        public ConfigurationBuilder putProperty(String name, String value) 
        /** 
         * Sets path to root configuration properties where shared Azure SDK properties are defined. When local per-client 
         * property is missing, {@link Configuration} falls back to shared properties. 
         * 
         * <!-- src_embed com.azure.core.util.Configuration --> 
         * <pre> 
         * Configuration configuration = new ConfigurationBuilder(new SampleSource(properties)) 
         *     .root("azure.sdk") 
         *     .buildSection("client-name"); 
         * 
         * ConfigurationProperty<String> proxyHostnameProperty = ConfigurationPropertyBuilder.ofString("http.proxy.hostname") 
         *     .shared(true) 
         *     .build(); 
         * System.out.println(configuration.get(proxyHostnameProperty)); 
         * </pre> 
         * <!-- end com.com.azure.core.util.Configuration --> 
         * 
         * @param rootPath absolute root path, can be {@code null}. 
         * @return {@code ConfigurationBuilder} instance for chaining. 
         */ 
        public ConfigurationBuilder root(String rootPath) 
        /** 
         * Builds root {@link Configuration} section. Use it for shared properties only. To read client-specific 
         * configuration, use {@link ConfigurationBuilder#buildSection(String)} which can read per-client and shared 
         * properties. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationBuilder#build --> 
         * <pre> 
         * // Builds shared Configuration only. 
         * Configuration sharedConfiguration = new ConfigurationBuilder(new SampleSource(properties)) 
         *     .root("azure.sdk") 
         *     .build(); 
         * </pre> 
         * <!-- end com.com.azure.core.util.ConfigurationBuilder#build --> 
         * 
         * @return Root {@link Configuration} with shared properties. 
         */ 
        public Configuration build() 
        /** 
         * Builds {@link Configuration} section that supports retrieving properties from client-specific section with 
         * fallback to root section for properties that can be shared between clients. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationBuilder#buildSection --> 
         * <pre> 
         * // Builds Configuration for <client-name> with fallback to shared properties. 
         * configuration = new ConfigurationBuilder(new SampleSource(properties)) 
         *     .root("azure.sdk") 
         *     .buildSection("client-name"); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationBuilder#buildSection --> 
         * 
         * @param path relative path from {@link ConfigurationBuilder#root(String)} to client section. 
         * @return Client {@link Configuration} capable of reading client-specific and shared properties. 
         */ 
        public Configuration buildSection(String path) 
    } 
    /** 
     * Represents configuration property. 
     * 
     * @param <T> Type of property value. 
     */ 
    public final class ConfigurationProperty<T> { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets property aliases - alternative names property can have. 
         * 
         * @return array of name aliases. 
         */ 
        public Iterable<String> getAliases() 
        /** 
         * Gets converter for property value. 
         * 
         * @return property value converter. 
         */ 
        public Function<String, T> getConverter() 
        /** 
         * Gets property default value to be used when property is missing in the configuration. 
         * 
         * @return default value. 
         */ 
        public T getDefaultValue() 
        /** 
         * Gets name of environment variables this property can be configured with. 
         * 
         * @return environment variable name. 
         */ 
        public String getEnvironmentVariableName() 
        /** 
         * Gets full property name including relative path to it. 
         * 
         * @return property name. 
         */ 
        public String getName() 
        /** 
         * Returns true if property is required, used for validation purposes. 
         * 
         * @return flag indicating if the property is required. 
         */ 
        public boolean isRequired() 
        /** 
         * Returns true if property can be shared between clients and {@link Configuration#get(ConfigurationProperty)} 
         * should look for it in per-client and root sections. 
         * 
         * @return flag indicating if the property is shared. 
         */ 
        public boolean isShared() 
        /** 
         * Gets name of system property this property can be configured with. 
         * 
         * @return system property name. 
         */ 
        public String getSystemPropertyName() 
        /** 
         * Returns property value sanitizer that is used to securely log property value. 
         * 
         * @return function that sanitizes property value. 
         */ 
        public Function<String, String> getValueSanitizer() 
    } 
    /** 
     * Builds configuration property. 
     * 
     * @param <T> The property value type. 
     */ 
    public final class ConfigurationPropertyBuilder<T> { 
        /** 
         * Constructs {@code ConfigurationPropertyBuilder} instance. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationPropertyBuilder --> 
         * <pre> 
         * ConfigurationProperty<SampleEnumProperty> modeProperty = 
         *     new ConfigurationPropertyBuilder<>("mode", SampleEnumProperty::fromString) 
         *         .logValue(true) 
         *         .defaultValue(SampleEnumProperty.MODE_1) 
         *         .build(); 
         * System.out.println(configuration.get(modeProperty)); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationPropertyBuilder --> 
         * 
         * @param name name of the property. 
         * @param converter Converter used to map the configuration to {@code T}. 
         */ 
        public ConfigurationPropertyBuilder(String name, Function<String, T> converter) 
        /** 
         * Sets one or more alias for property. {@link Configuration#get(ConfigurationProperty)} attempts to retrieve 
         * property by name first and only then tries to retrieve properties by alias in the order aliases are provided. 
         * 
         * @param aliases one or more alias for the property name. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> aliases(String... aliases) 
        /** 
         * Sets default property value. {@code null} by default. 
         * 
         * @param defaultValue value to be returned by {@link Configuration#get(ConfigurationProperty)} if the property 
         * isn't found. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> defaultValue(T defaultValue) 
        /** 
         * Sets environment variable name that can represent this property if explicit configuration is not set. 
         * 
         * <p> 
         * When property value is not found by {@code name} or {@code alias}, 
         * {@link Configuration#get(ConfigurationProperty)} falls back to system properties and environment variables. 
         * <p> 
         * When environment variable (or system property) is not set, {@link Configuration#get(ConfigurationProperty)} does 
         * not attempt to read environment configuration. 
         * 
         * @param environmentVariableName environment variable name. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> environmentVariableName(String environmentVariableName) 
        /** 
         * Sets flag indicating if property value can be logged. Default is {@code false}, indicating that property value 
         * should not be logged. When and if retrieving of corresponding configuration property is logged, 
         * {@link Configuration} will use "redacted" string instead of property value. If flag is set to {@code true}, value 
         * is populated on the logs as is. 
         * 
         * @param logValue If set to {@code true}, {@link Configuration#get(ConfigurationProperty)} will log property value, 
         * Otherwise, value is redacted. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> logValue(boolean logValue) 
        /** 
         * Creates {@link ConfigurationPropertyBuilder} configured to log property value and parse value using 
         * {@link Boolean#parseBoolean(String)}. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationPropertyBuilder.ofBoolean --> 
         * <pre> 
         * ConfigurationProperty<Boolean> booleanProperty = ConfigurationPropertyBuilder.ofBoolean("is-enabled") 
         *     .build(); 
         * System.out.println(configuration.get(booleanProperty)); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationPropertyBuilder.ofBoolean --> 
         * 
         * @param name property name. 
         * @return instance of {@link ConfigurationPropertyBuilder}. 
         */ 
        public static ConfigurationPropertyBuilder<Boolean> ofBoolean(String name) 
        /** 
         * Creates {@link ConfigurationPropertyBuilder} configured to log property value and parses value as long number of 
         * milliseconds, proxying  {@link NumberFormatException} exception. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationPropertyBuilder.ofDuration --> 
         * <pre> 
         * ConfigurationProperty<Duration> timeoutProperty = ConfigurationPropertyBuilder.ofDuration("timeout") 
         *     .build(); 
         * System.out.println(configuration.get(timeoutProperty)); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationPropertyBuilder.ofDuration --> 
         * 
         * @param name property name. 
         * @return instance of {@link ConfigurationPropertyBuilder}. 
         */ 
        public static ConfigurationPropertyBuilder<Duration> ofDuration(String name) 
        /** 
         * Creates {@link ConfigurationPropertyBuilder} configured to log property value and parse value using 
         * {@link Integer#valueOf(String)}, proxying {@link NumberFormatException} exception. 
         * 
         * <!-- src_embed com.azure.core.util.ConfigurationPropertyBuilder.ofInteger --> 
         * <pre> 
         * ConfigurationProperty<Integer> integerProperty = ConfigurationPropertyBuilder.ofInteger("retry-count") 
         *     .build(); 
         * System.out.println(configuration.get(integerProperty)); 
         * </pre> 
         * <!-- end com.azure.core.util.ConfigurationPropertyBuilder.ofInteger --> 
         * 
         * @param name property name. 
         * @return instance of {@link ConfigurationPropertyBuilder}. 
         */ 
        public static ConfigurationPropertyBuilder<Integer> ofInteger(String name) 
        /** 
         * Creates default {@link ConfigurationPropertyBuilder}. String property values are redacted in logs by default. If 
         * property value does not contain sensitive information, use {@link ConfigurationPropertyBuilder#logValue} to 
         * enable logging. 
         * 
         * <!-- src_embed com.azure.core.util.Configuration.get#ConfigurationProperty --> 
         * <pre> 
         * ConfigurationProperty<String> property = ConfigurationPropertyBuilder.ofString("http.proxy.hostname") 
         *     .shared(true) 
         *     .logValue(true) 
         *     .systemPropertyName("http.proxyHost") 
         *     .build(); 
         * 
         * // attempts to get local `azure.sdk.<client-name>.http.proxy.host` property and falls back to 
         * // shared azure.sdk.http.proxy.port 
         * System.out.println(configuration.get(property)); 
         * </pre> 
         * <!-- end com.azure.core.util.Configuration.get#ConfigurationProperty --> 
         * 
         * @param name property name. 
         * @return instance of {@link ConfigurationPropertyBuilder}. 
         */ 
        public static ConfigurationPropertyBuilder<String> ofString(String name) 
        /** 
         * Sets flag indicating if property is required. Default is {@code false}, indicating that property is optional. 
         * 
         * @param required If set to {@code true}, {@link Configuration#get(ConfigurationProperty)} will throw when property 
         * is not found. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> required(boolean required) 
        /** 
         * Sets flag indicating that property can be provided in the shared configuration section in addition to 
         * client-specific configuration section. Default is {@code false}, indicating that property can only be provided in 
         * local configuration. 
         * 
         * @param shared If set to {@code true}, {@link Configuration#get(ConfigurationProperty)} will attempt to retrieve 
         * property from local configuration and fall back to shared section, when local property is missing. Otherwise, 
         * only local configuration will be checked. 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> shared(boolean shared) 
        /** 
         * Sets system property name that can represent this property if explicit configuration is not set. 
         * 
         * <p> 
         * When property value is not found by {@code name} or {@code alias}, 
         * {@link Configuration#get(ConfigurationProperty)} falls back to system properties and environment variables. 
         * <p> 
         * When environment variable (or system property) is not set, {@link Configuration#get(ConfigurationProperty)} does 
         * not attempt to read environment configuration. 
         * 
         * @param systemPropertyName one or more environment variable (or system property). 
         * @return the updated ConfigurationPropertyBuilder object. 
         */ 
        public ConfigurationPropertyBuilder<T> systemPropertyName(String systemPropertyName) 
        /** 
         * Builds configuration property instance. 
         * 
         * @return {@link ConfigurationProperty} instance. 
         */ 
        public ConfigurationProperty<T> build() 
    } 
    @FunctionalInterface
    /** 
     * Configuration property source which provides configuration values from a specific place. Samples may include 
     * properties file supported by frameworks or other source. 
     * 
     * Note that environment configuration (environment variables and system properties) are supported by default and 
     * don't need a source implementation. 
     */ 
    public interface ConfigurationSource { 
        /** 
         * Returns all properties (name and value) which names start with given path. 
         * Null (or empty) path indicate that all properties should be returned. 
         * 
         * Example: 
         * <p> 
         * With following configuration properties: 
         * <ul> 
         *   <li>azure.sdk.foo = 1</li> 
         *   <li>azure.sdk.bar.baz = 2</li> 
         * </ul> 
         * 
         * <p> 
         * {@link ConfigurationSource} implementation must the following behavior: 
         * <ul> 
         *       <li>{@code getProperties(null} must return both properties</li> 
         *       <li>{@code getProperties("azure.sdk")} must return both properties</li> 
         *       <li>{@code getProperties("azure.sdk.foo")} must return {"azure.sdk.foo", "1"}</li> 
         *       <li>{@code getProperties("azure.sdk.ba")} must return empty map</li> 
         * </ul> 
         * 
         * @param source property name prefix 
         * @return Map of properties under given path. 
         */ 
        Map<String, String> getProperties(String source) 
    } 
    @Immutable
    /** 
     * {@code Context} offers a means of passing arbitrary data (key-value pairs) to pipeline policies. 
     * Most applications do not need to pass arbitrary data to the pipeline and can pass {@code Context.NONE} or 
     * {@code null}. 
     * <p> 
     * Each context object is immutable. The {@link #addData(Object, Object)} method creates a new 
     * {@code Context} object that refers to its parent, forming a linked list. 
     */ 
    public class Context { 
        /** 
         * Signifies that no data needs to be passed to the pipeline. 
         */ 
        public static final Context NONE ; 
        /** 
         * Constructs a new {@link Context} object. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.context#object-object --> 
         * <pre> 
         * // Create an empty context having no data 
         * Context emptyContext = Context.NONE; 
         * 
         * // OpenTelemetry context can be optionally passed using PARENT_TRACE_CONTEXT_KEY 
         * // when OpenTelemetry context is not provided explicitly, ambient 
         * // io.opentelemetry.context.Context.current() is used 
         * 
         * // Context contextWithSpan = new Context(PARENT_TRACE_CONTEXT_KEY, openTelemetryContext); 
         * </pre> 
         * <!-- end com.azure.core.util.context#object-object --> 
         * 
         * @param key The key with which the specified value should be associated. 
         * @param value The value to be associated with the specified key. 
         * @throws IllegalArgumentException If {@code key} is {@code null}. 
         */ 
        public Context(Object key, Object value) 
        /** 
         * Adds a new immutable {@link Context} object with the specified key-value pair to 
         * the existing {@link Context} chain. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.context.addData#object-object --> 
         * <pre> 
         * // Users can pass parent trace context information and additional metadata to attach to spans created by SDKs 
         * // using the com.azure.core.util.Context object. 
         * final String hostNameValue = "host-name-value"; 
         * final String entityPathValue = "entity-path-value"; 
         * 
         * // TraceContext represents a tracing solution context type - io.opentelemetry.context.Context for OpenTelemetry. 
         * final TraceContext parentContext = TraceContext.root(); 
         * Context parentSpanContext = new Context(PARENT_TRACE_CONTEXT_KEY, parentContext); 
         * 
         * // Add a new key value pair to the existing context object. 
         * Context updatedContext = parentSpanContext.addData(HOST_NAME_KEY, hostNameValue) 
         *     .addData(ENTITY_PATH_KEY, entityPathValue); 
         * 
         * // Both key values found on the same updated context object 
         * System.out.printf("Hostname value: %s%n", updatedContext.getData(HOST_NAME_KEY).get()); 
         * System.out.printf("Entity Path value: %s%n", updatedContext.getData(ENTITY_PATH_KEY).get()); 
         * </pre> 
         * <!-- end com.azure.core.util.context.addData#object-object --> 
         * 
         * @param key The key with which the specified value should be associated. 
         * @param value The value to be associated with the specified key. 
         * @return the new {@link Context} object containing the specified pair added to the set of pairs. 
         * @throws IllegalArgumentException If {@code key} is {@code null}. 
         */ 
        public Context addData(Object key, Object value) 
        /** 
         * Scans the linked-list of {@link Context} objects looking for one with the specified key. 
         * Note that the first key found, i.e. the most recently added, will be returned. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.context.getData#object --> 
         * <pre> 
         * final String key1 = "Key1"; 
         * final String value1 = "first-value"; 
         * 
         * // Create a context object with given key and value 
         * Context context = new Context(key1, value1); 
         * 
         * // Look for the specified key in the returned context object 
         * Optional<Object> optionalObject = context.getData(key1); 
         * if (optionalObject.isPresent()) { 
         *     System.out.printf("Key1 value: %s%n", optionalObject.get()); 
         * } else { 
         *     System.out.println("Key1 does not exist or have data."); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.context.getData#object --> 
         * 
         * @param key The key to search for. 
         * @return The value of the specified key if it exists. 
         * @throws IllegalArgumentException If {@code key} is {@code null}. 
         */ 
        public Optional<Object> getData(Object key) 
        /** 
         * Creates a new immutable {@link Context} object with all the keys and values provided by 
         * the input {@link Map}. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.context.of#map --> 
         * <pre> 
         * final String key1 = "Key1"; 
         * final String value1 = "first-value"; 
         * Map<Object, Object> keyValueMap = new HashMap<>(); 
         * keyValueMap.put(key1, value1); 
         * 
         * // Create a context using the provided key value pair map 
         * Context keyValueContext = Context.of(keyValueMap); 
         * System.out.printf("Key1 value %s%n", keyValueContext.getData(key1).get()); 
         * </pre> 
         * <!-- end com.azure.core.util.context.of#map --> 
         * 
         * @param keyValues The input key value pairs that will be added to this context. 
         * @return Context object containing all the key-value pairs in the input map. 
         * @throws IllegalArgumentException If {@code keyValues} is {@code null} or empty 
         */ 
        public static Context of(Map<Object, Object> keyValues) 
        /** 
         * Scans the linked-list of {@link Context} objects populating a {@link Map} with the values of the context. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.Context.getValues --> 
         * <pre> 
         * final String key1 = "Key1"; 
         * final String value1 = "first-value"; 
         * final String key2 = "Key2"; 
         * final String value2 = "second-value"; 
         * 
         * Context context = new Context(key1, value1) 
         *     .addData(key2, value2); 
         * 
         * Map<Object, Object> contextValues = context.getValues(); 
         * if (contextValues.containsKey(key1)) { 
         *     System.out.printf("Key1 value: %s%n", contextValues.get(key1)); 
         * } else { 
         *     System.out.println("Key1 does not exist."); 
         * } 
         * 
         * if (contextValues.containsKey(key2)) { 
         *     System.out.printf("Key2 value: %s%n", contextValues.get(key2)); 
         * } else { 
         *     System.out.println("Key2 does not exist."); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.Context.getValues --> 
         * 
         * @return A map containing all values of the context linked-list. 
         */ 
        public Map<Object, Object> getValues() 
    } 
    /** 
     * A utility type that can be used to add and retrieve instances commonly used in {@link Context}. 
     */ 
    public final class Contexts { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Returns a version of the {@link Context} reflecting mutations. 
         * @return The version of the {@link Context} reflecting mutations. 
         */ 
        public Context getContext() 
        /** 
         * Creates {@link Contexts} from empty {@link Context}. 
         * @return The {@link Contexts} instance. 
         */ 
        public static Contexts empty() 
        /** 
         * Retrieves request's {@link ProgressReporter} from the {@link Context}. 
         * @return The {@link ProgressReporter}. 
         */ 
        public ProgressReporter getHttpRequestProgressReporter() 
        /** 
         * Adds request's {@link ProgressReporter} instance to the {@link Context}. 
         * @param progressReporter The {@link ProgressReporter} instance. 
         * @return Itself. 
         */ 
        public Contexts setHttpRequestProgressReporter(ProgressReporter progressReporter) 
        /** 
         * Creates {@link Contexts} from supplied {@link Context}. 
         * @param context Existing {@link Context}. Must not be null. 
         * @return The {@link Contexts} instance. 
         * @throws NullPointerException If {@code context} is null. 
         */ 
        public static Contexts with(Context context) 
    } 
    /** 
     * This class contains utility methods useful for building client libraries. 
     */ 
    public final class CoreUtils { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Helper method that safely adds a {@link Runtime#addShutdownHook(Thread)} to the JVM that will run when the JVM is 
         * shutting down. 
         * <p> 
         * {@link Runtime#addShutdownHook(Thread)} checks for security privileges and will throw an exception if the proper 
         * security isn't available. So, if running with a security manager, setting 
         * {@code AZURE_ENABLE_SHUTDOWN_HOOK_WITH_PRIVILEGE} to true will have this method use access controller to add 
         * the shutdown hook with privileged permissions. 
         * <p> 
         * If {@code shutdownThread} is null, no shutdown hook will be added and this method will return null. 
         * 
         * @param shutdownThread The {@link Thread} that will be added as a 
         * {@link Runtime#addShutdownHook(Thread) shutdown hook}. 
         * @return The {@link Thread} that was passed in. 
         */ 
        public static Thread addShutdownHookSafely(Thread shutdownThread) 
        /** 
         * Helper method that safely adds a {@link Runtime#addShutdownHook(Thread)} to the JVM that will close the 
         * {@code executorService} when the JVM is shutting down. 
         * <p> 
         * {@link Runtime#addShutdownHook(Thread)} checks for security privileges and will throw an exception if the proper 
         * security isn't available. So, if running with a security manager, setting 
         * {@code AZURE_ENABLE_SHUTDOWN_HOOK_WITH_PRIVILEGE} to true will have this method use access controller to add 
         * the shutdown hook with privileged permissions. 
         * <p> 
         * If {@code executorService} is null, no shutdown hook will be added and this method will return null. 
         * <p> 
         * The {@code shutdownTimeout} is the amount of time to wait for the {@code executorService} to shutdown. If the 
         * {@code executorService} doesn't shutdown within half the timeout, it will be forcefully shutdown. 
         * 
         * @param executorService The {@link ExecutorService} to shutdown when the JVM is shutting down. 
         * @param shutdownTimeout The amount of time to wait for the {@code executorService} to shutdown. 
         * @return The {@code executorService} that was passed in. 
         * @throws NullPointerException If {@code shutdownTimeout} is null. 
         * @throws IllegalArgumentException If {@code shutdownTimeout} is zero or negative. 
         */ 
        public static ExecutorService addShutdownHookSafely(ExecutorService executorService, Duration shutdownTimeout) 
        /** 
         * Retrieves the application ID from either a {@link ClientOptions} or {@link HttpLogOptions}. 
         * <p> 
         * This method first checks {@code clientOptions} for having an application ID then {@code logOptions}, finally 
         * returning null if neither are set. 
         * <p> 
         * {@code clientOptions} is checked first as {@code logOptions} application ID is deprecated. 
         * 
         * @param clientOptions The {@link ClientOptions}. 
         * @param logOptions The {@link HttpLogOptions}. 
         * @return The application ID from either {@code clientOptions} or {@code logOptions}, if neither are set null. 
         */ 
        public static String getApplicationId(ClientOptions clientOptions, HttpLogOptions logOptions) 
        /** 
         * Turns an array into a string mapping each element to a string and delimits them using a coma. 
         * 
         * @param array Array being formatted to a string. 
         * @param mapper Function that maps each element to a string. 
         * @param <T> Generic representing the type of the array. 
         * @return Array with each element mapped and delimited, otherwise null if the array is empty or null. 
         */ 
        public static <T> String arrayToString(T[] array, Function<T, String> mapper) 
        /** 
         * Attempts to convert a byte stream into the properly encoded String. 
         * <p> 
         * This utility method will attempt to find the encoding for the String in this order. 
         * <ol> 
         *     <li>Find the byte order mark in the byte array.</li> 
         *     <li>Find the {@code charset} in the {@code Content-Type} header.</li> 
         *     <li>Default to {@code UTF-8}.</li> 
         * </ol> 
         * 
         * @param bytes Byte array. 
         * @param contentType {@code Content-Type} header value. 
         * @return A string representation of the byte array encoded to the found encoding. 
         */ 
        public static String bomAwareToString(byte[] bytes, String contentType) 
        /** 
         * Converts a byte array into a hex string. 
         * 
         * <p>The hex string returned uses characters {@code 0123456789abcdef}, if uppercase {@code ABCDEF} is required the 
         * returned string will need to be {@link String#toUpperCase() uppercased}.</p> 
         * 
         * <p>If {@code bytes} is null, null will be returned. If {@code bytes} was an empty array an empty string is 
         * returned.</p> 
         * 
         * @param bytes The byte array to convert into a hex string. 
         * @return A hex string representing the {@code bytes} that were passed, or null if {@code bytes} were null. 
         */ 
        public static String bytesToHexString(byte[] bytes) 
        /** 
         * Creates a copy of the source byte array. 
         * 
         * @param source Array to make copy of 
         * @return A copy of the array, or null if source was null. 
         */ 
        public static byte[] clone(byte[] source) 
        /** 
         * Creates a copy of the source int array. 
         * 
         * @param source Array to make copy of 
         * @return A copy of the array, or null if source was null. 
         */ 
        public static int[] clone(int[] source) 
        /** 
         * Creates a copy of the source array. 
         * 
         * @param source Array being copied. 
         * @param <T> Generic representing the type of the source array. 
         * @return A copy of the array or null if source was null. 
         */ 
        public static <T> T[] clone(T[] source) 
        /** 
         * Creates {@link HttpHeaders} from the provided {@link ClientOptions}. 
         * <p> 
         * If {@code clientOptions} is null or {@link ClientOptions#getHeaders()} doesn't return any {@link Header} values 
         * null will be returned. 
         * 
         * @param clientOptions The {@link ClientOptions} used to create the {@link HttpHeaders}. 
         * @return {@link HttpHeaders} containing the {@link Header} values from {@link ClientOptions#getHeaders()} if 
         * {@code clientOptions} isn't null and contains {@link Header} values, otherwise null. 
         */ 
        public static HttpHeaders createHttpHeadersFromClientOptions(ClientOptions clientOptions) 
        /** 
         * Attempts to load an environment configured default timeout. 
         * <p> 
         * If the environment default timeout isn't configured, {@code defaultTimeout} will be returned. If the environment 
         * default timeout is a string that isn't parseable by {@link Long#parseLong(String)}, {@code defaultTimeout} will 
         * be returned. If the environment default timeout is less than 0, {@link Duration#ZERO} will be returned indicated 
         * that there is no timeout period. 
         * 
         * @param configuration The environment configurations. 
         * @param timeoutPropertyName The default timeout property name. 
         * @param defaultTimeout The fallback timeout to be used. 
         * @param logger A {@link ClientLogger} to log exceptions. 
         * @return Either the environment configured default timeout, {@code defaultTimeoutMillis}, or 0. 
         */ 
        public static Duration getDefaultTimeoutFromEnvironment(Configuration configuration, String timeoutPropertyName, Duration defaultTimeout, ClientLogger logger) 
        /** 
         * Converts a {@link Duration} to a string in ISO-8601 format with support for a day component. 
         * <p> 
         * {@link Duration#toString()} doesn't use a day component, so if the duration is greater than 24 hours it would 
         * return an ISO-8601 duration string like {@code PT48H}. This method returns an ISO-8601 duration string with a day 
         * component if the duration is greater than 24 hours, such as {@code P2D} instead of {@code PT48H}. 
         * 
         * @param duration The {@link Duration} to convert. 
         * @return The {@link Duration} as a string in ISO-8601 format with support for a day component, or null if the 
         * provided {@link Duration} was null. 
         */ 
        public static String durationToStringWithDays(Duration duration) 
        /** 
         * Extracts and combines the generic items from all the pages linked together. 
         * 
         * @param page The paged response from server holding generic items. 
         * @param context Metadata that is passed into the function that fetches the items from the next page. 
         * @param content The function which fetches items from the next page. 
         * @param <T> The type of the item being returned by the paged response. 
         * @return The publisher holding all the generic items combined. 
         * @deprecated Use localized implementation. 
         */ 
        @Deprecated public static <T> Publisher<T> extractAndFetch(PagedResponse<T> page, Context context, BiFunction<String, Context, Publisher<T>> content) 
        /** 
         * Extracts the size from a {@code Content-Range} header. 
         * <p> 
         * The {@code Content-Range} header can take the following forms: 
         * 
         * <ul> 
         * <li>{@code <unit> <start>-<end>/<size>}</li> 
         * <li>{@code <unit> <start>-<end>/}*</li> 
         * <li>{@code <unit> }*{@code /<size>}</li> 
         * </ul> 
         * 
         * If the {@code <size>} is represented by * this method will return -1. 
         * <p> 
         * If {@code contentRange} is null a {@link NullPointerException} will be thrown, if it doesn't contain a size 
         * segment ({@code /<size>} or /*) an {@link IllegalArgumentException} will be thrown. 
         * 
         * @param contentRange The {@code Content-Range} header to extract the size from. 
         * @return The size contained in the {@code Content-Range}, or -1 if the size was *. 
         * @throws NullPointerException If {@code contentRange} is null. 
         * @throws IllegalArgumentException If {@code contentRange} doesn't contain a {@code <size>} segment. 
         * @throws NumberFormatException If the {@code <size>} segment of the {@code contentRange} isn't a valid number. 
         */ 
        public static long extractSizeFromContentRange(String contentRange) 
        /** 
         * Returns the first instance of the given class from an array of Objects. 
         * 
         * @param args Array of objects to search through to find the first instance of the given `clazz` type. 
         * @param clazz The type trying to be found. 
         * @param <T> Generic type 
         * @return The first object of the desired type, otherwise null. 
         */ 
        public static <T> T findFirstOfType(Object[] args, Class<T> clazz) 
        /** 
         * Merges two {@link Context Contexts} into a new {@link Context}. 
         * 
         * @param into Context being merged into. 
         * @param from Context being merged. 
         * @return A new Context that is the merged Contexts. 
         * @throws NullPointerException If either {@code into} or {@code from} is null. 
         */ 
        public static Context mergeContexts(Context into, Context from) 
        /** 
         * Checks if the array is null or empty. 
         * 
         * @param array Array being checked for nullness or emptiness. 
         * @return True if the array is null or empty, false otherwise. 
         */ 
        public static boolean isNullOrEmpty(Object[] array) 
        /** 
         * Checks if the collection is null or empty. 
         * 
         * @param collection Collection being checked for nullness or emptiness. 
         * @return True if the collection is null or empty, false otherwise. 
         */ 
        public static boolean isNullOrEmpty(Collection<?> collection) 
        /** 
         * Checks if the map is null or empty. 
         * 
         * @param map Map being checked for nullness or emptiness. 
         * @return True if the map is null or empty, false otherwise. 
         */ 
        public static boolean isNullOrEmpty(Map<?, ?> map) 
        /** 
         * Checks if the character sequence is null or empty. 
         * 
         * @param charSequence Character sequence being checked for nullness or emptiness. 
         * @return True if the character sequence is null or empty, false otherwise. 
         */ 
        public static boolean isNullOrEmpty(CharSequence charSequence) 
        /** 
         * Processes an authenticate header, such as {@link HttpHeaderName#WWW_AUTHENTICATE} or 
         * {@link HttpHeaderName#PROXY_AUTHENTICATE}, into a list of {@link AuthenticateChallenge}. 
         * <p> 
         * If the {@code authenticateHeader} is null or empty an empty list will be returned. 
         * <p> 
         * This method will parse the authenticate header as plainly as possible, meaning no casing will be changed on the 
         * scheme and no decoding will be done on the parameters. The only processing done is removal of quotes around 
         * parameter values and backslashes escaping values. Ex, {@code "va\"lue"} will be parsed as {@code va"lue}. 
         * <p> 
         * In addition to processing as plainly as possible, this method will not validate the authenticate header, it will 
         * only parse it. Though, if the authenticate header has syntax errors an {@link IllegalStateException} will be 
         * thrown. 
         * <p> 
         * A list of {@link AuthenticateChallenge} will be returned as it is valid for multiple authenticate challenges to 
         * use the same scheme, therefore a map cannot be used as the scheme would be the key and only one challenge would 
         * be stored. 
         * 
         * @param authenticateHeader The authenticate header to be parsed. 
         * @return A list of authenticate challenges. 
         * @throws IllegalArgumentException If the {@code authenticateHeader} has syntax errors. 
         */ 
        public static List<AuthenticateChallenge> parseAuthenticateHeader(String authenticateHeader) 
        /** 
         * Parses a string into an {@link OffsetDateTime}. 
         * <p> 
         * If {@code dateString} is null, null will be returned. 
         * <p> 
         * This method attempts to parse the {@code dateString} using 
         * {@link DateTimeFormatter#parseBest(CharSequence, TemporalQuery[])}. This will use 
         * {@link OffsetDateTime#from(TemporalAccessor)} as the first attempt and will fall back to 
         * {@link LocalDateTime#from(TemporalAccessor)} with setting the offset as {@link ZoneOffset#UTC}. 
         * 
         * @param dateString The string to parse into an {@link OffsetDateTime}. 
         * @return The parsed {@link OffsetDateTime}, or null if {@code dateString} was null. 
         * @throws DateTimeException If the {@code dateString} cannot be parsed by either 
         * {@link OffsetDateTime#from(TemporalAccessor)} or {@link LocalDateTime#from(TemporalAccessor)}. 
         */ 
        public static OffsetDateTime parseBestOffsetDateTime(String dateString) 
        /** 
         * Utility method for parsing query parameters one-by-one without the use of string splitting. 
         * <p> 
         * This method provides an optimization over parsing query parameters with {@link String#split(String)} or a 
         * {@link java.util.regex.Pattern} as it doesn't allocate any arrays to maintain values, instead it parses the query 
         * parameters linearly. 
         * <p> 
         * Query parameter parsing works the following way, {@code key=value} will turn into an immutable {@link Map.Entry} 
         * where the {@link Map.Entry#getKey()} is {@code key} and the {@link Map.Entry#getValue()} is {@code value}. For 
         * query parameters without a value, {@code key=} or just {@code key}, the value will be an empty string. 
         * 
         * @param queryParameters The query parameter string. 
         * @return An {@link Iterator} over the query parameter key-value pairs. 
         */ 
        public static Iterator<Map.Entry<String, String>> parseQueryParameters(String queryParameters) 
        /** 
         * Helper method that returns an immutable {@link Map} of properties defined in {@code propertiesFileName}. 
         * 
         * @param propertiesFileName The file name defining the properties. 
         * @return an immutable {@link Map}. 
         */ 
        public static Map<String, String> getProperties(String propertiesFileName) 
        /** 
         * Creates a type 4 (pseudo randomly generated) UUID. 
         * <p> 
         * The {@link UUID} is generated using a non-cryptographically strong pseudo random number generator. 
         * 
         * @return A randomly generated {@link UUID}. 
         */ 
        public static UUID randomUuid() 
        /** 
         * Calls {@link Future#get(long, TimeUnit)} and returns the value if the {@code future} completes before the timeout 
         * is triggered. If the timeout is triggered, the {@code future} is {@link Future#cancel(boolean) cancelled} 
         * interrupting the execution of the task that the {@link Future} represented. 
         * <p> 
         * If the timeout is {@link Duration#isZero()} or is {@link Duration#isNegative()} then the timeout will be ignored 
         * and an infinite timeout will be used. 
         * 
         * @param <T> The type of value returned by the {@code future}. 
         * @param future The {@link Future} to get the value from. 
         * @param timeout The timeout value. If the timeout is {@link Duration#isZero()} or is {@link Duration#isNegative()} 
         * then the timeout will be ignored and an infinite timeout will be used. 
         * @return The value from the {@code future}. 
         * @throws NullPointerException If {@code future} is null. 
         * @throws CancellationException If the computation was cancelled. 
         * @throws ExecutionException If the computation threw an exception. 
         * @throws InterruptedException If the current thread was interrupted while waiting. 
         * @throws TimeoutException If the wait timed out. 
         * @throws RuntimeException If the {@code future} threw an exception during processing. 
         * @throws Error If the {@code future} threw an {@link Error} during processing. 
         */ 
        public static <T> T getResultWithTimeout(Future<T> future, Duration timeout) throws InterruptedException, ExecutionException, TimeoutException
        /** 
         * Optimized version of {@link String#join(CharSequence, Iterable)} when the {@code values} has a small set of 
         * object. 
         * 
         * @param delimiter Delimiter between the values. 
         * @param values The values to join. 
         * @return The {@code values} joined delimited by the {@code delimiter}. 
         * @throws NullPointerException If {@code delimiter} or {@code values} is null. 
         */ 
        public static String stringJoin(String delimiter, List<String> values) 
    } 
    /** 
     * Wrapper over java.time.OffsetDateTime used for specifying RFC1123 format during serialization and deserialization. 
     */ 
    public final class DateTimeRfc1123 { 
        /** 
         * Creates a new DateTimeRfc1123 object with the specified DateTime. 
         * @param dateTime The DateTime object to wrap. 
         */ 
        public DateTimeRfc1123(OffsetDateTime dateTime) 
        /** 
         * Creates a new DateTimeRfc1123 object with the specified DateTime. 
         * @param formattedString The datetime string in RFC1123 format 
         */ 
        public DateTimeRfc1123(String formattedString) 
        /** 
         * Returns the underlying DateTime. 
         * @return The underlying DateTime. 
         */ 
        public OffsetDateTime getDateTime() 
        @Override public boolean equals(Object obj) 
        @Override public int hashCode() 
        /** 
         * Convert the {@link OffsetDateTime dateTime} to date time string in RFC1123 format. 
         * 
         * @param dateTime The date time in OffsetDateTime format. 
         * @return The date time string in RFC1123 format. 
         * @throws IllegalArgumentException If {@link OffsetDateTime#getDayOfWeek()} or 
         * {@link OffsetDateTime#getDayOfMonth()} is an unknown value. 
         */ 
        public static String toRfc1123String(OffsetDateTime dateTime) 
        @Override public String toString() 
    } 
    /** 
     * This class represents an HTTP ETag. An ETag value could be strong or weak ETag. 
     * For more information, check out <a href="https://en.wikipedia.org/wiki/HTTP_ETag">Wikipedia's HTTP ETag</a>. 
     */ 
    public final class ETag { 
        /** 
         * The asterisk is a special value representing any resource. 
         */ 
        public static final ETag ALL = new ETag ( /* Elided */ ) ; 
        /** 
         * Creates a new instance of {@link ETag}. 
         * 
         * @param eTag The HTTP entity tag string value. 
         */ 
        public ETag(String eTag) 
        @Override public boolean equals(Object o) 
        @Override public int hashCode() 
        @Override public String toString() 
    } 
    /** 
     * Interface for expandable enums. 
     * 
     * @param <T> The type of objects to be listed in the expandable enum. 
     */ 
    public interface ExpandableEnum<T> { 
        /** 
         * Returns the value represented by this expandable enum instance. 
         * 
         * @return The value represented by this expandable enum instance. 
         */ 
        T getValue() 
    } 
    /** 
     * Base implementation for expandable, single string enums. 
     * 
     * @param <T> a specific expandable enum type 
     */ 
    public abstract class ExpandableStringEnum<T extends ExpandableStringEnum<T>> implements ExpandableEnum<String> { 
        /** 
         * Creates a new instance of {@link ExpandableStringEnum} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link ExpandableStringEnum} which doesn't 
         * have a String enum value. 
         * 
         * @deprecated Use the {@link #fromString(String, Class)} factory method. 
         */ 
        @Deprecated public ExpandableStringEnum() 
        @Override public boolean equals(Object obj) 
        /** 
         * Creates an instance of the specific expandable string enum from a String. 
         * 
         * @param name The value to create the instance from. 
         * @param clazz The class of the expandable string enum. 
         * @param <T> the class of the expandable string enum. 
         * @return The expandable string enum instance. 
         * 
         * @throws RuntimeException wrapping implementation class constructor exception (if any is thrown). 
         */ 
        protected static <T extends ExpandableStringEnum<T>> T fromString(String name, Class<T> clazz) 
        @Override public int hashCode() 
        @Override public String toString() 
        @Override public String getValue() 
        /** 
         * Gets a collection of all known values to an expandable string enum type. 
         * 
         * @param clazz the class of the expandable string enum. 
         * @param <T> the class of the expandable string enum. 
         * @return A collection of all known values for the given {@code clazz}. 
         */ 
        protected static <T extends ExpandableStringEnum<T>> Collection<T> values(Class<T> clazz) 
    } 
    /** 
     * Utility type exposing methods to deal with {@link Flux}. 
     */ 
    public final class FluxUtil { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Adds progress reporting to the provided {@link Flux} of {@link ByteBuffer}. 
         * 
         * <p> 
         *     Each {@link ByteBuffer} that's emitted from the {@link Flux} will report {@link ByteBuffer#remaining()}. 
         * </p> 
         * <p> 
         *     When {@link Flux} is resubscribed the progress is reset. If the flux is not replayable, resubscribing 
         *     can result in empty or partial data then progress reporting might not be accurate. 
         * </p> 
         * <p> 
         *     If {@link ProgressReporter} is not provided, i.e. is {@code null}, 
         *     then this method returns unmodified {@link Flux}. 
         * </p> 
         * 
         * @param flux A {@link Flux} to report progress on. 
         * @param progressReporter Optional {@link ProgressReporter}. 
         * @return A {@link Flux} that reports progress, or original {@link Flux} if {@link ProgressReporter} is not 
         * provided. 
         */ 
        public static Flux<ByteBuffer> addProgressReporting(Flux<ByteBuffer> flux, ProgressReporter progressReporter) 
        /** 
         * Gets the content of the provided ByteBuffer as a byte array. This method will create a new byte array even if the 
         * ByteBuffer can have optionally backing array. 
         * 
         * @param byteBuffer the byte buffer 
         * @return the byte array 
         */ 
        public static byte[] byteBufferToArray(ByteBuffer byteBuffer) 
        /** 
         * Collects ByteBuffers returned in a network response into a byte array. 
         * <p> 
         * The {@code headers} are inspected for containing an {@code Content-Length} which determines if a size hinted 
         * collection, {@link #collectBytesInByteBufferStream(Flux, int)}, or default collection, {@link 
         * #collectBytesInByteBufferStream(Flux)}, will be used. 
         * 
         * @param stream A network response ByteBuffer stream. 
         * @param headers The HTTP headers of the response. 
         * @return A Mono which emits the collected network response ByteBuffers. 
         * @throws NullPointerException If {@code headers} is null. 
         * @throws IllegalStateException If the size of the network response is greater than {@link Integer#MAX_VALUE}. 
         */ 
        public static Mono<byte[]> collectBytesFromNetworkResponse(Flux<ByteBuffer> stream, HttpHeaders headers) 
        /** 
         * Collects ByteBuffers emitted by a Flux into a byte array. 
         * 
         * @param stream A stream which emits ByteBuffer instances. 
         * @return A Mono which emits the concatenation of all the ByteBuffer instances given by the source Flux. 
         * @throws IllegalStateException If the combined size of the emitted ByteBuffers is greater than {@link 
         * Integer#MAX_VALUE}. 
         */ 
        public static Mono<byte[]> collectBytesInByteBufferStream(Flux<ByteBuffer> stream) 
        /** 
         * Collects ByteBuffers emitted by a Flux into a byte array. 
         * <p> 
         * Unlike {@link #collectBytesInByteBufferStream(Flux)}, this method accepts a second parameter {@code sizeHint}. 
         * This size hint allows for optimizations when creating the initial buffer to reduce the number of times it needs 
         * to be resized while concatenating emitted ByteBuffers. 
         * 
         * @param stream A stream which emits ByteBuffer instances. 
         * @param sizeHint A hint about the expected stream size. 
         * @return A Mono which emits the concatenation of all the ByteBuffer instances given by the source Flux. 
         * @throws IllegalArgumentException If {@code sizeHint} is equal to or less than {@code 0}. 
         * @throws IllegalStateException If the combined size of the emitted ByteBuffers is greater than {@link 
         * Integer#MAX_VALUE}. 
         */ 
        public static Mono<byte[]> collectBytesInByteBufferStream(Flux<ByteBuffer> stream, int sizeHint) 
        /** 
         * Creates a {@link Flux} that is capable of resuming a download by applying retry logic when an error occurs. 
         * 
         * @param downloadSupplier Supplier of the initial download. 
         * @param onDownloadErrorResume {@link BiFunction} of {@link Throwable} and {@link Long} which is used to resume 
         * downloading when an error occurs. 
         * @param maxRetries The maximum number of times a download can be resumed when an error occurs. 
         * @return A {@link Flux} that downloads reliably. 
         */ 
        public static Flux<ByteBuffer> createRetriableDownloadFlux(Supplier<Flux<ByteBuffer>> downloadSupplier, BiFunction<Throwable, Long, Flux<ByteBuffer>> onDownloadErrorResume, int maxRetries) 
        /** 
         * Creates a {@link Flux} that is capable of resuming a download by applying retry logic when an error occurs. 
         * 
         * @param downloadSupplier Supplier of the initial download. 
         * @param onDownloadErrorResume {@link BiFunction} of {@link Throwable} and {@link Long} which is used to resume 
         * downloading when an error occurs. 
         * @param maxRetries The maximum number of times a download can be resumed when an error occurs. 
         * @param position The initial offset for the download. 
         * @return A {@link Flux} that downloads reliably. 
         */ 
        public static Flux<ByteBuffer> createRetriableDownloadFlux(Supplier<Flux<ByteBuffer>> downloadSupplier, BiFunction<Throwable, Long, Flux<ByteBuffer>> onDownloadErrorResume, int maxRetries, long position) 
        /** 
         * Creates a {@link Flux} that is capable of resuming a download by applying retry logic when an error occurs. 
         * 
         * @param downloadSupplier Supplier of the initial download. 
         * @param onDownloadErrorResume {@link BiFunction} of {@link Throwable} and {@link Long} which is used to resume 
         * downloading when an error occurs. 
         * @param retryOptions The options for retrying. 
         * @param position The initial offset for the download. 
         * @return A {@link Flux} that downloads reliably. 
         */ 
        public static Flux<ByteBuffer> createRetriableDownloadFlux(Supplier<Flux<ByteBuffer>> downloadSupplier, BiFunction<Throwable, Long, Flux<ByteBuffer>> onDownloadErrorResume, RetryOptions retryOptions, long position) 
        /** 
         * Checks if a type is Flux<ByteBuffer>. 
         * 
         * @param entityType the type to check 
         * @return whether the type represents a Flux that emits ByteBuffer 
         */ 
        public static boolean isFluxByteBuffer(Type entityType) 
        /** 
         * This method converts the incoming {@code deferContextual} from {@link reactor.util.context.Context Reactor 
         * Context} to {@link Context Azure Context} and calls the given lambda function with this context and returns a 
         * collection of type {@code T} 
         * <p> 
         * If the reactor context is empty, {@link Context#NONE} will be used to call the lambda function 
         * </p> 
         * 
         * <p><strong>Code samples</strong></p> 
         * <!-- src_embed com.azure.core.implementation.util.FluxUtil.fluxContext --> 
         * <pre> 
         * String prefix = "Hello, "; 
         * Flux<String> response = FluxUtil 
         *     .fluxContext(context -> serviceCallReturnsCollection(prefix, context)); 
         * </pre> 
         * <!-- end com.azure.core.implementation.util.FluxUtil.fluxContext --> 
         * 
         * @param serviceCall The lambda function that makes the service call into which the context will be passed 
         * @param <T> The type of response returned from the service call 
         * @return The response from service call 
         */ 
        public static <T> Flux<T> fluxContext(Function<Context, Flux<T>> serviceCall) 
        /** 
         * Propagates a {@link RuntimeException} through the error channel of {@link Flux}. 
         * 
         * @param logger The {@link ClientLogger} to log the exception. 
         * @param ex The {@link RuntimeException}. 
         * @param <T> The return type. 
         * @return A {@link Flux} that terminates with error wrapping the {@link RuntimeException}. 
         */ 
        public static <T> Flux<T> fluxError(ClientLogger logger, RuntimeException ex) 
        /** 
         * Propagates a {@link RuntimeException} through the error channel of {@link Mono}. 
         * 
         * @param logger The {@link ClientLogger} to log the exception. 
         * @param ex The {@link RuntimeException}. 
         * @param <T> The return type. 
         * @return A {@link Mono} that terminates with error wrapping the {@link RuntimeException}. 
         */ 
        public static <T> Mono<T> monoError(ClientLogger logger, RuntimeException ex) 
        /** 
         * Propagates a {@link RuntimeException} through the error channel of {@link Mono}. 
         * 
         * @param logBuilder The {@link LoggingEventBuilder} with context to log the exception. 
         * @param ex The {@link RuntimeException}. 
         * @param <T> The return type. 
         * @return A {@link Mono} that terminates with error wrapping the {@link RuntimeException}. 
         */ 
        public static <T> Mono<T> monoError(LoggingEventBuilder logBuilder, RuntimeException ex) 
        /** 
         * Propagates a {@link RuntimeException} through the error channel of {@link PagedFlux}. 
         * 
         * @param logger The {@link ClientLogger} to log the exception. 
         * @param ex The {@link RuntimeException}. 
         * @param <T> The return type. 
         * @return A {@link PagedFlux} that terminates with error wrapping the {@link RuntimeException}. 
         */ 
        public static <T> PagedFlux<T> pagedFluxError(ClientLogger logger, RuntimeException ex) 
        /** 
         * Creates a {@link Flux} from an {@link AsynchronousFileChannel} which reads the entire file. 
         * 
         * @param fileChannel The file channel. 
         * @return The AsyncInputStream. 
         */ 
        public static Flux<ByteBuffer> readFile(AsynchronousFileChannel fileChannel) 
        /** 
         * Creates a {@link Flux} from an {@link AsynchronousFileChannel} which reads part of a file. 
         * 
         * @param fileChannel The file channel. 
         * @param offset The offset in the file to begin reading. 
         * @param length The number of bytes to read from the file. 
         * @return the Flux. 
         */ 
        public static Flux<ByteBuffer> readFile(AsynchronousFileChannel fileChannel, long offset, long length) 
        /** 
         * Creates a {@link Flux} from an {@link AsynchronousFileChannel} which reads part of a file into chunks of the 
         * given size. 
         * 
         * @param fileChannel The file channel. 
         * @param chunkSize the size of file chunks to read. 
         * @param offset The offset in the file to begin reading. 
         * @param length The number of bytes to read from the file. 
         * @return the Flux. 
         */ 
        public static Flux<ByteBuffer> readFile(AsynchronousFileChannel fileChannel, int chunkSize, long offset, long length) 
        /** 
         * Converts an {@link InputStream} into a {@link Flux} of {@link ByteBuffer} using a chunk size of 4096. 
         * <p> 
         * Given that {@link InputStream} is not guaranteed to be replayable the returned {@link Flux} should be considered 
         * non-replayable as well. 
         * <p> 
         * If the passed {@link InputStream} is {@code null} {@link Flux#empty()} will be returned. 
         * 
         * @param inputStream The {@link InputStream} to convert into a {@link Flux}. 
         * @return A {@link Flux} of {@link ByteBuffer ByteBuffers} that contains the contents of the stream. 
         */ 
        public static Flux<ByteBuffer> toFluxByteBuffer(InputStream inputStream) 
        /** 
         * Converts an {@link InputStream} into a {@link Flux} of {@link ByteBuffer}. 
         * <p> 
         * Given that {@link InputStream} is not guaranteed to be replayable the returned {@link Flux} should be considered 
         * non-replayable as well. 
         * <p> 
         * If the passed {@link InputStream} is {@code null} {@link Flux#empty()} will be returned. 
         * 
         * @param inputStream The {@link InputStream} to convert into a {@link Flux}. 
         * @param chunkSize The requested size for each {@link ByteBuffer}. 
         * @return A {@link Flux} of {@link ByteBuffer ByteBuffers} that contains the contents of the stream. 
         * @throws IllegalArgumentException If {@code chunkSize} is less than or equal to {@code 0}. 
         */ 
        public static Flux<ByteBuffer> toFluxByteBuffer(InputStream inputStream, int chunkSize) 
        /** 
         * Converts the incoming content to Mono. 
         * 
         * @param <T> The type of the Response, which will be returned in the Mono. 
         * @param response whose {@link Response#getValue() value} is to be converted 
         * @return The converted {@link Mono} 
         */ 
        public static <T> Mono<T> toMono(Response<T> response) 
        /** 
         * Converts an Azure context to Reactor context. If the Azure context is {@code null} or empty, {@link 
         * reactor.util.context.Context#empty()} will be returned. 
         * 
         * @param context The Azure context. 
         * @return The Reactor context. 
         */ 
        public static reactor.util.context.Context toReactorContext(Context context) 
        /** 
         * This method converts the incoming {@code deferContextual} from {@link reactor.util.context.Context Reactor 
         * Context} to {@link Context Azure Context} and calls the given lambda function with this context and returns a 
         * single entity of type {@code T} 
         * <p> 
         * If the reactor context is empty, {@link Context#NONE} will be used to call the lambda function 
         * </p> 
         * 
         * <p><strong>Code samples</strong></p> 
         * <!-- src_embed com.azure.core.implementation.util.FluxUtil.withContext --> 
         * <pre> 
         * String prefix = "Hello, "; 
         * Mono<String> response = FluxUtil 
         *     .withContext(context -> serviceCallReturnsSingle(prefix, context)); 
         * </pre> 
         * <!-- end com.azure.core.implementation.util.FluxUtil.withContext --> 
         * 
         * @param serviceCall The lambda function that makes the service call into which azure context will be passed 
         * @param <T> The type of response returned from the service call 
         * @return The response from service call 
         */ 
        public static <T> Mono<T> withContext(Function<Context, Mono<T>> serviceCall) 
        /** 
         * This method converts the incoming {@code deferContextual} from {@link reactor.util.context.Context Reactor 
         * Context} to {@link Context Azure Context}, adds the specified context attributes and calls the given lambda 
         * function with this context and returns a single entity of type {@code T} 
         * <p> 
         * If the reactor context is empty, {@link Context#NONE} will be used to call the lambda function 
         * </p> 
         * 
         * @param serviceCall serviceCall The lambda function that makes the service call into which azure context will be 
         * passed 
         * @param contextAttributes The map of attributes sent by the calling method to be set on {@link Context}. 
         * @param <T> The type of response returned from the service call 
         * @return The response from service call 
         */ 
        public static <T> Mono<T> withContext(Function<Context, Mono<T>> serviceCall, Map<String, String> contextAttributes) 
        /** 
         * Writes the {@link ByteBuffer ByteBuffers} emitted by a {@link Flux} of {@link ByteBuffer} to an {@link 
         * AsynchronousFileChannel}. 
         * <p> 
         * The {@code outFile} is not closed by this call, closing of the {@code outFile} is managed by the caller. 
         * <p> 
         * The response {@link Mono} will emit an error if {@code content} or {@code outFile} are null. Additionally, an 
         * error will be emitted if the {@code outFile} wasn't opened with the proper open options, such as {@link 
         * StandardOpenOption#WRITE}. 
         * 
         * @param content The {@link Flux} of {@link ByteBuffer} content. 
         * @param outFile The {@link AsynchronousFileChannel}. 
         * @return A {@link Mono} which emits a completion status once the {@link Flux} has been written to the {@link 
         * AsynchronousFileChannel}. 
         * @throws NullPointerException When {@code content} is null. 
         * @throws NullPointerException When {@code outFile} is null. 
         */ 
        public static Mono<Void> writeFile(Flux<ByteBuffer> content, AsynchronousFileChannel outFile) 
        /** 
         * Writes the {@link ByteBuffer ByteBuffers} emitted by a {@link Flux} of {@link ByteBuffer} to an {@link 
         * AsynchronousFileChannel} starting at the given {@code position} in the file. 
         * <p> 
         * The {@code outFile} is not closed by this call, closing of the {@code outFile} is managed by the caller. 
         * <p> 
         * The response {@link Mono} will emit an error if {@code content} or {@code outFile} are null or {@code position} 
         * is less than 0. Additionally, an error will be emitted if the {@code outFile} wasn't opened with the proper open 
         * options, such as {@link StandardOpenOption#WRITE}. 
         * 
         * @param content The {@link Flux} of {@link ByteBuffer} content. 
         * @param outFile The {@link AsynchronousFileChannel}. 
         * @param position The position in the file to begin writing the {@code content}. 
         * @return A {@link Mono} which emits a completion status once the {@link Flux} has been written to the {@link 
         * AsynchronousFileChannel}. 
         * @throws NullPointerException When {@code content} is null. 
         * @throws NullPointerException When {@code outFile} is null. 
         * @throws IllegalArgumentException When {@code position} is negative. 
         */ 
        public static Mono<Void> writeFile(Flux<ByteBuffer> content, AsynchronousFileChannel outFile, long position) 
        /** 
         * Writes the {@link ByteBuffer ByteBuffers} emitted by a {@link Flux} of {@link ByteBuffer} to an {@link 
         * AsynchronousByteChannel}. 
         * <p> 
         * The {@code channel} is not closed by this call, closing of the {@code channel} is managed by the caller. 
         * <p> 
         * The response {@link Mono} will emit an error if {@code content} or {@code channel} are null. 
         * 
         * @param content The {@link Flux} of {@link ByteBuffer} content. 
         * @param channel The {@link AsynchronousByteChannel}. 
         * @return A {@link Mono} which emits a completion status once the {@link Flux} has been written to the {@link 
         * AsynchronousByteChannel}. 
         * @throws NullPointerException When {@code content} is null. 
         * @throws NullPointerException When {@code channel} is null. 
         */ 
        public static Mono<Void> writeToAsynchronousByteChannel(Flux<ByteBuffer> content, AsynchronousByteChannel channel) 
        /** 
         * Writes the {@link ByteBuffer ByteBuffers} emitted by a {@link Flux} of {@link ByteBuffer} to an {@link 
         * OutputStream}. 
         * <p> 
         * The {@code stream} is not closed by this call, closing of the {@code stream} is managed by the caller. 
         * <p> 
         * The response {@link Mono} will emit an error if {@code content} or {@code stream} are null. Additionally, an 
         * error will be emitted if an exception occurs while writing the {@code content} to the {@code stream}. 
         * 
         * @param content The {@link Flux} of {@link ByteBuffer} content. 
         * @param stream The {@link OutputStream} being written into. 
         * @return A {@link Mono} which emits a completion status once the {@link Flux} has been written to the {@link 
         * OutputStream}, or an error status if writing fails. 
         */ 
        public static Mono<Void> writeToOutputStream(Flux<ByteBuffer> content, OutputStream stream) 
        /** 
         * Writes the {@link ByteBuffer ByteBuffers} emitted by a {@link Flux} of {@link ByteBuffer} to an {@link 
         * WritableByteChannel}. 
         * <p> 
         * The {@code channel} is not closed by this call, closing of the {@code channel} is managed by the caller. 
         * <p> 
         * The response {@link Mono} will emit an error if {@code content} or {@code channel} are null. 
         * 
         * @param content The {@link Flux} of {@link ByteBuffer} content. 
         * @param channel The {@link WritableByteChannel}. 
         * @return A {@link Mono} which emits a completion status once the {@link Flux} has been written to the {@link 
         * WritableByteChannel}. 
         * @throws NullPointerException When {@code content} is null. 
         * @throws NullPointerException When {@code channel} is null. 
         */ 
        public static Mono<Void> writeToWritableByteChannel(Flux<ByteBuffer> content, WritableByteChannel channel) 
    } 
    /** 
     * Represents a single header to be set on a request. 
     * <p> 
     * If multiple header values are added to a request with the same name (case-insensitive), then the values will be 
     * appended at the end of the same {@link Header} with commas separating them. 
     */ 
    public class Header { 
        /** 
         * Create a Header instance using the provided name and value. 
         * 
         * @param name the name of the header. 
         * @param value the value of the header. 
         * @throws NullPointerException if {@code name} is null. 
         */ 
        public Header(String name, String value) 
        /** 
         * Create a Header instance using the provided name and values. 
         * 
         * @param name the name of the header. 
         * @param values the values of the header. 
         * @throws NullPointerException if {@code name} is null. 
         */ 
        public Header(String name, String... values) 
        /** 
         * Create a Header instance using the provided name and values. 
         * 
         * @param name the name of the header. 
         * @param values the values of the header. 
         * @throws NullPointerException if {@code name} is null. 
         */ 
        public Header(String name, List<String> values) 
        /** 
         * Add a new value to the end of the Header. 
         * 
         * @param value the value to add 
         */ 
        public void addValue(String value) 
        /** 
         * Gets the header name. 
         * 
         * @return the name of this {@link Header} 
         */ 
        public String getName() 
        /** 
         * Gets the String representation of the header. 
         * 
         * @return the String representation of this Header. 
         */ 
        @Override public String toString() 
        /** 
         * Gets the combined, comma-separated value of this {@link Header}, taking into account all values provided. 
         * 
         * @return the value of this Header 
         */ 
        public String getValue() 
        /** 
         * Gets the comma separated value as an array. Changes made to this array will not be reflected in the headers. 
         * 
         * @return the values of this {@link Header} that are separated by a comma 
         */ 
        public String[] getValues() 
        /** 
         * Returns all values associated with this header, represented as an unmodifiable list of strings. 
         * 
         * @return An unmodifiable list containing all values associated with this header. 
         */ 
        public List<String> getValuesList() 
    } 
    @Fluent
    /** 
     * General configuration options for {@link HttpClient HttpClients}. 
     * <p> 
     * {@link HttpClient} implementations may not support all configuration options in this class. 
     */ 
    public final class HttpClientOptions extends ClientOptions { 
        /** 
         * Creates a new instance of {@link HttpClientOptions}. 
         */ 
        public HttpClientOptions() 
        @Override public HttpClientOptions setApplicationId(String applicationId) 
        /** 
         * Gets the configuration store that the {@link HttpClient} will use. 
         * 
         * @return The configuration store to use. 
         */ 
        public Configuration getConfiguration() 
        /** 
         * Sets the configuration store that the {@link HttpClient} will use. 
         * 
         * @param configuration The configuration store to use. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setConfiguration(Configuration configuration) 
        /** 
         * Gets the duration of time before an idle connection is closed. 
         * <p> 
         * The default connection idle timeout is 60 seconds. 
         * 
         * @return The connection idle timeout duration. 
         */ 
        public Duration getConnectionIdleTimeout() 
        /** 
         * Sets the duration of time before an idle connection. 
         * <p> 
         * The connection idle timeout begins once the connection has completed its last network request. Every time the 
         * connection is used the idle timeout will reset. 
         * <p> 
         * If {@code connectionIdleTimeout} is null a 60-second timeout will be used, if it is a {@link Duration} less than 
         * or equal to zero then no timeout period will be applied. When applying the timeout the greatest of one 
         * millisecond and the value of {@code connectionIdleTimeout} will be used. 
         * <p> 
         * The default connection idle timeout is 60 seconds. 
         * 
         * @param connectionIdleTimeout The connection idle timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setConnectionIdleTimeout(Duration connectionIdleTimeout) 
        /** 
         * Gets the connection timeout for a request to be sent. 
         * <p> 
         * The connection timeout begins once the request attempts to connect to the remote host and finishes when the 
         * connection is resolved. 
         * <p> 
         * If {@code connectTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_CONNECT_TIMEOUT} or a 
         * 10-second timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout will be 
         * applied. When applying the timeout the greatest of one millisecond and the value of {@code connectTimeout} will 
         * be used. 
         * <p> 
         * The default connection timeout is 10 seconds. 
         * 
         * @return The connection timeout of a request to be sent. 
         */ 
        public Duration getConnectTimeout() 
        /** 
         * Sets the connection timeout for a request to be sent. 
         * <p> 
         * The connection timeout begins once the request attempts to connect to the remote host and finishes when the 
         * connection is resolved. 
         * <p> 
         * If {@code connectTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_CONNECT_TIMEOUT} or a 
         * 10-second timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout will be 
         * applied. When applying the timeout the greatest of one millisecond and the value of {@code connectTimeout} will 
         * be used. 
         * <p> 
         * The default connection timeout is 10 seconds. 
         * 
         * @param connectTimeout Connect timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setConnectTimeout(Duration connectTimeout) 
        @Override public HttpClientOptions setHeaders(Iterable<Header> headers) 
        /** 
         * Gets type of the {@link HttpClientProvider} implementation that should be used to construct an instance of 
         * {@link HttpClient}. 
         * 
         * @return The {@link HttpClientProvider} implementation used to create an instance of {@link HttpClient}. 
         */ 
        public Class<? extends HttpClientProvider> getHttpClientProvider() 
        /** 
         * Sets the type of the {@link HttpClientProvider} implementation that should be used to construct an instance of 
         * {@link HttpClient}. 
         * 
         * If the value isn't set or is an empty string the first {@link HttpClientProvider} resolved by {@link java.util.ServiceLoader} will 
         * be used to create an instance of {@link HttpClient}. If the value is set and doesn't match any 
         * {@link HttpClientProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         * attempting to create an instance of {@link HttpClient}. 
         * 
         * @param httpClientProvider The {@link HttpClientProvider} implementation used to create an instance of 
         * {@link HttpClient}. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setHttpClientProvider(Class<? extends HttpClientProvider> httpClientProvider) 
        /** 
         * Gets the maximum connection pool size used by the underlying HTTP client. 
         * <p> 
         * Modifying the maximum connection pool size may have effects on the performance of an application. Increasing the 
         * maximum connection pool will result in more connections being available for an application but may result in more 
         * contention for network resources. It is recommended to perform performance analysis on different maximum 
         * connection pool sizes to find the right configuration for an application. 
         * <p> 
         * This maximum connection pool size is not a global configuration but an instance level configuration for each 
         * {@link HttpClient} created using this {@link HttpClientOptions}. 
         * <p> 
         * The default maximum connection pool size is determined by the underlying HTTP client. Setting the maximum 
         * connection pool size to null resets the configuration to use the default determined by the underlying HTTP 
         * client. 
         * 
         * @return The maximum connection pool size. 
         */ 
        public Integer getMaximumConnectionPoolSize() 
        /** 
         * Sets the maximum connection pool size used by the underlying HTTP client. 
         * <p> 
         * Modifying the maximum connection pool size may have effects on the performance of an application. Increasing the 
         * maximum connection pool will result in more connections being available for an application but may result in more 
         * contention for network resources. It is recommended to perform performance analysis on different maximum 
         * connection pool sizes to find the right configuration for an application. 
         * <p> 
         * This maximum connection pool size is not a global configuration but an instance level configuration for each 
         * {@link HttpClient} created using this {@link HttpClientOptions}. 
         * <p> 
         * The default maximum connection pool size is determined by the underlying HTTP client. Setting the maximum 
         * connection pool size to null resets the configuration to use the default determined by the underlying HTTP 
         * client. 
         * 
         * @param maximumConnectionPoolSize The maximum connection pool size. 
         * @return The updated HttpClientOptions object. 
         * @throws IllegalArgumentException If {@code maximumConnectionPoolSize} is not null and is less than {@code 1}. 
         */ 
        public HttpClientOptions setMaximumConnectionPoolSize(Integer maximumConnectionPoolSize) 
        /** 
         * Gets the {@link ProxyOptions proxy options} that the {@link HttpClient} will use. 
         * 
         * @return The proxy options to use. 
         */ 
        public ProxyOptions getProxyOptions() 
        /** 
         * Sets the {@link ProxyOptions proxy options} that the {@link HttpClient} will use. 
         * 
         * @param proxyOptions The proxy options to use. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setProxyOptions(ProxyOptions proxyOptions) 
        /** 
         * Gets the read timeout duration used when reading the server response. 
         * <p> 
         * The default read timeout is 60 seconds. 
         * 
         * @return The read timeout duration. 
         */ 
        public Duration getReadTimeout() 
        /** 
         * Sets the read timeout duration used when reading the server response. 
         * <p> 
         * The read timeout begins once the first response read is triggered after the server response is received. This 
         * timeout triggers periodically but won't fire its operation if another read operation has completed between when 
         * the timeout is triggered and completes. 
         * <p> 
         * If {@code readTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_READ_TIMEOUT} or a 60-second 
         * timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout period will be 
         * applied to response read. When applying the timeout the greatest of one millisecond and the value of 
         * {@code readTimeout} will be used. 
         * <p> 
         * The default read timeout is 60 seconds. 
         * 
         * @param readTimeout Read timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions readTimeout(Duration readTimeout) 
        /** 
         * Sets the read timeout duration used when reading the server response. 
         * <p> 
         * The read timeout begins once the first response read is triggered after the server response is received. This 
         * timeout triggers periodically but won't fire its operation if another read operation has completed between when 
         * the timeout is triggered and completes. 
         * <p> 
         * If {@code readTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_READ_TIMEOUT} or a 60-second 
         * timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout period will be 
         * applied to response read. When applying the timeout the greatest of one millisecond and the value of 
         * {@code readTimeout} will be used. 
         * <p> 
         * The default read timeout is 60 seconds. 
         * 
         * @param readTimeout Read timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setReadTimeout(Duration readTimeout) 
        /** 
         * Gets the response timeout duration used when waiting for a server to reply. 
         * <p> 
         * The response timeout begins once the request write completes and finishes once the first response read is 
         * triggered when the server response is received. 
         * <p> 
         * If {@code responseTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_RESPONSE_TIMEOUT} or a 
         * 60-second timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout will be 
         * applied to the response. When applying the timeout the greatest of one millisecond and the value of 
         * {@code responseTimeout} will be used. 
         * <p> 
         * The default response timeout is 60 seconds. 
         * 
         * @return The response timeout duration. 
         */ 
        public Duration getResponseTimeout() 
        /** 
         * Sets the response timeout duration used when waiting for a server to reply. 
         * <p> 
         * The response timeout begins once the request write completes and finishes once the first response read is 
         * triggered when the server response is received. 
         * <p> 
         * If {@code responseTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_RESPONSE_TIMEOUT} or a 
         * 60-second timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout will be 
         * applied to the response. When applying the timeout the greatest of one millisecond and the value of 
         * {@code responseTimeout} will be used. 
         * <p> 
         * The default response timeout is 60 seconds. 
         * 
         * @param responseTimeout Response timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions responseTimeout(Duration responseTimeout) 
        /** 
         * Sets the response timeout duration used when waiting for a server to reply. 
         * <p> 
         * The response timeout begins once the request write completes and finishes once the first response read is 
         * triggered when the server response is received. 
         * <p> 
         * If {@code responseTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_RESPONSE_TIMEOUT} or a 
         * 60-second timeout will be used, if it is a {@link Duration} less than or equal to zero then no timeout will be 
         * applied to the response. When applying the timeout the greatest of one millisecond and the value of 
         * {@code responseTimeout} will be used. 
         * <p> 
         * The default response timeout is 60 seconds. 
         * 
         * @param responseTimeout Response timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setResponseTimeout(Duration responseTimeout) 
        /** 
         * Gets the writing timeout for a request to be sent. 
         * <p> 
         * The writing timeout does not apply to the entire request but to each emission being sent over the wire. For 
         * example a request body which emits {@code 10} {@code 8KB} buffers will trigger {@code 10} write operations, the 
         * outbound buffer will be periodically checked to determine if it is still draining. 
         * <p> 
         * If {@code writeTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_WRITE_TIMEOUT} or a 60-second 
         * timeout will be used, if it is a {@link Duration} less than or equal to zero then no write timeout will be 
         * applied. When applying the timeout the greatest of one millisecond and the value of {@code writeTimeout} will be 
         * used. 
         * <p> 
         * The default writing timeout is 60 seconds. 
         * 
         * @return The writing timeout of a request to be sent. 
         */ 
        public Duration getWriteTimeout() 
        /** 
         * Sets the writing timeout for a request to be sent. 
         * <p> 
         * The writing timeout does not apply to the entire request but to each emission being sent over the wire. For 
         * example a request body which emits {@code 10} {@code 8KB} buffers will trigger {@code 10} write operations, the 
         * outbound buffer will be periodically checked to determine if it is still draining. 
         * <p> 
         * If {@code writeTimeout} is null either {@link Configuration#PROPERTY_AZURE_REQUEST_WRITE_TIMEOUT} or a 60-second 
         * timeout will be used, if it is a {@link Duration} less than or equal to zero then no write timeout will be 
         * applied. When applying the timeout the greatest of one millisecond and the value of {@code writeTimeout} will be 
         * used. 
         * <p> 
         * The default writing timeout is 60 seconds. 
         * 
         * @param writeTimeout Write operation timeout duration. 
         * @return The updated HttpClientOptions object. 
         */ 
        public HttpClientOptions setWriteTimeout(Duration writeTimeout) 
    } 
    /** 
     * This class provides utility to iterate over values using standard 'for-each' style loops or to convert them into a 
     * {@link Stream} and operate in that fashion. 
     * 
     * <p> 
     * <strong>Code sample using Stream</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.iterableStream.stream --> 
     * <pre> 
     * // process the stream 
     * myIterableStream.stream().forEach(resp -> { 
     *     if (resp.getStatusCode() == HttpURLConnection.HTTP_OK) { 
     *         System.out.printf("Response headers are %s. Url %s%n", resp.getDeserializedHeaders(), 
     *             resp.getRequest().getUrl()); 
     *         resp.getElements().forEach(value -> System.out.printf("Response value is %d%n", value)); 
     *     } 
     * }); 
     * </pre> 
     * <!-- end com.azure.core.util.iterableStream.stream --> 
     * 
     * <p> 
     * <strong>Code sample using Iterator</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.iterableStream.iterator.while --> 
     * <pre> 
     * // Iterate over iterator 
     * for (PagedResponseBase<String, Integer> resp : myIterableStream) { 
     *     if (resp.getStatusCode() == HttpURLConnection.HTTP_OK) { 
     *         System.out.printf("Response headers are %s. Url %s%n", resp.getDeserializedHeaders(), 
     *             resp.getRequest().getUrl()); 
     *         resp.getElements().forEach(value -> System.out.printf("Response value is %d%n", value)); 
     *     } 
     * } 
     * </pre> 
     * <!-- end com.azure.core.util.iterableStream.iterator.while --> 
     * 
     * <p> 
     * <strong>Code sample using Stream and filter</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.iterableStream.stream.filter --> 
     * <pre> 
     * // process the stream 
     * myIterableStream.stream().filter(resp -> resp.getStatusCode() == HttpURLConnection.HTTP_OK) 
     *     .limit(10) 
     *     .forEach(resp -> { 
     *         System.out.printf("Response headers are %s. Url %s%n", resp.getDeserializedHeaders(), 
     *             resp.getRequest().getUrl()); 
     *         resp.getElements().forEach(value -> System.out.printf("Response value is %d%n", value)); 
     *     }); 
     * </pre> 
     * <!-- end com.azure.core.util.iterableStream.stream.filter --> 
     * 
     * @param <T> The type of value in this {@link Iterable}. 
     * @see Iterable 
     */ 
    public class IterableStream<T> implements Iterable<T> { 
        /** 
         * Creates an instance with the given {@link Flux}. 
         * 
         * @param flux Flux of items to iterate over. 
         * @throws NullPointerException If {@code flux} is {@code null}. 
         */ 
        public IterableStream(Flux<T> flux) 
        /** 
         * Creates an instance with the given {@link Iterable}. 
         * 
         * @param iterable Collection of items to iterate over. 
         * @throws NullPointerException If {@code iterable} is {@code null}. 
         */ 
        public IterableStream(Iterable<T> iterable) 
        /** 
         * Utility function to provide {@link Iterator} of value {@code T}. 
         * 
         * @return {@link Iterator} of value {@code T}. 
         */ 
        @Override public Iterator<T> iterator() 
        /** 
         * Creates an {@link IterableStream} from an {@link Iterable}. 
         * <p> 
         * An empty {@link IterableStream} will be returned if the input iterable is {@code null}. 
         * 
         * @param iterable Collection of items to iterate over. 
         * @param <T> The type of value in this {@link Iterable}. 
         * @return An {@link IterableStream} based on the passed collection. 
         */ 
        public static <T> IterableStream<T> of(Iterable<T> iterable) 
        /** 
         * Utility function to provide {@link Stream} of value {@code T}. 
         * 
         * @return {@link Stream} of value {@code T}. 
         */ 
        public Stream<T> stream() 
    } 
    @Fluent
    /** 
     * The options to configure library-specific information on {@link TracerProvider} 
     * and {@link MeterProvider}. 
     */ 
    public final class LibraryTelemetryOptions { 
        /** 
         * Creates an instance of {@link LibraryTelemetryOptions}. 
         * 
         * @param libraryName The client library name. 
         */ 
        public LibraryTelemetryOptions(String libraryName) 
        /** 
         * Gets the client library name. 
         * 
         * @return The client library name. 
         */ 
        public String getLibraryName() 
        /** 
         * Gets the client library version. 
         * 
         * @return The client library version. 
         */ 
        public String getLibraryVersion() 
        /** 
         * Sets the client library version. 
         * 
         * @param libraryVersion The client library version. 
         * @return The updated {@link LibraryTelemetryOptions} object. 
         */ 
        public LibraryTelemetryOptions setLibraryVersion(String libraryVersion) 
        /** 
         * Gets the Azure Resource Provider namespace. 
         * 
         * @return The Azure Resource Provider  namespace. 
         */ 
        public String getResourceProviderNamespace() 
        /** 
         * Sets the Azure namespace. 
         * 
         * @param rpNamespace The Azure Resource Provider namespace client library communicates with. 
         * @return The updated {@link LibraryTelemetryOptions} object. 
         */ 
        public LibraryTelemetryOptions setResourceProviderNamespace(String rpNamespace) 
        /** 
         * Gets the schema URL describing specific schema and version of the telemetry 
         * the library emits. 
         * 
         * @return The schema URL. 
         */ 
        public String getSchemaUrl() 
        /** 
         * Sets the schema URL describing specific schema and version of the telemetry 
         * the library emits. 
         * 
         * @param schemaUrl The schema URL. 
         * @return The updated {@link LibraryTelemetryOptions} object. 
         */ 
        public LibraryTelemetryOptions setSchemaUrl(String schemaUrl) 
    } 
    /** 
     * Metrics configuration options for clients. 
     */ 
    public class MetricsOptions { 
        /** 
         * Creates new instance of {@link MetricsOptions} 
         */ 
        public MetricsOptions() 
        /** 
         * Creates new instance of {@link MetricsOptions} 
         * 
         * @param meterProvider type of the {@link MeterProvider} implementation that should be used to construct an instance of 
         * {@link Meter}. 
         * If the value is not set (or {@code null}), then the first {@link MeterProvider} resolved by {@link java.util.ServiceLoader} will 
         * be used to create an instance of {@link Meter}. If the value is set and doesn't match any 
         * {@link MeterProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         *  attempting to create an instance of {@link Meter}. 
         */ 
        protected MetricsOptions(Class<? extends MeterProvider> meterProvider) 
        /** 
         * Flag indicating if metrics should be enabled. 
         * @return {@code true} if metrics are enabled, {@code false} otherwise. 
         */ 
        public boolean isEnabled() 
        /** 
         * Enables or disables metrics. By default, metrics are enabled if and only if metrics implementation is detected. 
         * 
         * @param enabled pass {@code true} to enable metrics. 
         * @return the updated {@link MetricsOptions} object. 
         */ 
        public MetricsOptions setEnabled(boolean enabled) 
        /** 
         * Attempts to load metrics options from the configuration. 
         * 
         * @param configuration The {@link Configuration} instance containing metrics options. If 
         * {@code null} is passed then {@link Configuration#getGlobalConfiguration()} will be used. 
         * @return A {@link MetricsOptions} reflecting a metrics loaded from configuration, if no options are found, default 
         * (enabled) options will be returned. 
         */ 
        public static MetricsOptions fromConfiguration(Configuration configuration) 
        /** 
         * Gets configured {@link MeterProvider} implementation that should be used to construct an instance of 
         * {@link Meter}. 
         * 
         * @return The {@link MeterProvider} implementation used to create an instance of {@link Meter}. 
         */ 
        public Class<? extends MeterProvider> getMeterProvider() 
    } 
    @FunctionalInterface
    /** 
     * A {@link ProgressListener} is an interface that can be used to listen to the progress of the I/O transfers. 
     * The {@link #handleProgress(long)} method will be called periodically with the total progress accumulated 
     * at the given point of time. 
     * 
     * <p> 
     * <strong>Code samples</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.ProgressReportingE2ESample --> 
     * <pre> 
     * /** 
     *  * A simple operation that simulates I/O activity. 
     *  * @param progressReporter The {@link ProgressReporter}. 
     *  */ 
     * public static void simpleOperation(ProgressReporter progressReporter) { 
     *     for (long i = 0; i < 100; i++) { 
     *         // Simulate 100 I/Os with 10 progress. 
     *         progressReporter.reportProgress(10); 
     *     } 
     * } 
     * 
     * /** 
     *  * A complex operation that simulates I/O activity by invoking multiple {@link #simpleOperation(ProgressReporter)}. 
     *  * @param progressReporter The {@link ProgressReporter}. 
     *  */ 
     * public static void complexOperation(ProgressReporter progressReporter) { 
     *     simpleOperation(progressReporter.createChild()); 
     *     simpleOperation(progressReporter.createChild()); 
     *     simpleOperation(progressReporter.createChild()); 
     * } 
     * 
     * /** 
     *  * The main method. 
     *  * @param args Program arguments. 
     *  */ 
     * public static void main(String[] args) { 
     *     // Execute simpleOperation 
     *     ProgressReporter simpleOperationProgressReporter = ProgressReporter 
     *         .withProgressListener(progress -> System.out.println("Simple operation progress " + progress)); 
     *     simpleOperation(simpleOperationProgressReporter); 
     * 
     *     // Execute complexOperation 
     *     ProgressReporter complexOperationProgressReporter = ProgressReporter 
     *         .withProgressListener(progress -> System.out.println("Complex operation progress " + progress)); 
     *     complexOperation(complexOperationProgressReporter); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.util.ProgressReportingE2ESample --> 
     */ 
    public interface ProgressListener { 
        /** 
         * The callback function invoked as progress is reported. 
         * 
         * <p> 
         * The callback can be called concurrently from multiple threads if reporting spans across multiple 
         * requests. The implementor must not perform thread blocking operations in the handler code. 
         * </p> 
         * 
         * @param progress The total progress at the current point of time. 
         */ 
        void handleProgress(long progress) 
    } 
    /** 
     * {@link ProgressReporter} offers a convenient way to add progress tracking to I/O operations. 
     * <p> 
     * The {@link ProgressReporter} can be used to track a single operation as well as the progress of 
     * complex operations that involve multiple sub-operations. In the latter case {@link ProgressReporter} 
     * forms a tree where child nodes track the progress of sub-operations and report to the parent which in turn 
     * aggregates the total progress. The reporting tree can have arbitrary level of nesting. 
     * 
     * <p> 
     * <strong>Code samples</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.ProgressReportingE2ESample --> 
     * <pre> 
     * /** 
     *  * A simple operation that simulates I/O activity. 
     *  * @param progressReporter The {@link ProgressReporter}. 
     *  */ 
     * public static void simpleOperation(ProgressReporter progressReporter) { 
     *     for (long i = 0; i < 100; i++) { 
     *         // Simulate 100 I/Os with 10 progress. 
     *         progressReporter.reportProgress(10); 
     *     } 
     * } 
     * 
     * /** 
     *  * A complex operation that simulates I/O activity by invoking multiple {@link #simpleOperation(ProgressReporter)}. 
     *  * @param progressReporter The {@link ProgressReporter}. 
     *  */ 
     * public static void complexOperation(ProgressReporter progressReporter) { 
     *     simpleOperation(progressReporter.createChild()); 
     *     simpleOperation(progressReporter.createChild()); 
     *     simpleOperation(progressReporter.createChild()); 
     * } 
     * 
     * /** 
     *  * The main method. 
     *  * @param args Program arguments. 
     *  */ 
     * public static void main(String[] args) { 
     *     // Execute simpleOperation 
     *     ProgressReporter simpleOperationProgressReporter = ProgressReporter 
     *         .withProgressListener(progress -> System.out.println("Simple operation progress " + progress)); 
     *     simpleOperation(simpleOperationProgressReporter); 
     * 
     *     // Execute complexOperation 
     *     ProgressReporter complexOperationProgressReporter = ProgressReporter 
     *         .withProgressListener(progress -> System.out.println("Complex operation progress " + progress)); 
     *     complexOperation(complexOperationProgressReporter); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.util.ProgressReportingE2ESample --> 
     */ 
    public final class ProgressReporter { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates child {@link ProgressReporter} that can be used to track sub-progress when tracked activity spans 
         * across concurrent processes. Child {@link ProgressReporter} notifies parent about progress and 
         * parent notifies {@link ProgressListener}. 
         * @return The child {@link ProgressReporter}. 
         */ 
        public ProgressReporter createChild() 
        /** 
         * Accumulates the provided {@code progress} and notifies. 
         * 
         * <p> 
         * If this is a root {@link ProgressReporter} 
         * then attached {@link ProgressListener} is notified about accumulated progress. 
         * Otherwise, the provided {@code progress} is reported to the parent {@link ProgressReporter}. 
         * </p> 
         * 
         * @param progress The number to be accumulated. 
         */ 
        public void reportProgress(long progress) 
        /** 
         * Resets progress to zero and notifies. 
         * <p> 
         * If this is a root {@link ProgressReporter} then attached {@link ProgressListener} is notified. 
         * Otherwise, already accumulated progress is subtracted from the parent {@link ProgressReporter}'s progress. 
         * </p> 
         */ 
        public void reset() 
        /** 
         * Creates a {@link ProgressReporter} that notifies {@link ProgressListener}. 
         * @param progressListener The {@link ProgressListener} to be notified about progress. Must not be null. 
         * @return The {@link ProgressReporter} instance. 
         * @throws NullPointerException If {@code progressReceiver} is null. 
         */ 
        public static ProgressReporter withProgressListener(ProgressListener progressListener) 
    } 
    /** 
     * This interface represents managing references to {@link Object Objects} and providing the ability to run a 
     * cleaning operation once the object is no longer able to be reference. 
     * <p> 
     * Expected usage of this is through {@link ReferenceManager#INSTANCE}. 
     */ 
    public interface ReferenceManager { 
        /** 
         * The global instance of {@link ReferenceManager} that should be used to maintain object references. 
         */ 
        ReferenceManager INSTANCE = new ReferenceManagerImpl ( /* Elided */ ) ; 
        /** 
         * Registers the {@code object} and the cleaning action to run once the object becomes phantom reachable. 
         * <p> 
         * The {@code cleanupAction} cannot have a reference to the {@code object}, otherwise the object will never be able 
         * to become phantom reachable. 
         * <p> 
         * Exceptions thrown by {@code cleanupAction} are ignored. 
         * 
         * @param object The object to monitor. 
         * @param cleanupAction The cleanup action to perform when the {@code object} becomes phantom reachable. 
         * @throws NullPointerException If either {@code object} or {@code cleanupAction} are null. 
         */ 
        void register(Object object, Runnable cleanupAction) 
    } 
    /** 
     * A generic interface for sending HTTP requests using the provided service version. 
     */ 
    public interface ServiceVersion { 
        /** 
         * Gets the string representation of the {@link ServiceVersion} 
         * 
         * @return the string representation of the {@link ServiceVersion} 
         */ 
        String getVersion() 
    } 
    /** 
     * An {@link ScheduledExecutorService} that is shared by multiple consumers. 
     * <p> 
     * If {@link SharedExecutorService#setExecutorService(ScheduledExecutorService)} isn't called a default shared executor 
     * service is created using the following configuration settings: 
     * <ul> 
     *     <li>{@code azure.sdk.shared.threadpool.maxpoolsize} system property or 
     *     {@code AZURE_SDK_SHARED_THREADPOOL_MAXPOOLSIZE} environment variable - The maximum pool size of the shared 
     *     executor service. If not set, it defaults to 10 times the number of available processors.</li> 
     *     <li>{@code azure.sdk.shared.threadpool.keepalivemillis} system property or 
     *     {code AZURE_SDK_SHARED_THREADPOOL_KEEPALIVEMILLIS} environment variable - The keep alive time in millis for 
     *     threads in the shared executor service. If not set, it defaults to 60 seconds. Limited to integer size.</li> 
     *     <li>{@code azure.sdk.shared.threadpool.usevirtualthreads} system property or 
     *     {@code AZURE_SDK_SHARED_THREADPOOL_USEVIRTUALTHREADS} environment variable - A boolean flag to indicate if the 
     *     shared executor service should use virtual threads. If not set, it defaults to true. Ignored if virtual threads 
     *     are not available in the runtime.</li> 
     * </ul> 
     */ 
    public final class SharedExecutorService implements ScheduledExecutorService { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Shutdown isn't supported for this executor service as it is shared by multiple consumers. 
         * <p> 
         * Calling this method will result in an {@link UnsupportedOperationException} being thrown. 
         * 
         * @param timeout The amount of time to wait for the executor service to shutdown. 
         * @param unit The unit of time for the timeout. 
         * @return Nothing will be returned as an exception will always be thrown. 
         * @throws UnsupportedOperationException This method will always throw an exception. 
         */ 
        @Override public boolean awaitTermination(long timeout, TimeUnit unit) 
        @Override public void execute(Runnable command) 
        /** 
         * Gets the backing executor service for the shared instance. 
         * <p> 
         * This returns the executor service for all users of the {@link #getInstance() shared instance}. Meaning, if 
         * another area in code already had a reference to the shared instance. 
         * <p> 
         * This may return null if the shared instance has not been set yet. 
         * 
         * @return The executor service that is set as the shared instance, may be null if a shared instance hasn't been 
         * set. 
         */ 
        public ScheduledExecutorService getExecutorService() 
        /** 
         * Sets the backing executor service for the shared instance. 
         * <p> 
         * This updates the executor service for all users of the {@link #getInstance() shared instance}. Meaning, if 
         * another area in code already had a reference to the shared instance, it will now use the passed executor service 
         * to execute tasks. 
         * <p> 
         * If the executor service is already set, this will replace it with the new executor service. If the replaced 
         * executor service was created by this class, it will be shut down. 
         * <p> 
         * If the passed executor service is null, this will throw a {@link NullPointerException}. If the passed executor 
         * service is shutdown or terminated, this will throw an {@link IllegalStateException}. 
         * 
         * @param executorService The executor service to set as the shared instance. 
         * @throws NullPointerException If the passed executor service is null. 
         * @throws IllegalStateException If the passed executor service is shutdown or terminated. 
         */ 
        public void setExecutorService(ScheduledExecutorService executorService) 
        /** 
         * Gets the shared instance of the executor service. 
         * 
         * @return The shared instance of the executor service. 
         */ 
        public static SharedExecutorService getInstance() 
        @Override public <T> List<Future<T>> invokeAll(Collection<? extends Callable<T>> tasks) throws InterruptedException
        @Override public <T> List<Future<T>> invokeAll(Collection<? extends Callable<T>> tasks, long timeout, TimeUnit unit) throws InterruptedException
        @Override public <T> T invokeAny(Collection<? extends Callable<T>> tasks) throws InterruptedException, ExecutionException
        @Override public <T> T invokeAny(Collection<? extends Callable<T>> tasks, long timeout, TimeUnit unit) throws InterruptedException, ExecutionException, TimeoutException
        /** 
         * Resets the state of the {@link #getInstance()} to an uninitialized state. 
         * <p> 
         * This will shut down the executor service if it was created by this class. 
         */ 
        public void reset() 
        @Override public ScheduledFuture<?> schedule(Runnable command, long delay, TimeUnit unit) 
        @Override public <V> ScheduledFuture<V> schedule(Callable<V> callable, long delay, TimeUnit unit) 
        @Override public ScheduledFuture<?> scheduleAtFixedRate(Runnable command, long initialDelay, long period, TimeUnit unit) 
        @Override public ScheduledFuture<?> scheduleWithFixedDelay(Runnable command, long initialDelay, long delay, TimeUnit unit) 
        /** 
         * Checks if the executor service is shutdown. 
         * <p> 
         * Will always return false as the shared executor service cannot be shut down. 
         * 
         * @return False, as the shared executor service cannot be shut down. 
         */ 
        @Override public boolean isShutdown() 
        /** 
         * Shutdown isn't supported for this executor service as it is shared by multiple consumers. 
         * <p> 
         * Calling this method will result in an {@link UnsupportedOperationException} being thrown. 
         * 
         * @throws UnsupportedOperationException This method will always throw an exception. 
         */ 
        @Override public void shutdown() 
        /** 
         * Shutdown isn't supported for this executor service as it is shared by multiple consumers. 
         * <p> 
         * Calling this method will result in an {@link UnsupportedOperationException} being thrown. 
         * 
         * @return Nothing will be returned as an exception will always be thrown. 
         * @throws UnsupportedOperationException This method will always throw an exception. 
         */ 
        @Override public List<Runnable> shutdownNow() 
        @Override public <T> Future<T> submit(Callable<T> task) 
        @Override public Future<?> submit(Runnable task) 
        @Override public <T> Future<T> submit(Runnable task, T result) 
        /** 
         * Checks if the executor service is terminated. 
         * <p> 
         * Will always return false as the shared executor service cannot be terminated. 
         * 
         * @return False, as the shared executor service cannot be terminated. 
         */ 
        @Override public boolean isTerminated() 
    } 
    @Immutable
    /** 
     * Generic attribute collection applicable to metrics, tracing and logging implementations. 
     * Implementation is capable of handling different attribute types, caching and optimizing the internal representation. 
     */ 
    public interface TelemetryAttributes { 
        // This interface does not declare any API. 
    } 
    /** 
     * Tracing configuration options for clients. 
     */ 
    public class TracingOptions { 
        /** 
         * Creates new instance of {@link TracingOptions} 
         */ 
        public TracingOptions() 
        /** 
         * Creates new instance of {@link TracingOptions} 
         * 
         * @param tracerProvider The type of the {@link TracerProvider} implementation that should be used to construct an instance of 
         * {@link Tracer}. 
         * 
         * If the value is not set (or {@code null}), then the first {@link TracerProvider} resolved by {@link java.util.ServiceLoader} will 
         * be used to create an instance of {@link Tracer}. If the value is set and doesn't match any 
         * {@link TracerProvider} resolved by {@link java.util.ServiceLoader} an {@link IllegalStateException} will be thrown when 
         *  attempting to create an instance of {@link Tracer}. 
         */ 
        protected TracingOptions(Class<? extends TracerProvider> tracerProvider) 
        /** 
         * Gets the set of query parameter names that are allowed to be recorded in the URL. 
         * @return The set of query parameter names that are allowed to be recorded in the URL. 
         */ 
        public Set<String> getAllowedTracingQueryParamNames() 
        /** 
         * Sets the set of query parameter names that are allowed to be recorded in the URL. 
         * @param allowedQueryParamNames The set of query parameter names that are allowed to be recorded in the URL. 
         * @return The updated {@link TracingOptions} object. 
         */ 
        public TracingOptions setAllowedTracingQueryParamNames(Set<String> allowedQueryParamNames) 
        /** 
         * Flag indicating if distributed tracing should be enabled. 
         * @return {@code true} if tracing is enabled, {@code false} otherwise. 
         */ 
        public boolean isEnabled() 
        /** 
         * Enables or disables distributed tracing. By default, tracing is enabled if and only if tracing implementation is detected. 
         * 
         * @param enabled pass {@code true} to enable tracing. 
         * @return the updated {@code TracingOptions} object. 
         */ 
        public TracingOptions setEnabled(boolean enabled) 
        /** 
         * Loads tracing options from the configuration. 
         * 
         * @param configuration The {@link Configuration} instance containing tracing options. If 
         * {@code null} is passed then {@link Configuration#getGlobalConfiguration()} will be used. 
         * @return A {@link TracingOptions} reflecting updated tracing options loaded from the configuration, 
         * if no tracing options are found, default (enabled) tracing options will be returned. 
         */ 
        public static TracingOptions fromConfiguration(Configuration configuration) 
        /** 
         * Gets name of the {@link TracerProvider} implementation that should be used to construct an instance of 
         * {@link Tracer}. 
         * 
         * @return The {@link TracerProvider} implementation used to create an instance of {@link Tracer}. 
         */ 
        public Class<? extends TracerProvider> getTracerProvider() 
    } 
    /** 
     * A builder class that is used to create URLs. 
     */ 
    public final class UrlBuilder { 
        /** 
         * Creates a new instance of {@link UrlBuilder}. 
         */ 
        public UrlBuilder() 
        /** 
         * Append the provided query parameter name and encoded value to query string for the final URL. 
         * 
         * @param queryParameterName The name of the query parameter. 
         * @param queryParameterEncodedValue The encoded value of the query parameter. 
         * @return The provided query parameter name and encoded value to query string for the final URL. 
         * @throws NullPointerException if {@code queryParameterName} or {@code queryParameterEncodedValue} are null. 
         */ 
        public UrlBuilder addQueryParameter(String queryParameterName, String queryParameterEncodedValue) 
        /** 
         * Clear the query that will be used to build the final URL. 
         * 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder clearQuery() 
        /** 
         * Get the host that has been assigned to this UrlBuilder. 
         * 
         * @return the host that has been assigned to this UrlBuilder. 
         */ 
        public String getHost() 
        /** 
         * Set the host that will be used to build the final URL. 
         * 
         * @param host The host that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setHost(String host) 
        /** 
         * Parses the passed {@code url} string into a UrlBuilder. 
         * 
         * @param url The URL string to parse. 
         * @return The UrlBuilder that was created from parsing the passed URL string. 
         */ 
        public static UrlBuilder parse(String url) 
        /** 
         * Parse a UrlBuilder from the provided URL object. 
         * 
         * @param url The URL object to parse. 
         * @return The UrlBuilder that was parsed from the URL object. 
         */ 
        public static UrlBuilder parse(URL url) 
        /** 
         * Get the path that has been assigned to this UrlBuilder. 
         * 
         * @return the path that has been assigned to this UrlBuilder. 
         */ 
        public String getPath() 
        /** 
         * Set the path that will be used to build the final URL. 
         * 
         * @param path The path that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setPath(String path) 
        /** 
         * Get the port that has been assigned to this UrlBuilder. 
         * 
         * @return the port that has been assigned to this UrlBuilder. 
         */ 
        public Integer getPort() 
        /** 
         * Set the port that will be used to build the final URL. 
         * 
         * @param port The port that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setPort(String port) 
        /** 
         * Set the port that will be used to build the final URL. 
         * 
         * @param port The port that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setPort(int port) 
        /** 
         * Get a view of the query that has been assigned to this UrlBuilder. 
         * <p> 
         * Changes to the {@link Map} returned by this API won't be reflected in the UrlBuilder. 
         * 
         * @return A view of the query that has been assigned to this UrlBuilder. 
         */ 
        public Map<String, String> getQuery() 
        /** 
         * Set the query that will be used to build the final URL. 
         * 
         * @param query The query that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setQuery(String query) 
        /** 
         * Set the provided query parameter name and encoded value to query string for the final URL. 
         * 
         * @param queryParameterName The name of the query parameter. 
         * @param queryParameterEncodedValue The encoded value of the query parameter. 
         * @return The provided query parameter name and encoded value to query string for the final URL. 
         * @throws NullPointerException if {@code queryParameterName} or {@code queryParameterEncodedValue} are null. 
         */ 
        public UrlBuilder setQueryParameter(String queryParameterName, String queryParameterEncodedValue) 
        /** 
         * Returns the query string currently configured in this UrlBuilder instance. 
         * 
         * @return A String containing the currently configured query string. 
         */ 
        public String getQueryString() 
        /** 
         * Get the scheme/protocol that has been assigned to this UrlBuilder. 
         * 
         * @return the scheme/protocol that has been assigned to this UrlBuilder. 
         */ 
        public String getScheme() 
        /** 
         * Set the scheme/protocol that will be used to build the final URL. 
         * 
         * @param scheme The scheme/protocol that will be used to build the final URL. 
         * @return This UrlBuilder so that multiple setters can be chained together. 
         */ 
        public UrlBuilder setScheme(String scheme) 
        /** 
         * Get the string representation of the URL that is being built. 
         * 
         * @return The string representation of the URL that is being built. 
         */ 
        @Override public String toString() 
        /** 
         * Get the URL that is being built. 
         * 
         * @return The URL that is being built. 
         * @throws MalformedURLException if the URL is not fully formed. 
         */ 
        public URL toUrl() throws MalformedURLException
    } 
    /** 
     * Class to hold the properties used in user agent strings. 
     */ 
    public class UserAgentProperties { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Returns the name of the client library. 
         * 
         * @return the name of the client library. 
         */ 
        public String getName() 
        /** 
         * Returns the version of the client library. 
         * 
         * @return the version of the client library. 
         */ 
        public String getVersion() 
    } 
    /** 
     * Utility for building user agent string for Azure client libraries as specified in the 
     * <a href="https://azure.github.io/azure-sdk/general_azurecore.html#telemetry-policy">design guidelines</a>. 
     */ 
    public final class UserAgentUtil { 
        /** 
         * Default {@code UserAgent} header. 
         */ 
        public static final String DEFAULT_USER_AGENT_HEADER = "azsdk-java"; 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Return user agent string for the given sdk name and version. 
         * 
         * @param applicationId Name of the application. 
         * @param sdkName Name of the SDK. 
         * @param sdkVersion Version of the SDK. 
         * @param configuration The configuration to use to determine if platform info should be included in the user agent 
         * string. 
         * 
         * @return User agent string as specified in design guidelines. 
         * 
         * @throws IllegalArgumentException If {@code applicationId} contains spaces. 
         */ 
        public static String toUserAgentString(String applicationId, String sdkName, String sdkVersion, Configuration configuration) 
    } 
} 
/** 
 * Package containing utilities for client builders. 
 */ 
package com.azure.core.util.builder { 
    /** 
     * This class contains utility methods useful for client builders. 
     */ 
    public final class ClientBuilderUtil { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * This method validates that customized {@link HttpPipelinePolicy retry policy} and customized {@link RetryOptions} 
         * are mutually exclusive. 
         * If no customization was made then it falls back to the default. 
         * @param retryPolicy a customized {@link HttpPipelinePolicy}. 
         * @param retryOptions a customized {@link RetryOptions}. 
         * @return final {@link RetryPolicy} to be used by the builder. 
         * @throws IllegalStateException if both {@code retryPolicy} and {@code retryOptions} are not {@code null}. 
         */ 
        public static HttpPipelinePolicy validateAndGetRetryPolicy(HttpPipelinePolicy retryPolicy, RetryOptions retryOptions) 
        /** 
         * This method validates that customized {@link HttpPipelinePolicy retry policy} and customized {@link RetryOptions} 
         * are mutually exclusive. 
         * If no customization was made then it falls back to the default. 
         * @param retryPolicy a customized {@link HttpPipelinePolicy}. 
         * @param retryOptions a customized {@link RetryOptions}. 
         * @param defaultPolicy a default {@link HttpPipelinePolicy}. 
         * @return final {@link RetryPolicy} to be used by the builder. 
         * @throws NullPointerException if {@code defaultPolicy} is {@code null}. 
         * @throws IllegalStateException if both {@code retryPolicy} and {@code retryOptions} are not {@code null}. 
         */ 
        public static HttpPipelinePolicy validateAndGetRetryPolicy(HttpPipelinePolicy retryPolicy, RetryOptions retryOptions, HttpPipelinePolicy defaultPolicy) 
    } 
} 
/** 
 * Package containing APIs for IO operations. 
 */ 
package com.azure.core.util.io { 
    /** 
     * Utilities related to IO operations that involve channels, streams, byte transfers. 
     */ 
    public final class IOUtils { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Adapts {@link AsynchronousFileChannel} to {@link AsynchronousByteChannel}. 
         * @param fileChannel The {@link AsynchronousFileChannel}. 
         * @param position The position in the file to begin writing or reading the {@code content}. 
         * @return A {@link AsynchronousByteChannel} that delegates to {@code fileChannel}. 
         * @throws NullPointerException When {@code fileChannel} is null. 
         * @throws IllegalArgumentException When {@code position} is negative. 
         */ 
        public static AsynchronousByteChannel toAsynchronousByteChannel(AsynchronousFileChannel fileChannel, long position) 
        /** 
         * Transfers bytes from {@link ReadableByteChannel} to {@link WritableByteChannel}. 
         * 
         * @param source A source {@link ReadableByteChannel}. 
         * @param destination A destination {@link WritableByteChannel}. 
         * @throws IOException When I/O operation fails. 
         * @throws NullPointerException When {@code source} or {@code destination} is null. 
         */ 
        public static void transfer(ReadableByteChannel source, WritableByteChannel destination) throws IOException
        /** 
         * Transfers bytes from {@link ReadableByteChannel} to {@link WritableByteChannel}. 
         * 
         * @param source A source {@link ReadableByteChannel}. 
         * @param destination A destination {@link WritableByteChannel}. 
         * @param estimatedSourceSize An estimated size of the source channel, may be null. Used to better determine the 
         * size of the buffer used to transfer data in an attempt to reduce read and write calls. 
         * @throws IOException When I/O operation fails. 
         * @throws NullPointerException When {@code source} or {@code destination} is null. 
         */ 
        public static void transfer(ReadableByteChannel source, WritableByteChannel destination, Long estimatedSourceSize) throws IOException
        /** 
         * Transfers bytes from {@link ReadableByteChannel} to {@link AsynchronousByteChannel}. 
         * 
         * @param source A source {@link ReadableByteChannel}. 
         * @param destination A destination {@link AsynchronousByteChannel}. 
         * @return A {@link Mono} that completes when transfer is finished. 
         * @throws NullPointerException When {@code source} or {@code destination} is null. 
         */ 
        public static Mono<Void> transferAsync(ReadableByteChannel source, AsynchronousByteChannel destination) 
        /** 
         * Transfers bytes from {@link ReadableByteChannel} to {@link AsynchronousByteChannel}. 
         * 
         * @param source A source {@link ReadableByteChannel}. 
         * @param destination A destination {@link AsynchronousByteChannel}. 
         * @param estimatedSourceSize An estimated size of the source channel, may be null. Used to better determine the 
         * size of the buffer used to transfer data in an attempt to reduce read and write calls. 
         * @return A {@link Mono} that completes when transfer is finished. 
         * @throws NullPointerException When {@code source} or {@code destination} is null. 
         */ 
        public static Mono<Void> transferAsync(ReadableByteChannel source, AsynchronousByteChannel destination, Long estimatedSourceSize) 
        /** 
         * Transfers the {@link StreamResponse} content to {@link AsynchronousByteChannel}. 
         * Resumes the transfer in case of errors. 
         * 
         * @param targetChannel The destination {@link AsynchronousByteChannel}. 
         * @param sourceResponse The initial {@link StreamResponse}. 
         * @param onErrorResume A {@link BiFunction} of {@link Throwable} and {@link Long} which is used to resume 
         * downloading when an error occurs. The function accepts a {@link Throwable} and offset at the destination 
         * from beginning of writing at which the error occurred. 
         * @param progressReporter The {@link ProgressReporter}. 
         * @param maxRetries The maximum number of times a download can be resumed when an error occurs. 
         * @return A {@link Mono} which completion indicates successful transfer. 
         */ 
        public static Mono<Void> transferStreamResponseToAsynchronousByteChannel(AsynchronousByteChannel targetChannel, StreamResponse sourceResponse, BiFunction<Throwable, Long, Mono<StreamResponse>> onErrorResume, ProgressReporter progressReporter, int maxRetries) 
    } 
} 
/** 
 * Package containing logging APIs. 
 */ 
package com.azure.core.util.logging { 
    /** 
     * This is a fluent logger helper class that wraps a pluggable {@link Logger}. 
     * 
     * <p> 
     * This logger logs format-able messages that use {@code {}} as the placeholder. When a {@link Throwable throwable} 
     * is the last argument of the format varargs and the logger is enabled for {@link ClientLogger#verbose(String, 
     * Object...) verbose}, the stack trace for the throwable is logged. 
     * </p> 
     * 
     * <p> 
     * A minimum logging level threshold is determined by the 
     * {@link Configuration#PROPERTY_AZURE_LOG_LEVEL AZURE_LOG_LEVEL} environment configuration. By default logging is 
     * <b>disabled</b>. 
     * </p> 
     * 
     * <p> 
     * <strong>Log level hierarchy</strong> 
     * </p> 
     * <ol> 
     * <li>{@link ClientLogger#error(String, Object...) Error}</li> 
     * <li>{@link ClientLogger#warning(String, Object...) Warning}</li> 
     * <li>{@link ClientLogger#info(String, Object...) Info}</li> 
     * <li>{@link ClientLogger#verbose(String, Object...) Verbose}</li> 
     * </ol> 
     * 
     * <p> 
     * The logger is capable of producing json-formatted messages enriched with key value pairs. 
     * Context can be provided in the constructor and populated on every message or added per each log record. 
     * </p> 
     * 
     * @see Configuration 
     */ 
    public class ClientLogger { 
        /** 
         * Retrieves a logger for the passed class using the {@link LoggerFactory}. 
         * 
         * @param clazz Class creating the logger. 
         */ 
        public ClientLogger(Class<?> clazz) 
        /** 
         * Retrieves a logger for the passed class name using the {@link LoggerFactory}. 
         * 
         * @param className Class name creating the logger. 
         * @throws RuntimeException when logging configuration is invalid depending on SLF4J implementation. 
         */ 
        public ClientLogger(String className) 
        /** 
         * Retrieves a logger for the passed class using the {@link LoggerFactory}. 
         * 
         * @param clazz Class creating the logger. 
         * @param context Context to be populated on every log record written with this logger. Objects are serialized with 
         * {@code toString()} method. 
         * @throws NullPointerException If {@code clazz} is null. 
         */ 
        public ClientLogger(Class<?> clazz, Map<String, Object> context) 
        /** 
         * Retrieves a logger for the passed class name using the {@link LoggerFactory} with context that will be populated 
         * on all log records produced with this logger. 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger#globalcontext --> 
         * <pre> 
         * Map<String, Object> context = new HashMap<>(); 
         * context.put("connectionId", "95a47cf"); 
         * 
         * ClientLogger loggerWithContext = new ClientLogger(ClientLoggerJavaDocCodeSnippets.class, context); 
         * loggerWithContext.info("A formattable message. Hello, {}", name); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger#globalcontext --> 
         * 
         * @param className Class name creating the logger. 
         * @param context Context to be populated on every log record written with this logger. Objects are serialized with 
         * {@code toString()} method. 
         * @throws RuntimeException when logging configuration is invalid depending on SLF4J implementation. 
         */ 
        public ClientLogger(String className, Map<String, Object> context) 
        /** 
         * Creates {@link LoggingEventBuilder} for {@code error} log level that can be used to enrich log with additional 
         * context. 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with context at error level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * <pre> 
         * logger.atVerbose() 
         *     .addKeyValue("key", 1L) 
         *     .log(() -> String.format("Param 1: %s, Param 2: %s, Param 3: %s", "param1", "param2", "param3")); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * 
         * @return instance of {@link LoggingEventBuilder}  or no-op if error logging is disabled. 
         */ 
        public LoggingEventBuilder atError() 
        /** 
         * Creates {@link LoggingEventBuilder} for {@code info} log level that can be used to enrich log with additional 
         * context. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with context at info level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.atInfo --> 
         * <pre> 
         * logger.atInfo() 
         *     .addKeyValue("key", "value") 
         *     .log("A formattable message. Hello, {}", name); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.atInfo --> 
         * 
         * @return instance of {@link LoggingEventBuilder} or no-op if info logging is disabled. 
         */ 
        public LoggingEventBuilder atInfo() 
        /** 
         * Creates {@link LoggingEventBuilder} for log level that can be used to enrich log with additional context. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with context at provided level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.atLevel --> 
         * <pre> 
         * LogLevel level = response.getStatusCode() == 200 ? LogLevel.INFORMATIONAL : LogLevel.WARNING; 
         * logger.atLevel(level) 
         *     .addKeyValue("key", "value") 
         *     .log("message"); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.atLevel --> 
         * 
         * @param level log level. 
         * @return instance of {@link LoggingEventBuilder} or no-op if logging at provided level is disabled. 
         */ 
        public LoggingEventBuilder atLevel(LogLevel level) 
        /** 
         * Creates {@link LoggingEventBuilder} for {@code verbose} log level that can be used to enrich log with additional 
         * context. 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with context at verbose level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * <pre> 
         * logger.atVerbose() 
         *     .addKeyValue("key", 1L) 
         *     .log(() -> String.format("Param 1: %s, Param 2: %s, Param 3: %s", "param1", "param2", "param3")); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * 
         * @return instance of {@link LoggingEventBuilder} or no-op if verbose logging is disabled. 
         */ 
        public LoggingEventBuilder atVerbose() 
        /** 
         * Creates {@link LoggingEventBuilder} for {@code warning} log level that can be used to enrich log with additional 
         * context. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with context at warning level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.atWarning --> 
         * <pre> 
         * logger.atWarning() 
         *     .addKeyValue("key", "value") 
         *     .log("A formattable message. Hello, {}", name, exception); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.atWarning --> 
         * 
         * @return instance of {@link LoggingEventBuilder} or no-op if warn logging is disabled. 
         */ 
        public LoggingEventBuilder atWarning() 
        /** 
         * Determines if the app or environment logger support logging at the given log level. 
         * 
         * @param logLevel Logging level for the log message. 
         * @return Flag indicating if the environment and logger are configured to support logging at the given log level. 
         */ 
        public boolean canLogAtLevel(LogLevel logLevel) 
        /** 
         * Logs a message at {@code error} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at error log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.error --> 
         * <pre> 
         * try { 
         *     upload(resource); 
         * } catch (IOException ex) { 
         *     logger.error(ex.getMessage()); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.error --> 
         * 
         * @param message The message to log. 
         */ 
        public void error(String message) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code error} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging an error with stack trace.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.error#string-object --> 
         * <pre> 
         * try { 
         *     upload(resource); 
         * } catch (IOException ex) { 
         *     logger.error("A formattable message. Hello, {}", name, ex); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.error#string-object --> 
         * 
         * @param format The format-able message to log. 
         * @param args Arguments for the message. If an exception is being logged, the last argument should be the 
         * {@link Throwable}. 
         */ 
        public void error(String format, Object... args) 
        /** 
         * Logs a message at {@code info} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at verbose log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.info --> 
         * <pre> 
         * logger.info("A log message"); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.info --> 
         * 
         * @param message The message to log. 
         */ 
        public void info(String message) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code informational} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at informational log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.info#string-object --> 
         * <pre> 
         * logger.info("A formattable message. Hello, {}", name); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.info#string-object --> 
         * 
         * @param format The format-able message to log 
         * @param args Arguments for the message. If an exception is being logged, the last argument should be the 
         * {@link Throwable}. 
         */ 
        public void info(String format, Object... args) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at the given {@code logLevel}. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with a specific log level</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.log --> 
         * <pre> 
         * logger.log(LogLevel.VERBOSE, 
         *     () -> String.format("Param 1: %s, Param 2: %s, Param 3: %s", "param1", "param2", "param3")); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.log --> 
         * 
         * @param logLevel Logging level for the log message. 
         * @param message The format-able message to log. 
         */ 
        public void log(LogLevel logLevel, Supplier<String> message) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code verbose} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging with a specific log level and exception</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.log#throwable --> 
         * <pre> 
         * Throwable illegalArgumentException = new IllegalArgumentException("An invalid argument was encountered."); 
         * logger.log(LogLevel.VERBOSE, 
         *     () -> String.format("Param 1: %s, Param 2: %s, Param 3: %s", "param1", "param2", "param3"), 
         *     illegalArgumentException); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.log#throwable --> 
         * 
         * @param logLevel Logging level for the log message. 
         * @param message The format-able message to log. 
         * @param throwable Throwable for the message. {@link Throwable}. 
         */ 
        public void log(LogLevel logLevel, Supplier<String> message, Throwable throwable) 
        /** 
         * Logs the {@link RuntimeException} at the error level and returns it to be thrown. 
         * <p> 
         * This API covers the cases where a runtime exception type needs to be thrown and logged. If a {@link Throwable} is 
         * being logged use {@link #logThrowableAsError(Throwable)} instead. 
         * 
         * @param runtimeException RuntimeException to be logged and returned. 
         * @return The passed {@code RuntimeException}. 
         * @throws NullPointerException If {@code runtimeException} is {@code null}. 
         */ 
        public RuntimeException logExceptionAsError(RuntimeException runtimeException) 
        /** 
         * Logs the {@link RuntimeException} at the warning level and returns it to be thrown. 
         * <p> 
         * This API covers the cases where a runtime exception type needs to be thrown and logged. If a {@link Throwable} is 
         * being logged use {@link #logThrowableAsWarning(Throwable)} instead. 
         * 
         * @param runtimeException RuntimeException to be logged and returned. 
         * @return The passed {@link RuntimeException}. 
         * @throws NullPointerException If {@code runtimeException} is {@code null}. 
         */ 
        public RuntimeException logExceptionAsWarning(RuntimeException runtimeException) 
        /** 
         * Logs the {@link Throwable} at the warning level and returns it to be thrown. 
         * <p> 
         * This API covers the cases where a checked exception type needs to be thrown and logged. If a 
         * {@link RuntimeException} is being logged use {@link #logExceptionAsWarning(RuntimeException)} instead. 
         * 
         * @param throwable Throwable to be logged and returned. 
         * @param <T> Type of the Throwable being logged. 
         * @return The passed {@link Throwable}. 
         * @throws NullPointerException If {@code throwable} is {@code null}. 
         * @deprecated Use {@link #logThrowableAsWarning(Throwable)} instead. 
         */ 
        @Deprecated public <T extends Throwable> T logThowableAsWarning(T throwable) 
        /** 
         * Logs the {@link Throwable} at the error level and returns it to be thrown. 
         * <p> 
         * This API covers the cases where a checked exception type needs to be thrown and logged. If a 
         * {@link RuntimeException} is being logged use {@link #logExceptionAsError(RuntimeException)} instead. 
         * 
         * @param throwable Throwable to be logged and returned. 
         * @param <T> Type of the Throwable being logged. 
         * @return The passed {@link Throwable}. 
         * @throws NullPointerException If {@code throwable} is {@code null}. 
         */ 
        public <T extends Throwable> T logThrowableAsError(T throwable) 
        /** 
         * Logs the {@link Throwable} at the warning level and returns it to be thrown. 
         * <p> 
         * This API covers the cases where a checked exception type needs to be thrown and logged. If a 
         * {@link RuntimeException} is being logged use {@link #logExceptionAsWarning(RuntimeException)} instead. 
         * 
         * @param throwable Throwable to be logged and returned. 
         * @param <T> Type of the Throwable being logged. 
         * @return The passed {@link Throwable}. 
         * @throws NullPointerException If {@code throwable} is {@code null}. 
         */ 
        public <T extends Throwable> T logThrowableAsWarning(T throwable) 
        /** 
         * Logs a message at {@code verbose} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at verbose log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.verbose --> 
         * <pre> 
         * logger.verbose("A log message"); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.verbose --> 
         * 
         * @param message The message to log. 
         */ 
        public void verbose(String message) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code verbose} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at verbose log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.verbose#string-object --> 
         * <pre> 
         * logger.verbose("A formattable message. Hello, {}", name); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.verbose#string-object --> 
         * 
         * @param format The formattable message to log. 
         * @param args Arguments for the message. If an exception is being logged, the last argument should be the 
         * {@link Throwable}. 
         */ 
        public void verbose(String format, Object... args) 
        /** 
         * Logs a message at {@code warning} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at warning log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.warning --> 
         * <pre> 
         * Throwable detailedException = new IllegalArgumentException("A exception with a detailed message"); 
         * logger.warning(detailedException.getMessage()); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.warning --> 
         * 
         * @param message The message to log. 
         */ 
        public void warning(String message) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code warning} log level. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Logging a message at warning log level.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.warning#string-object --> 
         * <pre> 
         * Throwable exception = new IllegalArgumentException("An invalid argument was encountered."); 
         * logger.warning("A formattable message. Hello, {}", name, exception); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.warning#string-object --> 
         * 
         * @param format The format-able message to log. 
         * @param args Arguments for the message. If an exception is being logged, the last argument should be the 
         * {@link Throwable}. 
         */ 
        public void warning(String format, Object... args) 
    } 
    /** 
     * Enum which represent logging levels used in Azure SDKs. 
     */ 
    public enum LogLevel { 
        VERBOSE(1, "1", "verbose", "debug"), 
            /** 
             * Indicates that log level is at verbose level. 
             */ 
        INFORMATIONAL(2, "2", "info", "information", "informational"), 
            /** 
             * Indicates that log level is at information level. 
             */ 
        WARNING(3, "3", "warn", "warning"), 
            /** 
             * Indicates that log level is at warning level. 
             */ 
        ERROR(4, "4", "err", "error"), 
            /** 
             * Indicates that log level is at error level. 
             */ 
        NOT_SET(5, "5"); 
            /** 
             * Indicates that no log level is set. 
             */ 
        /** 
         * Converts the passed log level string to the corresponding {@link LogLevel}. 
         * 
         * @param logLevelVal The log level value which needs to convert 
         * @return The LogLevel Enum if pass in the valid string. 
         * The valid strings for {@link LogLevel} are: 
         * <ul> 
         * <li>VERBOSE: "verbose", "debug"</li> 
         * <li>INFO: "info", "information", "informational"</li> 
         * <li>WARNING: "warn", "warning"</li> 
         * <li>ERROR: "err", "error"</li> 
         * </ul> 
         * Returns NOT_SET if null is passed in. 
         * @throws IllegalArgumentException if the log level value is invalid. 
         */ 
        public static LogLevel fromString(String logLevelVal) 
        /** 
         * Converts the log level into a numeric representation used for comparisons. 
         * 
         * @return The numeric representation of the log level. 
         */ 
        public int getLogLevel() 
    } 
    @Fluent
    /** 
     * This class provides fluent API to write logs using {@link ClientLogger} and 
     * enrich them with additional context. 
     * 
     * <p> 
     * <strong>Code samples</strong> 
     * </p> 
     * 
     * <p> 
     * Logging event with context. 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.logging.loggingeventbuilder --> 
     * <pre> 
     * logger.atInfo() 
     *     .addKeyValue("key1", "value1") 
     *     .addKeyValue("key2", true) 
     *     .addKeyValue("key3", () -> getName()) 
     *     .log("A formattable message. Hello, {}", name); 
     * </pre> 
     * <!-- end com.azure.core.util.logging.loggingeventbuilder --> 
     */ 
    public final class LoggingEventBuilder { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Adds key with String value pair to the context of current log being created. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Adding string value to logging event context.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.clientlogger.atInfo --> 
         * <pre> 
         * logger.atInfo() 
         *     .addKeyValue("key", "value") 
         *     .log("A formattable message. Hello, {}", name); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.clientlogger.atInfo --> 
         * 
         * @param key String key. 
         * @param value String value. 
         * @return The updated {@code LoggingEventBuilder} object. 
         */ 
        public LoggingEventBuilder addKeyValue(String key, String value) 
        /** 
         * Adds key with Object value to the context of current log being created. 
         * If logging is enabled at given level, and object is not null, uses {@code value.toString()} to 
         * serialize object. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Adding string value to logging event context.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#object --> 
         * <pre> 
         * logger.atVerbose() 
         *     // equivalent to addKeyValue("key", () -> new LoggableObject("string representation").toString() 
         *     .addKeyValue("key", new LoggableObject("string representation")) 
         *     .log("Param 1: {}, Param 2: {}, Param 3: {}", "param1", "param2", "param3"); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#object --> 
         * 
         * @param key String key. 
         * @param value Object value. 
         * @return The updated {@code LoggingEventBuilder} object. 
         */ 
        public LoggingEventBuilder addKeyValue(String key, Object value) 
        /** 
         * Adds key with boolean value to the context of current log being created. 
         * 
         * @param key String key. 
         * @param value boolean value. 
         * @return The updated {@code LoggingEventBuilder} object. 
         */ 
        public LoggingEventBuilder addKeyValue(String key, boolean value) 
        /** 
         * Adds key with long value to the context of current log event being created. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Adding an integer value to logging event context.</p> 
         * 
         * <!-- src_embed com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * <pre> 
         * logger.atVerbose() 
         *     .addKeyValue("key", 1L) 
         *     .log(() -> String.format("Param 1: %s, Param 2: %s, Param 3: %s", "param1", "param2", "param3")); 
         * </pre> 
         * <!-- end com.azure.core.util.logging.ClientLogger.atVerbose.addKeyValue#primitive --> 
         * 
         * @param key String key. 
         * @param value long value. 
         * @return The updated {@code LoggingEventBuilder} object. 
         */ 
        public LoggingEventBuilder addKeyValue(String key, long value) 
        /** 
         * Adds key with String value supplier to the context of current log event being created. 
         * 
         * @param key String key. 
         * @param valueSupplier String value supplier function. 
         * @return The updated {@code LoggingEventBuilder} object. 
         */ 
        public LoggingEventBuilder addKeyValue(String key, Supplier<String> valueSupplier) 
        /** 
         * Logs message annotated with context. 
         * 
         * @param message the message to log. 
         */ 
        public void log(String message) 
        /** 
         * Logs message annotated with context. 
         * 
         * @param messageSupplier string message supplier. 
         */ 
        public void log(Supplier<String> messageSupplier) 
        /** 
         * Logs the {@link Throwable} and returns it to be thrown. 
         * 
         * @param throwable Throwable to be logged and returned. 
         * @return The passed {@link Throwable}. 
         * @throws NullPointerException If {@code throwable} is {@code null}. 
         */ 
        public Throwable log(Throwable throwable) 
        /** 
         * Logs the {@link RuntimeException} and returns it to be thrown. 
         * This API covers the cases where a checked exception type needs to be thrown and logged. 
         * 
         * @param runtimeException RuntimeException to be logged and returned. 
         * @return The passed {@link RuntimeException}. 
         * @throws NullPointerException If {@code runtimeException} is {@code null}. 
         */ 
        public RuntimeException log(RuntimeException runtimeException) 
        /** 
         * Logs message annotated with context. 
         * 
         * @param messageSupplier string message supplier. 
         * @param throwable {@link Throwable} for the message. 
         */ 
        public void log(Supplier<String> messageSupplier, Throwable throwable) 
        /** 
         * Logs a format-able message that uses {@code {}} as the placeholder at {@code warning} log level. 
         * 
         * @param format The format-able message to log. 
         * @param args Arguments for the message. If an exception is being logged, the last argument should be the {@link 
         * Throwable}. 
         */ 
        public void log(String format, Object... args) 
    } 
} 
/** 
 * Package containing core utility classes. 
 */ 
package com.azure.core.util.metrics { 
    /** 
     *A histogram instrument that records {@code long} values. 
     */ 
    public interface DoubleHistogram { 
        /** 
         * Flag indicating if metric implementation is detected and functional, use it to minimize performance impact associated with metrics, 
         * e.g. measuring latency. 
         * 
         * @return {@code true} if enabled, {@code false} otherwise 
         */ 
        boolean isEnabled() 
        /** 
         * Records a value with a set of attributes. 
         * 
         * @param value The amount of the measurement. 
         * @param attributes Collection of attributes representing metric dimensions. 
         * @param context The explicit context to associate with this measurement. 
         */ 
        void record(double value, TelemetryAttributes attributes, Context context) 
    } 
    /** 
     * A counter instrument that records {@code long} values. 
     * 
     * <p> 
     * Counters only allow adding positive values, and guarantee the resulting metrics will be 
     * always-increasing monotonic sums. 
     */ 
    public interface LongCounter { 
        /** 
         * Records a value with a set of attributes. 
         * 
         * @param value The amount of the measurement. 
         * @param attributes Collection of attributes representing metric dimensions. 
         * @param context The explicit context to associate with this measurement. 
         */ 
        void add(long value, TelemetryAttributes attributes, Context context) 
        /** 
         * Flag indicating if metric implementation is detected and functional, use it to minimize performance impact associated with metrics, 
         * e.g. measuring latency. 
         * 
         * @return {@code true} if enabled, {@code false} otherwise 
         */ 
        boolean isEnabled() 
    } 
    /** 
     * A counter instrument that records {@code long} values. 
     * 
     * <p> 
     * Counters only allow adding positive values, and guarantee the resulting metrics will be 
     * always-increasing monotonic sums. 
     */ 
    public interface LongGauge { 
        /** 
         * Flag indicating if metric implementation is detected and functional, use it to minimize performance impact associated with metrics, 
         * e.g. measuring latency. 
         * 
         * @return {@code true} if enabled, {@code false} otherwise 
         */ 
        boolean isEnabled() 
        /** 
         * Registers callbacks to obtain measurements. Make sure to close result to stop reporting metric. 
         * 
         * @param valueSupplier Callback that will periodically be requested to obtain current value. 
         * @param attributes Collection of attributes representing metric dimensions. Caller that wants to 
         *                   record dynamic attributes, should register callback per each attribute combination. 
         * @return instance of {@link AutoCloseable} subscription. 
         */ 
        AutoCloseable registerCallback(Supplier<Long> valueSupplier, TelemetryAttributes attributes) 
    } 
    /** 
     * Meter is generally associated with Azure Service Client instance and allows creating 
     * instruments that represent individual metrics such as number of active connections or 
     * HTTP call latency. 
     * 
     * Choose instrument kind based on OpenTelemetry guidelines: 
     * https://opentelemetry.io/docs/reference/specification/metrics/api/#counter-creation 
     * 
     * This class is intended to be used by Azure client libraries and provides abstraction over different metrics 
     * implementations. 
     * Application developers should use metrics implementations such as OpenTelemetry or Micrometer directly. 
     * 
     * <!-- src_embed com.azure.core.util.metrics.Meter.doubleHistogram --> 
     * <pre> 
     * 
     * // Meter and instruments should be created along with service client instance and retained for the client 
     * // lifetime for optimal performance 
     * Meter meter = meterProvider 
     *     .createMeter("azure-core", "1.0.0", new MetricsOptions()); 
     * 
     * DoubleHistogram amqpLinkDuration = meter 
     *     .createDoubleHistogram("az.core.amqp.link.duration", "AMQP link response time.", "ms"); 
     * 
     * TelemetryAttributes attributes = defaultMeter.createAttributes( 
     *     Collections.singletonMap("endpoint", "http://service-endpoint.azure.com")); 
     * 
     * // when measured operation starts, record the measurement 
     * Instant start = Instant.now(); 
     * 
     * doThings(); 
     * 
     * // optionally check if meter is operational for the best performance 
     * if (amqpLinkDuration.isEnabled()) { 
     *     amqpLinkDuration.record(Instant.now().toEpochMilli() - start.toEpochMilli(), attributes, currentContext); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.util.metrics.Meter.doubleHistogram --> 
     */ 
    public interface Meter extends AutoCloseable { 
        /** 
         * {@inheritDoc} 
         */ 
        @Override void close() 
        /** 
         * Creates and returns attribute collection implementation specific to the meter implementation. 
         * Attribute collections differ in how they support different types of attributes and internal 
         * data structures they use. 
         * 
         * For the best performance, client libraries should create and cache attribute collections 
         * for the client lifetime and pass cached instance when recoding new measurements. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.Meter.longCounter#errorFlag --> 
         * <pre> 
         * 
         * // Create attributes for possible error codes. Can be done lazily once specific error code is received. 
         * TelemetryAttributes successAttributes = defaultMeter.createAttributes(new HashMap<String, Object>() {{ 
         *         put("endpoint", "http://service-endpoint.azure.com"); 
         *         put("error", true); 
         *     }}); 
         * 
         * TelemetryAttributes errorAttributes =  defaultMeter.createAttributes(new HashMap<String, Object>() {{ 
         *         put("endpoint", "http://service-endpoint.azure.com"); 
         *         put("error", false); 
         *     }}); 
         * 
         * LongCounter httpConnections = defaultMeter.createLongCounter("az.core.http.connections", 
         *     "Number of created HTTP connections", null); 
         * 
         * boolean success = false; 
         * try { 
         *     success = doThings(); 
         * } finally { 
         *     httpConnections.add(1, success ? successAttributes : errorAttributes, currentContext); 
         * } 
         * 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.Meter.longCounter#errorFlag --> 
         * @param attributeMap map of key value pairs to cache. 
         * @return an instance of {@code AttributesBuilder} 
         */ 
        TelemetryAttributes createAttributes(Map<String, Object> attributeMap) 
        /** 
         * Creates histogram instrument allowing to record long values. Histograms should be used for latency or other measurements where 
         * distribution of values is important and values are statistically bounded. 
         * 
         * See https://opentelemetry.io/docs/reference/specification/metrics/api/#histogram for more details. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.Meter.doubleHistogram --> 
         * <pre> 
         * 
         * // Meter and instruments should be created along with service client instance and retained for the client 
         * // lifetime for optimal performance 
         * Meter meter = meterProvider 
         *     .createMeter("azure-core", "1.0.0", new MetricsOptions()); 
         * 
         * DoubleHistogram amqpLinkDuration = meter 
         *     .createDoubleHistogram("az.core.amqp.link.duration", "AMQP link response time.", "ms"); 
         * 
         * TelemetryAttributes attributes = defaultMeter.createAttributes( 
         *     Collections.singletonMap("endpoint", "http://service-endpoint.azure.com")); 
         * 
         * // when measured operation starts, record the measurement 
         * Instant start = Instant.now(); 
         * 
         * doThings(); 
         * 
         * // optionally check if meter is operational for the best performance 
         * if (amqpLinkDuration.isEnabled()) { 
         *     amqpLinkDuration.record(Instant.now().toEpochMilli() - start.toEpochMilli(), attributes, currentContext); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.Meter.doubleHistogram --> 
         * 
         * @param name short histogram name following https://opentelemetry.io/docs/reference/specification/metrics/api/#instrument-naming-rule 
         * @param description free-form text describing the instrument 
         * @param unit optional unit of measurement. 
         * @return new instance of {@link DoubleHistogram} 
         * @throws NullPointerException if name or description is null. 
         */ 
        DoubleHistogram createDoubleHistogram(String name, String description, String unit) 
        /** 
         * Creates Counter instrument that is used to record incrementing values, such as number of sent messages or created 
         * connections. 
         * 
         * Use {@link Meter#createLongUpDownCounter(String, String, String)} for counters that can go down, 
         * such as number of active connections or queue size. 
         * 
         * See https://opentelemetry.io/docs/reference/specification/metrics/api/#counter for more details. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.Meter.longCounter --> 
         * <pre> 
         * TelemetryAttributes attributes = defaultMeter.createAttributes(new HashMap<String, Object>() {{ 
         *         put("endpoint", "http://service-endpoint.azure.com"); 
         *         put("status", "ok"); 
         *     }}); 
         * 
         * LongCounter createdHttpConnections = defaultMeter.createLongCounter("az.core.http.connections", 
         *     "Number of created HTTP connections", null); 
         * 
         * createdHttpConnections.add(1, attributes, currentContext); 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.Meter.longCounter --> 
         * 
         * @param name short counter  name following https://opentelemetry.io/docs/reference/specification/metrics/api/#instrument-naming-rule 
         * @param description free-form text describing the counter 
         * @param unit optional unit of measurement. 
         * @return new instance of {@link LongCounter} 
         * @throws NullPointerException if name or description is null. 
         */ 
        LongCounter createLongCounter(String name, String description, String unit) 
        /** 
         * Creates {@link LongGauge} instrument that is used to asynchronously record current value of metric. 
         * 
         * See https://opentelemetry.io/docs/reference/specification/metrics/api/#asynchronous-gauge for more details. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.Meter.longGauge --> 
         * <pre> 
         * TelemetryAttributes attributes = defaultMeter.createAttributes(new HashMap<String, Object>() {{ 
         *         put("endpoint", "http://service-endpoint.azure.com"); 
         *         put("container", "my-container"); 
         *     }}); 
         * 
         * LongGauge latestSequenceNumber = defaultMeter.createLongGauge("az.eventhubs.consumer.sequence_number", 
         *     "Sequence number of the latest event received from the broker.", null); 
         * 
         * AutoCloseable subscription = latestSequenceNumber.registerCallback(sequenceNumber::get, attributes); 
         * 
         * // update value when event is received 
         * sequenceNumber.set(getSequenceNumber()); 
         * 
         * try { 
         *     subscription.close(); 
         * } catch (Exception e) { 
         *     e.printStackTrace(); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.Meter.longGauge --> 
         * 
         * @param name short counter  name following https://opentelemetry.io/docs/reference/specification/metrics/api/#instrument-naming-rule 
         * @param description free-form text describing the counter 
         * @param unit optional unit of measurement. 
         * @return new instance of {@link LongGauge} 
         * @throws NullPointerException if name or description is null. 
         */ 
        default LongGauge createLongGauge(String name, String description, String unit) 
        /** 
         * Creates UpDownCounter instrument that is used to record values that can go up or down, such as number of active 
         * connections or queue size. 
         * 
         * See https://opentelemetry.io/docs/reference/specification/metrics/api/#updowncounter for more details. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.Meter.upDownCounter --> 
         * <pre> 
         * TelemetryAttributes attributes = defaultMeter.createAttributes(new HashMap<String, Object>() {{ 
         *         put("endpoint", "http://service-endpoint.azure.com"); 
         *         put("status", "ok"); 
         *     }}); 
         * 
         * LongCounter activeHttpConnections = defaultMeter.createLongUpDownCounter("az.core.http.active.connections", 
         *     "Number of active HTTP connections", null); 
         * 
         * // on connection initialized: 
         * activeHttpConnections.add(1, attributes, currentContext); 
         * 
         * // on connection closed: 
         * activeHttpConnections.add(-1, attributes, currentContext); 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.Meter.upDownCounter --> 
         * 
         * @param name short counter name following https://opentelemetry.io/docs/reference/specification/metrics/api/#instrument-naming-rule 
         * @param description free-form text describing the counter 
         * @param unit optional unit of measurement. 
         * @return new instance of {@link LongCounter} 
         * @throws NullPointerException if name or description is null. 
         */ 
        LongCounter createLongUpDownCounter(String name, String description, String unit) 
        /** 
         * Checks if Meter implementation was found, and it's enabled. 
         * 
         * @return true if Meter is enabled, false otherwise. 
         */ 
        boolean isEnabled() 
    } 
    /** 
     * Resolves and provides {@link Meter} implementation. 
     * <p> 
     * This class is intended to be used by Azure client libraries and provides abstraction over different metrics 
     * implementations. 
     * Application developers should use metrics implementations such as OpenTelemetry or Micrometer directly. 
     */ 
    public interface MeterProvider { 
        /** 
         * Creates meter instance. 
         * 
         * @param libraryOptions Azure SDK telemetry options. 
         * @param applicationOptions instance of {@link MetricsOptions} provided by the application. 
         * @return a meter instance. 
         */ 
        default Meter createMeter(LibraryTelemetryOptions libraryOptions, MetricsOptions applicationOptions) 
        /** 
         * Creates named and versioned meter instance. 
         * 
         * <!-- src_embed com.azure.core.util.metrics.MeterProvider.createMeter --> 
         * <pre> 
         * MetricsOptions metricsOptions = new MetricsOptions(); 
         * 
         * Meter meter = MeterProvider.getDefaultProvider().createMeter("azure-core", "1.0.0", metricsOptions); 
         * </pre> 
         * <!-- end com.azure.core.util.metrics.MeterProvider.createMeter --> 
         * 
         * @param libraryName Azure client library package name 
         * @param libraryVersion Azure client library version 
         * @param applicationOptions instance of {@link MetricsOptions} provided by the application. 
         * @return a meter instance. 
         */ 
        Meter createMeter(String libraryName, String libraryVersion, MetricsOptions applicationOptions) 
        /** 
         * Returns default implementation of {@code MeterProvider} that uses SPI to resolve metrics implementation. 
         * @return an instance of {@code MeterProvider} 
         */ 
        static MeterProvider getDefaultProvider() 
    } 
} 
/** 
 * Package containing paging abstraction. 
 */ 
package com.azure.core.util.paging { 
    /** 
     * Represents a page returned, this page may contain a reference to additional pages known as a continuation token. 
     * 
     * @param <C> Type of the continuation token. 
     * @param <T> Type of the elements in the page. 
     * @see ContinuablePagedFlux 
     */ 
    public interface ContinuablePage<C, T> { 
        /** 
         * Gets the reference to the next page. 
         * 
         * @return The next page reference or {@code null} if there isn't a next page. 
         */ 
        C getContinuationToken() 
        /** 
         * Gets an {@link IterableStream} of elements in the page. 
         * 
         * @return An {@link IterableStream} containing the elements in the page. 
         */ 
        IterableStream<T> getElements() 
    } 
    /** 
     * This class is a {@link Flux} implementation that provides the ability to operate on pages of type {@link 
     * ContinuablePage} and individual items in such pages. This type supports user-provided continuation tokens, allowing 
     * for restarting from a previously-retrieved continuation token. 
     * 
     * @param <C> Type of the continuation token. 
     * @param <T> Type of the elements in the page. 
     * @param <P> Type of the page. 
     * @see Flux 
     * @see ContinuablePage 
     */ 
    public abstract class ContinuablePagedFlux<C, T, P extends ContinuablePage<C, T>> extends Flux<T> { 
        /** 
         * Creates an instance of ContinuablePagedFlux. 
         * <p> 
         * Continuation completes when the last returned continuation token is null. 
         */ 
        public ContinuablePagedFlux() 
        /** 
         * Creates an instance of ContinuablePagedFlux. 
         * <p> 
         * If {@code continuationPredicate} is null then the predicate will only check if the continuation token is 
         * non-null. 
         * 
         * @param continuationPredicate A predicate which determines if paging should continue. 
         */ 
        protected ContinuablePagedFlux(Predicate<C> continuationPredicate) 
        /** 
         * Gets a {@link Flux} of {@link ContinuablePage} starting at the first page. 
         * 
         * @return A {@link Flux} of {@link ContinuablePage}. 
         */ 
        public abstract Flux<P> byPage() 
        /** 
         * Gets a {@link Flux} of {@link ContinuablePage} beginning at the page identified by the given continuation token. 
         * 
         * @param continuationToken A continuation token identifying the page to select. 
         * @return A {@link Flux} of {@link ContinuablePage}. 
         */ 
        public abstract Flux<P> byPage(C continuationToken) 
        /** 
         * Gets a {@link Flux} of {@link ContinuablePage} starting at the first page requesting each page to contain a 
         * number of elements equal to the preferred page size. 
         * <p> 
         * The service may or may not honor the preferred page size therefore the client <em>MUST</em> be prepared to handle 
         * pages with different page sizes. 
         * 
         * @param preferredPageSize The preferred page size. 
         * @return A {@link Flux} of {@link ContinuablePage}. 
         */ 
        public abstract Flux<P> byPage(int preferredPageSize) 
        /** 
         * Gets a {@link Flux} of {@link ContinuablePage} beginning at the page identified by the given continuation token 
         * requesting each page to contain the number of elements equal to the preferred page size. 
         * <p> 
         * The service may or may not honor the preferred page size therefore the client <em>MUST</em> be prepared to handle 
         * pages with different page sizes. 
         * 
         * @param continuationToken A continuation token identifying the page to select. 
         * @param preferredPageSize The preferred page size. 
         * @return A {@link Flux} of {@link ContinuablePage}. 
         */ 
        public abstract Flux<P> byPage(C continuationToken, int preferredPageSize) 
        /** 
         * Gets the {@link Predicate} that determines if paging should continue. 
         * 
         * @return The {@link Predicate} that determines if paging should continue. 
         */ 
        protected final Predicate<C> getContinuationPredicate() 
    } 
    /** 
     * The default implementation of {@link ContinuablePagedFlux}. 
     * <p> 
     * This type is a Flux that provides the ability to operate on pages of type {@link ContinuablePage} and individual 
     * items in such pages. This type supports user-provided continuation tokens, allowing for restarting from a 
     * previously-retrieved continuation token. 
     * <p> 
     * The type is backed by the Page Retriever provider provided in it's constructor. The provider is expected to return 
     * {@link PageRetriever} when called. The provider is invoked for each Subscription to this Flux. Given provider is 
     * called per Subscription, the provider implementation can create one or more objects to store any state and Page 
     * Retriever can capture and use those objects. This indirectly associate the state objects to the Subscription. The 
     * Page Retriever can get called multiple times in serial fashion, each time after the completion of the Flux returned 
     * by the previous invocation. The final completion signal will be send to the Subscriber when the last Page emitted by 
     * the Flux returned by the Page Retriever has {@code null} continuation token. 
     * 
     * <p> 
     * <strong>Extending PagedFluxCore for Custom Continuation Token support</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.paging.pagedfluxcore.continuationtoken --> 
     * <pre> 
     * class ContinuationState<C> { 
     *     private C lastContinuationToken; 
     *     private boolean isDone; 
     * 
     *     ContinuationState(C token) { 
     *         this.lastContinuationToken = token; 
     *     } 
     * 
     *     void setLastContinuationToken(C token) { 
     *         this.isDone = token == null; 
     *         this.lastContinuationToken = token; 
     *     } 
     * 
     *     C getLastContinuationToken() { 
     *         return this.lastContinuationToken; 
     *     } 
     * 
     *     boolean isDone() { 
     *         return this.isDone; 
     *     } 
     * } 
     * 
     * class FileContinuationToken { 
     *     private final int nextLinkId; 
     * 
     *     FileContinuationToken(int nextLinkId) { 
     *         this.nextLinkId = nextLinkId; 
     *     } 
     * 
     *     public int getNextLinkId() { 
     *         return nextLinkId; 
     *     } 
     * } 
     * 
     * class File { 
     *     private final String guid; 
     * 
     *     File(String guid) { 
     *         this.guid = guid; 
     *     } 
     * 
     *     public String getGuid() { 
     *         return guid; 
     *     } 
     * } 
     * 
     * class FilePage implements ContinuablePage<FileContinuationToken, File> { 
     *     private final IterableStream<File> elements; 
     *     private final FileContinuationToken fileContinuationToken; 
     * 
     *     FilePage(List<File> elements, FileContinuationToken fileContinuationToken) { 
     *         this.elements = IterableStream.of(elements); 
     *         this.fileContinuationToken = fileContinuationToken; 
     *     } 
     * 
     *     @Override 
     *     public IterableStream<File> getElements() { 
     *         return elements; 
     *     } 
     * 
     *     @Override 
     *     public FileContinuationToken getContinuationToken() { 
     *         return fileContinuationToken; 
     *     } 
     * } 
     * 
     * class FileShareServiceClient { 
     *     Flux<FilePage> getFilePages(FileContinuationToken token) { 
     *         List<File> files = Collections.singletonList(new File(UUID.randomUUID().toString())); 
     *         if (token.getNextLinkId() < 10) { 
     *             return Flux.just(new FilePage(files, null)); 
     *         } else { 
     *             return Flux.just(new FilePage(files, 
     *                 new FileContinuationToken((int) Math.floor(Math.random() * 20)))); 
     *         } 
     *     } 
     * } 
     * 
     * FileShareServiceClient client = new FileShareServiceClient(); 
     * 
     * Supplier<PageRetriever<FileContinuationToken, FilePage>> pageRetrieverProvider = () -> 
     *     (continuationToken, pageSize) -> client.getFilePages(continuationToken); 
     * 
     * class FilePagedFlux extends ContinuablePagedFluxCore<FileContinuationToken, File, FilePage> { 
     *     FilePagedFlux(Supplier<PageRetriever<FileContinuationToken, FilePage>> 
     *         pageRetrieverProvider) { 
     *         super(pageRetrieverProvider); 
     *     } 
     * } 
     * 
     * FilePagedFlux filePagedFlux = new FilePagedFlux(pageRetrieverProvider); 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.paging.pagedfluxcore.continuationtoken --> 
     * 
     * @param <C> the type of the continuation token 
     * @param <T> The type of elements in a {@link ContinuablePage} 
     * @param <P> The {@link ContinuablePage} holding items of type {@code T}. 
     * @see ContinuablePagedFlux 
     * @see ContinuablePage 
     */ 
    public abstract class ContinuablePagedFluxCore<C, T, P extends ContinuablePage<C, T>> extends ContinuablePagedFlux<C, T, P> { 
        /** 
         * Creates an instance of {@link ContinuablePagedFluxCore}. 
         * 
         * @param pageRetrieverProvider a provider that returns {@link PageRetriever}. 
         * @throws NullPointerException If {@code pageRetrieverProvider} is null. 
         */ 
        protected ContinuablePagedFluxCore(Supplier<PageRetriever<C, P>> pageRetrieverProvider) 
        /** 
         * Creates an instance of {@link ContinuablePagedFluxCore}. 
         * 
         * @param pageRetrieverProvider a provider that returns {@link PageRetriever}. 
         * @param pageSize the preferred page size 
         * @throws NullPointerException If {@code pageRetrieverProvider} is null. 
         * @throws IllegalArgumentException If {@code pageSize} is less than or equal to zero. 
         */ 
        protected ContinuablePagedFluxCore(Supplier<PageRetriever<C, P>> pageRetrieverProvider, int pageSize) 
        /** 
         * Creates an instance of {@link ContinuablePagedFluxCore}. 
         * 
         * @param pageRetrieverProvider A provider that returns {@link PageRetriever}. 
         * @param pageSize The preferred page size. 
         * @param continuationPredicate A predicate which determines if paging should continue. 
         * @throws NullPointerException If {@code pageRetrieverProvider} is null. 
         * @throws IllegalArgumentException If {@code pageSize} is not null and is less than or equal to zero. 
         */ 
        protected ContinuablePagedFluxCore(Supplier<PageRetriever<C, P>> pageRetrieverProvider, Integer pageSize, Predicate<C> continuationPredicate) 
        @Override public Flux<P> byPage() 
        @Override public Flux<P> byPage(C continuationToken) 
        @Override public Flux<P> byPage(int preferredPageSize) 
        @Override public Flux<P> byPage(C continuationToken, int preferredPageSize) 
        /** 
         * Get the page size configured this {@link ContinuablePagedFluxCore}. 
         * 
         * @return the page size configured, {@code null} if unspecified. 
         */ 
        public Integer getPageSize() 
        /** 
         * Subscribe to consume all items of type {@code T} in the sequence respectively. This is recommended for most 
         * common scenarios. This will seamlessly fetch next page when required and provide with a {@link Flux} of items. 
         * 
         * @param coreSubscriber The subscriber for this {@link ContinuablePagedFluxCore} 
         */ 
        @Override public void subscribe(CoreSubscriber<? super T> coreSubscriber) 
    } 
    /** 
     * This class provides utility to iterate over {@link ContinuablePage} using {@link Stream} {@link Iterable} 
     * interfaces. 
     * 
     * @param <C> the type of the continuation token 
     * @param <T> The type of elements in a {@link ContinuablePage} 
     * @param <P> The {@link ContinuablePage} holding items of type {@code T}. 
     * @see IterableStream 
     * @see ContinuablePagedFlux 
     */ 
    public class ContinuablePagedIterable<C, T, P extends ContinuablePage<C, T>> extends IterableStream<T> { 
        /** 
         * Creates instance with the given {@link ContinuablePagedFlux}. 
         * 
         * @param pagedFlux the paged flux use as iterable 
         */ 
        public ContinuablePagedIterable(ContinuablePagedFlux<C, T, P> pagedFlux) 
        /** 
         * Creates instance with the given {@link ContinuablePagedFlux}. 
         * 
         * @param pagedFlux the paged flux use as iterable 
         * @param batchSize the bounded capacity to prefetch from the {@link ContinuablePagedFlux} 
         */ 
        public ContinuablePagedIterable(ContinuablePagedFlux<C, T, P> pagedFlux, int batchSize) 
        /** 
         * Creates instance with the given {@link PageRetrieverSync provider}. 
         * 
         * @param pageRetrieverSyncProvider A provider that returns {@link PageRetrieverSync}. 
         * @param pageSize The preferred page size. 
         * @param continuationPredicate A predicate which determines if paging should continue. 
         * @throws NullPointerException If {@code pageRetrieverSyncProvider} is null. 
         * @throws IllegalArgumentException If {@code pageSize} is not null and is less than or equal to zero. 
         */ 
        public ContinuablePagedIterable(Supplier<PageRetrieverSync<C, P>> pageRetrieverSyncProvider, Integer pageSize, Predicate<C> continuationPredicate) 
        /** 
         * Retrieve the {@link Iterable}, one page at a time. It will provide same {@link Iterable} of T values from 
         * starting if called multiple times. 
         * 
         * @return {@link Stream} of a pages 
         */ 
        public Iterable<P> iterableByPage() 
        /** 
         * Retrieve the {@link Iterable}, one page at a time, starting from the next page associated with the given 
         * continuation token. To start from first page, use {@link #iterableByPage()} instead. 
         * 
         * @param continuationToken The continuation token used to fetch the next page 
         * @return {@link Iterable} of a pages 
         */ 
        public Iterable<P> iterableByPage(C continuationToken) 
        /** 
         * Retrieve the {@link Iterable}, one page at a time, with each page containing {@code preferredPageSize} items. 
         * <p> 
         * It will provide same {@link Iterable} of T values from starting if called multiple times. 
         * 
         * @param preferredPageSize the preferred page size, service may or may not honor the page size preference hence 
         * client MUST be prepared to handle pages with different page size. 
         * @return {@link Iterable} of a pages 
         */ 
        public Iterable<P> iterableByPage(int preferredPageSize) 
        /** 
         * Retrieve the {@link Iterable}, one page at a time, with each page containing {@code preferredPageSize} items, 
         * starting from the next page associated with the given continuation token. To start from first page, use {@link 
         * #iterableByPage()} or {@link #iterableByPage(int)} instead. 
         * 
         * @param preferredPageSize the preferred page size, service may or may not honor the page size preference hence 
         * client MUST be prepared to handle pages with different page size. 
         * @param continuationToken The continuation token used to fetch the next page 
         * @return {@link Iterable} of a pages 
         */ 
        public Iterable<P> iterableByPage(C continuationToken, int preferredPageSize) 
        @Override public Iterator<T> iterator() 
        @Override public Stream<T> stream() 
        /** 
         * Retrieve the {@link Stream}, one page at a time. It will provide same {@link Stream} of T values from starting if 
         * called multiple times. 
         * 
         * @return {@link Stream} of a pages 
         */ 
        public Stream<P> streamByPage() 
        /** 
         * Retrieve the {@link Stream}, one page at a time, starting from the next page associated with the given 
         * continuation token. To start from first page, use {@link #streamByPage()} instead. 
         * 
         * @param continuationToken The continuation token used to fetch the next page 
         * @return {@link Stream} of a pages 
         */ 
        public Stream<P> streamByPage(C continuationToken) 
        /** 
         * Retrieve the {@link Stream}, one page at a time, with each page containing {@code preferredPageSize} items. 
         * <p> 
         * It will provide same {@link Stream} of T values from starting if called multiple times. 
         * 
         * @param preferredPageSize the preferred page size, service may or may not honor the page size preference hence 
         * client MUST be prepared to handle pages with different page size. 
         * @return {@link Stream} of a pages 
         */ 
        public Stream<P> streamByPage(int preferredPageSize) 
        /** 
         * Retrieve the {@link Stream}, one page at a time, with each page containing {@code preferredPageSize} items, 
         * starting from the next page associated with the given continuation token. To start from first page, use {@link 
         * #streamByPage()} or {@link #streamByPage(int)} instead. 
         * 
         * @param preferredPageSize the preferred page size, service may or may not honor the page size preference hence 
         * client MUST be prepared to handle pages with different page size. 
         * @param continuationToken The continuation token used to fetch the next page 
         * @return {@link Stream} of a pages 
         */ 
        public Stream<P> streamByPage(C continuationToken, int preferredPageSize) 
    } 
    @FunctionalInterface
    /** 
     * This class handles retrieving pages. 
     * 
     * @param <C> Type of the continuation token. 
     * @param <P> the page elements type 
     */ 
    public interface PageRetriever<C, P> { 
        /** 
         * Retrieves one or more pages starting from the page identified by the given continuation token. 
         * 
         * @param continuationToken Token identifying which page to retrieve, passing {@code null} indicates to retrieve 
         * the first page. 
         * @param pageSize The number of items to retrieve per page, passing {@code null} will use the source's default 
         * page size. 
         * @return A {@link Flux} that emits one or more pages. 
         */ 
        Flux<P> get(C continuationToken, Integer pageSize) 
    } 
    @FunctionalInterface
    /** 
     * This class handles retrieving page synchronously. 
     * 
     * @param <C> Type of the continuation token. 
     * @param <P> the page elements type 
     */ 
    public interface PageRetrieverSync<C, P> { 
        /** 
         * Retrieves one starting from the page identified by the given continuation token. 
         * 
         * @param continuationToken Token identifying which page to retrieve, passing {@code null} indicates to retrieve 
         * the first page. 
         * @param pageSize The number of items to retrieve per page, passing {@code null} will use the source's default 
         * page size. 
         * @return A page of elements type <P>. 
         */ 
        P getPage(C continuationToken, Integer pageSize) 
    } 
} 
/** 
 * <p>This package contains utility classes and interfaces for handling long-running operations in the 
 * Azure client libraries.</p> 
 * 
 * <p>Long-running operations are operations such as the creation or deletion of a resource, which take a significant 
 * amount of time to complete. These operations are typically handled asynchronously, with the client initiating the 
 * operation and then polling the service at intervals to determine whether the operation has completed.</p> 
 * 
 * <p>This package provides a standard mechanism for initiating, tracking, and retrieving the results of long-running 
 * operations</p> 
 * 
 * <p><strong>Code Sample: Asynchronously wait for polling to complete and then retrieve the final result</strong></p> 
 * 
 * <!-- src_embed com.azure.core.util.polling.poller.getResult --> 
 * <pre> 
 * LocalDateTime timeToReturnFinalResponse = LocalDateTime.now().plus(Duration.ofMinutes(5)); 
 * 
 * // Create poller instance 
 * PollerFlux<String, String> poller = new PollerFlux<>(Duration.ofMillis(100), 
 *     (context) -> Mono.empty(), 
 *     (context) ->  { 
 *         if (LocalDateTime.now().isBefore(timeToReturnFinalResponse)) { 
 *             System.out.println("Returning intermediate response."); 
 *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, 
 *                     "Operation in progress.")); 
 *         } else { 
 *             System.out.println("Returning final response."); 
 *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.SUCCESSFULLY_COMPLETED, 
 *                     "Operation completed.")); 
 *         } 
 *     }, 
 *     (activationResponse, context) -> Mono.just("FromServer:OperationIsCancelled"), 
 *     (context) -> Mono.just("FromServer:FinalOutput")); 
 * 
 * poller.take(Duration.ofMinutes(30)) 
 *         .last() 
 *         .flatMap(asyncPollResponse -> { 
 *             if (asyncPollResponse.getStatus() == LongRunningOperationStatus.SUCCESSFULLY_COMPLETED) { 
 *                 // operation completed successfully, retrieving final result. 
 *                 return asyncPollResponse 
 *                         .getFinalResult(); 
 *             } else { 
 *                 return Mono.error(new RuntimeException("polling completed unsuccessfully with status:" 
 *                         + asyncPollResponse.getStatus())); 
 *             } 
 *         }).block(); 
 * 
 * </pre> 
 * <!-- end com.azure.core.util.polling.poller.getResult --> 
 * 
 * <p><strong>Code Sample: Using a SimpleSyncPoller to poll until the operation is successfully completed</strong></p> 
 * 
 * <!-- src_embed com.azure.core.util.polling.simpleSyncPoller.instantiationAndPoll --> 
 * <pre> 
 * LongRunningOperationStatus operationStatus = syncPoller.poll().getStatus(); 
 * while (operationStatus != LongRunningOperationStatus.SUCCESSFULLY_COMPLETED) { 
 *     System.out.println("Polling status: " + operationStatus.toString()); 
 *     System.out.println("Polling response: " + operationStatus.toString()); 
 *     operationStatus = syncPoller.poll().getStatus(); 
 * } 
 * </pre> 
 * <!-- end com.azure.core.util.polling.simpleSyncPoller.instantiationAndPoll --> 
 * 
 * 
 * @see com.azure.core.util.polling.PollerFlux 
 * @see com.azure.core.util.polling.SimpleSyncPoller 
 */ 
package com.azure.core.util.polling { 
    /** 
     * AsyncPollResponse represents an event emitted by the {@link PollerFlux} that asynchronously polls 
     * a long-running operation (LRO). An AsyncPollResponse event provides information such as the current 
     * {@link LongRunningOperationStatus status} of the LRO, any {@link #getValue value} returned 
     * by the poll, as well as other useful information provided by the service. 
     * AsyncPollResponse also exposes {@link #cancelOperation} method to cancel the long-running operation 
     * from reactor operator chain and {@link #getFinalResult()} method that returns final result of 
     * the long-running operation. 
     * 
     * @param <T> The type of poll response value. 
     * @param <U> The type of the final result of long-running operation. 
     */ 
    public final class AsyncPollResponse<T, U> { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets a {@link Mono} whereupon subscription it cancels the remote long-running operation if cancellation is 
         * supported by the service. 
         * 
         * @return A {@link Mono} whereupon subscription it cancels the remote long-running operation if cancellation 
         * is supported by the service. 
         */ 
        public Mono<T> cancelOperation() 
        /** 
         * Gets a {@link Mono} whereupon subscription it fetches the final result of the long-running operation if it is 
         * supported by the service. 
         * <p> 
         * If the long-running operation isn't complete an empty {@link Mono} will be returned. 
         * 
         * @return A {@link Mono} whereupon subscription it fetches the final result of the long-running operation if it is 
         * supported by the service. If the long-running operation is not completed, then an empty {@link Mono} will be 
         * returned. 
         */ 
        public Mono<U> getFinalResult() 
        /** 
         * Represents the status of the long-running operation at the time the last polling operation finished successfully. 
         * 
         * @return A {@link LongRunningOperationStatus} representing the result of the poll operation. 
         */ 
        public LongRunningOperationStatus getStatus() 
        /** 
         * The value returned as a result of the last successful poll operation. This can be any custom user defined object, 
         * or null if no value was returned from the service. 
         * 
         * @return T result of poll operation. 
         */ 
        public T getValue() 
    } 
    /** 
     * A polling strategy that chains multiple polling strategies, finds the first strategy that can poll the current 
     * long-running operation, and polls with that strategy. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public final class ChainedPollingStrategy<T, U> implements PollingStrategy<T, U> { 
        /** 
         * Creates a chained polling strategy with a list of polling strategies. 
         * @param strategies the list of polling strategies 
         * @throws NullPointerException If {@code strategies} is null. 
         * @throws IllegalArgumentException If {@code strategies} is an empty list. 
         */ 
        public ChainedPollingStrategy(List<PollingStrategy<T, U>> strategies) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public Mono<T> cancel(PollingContext<T> pollingContext, PollResponse<T> initialResponse) 
        @Override public Mono<Boolean> canPoll(Response<?> initialResponse) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public Mono<PollResponse<T>> poll(PollingContext<T> context, TypeReference<T> pollResponseType) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public Mono<U> getResult(PollingContext<T> context, TypeReference<U> resultType) 
    } 
    /** 
     * The default polling strategy to use with Azure data plane services. The default polling strategy will attempt 3 
     * known strategies, {@link OperationResourcePollingStrategy}, {@link LocationPollingStrategy}, and 
     * {@link StatusCheckPollingStrategy}, in this order. The first strategy that can poll on the initial response will be 
     * used. The created chained polling strategy is capable of handling most of the polling scenarios in Azure. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public final class DefaultPollingStrategy<T, U> implements PollingStrategy<T, U> { 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link OperationResourcePollingStrategy}, 
         * {@link LocationPollingStrategy}, and {@link StatusCheckPollingStrategy}, in this order, with a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public DefaultPollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link OperationResourcePollingStrategy}, 
         * {@link LocationPollingStrategy}, and {@link StatusCheckPollingStrategy}, in this order, with a custom 
         * serializer. 
         * 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException If {@code pollingStrategyOptions} is null. 
         */ 
        public DefaultPollingStrategy(PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link OperationResourcePollingStrategy}, 
         * {@link LocationPollingStrategy}, and {@link StatusCheckPollingStrategy}, in this order, with a custom 
         * serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public DefaultPollingStrategy(HttpPipeline httpPipeline, JsonSerializer serializer) 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link OperationResourcePollingStrategy}, 
         * {@link LocationPollingStrategy}, and {@link StatusCheckPollingStrategy}, in this order, with a custom 
         * serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public DefaultPollingStrategy(HttpPipeline httpPipeline, JsonSerializer serializer, Context context) 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link OperationResourcePollingStrategy}, 
         * {@link LocationPollingStrategy}, and {@link StatusCheckPollingStrategy}, in this order, with a custom 
         * serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with. 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses. 
         * @param context an instance of {@link Context}. 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public DefaultPollingStrategy(HttpPipeline httpPipeline, String endpoint, JsonSerializer serializer, Context context) 
        @Override public Mono<Boolean> canPoll(Response<?> initialResponse) 
        @Override public Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<PollResponse<T>> poll(PollingContext<T> context, TypeReference<T> pollResponseType) 
        @Override public Mono<U> getResult(PollingContext<T> context, TypeReference<U> resultType) 
    } 
    /** 
     * Implements a Location polling strategy. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class LocationPollingStrategy<T, U> implements PollingStrategy<T, U> { 
        /** 
         * Creates an instance of the location polling strategy using a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public LocationPollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException If {@code pollingStrategyOptions} is null. 
         */ 
        public LocationPollingStrategy(PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public LocationPollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public LocationPollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, Context context) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public LocationPollingStrategy(HttpPipeline httpPipeline, String endpoint, ObjectSerializer serializer, Context context) 
        @Override public Mono<Boolean> canPoll(Response<?> initialResponse) 
        @Override public Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<PollResponse<T>> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<U> getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * An enum to represent all possible states that a long-running operation may find itself in. 
     * The poll operation is considered complete when the status is one of {@code SUCCESSFULLY_COMPLETED}, 
     * {@code USER_CANCELLED} or {@code FAILED}. 
     */ 
    public final class LongRunningOperationStatus extends ExpandableStringEnum<LongRunningOperationStatus> { 
        /** 
         *Represents that polling has not yet started for this long-running operation. 
         */ 
        public static final LongRunningOperationStatus NOT_STARTED = fromString("NOT_STARTED", false); 
        /** 
         *Represents that the long-running operation is in progress and not yet complete. 
         */ 
        public static final LongRunningOperationStatus IN_PROGRESS = fromString("IN_PROGRESS", false); 
        /** 
         *Represent that the long-running operation is completed successfully. 
         */ 
        public static final LongRunningOperationStatus SUCCESSFULLY_COMPLETED = fromString("SUCCESSFULLY_COMPLETED", true); 
        /** 
         * Represents that the long-running operation has failed to successfully complete, however this is still 
         * considered as complete long-running operation, meaning that the {@link PollerFlux} instance will report 
         * that it is complete. 
         */ 
        public static final LongRunningOperationStatus FAILED = fromString("FAILED", true); 
        /** 
         * Represents that the long-running operation is cancelled by user, however this is still 
         * considered as complete long-running operation. 
         */ 
        public static final LongRunningOperationStatus USER_CANCELLED = fromString("USER_CANCELLED", true); 
        /** 
         * Creates a new instance of {@link LongRunningOperationStatus} without a {@link #toString()} value. 
         * <p> 
         * This constructor shouldn't be called as it will produce a {@link LongRunningOperationStatus} which doesn't 
         * have a String enum value. 
         * 
         * @deprecated Use one of the constants or the {@link #fromString(String, boolean)} factory method. 
         */ 
        @Deprecated public LongRunningOperationStatus() 
        /** 
         * Returns a boolean to represent if the operation is in a completed state or not. 
         * @return True if the operation is in a completed state, otherwise false. 
         */ 
        public boolean isComplete() 
        /** 
         * Creates or finds a {@link LongRunningOperationStatus} from its string representation. 
         * @param name a name to look for 
         * @param isComplete a status to indicate if the operation is complete or not. 
         * @throws IllegalArgumentException if {@code name} matches a pre-configured {@link LongRunningOperationStatus} but 
         * {@code isComplete} doesn't match its pre-configured complete status. 
         * @return the corresponding {@link LongRunningOperationStatus} 
         */ 
        public static LongRunningOperationStatus fromString(String name, boolean isComplete) 
    } 
    /** 
     * Implements an operation resource polling strategy, typically from Operation-Location. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class OperationResourcePollingStrategy<T, U> implements PollingStrategy<T, U> { 
        /** 
         * Creates an instance of the operation resource polling strategy using a JSON serializer and "Operation-Location" 
         * as the header for polling. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         */ 
        public OperationResourcePollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param operationLocationHeaderName a custom header for polling the long-running operation. 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException if {@code pollingStrategyOptions} is null. 
         */ 
        public OperationResourcePollingStrategy(HttpHeaderName operationLocationHeaderName, PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param operationLocationHeaderName a custom header for polling the long-running operation 
         */ 
        public OperationResourcePollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, String operationLocationHeaderName) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param operationLocationHeaderName a custom header for polling the long-running operation 
         * @param context an instance of {@link com.azure.core.util.Context} 
         */ 
        public OperationResourcePollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, String operationLocationHeaderName, Context context) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with. 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses. 
         * @param operationLocationHeaderName a custom header for polling the long-running operation. 
         * @param context an instance of {@link com.azure.core.util.Context}. 
         */ 
        public OperationResourcePollingStrategy(HttpPipeline httpPipeline, String endpoint, ObjectSerializer serializer, String operationLocationHeaderName, Context context) 
        @Override public Mono<Boolean> canPoll(Response<?> initialResponse) 
        @Override public Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<PollResponse<T>> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<U> getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    @Immutable
    /** 
     *PollOperationDetails provides details for long running operations. 
     */ 
    public final class PollOperationDetails implements JsonSerializable<PollOperationDetails> { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets the error object that describes the error when status is "Failed". 
         * 
         * @return the error object that describes the error when status is "Failed". 
         */ 
        public ResponseError getError() 
        /** 
         * Reads a JSON stream into a {@link PollOperationDetails}. 
         * 
         * @param jsonReader The {@link JsonReader} being read. 
         * @return The {@link PollOperationDetails} that the JSON stream represented, or null if it pointed to JSON null. 
         * @throws IllegalStateException If the deserialized JSON object was missing any required properties. 
         * @throws IOException If a {@link PollOperationDetails} fails to be read from the {@code jsonReader}. 
         */ 
        public static PollOperationDetails fromJson(JsonReader jsonReader) throws IOException
        /** 
         * Gets the unique ID of the operation. 
         * 
         * @return the unique ID of the operation. 
         */ 
        public String getOperationId() 
        @Override public JsonWriter toJson(JsonWriter jsonWriter) throws IOException
    } 
    /** 
     * PollResponse represents a single response from a service for a long-running polling operation. It provides 
     * information such as the current {@link LongRunningOperationStatus status} of the long-running operation, any 
     * {@link #getValue value} returned in the poll, as well as other useful information provided by the service. 
     * 
     * <p> 
     * <strong>Code Sample Creating PollResponse Object</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.pollresponse.status.value --> 
     * <pre> 
     * // Lets say we want to crete poll response with status as IN_PROGRESS 
     * 
     * PollResponse<String> inProgressPollResponse 
     *     = new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, "my custom response"); 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.pollresponse.status.value --> 
     * 
     * <p> 
     * <strong>Code Sample Creating PollResponse Object with custom status</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.pollresponse.custom.status.value --> 
     * <pre> 
     * // Lets say we want to crete poll response with custom status as OTHER_CUSTOM_STATUS 
     * 
     * PollResponse<String> pollResponseWithCustomStatus 
     *     = new PollResponse<>(LongRunningOperationStatus.fromString("OTHER_CUSTOM_STATUS", false), 
     *         "my custom status response"); 
     * </pre> 
     * <!-- end com.azure.core.util.polling.pollresponse.custom.status.value --> 
     * 
     * @param <T> Type of poll response value. 
     * @see LongRunningOperationStatus 
     */ 
    public final class PollResponse<T> { 
        /** 
         * Creates a new {@link PollResponse} with status and value. 
         * 
         * <p><strong>Code Sample Creating PollResponse Object</strong></p> 
         * <!-- src_embed com.azure.core.util.polling.pollresponse.status.value --> 
         * <pre> 
         * // Lets say we want to crete poll response with status as IN_PROGRESS 
         * 
         * PollResponse<String> inProgressPollResponse 
         *     = new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, "my custom response"); 
         * 
         * </pre> 
         * <!-- end com.azure.core.util.polling.pollresponse.status.value --> 
         * 
         * @param status Mandatory operation status as defined in {@link LongRunningOperationStatus}. 
         * @param value The value as a result of poll operation. This can be any custom user-defined object. Null is also 
         *     valid. 
         * @throws NullPointerException If {@code status} is {@code null}. 
         */ 
        public PollResponse(LongRunningOperationStatus status, T value) 
        /** 
         * Creates a new {@link PollResponse} with status, value, retryAfter and properties. 
         * 
         * <p><strong>Code Sample Creating PollResponse Object</strong></p> 
         * <!-- src_embed com.azure.core.util.polling.pollresponse.status.value.retryAfter.properties --> 
         * <pre> 
         * 
         * // Lets say we want to crete poll response with status as IN_PROGRESS 
         * PollResponse<String> inProgressPollResponse 
         *     = new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, "my custom response", 
         *     Duration.ofMillis(2000)); 
         * </pre> 
         * <!-- end com.azure.core.util.polling.pollresponse.status.value.retryAfter.properties --> 
         * 
         * @param status Mandatory operation status as defined in {@link LongRunningOperationStatus}. 
         * @param value The value as a result of poll operation. This can be any custom user-defined object. Null is also 
         *     valid. 
         * @param retryAfter Represents the delay the service has requested until the next polling operation is performed. A 
         *     {@code null}, zero or negative value will be taken to mean that the poller should determine on its 
         *     own when the next poll operation is to occur. 
         * @throws NullPointerException If {@code status} is {@code null}. 
         */ 
        public PollResponse(LongRunningOperationStatus status, T value, Duration retryAfter) 
        /** 
         * Returns the delay the service has requested until the next polling operation is performed. A null or negative 
         * value will be taken to mean that the poller should determine on its own when the next poll operation is 
         * to occur. 
         * @return Duration How long to wait before next retry. 
         */ 
        public Duration getRetryAfter() 
        /** 
         * Represents the status of the long-running operation at the time the last polling operation finished successfully. 
         * @return A {@link LongRunningOperationStatus} representing the result of the poll operation. 
         */ 
        public LongRunningOperationStatus getStatus() 
        /** 
         * The value returned as a result of the last successful poll operation. This can be any custom user defined object, 
         * or null if no value was returned from the service. 
         * 
         * @return T result of poll operation. 
         */ 
        public T getValue() 
    } 
    /** 
     * A Flux that simplifies the task of executing long-running operations against an Azure service. A subscription to 
     * {@link PollerFlux} initiates a long-running operation and polls the status until it completes. 
     * 
     * <p> 
     * <strong>Code samples</strong> 
     * </p> 
     * 
     * <p> 
     * <strong>Instantiating and subscribing to PollerFlux</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.instantiationAndSubscribe --> 
     * <pre> 
     * LocalDateTime timeToReturnFinalResponse = LocalDateTime.now().plus(Duration.ofMillis(800)); 
     * 
     * // Create poller instance 
     * PollerFlux<String, String> poller = new PollerFlux<>(Duration.ofMillis(100), 
     *     (context) -> Mono.empty(), 
     *     // Define your custom poll operation 
     *     (context) ->  { 
     *         if (LocalDateTime.now().isBefore(timeToReturnFinalResponse)) { 
     *             System.out.println("Returning intermediate response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, 
     *                     "Operation in progress.")); 
     *         } else { 
     *             System.out.println("Returning final response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.SUCCESSFULLY_COMPLETED, 
     *                     "Operation completed.")); 
     *         } 
     *     }, 
     *     (activationResponse, context) -> Mono.error(new RuntimeException("Cancellation is not supported")), 
     *     (context) -> Mono.just("Final Output")); 
     * 
     * // Listen to poll responses 
     * poller.subscribe(response -> { 
     *     // Process poll response 
     *     System.out.printf("Got response. Status: %s, Value: %s%n", response.getStatus(), response.getValue()); 
     * }); 
     * // Do something else 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.instantiationAndSubscribe --> 
     * 
     * <p> 
     * <strong>Asynchronously wait for polling to complete and then retrieve the final result</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.getResult --> 
     * <pre> 
     * LocalDateTime timeToReturnFinalResponse = LocalDateTime.now().plus(Duration.ofMinutes(5)); 
     * 
     * // Create poller instance 
     * PollerFlux<String, String> poller = new PollerFlux<>(Duration.ofMillis(100), 
     *     (context) -> Mono.empty(), 
     *     (context) ->  { 
     *         if (LocalDateTime.now().isBefore(timeToReturnFinalResponse)) { 
     *             System.out.println("Returning intermediate response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, 
     *                     "Operation in progress.")); 
     *         } else { 
     *             System.out.println("Returning final response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.SUCCESSFULLY_COMPLETED, 
     *                     "Operation completed.")); 
     *         } 
     *     }, 
     *     (activationResponse, context) -> Mono.just("FromServer:OperationIsCancelled"), 
     *     (context) -> Mono.just("FromServer:FinalOutput")); 
     * 
     * poller.take(Duration.ofMinutes(30)) 
     *         .last() 
     *         .flatMap(asyncPollResponse -> { 
     *             if (asyncPollResponse.getStatus() == LongRunningOperationStatus.SUCCESSFULLY_COMPLETED) { 
     *                 // operation completed successfully, retrieving final result. 
     *                 return asyncPollResponse 
     *                         .getFinalResult(); 
     *             } else { 
     *                 return Mono.error(new RuntimeException("polling completed unsuccessfully with status:" 
     *                         + asyncPollResponse.getStatus())); 
     *             } 
     *         }).block(); 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.getResult --> 
     * 
     * <p> 
     * <strong>Block for polling to complete and then retrieve the final result</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.blockAndGetResult --> 
     * <pre> 
     * AsyncPollResponse<String, String> terminalResponse = pollerFlux.blockLast(); 
     * System.out.printf("Polling complete. Final Status: %s", terminalResponse.getStatus()); 
     * if (terminalResponse.getStatus() == LongRunningOperationStatus.SUCCESSFULLY_COMPLETED) { 
     *     String finalResult = terminalResponse.getFinalResult().block(); 
     *     System.out.printf("Polling complete. Final Status: %s", finalResult); 
     * } 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.blockAndGetResult --> 
     * 
     * <p> 
     * <strong>Asynchronously poll until poller receives matching status</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.pollUntil --> 
     * <pre> 
     * final Predicate<AsyncPollResponse<String, String>> isComplete = response -> { 
     *     return response.getStatus() != LongRunningOperationStatus.IN_PROGRESS 
     *         && response.getStatus() != LongRunningOperationStatus.NOT_STARTED; 
     * }; 
     * 
     * pollerFlux 
     *     .takeUntil(isComplete) 
     *     .subscribe(completed -> { 
     *         System.out.println("Completed poll response, status: " + completed.getStatus()); 
     *     }); 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.pollUntil --> 
     * 
     * <p> 
     * <strong>Asynchronously cancel the long running operation</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.cancelOperation --> 
     * <pre> 
     * LocalDateTime timeToReturnFinalResponse = LocalDateTime.now().plus(Duration.ofMinutes(5)); 
     * 
     * // Create poller instance 
     * PollerFlux<String, String> poller = new PollerFlux<>(Duration.ofMillis(100), 
     *     (context) -> Mono.empty(), 
     *     (context) ->  { 
     *         if (LocalDateTime.now().isBefore(timeToReturnFinalResponse)) { 
     *             System.out.println("Returning intermediate response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.IN_PROGRESS, 
     *                     "Operation in progress.")); 
     *         } else { 
     *             System.out.println("Returning final response."); 
     *             return Mono.just(new PollResponse<>(LongRunningOperationStatus.SUCCESSFULLY_COMPLETED, 
     *                     "Operation completed.")); 
     *         } 
     *     }, 
     *     (activationResponse, context) -> Mono.just("FromServer:OperationIsCancelled"), 
     *     (context) -> Mono.just("FromServer:FinalOutput")); 
     * 
     * // Asynchronously wait 30 minutes to complete the polling, if not completed 
     * // within in the time then cancel the server operation. 
     * poller.take(Duration.ofMinutes(30)) 
     *         .last() 
     *         .flatMap(asyncPollResponse -> { 
     *             if (!asyncPollResponse.getStatus().isComplete()) { 
     *                 return asyncPollResponse 
     *                         .cancelOperation() 
     *                         .then(Mono.error(new RuntimeException("Operation is cancelled!"))); 
     *             } else { 
     *                 return Mono.just(asyncPollResponse); 
     *             } 
     *         }).block(); 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.cancelOperation --> 
     * 
     * <p> 
     * <strong>Instantiating and subscribing to PollerFlux from a known polling strategy</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.instantiationAndSubscribeWithPollingStrategy --> 
     * <pre> 
     * // Create poller instance 
     * PollerFlux<BinaryData, String> poller = PollerFlux.create( 
     *     Duration.ofMillis(100), 
     *     // pass in your custom activation operation 
     *     () -> Mono.just(new SimpleResponse<Void>(new HttpRequest( 
     *         HttpMethod.POST, 
     *         "http://httpbin.org"), 
     *         202, 
     *         new HttpHeaders().set("Operation-Location", "http://httpbin.org"), 
     *         null)), 
     *     new OperationResourcePollingStrategy<>(new HttpPipelineBuilder().build()), 
     *     TypeReference.createInstance(BinaryData.class), 
     *     TypeReference.createInstance(String.class)); 
     * 
     * // Listen to poll responses 
     * poller.subscribe(response -> { 
     *     // Process poll response 
     *     System.out.printf("Got response. Status: %s, Value: %s%n", response.getStatus(), response.getValue()); 
     * }); 
     * // Do something else 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.instantiationAndSubscribeWithPollingStrategy --> 
     * 
     * <p> 
     * <strong>Instantiating and subscribing to PollerFlux from a custom polling strategy</strong> 
     * </p> 
     * <!-- src_embed com.azure.core.util.polling.poller.initializeAndSubscribeWithCustomPollingStrategy --> 
     * <pre> 
     * 
     * // Create custom polling strategy based on OperationResourcePollingStrategy 
     * PollingStrategy<BinaryData, String> strategy = new OperationResourcePollingStrategy<BinaryData, String>( 
     *         new HttpPipelineBuilder().build()) { 
     *     // override any interface method to customize the polling behavior 
     *     @Override 
     *     public Mono<PollResponse<BinaryData>> poll(PollingContext<BinaryData> context, 
     *                                                TypeReference<BinaryData> pollResponseType) { 
     *         return Mono.just(new PollResponse<>( 
     *             LongRunningOperationStatus.SUCCESSFULLY_COMPLETED, 
     *             BinaryData.fromString(""))); 
     *     } 
     * }; 
     * 
     * // Create poller instance 
     * PollerFlux<BinaryData, String> poller = PollerFlux.create( 
     *     Duration.ofMillis(100), 
     *     // pass in your custom activation operation 
     *     () -> Mono.just(new SimpleResponse<Void>(new HttpRequest( 
     *         HttpMethod.POST, 
     *         "http://httpbin.org"), 
     *         202, 
     *         new HttpHeaders().set("Operation-Location", "http://httpbin.org"), 
     *         null)), 
     *     strategy, 
     *     TypeReference.createInstance(BinaryData.class), 
     *     TypeReference.createInstance(String.class)); 
     * 
     * // Listen to poll responses 
     * poller.subscribe(response -> { 
     *     // Process poll response 
     *     System.out.printf("Got response. Status: %s, Value: %s%n", response.getStatus(), response.getValue()); 
     * }); 
     * // Do something else 
     * 
     * </pre> 
     * <!-- end com.azure.core.util.polling.poller.initializeAndSubscribeWithCustomPollingStrategy --> 
     * 
     * @see com.azure.core.util.polling 
     * @param <T> The type of poll response value. 
     * @param <U> The type of the final result of long-running operation. 
     */ 
    public final class PollerFlux<T, U> extends Flux<AsyncPollResponse<T, U>> { 
        /** 
         * Creates PollerFlux. 
         * 
         * @param pollInterval the polling interval 
         * @param activationOperation the activation operation to activate (start) the long-running operation. This 
         * operation will be invoked at most once across all subscriptions. This parameter is required. If there is no 
         * specific activation work to be done then invocation should return Mono.empty(), this operation will be called 
         * with a new {@link PollingContext}. 
         * @param pollOperation the operation to poll the current state of long-running operation. This parameter is 
         * required and the operation will be called with current {@link PollingContext}. 
         * @param cancelOperation a {@link Function} that represents the operation to cancel the long-running operation if 
         * service supports cancellation. This parameter is required. If service does not support cancellation then the 
         * implementer should return {@link Mono#error}with an error message indicating absence of cancellation support. The 
         * operation will be called with current {@link PollingContext}. 
         * @param fetchResultOperation a {@link Function} that represents the  operation to retrieve final result of the 
         * long-running operation if service support it. This parameter is required and operation will be called with the 
         * current {@link PollingContext}. If service does not have an api to fetch final result and if final result is same 
         * as final poll response value then implementer can choose to simply return value from provided final poll 
         * response. 
         * @throws NullPointerException if {@code pollInterval}, {@code activationOperation}, {@code pollOperation}, 
         * {@code cancelOperation} or {@code fetchResultOperation} is {@code null}. 
         * @throws IllegalArgumentException if {@code pollInterval} is zero or negative. 
         */ 
        public PollerFlux(Duration pollInterval, Function<PollingContext<T>, Mono<T>> activationOperation, Function<PollingContext<T>, Mono<PollResponse<T>>> pollOperation, BiFunction<PollingContext<T>, PollResponse<T>, Mono<T>> cancelOperation, Function<PollingContext<T>, Mono<U>> fetchResultOperation) 
        /** 
         * Creates PollerFlux. 
         * <p> 
         * This method differs from the PollerFlux constructor in that the constructor uses an activationOperation which 
         * returns a Mono that emits result, the create method uses an activationOperation which returns a Mono that emits 
         * {@link PollResponse}. The {@link PollResponse} holds the result. If the {@link PollResponse} from the 
         * activationOperation indicate that long-running operation is completed then the pollOperation will not be called. 
         * 
         * @param pollInterval the polling interval 
         * @param activationOperation the activation operation to activate (start) the long-running operation. This 
         * operation will be invoked at most once across all subscriptions. This parameter is required. If there is no 
         * specific activation work to be done then invocation should return Mono.empty(), this operation will be called 
         * with a new {@link PollingContext}. 
         * @param pollOperation the operation to poll the current state of long-running operation. This parameter is 
         * required and the operation will be called with current {@link PollingContext}. 
         * @param cancelOperation a {@link Function} that represents the operation to cancel the long-running operation if 
         * service supports cancellation. This parameter is required. If service does not support cancellation then the 
         * implementer should return {@link Mono#error} with an error message indicating absence of cancellation support. 
         * The operation will be called with current {@link PollingContext}. 
         * @param fetchResultOperation a {@link Function} that represents the  operation to retrieve final result of the 
         * long-running operation if service support it. This parameter is required and operation will be called current 
         * {@link PollingContext}. If service does not have an api to fetch final result and if final result is same as 
         * final poll response value then implementer can choose to simply return value from provided final poll response. 
         * @param <T> The type of poll response value. 
         * @param <U> The type of the final result of long-running operation. 
         * @return PollerFlux 
         * @throws NullPointerException if {@code pollInterval}, {@code activationOperation}, {@code pollOperation}, 
         * {@code cancelOperation} or {@code fetchResultOperation} is {@code null}. 
         * @throws IllegalArgumentException if {@code pollInterval} is zero or negative. 
         */ 
        public static <T, U> PollerFlux<T, U> create(Duration pollInterval, Function<PollingContext<T>, Mono<PollResponse<T>>> activationOperation, Function<PollingContext<T>, Mono<PollResponse<T>>> pollOperation, BiFunction<PollingContext<T>, PollResponse<T>, Mono<T>> cancelOperation, Function<PollingContext<T>, Mono<U>> fetchResultOperation) 
        /** 
         * Creates PollerFlux. 
         * <p> 
         * This method uses a {@link PollingStrategy} to poll the status of a long-running operation after the activation 
         * operation is invoked. See {@link PollingStrategy} for more details of known polling strategies and how to create 
         * a custom strategy. 
         * 
         * @param pollInterval the polling interval 
         * @param initialOperation the activation operation to activate (start) the long-running operation. This operation 
         * will be invoked at most once across all subscriptions. This parameter is required. If there is no specific 
         * activation work to be done then invocation should return Mono.empty(), this operation will be called with a new 
         * {@link PollingContext}. 
         * @param strategy a known strategy for polling a long-running operation in Azure 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @param resultType the {@link TypeReference} of the final result object to deserialize into, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @param <T> The type of poll response value. 
         * @param <U> The type of the final result of long-running operation. 
         * @return PollerFlux 
         * @throws NullPointerException if {@code pollInterval}, {@code initialOperation}, {@code strategy}, 
         * {@code pollResponseType} or {@code resultType} is {@code null}. 
         * @throws IllegalArgumentException if {@code pollInterval} is zero or negative. 
         */ 
        public static <T, U> PollerFlux<T, U> create(Duration pollInterval, Supplier<Mono<? extends Response<?>>> initialOperation, PollingStrategy<T, U> strategy, TypeReference<T> pollResponseType, TypeReference<U> resultType) 
        /** 
         * Creates a PollerFlux instance that returns an error on subscription. 
         * 
         * @param ex The exception to be returned on subscription of this {@link PollerFlux}. 
         * @param <T> The type of poll response value. 
         * @param <U> The type of the final result of long-running operation. 
         * @return A poller flux instance that returns an error without emitting any data. 
         * @see Mono#error(Throwable) 
         * @see Flux#error(Throwable) 
         */ 
        public static <T, U> PollerFlux<T, U> error(Exception ex) 
        /** 
         * Returns the current polling duration for this {@link PollerFlux} instance. 
         * 
         * @return The current polling duration. 
         */ 
        public Duration getPollInterval() 
        /** 
         * Sets the poll interval for this poller. The new interval will be used for all subsequent polling operations 
         * including the subscriptions that are already in progress. 
         * 
         * @param pollInterval The new poll interval for this poller. 
         * @return The updated instance of {@link PollerFlux}. 
         * @throws NullPointerException if the {@code pollInterval} is null. 
         * @throws IllegalArgumentException if the {@code pollInterval} is zero or negative. 
         */ 
        public PollerFlux<T, U> setPollInterval(Duration pollInterval) 
        @Override public void subscribe(CoreSubscriber<? super AsyncPollResponse<T, U>> actual) 
        /** 
         * Gets a synchronous blocking poller. 
         * 
         * @return a synchronous blocking poller. 
         */ 
        public SyncPoller<T, U> getSyncPoller() 
    } 
    /** 
     * A key/value store that is propagated between various poll related operations associated with 
     * {@link PollerFlux} and {@link SyncPoller} poller. The context also expose activation and 
     * latest {@link PollResponse}. 
     * 
     * @param <T> the type of the poll response. 
     */ 
    public final class PollingContext<T> { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Gets the activation {@link PollResponse} holding the result of an activation operation call. 
         * 
         * @return The activation {@link PollResponse} holding the result of an activation operation call. 
         */ 
        public PollResponse<T> getActivationResponse() 
        /** 
         * Get a value from the context with the provided key. 
         * 
         * @param name the key to look for 
         * @return the value of the key if exists, else null 
         */ 
        public String getData(String name) 
        /** 
         * Set a key-value pair in the context. 
         * 
         * @param name the key 
         * @param value the value 
         * @return an updated PollingContext 
         */ 
        public PollingContext<T> setData(String name, String value) 
        /** 
         * Gets the latest {@link PollResponse} in the polling operation. 
         * 
         * @return The latest {@link PollResponse} in the polling operation. 
         */ 
        public PollResponse<T> getLatestResponse() 
    } 
    /** 
     * Represents a known strategy for polling a long-running operation in Azure. 
     * 
     * <p> 
     * 
     * The methods in the polling strategy will be invoked from the {@link com.azure.core.util.polling.PollerFlux}. The 
     * order of the invocations is: 
     * 
     * <ol> 
     * <li>{@link #canPoll(Response)} - exits if returns false</li> 
     * <li>{@link #onInitialResponse(Response, PollingContext, TypeReference)} - immediately after 
     * {@link #canPoll(Response)} returns true</li> 
     * <li>{@link #poll(PollingContext, TypeReference)} - invoked after each polling interval, if the last polling 
     * response indicates an "In Progress" status. Returns a {@link PollResponse} with the latest status</li> 
     * <li>{@link #getResult(PollingContext, TypeReference)} - invoked when the last polling response indicates a 
     * "Successfully Completed" status. Returns the final result of the given type</li> 
     * </ol> 
     * 
     * If the user decides to cancel the {@link AsyncPollResponse} or {@link SyncPoller}, the 
     * {@link #cancel(PollingContext, PollResponse)} method will be invoked. If the strategy doesn't support cancellation, 
     * an error will be returned. 
     * 
     * <p> 
     * 
     * Users are not expected to provide their own implementation of this interface. Built-in polling strategies in this 
     * library and other client libraries are often sufficient for handling polling in most long-running operations in 
     * Azure. When there are special scenarios, built-in polling strategies can be inherited and select methods can be 
     * overridden to accomplish the polling requirements, without writing an entire polling strategy from scratch. 
     * 
     * @param <T> the {@link TypeReference} of the response type from a polling call, or BinaryData if raw response body 
     * should be kept 
     * @param <U> the {@link TypeReference} of the final result object to deserialize into, or BinaryData if raw response 
     * body should be kept 
     */ 
    public interface PollingStrategy<T, U> { 
        /** 
         * Cancels the long-running operation if service supports cancellation. If service does not support cancellation 
         * then the implementer should return Mono.error with an error message indicating absence of cancellation. 
         * 
         * Implementing this method is optional - by default, cancellation will not be supported unless overridden. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation, or null if the polling has 
         *                       started in a {@link SyncPoller} 
         * @param initialResponse the response from the initial operation 
         * @return a publisher emitting the cancellation response content 
         */ 
        default Mono<T> cancel(PollingContext<T> pollingContext, PollResponse<T> initialResponse) 
        /** 
         * Checks if this strategy is able to handle polling for this long-running operation based on the information in 
         * the initial response. 
         * 
         * @param initialResponse the response from the initial method call to activate the long-running operation 
         * @return true if this polling strategy can handle the initial response, false if not 
         */ 
        Mono<Boolean> canPoll(Response<?> initialResponse) 
        /** 
         * Parses the initial response into a {@link LongRunningOperationStatus}, and stores information useful for polling 
         * in the {@link PollingContext}. If the result is anything other than {@link LongRunningOperationStatus#IN_PROGRESS}, 
         * the long-running operation will be terminated and none of the other methods will be invoked. 
         * 
         * @param response the response from the initial method call to activate the long-running operation 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         *                         response body should be kept. This should match the generic parameter {@link U}. 
         * @return a publisher emitting the poll response containing the status and the response content 
         */ 
        Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * Parses the response from the polling URL into a {@link PollResponse}, and stores information 
         * useful for further polling and final response in the {@link PollingContext}. The result must have the 
         * {@link LongRunningOperationStatus} specified, and the entire polling response content as a {@link BinaryData}. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         *                         response body should be kept. This should match the generic parameter {@link U}. 
         * @return a publisher emitting the poll response containing the status and the response content 
         */ 
        Mono<PollResponse<T>> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * Parses the response from the final GET call into the result type of the long-running operation. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param resultType the {@link TypeReference} of the final result object to deserialize into, or BinaryData if 
         *                   raw response body should be kept. 
         * @return a publisher emitting the final result 
         */ 
        Mono<U> getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    @Fluent
    /** 
     * Options to configure polling strategy. 
     */ 
    public final class PollingStrategyOptions { 
        /** 
         * The {@link HttpPipeline} to use for polling and getting the final result of the long-running operation. 
         * 
         * @param httpPipeline {@link HttpPipeline} to use for polling and getting the final result of the long-running operation. 
         * @throws NullPointerException if {@code httpPipeline} is null. 
         */ 
        public PollingStrategyOptions(HttpPipeline httpPipeline) 
        /** 
         * Returns the context to use for sending the request using the {@link #getHttpPipeline()}. 
         * 
         * @return the context to use for sending the request using the {@link #getHttpPipeline()}. 
         */ 
        public Context getContext() 
        /** 
         * Sets the context to use for sending the request using the {@link #getHttpPipeline()}. 
         * 
         * @param context the context to use for sending the request using the {@link #getHttpPipeline()}. 
         * @return the updated {@link PollingStrategyOptions} instance. 
         */ 
        public PollingStrategyOptions setContext(Context context) 
        /** 
         * Returns the endpoint that will be used as prefix if the service response returns a relative path for getting the 
         * long-running operation status and final result. 
         * 
         * @return the endpoint that will be used as prefix if the service response returns a relative path for getting the 
         * long-running operation status and final result. 
         */ 
        public String getEndpoint() 
        /** 
         * Sets the endpoint that will be used as prefix if the service response returns a relative path for getting the 
         * long-running operation status and final result. 
         * 
         * @param endpoint the endpoint that will be used as prefix if the service response returns a relative path for getting the 
         * long-running operation status and final result. 
         * @return the updated {@link PollingStrategyOptions} instance. 
         */ 
        public PollingStrategyOptions setEndpoint(String endpoint) 
        /** 
         * Returns {@link HttpPipeline} to use for polling and getting the final result of the long-running operation. 
         * 
         * @return {@link HttpPipeline} to use for polling and getting the final result of the long-running operation. 
         */ 
        public HttpPipeline getHttpPipeline() 
        /** 
         * Returns the serializer to use for serializing and deserializing the request and response. 
         * 
         * @return the serializer to use for serializing and deserializing the request and response. 
         */ 
        public ObjectSerializer getSerializer() 
        /** 
         * Set the serializer to use for serializing and deserializing the request and response. 
         * 
         * @param serializer the serializer to use for serializing and deserializing the request and response. 
         * @return the updated {@link PollingStrategyOptions} instance. 
         */ 
        public PollingStrategyOptions setSerializer(ObjectSerializer serializer) 
        /** 
         * Returns the service version that will be added as query param to each polling 
         * request and final result request URL. If the request URL already contains a service version, it will be replaced 
         * by the service version set in this constructor. 
         * 
         * @return the service version to use for polling and getting the final result. 
         */ 
        public String getServiceVersion() 
        /** 
         * Sets the service version that will be added as query param to each polling 
         * request and final result request URL. If the request URL already contains a service version, it will be replaced 
         * by the service version set in this constructor. 
         * 
         * @param serviceVersion the service version to use for polling and getting the final result. 
         * @return the updated {@link PollingStrategyOptions} instance. 
         */ 
        public PollingStrategyOptions setServiceVersion(String serviceVersion) 
    } 
    /** 
     * Fallback polling strategy that doesn't poll but exits successfully if no other polling strategies are detected 
     * and status code is 2xx. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class StatusCheckPollingStrategy<T, U> implements PollingStrategy<T, U> { 
        /** 
         * Creates a status check polling strategy with a JSON serializer. 
         */ 
        public StatusCheckPollingStrategy() 
        /** 
         * Creates a status check polling strategy with a custom object serializer. 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         */ 
        public StatusCheckPollingStrategy(ObjectSerializer serializer) 
        @Override public Mono<Boolean> canPoll(Response<?> initialResponse) 
        @Override public Mono<PollResponse<T>> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public Mono<PollResponse<T>> poll(PollingContext<T> context, TypeReference<T> pollResponseType) 
        @Override public Mono<U> getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * A synchronous polling strategy that chains multiple synchronous polling strategies, finds the first strategy that can 
     * poll the current long-running operation, and polls with that strategy. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public final class SyncChainedPollingStrategy<T, U> implements SyncPollingStrategy<T, U> { 
        /** 
         * Creates a synchronous chained polling strategy with a list of polling strategies. 
         * 
         * @param strategies the list of synchronous polling strategies 
         * @throws NullPointerException If {@code strategies} is null. 
         * @throws IllegalArgumentException If {@code strategies} is an empty list. 
         */ 
        public SyncChainedPollingStrategy(List<SyncPollingStrategy<T, U>> strategies) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public T cancel(PollingContext<T> pollingContext, PollResponse<T> initialResponse) 
        @Override public boolean canPoll(Response<?> initialResponse) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public PollResponse<T> poll(PollingContext<T> context, TypeReference<T> pollResponseType) 
        /** 
         * {@inheritDoc} 
         * 
         * @throws NullPointerException if {@link #canPoll(Response)} is not called prior to this, or if it returns false. 
         */ 
        @Override public U getResult(PollingContext<T> context, TypeReference<U> resultType) 
    } 
    /** 
     * The default synchronous polling strategy to use with Azure data plane services. The default polling strategy will 
     * attempt three known strategies, {@link SyncOperationResourcePollingStrategy}, {@link SyncLocationPollingStrategy}, 
     * and {@link SyncStatusCheckPollingStrategy}, in this order. The first strategy that can poll on the initial response 
     * will be used. The created chained polling strategy is capable of handling most of the polling scenarios in Azure. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public final class SyncDefaultPollingStrategy<T, U> implements SyncPollingStrategy<T, U> { 
        /** 
         * Creates a synchronous chained polling strategy with three known polling strategies, 
         * {@link SyncOperationResourcePollingStrategy}, {@link SyncLocationPollingStrategy}, and 
         * {@link SyncStatusCheckPollingStrategy}, in this order, with a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncDefaultPollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates a chained polling strategy with 3 known polling strategies, {@link SyncOperationResourcePollingStrategy}, 
         * {@link SyncLocationPollingStrategy}, and {@link SyncStatusCheckPollingStrategy}, in this order, with a custom 
         * serializer. 
         * 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException If {@code pollingStrategyOptions} is null. 
         */ 
        public SyncDefaultPollingStrategy(PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates a synchronous chained polling strategy with three known polling strategies, 
         * {@link SyncOperationResourcePollingStrategy}, {@link SyncLocationPollingStrategy}, and 
         * {@link SyncStatusCheckPollingStrategy}, in this order, with a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncDefaultPollingStrategy(HttpPipeline httpPipeline, JsonSerializer serializer) 
        /** 
         * Creates a synchronous chained polling strategy with three known polling strategies, 
         * {@link SyncOperationResourcePollingStrategy}, {@link SyncLocationPollingStrategy}, and 
         * {@link SyncStatusCheckPollingStrategy}, in this order, with a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncDefaultPollingStrategy(HttpPipeline httpPipeline, JsonSerializer serializer, Context context) 
        /** 
         * Creates a synchronous chained polling strategy with three known polling strategies, 
         * {@link SyncOperationResourcePollingStrategy}, {@link SyncLocationPollingStrategy}, and 
         * {@link SyncStatusCheckPollingStrategy}, in this order, with a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with. 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses. 
         * @param context an instance of {@link Context}. 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncDefaultPollingStrategy(HttpPipeline httpPipeline, String endpoint, JsonSerializer serializer, Context context) 
        @Override public T cancel(PollingContext<T> pollingContext, PollResponse<T> initialResponse) 
        @Override public boolean canPoll(Response<?> initialResponse) 
        @Override public PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public PollResponse<T> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public U getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * Implements a synchronous Location polling strategy. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class SyncLocationPollingStrategy<T, U> implements SyncPollingStrategy<T, U> { 
        /** 
         * Creates an instance of the location polling strategy using a JSON serializer. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncLocationPollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException If {@code pollingStrategyOptions} is null. 
         */ 
        public SyncLocationPollingStrategy(PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncLocationPollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncLocationPollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, Context context) 
        /** 
         * Creates an instance of the location polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param context an instance of {@link Context} 
         * @throws NullPointerException If {@code httpPipeline} is null. 
         */ 
        public SyncLocationPollingStrategy(HttpPipeline httpPipeline, String endpoint, ObjectSerializer serializer, Context context) 
        @Override public boolean canPoll(Response<?> initialResponse) 
        @Override public PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public PollResponse<T> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public U getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * Implements a synchronous operation resource polling strategy, typically from Operation-Location. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class SyncOperationResourcePollingStrategy<T, U> implements SyncPollingStrategy<T, U> { 
        /** 
         * Creates an instance of the operation resource polling strategy using a JSON serializer and "Operation-Location" 
         * as the header for polling. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         */ 
        public SyncOperationResourcePollingStrategy(HttpPipeline httpPipeline) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param operationLocationHeaderName a custom header for polling the long-running operation. 
         * @param pollingStrategyOptions options to configure this polling strategy. 
         * @throws NullPointerException if {@code pollingStrategyOptions} is null. 
         */ 
        public SyncOperationResourcePollingStrategy(HttpHeaderName operationLocationHeaderName, PollingStrategyOptions pollingStrategyOptions) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param operationLocationHeaderName a custom header for polling the long-running operation 
         */ 
        public SyncOperationResourcePollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, String operationLocationHeaderName) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         * @param operationLocationHeaderName a custom header for polling the long-running operation 
         * @param context an instance of {@link com.azure.core.util.Context} 
         */ 
        public SyncOperationResourcePollingStrategy(HttpPipeline httpPipeline, ObjectSerializer serializer, String operationLocationHeaderName, Context context) 
        /** 
         * Creates an instance of the operation resource polling strategy. 
         * 
         * @param httpPipeline an instance of {@link HttpPipeline} to send requests with. 
         * @param endpoint an endpoint for creating an absolute path when the path itself is relative. 
         * @param serializer a custom serializer for serializing and deserializing polling responses. 
         * @param operationLocationHeaderName a custom header for polling the long-running operation. 
         * @param context an instance of {@link com.azure.core.util.Context}. 
         */ 
        public SyncOperationResourcePollingStrategy(HttpPipeline httpPipeline, String endpoint, ObjectSerializer serializer, String operationLocationHeaderName, Context context) 
        @Override public boolean canPoll(Response<?> initialResponse) 
        @Override public PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public PollResponse<T> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public U getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * A type that offers API that simplifies the task of executing long-running operations against an Azure service. 
     * 
     * <p> 
     * It provides the following functionality: 
     * <ul> 
     * <li>Querying the current state of the long-running operation.</li> 
     * <li>Requesting cancellation of long-running operation, if supported by the service.</li> 
     * <li>Fetching final result of long-running operation, if supported by the service.</li> 
     * <li>Wait for long-running operation to complete, with optional timeout.</li> 
     * <li>Wait for long-running operation to reach a specific state.</li> 
     * </ul> 
     * 
     * @param <T> The type of poll response value. 
     * @param <U> The type of the final result of long-running operation. 
     */ 
    public interface SyncPoller<T, U> { 
        /** 
         * Cancels the remote long-running operation if cancellation is supported by the service. 
         * <p> 
         * If cancellation isn't supported by the service this will throw an exception. 
         * 
         * @throws RuntimeException if the operation does not support cancellation. 
         */ 
        void cancelOperation() 
        /** 
         * Creates default SyncPoller. 
         * 
         * @param pollInterval the polling interval. 
         * @param syncActivationOperation the operation to synchronously activate (start) the long-running operation, this 
         * operation will be called with a new {@link PollingContext}. 
         * @param pollOperation the operation to poll the current state of long-running operation, this parameter is 
         * required and the operation will be called with current {@link PollingContext}. 
         * @param cancelOperation a {@link Function} that represents the operation to cancel the long-running operation if 
         * service supports cancellation, this parameter is required and if service does not support cancellation then the 
         * implementer should throw an exception with an error message indicating absence of cancellation support, the 
         * operation will be called with current {@link PollingContext}. 
         * @param fetchResultOperation a {@link Function} that represents the  operation to retrieve final result of the 
         * long-running operation if service support it, this parameter is required and operation will be called current 
         * {@link PollingContext}, if service does not have an api to fetch final result and if final result is same as 
         * final poll response value then implementer can choose to simply return value from provided final poll response. 
         * @param <T> The type of poll response value. 
         * @param <U> The type of the final result of long-running operation. 
         * @return new {@link SyncPoller} instance. 
         * @throws NullPointerException if {@code pollInterval}, {@code syncActivationOperation}, {@code pollOperation}, 
         * {@code cancelOperation} or {@code fetchResultOperation} is {@code null}. 
         * @throws IllegalArgumentException if {@code pollInterval} is zero or negative. 
         */ 
        static <T, U> SyncPoller<T, U> createPoller(Duration pollInterval, Function<PollingContext<T>, PollResponse<T>> syncActivationOperation, Function<PollingContext<T>, PollResponse<T>> pollOperation, BiFunction<PollingContext<T>, PollResponse<T>, T> cancelOperation, Function<PollingContext<T>, U> fetchResultOperation) 
        /** 
         * Creates PollerFlux. 
         * <p> 
         * This method uses a {@link SyncPollingStrategy} to poll the status of a long-running operation after the 
         * activation operation is invoked. See {@link SyncPollingStrategy} for more details of known polling strategies and 
         * how to create a custom strategy. 
         * 
         * @param pollInterval the polling interval 
         * @param initialOperation the activation operation to activate (start) the long-running operation. This operation 
         * will be invoked at most once across all subscriptions. This parameter is required. If there is no specific 
         * activation work to be done then invocation should return null, this operation will be called with a new 
         * {@link PollingContext}. 
         * @param strategy a known synchronous strategy for polling a long-running operation in Azure 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @param resultType the {@link TypeReference} of the final result object to deserialize into, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @param <T> The type of poll response value. 
         * @param <U> The type of the final result of long-running operation. 
         * @return new {@link SyncPoller} instance. 
         * @throws NullPointerException if {@code pollInterval}, {@code initialOperation}, {@code strategy}, 
         * {@code pollResponseType} or {@code resultType} is {@code null}. 
         * @throws IllegalArgumentException if {@code pollInterval} is zero or negative. 
         */ 
        static <T, U> SyncPoller<T, U> createPoller(Duration pollInterval, Supplier<Response<?>> initialOperation, SyncPollingStrategy<T, U> strategy, TypeReference<T> pollResponseType, TypeReference<U> resultType) 
        /** 
         * Retrieve the final result of the long-running operation. 
         * <p> 
         * If polling hasn't completed this will wait indefinitely until polling completes. 
         * 
         * @return the final result of the long-running operation if there is one. 
         */ 
        U getFinalResult() 
        /** 
         * Retrieve the final result of the long-running operation. 
         * <p> 
         * If polling hasn't completed this will wait for the {@code timeout} for polling to complete. In this case this 
         * API is effectively equivalent to {@link #waitForCompletion(Duration)} + {@link #getFinalResult()}. 
         * <p> 
         * Polling will continue until a completed {@link LongRunningOperationStatus} is received or the timeout expires. 
         * <p> 
         * The {@code timeout} is applied in two ways, first it's used during each poll operation to time it out if the 
         * polling operation takes too long. Second, it's used to determine when the wait for should stop. If polling 
         * doesn't reach a completion state before the {@code timeout} elapses a {@link RuntimeException} wrapping a 
         * {@link TimeoutException} will be thrown. 
         * <p> 
         * If this method isn't overridden by the implementation then this method is effectively equivalent to calling 
         * {@link #waitForCompletion(Duration)} then {@link #getFinalResult()}. 
         * 
         * @param timeout the duration to wait for polling completion. 
         * @return the final result of the long-running operation if there is one. 
         * @throws NullPointerException If {@code timeout} is null. 
         * @throws IllegalArgumentException If {@code timeout} is zero or negative. 
         * @throws RuntimeException If polling doesn't complete before the {@code timeout} elapses. 
         * ({@link RuntimeException#getCause()} should be a {@link TimeoutException}). 
         */ 
        default U getFinalResult(Duration timeout) 
        /** 
         * Poll once and return the poll response received. 
         * 
         * @return the poll response 
         */ 
        PollResponse<T> poll() 
        /** 
         * Sets the poll interval for this poller. The new interval will be used for all subsequent polling operations 
         * including the polling operations that are already in progress. 
         * 
         * @param pollInterval The new poll interval for this poller. 
         * @return The updated instance of {@link SyncPoller}. 
         * @throws NullPointerException if the {@code pollInterval} is null. 
         * @throws IllegalArgumentException if the {@code pollInterval} is zero or negative. 
         */ 
        default SyncPoller<T, U> setPollInterval(Duration pollInterval) 
        /** 
         * Wait for polling to complete. The polling is considered complete based on status defined in 
         * {@link LongRunningOperationStatus}. 
         * <p> 
         * This operation will wait indefinitely until a completed {@link LongRunningOperationStatus} is received. 
         * 
         * @return the final poll response 
         */ 
        PollResponse<T> waitForCompletion() 
        /** 
         * Wait for polling to complete with a timeout. The polling is considered complete based on status defined in 
         * {@link LongRunningOperationStatus} or if the timeout expires. 
         * <p> 
         * Polling will continue until a completed {@link LongRunningOperationStatus} is received or the timeout expires. 
         * <p> 
         * The {@code timeout} is applied in two ways, first it's used during each poll operation to time it out if the 
         * polling operation takes too long. Second, it's used to determine when the wait for should stop. If polling 
         * doesn't reach a completion state before the {@code timeout} elapses a {@link RuntimeException} wrapping a 
         * {@link TimeoutException} will be thrown. 
         * 
         * @param timeout the duration to wait for polling completion. 
         * @return the final poll response. 
         * @throws NullPointerException If {@code timeout} is null. 
         * @throws IllegalArgumentException If {@code timeout} is zero or negative. 
         * @throws RuntimeException If polling doesn't complete before the {@code timeout} elapses. 
         * ({@link RuntimeException#getCause()} should be a {@link TimeoutException}). 
         */ 
        PollResponse<T> waitForCompletion(Duration timeout) 
        /** 
         * Wait for the given {@link LongRunningOperationStatus} to receive. 
         * <p> 
         * This operation will wait indefinitely until the {@code statusToWaitFor} is received or a 
         * {@link LongRunningOperationStatus#isComplete()} state is reached. 
         * 
         * @param statusToWaitFor the desired {@link LongRunningOperationStatus} to block for. 
         * @return {@link PollResponse} whose {@link PollResponse#getStatus()} matches {@code statusToWaitFor} or is 
         * {@link LongRunningOperationStatus#isComplete()}. 
         * @throws NullPointerException if {@code statusToWaitFor} is {@code null}. 
         */ 
        PollResponse<T> waitUntil(LongRunningOperationStatus statusToWaitFor) 
        /** 
         * Wait for the given {@link LongRunningOperationStatus} with a timeout. 
         * <p> 
         * Polling will continue until a response is returned with a {@link LongRunningOperationStatus} matching 
         * {@code statusToWaitFor}, a {@link LongRunningOperationStatus#isComplete()} state is reached, or the timeout 
         * expires. 
         * <p> 
         * Unlike {@link #waitForCompletion(Duration)} or {@link #getFinalResult(Duration)}, when the timeout elapses a 
         * {@link RuntimeException} wrapping a {@link TimeoutException} will not be thrown. Instead, the last poll response 
         * will be returned. This is because unlike a completion state, a wait for state may be skipped if the state 
         * is reached and completed before a poll operation is executed. For example, if a long-running operation has the 
         * flow {@code A -> B -> C -> D} and the {@code statusToWaitFor} is {@code B} and the first poll request returns 
         * state {@code A} but in the time between polls state {@code B} completes, then the next poll request will return 
         * state {@code C} and the {@code statusToWaitFor} will never be returned. 
         * <p> 
         * This may return null if no poll operation completes within the timeout. 
         * 
         * @param timeout the duration to wait for the polling. 
         * @param statusToWaitFor the desired {@link LongRunningOperationStatus} to block for. 
         * @return {@link PollResponse} whose {@link PollResponse#getStatus()} matches {@code statusToWaitFor}, or null if 
         * no response was returned within the timeout. 
         * @throws NullPointerException if {@code statusToWaitFor} or {@code timeout} is {@code null}. 
         * @throws IllegalArgumentException if {@code timeout} is zero or negative. 
         */ 
        PollResponse<T> waitUntil(Duration timeout, LongRunningOperationStatus statusToWaitFor) 
    } 
    /** 
     * Represents a known strategy for polling a long-running operation in Azure. 
     * <p> 
     * The methods in the polling strategy will be invoked from the {@link SyncPoller}. The order of the invocations is: 
     * 
     * <ol> 
     * <li>{@link #canPoll(Response)} - exits if returns false</li> 
     * <li>{@link #onInitialResponse(Response, PollingContext, TypeReference)} - immediately after 
     * {@link #canPoll(Response)} returns true</li> 
     * <li>{@link #poll(PollingContext, TypeReference)} - invoked after each polling interval, if the last polling response 
     * indicates an "In Progress" status. Returns a {@link PollResponse} with the latest status</li> 
     * <li>{@link #getResult(PollingContext, TypeReference)} - invoked when the last polling response indicates a 
     * "Successfully Completed" status. Returns the final result of the given type</li> 
     * </ol> 
     * 
     * If the user decides to cancel the {@link PollingContext} or {@link SyncPoller}, the 
     * {@link #cancel(PollingContext, PollResponse)} method will be invoked. If the strategy doesn't support cancellation, 
     * an error will be returned. 
     * <p> 
     * Users are not expected to provide their own implementation of this interface. Built-in polling strategies in this 
     * library and other client libraries are often sufficient for handling polling in most long-running operations in 
     * Azure. When there are special scenarios, built-in polling strategies can be inherited and select methods can be 
     * overridden to accomplish the polling requirements, without writing an entire polling strategy from scratch. 
     * 
     * @param <T> the {@link TypeReference} of the response type from a polling call, or BinaryData if raw response body 
     * should be kept 
     * @param <U> the {@link TypeReference} of the final result object to deserialize into, or BinaryData if raw response 
     * body should be kept 
     */ 
    public interface SyncPollingStrategy<T, U> { 
        /** 
         * Cancels the long-running operation if service supports cancellation. If service does not support cancellation 
         * then the implementer should throw an {@link IllegalStateException} with an error message indicating absence of 
         * cancellation. 
         * <p> 
         * Implementing this method is optional - by default, cancellation will not be supported unless overridden. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation, or null if the polling has 
         * started in a {@link SyncPoller} 
         * @param initialResponse the response from the initial operation 
         * @return the cancellation response content 
         * @throws IllegalStateException If cancellation isn't supported. 
         */ 
        default T cancel(PollingContext<T> pollingContext, PollResponse<T> initialResponse) 
        /** 
         * Checks if this strategy is able to handle polling for this long-running operation based on the information in the 
         * initial response. 
         * 
         * @param initialResponse the response from the initial method call to activate the long-running operation 
         * @return true if this polling strategy can handle the initial response, false if not 
         */ 
        boolean canPoll(Response<?> initialResponse) 
        /** 
         * Parses the initial response into a {@link LongRunningOperationStatus}, and stores information useful for polling 
         * in the {@link PollingContext}. If the result is anything other than 
         * {@link LongRunningOperationStatus#IN_PROGRESS}, the long-running operation will be terminated and none of the 
         * other methods will be invoked. 
         * 
         * @param response the response from the initial method call to activate the long-running operation 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @return the poll response containing the status and the response content 
         */ 
        PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * Parses the response from the polling URL into a {@link PollResponse}, and stores information useful for further 
         * polling and final response in the {@link PollingContext}. The result must have the 
         * {@link LongRunningOperationStatus} specified, and the entire polling response content as a {@link BinaryData}. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param pollResponseType the {@link TypeReference} of the response type from a polling call, or BinaryData if raw 
         * response body should be kept. This should match the generic parameter {@link U}. 
         * @return the poll response containing the status and the response content 
         */ 
        PollResponse<T> poll(PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        /** 
         * Parses the response from the final GET call into the result type of the long-running operation. 
         * 
         * @param pollingContext the {@link PollingContext} for the current polling operation 
         * @param resultType the {@link TypeReference} of the final result object to deserialize into, or BinaryData if raw 
         * response body should be kept. 
         * @return the final result 
         */ 
        U getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
    /** 
     * Fallback polling strategy that doesn't poll but exits successfully if no other polling strategies are detected and 
     * status code is 2xx. 
     * 
     * @param <T> the type of the response type from a polling call, or BinaryData if raw response body should be kept 
     * @param <U> the type of the final result object to deserialize into, or BinaryData if raw response body should be 
     * kept 
     */ 
    public class SyncStatusCheckPollingStrategy<T, U> implements SyncPollingStrategy<T, U> { 
        /** 
         * Creates a status check polling strategy with a JSON serializer. 
         */ 
        public SyncStatusCheckPollingStrategy() 
        /** 
         * Creates a status check polling strategy with a custom object serializer. 
         * 
         * @param serializer a custom serializer for serializing and deserializing polling responses 
         */ 
        public SyncStatusCheckPollingStrategy(ObjectSerializer serializer) 
        @Override public boolean canPoll(Response<?> initialResponse) 
        @Override public PollResponse<T> onInitialResponse(Response<?> response, PollingContext<T> pollingContext, TypeReference<T> pollResponseType) 
        @Override public PollResponse<T> poll(PollingContext<T> context, TypeReference<T> pollResponseType) 
        @Override public U getResult(PollingContext<T> pollingContext, TypeReference<U> resultType) 
    } 
} 
/** 
 * Package containing interfaces describing serialization and deserialization contract. 
 */ 
package com.azure.core.util.serializer { 
    /** 
     * Swagger collection format to use for joining {@link java.util.List} parameters in paths, queries, and headers. See 
     * <a href="https://github.com/swagger-api/swagger-spec/blob/master/versions/2.0.md#fixed-fields-7"> 
     * https://github.com/swagger-api/swagger-spec/blob/master/versions/2.0.md#fixed-fields-7</a>. 
     */ 
    public enum CollectionFormat { 
        CSV(","), 
            /** 
             * Comma separated values. E.g. foo,bar 
             */ 
        SSV(" "), 
            /** 
             * Space separated values. E.g. foo bar 
             */ 
        TSV("\t"), 
            /** 
             * Tab separated values. E.g. foo\tbar 
             */ 
        PIPES("|"), 
            /** 
             * Pipe(|) separated values. E.g. foo|bar 
             */ 
        MULTI("&"); 
            /** 
             * Corresponds to multiple parameter instances instead of multiple values for a single instance. E.g. 
             * foo=bar&foo=baz 
             */ 
        /** 
         * Gets the delimiter used to join a list of parameters. 
         * 
         * @return the delimiter of the current collection format. 
         */ 
        public String getDelimiter() 
    } 
    /** 
     * Implementation of {@link SerializerAdapter} for Jackson. 
     */ 
    public class JacksonAdapter implements SerializerAdapter { 
        /** 
         * Creates a new JacksonAdapter instance with default mapper settings. 
         */ 
        public JacksonAdapter() 
        /** 
         * Creates a new JacksonAdapter instance with Azure Core mapper settings and applies additional configuration 
         * through {@code configureSerialization} callback. 
         * <p> 
         * {@code configureSerialization} callback provides outer and inner instances of {@link ObjectMapper}. Both of them 
         * are pre-configured for Azure serialization needs, but only outer mapper capable of flattening and populating 
         * additionalProperties. Outer mapper is used by {@code JacksonAdapter} for all serialization needs. 
         * <p> 
         * Register modules on the outer instance to add custom (de)serializers similar to 
         * {@code new JacksonAdapter((outer, inner) -> outer.registerModule(new MyModule()))} 
         * 
         * Use inner mapper for chaining serialization logic in your (de)serializers. 
         * 
         * @param configureSerialization Applies additional configuration to outer mapper using inner mapper for module 
         * chaining. 
         * @deprecated This API will be removed in the future. Please use {@link #createDefaultSerializerAdapter()} if you 
         * need to use JacksonAdapter. 
         */ 
        @Deprecated public JacksonAdapter(BiConsumer<ObjectMapper, ObjectMapper> configureSerialization) 
        /** 
         * maintain singleton instance of the default serializer adapter. 
         * 
         * @return the default serializer 
         */ 
        public static SerializerAdapter createDefaultSerializerAdapter() 
        @Override public <T> T deserialize(HttpHeaders headers, Type deserializedHeadersType) throws IOException
        @Override public <T> T deserialize(String value, Type type, SerializerEncoding encoding) throws IOException
        @Override public <T> T deserialize(byte[] bytes, Type type, SerializerEncoding encoding) throws IOException
        @Override public <T> T deserialize(InputStream inputStream, Type type, SerializerEncoding encoding) throws IOException
        @Override public <T> T deserializeHeader(Header header, Type type) throws IOException
        @Override public String serialize(Object object, SerializerEncoding encoding) throws IOException
        @Override public void serialize(Object object, SerializerEncoding encoding, OutputStream outputStream) throws IOException
        @Override public String serializeList(List<?> list, CollectionFormat format) 
        /** 
         * @return the original serializer type. 
         * @deprecated deprecated to avoid direct {@link ObjectMapper} usage in favor of using more resilient and debuggable 
         * {@link JacksonAdapter} APIs. 
         */ 
        @Deprecated public ObjectMapper serializer() 
        @Override public String serializeRaw(Object object) 
        @Override public byte[] serializeToBytes(Object object, SerializerEncoding encoding) throws IOException
        /** 
         * Gets a static instance of {@link ObjectMapper} that doesn't handle flattening. 
         * 
         * @return an instance of {@link ObjectMapper}. 
         * @deprecated deprecated, use {@code JacksonAdapter(BiConsumer<ObjectMapper, ObjectMapper>)} constructor to 
         * configure modules. 
         */ 
        @Deprecated protected ObjectMapper simpleMapper() 
    } 
    /** 
     * Generic interface covering basic JSON serialization and deserialization methods. 
     */ 
    public interface JsonSerializer extends ObjectSerializer { 
        /** 
         * Reads a JSON stream into its object representation. 
         * 
         * @param stream JSON stream. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return The object represented by the deserialized JSON stream. 
         */ 
        @Override <T> T deserialize(InputStream stream, TypeReference<T> typeReference) 
        /** 
         * Reads a JSON stream into its object representation. 
         * 
         * @param stream JSON stream. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return Reactive stream that emits the object represented by the deserialized JSON stream. 
         */ 
        @Override <T> Mono<T> deserializeAsync(InputStream stream, TypeReference<T> typeReference) 
        /** 
         * Reads a JSON byte array into its object representation. 
         * 
         * @param data JSON byte array. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return The object represented by the deserialized JSON byte array. 
         */ 
        @Override default <T> T deserializeFromBytes(byte[] data, TypeReference<T> typeReference) 
        /** 
         * Reads a JSON byte array into its object representation. 
         * 
         * @param data JSON byte array. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return Reactive stream that emits the object represented by the deserialized JSON byte array. 
         */ 
        @Override default <T> Mono<T> deserializeFromBytesAsync(byte[] data, TypeReference<T> typeReference) 
        /** 
         * Writes an object's JSON representation into a stream. 
         * 
         * @param stream {@link OutputStream} where the object's JSON representation will be written. 
         * @param value The object. 
         */ 
        @Override void serialize(OutputStream stream, Object value) 
        /** 
         * Writes an object's JSON representation into a stream. 
         * 
         * @param stream {@link OutputStream} where the object's JSON representation will be written. 
         * @param value The object. 
         * @return Reactive stream that will indicate operation completion. 
         */ 
        @Override Mono<Void> serializeAsync(OutputStream stream, Object value) 
        /** 
         * Converts the object into a JSON byte array. 
         * 
         * @param value The object. 
         * @return The JSON binary representation of the serialized object. 
         */ 
        @Override default byte[] serializeToBytes(Object value) 
        /** 
         * Converts the object into a JSON byte array. 
         * 
         * @param value The object. 
         * @return Reactive stream that emits the JSON binary representation of the serialized object. 
         */ 
        @Override default Mono<byte[]> serializeToBytesAsync(Object value) 
    } 
    /** 
     * Interface to be implemented by an azure-core plugin that wishes to provide a {@link JsonSerializer} implementation. 
     */ 
    public interface JsonSerializerProvider { 
        /** 
         * Creates a new instance of the {@link JsonSerializer} that this JsonSerializerProvider is configured to create. 
         * 
         * @return A new {@link JsonSerializer} instance. 
         */ 
        JsonSerializer createInstance() 
    } 
    /** 
     * This class is a proxy for using a {@link JsonSerializerProvider} loaded from the classpath. 
     */ 
    public final class JsonSerializerProviders { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates an instance of {@link JsonSerializer} using the first {@link JsonSerializerProvider} found in the 
         * classpath. 
         * 
         * @return A new instance of {@link JsonSerializer}. 
         * @throws IllegalStateException if a {@link JsonSerializerProvider} is not found in the classpath. 
         */ 
        public static JsonSerializer createInstance() 
        /** 
         * Creates an instance of {@link JsonSerializer} using the first {@link JsonSerializerProvider} found in the 
         * classpath. If no provider is found in classpath, a default provider will be included if {@code useDefaultIfAbsent} 
         * is set to true. 
         * 
         * @param useDefaultIfAbsent If no provider is found in classpath, a default provider will be used. 
         * if {@code useDefaultIfAbsent} is set to true. 
         * @return A new instance of {@link JsonSerializer}. 
         * @throws IllegalStateException if a {@link JsonSerializerProvider} is not found in the classpath and 
         * {@code useDefaultIfAbsent} is set to false. 
         */ 
        public static JsonSerializer createInstance(boolean useDefaultIfAbsent) 
    } 
    /** 
     * Generic interface that attempts to retrieve the JSON serialized property name from {@link Member}. 
     */ 
    public interface MemberNameConverter { 
        /** 
         * Attempts to get the JSON serialized property name from the passed {@link Member}. 
         * <p> 
         * If a {@link java.lang.reflect.Constructor} or {@link java.lang.reflect.Executable} is passed {@code null} will be 
         * returned. 
         * 
         * @param member The {@link Member} that will have its JSON serialized property name retrieved. 
         * @return The JSON property name for the {@link Member}. 
         */ 
        String convertMemberName(Member member) 
    } 
    /** 
     * Interface to be implemented by an azure-core plugin that wishes to provide a {@link MemberNameConverter} 
     * implementation. 
     */ 
    public interface MemberNameConverterProvider { 
        /** 
         * Creates a new instance of the {@link MemberNameConverter} that this MemberNameConverterProvider is configured to 
         * create. 
         * 
         * @return A new {@link MemberNameConverter} instance. 
         */ 
        MemberNameConverter createInstance() 
    } 
    /** 
     * This class is a proxy for using a {@link MemberNameConverterProvider} loaded from the classpath. 
     */ 
    public final class MemberNameConverterProviders { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * Creates an instance of {@link MemberNameConverter} using the first {@link MemberNameConverterProvider} found in 
         * the classpath. 
         * 
         * @return A new instance of {@link MemberNameConverter}. 
         */ 
        public static MemberNameConverter createInstance() 
    } 
    /** 
     * Generic interface covering serializing and deserialization objects. 
     */ 
    public interface ObjectSerializer { 
        /** 
         * Reads a stream into its object representation. 
         * 
         * @param stream {@link InputStream} of data. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return The object represented by the deserialized stream. 
         */ 
        <T> T deserialize(InputStream stream, TypeReference<T> typeReference) 
        /** 
         * Reads a stream into its object representation. 
         * 
         * @param stream {@link InputStream} of data. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return Reactive stream that emits the object represented by the deserialized stream. 
         */ 
        <T> Mono<T> deserializeAsync(InputStream stream, TypeReference<T> typeReference) 
        /** 
         * Reads a byte array into its object representation. 
         * 
         * @param data Byte array. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return The object represented by the deserialized byte array. 
         */ 
        default <T> T deserializeFromBytes(byte[] data, TypeReference<T> typeReference) 
        /** 
         * Reads a byte array into its object representation. 
         * 
         * @param data Byte array. 
         * @param typeReference {@link TypeReference} representing the object. 
         * @param <T> Type of the object. 
         * @return Reactive stream that emits the object represented by the deserialized byte array. 
         */ 
        default <T> Mono<T> deserializeFromBytesAsync(byte[] data, TypeReference<T> typeReference) 
        /** 
         * Writes the serialized object into a stream. 
         * 
         * @param stream {@link OutputStream} where the serialized object will be written. 
         * @param value The object. 
         */ 
        void serialize(OutputStream stream, Object value) 
        /** 
         * Writes the serialized object into a stream. 
         * 
         * @param stream {@link OutputStream} where the serialized object will be written. 
         * @param value The object. 
         * @return Reactive stream that will indicate operation completion. 
         */ 
        Mono<Void> serializeAsync(OutputStream stream, Object value) 
        /** 
         * Converts the object into a byte array. 
         * 
         * @param value The object. 
         * @return The binary representation of the serialized object. 
         */ 
        default byte[] serializeToBytes(Object value) 
        /** 
         * Converts the object into a byte array. 
         * 
         * @param value The object. 
         * @return Reactive stream that emits the binary representation of the serialized object. 
         */ 
        default Mono<byte[]> serializeToBytesAsync(Object value) 
    } 
    /** 
     * An interface defining the behaviors of a serializer. 
     */ 
    public interface SerializerAdapter { 
        /** 
         * Deserialize the provided headers returned from a REST API to an entity instance declared as the model to hold 
         * 'Matching' headers. 
         * <p> 
         * 'Matching' headers are the REST API returned headers those with: 
         * 
         * <ol> 
         *   <li>header names same as name of a properties in the entity.</li> 
         *   <li>header names start with value of {@link com.azure.core.annotation.HeaderCollection} annotation applied to 
         *   the properties in the entity.</li> 
         * </ol> 
         * 
         * When needed, the 'header entity' types must be declared as first generic argument of 
         * {@link com.azure.core.http.rest.ResponseBase} returned by java proxy method corresponding to the REST API. 
         * e.g. 
         * {@code Mono<RestResponseBase<FooMetadataHeaders, Void>> getMetadata(args);} 
         * {@code 
         *      class FooMetadataHeaders { 
         *          String name; 
         *          {@literal @}HeaderCollection("header-collection-prefix-") 
         *          Map<String,String> headerCollection; 
         *      } 
         * } 
         * 
         * in the case of above example, this method produces an instance of FooMetadataHeaders from provided 
         * {@code headers}. 
         * 
         * @param headers the REST API returned headers 
         * @param <T> the type of the deserialized object 
         * @param type the type to deserialize 
         * @return instance of header entity type created based on provided {@code headers}, if header entity model does 
         * not exist then return null 
         * @throws IOException If an I/O error occurs 
         */ 
        <T> T deserialize(HttpHeaders headers, Type type) throws IOException
        /** 
         * Deserializes a string into an object. 
         * 
         * @param value The string to deserialize. 
         * @param <T> The type of the deserialized object. 
         * @param type The type of the deserialized object. 
         * @param encoding The deserialization encoding. 
         * @return The string deserialized into an object. 
         * @throws IOException If an IO exception was thrown during deserialization. 
         */ 
        <T> T deserialize(String value, Type type, SerializerEncoding encoding) throws IOException
        /** 
         * Deserializes a byte array into an object. 
         * 
         * @param bytes The byte array to deserialize. 
         * @param type The type of the deserialized object. 
         * @param encoding The deserialization encoding. 
         * @param <T> The type of the deserialized object. 
         * @return The string deserialized into an object. 
         * @throws IOException If an IO exception was thrown during serialization. 
         */ 
        default <T> T deserialize(byte[] bytes, Type type, SerializerEncoding encoding) throws IOException
        /** 
         * Deserializes a stream into an object. 
         * 
         * @param inputStream The {@link InputStream} to deserialize. 
         * @param type The type of the deserialized object. 
         * @param encoding The deserialization encoding. 
         * @param <T> The type of the deserialized object. 
         * @return The stream deserialized into an object. 
         * @throws IOException If an IO exception was thrown during serialization. 
         */ 
        default <T> T deserialize(InputStream inputStream, Type type, SerializerEncoding encoding) throws IOException
        /** 
         * Deserializes the provided header returned from a REST API to en entity instance declared as the model of the 
         * header. 
         * 
         * @param header The header. 
         * @param type The type that represents the deserialized header. 
         * @param <T> The type of the deserialized header. 
         * @return A new instance of the type that represents the deserialized header. 
         * @throws IOException If an I/O error occurs. 
         */ 
        default <T> T deserializeHeader(Header header, Type type) throws IOException
        /** 
         * Serializes an object into a string. 
         * 
         * @param object The object to serialize. 
         * @param encoding The serialization encoding. 
         * @return The object serialized as a string using the specified encoding. If the object is null, null is returned. 
         * @throws IOException If an IO exception was thrown during serialization. 
         */ 
        String serialize(Object object, SerializerEncoding encoding) throws IOException
        /** 
         * Serializes an object and writes its output into an {@link OutputStream}. 
         * 
         * @param object The object to serialize. 
         * @param encoding The serialization encoding. 
         * @param outputStream The {@link OutputStream} where the serialized object will be written. 
         * @throws IOException If an IO exception was thrown during serialization. 
         */ 
        default void serialize(Object object, SerializerEncoding encoding, OutputStream outputStream) throws IOException
        /** 
         * Serializes an iterable into a string with the delimiter specified with the Swagger collection format joining each 
         * individual serialized items in the list. 
         * 
         * @param iterable The iterable to serialize. 
         * @param format The collection joining format. 
         * @return The iterable serialized as a joined string. 
         */ 
        default String serializeIterable(Iterable<?> iterable, CollectionFormat format) 
        /** 
         * Serializes a list into a string with the delimiter specified with the Swagger collection format joining each 
         * individual serialized items in the list. 
         * 
         * @param list The list to serialize. 
         * @param format The collection joining format. 
         * @return The list serialized as a joined string. 
         */ 
        String serializeList(List<?> list, CollectionFormat format) 
        /** 
         * Serializes an object into a raw string, leading and trailing quotes will be trimmed. 
         * 
         * @param object The object to serialize. 
         * @return The object serialized as a string. If the object is null, null is returned. 
         */ 
        String serializeRaw(Object object) 
        /** 
         * Serializes an object into a byte array. 
         * 
         * @param object The object to serialize. 
         * @param encoding The serialization encoding. 
         * @return The object serialized as a byte array. 
         * @throws IOException If an IO exception was thrown during serialization. 
         */ 
        default byte[] serializeToBytes(Object object, SerializerEncoding encoding) throws IOException
    } 
    /** 
     * Supported serialization encoding formats. 
     */ 
    public enum SerializerEncoding { 
        JSON, 
            /** 
             * JavaScript Object Notation. 
             */ 
        XML, 
            /** 
             * Extensible Markup Language. 
             */ 
        TEXT; 
            /** 
             * Text. 
             */ 
        /** 
         * Determines the serializer encoding to use based on the Content-Type header. 
         * 
         * @param headers the headers to check the encoding for. 
         * @return the serializer encoding to use for the body. {@link #JSON} if there is no Content-Type header or an 
         * unrecognized Content-Type encoding is returned. 
         */ 
        public static SerializerEncoding fromHeaders(HttpHeaders headers) 
    } 
    /** 
     * This class represents a generic Java type, retaining information about generics. 
     * 
     * <p> 
     * <strong>Code sample</strong> 
     * </p> 
     * 
     * <!-- src_embed com.azure.core.util.serializer.constructor --> 
     * <pre> 
     * // Construct a TypeReference<T> for a Java generic type. 
     * // This pattern should only be used for generic types, for classes use the createInstance factory method. 
     * TypeReference<Map<String, Object>> typeReference = new TypeReference<Map<String, Object>>() { }; 
     * </pre> 
     * <!-- end com.azure.core.util.serializer.constructor --> 
     * 
     * <!-- src_embed com.azure.core.util.serializer.createInstance#class --> 
     * <pre> 
     * // Construct a TypeReference<T> for a Java class. 
     * // This pattern should only be used for non-generic classes when possible, use the constructor for generic 
     * // class when possible. 
     * TypeReference<Integer> typeReference = TypeReference.createInstance(int.class); 
     * </pre> 
     * <!-- end com.azure.core.util.serializer.createInstance#class --> 
     * 
     * @param <T> The type being represented. 
     */ 
    public abstract class TypeReference<T> { 
        /** 
         * Constructs a new {@link TypeReference} which maintains generic information. 
         * 
         * @throws IllegalArgumentException If the reference is constructed without type information. 
         */ 
        public TypeReference() 
        /** 
         * Creates and instance of {@link TypeReference} which maintains the generic {@code T} of the passed {@link Class}. 
         * <p> 
         * This method will cache the instance of {@link TypeReference} using the passed {@link Class} as the key. This is 
         * meant to be used with non-generic types such as primitive object types and POJOs, not {@code Map<String, Object>} 
         * or {@code List<Integer>} parameterized types. 
         * 
         * @param clazz {@link Class} that contains generic information used to create the {@link TypeReference}. 
         * @param <T> The generic type. 
         * @return Either the cached or new instance of {@link TypeReference}. 
         */ 
        public static <T> TypeReference<T> createInstance(Class<T> clazz) 
        /** 
         * Returns the {@link Class} representing instance of the {@link TypeReference} created. 
         * 
         * @return The {@link Class} representing instance of the {@link TypeReference} created 
         * using the {@link TypeReference#createInstance(Class)}, otherwise returns {@code null}. 
         */ 
        public Class<T> getJavaClass() 
        /** 
         * Returns the {@link Type} representing {@code T}. 
         * 
         * @return The {@link Type} representing {@code T}. 
         */ 
        public Type getJavaType() 
    } 
} 
/** 
 * Package containing API for tracing. 
 */ 
package com.azure.core.util.tracing { 
    @Deprecated
    /** 
     * Contains constants common AMQP protocol process calls. 
     * 
     * @deprecated use {@link StartSpanOptions} 
     */ 
    public enum ProcessKind { 
        SEND, 
            /** 
             * Amqp Send Message process call to send data. 
             */ 
        MESSAGE, 
            /** 
             * Amqp message process call to receive data. 
             */ 
        PROCESS; 
            /** 
             * Custom process call to process received messages. 
             */ 
    } 
    /** 
     * Represents the tracing span type. 
     */ 
    public enum SpanKind { 
        INTERNAL, 
            /** 
             * Indicates that the span is used internally. 
             */ 
        CLIENT, 
            /** 
             * Indicates that the span covers the client-side wrapper around an RPC or other remote request. 
             */ 
        SERVER, 
            /** 
             * Indicates that the span covers server-side handling of an RPC or other remote request. 
             */ 
        PRODUCER, 
            /** 
             * Indicates that the span describes producer sending a message to a broker. Unlike client and server, there is no 
             * direct critical path latency relationship between producer and consumer spans. 
             */ 
        CONSUMER; 
            /** 
             * Indicates that the span describes consumer receiving a message from a broker. Unlike client and server, there is 
             * no direct critical path latency relationship between producer and consumer spans. 
             */ 
    } 
    @Fluent
    /** 
     * Represents span options that are available before span starts and describe it. 
     */ 
    public final class StartSpanOptions { 
        /** 
         * Create start options with given kind 
         * 
         * @param kind The kind of the span to be created. 
         */ 
        public StartSpanOptions(SpanKind kind) 
        /** 
         * Add link to span. 
         * 
         * @param link link. 
         * @return this instance for chaining. 
         */ 
        public StartSpanOptions addLink(TracingLink link) 
        /** 
         * Sets attribute on span before its started. Such attributes may affect sampling decision. 
         * Adding duplicate attributes, update, or removal is discouraged, since underlying implementations 
         * behavior can vary. 
         * 
         * @param key attribute key. 
         * @param value attribute value. Note that underlying tracer implementations limit supported value types. 
         *              OpenTelemetry implementation supports following types: 
         * <ul> 
         *     <li>{@link String}</li> 
         *     <li>{@code int}</li> 
         *     <li>{@code double}</li> 
         *     <li>{@code boolean}</li> 
         *     <li>{@code long}</li> 
         *     <li>Arrays of the above</li> 
         * </ul> 
         * @return this instance for chaining. 
         */ 
        public StartSpanOptions setAttribute(String key, Object value) 
        /** 
         * Gets all attributes on span that should be set before span is started. 
         * 
         * @return attributes to be set on span and used for sampling. 
         */ 
        public Map<String, Object> getAttributes() 
        /** 
         * Gets links to be set on span. 
         * 
         * @return list of links. 
         */ 
        public List<TracingLink> getLinks() 
        /** 
         * Gets remote parent. 
         * @return context with remote parent span context on it. 
         */ 
        public Context getRemoteParent() 
        /** 
         * Sets remote parent context. 
         * 
         * @param parent context with remote span context. 
         * @return this instance for chaining. 
         */ 
        public StartSpanOptions setRemoteParent(Context parent) 
        /** 
         * Gets span kind. 
         * 
         * @return span kind. 
         */ 
        public SpanKind getSpanKind() 
        /** 
         * Gets span start time. 
         * @return start timestamp. 
         */ 
        public Instant getStartTimestamp() 
        /** 
         * Sets span start timestamp. This is optional and used to record past spans. 
         * If not set, uses current time. 
         * 
         * @param timestamp span start time. 
         * @return this instance for chaining. 
         */ 
        public StartSpanOptions setStartTimestamp(Instant timestamp) 
    } 
    /** 
     * Contract that all tracers must implement to be pluggable into the SDK. 
     * 
     * @see TracerProxy 
     */ 
    public interface Tracer { 
        /** 
         * Key for {@link Context} which indicates that the context contains parent span data. This span will be used 
         * as the parent span for all spans the SDK creates. 
         * <p> 
         * If no span data is listed when the span is created it will default to using this span key as the parent span. 
         * 
         * @deprecated Deprecated in favor of PARENT_TRACE_CONTEXT_KEY, use it to propagate full io.opentelemetry.Context 
         */ 
        @Deprecated String PARENT_SPAN_KEY = "parent-span"; 
        /** 
         * {@link Context} key to store trace context. This context will be used as a parent context 
         * for new spans and propagated in outgoing HTTP calls. 
         * 
         */ 
        String PARENT_TRACE_CONTEXT_KEY = "trace-context"; 
        /** 
         * Key for {@link Context} which indicates that the context contains the name for the user spans that are 
         * created. 
         * <p> 
         * If no span name is listed when the span is created it will default to using the calling method's name. 
         * 
         * @deprecated please pass span name to Tracer.start methods. 
         */ 
        @Deprecated String USER_SPAN_NAME_KEY = "user-span-name"; 
        /** 
         * Key for {@link Context} which indicates that the context contains an entity path. 
         */ 
        String ENTITY_PATH_KEY = "entity-path"; 
        /** 
         * Key for {@link Context} which indicates that the context contains the hostname. 
         */ 
        String HOST_NAME_KEY = "hostname"; 
        /** 
         * Key for {@link Context} which indicates that the context contains a message span context. 
         */ 
        String SPAN_CONTEXT_KEY = "span-context"; 
        /** 
         * Key for {@link Context} which indicates that the context contains a "Diagnostic Id" for the service call. 
         * 
         * @deprecated use {@link Tracer#extractContext(Function)} and {@link Tracer#injectContext(BiConsumer, Context)} 
         *             for context propagation. 
         */ 
        @Deprecated String DIAGNOSTIC_ID_KEY = "Diagnostic-Id"; 
        /** 
         * Key for {@link Context} the scope of code where the given Span is in the current Context. 
         * 
         * @deprecated use {@link Tracer#makeSpanCurrent(Context)} instead. 
         */ 
        @Deprecated String SCOPE_KEY = "scope"; 
        /** 
         * Key for {@link Context} which indicates that the context contains the Azure resource provider namespace. 
         * 
         * @deprecated Pass Azure Resource Provider Namespace to Tracer factory method {@link TracerProvider#createTracer(String, String, String, TracingOptions)} 
         */ 
        @Deprecated String AZ_TRACING_NAMESPACE_KEY = "az.namespace"; 
        /** 
         * Key for {@link Context} which indicates the shared span builder that is in the current Context. 
         * 
         * @deprecated use {@link StartSpanOptions#addLink(TracingLink)} instead 
         */ 
        @Deprecated String SPAN_BUILDER_KEY = "builder"; 
        /** 
         * Key for {@link Context} which indicates the time of the last enqueued message in the partition's stream. 
         * 
         * @deprecated Use {@link StartSpanOptions#addLink(TracingLink)} and pass enqueued time as an attribute on link. 
         */ 
        @Deprecated String MESSAGE_ENQUEUED_TIME = "x-opt-enqueued-time"; 
        /** 
         * Key for {@link Context} which disables tracing for the request associated with the current context. 
         */ 
        String DISABLE_TRACING_KEY = "disable-tracing"; 
        /** 
         * Adds an event to the current span with the provided {@code timestamp} and {@code attributes}. 
         * <p>This API does not provide any normalization if provided timestamps are out of range of the current 
         * span timeline</p> 
         * <p>Supported attribute values include String, double, boolean, long, String [], double [], long []. 
         * Any other Object value type and null values will be silently ignored.</p> 
         * 
         * @param name the name of the event. 
         * @param attributes the additional attributes to be set for the event. 
         * @param timestamp The instant, in UTC, at which the event will be associated to the span. 
         * @throws NullPointerException if {@code eventName} is {@code null}. 
         * @deprecated Use {@link #addEvent(String, Map, OffsetDateTime, Context)} 
         */ 
        @Deprecated default void addEvent(String name, Map<String, Object> attributes, OffsetDateTime timestamp) 
        /** 
         * Adds an event to the span present in the {@code Context} with the provided {@code timestamp} 
         * and {@code attributes}. 
         * <p>This API does not provide any normalization if provided timestamps are out of range of the current 
         * span timeline</p> 
         * <p>Supported attribute values include String, double, boolean, long, String [], double [], long []. 
         * Any other Object value type and null values will be silently ignored.</p> 
         * 
         * <!-- src_embed com.azure.core.util.tracing.addEvent --> 
         * <pre> 
         * Context span = tracer.start("Cosmos.getItem", Context.NONE); 
         * tracer.addEvent("trying another endpoint", Collections.singletonMap("endpoint", "westus3"), OffsetDateTime.now(), span); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.addEvent --> 
         * 
         * @param name the name of the event. 
         * @param attributes the additional attributes to be set for the event. 
         * @param timestamp The instant, in UTC, at which the event will be associated to the span. 
         * @param context the call metadata containing information of the span to which the event should be associated with. 
         * @throws NullPointerException if {@code eventName} is {@code null}. 
         */ 
        default void addEvent(String name, Map<String, Object> attributes, OffsetDateTime timestamp, Context context) 
        /** 
         * Provides a way to link multiple tracing spans. 
         * Used in batching operations to relate multiple requests under a single batch. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Link multiple spans using their span context information</p> 
         * 
         * @param context Additional metadata that is passed through the call stack. 
         * @throws NullPointerException if {@code context} is {@code null}. 
         * 
         * @deprecated use {@link StartSpanOptions#addLink(TracingLink)} )} 
         */ 
        @Deprecated default void addLink(Context context) 
        /** 
         * Adds metadata to the current span. If no span information is found in the context, then no metadata is added. 
         * <!-- src_embed com.azure.core.util.tracing.set-attribute#string --> 
         * <pre> 
         * span = tracer.start("EventHubs.process", Context.NONE); 
         * tracer.setAttribute("bar", "baz", span); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.set-attribute#string --> 
         * 
         * @param key Name of the metadata. 
         * @param value Value of the metadata. 
         * @param context Additional metadata that is passed through the call stack. 
         * @throws NullPointerException if {@code key} or {@code value} or {@code context} is {@code null}. 
         */ 
        void setAttribute(String key, String value, Context context) 
        /** 
         * Sets long attribute. 
         * 
         * <!-- src_embed com.azure.core.util.tracing.set-attribute#int --> 
         * <pre> 
         * Context span = tracer.start("EventHubs.process", Context.NONE); 
         * tracer.setAttribute("foo", 42, span); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.set-attribute#int --> 
         * @param key attribute name 
         * @param value atteribute value 
         * @param context tracing context 
         */ 
        default void setAttribute(String key, long value, Context context) 
        /** 
         * Sets an attribute on span. 
         * Adding duplicate attributes, update, or removal is discouraged, since underlying implementations 
         * behavior can vary. 
         * 
         * @param key attribute key. 
         * @param value attribute value. Note that underlying tracer implementations limit supported value types. 
         *              OpenTelemetry implementation supports following types: 
         * <ul> 
         *     <li>{@link String}</li> 
         *     <li>{@code int}</li> 
         *     <li>{@code double}</li> 
         *     <li>{@code boolean}</li> 
         *     <li>{@code long}</li> 
         * </ul> 
         * @param context context containing span to which attribute is added. 
         */ 
        default void setAttribute(String key, Object value, Context context) 
        /** 
         * Checks if tracer is enabled. 
         * 
         * <!-- src_embed com.azure.core.util.tracing.isEnabled --> 
         * <pre> 
         * if (!tracer.isEnabled()) { 
         *     doWork(); 
         * } else { 
         *     Context span = tracer.start("span", Context.NONE); 
         *     try { 
         *         doWork(); 
         *     } catch (Throwable ex) { 
         *         throwable = ex; 
         *     } finally { 
         *         tracer.end(null, throwable, span); 
         *     } 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.isEnabled --> 
         * 
         * @return true if tracer is enabled, false otherwise. 
         */ 
        default boolean isEnabled() 
        /** 
         * Completes the current tracing span. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Completes the tracing span present in the context, with the corresponding OpenTelemetry status for the given 
         * response status code</p> 
         * 
         * @param responseCode Response status code if the span is in an HTTP call context. 
         * @param error {@link Throwable} that happened during the span or {@code null} if no exception occurred. 
         * @param context Additional metadata that is passed through the call stack. 
         * @throws NullPointerException if {@code context} is {@code null}. 
         * 
         * @deprecated set specific attribute e.g. http_status_code explicitly and use {@link Tracer#end(String, Throwable, Context)}. 
         */ 
        @Deprecated default void end(int responseCode, Throwable error, Context context) 
        /** 
         * Completes span on the context. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Completes the tracing span with unset status</p> 
         * 
         * <!-- src_embed com.azure.core.util.tracing.end#success --> 
         * <pre> 
         * Context messageSpan = tracer.start("ServiceBus.message", new StartSpanOptions(SpanKind.PRODUCER), Context.NONE); 
         * tracer.end(null, null, messageSpan); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.end#success --> 
         * 
         * <p>Completes the tracing span with provided error message</p> 
         * 
         * <!-- src_embed com.azure.core.util.tracing.end#errorStatus --> 
         * <pre> 
         * Context span = tracer.start("ServiceBus.send", new StartSpanOptions(SpanKind.CLIENT), Context.NONE); 
         * tracer.end("amqp:not-found", null, span); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.end#errorStatus --> 
         * 
         * <p>Completes the tracing span with provided exception</p> 
         * 
         * <!-- src_embed com.azure.core.util.tracing.end#exception --> 
         * <pre> 
         * Context sendSpan = tracer.start("ServiceBus.send", new StartSpanOptions(SpanKind.CLIENT), Context.NONE); 
         * try (AutoCloseable scope = tracer.makeSpanCurrent(sendSpan)) { 
         *     doWork(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(null, throwable, sendSpan); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.end#exception --> 
         * 
         * @param errorMessage The error message that occurred during the call, or {@code null} if no error. 
         *   occurred. Any other non-null string indicates an error with description provided in {@code errorMessage}. 
         * 
         * @param throwable {@link Throwable} that happened during the span or {@code null} if no exception occurred. 
         * @param context Additional metadata that is passed through the call stack. 
         * @throws NullPointerException if {@code context} is {@code null}. 
         */ 
        void end(String errorMessage, Throwable throwable, Context context) 
        /** 
         * Extracts the span's context as {@link Context} from upstream. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Extracts the corresponding span context information from a valid diagnostic id</p> 
         * <!-- src_embed com.azure.core.util.tracing.start#remote-parent-extract --> 
         * <pre> 
         * Context parentContext = tracer.extractContext(name -> { 
         *     Object value = messageProperties.get(name); 
         *     return value instanceof String ? (String) value : null; 
         * }); 
         * 
         * StartSpanOptions remoteParentOptions = new StartSpanOptions(SpanKind.CONSUMER) 
         *     .setRemoteParent(parentContext); 
         * 
         * Context spanWithRemoteParent = tracer.start("EventHubs.process", remoteParentOptions, Context.NONE); 
         * 
         * try (AutoCloseable scope = tracer.makeSpanCurrent(spanWithRemoteParent)) { 
         *     doWork(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(null, throwable, spanWithRemoteParent); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.start#remote-parent-extract --> 
         * 
         * @param headerGetter Unique identifier for the trace information of the span and todo. 
         * @return The updated {@link Context} object containing the span context. 
         * @throws NullPointerException if {@code diagnosticId} or {@code context} is {@code null}. 
         */ 
        default Context extractContext(Function<String, String> headerGetter) 
        /** 
         * Extracts the span's context as {@link Context} from upstream. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Extracts the corresponding span context information from a valid diagnostic id</p> 
         * 
         * @param diagnosticId Unique identifier for the trace information of the span. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return The updated {@link Context} object containing the span context. 
         * @throws NullPointerException if {@code diagnosticId} or {@code context} is {@code null}. 
         * @deprecated use {@link Tracer#extractContext(Function)} 
         */ 
        @Deprecated default Context extractContext(String diagnosticId, Context context) 
        /** 
         * Injects tracing context. 
         * 
         * <!-- src_embed com.azure.core.util.tracing.injectContext --> 
         * <pre> 
         * Context httpSpan = tracer.start("HTTP GET", new StartSpanOptions(SpanKind.CLIENT), methodSpan); 
         * tracer.injectContext((headerName, headerValue) -> request.setHeader(headerName, headerValue), httpSpan); 
         * 
         * try (AutoCloseable scope = tracer.makeSpanCurrent(httpSpan)) { 
         *     HttpResponse response = getResponse(request); 
         *     httpResponseCode = response.getStatusCode(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(httpResponseCode, throwable, httpSpan); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.injectContext --> 
         * @param headerSetter callback to set context with. 
         * @param context trace context instance 
         */ 
        default void injectContext(BiConsumer<String, String> headerSetter, Context context) 
        /** 
         * Makes span current. Implementations may put it on ThreadLocal. 
         * Make sure to always use try-with-resource statement with makeSpanCurrent 
         * @param context Context with span. 
         * 
         * <!-- src_embed com.azure.core.util.tracing.makeCurrent --> 
         * <pre> 
         * Context span = tracer.start("EventHubs.process", new StartSpanOptions(SpanKind.CONSUMER), Context.NONE); 
         * try (AutoCloseable scope = tracer.makeSpanCurrent(span)) { 
         *     doWork(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(null, throwable, span); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.makeCurrent --> 
         * 
         * @return Closeable that should be closed in the same thread with try-with-resource statement. 
         */ 
        default AutoCloseable makeSpanCurrent(Context context) 
        /** 
         * Checks if span is sampled in. 
         * 
         * @param span Span to check. 
         * @return true if span is recording, false otherwise. 
         */ 
        default boolean isRecording(Context span) 
        /** 
         * Returns a span builder with the provided name in {@link Context}. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Returns a builder with the provided span name.</p> 
         * 
         * @param spanName Name to give the span for the created builder. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return The updated {@link Context} object containing the span builder. 
         * @throws NullPointerException if {@code context} or {@code spanName} is {@code null}. 
         * @deprecated use {@link StartSpanOptions#addLink(TracingLink)} instead 
         */ 
        @Deprecated default Context getSharedSpanBuilder(String spanName, Context context) 
        /** 
         * Sets the name for spans that are created. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Retrieve the span name of the returned span</p> 
         * 
         * @param spanName Name to give the next span. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return The updated {@link Context} object containing the name of the returned span. 
         * @throws NullPointerException if {@code spanName} or {@code context} is {@code null}. 
         * @deprecated not needed. 
         */ 
        @Deprecated default Context setSpanName(String spanName, Context context) 
        /** 
         * Creates a new tracing span. 
         * <p> 
         * The {@code context} will be checked for information about a parent span. If a parent span is found, the new span 
         * will be added as a child. Otherwise, the parent span will be created and added to the {@code context} and any 
         * downstream {@code start()} calls will use the created span as the parent. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Starts a tracing span with provided method name and explicit parent span</p> 
         * <!-- src_embed com.azure.core.util.tracing.start#name --> 
         * <pre> 
         * // start a new tracing span with given name and parent context implicitly propagated 
         * // in io.opentelemetry.context.Context.current() 
         * 
         * Throwable throwable = null; 
         * Context span = tracer.start("keyvault.setsecret", Context.NONE); 
         * try { 
         *     doWork(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(null, throwable, span); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.start#name --> 
         * 
         * @param methodName Name of the method triggering the span creation. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return The updated {@link Context} object containing the returned span. 
         * @throws NullPointerException if {@code methodName} or {@code context} is {@code null}. 
         */ 
        Context start(String methodName, Context context) 
        /** 
         * Creates a new tracing span. 
         * <p> 
         * The {@code context} will be checked for information about a parent span. If a parent span is found, the new span 
         * will be added as a child. Otherwise, the parent span will be created and added to the {@code context} and any 
         * downstream {@code start()} calls will use the created span as the parent. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Starts a tracing span with provided method name and explicit parent span</p> 
         * <!-- src_embed com.azure.core.util.tracing.start#options --> 
         * <pre> 
         * // start a new CLIENT tracing span with the given start options and explicit parent context 
         * StartSpanOptions options = new StartSpanOptions(SpanKind.CLIENT) 
         *     .setAttribute("key", "value"); 
         * Context spanFromOptions = tracer.start("keyvault.setsecret", options, Context.NONE); 
         * try { 
         *     doWork(); 
         * } catch (Throwable ex) { 
         *     throwable = ex; 
         * } finally { 
         *     tracer.end(null, throwable, spanFromOptions); 
         * } 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.start#options --> 
         * 
         * @param methodName Name of the method triggering the span creation. 
         * @param options span creation options. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return The updated {@link Context} object containing the returned span. 
         * @throws NullPointerException if {@code options} or {@code context} is {@code null}. 
         */ 
        default Context start(String methodName, StartSpanOptions options, Context context) 
        /** 
         * Creates a new tracing span for AMQP calls. 
         * 
         * <p> 
         * The {@code context} will be checked for information about a parent span. If a parent span is found, the new span 
         * will be added as a child. Otherwise, the parent span will be created and added to the {@code context} and any 
         * downstream {@code start()} calls will use the created span as the parent. 
         * 
         * <p> 
         * Sets additional request attributes on the created span when {@code processKind} is 
         * {@link ProcessKind#SEND ProcessKind.SEND}. 
         * 
         * <p> 
         * Returns the diagnostic Id and span context of the returned span when {@code processKind} is 
         * {@link ProcessKind#MESSAGE ProcessKind.MESSAGE}. 
         * 
         * <p> 
         * Creates a new tracing span with remote parent and returns that scope when the given when {@code processKind} 
         * is {@link ProcessKind#PROCESS ProcessKind.PROCESS}. 
         * 
         * <p><strong>Code samples</strong></p> 
         * 
         * <p>Starts a tracing span with provided method name and AMQP operation SEND</p> 
         * 
         * @param spanName Name of the method triggering the span creation. 
         * @param context Additional metadata that is passed through the call stack. 
         * @param processKind AMQP operation kind. 
         * @return The updated {@link Context} object containing the returned span. 
         * @throws NullPointerException if {@code methodName} or {@code context} or {@code processKind} is {@code null}. 
         * 
         * @deprecated use {@link Tracer#start(String, StartSpanOptions, Context)} instead. 
         */ 
        @Deprecated default Context start(String spanName, Context context, ProcessKind processKind) 
    } 
    /** 
     * Resolves and provides {@link Tracer} implementation. 
     * <p> 
     * This class is intended to be used by Azure client libraries and provides abstraction over possible tracing 
     * implementations. 
     * Application developers should use tracing libraries such as OpenTelemetry or Spring tracing. 
     */ 
    public interface TracerProvider { 
        /** 
         * Creates tracer provider instance. 
         * 
         * <p><strong>Code Samples</strong></p> 
         * 
         * <!-- src_embed com.azure.core.util.tracing.TracerProvider#create-tracer --> 
         * <pre> 
         * 
         * LibraryTelemetryOptions libraryOptions = new LibraryTelemetryOptions("azure-storage-blobs") 
         *     .setLibraryVersion("12.20.0") 
         *     .setResourceProviderNamespace("Microsoft.Storage") 
         *     .setSchemaUrl("https://opentelemetry.io/schemas/1.23.1"); 
         * 
         * Tracer tracer = TracerProvider.getDefaultProvider() 
         *     .createTracer(libraryOptions, clientOptions.getTracingOptions()); 
         * HttpPipeline pipeline = new HttpPipelineBuilder() 
         *     .tracer(tracer) 
         *     .clientOptions(clientOptions) 
         *     .build(); 
         * </pre> 
         * <!-- end com.azure.core.util.tracing.TracerProvider#create-tracer --> 
         * 
         * @param libraryOptions Library-specific telemetry options. 
         * @param options Tracing options configured by the application. 
         * @return a tracer instance. 
         */ 
        default Tracer createTracer(LibraryTelemetryOptions libraryOptions, TracingOptions options) 
        /** 
         * Creates named and versioned tracer instance. 
         * 
         * @param libraryName Azure client library package name 
         * @param libraryVersion Azure client library version 
         * @param azNamespace Azure Resource Provider namespace. 
         * @param options instance of {@link TracingOptions} 
         * @return a tracer instance. 
         */ 
        Tracer createTracer(String libraryName, String libraryVersion, String azNamespace, TracingOptions options) 
        /** 
         * Returns default implementation of {@code TracerProvider} that uses SPI to resolve tracing implementation. 
         * @return an instance of {@code TracerProvider} 
         */ 
        static TracerProvider getDefaultProvider() 
    } 
    @Deprecated
    /** 
     * This class provides a means for all client libraries to augment the context information they have received from an 
     * end user with additional distributed tracing information, that may then be passed on to a backend for analysis. 
     * 
     * @see Tracer 
     * @deprecated use {@link TracerProvider} 
     */ 
    public final class TracerProxy { 
        // This class does not have any public constructors, and is not able to be instantiated using 'new'. 
        /** 
         * For the plugged in {@link Tracer tracer}, the key-value pair metadata is added to its current span. If the {@code 
         * context} does not contain a span, then no metadata is added. 
         * 
         * @param key Name of the metadata. 
         * @param value Value of the metadata. 
         * @param context Additional metadata that is passed through the call stack. 
         */ 
        public static void setAttribute(String key, String value, Context context) 
        /** 
         * For the plugged in {@link Tracer tracer}, its current tracing span is marked as completed. 
         * 
         * @param responseCode Response status code if the span is in an HTTP call context. 
         * @param error {@link Throwable} that happened during the span or {@code null} if no exception occurred. 
         * @param context Additional metadata that is passed through the call stack. 
         */ 
        public static void end(int responseCode, Throwable error, Context context) 
        /** 
         * Sets the span name for each {@link Tracer tracer} plugged into the SDK. 
         * 
         * @param spanName Name of the span. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return An updated {@link Context} object. 
         */ 
        public static Context setSpanName(String spanName, Context context) 
        /** 
         * A new tracing span with INTERNAL kind is created for each {@link Tracer tracer} plugged into the SDK. 
         * <p> 
         * The {@code context} will be checked for information about a parent span. If a parent span is found, the new span 
         * will be added as a child. Otherwise, the parent span will be created and added to the {@code context} and any 
         * downstream {@code start()} calls will use the created span as the parent. 
         * 
         * @param methodName Name of the method triggering the span creation. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return An updated {@link Context} object. 
         */ 
        public static Context start(String methodName, Context context) 
        /** 
         * A new tracing span is created for each {@link Tracer tracer} plugged into the SDK. 
         * <p> 
         * The {@code context} will be checked for information about a parent span. If a parent span is found, the new span 
         * will be added as a child. Otherwise, the parent span will be created and added to the {@code context} and any 
         * downstream {@code start()} calls will use the created span as the parent. 
         * 
         * @param methodName Name of the method triggering the span creation. 
         * @param spanOptions span creation options. 
         * @param context Additional metadata that is passed through the call stack. 
         * @return An updated {@link Context} object. 
         */ 
        public static Context start(String methodName, StartSpanOptions spanOptions, Context context) 
        /** 
         * Returns true if tracing is enabled. 
         * 
         * @return true if tracing is enabled. 
         */ 
        public static boolean isTracingEnabled() 
    } 
    @Immutable
    /** 
     * Represents tracing link that connects one trace to another. 
     */ 
    public class TracingLink { 
        /** 
         * Creates link traces without attributes 
         * @param context instance of context that contains span context 
         */ 
        public TracingLink(Context context) 
        /** 
         * Creates link with attributes. 
         * @param context instance of context that contains span context 
         * @param attributes instance of link attributes 
         */ 
        public TracingLink(Context context, Map<String, Object> attributes) 
        /** 
         * Gets link attributes 
         * @return attributes instance 
         */ 
        public Map<String, Object> getAttributes() 
        /** 
         * Gets linked context 
         * @return context instance 
         */ 
        public Context getContext() 
    } 
} 
```