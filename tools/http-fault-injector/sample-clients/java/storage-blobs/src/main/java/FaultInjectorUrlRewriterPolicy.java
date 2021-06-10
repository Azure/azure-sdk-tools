// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import com.azure.core.http.HttpPipelineCallContext;
import com.azure.core.http.HttpPipelineNextPolicy;
import com.azure.core.http.HttpPipelinePosition;
import com.azure.core.http.HttpResponse;
import com.azure.core.http.policy.HttpPipelinePolicy;
import reactor.core.publisher.Mono;

/**
 * General purpose {@link HttpPipelinePolicy} which re-writes the request URL to send it to the HTTP fault injector.
 */
public final class FaultInjectorUrlRewriterPolicy implements HttpPipelinePolicy {
    private final String host;
    private final int port;

    /**
     * Default constructor for {@link FaultInjectorUrlRewriterPolicy} which expects the HTTP fault injector to use
     * {@link Utils#DEFAULT_HTTP_FAULT_INJECTOR_HOST} and running on port
     * {@link Utils#DEFAULT_HTTP_FAULT_INJECTOR_HTTPS_PORT}.
     */
    public FaultInjectorUrlRewriterPolicy() {
        this(Utils.DEFAULT_HTTP_FAULT_INJECTOR_HOST, Utils.DEFAULT_HTTP_FAULT_INJECTOR_HTTPS_PORT);
    }

    /**
     * Constructor used to configure re-writing the request URL to the non-default HTTP fault injector host and port.
     *
     * @param host The host HTTP fault injector is running on.
     * @param port The port HTTP fault injector is using.
     * @throws NullPointerException If {@code host} is null.
     * @throws IllegalArgumentException If {@code host} is an empty string or {@code port} is an invalid port.
     */
    public FaultInjectorUrlRewriterPolicy(String host, int port) {
        Utils.validateHostAndPort(host, port);

        this.host = host;
        this.port = port;
    }

    @Override
    public Mono<HttpResponse> process(HttpPipelineCallContext context, HttpPipelineNextPolicy next) {
        context.setHttpRequest(Utils.rewriteUrlToUseFaultInjector(context.getHttpRequest(), host, port));

        return next.process();
    }

    @Override
    public HttpPipelinePosition getPipelinePosition() {
        // The policy should be ran per retry in case calls are made to a secondary, fail-over host.
        return HttpPipelinePosition.PER_RETRY;
    }
}
