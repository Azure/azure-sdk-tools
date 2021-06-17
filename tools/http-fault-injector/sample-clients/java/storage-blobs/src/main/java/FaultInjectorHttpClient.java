// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

import com.azure.core.http.HttpClient;
import com.azure.core.http.HttpRequest;
import com.azure.core.http.HttpResponse;
import com.azure.core.util.Context;
import reactor.core.publisher.Mono;

import java.util.Objects;

/**
 * General purpose {@link HttpClient} which re-writes request URLs to use the HTTP fault injector before using an
 * underlying {@link HttpClient} to send the network request.
 */
public final class FaultInjectorHttpClient implements HttpClient {
    private final HttpClient httpClient;
    private final String scheme;
    private final String host;
    private final int port;

    /**
     * Default constructor for {@link FaultInjectorHttpClient} which expects the HTTP fault injector to use
     * {@link Utils#DEFAULT_HTTP_FAULT_INJECTOR_SCHEME}, {@link Utils#DEFAULT_HTTP_FAULT_INJECTOR_HOST}
     * and running on port {@link Utils#DEFAULT_HTTP_FAULT_INJECTOR_HTTP_PORT}.
     *
     * @param httpClient The underlying {@link HttpClient} used to make network requests.
     */
    public FaultInjectorHttpClient(HttpClient httpClient) {
        this(httpClient, Utils.DEFAULT_HTTP_FAULT_INJECTOR_SCHEME,
             Util.DEFAULT_HTTP_FAULT_INJECTOR_HOST, Utils.DEFAULT_HTTP_FAULT_INJECTOR_HTTP_PORT);
    }

    /**
     * Constructor for {@link FaultInjectorHttpClient} which allows for the configuration of which {@code host} and
     * {@code port} the HTTP fault injector is using.
     *
     * @param httpClient The underlying {@link HttpClient} used to make network requests.
     * @param host The host HTTP fault injector is running on.
     * @param port The port HTTP fault injector is using.
     * @throws NullPointerException If {@code httpClient} or {@code host} is null.
     * @throws IllegalArgumentException If {@code host} is an empty string or {@code port} is an invalid port.
     */
    public FaultInjectorHttpClient(HttpClient httpClient, String scheme, String host, int port) {
        this.httpClient = Objects.requireNonNull(httpClient, "'httpClient' cannot be null.");
        Utils.validateSchemeHostAndPort(scheme, host, port);

        this.scheme = scheme;
        this.host = host;
        this.port = port;
    }

    @Override
    public Mono<HttpResponse> send(HttpRequest request) {
        return send(request, Context.NONE);
    }

    @Override
    public Mono<HttpResponse> send(HttpRequest request, Context context) {
        return Mono.defer(() -> Mono.fromCallable(() -> Utils.rewriteUrlToUseFaultInjector(request, scheme, host, port)))
            .flatMap(rewrittenHttpRequest -> httpClient.send(rewrittenHttpRequest, context));
    }
}
