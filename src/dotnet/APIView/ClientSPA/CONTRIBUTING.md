# Contributing

This is the SPA Client project for [APIView](../APIViewWeb/CONTRIBUTING.md)

The production build hosts the SPA under `/spa/browser/`. Revision and comparison links must respect the configured base path, including when development serves the SPA at `/`.

## Pre-requisites

### Development machine setup
- Ensure you have completed [APIView Setup](../APIViewWeb/CONTRIBUTING.md#development-machine-setup)
- Install angular cli
    ```
    npm install -g @angular/cli
    ```
- Install node modules at `C:\git\azure-sdk-tools\src\dotnet\APIView\ClientSPA`
    ```
    npm install
    ```

### Running SPA CLient Locally
To run client SPA locally use the angular serve command with ssl option
```
 npx ng serve --ssl
```

### Testing SPA Client Locally
To run client SPA tests use angular test command
```
 npx ng test
```
