# APIView — Legacy Razor Pages System

This document describes the legacy server-rendered Razor Pages frontend in APIView: what it is, which languages still use it, how it coexists with the modern Angular SPA, and what the eventual migration path looks like.

For the current architecture, see [overview.md](overview.md).

> **Development policy:** We are not investing new features into the flat-token parser or Razor frontend. When implementing new features, scope the work to the Angular SPA and tree-token model only unless explicitly asked to support the legacy system.

---

## 1. Background

APIView originally shipped as a traditional ASP.NET Core Razor Pages application. All HTML was rendered server-side, with jQuery + Bootstrap for interactivity. Over time the frontend was rebuilt as an Angular SPA (`ClientSPA/`), but the Razor infrastructure remains in the codebase for two reasons:

1. **Some language parsers still emit the legacy flat-token format** (`CodeFileToken[]` / `ParserStyle.Flat`) — specifically C, C++, JSON, Swagger/OpenAPI, Protocol Buffers, and XML — and the Angular SPA only renders the modern tree-token format (`ReviewLine[]` / `ParserStyle.Tree`).
2. **Several non-review pages** (login, error, unauthorized) are still Razor-rendered.

---

## 2. Token Format: Flat vs. Tree

The routing decision between Razor and SPA is driven by the **`ParserStyle`** enum stored on every `APICodeFileModel`:

```csharp
public enum ParserStyle {
    Flat = 0,   // Legacy: CodeFileToken[] array
    Tree        // Modern: ReviewLine[] hierarchy
}
```

When a user navigates to `/Assemblies/{id}` (the Razor review route), the backend checks the active revision's `ParserStyle`:

- **`Tree`** → Immediately redirects to the Angular SPA at `/review/{id}/{revisionId}`. No Razor rendering occurs.
- **`Flat`** → Falls through to the `LegacyReview.cshtml` page, which shows a message that the review can no longer be fully rendered, and displays any existing comments.

This logic lives in [PageModelHelpers.cs](../APIViewWeb/Helpers/PageModelHelpers.cs) (~line 295):

```csharp
if (activeRevision.Files[0].ParserStyle == ParserStyle.Tree)
{
    reviewPageContent.Directive = ReviewContentModelDirective.RedirectToSPAUI;
    // ...
    return reviewPageContent;
}
```

---

## 3. Which Languages Use Which Format

The `UsesTreeStyleParser` property on each language service controls which format the parser emits. The default is `true` (tree/modern). Only a few languages explicitly override it to `false`.

### a. Modern (Tree) — Rendered in Angular SPA

These languages set `UsesTreeStyleParser = true` (or inherit the default) and produce `ReviewLine[]` output:

| Language | Service Class | Notes |
|---|---|---|
| C# | `CSharpLanguageService` | External .NET parser |
| Java | `JavaLanguageService` | External JAR processor |
| Python | `PythonLanguageService` | External Python script |
| JavaScript / TypeScript | `JavaScriptLanguageService` | External Node.js processor |
| Go | `GoLanguageService` | External Go binary (`apiviewgo`) |
| Rust | `RustLanguageService` | External Node.js processor |
| Swift | `SwiftLanguageService` | Deserializes pre-parsed JSON from external SwiftAPIView parser |
| TypeSpec | `TypeSpecLanguageService` | Sandboxed via DevOps pipeline; parsed by external `typespec-apiview` emitter |

### b. Legacy (Flat) — Falls to LegacyReview.cshtml

These languages explicitly set `UsesTreeStyleParser = false` and produce `CodeFileToken[]` output:

| Language | Service Class | File | Notes |
|---|---|---|---|
| C | `CLanguageService` | [CLanguageService.cs](../APIViewWeb/Languages/CLanguageService.cs#L76) | Internal JSON transformer |
| C++ | `CppLanguageService` | [CppLanguageService.cs](../APIViewWeb/Languages/CppLanguageService.cs#L120) | Internal JSON transformer |
| JSON (generic) | `JsonLanguageService` | [JsonLanguageService.cs](../APIViewWeb/Languages/JsonLanguageService.cs#L16) | Direct deserialization |
| Swagger / OpenAPI | `SwaggerLanguageService` | [SwaggerLanguageService.cs](../APIViewWeb/Languages/SwaggerLanguageService.cs#L24) | Pipeline-based |
| Protocol Buffers | `ProtocolLanguageService` | [ProtocolLanguageService.cs](../APIViewWeb/Languages/ProtocolLanguageService.cs#L18) | External processor |
| XML | `XmlLanguageService` | [XmlLanguageService.cs](../APIViewWeb/Languages/XmlLanguageService.cs#L16) | Direct XML/JSON deserialization |

> **C and C++ use `UsesTreeStyleParser = false` even though they internally construct `ReviewLine[]` structures.** They override the flag because their rendering pipeline still depends on legacy code paths. These are candidates for migration to `Tree` once the internal transformer is updated.

---

## 4. Razor Pages Inventory

All Razor Pages live under [APIViewWeb/Pages/](../APIViewWeb/Pages/).

### a. Review-Related Pages (Assemblies/)

| Page | Class | Current Role |
|---|---|---|
| `Review.cshtml` | `ReviewPageModel` | **Router**: checks `ParserStyle`, redirects to SPA for tree-style, falls through to legacy for flat-style |
| `LegacyReview.cshtml` | `LegacyReview` | **Degraded fallback**: shows "This review can not be viewed anymore" + existing comments |
| `Index.cshtml` | `IndexPageModel` | Review listing with search/filter/pagination (50 per page) |
| `Conversation.cshtml` | `ConversationModel` | Comment thread discussion view |
| `Revisions.cshtml` | `RevisionsPageModel` | Revision management |
| `Samples.cshtml` | `SamplesPageModel` | Code sample attachments |
| `Delete.cshtml` | `DeletePageModel` | Review deletion confirmation |
| `Profile.cshtml` | `ProfilePageModel` | User profile / preferences |
| `RequestedReviews.cshtml` | `RequestedReviewsPageModel` | Reviews assigned to the current user |

### b. Infrastructure Pages

| Page | Purpose |
|---|---|
| `Login.cshtml` | GitHub OAuth login entry |
| `Unauthorized.cshtml` | 403 error page |
| `Error.cshtml` | Generic error page |

### c. Shared Partials (Pages/Shared/)

These partials provide the layout and reusable fragments for the Razor pages:

| Partial | Purpose |
|---|---|
| `_Layout.cshtml` | Master layout: Bootstrap navbar, header/footer, theme support, script/CSS bundles |
| `Navigation.cshtml` | Recursive navigation tree (namespaces → types → members) |
| `_CodeLine.cshtml` | Legacy flat-token line renderer (syntax highlighting via `CodeFileHtmlRenderer`) |
| `_CodeLinePartial.cshtml` | Modern code line renderer (used when Razor still renders tree-style content) |
| `_CommentFormPartial.cshtml` | Comment input form with @mention support |
| `_CommentThreadPartial.cshtml` | Full comment thread (all replies, resolved state) |
| `_CommentThreadInnerPartial.cshtml` | Single comment within a thread |
| `_CommentThreadReplyPartial.cshtml` | Reply form within thread |
| `_APIRevisionsPartial.cshtml` | Revision dropdown selector |
| `_RevisionSelectPickerPartial.cshtml` | Advanced revision picker for selecting diff target |
| `_CrossLanguageViewPartial.cshtml` | Tabs showing the same API across multiple language SDKs |
| `_DiagnosticsPartial.cshtml` | Parser warning/error list |
| `_ReviewsPartial.cshtml` | Review list rows (used on Index page) |
| `_ReviewBadge.cshtml` | Approval status badge |
| `_AddAPIRevisionsPartial.cshtml` | Upload form for new API revision |
| `_AddSamplesRevisionsPartial.cshtml` | Upload form for code samples |
| `_SamplesRevisionsPartial.cshtml` | Sample version dropdown |
| `_ReviewUploadHelp.cshtml` | Upload format help text |
| `_SelectPickerPartial.cshtml` | Multi-select dropdown (Bootstrap-based) |
| `_CookieConsentPartial.cshtml` | Privacy/cookie consent banner |
| `_ValidationScriptsPartial.cshtml` | jQuery validation scripts |

---

## 5. Legacy MVC Controllers

In addition to Razor Pages, the legacy system uses traditional MVC controllers at [APIViewWeb/Controllers/](../APIViewWeb/Controllers/):

| Controller | Endpoints | Purpose |
|---|---|---|
| `AccountController` | `Login`, `Logout` | GitHub OAuth entry/exit |
| `ReviewController` | `UpdateApiReview`, `ApprovePackageName` | CI pipeline webhook, package approval |
| `CommentsController` | `Add`, `Update`, `Resolve` | Comment CRUD (both review and sample comments) |
| `AutoReviewController` | `GetReviewStatus` | Returns approval status to CI pipelines (200/201/202) |
| `UserProfileController` | (profile management) | User preferences |
| `AuthTestController` | (test endpoints) | Auth scheme validation (test-only) |

> **Modern equivalents** of these controllers exist in [APIViewWeb/LeanControllers/](../APIViewWeb/LeanControllers/) — 14 REST controllers with token-based auth. The legacy MVC controllers remain for backward compatibility with existing CI pipelines and the Razor page forms.

---

## 6. Legacy HTML Rendering Pipeline

When a flat-token (`ParserStyle.Flat`) review is rendered, the pipeline is:

```
CodeFile.Tokens[]  (flat CodeFileToken[] array)
        │
        ▼
CodeFileRenderer.Render()
  Groups tokens into CodeLine[] by Newline tokens
  Handles: DocumentRange, DeprecatedRange, SkipDiffRange,
           FoldableSections, HiddenApiRange, Tables
        │
        ▼
CodeFileHtmlRenderer.RenderToken()
  Wraps each token in <span> or <a> with CSS classes:
    .class (TypeName), .name (MemberName), .keyword,
    .value (StringLiteral), .code-comment, .commentable,
    .deprecated, .hidden-api, .documentation
        │
        ▼
_CodeLine.cshtml partial
  Emits line-by-line HTML with line numbers,
  comment anchors (DefinitionId), and diff styling
```

### a. Legacy Diff Rendering

1. Both revisions' `CodeFile` blobs are loaded from Blob Storage.
2. Each is rendered to parallel arrays: HTML lines (via `CodeFileHtmlRenderer`) and plain-text lines (via `CodeFileRenderer`).
3. `InlineDiff.Compute(oldText, newText, oldHtml, newHtml)` runs a longest-common-subsequence algorithm on the text lines and annotates the HTML lines with diff kinds (`Added`, `Removed`, `Unchanged`).
4. The Razor template applies CSS classes: `.diff-added` (green), `.diff-removed` (red/strikethrough), `.diff-change` (yellow).
5. Unchanged context is trimmed to 3 lines before/after each hunk, with `<span>.....</span>` separators.

---

## 7. Legacy Static Assets

The Razor pages use dedicated static assets at [APIViewWeb/wwwroot/](../APIViewWeb/wwwroot/):

| Asset | Purpose |
|---|---|
| `main.js` | Legacy JavaScript (jQuery-driven interactivity) |
| `main.css` | Legacy CSS (Razor page styling) |
| `lib/jquery/` | jQuery 3 |
| `lib/jquery-validation/` | Form validation |
| `lib/jquery-validation-unobtrusive/` | ASP.NET unobtrusive validation |
| `lib/bootstrap/` | Bootstrap 5 CSS/JS |
| `lib/mark/jquery.mark.js` | Text search highlighting |
| `lib/ResizeSensor/` | Container resize detection |
| `icons/` | Language and symbol SVG/PNG icons |
| `images/` | UI images |

The Angular SPA's build output lives separately under `wwwroot/spa/` and does not share these assets.

---

## 8. Coexistence Model

The Razor and SPA frontends coexist through a routing handoff:

```
User navigates to /Assemblies/{id}
        │
        ▼
  Review.cshtml.cs loads revision metadata
        │
        ├── ParserStyle == Tree?
        │       YES → HTTP redirect to /review/{id}/{revisionId}  (Angular SPA)
        │
        └── ParserStyle == Flat?
                YES → Render LegacyReview.cshtml
                       (degraded: "can not be viewed anymore" + comments)
```

The Angular SPA handles its own routing at `/review/...`, `/conversation/...`, `/revision/...`, `/samples/...`, etc. The ASP.NET backend serves the SPA as static files and provides the REST API at `/api/...`.

---

## 9. Migration Path

To fully retire the Razor frontend, the remaining flat-token languages need to be migrated to tree-style parsers:

| Language | Effort | Notes |
|---|---|---|
| **C** | Medium | Already builds `ReviewLine[]` internally; needs `UsesTreeStyleParser = true` flip and testing |
| **C++** | Medium | Same as C — internal transformer already produces hierarchical output |
| **Swagger / OpenAPI** | Medium | Pipeline-based parser needs to emit `ReviewLine[]` |
| **Protocol Buffers** | Low–Medium | External processor output needs format update |
| **JSON (generic)** | Low | Direct deserialization; needs to produce `ReviewLine[]` |
| **XML** | Low | Direct deserialization; needs to produce `ReviewLine[]` |

Once all languages emit `ParserStyle.Tree`, the Razor review pages, `CodeFileHtmlRenderer`, legacy `CodeFileRenderer` rendering path, `main.js`, `main.css`, jQuery libraries, and the legacy MVC controllers can be removed.
