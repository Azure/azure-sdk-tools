# Common Docker Questions

## Local Generation of the test-proxy image

### Build and Run

Invoke to generate a container (with optional tag):

```docker
docker build . -t test-proxy
```

Start locally using:

```docker
docker run -p 5000:5000 -p 5001:5001 -v <yourvolume>:/etc/testproxy -t test-proxy 
```

Generated files will be within `/etc/testproxy/` inside the docker image. Providing a volume as shown above is necessary if you want to propogate these recordings onto your local file system.

If you _don't_ provide a volume bound to `/etc/testproxy`, it's not actually the end of the world. Use `docker cp` to grab those files into your host system.

```docker
docker cp <containerid>:/etc/testproxy/ <target local path local path>
```

### Certificates

All necessary components for dev-certificate usage are present within the `dev_certificate` directory.

**Note that this certificate was generated with no password.**

Within are components of a **dev certificate** that has no usage outside of keeping your local usage of SSL happy. When running the container, you will need to trust this certificate if you want to connect to `https://localhost:5001` without cert validation failures. These certificates have no usage outside of your local box.

```powershell
# ensure root access
> $rootCert = $(Import-PfxCertificate -FilePath ./dev_certificate/dotnet-devcert.pfx -CertStoreLocation 'Cert:\LocalMachine\Root')
```

or via `dotnet`

```powershell
dotnet dev-certs https --clean --import ./dotnet-devcert.pfx --password=""
```

Or add and trust with the toolchain of your choice.

On a ubuntu-flavored distro of linux, feel free to re-use the import mechanism in the local file `import-dev-cert.sh`. Prior to using locally, ensure $CERT_FOLDER environment variable is set to the local directory `dev_certificate` to access necessary files!

### Confirm Success

Run the container and attempt a `curl https://localhost:5001/Admin/IsAlive`.

Do you see activity in docker logs + a successfully 200 response?

### Acknowledgement

The bulk of the methodology came from [this beautiful repo](https://github.com/BorisWilhelms/create-dotnet-devcert). Go give it a star!

## Troubleshooting Access to Public Container Registry

Most issues we've seen are related to having a prior `az acr login` or the like. The registry is intended to be anonymously available.

If your error looks something like this:

```
> docker pull azsdkengsys.azurecr.io/engsys/ubuntu_testproxy_server:latest
Error response from daemon: Head https://azsdkengsys.azurecr.io/v2/engsys/ubuntu_testproxy_server/manifests/latest: unauthorized: authentication required
```

Need to clear multiple sets of credentials.

1. `docker logout`
2. check `Windows Credentials` under `Credential Manager` -> Remove `azsdkengsys.azurecr.io` windows credential.
3. Attempt another pull

This occurs when a user has a **prior login** to `azsdkengsys.azurecr.io`. `az acr login` sets a windows credential with the value of the generated token. This token has a limited expiry, but it doesn't self clean. This means that even if you're already "logged out" of `az` context, etc, your console will still attempt to use that same expired token.  Need to manually clean it out.

For errors that look like:

```
> docker pull azsdkengsys.azurecr.io/engsys/ubuntu_testproxy_server:latest
Error response from daemon: Get https://azsdkengsys.azurecr.io/v2/: x509: certificate has expired or is not yet valid
```

Open up docker desktop and click the bug.

![image](https://user-images.githubusercontent.com/45376673/126579279-5048132c-39c0-4b40-a3b2-6da03553097b.png)

Then click `Restart`. Reference [this stack overflow](https://stackoverflow.com/questions/35289802/docker-pull-error-x509-certificate-has-expired-or-is-not-yet-valid) post. The docker daemon clock doesn't stay synced with windows, which causes these certificate failures.
