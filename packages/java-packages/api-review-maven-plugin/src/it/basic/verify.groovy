// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

def markdownFile = new File(basedir, "target/api-review/api.md")
def metadataFile = new File(basedir, "target/api-review/api.metadata.yml")
assert markdownFile.isFile()
assert metadataFile.isFile()
def bytes = markdownFile.bytes
def markdown = new String(bytes, "UTF-8")
assert markdown.contains("Widget")
assert !markdown.contains("\r")
assert markdown.endsWith("\n") && !markdown.endsWith("\n\n")
def hash = java.security.MessageDigest.getInstance("SHA-256").digest(bytes).encodeHex().toString()
assert metadataFile.getText("UTF-8") == "apiHash: ${hash}\npackageVersion: '1.0.0'\npackageRelativePath: 'sdk/test/azure-widget'\n"
return true
