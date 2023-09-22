## Overview

This application tokenises a Javascript project into a format useful for JavaScript API reviews. JavaScript API review parser is used by APIView system and CI pipelines to convert a JSON output file created by `api-extracor` to JSON token file intepreted by APIView to create and present review in APIView system.

## Building

1. Go to project directory `<repo root>/tools/apiview/parsers/js-api-parser` and Install npm packages.
    `npm install
2. Run `npm run-script build`

## How To Use

Run API extractor step on JS project to create json output file. This step is integrated within build commend for all Azure SDK projects in azure-sdk-for-js monorepo. So running build step is good enough to create input file for APIvIew parser. You can see a JSON file created in temp directory within package root directory once build step is completed succesfully for the package.

Run `node ./export.js <Path to api-extractor JSON output> <Path to apiviewFile>

For e.g.

`node .\export.js C:\git\azure-sdk-for-js\sdk\core\core-client\temp\core-client.api.json C:\git\azure-sdk-for-js\sdk\core\core-client\temp\apiview.json` 