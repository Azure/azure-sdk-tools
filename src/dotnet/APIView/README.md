# API View

https://apiview.dev/

## Developing

### Setup your secrets
- Open the .sln file,
- right click the `APIViewWeb` project,
- click `Manage User Secrets`, and
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

### Building client side JS and CSS
```ps
cd $repo
cd .\src\dotnet\APIView\APIViewWeb\Client
npm install
npm run-script build
```
