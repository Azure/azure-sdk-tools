# API View

https://apiview.dev/

## Developing

### Setup your secrets
- Open the .sln file,
- right click the `APIViewWeb` project,
- click `Manage User Secrets`,
- ping someone who works on the project for the undocumented magical secret settings, and
- add the following config:
    ```json
    {
        "Github":
        {
            "ClientId": "...",
            "ClientSecret": "..."
        },
        "Blob":
        {
            "ConnectionString": "..."
        },
        "Cosmos":
        {
            "ConnectionString": "..."
        }
    }

    ```

You can optionally set the conditional compilation symbol `LOCAL_DEV_SKIP_AUTH`
in the APIViewWeb project properties if you want to skip authenticating with
Github.  Some things won't work properly though and this is mostly intended for
new Language Service developers.

### Building client side JS and CSS
```ps
cd $repo
cd .\src\dotnet\APIView\APIViewWeb\Client
npm install
npm run-script build
```
