# Local Generation of the test-proxy image

## Build and Run

Invoke to generate a container (with optional tag):

```
docker build . -t test-proxy
```

Start locally using:

```
docker run -t test-proxy -p 5000:5000 -p 5001:5001
```

Generated files will be within `/etc/proxy/` inside the docker image. Provide a volume as necessary if you that.

## Certificates

All necessary components for dev-certificate usage are present within the `dev_certificate` directory.

**Note that this certificate was generated with no password.**

Within are components of a **dev certificate** that has no usage outside of keeping your local usage of SSL happy. When running the container, you will need to trust this certificate if you want to connect to `https://localhost:5001` without cert validation failures. These certificates have no usage outside of your local box.

```powershell
# ensure root access
> $rootCert = $(Import-PfxCertificate -FilePath ./dev_certificates/dotnet-devcert.pfx -CertStoreLocation 'Cert:\LocalMachine\Root')
```

or via `dotnet`

```powershell
dotnet dev-certs https --clean --import ./dotnet-devcert.pfx --password=""
```

Or add and trust with the toolchain of your choice.

On a ubuntu-flavored distro of linux, feel free to re-use the import mechanism in the local file `import-dev-cert.sh`. Prior to using locally, ensure $CERT_FOLDER environment variable is set to the directory `dev_certificate` to access necessary files!

## Confirm Success

Run the container and attempt a `curl https://localhost:5001/Admin/IsAlive`.

Do you see activity in docker logs + a successfully 200 response?

### Acknowledgement

The bulk of the methodology came from [this beautiful repo](https://github.com/BorisWilhelms/create-dotnet-devcert). Go give it a star!
