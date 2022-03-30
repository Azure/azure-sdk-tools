# Change Log - @autorest/gotest

This log was last generated on Tue, 29 Mar 2022 01:56:58 GMT and should not be manually modified.

## 2.2.1
Tue, 29 Mar 2022 01:56:58 GMT

### Patches

- use @autorest/testmodeler@2.2.3
- Client subscription param problem.
- LRO need to get final response type name.

## 2.2.0
Thu, 17 Mar 2022 07:43:40 GMT

### Minor changes

- Add sample generation.
- Update to latest azcore for mock test.
- Consolidate manual-written and auto-generated scenario test code.

### Patches

- Change from go get to go install to prevent warnning.
- Operation has no subscriptionID param but client has, need to handle it seperately.

## 2.1.4
Mon, 07 Mar 2022 02:56:30 GMT

### Patches

- Fix wrong generation for output variable with chain invoke.

## 2.1.3
Thu, 03 Mar 2022 05:50:36 GMT

### Patches

- Change response usage in examples.

## 2.1.2
Thu, 03 Mar 2022 02:23:21 GMT

### Patches

- Upgrade to latest testmodeler.

## 2.1.1
Thu, 24 Feb 2022 05:54:42 GMT

### Patches

- Fix param render bug for resource deployment step in api scenario.

## 2.1.0
Tue, 22 Feb 2022 10:58:11 GMT

### Minor changes

- Change output variable value fetch method according to new testmodeler.

## 2.0.0
Fri, 11 Feb 2022 09:47:39 GMT

### Breaking changes

- Add scenario test generation support.
- Add recording support to scenario test.

## 1.3.0
Wed, 12 Jan 2022 09:10:46 GMT

### Minor changes

- use new api scenario through testmodeler

## 1.2.0
Wed, 12 Jan 2022 02:19:25 GMT

### Minor changes

- Compatible with latest azcore and azidentity.
- Add response check to mock test generation.

### Patches

- Fix result check problem for lro operation with pageable config.
- Fix result log problem for multiple response operation.
- Fix wrong param name for pageable opeation with custom item name.
- Different conversion for choice and sealedchoice.
- Fix wrong generation of null value for object.
- Fix some generated problems including: polymorphism response type, client param, pager response check.
- Fix multiple time format and any-object default value issue.
- Refine log for mock test and fix array item code generate bug.
- Upgrade to latest autorest/core and autorest/go.

## 1.1.3
Mon, 29 Nov 2021 06:10:09 GMT

### Patches

- Replace incomplete response check with just log temporarily.

## 1.1.2
Mon, 15 Nov 2021 09:39:03 GMT

### Patches

- Fix some generation corner case.

## 1.1.1
Tue, 09 Nov 2021 10:20:51 GMT

### Patches

- Remove `go mod tidy` process.

## 1.1.0
Tue, 09 Nov 2021 09:17:24 GMT

### Minor changes

- Refactor structure and fix most of generation problem.

## 1.0.0
Mon, 01 Nov 2021 09:01:05 GMT

### Breaking changes

- Init public version of autorest extension for GO test generation

