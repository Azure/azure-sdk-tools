<!-- BEGIN MICROSOFT SECURITY.MD V0.0.5 BLOCK -->

## Security

Microsoft takes the security of our software products and services seriously, which includes all source code repositories managed through [our GitHub organizations](https://opensource.microsoft.com/), which include [Microsoft](https://github.com/Microsoft), [Azure](https://github.com/Azure), [DotNet](https://github.com/dotnet), and [AspNet](https://github.com/aspnet).  

If you believe you have found a security vulnerability in any Microsoft-owned repository that meets [Microsoft's definition of a security vulnerability](https://learn.microsoft.com/previous-versions/tn-archive/cc751383(v=technet.10)), please report it to us as described in [Reporting Security Issues](#reporting-security-issues).

## Policy

Microsoft follows the principle of [Coordinated Vulnerability Disclosure](https://www.microsoft.com/msrc/cvd).

## Reporting Security Issues

> [!IMPORTANT]
> **Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them to the Microsoft Security Response Center (MSRC) at [https://msrc.microsoft.com/report/vulnerability/new](https://msrc.microsoft.com/report/vulnerability/new).

If you are unable to sign in, follow the alternative submission instructions on the [MSRC reporting page](https://msrc.microsoft.com/report/vulnerability/new).

You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Additional information can be found at [microsoft.com/msrc](https://www.microsoft.com/msrc).

### Evidence Required for a Report

Please include the following information to help us understand the nature and scope of the possible issue. This information will help us triage your report more quickly.

* Type of issue (e.g. buffer overflow, SQL injection, cross-site scripting, etc.)

* The affected client or tool and full paths of source files related to the issue

* For a tool report, the specific Azure SDK environment in which the tool runs and evidence that the issue occurs in that environment

* The exact revision of the affected source code, with a direct URL if available. Eligible revisions are the current HEAD of `main` or an active feature branch.

* The attacker's access and the trust boundary crossed, including how input controlled by an untrusted party reaches the affected operation in its intended environment

* Any special configuration required to reproduce the issue, including diagnostic settings or caller customizations

* Step-by-step instructions and a reproducible demonstration of the issue

* Proof-of-concept or exploit code (if possible)

* Evidence of impact, including how an attacker might exploit the issue

Verify claims against the current code and authoritative service documentation, and clearly highlight what you could not verify. Reports must reproduce on an eligible revision, as findings limited to other revisions are not vulnerabilities or defense-in-depth enhancements. A hypothetical deployment or a caller deliberately supplying unsafe configuration is not sufficient evidence of a vulnerability.

### Preferred Languages

We prefer all communications to be in English.

## Trust Boundaries

The tools in this repository operate within Azure SDK development environments and rely on the trust boundaries of those environments. Security reports must distinguish a failure in a tool's own responsibilities from a failure of the host, pipeline sandbox, trusted network, or access controls on which it relies.

### Intended Use and Supported Environments

* The tools in this repository are intended for Azure SDK developers and Azure service partners working on Azure SDK libraries. They are not stand-alone products and are not intended for use outside the Azure SDK development context.

* Development scripts and command-line tools run in an Azure SDK developer's local environment, Azure SDK CI and release pipelines, or both. Hosted development tools such as [APIView](#apiview) operate within their own trusted environments. The [Development Tools and Automation](#development-tools-and-automation) section describes the shared boundaries and tool-specific requirements.

* All hosted UIs and services in this repository, including Release Planner and APIView, are internal-only and accessible only by Microsoft employees through the trusted Microsoft network. None are publicly accessible. Individual services may require additional authentication and authorization within that network boundary.

* We make no guarantees about security hardening for general use. Anyone adopting a tool outside the Azure SDK context is responsible for security due diligence and accepts the risks of that use. Different trust boundaries may expose the tool to exploits that its intended Azure SDK environment prevents.

### Scope of Reports

* An in-scope vulnerability report must demonstrate that an untrusted party can violate a security responsibility owned by an Azure SDK tool or its development infrastructure in the intended Azure SDK environment. The [Development Tools and Automation](#development-tools-and-automation) section describes the shared boundaries and the responsibilities specific to individual tools.

* Reports about tools used outside their intended Azure SDK environments are out of scope. A valid tool report must demonstrate a security issue in the specific Azure SDK environment in which the tool runs, subject to its [intended use and supported environments](#intended-use-and-supported-environments). Findings based only on a different deployment or hypothetical exposure do not establish a vulnerability in the Azure SDK tool.

* Reports about hosted UIs and services must account for their internal-only, Microsoft-employee access boundary. Assuming public access or first compromising that boundary does not establish a vulnerability in the hosted tool. An exploit of the network or employee-access boundary must be reported against that boundary rather than the tool.

* Do not report vulnerabilities or defense-in-depth enhancements that require first compromising the host environment, the network, or an Azure service. These preconditions already cross the relevant trust boundary.

* Do not report findings that require the authentication or authorization privileges of maintainers or trusted partners. Additional restrictions on actions available only to these trusted parties are, at most, low-risk and low-value defense-in-depth enhancements rather than vulnerabilities.

### Development Tools and Automation

* Repository scripts, tools, actions, test runners, and pipeline configuration are intended for SDK development and automation. Development scripts are intentionally permissive to preserve flexibility and reduce friction during development. More restrictive input handling may offer defense-in-depth, but these enhancements are generally low-risk and low-value.

* Tools such as the [Azure SDK Test Proxy](#test-proxy) and test frameworks are used locally by SDK developers and in GitHub Actions and Azure DevOps pipelines. They are not public-facing services or components intended for customer production applications.

* Test fixtures intentionally simulate conditions that would be unsafe in production. For example, HTTP Fault Injector can truncate, stall, or terminate responses, and Key Vault Mock Attestation produces synthetic attestations for key-release tests. These are intended testing capabilities, not production security guarantees. Demonstrating the configured test behavior alone does not establish a vulnerability.

* The shared localhost development certificate is a deliberately public testing asset, not a production identity. Reports must distinguish such fixtures from live credentials, rather than treating their presence in the repository as a credential disclosure. A genuine credential disclosure is not excluded merely because it occurs in test code or test artifacts.

* Azure services are required to maintain their specifications in the [Azure REST API specifications repository](https://github.com/Azure/azure-rest-api-specs). These specifications are the authoritative source for SDK generation and are implicitly trusted. Specifications obtained from other sources are non-authoritative, and callers are responsible for the risks of using them.

* Emitters, generator plugins, and other executable extensions run as code in the development environment. Callers are responsible for selecting trusted extensions. Loading an extension does not isolate its behavior, and any required isolation belongs to the host environment or sandbox rather than the SDK tool.

* Azure SDK pipeline and test runs in the ephemeral Azure DevOps sandbox have no access to secrets, Microsoft resources, or the company network. The sandbox also constrains resource use and limits execution time. The sandbox is the primary trust boundary.

* Findings confined to that sandbox are generally defense-in-depth enhancements rather than vulnerabilities. An exploit that depends on a failure of sandbox isolation is a flaw in the sandbox, not the SDK tooling running within it. Reports for such failures must target the sandbox rather than the SDK tool used to demonstrate them.

* Stress-test environments are runnable only by trusted maintainers and are themselves the sandbox for hosted stress runners and scenarios. Access to test credentials and resources within those environments is intentional and is not evidence of an isolation failure. The secret-free guarantees of the ephemeral CI sandbox do not apply to stress-test environments. Failures of stress-environment isolation must be reported against that boundary, not the hosted runner or scenario.

For example, a developer choosing a command for a local script to execute does not demonstrate an untrusted party crossing a boundary. When that script runs in a pipeline, the sandbox provides isolation rather than restrictions on the commands the script accepts.

Similarly, a developer passing a relative path such as `..\..\output` that resolves above the execution directory is not demonstrating a vulnerability. This applies whether the caller supplies the path directly or the tool reads it from an input file specified by the caller. The script or tool is honoring the caller's instructions, and the caller is responsible for validating paths from either source to prevent unintended access.

#### Test Proxy

* Test Proxy provides record and playback capabilities for trusted SDK tests, not a production network gateway. Tests intentionally select upstream destinations and control recording options, including redirect behavior. Forwarding a request to a test-selected destination or following a redirect does not, by itself, demonstrate a security vulnerability.

* Test authors are responsible for configuring service-specific sanitization and reviewing recordings for sensitive information before publishing them. Test Proxy remains responsible for correctly applying its default and configured sanitizers. Reports must distinguish a failure of those sanitizers from missing test-specific configuration or a caller deliberately disabling sanitization.

#### Azure SDK CLI and MCP Server

* The Azure SDK CLI and its MCP server expose the same development operations. The MCP server is intended for local execution within the developer or pipeline environment, not as a public or multi-tenant service. It uses the credentials and permissions available in that environment.

* Invoking an operation through an AI agent does not create a separate identity or isolation boundary. The user and host control which tools are available, the credentials and execution permissions granted to them, and any approval requirements. An authorized invocation of an intended capability is not, by itself, privilege escalation.

* Reports involving AI-generated tool calls must demonstrate a violation of a security responsibility in the intended environment. Influencing a model's response alone is not sufficient, but unauthorized actions or disclosure of protected information are not excluded merely because an AI agent is involved.

#### Check Enforcer

Check Enforcer is an informational and awareness monitor for the Azure SDK development workflow, not an enforcement mechanism. Its slash commands intentionally do not apply authorization checks. This behavior does not bypass the approval and merge boundary. Changes must still receive explicit approval, and a trusted maintainer must perform the merge. Reports that identify only the lack of authorization on these slash commands do not demonstrate a security vulnerability.

#### APIView

* APIView is an internal-facing development tool. Access requires the trusted Microsoft network, membership in either the `Microsoft` or `Azure` GitHub organization, and authorization. These are its key trust boundaries.

* APIView is not security hardened for general use. The risks of operating it within these boundaries have been reviewed and accepted. This risk acceptance does not extend to deployments outside the Azure SDK environment.

* Reports must demonstrate an issue in APIView's intended Azure SDK environment and must not assume access to the application without trusted-network access, the required GitHub organization membership, and authorization. An exploit of the trusted network or GitHub organization access controls must be reported against that boundary, not as an APIView vulnerability.

### Use of Azure SDK Clients

Tools may use Azure SDK clients to communicate with Azure services. These clients transform requests and responses without replicating service security checks or business rules. Services own authentication, authorization, and payload validation. Tools acting as callers remain responsible for client configuration and any validation needed to use data safely in their environment. The [Azure SDK client trust boundaries](https://github.com/Azure/azure-sdk/blob/main/SECURITY.md#trust-boundaries) describe the respective responsibilities of clients, services, and callers in detail.

## Automated Security Scanning

AI-based scanners and other automated security tools must apply the [trust boundaries](#trust-boundaries) above before classifying or reporting a finding. Automated findings must meet the same [evidence requirements](#evidence-required-for-a-report) as other security reports.

Reporters are responsible for reviewing and validating generated findings before submitting them. A suspicious code pattern or an unverified model response is not evidence of a vulnerability.

* Run the proposed reproduction and confirm that its observed result supports the claimed impact.

* Check the generated analysis against the affected code and the intended environment. Confirm the attacker's access and the path across the identified trust boundary.

* Remove unsupported claims and state any remaining uncertainty. Do not present assumptions or model-generated conclusions as verified facts.

* Submit a concise report using the shared [evidence requirements](#evidence-required-for-a-report), rather than forwarding unreviewed scanner output.

<!-- END MICROSOFT SECURITY.MD BLOCK -->
