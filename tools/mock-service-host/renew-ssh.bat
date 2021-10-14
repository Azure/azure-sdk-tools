
openssl rand -writerand .rnd
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=127.0.0.1/ST=Shanghai/L=Shanghai/O=ame" -keyout .ssh\127-0-0-1-ca.pem -out .ssh\127-0-0-1-ca.cer
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=localhost/ST=Shanghai/L=Shanghai/O=ame" -keyout .ssh\localhost-ca.pem -out .ssh\localhost-ca.crt
openssl req -new -newkey rsa:4096 -days 3650 -nodes -x509 -subj "/CN=login.microsoftonline.com/ST=Shanghai/L=Shanghai/O=ame" -keyout .ssh\login-microsoftonline-com-ca.pem -out .ssh\login-microsoftonline-com-ca.crt
