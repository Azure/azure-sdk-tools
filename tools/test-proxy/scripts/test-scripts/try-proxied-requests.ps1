$env:HTTP_PROXY="http://localhost:5000"

if (-not $env:PROXY_MANUAL_START) {
    test-proxy start --standard-proxy-mode
}

curl http://localhost:5000/Record/StartUniversal
curl http://httpbin.org/get
