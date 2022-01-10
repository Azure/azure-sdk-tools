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

After doing any setup described in the [general section](#generally), run the
[trust_proxy_cert.py](https://github.com/Azure/azure-sdk-for-python/blob/main/scripts/devops_tasks/trust_proxy_cert.py) script:
```cmd
~/azure-sdk-for-python> python scripts\devops_tasks\trust_proxy_cert.py
```

This will copy the [test proxy certificate](https://github.com/Azure/azure-sdk-for-python/blob/main/eng/common/testproxy/dotnet-devcert.crt) and place the copy
under `azure-sdk-for-python/.certificate` as a `pem` file.

The only remaining step is to set two environment variables to point to this certificate. The script will output the environment variables and values that you'll
need to set once it finishes running. For example:
```
Set the following certificate paths:
        SSL_CERT_DIR=C:\azure-sdk-for-python\.certificate
        REQUESTS_CA_BUNDLE=C:\azure-sdk-for-python\.certificate\dotnet-devcert.pem
```

Persistently set these environment variables. For example, in a Windows command prompt, use the `SETX` command (not the `SET` command) to set these variables.
Using the example above, you would run:
```cmd
SETX SSL_CERT_DIR "C:\azure-sdk-for-python\.certificate"
SETX REQUESTS_CA_BUNDLE "C:\azure-sdk-for-python\.certificate\dotnet-devcert.pem"
```

A new process should be started up to make these variables available. In a new terminal, running tests with the test proxy should now work with HTTPS requests.

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
