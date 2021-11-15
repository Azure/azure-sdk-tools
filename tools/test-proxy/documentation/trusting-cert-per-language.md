# How to trust the `dotnet-devcert.pfx` for your language

## Generally

All necessary components for dev-certificate usage are present within the `eng/common/testproxy/` directory.

**Note that this certificate was generated with password "password"**

Within this folder are components of a **dev certificate** that has no usage outside of keeping your local usage of SSL happy. When running the container, you will need to trust `dotnet-devcert.pfx` if you want to connect to `https://localhost:5001` without cert validation failures. This certificate has no usage outside of your local box and is strictly associated with `CN=localhost`.

```powershell
# ensure root access
> $rootCert = $(Import-PfxCertificate -FilePath eng/common/testproxy/dotnet-devcert.pfx -CertStoreLocation 'Cert:\LocalMachine\Root')
```

or via `dotnet`

```powershell
dotnet dev-certs https --clean --import eng/common/testproxy/dotnet-devcert.pfx --password="password"
dotnet dev-certs https --trust
```

On a ubuntu-flavored distro of linux, feel free to re-use the import mechanism in the local file `eng/common/testproxy/import-dev-cert.sh`. Prior to using locally, ensure $CERT_FOLDER environment variable is set to the local directory containing the script. Otherwise it won't be able to access necessary files!

Also note that taken to trust this cert will _also apply to installing the dotnet tool directly_. The test-proxy tool will consume the certificate just the same as the docker container does.

## Go

[Reference This Document](https://forfuncsake.github.io/post/2017/08/trust-extra-ca-cert-in-go-app/) for a walkthrough on how to add the certificate to the `trusted pool`.

## Python

As always, [stack overflow comes through](https://stackoverflow.com/a/39358282). Unlike `go`, there is nothing specific that needs to happen in the test code itself.

As a pre-req, ensure that the certificate file present [here](https://github.com/Azure/azure-sdk-for-python/blob/main/eng/common/testproxy/dotnet-devcert.crt) is downloaded and renamed to the `pem` file format. Normally a `.crt` file would be binary encoded in DER format, but the ASP.NET dev certs are not encoded that way when they are created, so we don't need to worry about re-encoding anything!

To trust this certificate...

1. Ensure that `SSL_CERTS_DIR` points to the directory containing your newly downloaded PEM file.
2. The `requests` library does NOT honor the `SSL_CERTS_DIR` environment variable. They have no intention of doing so. Instead, you'll need to...
   1. Find your `certifi` certificate bundle using `requests.certs.where()`. Once located, **combine it** with the newly downloaded pemfile mentioned above.
   2. [Here is scripted example of combining](https://github.com/Azure/azure-sdk-for-python/commit/3f4ef4d64382edd74a830bfb71622c6fd8edb5c1)
   3. Set `REQUESTS_CA_BUNDLE` to the location of the newly combined pemfile.
3. Ensure SSL verification is enabled still, notice that your requests to the test proxy still succeed!

## Java

A given certificate must be added to the `Java Certificate Store`.

1. Grab the `dotnet-devcert.crt` from the `eng/common/testproxy` directory of any azure-sdk language repo. Keep its location handy.
2. Find the Java install that you will be using to run your tests. EG: `C:\Program Files\Java\jre1.8.0_301`.
3. Open your preferred shell in `admin` mode.
4. Run `keytool.exe -cacerts -importcert -file <path-to-dotnet-devcert.crt> -alias DotNetDevCert`
   1. `keytool.exe` is part of the the `bin` folder within your Java install.
5. When prompted, the default password to the `Java Certificate Store` is `changeit`.
6. To clean up, run `keytool.exe -cacerts -delete -alias DotNetDevCert`.

## .NET

Use the `dotnet dev-certs` approach as recommended in [general section](#generally).

## JS

TODO:
