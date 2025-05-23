# Azure SDK QA Bot Shared

## Usage

### Start server

1. Create `env/.env.local` file, and add crendential from `PREPROCESS-ENV-LOCAL-BASE64` in key vault `AzureSDKQABotConfig`
1. `npm run dev:local`

### Request to process inline link and images

1. Install `REST Client` extension
1. Replace the `YOUR_API_KEY` in [preprocess request sample](./sample/preprocess.http) and click `Send Request`
