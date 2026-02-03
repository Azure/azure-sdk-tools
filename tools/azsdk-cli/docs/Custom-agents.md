# Custom Agents

## Definition

A Custom Agent is a specialized, autonomous software agent configured to handle specific domains or workflows within the Azure SDK ecosystem. Custom agents are pre-configured with domain expertise, appropriate tools, and behavioral patterns that enable them to work independently on complex tasks.

Custom agents differ from generic AI agents in that they:
- Have focused expertise in specific domains (e.g., TypeSpec authoring, package validation, documentation generation)
- Come pre-equipped with relevant tools and access permissions
- Follow predefined behavioral patterns and constraints
- Can make autonomous decisions within their domain
- Integrate seamlessly with the broader agent ecosystem

## When to Use

### Custom Agents vs. Skills vs. Copilot Instructions

Understanding when to use each mechanism is key to building effective automation:

#### Use Custom Agents When:

- **Complex domain expertise is required**: Tasks that benefit from specialized knowledge and reasoning patterns (e.g., reviewing TypeSpec definitions for best practices, analyzing test failures for root causes)
- **Autonomous decision-making is needed**: Scenarios where the agent must evaluate options and choose the best approach without constant guidance
- **Long-running or iterative workflows**: Tasks that involve multiple steps with feedback loops and adjustments (e.g., refactoring code, optimizing performance)
- **Domain-specific context is important**: Operations that require understanding of specialized concepts and relationships within a particular domain
- **Tool orchestration is complex**: Workflows that require sophisticated coordination of multiple tools and commands

#### Use Skills When:

- **Repeatable procedures with clear steps**: Well-defined processes that follow a consistent pattern
- **Modular automation is desired**: Tasks that should be composable and reusable across different contexts
- **Documentation of standard processes**: Capturing institutional knowledge about how to perform specific operations
- **Consistency is paramount**: Ensuring tasks are always executed the same way

#### Use Copilot Instructions When:

- **Repository-wide conventions apply**: General principles that affect all interactions (e.g., output formatting, confirmation policies)
- **Cross-cutting concerns**: Behaviors that should be consistent across all agents and Skills
- **Minimal, focused guidance**: High-level preferences that don't add significant token overhead

### Relationship Between Custom Agents, Skills, and Copilot Instructions

Custom agents can and should leverage both Skills and Copilot instructions:

- **Custom agents use Skills**: A custom agent working on package validation would reference and execute relevant validation Skills
- **Custom agents follow Copilot instructions**: All agents, including custom agents, adhere to the repository-wide conventions defined in Copilot instructions
- **Custom agents add value through reasoning**: While Skills provide the "what" and "how", custom agents provide the "when" and "why" through intelligent decision-making

### Examples of Custom Agent Use Cases

- **TypeSpec Review Agent**: Reviews TypeSpec definitions for compliance with Azure API standards, suggests improvements, and validates against best practices
- **Test Failure Analysis Agent**: Investigates test failures, correlates errors with code changes, and suggests fixes based on error patterns
- **Documentation Generation Agent**: Creates comprehensive documentation by analyzing code structure, understanding intent, and following documentation templates
- **Release Coordination Agent**: Manages the complex workflow of coordinating releases across multiple packages and languages
- **Migration Assistant Agent**: Helps migrate code from deprecated APIs to new patterns, understanding context and making appropriate transformations

## Repository to Store Them

### Storage Location

Custom agents should be stored in the `.github/agents/` directory of the repository where they will be used. This location is recognized by GitHub's agent infrastructure and allows for proper discovery and execution.

For agents used across multiple Azure SDK repositories, consider:

1. **Primary definition in azure-sdk-tools**: Store the canonical agent definition in `azure-sdk-tools/.github/agents/`
2. **Distribution via eng/common**: Use the engineering common sync framework to distribute agents to individual SDK repositories
3. **Repository-specific customization**: Allow individual repositories to override or extend agent behavior as needed

### Agent Configuration Files

Each custom agent should have:

- **Agent definition file**: Describes the agent's purpose, capabilities, tools, and behavioral constraints
- **Tool configuration**: Lists the MCP tools and CLI commands the agent can access
- **Prompt templates**: Defines how the agent should be invoked and what context it needs
- **Test scenarios**: Documents how to test and validate agent behavior

## How to Test

### Testing Strategies for Custom Agents

Custom agents require different testing approaches than Skills because they involve autonomous reasoning and decision-making.

#### 1. Unit Testing

Test individual agent capabilities in isolation:

- **Tool selection**: Verify the agent chooses appropriate tools for given tasks
- **Parameter validation**: Ensure the agent correctly formats tool parameters
- **Error handling**: Test how the agent responds to tool failures and unexpected outputs
- **Constraint adherence**: Validate that the agent respects its defined boundaries

#### 2. Integration Testing

Test the agent's interaction with the broader ecosystem:

- **Skill execution**: Verify the agent correctly invokes and interprets Skill results
- **Tool orchestration**: Test the agent's ability to coordinate multiple tools effectively
- **Context management**: Ensure the agent maintains appropriate context across operations
- **Copilot instruction compliance**: Validate the agent follows repository-wide conventions

#### 3. Scenario-Based Testing

Test the agent in realistic end-to-end scenarios:

- **Success paths**: Verify the agent completes standard workflows correctly
- **Error scenarios**: Test agent behavior when encountering errors, missing dependencies, or invalid inputs
- **Edge cases**: Validate handling of unusual but valid scenarios
- **Performance**: Measure agent response time and resource usage

#### 4. Evaluation Framework

Leverage the existing evaluation framework where possible:

- **Prompt-response testing**: Evaluate agent responses to standard prompts
- **Output validation**: Check that agent outputs match expected formats and content
- **Success criteria checking**: Verify the agent correctly determines task completion
- **Comparison testing**: Compare custom agent performance against baseline approaches

### Manual Testing and Validation

Before deploying a custom agent:

1. **Interactive testing**: Engage with the agent through natural language prompts to test understanding and reasoning
2. **Edge case exploration**: Deliberately provide ambiguous or challenging inputs
3. **Failure injection**: Simulate tool failures and external errors to test resilience
4. **User acceptance testing**: Have domain experts evaluate agent output quality

### Continuous Improvement

Custom agents should be monitored and improved over time:

- **Collect feedback**: Gather user and developer feedback on agent performance
- **Analyze failures**: Review cases where the agent failed or produced suboptimal results
- **Iterate on prompts**: Refine agent instructions based on observed behavior
- **Update capabilities**: Add new tools and Skills as the ecosystem evolves

## Best Practices

### Agent Design Principles

When creating custom agents:

1. **Single responsibility**: Each agent should have a clear, focused purpose
2. **Explicit constraints**: Define what the agent can and cannot do
3. **Graceful degradation**: Agent should handle tool failures and missing information elegantly
4. **Transparent operation**: Agent should explain its reasoning and actions
5. **Respect user intent**: Agent should seek clarification rather than make assumptions

### Documentation Requirements

Each custom agent must have comprehensive documentation including:

- Purpose and scope
- Capabilities and limitations
- Required permissions and tools
- Usage examples and scenarios
- Testing and validation procedures
- Maintenance and update procedures

### Security Considerations

Custom agents with elevated permissions require:

- Clear documentation of security boundaries
- Validation of all external inputs
- Audit logging of agent actions
- Regular security reviews
- Incident response procedures
