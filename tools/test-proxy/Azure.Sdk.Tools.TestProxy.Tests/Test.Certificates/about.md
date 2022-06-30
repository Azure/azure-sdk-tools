The values present in test_pem_value, test_pem_key are from an expired test environment.

The value present in test_public-key-only_pem is locally generated and from a self-signed cert. Generation steps:

```pwsh
$certname = "Your Cert Name"
$cert = New-SelfSignedCertificate -Subject "CN=$certname" -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec Signature -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256
Export-Certificate -Cert $cert -FilePath C:\Users\scbedd\Desktop\$certname.cer
openssl x509 -inform DER -outform PEM -in C:\Users\scbedd\Desktop\test_public_key_certificate.cer -out server.crt.pem
```