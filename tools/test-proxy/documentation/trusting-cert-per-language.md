# How to trust the `dotnet-devcert.pfx` for your language

## Generally

All necessary components for dev-certificate usage are present within the `dev_certificate` directory.

**Note that this certificate was generated with password "password"**

Within are components of a **dev certificate** that has no usage outside of keeping your local usage of SSL happy. When running the container, you will need to trust this certificate (`dotnet-devcert.pfx`) if you want to connect to `https://localhost:5001` without cert validation failures. This certificate has no usage outside of your local box and is strictly associated with `CN=localhost`.

```powershell
# ensure root access
> $rootCert = $(Import-PfxCertificate -FilePath ./dev_certificate/dotnet-devcert.pfx -CertStoreLocation 'Cert:\LocalMachine\Root')
```

or via `dotnet`

```powershell
dotnet dev-certs https --clean --import ./dotnet-devcert.pfx --password="password"
dotnet dev-certs https --trust
```

On a ubuntu-flavored distro of linux, feel free to re-use the import mechanism in the local file `tools/test-proxy/docker/dev_certificate/import-dev-cert.sh`. Prior to using locally, ensure $CERT_FOLDER environment variable is set to the local directory `dev_certificate` to access necessary files!

Also note that taken to trust this cert will _also apply to installing the dotnet tool directly_. The test-proxy tool will consume the certificate just the same as the docker container does.

## Go

[Reference This Document](https://forfuncsake.github.io/post/2017/08/trust-extra-ca-cert-in-go-app/) for a walkthrough on how to add the certificate to the `trusted pool`.

## Python

TODO:

## .NET

Use the `dotnet dev-certs` approach as recommended in [general section](#generally).

## JS

TODO: