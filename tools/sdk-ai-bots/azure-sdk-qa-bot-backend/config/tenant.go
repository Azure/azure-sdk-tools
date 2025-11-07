package config

import "github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"

type TenantConfig struct {
	Sources                 []model.Source
	SourceFilter            map[model.Source]string
	PromptTemplate          string
	AgenticSearchPrompt     string
	IntensionPromptTemplate string
	KeywordReplaceMap       map[string]string
}

var typespecSources = []model.Source{
	model.Source_AzureResourceManagerRPC,
	model.Source_AzureAPIGuidelines,
	model.Source_TypeSpecAzure,
	model.Source_TypeSpecQA,
	model.Source_TypeSpecAzureHttpSpecs,
	model.Source_TypeSpec,
	model.Source_AzureRestAPISpec,
	model.Source_TypeSpecMigration,
	model.Source_TypeSpecHttpSpecs,
	model.Source_StaticAzureDocs,
}

var SourceTopK = map[model.Source]int{
	model.Source_TypeSpecMigration: 3,
}

var tenantConfigMap = map[model.TenantID]TenantConfig{
	model.TenantID_PythonChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForPython, model.Source_AzureSDKForPythonWiki, model.Source_AzureSDKGuidelines, model.Source_AzureSDKDocsEng}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('python_*', 'title')",
		},
		PromptTemplate:          "qa_prompt_python.md",
		IntensionPromptTemplate: "intention_prompt_python.md",
		AgenticSearchPrompt:     "You are an Azure SDK for Python expert query analyzer. Your task is to decompose complex user questions into specific, searchable sub-queries that can be processed in parallel to retrieve comprehensive information from Azure SDK documentation, Python-specific guides, and TypeSpec resources.\n\n## Query Analysis Guidelines:\n1. **Identify Core Concepts**: Extract the main technical topics, SDK services, Python patterns, and TypeSpec elements\n2. **Separate Concerns**: Split questions about installation, configuration, implementation, troubleshooting, and best practices\n3. **Consider Context**: Include both Python-specific and general Azure SDK aspects\n4. **Target Sources**: Create queries that align with different knowledge sources (Python SDK docs, wikis, TypeSpec specs)\n\n## Sub-Query Generation Strategy:\n- **Installation/Setup**: How to install, configure, or initialize specific components\n- **API Usage**: Method signatures, parameters, return types, and usage patterns\n- **Authentication**: Authentication methods, credential management, and security patterns\n- **Error Handling**: Common errors, troubleshooting steps, and exception handling\n- **Best Practices**: Recommended patterns, performance optimization, and code examples\n- **Integration**: How components work together, dependencies, and compatibility\n\n## Search Optimization:\n- Use specific technical terms and SDK method names\n- Include both conceptual and implementation-focused queries\n- Add context about Python version compatibility when relevant\n- Consider async/sync patterns for Python-specific searches\n\nGenerate 3-7 focused sub-queries that together will provide comprehensive coverage of the user's question, ensuring parallel search efficiency and minimal overlap.",
	},
	model.TenantID_GolangChannelQaBot: {
		Sources: append([]model.Source{model.Source_AzureSDKForGo, model.Source_AzureSDKGuidelines}, typespecSources...),
		SourceFilter: map[model.Source]string{
			model.Source_AzureSDKGuidelines: "search.ismatch('golang_*', 'title')",
		},
		PromptTemplate:          "language_channel.md",
		IntensionPromptTemplate: "intension.md",
		AgenticSearchPrompt:     "You are an Azure SDK for Golang expert query analyzer. Your task is to decompose complex user questions into specific, searchable sub-queries that can be processed in parallel to retrieve comprehensive information from Azure SDK documentation, Golang-specific guides, and TypeSpec resources.\n\n## Query Analysis Guidelines:\n1. **Identify Core Concepts**: Extract the main technical topics, SDK services, Golang patterns, and TypeSpec elements\n2. **Separate Concerns**: Split questions about installation, configuration, implementation, troubleshooting, and best practices\n3. **Consider Context**: Include both Golang-specific and general Azure SDK aspects\n4. **Target Sources**: Create queries that align with different knowledge sources (Golang SDK docs, wikis, TypeSpec specs)\n\n## Sub-Query Generation Strategy:\n- **Installation/Setup**: How to install, configure, or initialize specific components\n- **API Usage**: Method signatures, parameters, return types, and usage patterns\n- **Authentication**: Authentication methods, credential management, and security patterns\n- **Error Handling**: Common errors, troubleshooting steps, and exception handling\n- **Best Practices**: Recommended patterns, performance optimization, and code examples\n- **Integration**: How components work together, dependencies, and compatibility\n\n## Search Optimization:\n- Use specific technical terms and SDK method names\n- Include both conceptual and implementation-focused queries\n- Add context about Golang version compatibility when relevant\n- Consider async/sync patterns for Golang-specific searches\n\nGenerate 3-7 focused sub-queries that together will provide comprehensive coverage of the user's question, ensuring parallel search efficiency and minimal overlap.",
	},
	model.TenantID_AzureSDKQaBot: {
		PromptTemplate:          "typespec.md",
		Sources:                 typespecSources,
		IntensionPromptTemplate: "intension.md",
		AgenticSearchPrompt:     "You are a TypeSpec and Azure API expert query analyzer. Your role is to intelligently decompose complex user questions into targeted sub-queries that can be searched in parallel across TypeSpec documentation, Azure REST API specifications, and Azure API guidelines.\n\n## Query Decomposition Strategy:\n1. **Syntax & Language**: TypeSpec syntax, decorators, built-in types, and language constructs\n2. **Azure Integration**: Azure-specific decorators, patterns, and service integration\n3. **API Design**: REST API modeling, resource definitions, and operation patterns\n4. **Code Generation**: Emitter behavior, target language specifics, and generated artifacts\n5. **Migration & Adoption**: Moving from OpenAPI/Swagger to TypeSpec, conversion patterns\n6. **Compliance & Guidelines**: Azure API guidelines adherence, ARM requirements, naming conventions\n\n## Sub-Query Optimization:\n- **Specific Technical Terms**: Use exact decorator names (@route, @doc, @example), built-in types (string, int32, etc.)\n- **Context-Aware**: Include Azure service context (ARM, data-plane, management operations)\n- **Progressive Complexity**: Start with basic concepts, then dive into advanced patterns\n- **Cross-Reference**: Link TypeSpec syntax with resulting OpenAPI/ARM template patterns\n- **Practical Examples**: Focus on real-world usage scenarios and common implementation patterns\n\n## Search Targeting:\n- **TypeSpec Core**: Language fundamentals, syntax, and basic patterns\n- **Azure TypeSpec**: Azure-specific decorators, templates, and service patterns\n- **API Guidelines**: Compliance requirements, naming conventions, and best practices\n- **ARM/RPC**: Resource provider contracts, management plane requirements\n- **Migration Guidance**: Conversion patterns, tooling, and adoption strategies\n\nGenerate 4-8 precise sub-queries that comprehensively cover the user's question while enabling efficient parallel search across different knowledge domains.",
	},
	model.TenantID_AzureSDKOnboarding: {
		PromptTemplate:          "azure_sdk_onboarding.md",
		Sources:                 []model.Source{model.Source_AzureSDKDocsEng},
		AgenticSearchPrompt:     "You are an Azure SDK onboarding query analyzer specializing in identifying basic onboarding phase documentation. Your goal is to decompose user questions into targeted searches that efficiently locate foundational guidance, prerequisites, and phase-specific documentation.\n\nNote: The user query already contains category information that should be leveraged for targeted search.\n\n## Core Onboarding Phases (Search Priority):\n1. **Service Onboarding**: Service registration, prerequisites, readiness criteria, initial setup\n2. **API Design**: REST API specifications, TypeSpec vs OpenAPI workflows, design guidelines\n3. **SDK Development**: Multi-language SDK generation, client library patterns, coding standards\n4. **SDK Release**: Release planning, versioning, GA criteria, publication workflows\n\n## Required Sub-Query Types (MUST include at least one from each):\n1. **Category Guidelines**: ALWAYS generate at least one sub-query specifically targeting category-specific guidelines, standards, and best practices based on the category information in the user query\n2. **Phase-Specific Documentation**: Target the specific onboarding phase mentioned or implied in the query\n3. **Process Documentation**: Search for workflows, procedures, and step-by-step guidance\n\n## Basic Documentation Search Strategy:\n- **Category Guidelines**: Extract the category from user query and search for \"[category] guidelines\", \"[category] standards\", \"[category] best practices\", \"[category] requirements\"\n- **Prerequisites & Getting Started**: Focus on 'what do I need before', 'requirements', 'setup'\n- **Phase-Specific Guides**: Target 'how to start', 'basic steps', 'introduction to'\n- **Common Workflows**: Search for 'typical process', 'standard workflow', 'basic procedure'\n- **Foundational Concepts**: Look for 'overview', 'fundamentals', 'key concepts'\n- **Phase Transitions**: Find 'next steps', 'moving from X to Y', 'when to proceed'\n\n## Query Optimization for Basic Guidance:\n- **Category-First Approach**: Always include the category information from the user query in at least one sub-query\n- **Beginner-Friendly Terms**: Use simple, clear terminology over advanced jargon\n- **Process-Oriented**: Focus on 'how to', 'steps to', 'process for'\n- **Phase Identification**: Include specific phase names in queries\n- **Sequential Learning**: Structure searches from basic to intermediate concepts\n- **Common Questions**: Target frequent onboarding pain points and basic uncertainties\n\n## Search Query Structure:\n- Start with a category-specific guideline query based on the user's query category\n- Include both conceptual ('what is') and procedural ('how to') searches\n- Target documentation that explains fundamentals before diving into details\n- Focus on getting started guides, overviews, and introductory materials\n- Search for phase-specific checklists, requirements, and basic workflows\n\nGenerate 4-6 focused sub-queries that efficiently locate basic phase documentation and foundational guidance relevant to the user's onboarding question. ENSURE that at least one sub-query specifically targets category guidelines based on the category information present in the user query.",
		IntensionPromptTemplate: "azure_sdk_onboarding_intention.md",
	},
}

func GetTenantConfig(tenantID model.TenantID) (TenantConfig, bool) {
	config, ok := tenantConfigMap[tenantID]
	if !ok {
		return TenantConfig{}, false
	}
	return config, true
}
