
openssl rand -writerand .rnd
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=127.0.0.1/ST=Shanghai/L=Shanghai/O=ame" -keyout $PSScriptRoot\..\.ssh\127-0-0-1-ca.pem -out $PSScriptRoot\..\.ssh\127-0-0-1-ca.crt
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=localhost/ST=Shanghai/L=Shanghai/O=ame" -keyout $PSScriptRoot\..\.ssh\localhost-ca.pem -out $PSScriptRoot\..\.ssh\localhost-ca.crt
