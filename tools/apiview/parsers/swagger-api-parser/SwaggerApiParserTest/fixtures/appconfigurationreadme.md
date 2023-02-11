# AppConfiguration

> see https://aka.ms/autorest

This is the AutoRest configuration file for AppConfiguration.

---

## Getting Started

To build the SDK for AppConfiguration, simply [Install AutoRest](https://aka.ms/autorest/install) and in this folder, run:

> `autorest`

To see additional help and options, run:

> `autorest --help`

---

## Configuration

### Basic Information

These are the global settings for the AppConfiguration API.

``` yaml
openapi-type: arm

tag: package-2022-05-01
```

### Tag: package-2022-05-01

These settings apply only when `--tag=2022-05-01` is specified on the command line.

``` yaml $(tag) == 'package-2022-05-01'
input-file:
- Microsoft.AppConfiguration/stable/2022-05-01/appconfiguration.json
```

### Tag: package-2022-03-01-preview

These settings apply only when `--tag=2022-03-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2022-03-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2022-03-01-preview/appconfiguration.json
```

### Tag: package-2021-10-01-preview

These settings apply only when `--tag=2021-10-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2021-10-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2021-10-01-preview/appconfiguration.json
```

### Tag: package-2021-03-01-preview

These settings apply only when `--tag=2021-03-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2021-03-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2021-03-01-preview/appconfiguration.json
```

### Tag: package-2020-07-01-preview

These settings apply only when `--tag=2020-07-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2020-07-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2020-07-01-preview/appconfiguration.json
```

### Tag: package-2020-06-01

These settings apply only when `--tag=package-2020-06-01` is specified on the command line.

``` yaml $(tag) == 'package-2020-06-01'
input-file:
- Microsoft.AppConfiguration/stable/2020-06-01/appconfiguration.json
```

### Tag: package-2019-11-01-preview

These settings apply only when `--tag=package-2019-11-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2019-11-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2019-11-01-preview/appconfiguration.json
```

### Tag: package-2019-02-01-preview

These settings apply only when `--tag=package-2019-02-01-preview` is specified on the command line.

``` yaml $(tag) == 'package-2019-02-01-preview'
input-file:
- Microsoft.AppConfiguration/preview/2019-02-01-preview/appconfiguration.json
```

### Tag: package-2019-10-01

These settings apply only when `--tag=package-2019-10-01` is specified on the command line.

``` yaml $(tag) == 'package-2019-10-01'
input-file:
- Microsoft.AppConfiguration/stable/2019-10-01/appconfiguration.json
```

---

