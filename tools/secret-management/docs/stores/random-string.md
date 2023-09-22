# Random String

## Implementing Class
[RandomStringGenerator](../../Azure.Sdk.Tools.SecretRotation.Stores.Generic/RandomStringGenerator.cs)

## Configuration Key
Random String

## Supported Functions
Origin

## Parameters

| Name                 | Type    | Description                                                             |
| -------------------- | ------- | ----------------------------------------------------------------------- |
| length               | integer | The length of the string to create                                      |
| useLowercase         | bool    | optional, should lowercase letters appear in the string `[a-z]`         |
| useUppercase         | bool    | optional, should uppercase letters appear in the string `[A-Z]`         |
| useNumbers           | bool    | optional, should numbers appear in the string `[0-9]`                   |
| useSpecialCharacters | bool    | optional, should special characters appear in the string `[!@#$%^&*()]` |

## Notes
At least one character class must be used. The resulting string will include at least one character from each of the character classes used.