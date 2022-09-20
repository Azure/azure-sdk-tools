
openssl rand -writerand .rnd
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=127.0.0.1/ST=Shanghai/L=Shanghai/O=ame" -keyout %~dp0..\.ssh\127-0-0-1-ca.pem -out %~dp0..\.ssh\127-0-0-1-ca.crt
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=localhost/ST=Shanghai/L=Shanghai/O=ame" -keyout %~dp0..\.ssh\localhost-ca.pem -out %~dp0..\.ssh\localhost-ca.crt
