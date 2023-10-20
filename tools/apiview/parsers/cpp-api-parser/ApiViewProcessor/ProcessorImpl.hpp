// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "ApiViewMessage.hpp"
#include "ApiViewProcessor.hpp"
#include "AstNode.hpp"
#include <clang/AST/ASTConsumer.h>
#include <clang/AST/Comment.h>
#include <clang/AST/CommentVisitor.h>
#include <clang/AST/RecursiveASTVisitor.h>
#include <clang/Frontend/CompilerInstance.h>
#include <clang/Frontend/FrontendAction.h>
#include <clang/Frontend/FrontendActions.h>
#include <clang/Tooling/CommonOptionsParser.h>
#include <clang/Tooling/Tooling.h>
#include <filesystem>
#include <llvm/Support/CommandLine.h>

class ApiViewProcessorImpl {
  std::unique_ptr<AzureClassesDatabase> m_classDatabase;
  std::vector<std::filesystem::path> m_filesToCompile;
  std::vector<std::filesystem::path> m_filesToIgnore;
  std::vector<std::filesystem::path> m_additionalIncludeDirectories;
  std::vector<std::string> m_additionalCompilerArguments;
  std::filesystem::path m_currentSourceRoot;
  std::string m_reviewName;
  std::string m_serviceName;
  std::string m_packageName;
  std::string m_repositoryRoot;
  mutable std::string m_sourceRoot;

  bool m_allowInternal{false};
  bool m_includeDetail{false};
  bool m_includePrivate{false};
  std::vector<std::string> m_filterNamespaces;

  class CollectCppClassesVisitor : public clang::RecursiveASTVisitor<CollectCppClassesVisitor> {
    ApiViewProcessorImpl* m_processorImpl;

    bool ShouldCollectNamedDecl(clang::NamedDecl* declarator);

  public:
    explicit CollectCppClassesVisitor(ApiViewProcessorImpl* processorImpl)
        : clang::RecursiveASTVisitor<CollectCppClassesVisitor>(), m_processorImpl{processorImpl}
    {
    }

    bool VisitDecl(clang::Decl*) { return true; }
    // The RecursiveASTVisitor visits every node in the AST, so we can use this to collect all the
    // named nodes which should be collected.

    bool VisitNamedDecl(clang::NamedDecl* namedDecl)
    {
      if (ShouldCollectNamedDecl(namedDecl))
      {
        m_processorImpl->m_classDatabase->CreateAstNode(namedDecl);
      }
      return true;
    }
  };

  class ExtractCppClassConsumer : public clang::ASTConsumer {
  public:
    explicit ExtractCppClassConsumer(ApiViewProcessorImpl* processorImpl)
        : clang::ASTConsumer(), m_visitor(processorImpl)
    {
    }

    virtual void HandleTranslationUnit(clang::ASTContext& context) override
    {
      m_visitor.TraverseDecl(context.getTranslationUnitDecl());
    }

  private:
    CollectCppClassesVisitor m_visitor;
  };

  class AstVisitorActionFactory : public clang::tooling::FrontendActionFactory {

    ApiViewProcessorImpl* m_processorImpl;
    // Inherited via FrontendActionFactory
    virtual std::unique_ptr<clang::FrontendAction> create() override
    {
      return std::make_unique<ApiViewProcessorImpl::AstVisitorAction>(m_processorImpl);
    }

  public:
    AstVisitorActionFactory(ApiViewProcessorImpl* processorImpl)
        : FrontendActionFactory(), m_processorImpl{processorImpl}
    {
    }
  };

  class AstVisitorAction : public clang::ASTFrontendAction {
    ApiViewProcessorImpl* m_processorImpl;

  public:
    AstVisitorAction(ApiViewProcessorImpl* processorImpl);
    virtual std::unique_ptr<clang::ASTConsumer> CreateASTConsumer(
        clang::CompilerInstance& compiler,
        llvm::StringRef /* inFile*/) override;
  };

protected:
  std::vector<std::filesystem::path> const& GetFilesToCompile() { return m_filesToCompile; }
  std::filesystem::path const& CurrentSourceRoot() { return m_currentSourceRoot; }

public:
  ApiViewProcessorImpl(
      std::string_view directoryToProcess,
      std::string_view const& configurationFile);
  ApiViewProcessorImpl(std::string_view directoryToProcess, nlohmann::json const& settingsJson);

  int ProcessApiView();

  std::unique_ptr<AzureClassesDatabase> const& GetClassesDatabase() { return m_classDatabase; }

  bool AllowInternal() const { return m_allowInternal; }
  bool IncludeDetail() const { return m_includeDetail; }
  bool IncludePrivate() const { return m_includePrivate; }
  std::string_view const ReviewName() const { return m_reviewName; };
  std::string_view const ServiceName() const { return m_serviceName; };
  std::string_view const PackageName() const { return m_packageName; };
  std::string_view const SourceRepository() const { return m_repositoryRoot; };
  std::string_view const RootDirectory() const
  {
    if (m_sourceRoot.empty())
    {
      m_sourceRoot = m_currentSourceRoot.string();
    }
    return m_sourceRoot;
  }
  std::vector<std::string> const& FilterNamespaces() { return m_filterNamespaces; }
};
