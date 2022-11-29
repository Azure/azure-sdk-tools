// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ApiViewProcessor.hpp"
#include "AstNode.hpp"
#include "ProcessorImpl.hpp"
#include <nlohmann/json.hpp>
#include <string_view>

using namespace clang;
using namespace clang::tooling;

ApiViewProcessor::ApiViewProcessor(
    std::string_view const& pathToProcessor,
    std::string_view const apiViewSettings)
    : m_processorImpl{std::make_unique<ApiViewProcessorImpl>(pathToProcessor, apiViewSettings)}
{
}
ApiViewProcessor::ApiViewProcessor(
    std::string_view const& pathToProcessor,
    nlohmann::json const& apiViewSettings)
    : m_processorImpl{std::make_unique<ApiViewProcessorImpl>(pathToProcessor, apiViewSettings)}
{
}

ApiViewProcessor::~ApiViewProcessor() {}

int ApiViewProcessor::ProcessApiView() { return m_processorImpl->ProcessApiView(); }

std::unique_ptr<AzureClassesDatabase> const& ApiViewProcessor::GetClassesDatabase()
{
  return m_processorImpl->GetClassesDatabase();
}
std::string_view const ApiViewProcessor::ReviewName() { return m_processorImpl->ReviewName(); };
std::string_view const ApiViewProcessor::ServiceName() { return m_processorImpl->ServiceName(); };
std::string_view const ApiViewProcessor::PackageName() { return m_processorImpl->PackageName(); };
