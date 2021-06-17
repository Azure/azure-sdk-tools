package httpfaultinjectorclient;

import java.net.URI;

import reactor.core.publisher.Mono;
import reactor.netty.http.client.HttpClient;
import reactor.netty.http.client.HttpClientResponse;

public class App {
    private static String _scheme = "http";
    private static String _host = "localhost";
    private static int _port = 7777;

    public static void main(String[] args) throws Exception {
        HttpClient httpClient = HttpClient.create();

        // When using HTTPS to the fault injector, you must either add the .NET developer certificate to the Java cacerts keystore,
        // or uncomment the following lines to disable SSL validation.
        //
        // io.netty.handler.ssl.SslContext sslContext = io.netty.handler.ssl.SslContextBuilder
        //     .forClient().trustManager(io.netty.handler.ssl.util.InsecureTrustManagerFactory.INSTANCE).build();
        // httpClient = httpClient.secure(sslContextBuilder -> sslContextBuilder.sslContext(sslContext));

        System.out.println("Sending request...");

        HttpClientResponse response = get(httpClient, "https://www.example.org").block();

        System.out.println(response.status());
    }

    private static Mono<HttpClientResponse> get(HttpClient httpClient, String uri) throws Exception {
        URI upstream = new URI(uri);

        URI upstreamBase = new URI(
            upstream.getScheme(),
            upstream.getUserInfo(),
            upstream.getHost(),
            upstream.getPort(),
            null,
            null,
            null);

        URI faultInjector = new URI(
            _scheme,
            upstream.getUserInfo(),
            _host,
            _port,
            upstream.getPath(),
            upstream.getQuery(),
            upstream.getFragment());

        return httpClient
            // Set "X-Upstream-Base-Uri" header to upstream host
            .headers(headers -> headers.add("X-Upstream-Base-Uri", upstreamBase.toString()))
            .get()
            // Set URI to fault injector
            .uri(faultInjector.toString())
            .response();
    }
}
