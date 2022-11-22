// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "ApiViewDiagnostic.hpp"
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

  bool m_includeInternal{false};
  bool m_includeDetail{false};
  bool m_includePrivate{false};
  std::string m_filterNamespace;

  class CollectCppClassesVisitor : public clang::RecursiveASTVisitor<CollectCppClassesVisitor> {
    ApiViewProcessorImpl* m_processorImpl;

    bool ShouldCollectNamedDecl(clang::NamedDecl* declarator);

  public:
    explicit CollectCppClassesVisitor(ApiViewProcessorImpl* processorImpl)
        : clang::RecursiveASTVisitor<CollectCppClassesVisitor>(), m_processorImpl{processorImpl}
    {
    }

    bool VisitDecl(clang::Decl*) { return true; }
    bool VisitNamedDecl(clang::NamedDecl* namedDecl)
    {
      if (ShouldCollectNamedDecl(namedDecl))
      {
        // Doesn't actually add an AST node to the database, but does flag if the named decl isn't
        // currently processed.
        m_processorImpl->m_classDatabase->CreateAstNode(namedDecl);
      }
      return true;
    }

    bool VisitFunctionDecl(clang::FunctionDecl* functionDecl)
    {
      // We're only interested in global functions, otherwise this sweeps up all function
      // declarations.
      if (ShouldCollectNamedDecl(functionDecl))
      {
        m_processorImpl->m_classDatabase->CreateAstNode(functionDecl);
      }
      return true;
    }
    bool VisitClassTemplateSpecializationDecl(clang::ClassTemplateSpecializationDecl* templateDecl)
    {
      if (ShouldCollectNamedDecl(templateDecl))
      {
        m_processorImpl->m_classDatabase->CreateAstNode(templateDecl);
      }
      return true;
    }
    bool VisitTemplateDecl(clang::TemplateDecl* templateDecl)
    {
      // We're only interested in global functions, otherwise this sweeps up all function
      // declarations.
      if (ShouldCollectNamedDecl(templateDecl))
      {
          m_processorImpl->m_classDatabase->CreateAstNode(templateDecl);
      }
      return true;
    }
    bool VisitTypeAliasDecl(clang::TypeAliasDecl* alias)
    {
      if (ShouldCollectNamedDecl(alias))
      {
        if (!alias->isCXXClassMember() && alias->getParentFunctionOrMethod() == nullptr)
        {
          m_processorImpl->m_classDatabase->CreateAstNode(alias);
        }
      }
      return true;
    }

    bool VisitVarDecl(clang::VarDecl* var)
    {
      if (ShouldCollectNamedDecl(var))
      {
        if (!var->isCXXClassMember() && var->getParentFunctionOrMethod() == nullptr)
        {
          m_processorImpl->m_classDatabase->CreateAstNode(var);
        }
      }
      return true;
    }
    bool VisitEnumDecl(clang::EnumDecl* enumDecl)
    {
      if (ShouldCollectNamedDecl(enumDecl))
      {
        if (!enumDecl->isCXXClassMember())
        {
          m_processorImpl->m_classDatabase->CreateAstNode(enumDecl);
        }
      }
      return true;
    }

    bool VisitCXXRecordDecl(clang::CXXRecordDecl* cxxDecl)
    {
      if (ShouldCollectNamedDecl(cxxDecl))
      {
        m_processorImpl->m_classDatabase->CreateAstNode(cxxDecl);
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

public:
  ApiViewProcessorImpl(ApiViewProcessorOptions const& options);
  ApiViewProcessorImpl(
      std::string_view directoryToProcess,
      std::string_view const& configurationFile);
  ApiViewProcessorImpl(std::string_view directoryToProcess, nlohmann::json const& settingsJson);

  int ProcessApiView();

  int ProcessApiView(
      std::string_view const& sourceLocation,
      std::vector<std::string> const& additionalCompilerArguments,
      std::vector<std::string_view> const& filesToProcess);

  std::unique_ptr<AzureClassesDatabase> const& GetClassesDatabase() { return m_classDatabase; }

  bool IncludeInternal() { return m_includeInternal; }
  bool IncludeDetail() { return m_includeDetail; }
  bool IncludePrivate() { return m_includePrivate; }
  std::string_view const ReviewName() { return m_reviewName; };
  std::string_view const ServiceName() { return m_serviceName; };
  std::string_view const PackageName() { return m_packageName; };
  std::string_view const FilterNamespace() { return m_filterNamespace; }
};
