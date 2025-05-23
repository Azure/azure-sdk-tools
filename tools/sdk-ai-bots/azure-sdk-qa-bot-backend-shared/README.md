# Azure SDK QA Bot Shared

## Usage

### Start server

1. Download crendential in `PREPROCESS-ENV-LOCAL-BASE64` from key vault `AzureSDKQABotConfig`, decode it from `base64`
1. Create `env/.env.local` file and add the decoded content to it
1. `npm run dev:local`

### Request to process inline link and images

1. Install `REST Client` extension
1. Replace the `YOUR_API_KEY` in [preprocess request sample](./sample/preprocess.http) and click `Send Request`
1. Check [test](./src/test/test.e2e.test.ts) for more cases

## Request

### body

```ts
interface PreprocessRequestBody {
  text: string;
  images?: string[];
}
```

### headers

```ts
interface Headers {
  'x-api-key': string
}
```

## Response

### body

```ts

interface PreprocessWarning {
  id: string;
  warning: string;
}

interface PreprocessResult {
  text: string;
  // if there's warnings, it indicates some links are failed to parse
  warnings?: PreprocessWarning[];
}
```
