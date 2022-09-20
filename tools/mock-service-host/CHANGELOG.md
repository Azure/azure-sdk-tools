# Change Log - @azure-tools/mock-service-host

## 0.1.17
2022-06-29

### Patches

- fix a bug of failing to set status for lro-callback GET
- mock resourceType for nested providers

## 0.1.16
2022-06-08

### Patches

- Use example response prior to mock response
- Use 200 response if can't find LRO callback url

## 0.1.15
2022-05-16

### Patches

- LRO mock priority problem

## 0.1.14
2022-04-21

### Patches

- Use oav@2.12.0 to fix security alert

## 0.1.13
2022-04-08

### Patches

- Fix LRO response sequence problem

## 0.1.12
2022-03-03

### Patches

- Feedback LRO response with status code 201, 202 or 204
- Remove all "nextLink" property from response body
- Do not escape for parameter values in request path

## 0.1.11
2022-01-19

### Patches

- mock resource type for non-pagable list.

