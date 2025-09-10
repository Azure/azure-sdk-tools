if (-not $env:PROXY_MANUAL_START) {
    test-proxy start --standard-proxy-mode
}

curl -X POST http://localhost:5000/Record/StartUniversal `
     -H "Content-Type: application/json" `
     -d '{"x-recording-file":"sdk/testrecord.json"}' -v

curl --proxy http://localhost:5000 http://httpbin.org/get

curl -X POST http://localhost:5000/Record/StopUniversal -v

curl -X POST http://localhost:5000/Record/StopUniversal
