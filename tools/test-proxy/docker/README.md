# Common Docker Questions


## Local Generation of the test-proxy image

### Build and Run

**Be aware there is a pre-step to prepare the working directory before building the dockerfile.**

This is necessary to access supporting certificate files located in eng/common. There is intent to adjust this build upon a `test-assets image` that will allow us to relocate build context to this local folder, but that is as yet incomplete.

Prior to any other steps, invoke:

```pwsh
./prepare.ps1
```

Then, invoke to generate a container (with optional tag):

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

### Windows Container

Actions images do not support swapping between `Linux` and `Windows` containers. This lack of support means that the azure-sdk engsys must also provide a test-proxy container for windows containers.

To build _that_, use the following command.

```docker
docker build . -f dockerfile-win -t test-proxy
```

### Certificates

All necessary components for dev-certificate usage are present within the `eng/common/testproxy` directory. Reference [trusting-cert-per-language.md](../documentation/trusting-cert-per-language.md) to learn how to add and trust with the toolchain of your choice.

Please note that each language + its SSL stack will provide different mechanisms for validating SSL certificates. Again, reference [trusting-cert-per-language.md](../documentation/trusting-cert-per-language.md) to understand the process beyond the most general case.

### Confirm Success

Run the container and attempt a `curl https://localhost:5001/Admin/IsAlive`.

Do you see activity in docker logs + a 200 response?

### Acknowledgement

The bulk of the methodology came from [this beautiful repo](https://github.com/BorisWilhelms/create-dotnet-devcert). Go give it a star!

## Troubleshooting Access to Public Container Registry

Most issues we've seen are related to having a prior `az acr login` or the like. The registry is intended to be anonymously available.

If your error looks something like this:

```
> docker pull azsdkengsys.azurecr.io/engsys/testproxy-lin:latest
Error response from daemon: Head https://azsdkengsys.azurecr.io/v2/engsys/testproxy-lin/manifests/latest: unauthorized: authentication required
```

Need to clear multiple sets of credentials.

1. `docker logout`
2. check `Windows Credentials` under `Credential Manager` -> Remove `azsdkengsys.azurecr.io` windows credential.
3. Attempt another pull

This occurs when a user has a **prior login** to `azsdkengsys.azurecr.io`. `az acr login` sets a windows credential with the value of the generated token. This token has a limited expiry, but it doesn't self clean. This means that even if you're already "logged out" of `az` context, etc, your console will still attempt to use that same expired token.  Need to manually clean it out.

For errors that look like:

```
> docker pull azsdkengsys.azurecr.io/engsys/testproxy-lin:latest
Error response from daemon: Get https://azsdkengsys.azurecr.io/v2/: x509: certificate has expired or is not yet valid
```

Open up docker desktop and click the bug.

![image](https://user-images.githubusercontent.com/45376673/126579279-5048132c-39c0-4b40-a3b2-6da03553097b.png)

Then click `Restart`. Reference [this stack overflow](https://stackoverflow.com/questions/35289802/docker-pull-error-x509-certificate-has-expired-or-is-not-yet-valid) post. The docker daemon clock doesn't stay synced with windows, which causes these certificate failures.

