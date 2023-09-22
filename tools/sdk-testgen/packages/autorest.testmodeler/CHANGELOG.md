# Change Log - @autorest/testmodeler

This log was last generated on Fri, 21 Jul 2023 08:36:29 GMT and should not be manually modified.

## 2.6.1
Fri, 21 Jul 2023 08:36:29 GMT

### Patches

- Support DataFactoryKeyVaultSecretReference of DataFactoryElement

## 2.6.0
Mon, 19 Jun 2023 08:50:04 GMT

### Minor changes

- Support DataFactoryElement

## 2.5.2
Wed, 14 Jun 2023 08:54:31 GMT

### Patches

- Upgrade m4 core version.

## 2.5.1
Mon, 13 Mar 2023 02:20:40 GMT

### Patches

- fix a bug of parsing body parameter and add eslint

## 2.5.0
Thu, 02 Feb 2023 09:40:48 GMT

### Patches

- Use webpack and remove npm-shrinkwrap.json
- Update dependency semver ranges

## 2.4.0
Mon, 12 Dec 2022 06:18:04 GMT

### Patches

- Upgrade @autorest/codemodel to 4.19.2, and use modelerfour@4.25.0

## 2.3.2
Mon, 22 Aug 2022 03:25:13 GMT

### Patches

- add option --explicit-types to allow customize tagged types in exported code model.
- fix bugs in loading remote api scenario
- fix bug in outputVariableModel generation when there is no response in scenario step

## 2.3.1
Fri, 05 Aug 2022 09:25:55 GMT

### Patches

- Consolidate operation groups for examples with m4.

## 2.3.0
Thu, 21 Jul 2022 06:46:35 GMT

### Minor changes

- Use modelerfour@4.23.7 and add securityParameters in ExampleModel for ApiKey securityDefinitions.
- use apiscenario 1.2 via oav@3.0.3
- add --testmodeler.api-scenario-loader-option to make api scenario loader configurable
- Add --testmodeler.export-explicit-type to support explicit types in exported codemodel.

## 2.2.5
Sun, 24 Apr 2022 09:41:30 GMT

### Patches

- Use oav@2.12.1

## 2.2.4
Tue, 29 Mar 2022 01:56:58 GMT

### Patches

- Oav loader cache problem.

## 2.2.3
Wed, 23 Mar 2022 06:19:34 GMT

### Patches

- upgrade @autorest/codemodel from 4.17.2 to 4.18.2

## 2.2.2
Mon, 14 Mar 2022 09:19:34 GMT

### Patches

- add option --testmodeler.add-armtemplate-payload-string (default as False) to enable/disable StepArmTemplateModel.armTemplatePayloadString

## 2.2.1
Thu, 03 Mar 2022 02:23:21 GMT

### Patches

- Use autorest session to log warning.

## 2.2.0
Tue, 22 Feb 2022 10:58:11 GMT

### Minor changes

- Add support for json pointer modeler for output variable of scenario test.

## 2.1.0
Fri, 11 Feb 2022 09:47:39 GMT

### Minor changes

- Refine scenario test modeler.

### Patches

- Fix newline platform compatibility problem.

## 2.0.0
Wed, 12 Jan 2022 09:10:46 GMT

### Breaking changes

- load api scenario with oav@2.11.3

## 1.1.0
Wed, 12 Jan 2022 02:19:25 GMT

### Minor changes

- Remove decode for query param in example model to be align with swagger example rule.
- Refactor config get set method.

### Patches

- Upgrade to latest autorest/core.

## 1.0.4
Fri, 12 Dec 2021 07:35:05 GMT

### Patches

- Match body parameter case insensivly.

## 1.0.3
Mon, 29 Nov 2021 06:10:09 GMT

### Patches

- 1. uncapitalize example parameter names. 2. skip non-json examples

## 1.0.2
Mon, 15 Nov 2021 09:39:03 GMT

### Patches

- Fix `additionalProperties` problem.

## 1.0.1
Tue, 09 Nov 2021 09:17:24 GMT

### Patches

- Fix `AdditionalProperties` model problem and duplicate operation id problem.

## 1.0.0
Fri, 29 Oct 2021 05:53:50 GMT

### Breaking changes

- Init public version of autorest extension for test modeler

