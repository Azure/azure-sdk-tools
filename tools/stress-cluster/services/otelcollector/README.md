# OpenTelemetry custom distro for stress testing

This `Dockerfile` builds the image we use for our OpenTelemetry collector in the stress testing cluster.

It includes only the Azure Monitor and 'debug' exporters, which shrinks it down quite a bit and uses only mariner-based images, for both building and for the final app image.

To test this out locally:

1. Create a .env file that looks like this:

   ```bash
   # make sure you bring in the quotes
   APPLICATIONINSIGHTS_CONNECTION_STRING='<appinsights connection string from the Azure portal>'
   ```

2. Run ./localtest.sh
