# Azure SDK Tools Contribution Guidelines

This is the repository of tool libraries used by Azure SDK purpose.
For every developed tool, create a new folder with name of the tool brief description. 
Please provide single purpose of every tool.
Do not add third party tool directly in your folder, use it as dependencies.

# README

Please add README file for every tool to illustrate the purpose of the tool, how to use the tool, test and maintain the tool.

# Testing

Please provide certain test cases to cover important workflow, especially the tool using in azure pipelines.

# Release ci.yml

For tool which is to publish to public repository, please provide ci.yml for building, testing and releasing. 
