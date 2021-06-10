// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import com.azure.core.http.HttpPipelineCallContext;
import com.azure.core.http.HttpPipelineNextPolicy;
import com.azure.core.http.HttpPipelinePosition;
import com.azure.core.http.HttpRequest;
import com.azure.core.http.HttpResponse;
import com.azure.core.http.policy.HttpPipelinePolicy;
import reactor.core.publisher.Mono;

import java.net.URI;
import java.util.Objects;

/**
 * General purpose {@link HttpPipelinePolicy} which re-writes the request URL to send it to the HTTP fault injector.
 */
public final class FaultInjectorUrlRewriterPolicy implements HttpPipelinePolicy {
    private static final String DEFAULT_HOST = "localhost";
    private static final int DEFAULT_PORT = 7778;

    private final String host;
    private final int port;

    /**
     * Default constructor for {@link FaultInjectorUrlRewriterPolicy} which expects the HTTP fault injector to use
     * the {@code localhost} and running on port {@code 7778} (HTTPS).
     */
    public FaultInjectorUrlRewriterPolicy() {
        this(DEFAULT_HOST, DEFAULT_PORT);
    }

    /**
     * Constructor used to configure re-writing the request URL to the non-default HTTP fault injector host and port.
     * @param host The host HTTP fault injector is running on.
     * @param port The port HTTP fault injector is using.
     * @throws NullPointerException If {@code host} is null.
     * @throws IllegalArgumentException If {@code host} is an empty string or {@code port} is an invalid port.
     */
    public FaultInjectorUrlRewriterPolicy(String host, int port) {
        Objects.requireNonNull(host, "'host' cannot be null.");

        if (host.isEmpty()) {
            throw new IllegalArgumentException("'host' must be a non-empty string.");
        }

        if (port < 1 || port > 65535) {
            throw new IllegalArgumentException("'port' must be a valid port number.");
        }

        this.host = host;
        this.port = port;
    }

    @Override
    public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) {
        try {
            HttpRequest request = context.getHttpRequest();

            URI requestUri = request.getUrl().toURI();
            URI faultInjectorUri = new URI(requestUri.getScheme(), requestUri.getUserInfo(), host, port,
                requestUri.getPath(), requestUri.getQuery(), requestUri.getFragment());

            request.setHeader("X-Upstream-Host", requestUri.getHost());
            request.setUrl(faultInjectorUri.toURL());
        } catch (Exception ex) {
            return Mono.error(ex);
        }

        return next.process();
    }

    @Override
    public HttpPipelinePosition getPipelinePosition() {
        // The policy should be ran per retry in case calls are made to a secondary, fail-over host.
        return HttpPipelinePosition.PER_RETRY;
    }
}
