# Change Log - @autorest/testmodeler

This log was last generated on Fri, 11 Mar 2022 09:19:34 GMT and should not be manually modified.

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

