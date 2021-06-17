// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import com.azure.core.http.HttpRequest;

import java.net.URI;
import java.util.Objects;

/**
 * Utility class containing constants and methods for re-writing {@link HttpRequest HttpRequests} to use the HTTP fault
 * injector.
 */
public final class Utils {
    /**
     * The default scheme used by HTTP fault injector.
     */
    public static final String DEFAULT_HTTP_FAULT_INJECTOR_SCHEME = "http";

    /**
     * The default host used by HTTP fault injector.
     */
    public static final String DEFAULT_HTTP_FAULT_INJECTOR_HOST = "localhost";

    /**
     * The default HTTP port used by HTTP fault injector.
     */
    public static final int DEFAULT_HTTP_FAULT_INJECTOR_HTTP_PORT = 7777;

    /**
     * The default HTTPS port used by HTTP fault injector.
     */
    public static final int DEFAULT_HTTP_FAULT_INJECTOR_HTTPS_PORT = 7778;

    /**
     * The HTTP header used by HTTP fault injector to determine where it needs to forward a request.
     */
    public static final String HTTP_FAULT_INJECTOR_UPSTREAM_BASE_URI_HEADER =  "X-Upstream-Base-Uri";

    /**
     * Utility method which re-writes the {@link HttpRequest HttpRequest's} URL to use the HTTP fault injector.
     * <p>
     * This will set the HTTP header {@link #HTTP_FAULT_INJECTOR_UPSTREAM_HOST_HEADER} to the request URL used by the
     * HTTP request before re-writing and will update the request URL to use the HTTP fault injector.
     *
     * @param request The {@link HttpRequest} having its URL re-written.
     * @param host The HTTP fault injector host.
     * @param port The HTTP fault injector port.
     * @return The updated {@link HttpRequest} with its URL re-written.
     * @throws NullPointerException If {@code request} or {@code host} are null.
     * @throws IllegalArgumentException If {@code host} is an empty string or {@code port} is
     * an invalid port.
     * @throws IllegalStateException If the request URL isn't valid or the HTTP fault injector URL isn't valid.
     */
    public static HttpRequest rewriteUrlToUseFaultInjector(HttpRequest request, String scheme, String host, int port) {
        validateSchemeHostAndPort(scheme, host, port);

        try {
            URI requestUri = request.getUrl().toURI();
            URI faultInjectorUri = new URI(scheme, requestUri.getUserInfo(), host,
                port, requestUri.getPath(), requestUri.getQuery(), requestUri.getFragment());

            String xUpstreamBaseUri = (requestUri.getPort() < 0)
                ? requestUri.getScheme() + "://" + requestUri.getHost()
                : requestUri.getScheme() + "://" + requestUri.getHost() + ":" + requestUri.getPort();

            return request.setHeader(HTTP_FAULT_INJECTOR_UPSTREAM_BASE_URI_HEADER, xUpstreamBaseUri)
                .setUrl(faultInjectorUri.toURL());
        } catch (Exception exception) {
            throw new IllegalStateException(exception);
        }
    }

    /*
     * Helper method for validating the HTTP fault injector host and port.
     */
    static void validateSchemeHostAndPort(String scheme, String host, int port) {
        Objects.requireNonNull(scheme, "'scheme' cannot be null.");

        Objects.requireNonNull(host, "'host' cannot be null.");

        if (scheme.isEmpty()) {
            throw new IllegalArgumentException("'scheme' must be a non-empty string.");
        }

        if (host.isEmpty()) {
            throw new IllegalArgumentException("'host' must be a non-empty string.");
        }

        if (port < 1 || port > 65535) {
            throw new IllegalArgumentException("'port' must be a valid port number.");
        }
    }

    private Utils() { }
}
