package httpfaultinjectorclient;

import java.net.URI;

import io.netty.handler.ssl.SslContext;
import io.netty.handler.ssl.SslContextBuilder;
import io.netty.handler.ssl.util.InsecureTrustManagerFactory;
import reactor.core.publisher.Mono;
import reactor.netty.http.client.HttpClient;
import reactor.netty.http.client.HttpClientResponse;

public class App {
    private static String _host = "localhost";
    private static int _port = 7778;

    public static void main(String[] args) throws Exception {
        // Allow insecure SSL certs
        SslContext sslContext = SslContextBuilder.forClient().trustManager(InsecureTrustManagerFactory.INSTANCE)
                .build();

        HttpClient httpClient = HttpClient.create()
                .secure(sslContextBuilder -> sslContextBuilder.sslContext(sslContext));

        System.out.println("Sending request...");

        HttpClientResponse response = get(httpClient, "https://www.example.org").block();

        System.out.println(response.status());
    }

    private static Mono<HttpClientResponse> get(HttpClient httpClient, String uri) throws Exception {
        URI upstream = new URI(uri);

        URI faultInjector = new URI(
            upstream.getScheme(),
            upstream.getUserInfo(),
            _host,
            _port,
            upstream.getPath(),
            upstream.getQuery(),
            upstream.getFragment());

        return httpClient
            // Set "Host" header to upstream host
            .headers(headers -> headers.add("Host", upstream.getHost()))
            .get()
            // Set URI to fault injector
            .uri(faultInjector.toString())
            .response();
    }
}
