# Security Notes for Azure SDK AI Bots

## Known Security Issues

### lodash.trimend ReDoS Vulnerability (GHSA-29mw-wpgm-hmr9)

**Status**: Known issue, awaiting upstream fix  
**Severity**: Moderate  
**Affected Package**: `azure-sdk-qa-bot`  
**Root Cause**: Transitive dependency from `@microsoft/teams-ai`

#### Dependency Chain
```
@microsoft/teams-ai@1.7.4
  └─ botbuilder-dialogs@4.23.3
      └─ @microsoft/recognizers-text-date-time@1.1.4
      └─ @microsoft/recognizers-text-number-with-unit@1.1.4
      └─ @microsoft/recognizers-text-suite@1.1.4
          └─ @microsoft/recognizers-text-number@1.1.4
              └─ lodash.trimend@4.5.1
```

#### Details
- **CVE**: GHSA-29mw-wpgm-hmr9
- **CWE**: CWE-400 (Uncontrolled Resource Consumption), CWE-1333 (Inefficient Regular Expression Complexity)
- **CVSS Score**: 5.3 (Moderate)
- **Description**: Regular Expression Denial of Service (ReDoS) in lodash.trimend

#### Mitigation Status
- Updated `@microsoft/teams-ai` to latest version (1.7.4)
- All intermediate packages are at their latest versions
- The vulnerability exists in all available versions of `lodash.trimend` (4.0.0 - 4.5.1)
- No fix is currently available as `lodash.trimend` is a deprecated package

#### Risk Assessment
The risk of exploitation in this context is **LOW** because:
1. The affected package is used for text recognition in bot dialog systems
2. Input is typically user chat messages which are already limited in length by Teams
3. The bot is authenticated and requires valid API keys
4. Input validation and sanitization have been added to prevent malicious inputs

#### Remediation Plan
1. Monitor Microsoft's repositories for updates to the recognizer packages
2. Consider replacing the functionality if Microsoft doesn't provide a fix
3. Monitor for new versions of `@microsoft/teams-ai` that may address this issue
4. Implement rate limiting and input length restrictions (already in place via Teams platform)

## Fixed Security Issues

### 1. Timing Attack Vulnerability in API Key Authentication
**Fixed**: ✅  
**File**: `azure-sdk-qa-bot-backend-shared/src/auth/key-auth.ts`  
**Fix**: Implemented `timingSafeEqual` from Node.js crypto module to prevent timing attacks on API key comparison

### 2. Server-Side Request Forgery (SSRF) via Image URLs
**Fixed**: ✅  
**File**: `azure-sdk-qa-bot-backend-shared/src/input/ImageContentExtractor.ts`  
**Fix**: Added URL validation to:
- Only allow HTTPS protocol
- Block private IP ranges (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
- Block localhost (127.0.0.1, ::1)
- Block link-local addresses (169.254.x.x)

### 3. URL Injection via Link Processing
**Fixed**: ✅  
**File**: `azure-sdk-qa-bot-backend-shared/src/routes/prompts.ts`  
**Fix**: Added proper URL validation and error handling to prevent malformed URLs from being processed

### 4. Vite Path Traversal Vulnerability (GHSA-93m4-6634-74q7)
**Fixed**: ✅  
**Affected Packages**: 
- `azure-sdk-qa-bot`
- `azure-sdk-qa-bot-backend-shared`
- `azure-sdk-qa-bot-knowledge-sync`  
**Fix**: Updated vite to latest version via `npm audit fix`

### 5. SheetJS (xlsx) Vulnerabilities
**Fixed**: ✅  
**Affected Package**: `azure-sdk-qa-bot-function`  
**Vulnerabilities**:
- GHSA-4r6h-8v6p-xvw6 (Prototype Pollution)
- GHSA-5pgg-2g8v-p4x9 (ReDoS)  
**Fix**: Replaced `xlsx@0.18.5` with `@e965/xlsx@0.20.3` (community-maintained fork with security fixes)

### 6. Zod DoS Vulnerability (GHSA-m95q-7qp3-xv42)
**Fixed**: ✅  
**Affected Package**: `azure-sdk-qa-bot-function`  
**Fix**: Updated dependencies via `npm audit fix` to get patched version of zod

## Security Best Practices Implemented

1. **Authentication**: All API endpoints require API key authentication via `X-API-Key` header
2. **Input Validation**: URLs and user inputs are validated before processing
3. **HTTPS Only**: All external requests are restricted to HTTPS protocol
4. **SSRF Prevention**: Private IP ranges and localhost are blocked from external requests
5. **Dependency Management**: Regular security audits and updates of dependencies
6. **Timing-Safe Comparisons**: Sensitive string comparisons use constant-time algorithms

## Maintenance

- Run `npm audit` regularly in all package directories
- Update dependencies monthly or when security advisories are published
- Monitor GitHub Security Advisories for the packages we depend on
- Test thoroughly after any security updates

## Reporting Security Issues

If you discover a security issue, please report it to the Azure Security team following the guidelines in [SECURITY.md](../../SECURITY.md).
