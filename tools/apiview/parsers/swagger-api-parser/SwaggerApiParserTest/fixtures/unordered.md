# Unordered

> see https://aka.ms/autorest

This is the AutoRest configuration file for Unordered.

---

## Getting Started

To build the SDK for ApiManagement, simply [Install AutoRest](https://aka.ms/autorest/install) and in this folder, run:

> `autorest`

To see additional help and options, run:

> `autorest --help`

---

## Configuration

### Basic Information

These are the global settings for the ApiManagement API.

``` yaml
title: UnorderedClient
description: Unordered Client
tag: package-2023-02
```


### Tag: package-2023-02

These settings apply only when `--tag=package-2023-02` is specified on the command line.

```yaml $(tag) == 'package-2023-02'
input-file:
  - z.json
  - a.json
```
