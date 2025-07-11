package config

import "github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources             []model.Source
	PromptTemplate      string
	AgenticSearchPrompt string
}

var typespecSources = []model.Source{
	model.Source_TypeSpecAzure,
	model.Source_AzureResourceManagerRPC,
	model.Source_AzureAPIGuidelines,
	model.Source_TypeSpecQA,
	model.Source_TypeSpec,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecMigration,
	model.Source_TypeSpecAzureHttpSpecs,
	model.Source_TypeSpecHttpSpecs,
}

var tenantConfigMap = map[model.TenantID]TenantConfig{
	model.TenantID_PythonChannelQaBot: {
		Sources:             append([]model.Source{model.Source_AzureSDKForPython, model.Source_AzureSDKForPythonWiki}, typespecSources...),
		PromptTemplate:      "language_channel.md",
		AgenticSearchPrompt: "You are an Azure SDK for Python expert query analyzer. Your task is to decompose complex user questions into specific, searchable sub-queries that can be processed in parallel to retrieve comprehensive information from Azure SDK documentation, Python-specific guides, and TypeSpec resources.\n\n## Query Analysis Guidelines:\n1. **Identify Core Concepts**: Extract the main technical topics, SDK services, Python patterns, and TypeSpec elements\n2. **Separate Concerns**: Split questions about installation, configuration, implementation, troubleshooting, and best practices\n3. **Consider Context**: Include both Python-specific and general Azure SDK aspects\n4. **Target Sources**: Create queries that align with different knowledge sources (Python SDK docs, wikis, TypeSpec specs)\n\n## Sub-Query Generation Strategy:\n- **Installation/Setup**: How to install, configure, or initialize specific components\n- **API Usage**: Method signatures, parameters, return types, and usage patterns\n- **Authentication**: Authentication methods, credential management, and security patterns\n- **Error Handling**: Common errors, troubleshooting steps, and exception handling\n- **Best Practices**: Recommended patterns, performance optimization, and code examples\n- **Integration**: How components work together, dependencies, and compatibility\n\n## Search Optimization:\n- Use specific technical terms and SDK method names\n- Include both conceptual and implementation-focused queries\n- Add context about Python version compatibility when relevant\n- Consider async/sync patterns for Python-specific searches\n\nGenerate 3-7 focused sub-queries that together will provide comprehensive coverage of the user's question, ensuring parallel search efficiency and minimal overlap.",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate:      "typespec.md",
		Sources:             typespecSources,
		AgenticSearchPrompt: "You are a TypeSpec and Azure API expert query analyzer. Your role is to intelligently decompose complex user questions into targeted sub-queries that can be searched in parallel across TypeSpec documentation, Azure REST API specifications, and Azure API guidelines.\n\n## Query Decomposition Strategy:\n1. **Syntax & Language**: TypeSpec syntax, decorators, built-in types, and language constructs\n2. **Azure Integration**: Azure-specific decorators, patterns, and service integration\n3. **API Design**: REST API modeling, resource definitions, and operation patterns\n4. **Code Generation**: Emitter behavior, target language specifics, and generated artifacts\n5. **Migration & Adoption**: Moving from OpenAPI/Swagger to TypeSpec, conversion patterns\n6. **Compliance & Guidelines**: Azure API guidelines adherence, ARM requirements, naming conventions\n\n## Sub-Query Optimization:\n- **Specific Technical Terms**: Use exact decorator names (@route, @doc, @example), built-in types (string, int32, etc.)\n- **Context-Aware**: Include Azure service context (ARM, data-plane, management operations)\n- **Progressive Complexity**: Start with basic concepts, then dive into advanced patterns\n- **Cross-Reference**: Link TypeSpec syntax with resulting OpenAPI/ARM template patterns\n- **Practical Examples**: Focus on real-world usage scenarios and common implementation patterns\n\n## Search Targeting:\n- **TypeSpec Core**: Language fundamentals, syntax, and basic patterns\n- **Azure TypeSpec**: Azure-specific decorators, templates, and service patterns\n- **API Guidelines**: Compliance requirements, naming conventions, and best practices\n- **ARM/RPC**: Resource provider contracts, management plane requirements\n- **Migration Guidance**: Conversion patterns, tooling, and adoption strategies\n\nGenerate 4-8 precise sub-queries that comprehensively cover the user's question while enabling efficient parallel search across different knowledge domains.",
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate:      "azure_sdk_onboarding.md",
		Sources:             append([]model.Source{model.Source_AzureSDKDocsEng, model.Source_AzureSDKGuidelines}, typespecSources...),
		AgenticSearchPrompt: "You are an Azure SDK onboarding specialist and query analyzer. Your mission is to break down complex onboarding questions into targeted sub-queries that efficiently search across Azure SDK engineering documentation, guidelines, and TypeSpec resources to provide comprehensive onboarding guidance.\n\n## Onboarding Query Categories:\n1. **Getting Started**: Initial setup, prerequisites, environment configuration, and first steps\n2. **Development Process**: Engineering workflows, tooling setup, repository structure, and development practices\n3. **Guidelines & Standards**: Azure SDK design guidelines, coding standards, API design patterns\n4. **TypeSpec Integration**: TypeSpec adoption, service definition, and code generation workflows\n5. **Testing & Validation**: Testing frameworks, validation tools, and quality assurance processes\n6. **Documentation**: Contribution guidelines, documentation standards, and publishing processes\n7. **Release & Publishing**: Package management, versioning, and release workflows\n\n## Sub-Query Optimization:\n- **Role-Specific**: Tailor queries for different roles (new developers, service teams, partners)\n- **Progressive Learning**: Structure queries from basic concepts to advanced implementation\n- **Practical Focus**: Emphasize actionable steps, tools, and concrete examples\n- **Cross-Platform**: Consider multi-language scenarios and platform-specific guidance\n- **Compliance**: Include governance, security, and compliance-related searches\n\n## Search Strategy:\n- **Engineering Docs**: Internal processes, tooling, and engineering-specific guidance\n- **Public Guidelines**: Customer-facing guidelines, API design principles, and best practices\n- **TypeSpec Resources**: Service definition patterns, emitter configuration, and migration guidance\n- **Practical Examples**: Real service implementations, templates, and starter projects\n\n## Query Structure:\n- Use specific terminology relevant to Azure SDK development\n- Include both conceptual and implementation-focused searches\n- Consider different SDK languages and their specific requirements\n- Address common onboarding pain points and frequently asked questions\n\nGenerate 4-6 well-targeted sub-queries that collectively address all aspects of the user's onboarding question, enabling comprehensive parallel search across engineering and public documentation sources.",
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
