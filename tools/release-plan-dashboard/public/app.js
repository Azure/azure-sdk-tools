(function () {
  "use strict";

  // ── Constants ───────────────────────────────────────────────
  const AUTO_REFRESH_INTERVAL = 60 * 60; // seconds (1 hour)
  const RETRY_DELAY_MS = 5000;
  const DEBOUNCE_DELAY_MS = 250;
  const FINISHED_DISPLAY_LIMIT = 20;
  const PR_STATUS_BATCH_SIZE = 10;
  const RENDER_THROTTLE_MS = 500;
  const COMMENT_MAX_LENGTH = 300;

  // Action button types used in language table and popup content
  const ACTION_TYPES = {
    GENERATE: "generate",
    FIX_CHECKS: "fix-checks",
    RELEASE: "release",
    MERGE: "merge",
    LINK_PR: "link-pr",
    MARK_READY: "mark-ready",
  };

  let refreshCountdown = AUTO_REFRESH_INTERVAL;
  let countdownTimer = null;

  // Canonical data — stored in Alpine store for reactivity.
  // These getters/setters provide convenient access from imperative code.
  function getPlans() { return store().plans; }
  function setPlans(plans) { store().plans = plans; }
  function getPrs() { return store().prs; }
  function setPrs(prs) { store().prs = prs; }

  const $ = (sel) => document.querySelector(sel);
  const $$ = (sel) => document.querySelectorAll(sel);

  // Alpine store reference — safe access that handles timing before Alpine loads
  function store() {
    if (typeof Alpine !== 'undefined' && Alpine.store) {
      return Alpine.store('app');
    }
    // Fallback proxy that queues updates (should rarely hit this)
    return _storeProxy;
  }

  // Proxy object that buffers store updates before Alpine initializes
  const _pendingUpdates = [];
  const _storeProxy = new Proxy({
    loading: true, loadingMessage: 'Loading release plans\u2026', error: '',
    showContent: false, showStatsBar: false,
    stats: { total: 0, inprogress: 0, partial: 0, new: 0, finished: 0, mgmt: 0, data: 0 },
    prCount: '',
    user: { name: '', isPM: false },
    filters: { search: '', plane: '', month: '', prLang: '', prStatus: '' },
    prFilterVisible: false,
  }, {
    set(target, prop, value) {
      target[prop] = value;
      _pendingUpdates.push({ prop, value });
      return true;
    }
  });

  // Flush buffered updates once Alpine is ready
  document.addEventListener('alpine:init', () => {
    const s = Alpine.store('app');
    for (const { prop, value } of _pendingUpdates) {
      if (typeof value === 'object' && !Array.isArray(value) && s[prop] && typeof s[prop] === 'object') {
        Object.assign(s[prop], value);
      } else {
        s[prop] = value;
      }
    }
    _pendingUpdates.length = 0;
  });

  // ── Create Release Plan guide modal ────────────────────────
  const _defaultModalTitle = 'How to Create a Release Plan';
  const _defaultModalBody = `<p>Release plans can be created using the <strong>Azure SDK Tools agent</strong> via <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Copilot CLI</a> or <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">VS Code</a>.</p>
    <h3>Step 1: Clone the API specs repo</h3>
    <pre class="guide-code"><code>git clone https://github.com/Azure/azure-rest-api-specs.git</code></pre>
    <p>Open Copilot CLI or VS Code from the repo root.</p>
    <h3>Step 2: Collect the following details</h3>
    <ol class="guide-list">
      <li><strong>Relative path</strong> to your TypeSpec project<br><em>e.g. <code>specification/contosowidgetmanager/Contoso.Management</code></em></li>
      <li><strong>SDK release type:</strong> beta or stable</li>
      <li><strong>API spec pull request</strong> (optional)</li>
      <li><strong>Service Tree ID</strong> for your service <em>(optional*)</em></li>
      <li><strong>Service Tree ID</strong> for your product <em>(optional*)</em></li>
    </ol>
    <p style="font-size:.82rem;color:#605e5c;">* Service Tree IDs are only required if this is the first time creating a release plan for your TypeSpec project path. For subsequent plans, they will be auto-populated.</p>
    <h3>Step 3: Use this prompt</h3>
    <p>Copy and paste the following prompt into Copilot CLI or VS Code Copilot chat, filling in your details:</p>
    <pre class="guide-prompt"><code>Create a release plan for TypeSpec project with following details:
- TypeSpec project path: &lt;specification/your-service/Your.Management&gt;
- SDK release type: &lt;beta or stable&gt;
- API spec PR: &lt;optional PR link&gt;
- Service Tree ID (Service): &lt;optional, your-service-tree-id&gt;
- Service Tree ID (Product): &lt;optional, your-product-tree-id&gt;</code></pre>
    <p style="font-size:.82rem;color:#605e5c;margin-top:12px;">📘 For more details, visit <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Azure SDK Tools Agent documentation</a>.</p>`;

  // Set default modal content in store once Alpine is ready
  document.addEventListener('alpine:init', () => {
    const s = Alpine.store('app');
    s._defaultModalTitle = _defaultModalTitle;
    s._defaultModalBody = _defaultModalBody;
  });

  const createPlanBtn = document.getElementById("btn-create-plan");
  if (createPlanBtn) {
    createPlanBtn.addEventListener("click", () => {
      store().openCreatePlanGuide();
    });
  }

  // ── Load user info ────────────────────────────────────────────
  // PM status is determined server-side only; client cannot grant itself PM access.
  // Even if someone sets store().user.isPM = true via DevTools, renderPMView()
  // guards on `currentUserIsPM` which is only set from the authenticated /auth/me response.
  let currentUserIsPM = false;
  async function loadUserInfo() {
    try {
      const res = await fetch("/auth/me");
      const user = await res.json();
      if (user && user.name) {
        store().user.name = user.name;
      }
      if (user && user.isPM) {
        currentUserIsPM = true;
        store().user.isPM = true;
      }
    } catch { /* ignore */ }
  }
  loadUserInfo();

  // ── Fetch data ──────────────────────────────────────────────
  async function fetchPlans() {
    store().loading = true;
    store().error = '';

    // Remember which cards are expanded before re-render
    const expandedIds = new Set();
    document.querySelectorAll("#tab-release-plans .card-summary.expanded").forEach(el => {
      const card = el.closest(".plan-card");
      if (card && card.dataset.planId) expandedIds.add(card.dataset.planId);
    });

    try {
      const params = new URLSearchParams(window.location.search);
      const planId = params.get("releasePlan") || params.get("releaseplan");
      let url = "/api/release-plans";
      if (planId) url += `?releasePlan=${encodeURIComponent(planId)}`;
      const res = await fetch(url);
      if (!res.ok) {
        throw new Error(`Failed to load release plans (${res.status}).`);
      }
      const data = await res.json();

      // Server is still warming up — show loading message and retry
      if (data.loading) {
        store().loadingMessage = "Release plans are being fetched. Dashboard will be available shortly\u2026";
        setTimeout(fetchPlans, RETRY_DELAY_MS);
        return;
      }

      // Release plan not found — show specific message
      if (data.notFound) {
        store().loading = false;
        store().error = `Release plan #${data.notFound} was not found. It may have been deleted or the ID is incorrect.`;
        store().showContent = false;
        return;
      }

      store().loading = false;
      setPlans(data.plans || []);

      // Populate month filter dropdown from available release months
      populateMonthFilter(getPlans());

      // Apply URL filter param if present
      const urlFilter = params.get("filter") || "";
      if (urlFilter) {
        store().filters.search = urlFilter;
      }

      // Apply URL month param if present
      const urlMonth = params.get("month") || "";
      if (urlMonth) {
        store().filters.month = urlMonth;
      }

      render(getPlans());
      if (currentUserIsPM) renderPMView(getPlans());
      updateTimestamp(data.fetchedAt);

      // Restore expanded cards after render
      if (expandedIds.size) {
        document.querySelectorAll("#tab-release-plans .plan-card").forEach(cardEl => {
          if (expandedIds.has(cardEl.dataset.planId)) {
            const summary = cardEl.querySelector(".card-summary");
            const details = cardEl.querySelector(".card-details");
            if (summary && details) {
              details.classList.add("open");
              summary.classList.add("expanded");
              if (!summary.dataset.prLoaded) {
                summary.dataset.prLoaded = "1";
                lazyLoadPrDetails(details, cardEl);
                if (cardEl.dataset.planId) lazyLoadPreviousSdkPrs(details, cardEl.dataset.planId);
              }
            }
          }
        });
      }

      // Auto-expand all cards when viewing a single release plan via URL param
      if (planId) {
        document.querySelectorAll("#tab-release-plans .plan-card").forEach(cardEl => {
          const summary = cardEl.querySelector(".card-summary");
          const details = cardEl.querySelector(".card-details");
          if (summary && details && !details.classList.contains("open")) {
            details.classList.add("open");
            summary.classList.add("expanded");
            if (!summary.dataset.prLoaded) {
              summary.dataset.prLoaded = "1";
              lazyLoadPrDetails(details, cardEl);
              if (cardEl.dataset.planId) lazyLoadPreviousSdkPrs(details, cardEl.dataset.planId);
            }
          }
        });
      }

      // Initialize PR tab with progressive status loading
      progressiveLoadPRStatuses(getPlans());
    } catch (err) {
      store().error = err.message;
    } finally {
      store().loading = false;
      resetCountdown();
    }
  }

  // ── Classify plane ─────────────────────────────────────────
  function classifyPlane(p) {
    // A plan can be both; we put it in mgmt if mgmt, data if data, mgmt as fallback
    if (p.mgmtScope === "Yes") return "mgmt";
    if (p.dataScope === "Yes") return "data";
    return "mgmt"; // default bucket
  }

  // ── Generate external package feed link ────────────────────
  function getPackageFeedUrl(lang, packageName, version, plan) {
    if (!packageName) return "";
    switch (lang) {
      case ".NET":
        return version
          ? `https://www.nuget.org/packages/${encodeURIComponent(packageName)}/${encodeURIComponent(version)}`
          : `https://www.nuget.org/packages/${encodeURIComponent(packageName)}`;
      case "Python":
        return version
          ? `https://pypi.org/project/${encodeURIComponent(packageName)}/${encodeURIComponent(version)}`
          : `https://pypi.org/project/${encodeURIComponent(packageName)}`;
      case "JavaScript":
        return version
          ? `https://www.npmjs.com/package/${encodeURIComponent(packageName)}/v/${encodeURIComponent(version)}`
          : `https://www.npmjs.com/package/${encodeURIComponent(packageName)}`;
      case "Java": {
        let groupName, artifactName;
        if (packageName.includes(":")) {
          const parts = packageName.split(":");
          groupName = parts[0];
          artifactName = parts[1];
        } else {
          artifactName = packageName;
          groupName = classifyPlane(plan) === "mgmt" ? "com.azure.resourcemanager" : "com.azure";
        }
        return version
          ? `https://central.sonatype.com/artifact/${encodeURIComponent(groupName)}/${encodeURIComponent(artifactName)}/${encodeURIComponent(version)}`
          : `https://central.sonatype.com/artifact/${encodeURIComponent(groupName)}/${encodeURIComponent(artifactName)}`;
      }
      case "Go":
        return version
          ? `https://github.com/Azure/azure-sdk-for-go/tree/${encodeURIComponent(packageName)}/v${encodeURIComponent(version)}/${encodeURIComponent(packageName)}`
          : `https://github.com/Azure/azure-sdk-for-go/tree/main/${encodeURIComponent(packageName)}`;
      default:
        return "";
    }
  }

  // Feed name and icon for each language's package registry
  function getPackageFeedInfo(lang) {
    switch (lang) {
      case ".NET":
        return { name: "NuGet", icon: '<svg class="feed-icon" viewBox="0 0 512 512" width="14" height="14"><circle cx="145" cy="367" r="105" fill="#004880"/><circle cx="371" cy="371" r="105" fill="#004880"/><circle cx="256" cy="145" r="73" fill="#004880"/></svg>' };
      case "Python":
        return { name: "PyPI", icon: '<svg class="feed-icon" viewBox="0 0 512 512" width="14" height="14"><path d="M254 1c-28 0-53 3-74 10-62 19-73 59-73 87v64h147v21H107C62 183 23 212 10 268c-15 64-16 104 0 171 12 49 40 85 88 85h57v-77c0-50 43-94 93-94h146c45 0 81-37 81-82V128c0-44-37-76-81-84-28-5-58-8-86-8h-54zm-82 50c17 0 30 14 30 31s-13 30-30 30c-17 0-31-13-31-30s14-31 31-31z" fill="#366994"/><path d="M393 183v75c0 52-44 96-93 96H154c-44 0-81 38-81 82v154c0 44 53 70 93 82 48 14 94 17 147 0 35-11 68-33 68-82V461H237v-21h215c47 0 64-33 81-82 18-51 17-99 0-171-12-52-35-82-81-82h-59v74zm-55 278c17 0 31 14 31 31s-14 30-31 30-30-13-30-30 13-31 30-31z" fill="#ffc331"/></svg>' };
      case "JavaScript":
        return { name: "npm", icon: '<svg class="feed-icon" viewBox="0 0 512 512" width="14" height="14"><rect width="512" height="512" fill="#cb3837" rx="50"/><path d="M227 327V185h57v142h57V128H100v256h127v-57z" fill="#fff"/></svg>' };
      case "Java":
        return { name: "Maven", icon: '<svg class="feed-icon" viewBox="0 0 512 512" width="14" height="14"><rect width="512" height="512" fill="#c71a36" rx="50"/><text x="256" y="350" text-anchor="middle" fill="#fff" font-size="200" font-family="serif" font-weight="bold">M</text></svg>' };
      case "Go":
        return { name: "GitHub", icon: '<svg class="feed-icon" viewBox="0 0 16 16" width="14" height="14"><path fill-rule="evenodd" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" fill="#333"/></svg>' };
      default:
        return { name: "Package", icon: "📦" };
    }
  }

  // A language is excluded only when ReleaseExclusionStatus is "Approved"
  function isLangExcluded(exclusionStatus) {
    const val = (exclusionStatus || "").toLowerCase().trim();
    return val === "approved";
  }

  function exclusionLabel(exclusionStatus) {
    const val = (exclusionStatus || "").toLowerCase().trim();
    if (val === "approved") return { text: "Exclusion Approved", cls: "row-excluded" };
    if (val === "requested") return { text: "Exclusion Requested", cls: "row-exclusion-requested" };
    return null;
  }

  // Check if a plan is private preview based on releasePlanType field
  // Values: "APEX Private Preview", "APEX Public Preview", "GA"
  function isPrivatePreviewPlan(p) {
    const rpt = (p.releasePlanType || "").toLowerCase();
    return rpt.includes("private");
  }

  /**
   * Computes the current workflow step and who action is required from.
   * Steps progress: API Spec → SDK Generation → SDK Review → Merge → Release.
   * IMPORTANT: PR status priority is merged > closed > draft > state. Closed is checked
   * before draft because GitHub keeps draft=true on closed draft PRs.
   * Release status uses exact match === "released" (not .includes) because "Unreleased"
   * contains "released".
   * @returns {{ status: string, action: string, statusClass: string }}
   */
  function computeCurrentStep(p) {
    const isPrivatePreview = isPrivatePreviewPlan(p);
    if (p.state === "Finished") {
      if (isPrivatePreview) return { status: "Completed", action: "", statusClass: "step-released" };
      return { status: "Released", action: "", statusClass: "step-released" };
    }

    const specPrUrl = (p.apiSpec && p.apiSpec.specPrUrl) || "";
    const apiReady = (p.apiReadiness || "").toLowerCase();

    // Step 1: API Spec checks
    const serviceTeam = p.submittedBy ? `Service Team (${p.submittedBy})` : "Service Team";
    if (!specPrUrl) return { status: "API Spec Not Available", action: serviceTeam, statusClass: "step-blocked" };
    if (apiReady !== "completed") return { status: "API Spec In Progress", action: "Spec PR Reviewer", statusClass: "step-inprogress" };

    // Private preview: no SDK stages — spec merged means done
    if (isPrivatePreview) return { status: "Completed", action: "", statusClass: "step-released" };

    // API spec is merged — check SDK status across non-excluded languages
    const langs = p.languages || {};
    const langKeys = Object.keys(langs);
    const activeLangs = langKeys.filter(k => !isLangExcluded(langs[k].exclusionStatus));
    if (!activeLangs.length) return { status: "No Active Languages", action: "", statusClass: "step-blocked" };

    // Check generation status
    const genFailed = activeLangs.some(k => {
      const gs = (langs[k].generationStatus || "").toLowerCase();
      return gs.includes("failed") || gs.includes("error");
    });
    if (genFailed) return { status: "SDK Generation Failed", action: serviceTeam, statusClass: "step-failed" };

    // Check if any SDK PRs exist
    const langsWithPr = activeLangs.filter(k => langs[k].sdkPrUrl);
    if (!langsWithPr.length) return { status: "SDK To Be Generated", action: serviceTeam, statusClass: "step-pending" };

    // Check PR statuses (use GitHub status if available, fall back to DevOps)
    const prStatuses = langsWithPr.map(k => {
      const l = langs[k];
      return (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
    });
    const allMerged = prStatuses.every(s => s.includes("merged") || s.includes("completed"));
    const allApproved = langsWithPr.every(k => {
      const d = langs[k].prDetails;
      return d && d.isApproved;
    });

    // Check release statuses
    const releaseStatuses = activeLangs.map(k => (langs[k].releaseStatus || "").toLowerCase());
    const allReleased = releaseStatuses.every(s => s.includes("completed") || s.includes("released"));

    if (allMerged && allReleased) return { status: "Released", action: "", statusClass: "step-released" };
    if (allMerged) return { status: "SDK Ready To Be Released", action: serviceTeam, statusClass: "step-ready" };
    if (allApproved) return { status: "SDK To Be Merged", action: serviceTeam, statusClass: "step-pending" };

    // If any PR is in draft (and not released), action is from Service Team
    const anyDraft = langsWithPr.some(k => {
      const st = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
      const rel = (langs[k].releaseStatus || "").toLowerCase();
      return st === "draft" && !rel.includes("released") && !rel.includes("completed");
    });
    if (anyDraft) return { status: "SDK PR In Draft", action: serviceTeam, statusClass: "step-pending" };

    // Some PRs not yet approved/merged
    return { status: "SDK Review In Progress", action: "SDK PR Reviewer", statusClass: "step-inprogress" };
  }

  // ── Classify release status ─────────────────────────────────
  // Returns "partial" if at least one language is released/completed
  // but not all non-excluded languages are released.
  function isPartiallyReleased(p) {
    const langs = p.languages || {};
    const langKeys = Object.keys(langs);
    if (!langKeys.length) return false;

    let releasedCount = 0;
    let excludedCount = 0;
    for (const lang of langKeys) {
      const l = langs[lang];
      const rel = (l.releaseStatus || "").toLowerCase();
      if (isLangExcluded(l.exclusionStatus)) excludedCount++;
      if (rel.includes("completed") || rel.includes("released")) releasedCount++;
    }
    const nonExcluded = langKeys.length - excludedCount;
    return releasedCount > 0 && releasedCount < nonExcluded;
  }

  // Parse "MMMM yyyy" into a sortable Date (or far future if unparseable)
  function parseReleaseMonth(str) {
    if (!str) return new Date(9999, 0);
    const d = new Date(str + " 1");
    return isNaN(d.getTime()) ? new Date(9999, 0) : d;
  }

  function isPastDue(p) {
    if (p.state === "Finished") return false;
    if (!p.releaseMonth) return false;
    const target = parseReleaseMonth(p.releaseMonth);
    if (target.getFullYear() === 9999) return false;
    // Past due if the target month is strictly before this month
    const now = new Date();
    const thisMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    return target < thisMonth;
  }

  // ── Detect possible duplicate release plans ──────────────────
  function detectDuplicates(plans) {
    plans.forEach(p => { delete p._duplicateOf; });

    // Group by plane + product name + release type
    const groups = new Map();
    for (const p of plans) {
      if (p.state === "Finished") continue;
      const product = (p.productName || "").toLowerCase().trim();
      const rt = (p.releaseType || "").toLowerCase().trim();
      if (!product) continue;
      const plane = classifyPlane(p);
      const key = `${plane}|${product}|${rt}`;
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(p);
    }

    for (const [, group] of groups) {
      if (group.length < 2) continue;

      let main = null;

      // Prefer one with SDK PRs linked
      const withPr = group.filter(p => {
        const langs = p.languages || {};
        return Object.values(langs).some(l => l.sdkPrUrl && !isLangExcluded(l.exclusionStatus));
      });
      const withoutPr = group.filter(p => {
        const langs = p.languages || {};
        return !Object.values(langs).some(l => l.sdkPrUrl && !isLangExcluded(l.exclusionStatus));
      });

      if (withPr.length >= 1 && withoutPr.length >= 1) {
        main = withPr[0];
        for (const dup of withoutPr) {
          dup._duplicateOf = main.releasePlanId || main.title;
        }
        continue;
      }

      // Prefer In Progress over New/Not Started
      const inProg = group.filter(p => p.state === "In Progress");
      const newOnes = group.filter(p => p.state === "New" || p.state === "Not Started");
      if (inProg.length >= 1 && newOnes.length >= 1) {
        main = inProg[0];
        for (const dup of newOnes) {
          dup._duplicateOf = main.releasePlanId || main.title;
        }
      }
    }
  }

  function getGlobalPlaneFilter() {
    return store().filters.plane;
  }

  // Populate the month filter dropdown with available release months
  function populateMonthFilter(plans) {
    const monthSet = new Set();
    for (const p of plans) {
      if (p.releaseMonth) monthSet.add(p.releaseMonth);
    }
    // Sort months chronologically
    const months = [...monthSet].sort((a, b) => parseReleaseMonth(a) - parseReleaseMonth(b));
    const select = document.getElementById("global-month-filter");
    if (!select) return;
    const currentValue = select.value;
    // Keep the "All Months" option, replace the rest
    select.innerHTML = '<option value="">All Months</option>';
    for (const m of months) {
      const opt = document.createElement("option");
      opt.value = m;
      opt.textContent = m;
      select.appendChild(opt);
    }
    // Restore selection if it still exists
    if (currentValue && months.includes(currentValue)) select.value = currentValue;
  }

  // Update URL parameters to reflect current filter state (for sharing)
  function syncFiltersToUrl() {
    const params = new URLSearchParams(window.location.search);
    const filter = store().filters.search.trim();
    const month = store().filters.month;

    if (filter) params.set("filter", filter); else params.delete("filter");
    if (month) params.set("month", month); else params.delete("month");

    const newUrl = params.toString() ? `${window.location.pathname}?${params}` : window.location.pathname;
    window.history.replaceState(null, "", newUrl);
  }

  // ── Render ──────────────────────────────────────────────────
  function matchesFilter(p, filter) {
    if (p.title.toLowerCase().includes(filter)) return true;
    if ((p.productName || "").toLowerCase().includes(filter)) return true;
    if ((p.serviceName || "").toLowerCase().includes(filter)) return true;
    if ((p.ownerPM || "").toLowerCase().includes(filter)) return true;
    if ((p.submittedBy || "").toLowerCase().includes(filter)) return true;
    if (String(p.releasePlanId || "").toLowerCase().includes(filter)) return true;
    // Spec project path / TypeSpec path
    if ((p.typeSpecPath || "").toLowerCase().includes(filter)) return true;
    // Spec PR URL or PR number
    if (p.apiSpec && p.apiSpec.specPrUrl && p.apiSpec.specPrUrl.toLowerCase().includes(filter)) return true;
    // Package names across all languages
    const langs = p.languages || {};
    for (const lang of Object.keys(langs)) {
      if ((langs[lang].packageName || "").toLowerCase().includes(filter)) return true;
      if ((langs[lang].sdkPrUrl || "").toLowerCase().includes(filter)) return true;
    }
    return false;
  }

  function getMonthFilter() {
    return store().filters.month;
  }

  function render(plans) {
    detectDuplicates(plans);
    const planeFilter = getGlobalPlaneFilter();
    const monthFilter = getMonthFilter();
    const filter = store().filters.search.toLowerCase();
    let filtered = filter ? plans.filter(p => matchesFilter(p, filter)) : plans;
    if (planeFilter) filtered = filtered.filter(p => classifyPlane(p) === planeFilter);
    if (monthFilter) filtered = filtered.filter(p => (p.releaseMonth || "").toLowerCase() === monthFilter.toLowerCase());

    const mgmt = filtered.filter((p) => classifyPlane(p) === "mgmt");
    const data = filtered.filter((p) => classifyPlane(p) === "data");

    // Detect if filtering is active (needed by splitByState)
    const params = new URLSearchParams(window.location.search);
    const singlePlan = params.get("releasePlan") || params.get("releaseplan");
    const isFiltering = !!(filter || singlePlan || monthFilter);

    function sortByReleaseMonth(a, b) {
      return parseReleaseMonth(a.releaseMonth) - parseReleaseMonth(b.releaseMonth);
    }

    function splitByState(arr) {
      const partial = [];
      const inProgress = [];
      const newItems = [];
      const finished = [];

      for (const p of arr) {
        if (p.state === "Finished") {
          finished.push(p);
        } else if (p.state === "New" || p.state === "Not Started") {
          newItems.push(p);
        } else if (isPartiallyReleased(p)) {
          partial.push(p);
        } else {
          inProgress.push(p);
        }
      }

      inProgress.sort(sortByReleaseMonth);
      partial.sort(sortByReleaseMonth);
      newItems.sort(sortByReleaseMonth);

      // When filtering/searching, show all finished plans that match;
      // otherwise limit to this/last month, max 20
      if (isFiltering) {
        finished.sort(sortByReleaseMonth);
        return { inProgress, partial, newItems, finished };
      }
      const now = new Date();
      const thisMonthKey = `${now.toLocaleString("en-US", { month: "long" })} ${now.getFullYear()}`.toLowerCase();
      const lastDate = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      const lastMonthKey = `${lastDate.toLocaleString("en-US", { month: "long" })} ${lastDate.getFullYear()}`.toLowerCase();
      const recentFinished = finished.filter(p => {
        const rm = (p.releaseMonth || "").toLowerCase();
        return rm.includes(thisMonthKey) || rm.includes(lastMonthKey);
      });
      recentFinished.sort(sortByReleaseMonth);
      const cappedFinished = recentFinished.slice(0, FINISHED_DISPLAY_LIMIT);

      return { inProgress, partial, newItems, finished: cappedFinished };
    }

    const mgmtSplit = splitByState(mgmt);
    const dataSplit = splitByState(data);

    // Populate store sections for reactive rendering
    const sec = store().sections;
    sec.mgmtInprogress = mgmtSplit.inProgress;
    sec.mgmtPartial = mgmtSplit.partial;
    sec.mgmtNew = mgmtSplit.newItems;
    sec.mgmtFinished = mgmtSplit.finished;
    sec.dataInprogress = dataSplit.inProgress;
    sec.dataPartial = dataSplit.partial;
    sec.dataNew = dataSplit.newItems;
    sec.dataFinished = dataSplit.finished;
    store().isFiltering = isFiltering;

    // Hide plane columns based on global plane filter
    const mgmtCol = document.getElementById("plane-col-mgmt");
    const dataCol = document.getElementById("plane-col-data");
    if (mgmtCol) mgmtCol.style.display = (planeFilter === "data") ? "none" : "";
    if (dataCol) dataCol.style.display = (planeFilter === "mgmt") ? "none" : "";

    // Hide plane headings when single releasePlan param or when plane has no items
    const mgmtHeading = $(".plane-heading-mgmt");
    const dataHeading = $(".plane-heading-data");
    if (singlePlan) {
      if (mgmtHeading) mgmtHeading.style.display = mgmt.length ? "" : "none";
      if (dataHeading) dataHeading.style.display = data.length ? "" : "none";
    } else {
      if (mgmtHeading) mgmtHeading.style.display = "";
      if (dataHeading) dataHeading.style.display = "";
    }

    // Stats — update Alpine store
    const totalInProgress = mgmtSplit.inProgress.length + dataSplit.inProgress.length;
    const totalPartial = mgmtSplit.partial.length + dataSplit.partial.length;
    const totalNew = mgmtSplit.newItems.length + dataSplit.newItems.length;
    const totalFinished = mgmtSplit.finished.length + dataSplit.finished.length;

    const s = store();
    s.stats.total = filtered.length;
    s.stats.inprogress = totalInProgress;
    s.stats.partial = totalPartial;
    s.stats.new = totalNew;
    s.stats.finished = totalFinished;
    s.stats.mgmt = mgmt.length;
    s.stats.data = data.length;
    s.showStatsBar = !singlePlan;
    s.showContent = true;
  }

  /** Refresh a single plan card when the refresh button is clicked. */
  async function handlePlanRefresh(btn) {
    const planId = btn.dataset.planId;
    if (!planId || btn.disabled) return;
    btn.disabled = true;
    btn.classList.add("spinning");
    try {
      const resp = await fetch(`/api/refresh-plan/${planId}`, { method: "POST" });
      const data = await resp.json();
      if (data.plan) {
        const idx = getPlans().findIndex(p => p.id === data.plan.id);
        if (idx >= 0) getPlans()[idx] = data.plan;
        // Re-render triggers Alpine x-for update with new section arrays
        render(getPlans());
      }
    } catch (err) {
      console.error("Refresh plan error:", err);
    } finally {
      btn.disabled = false;
      btn.classList.remove("spinning");
    }
  }



  function shortDate(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    if (isNaN(d)) return "";
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
  }

  // ── Card HTML ───────────────────────────────────────────────
  function apiReadinessBadge(p) {
    if (p.apiReadiness === "completed") {
      return '<span class="badge badge-api-completed">API Ready</span>';
    }
    if (p.apiReadiness === "pending") {
      return '<span class="badge badge-api-pending">API Pending</span>';
    }
    return "";
  }

  /** Generates the HTML for a release plan card (collapsed summary + expandable detail). */
  function cardHTML(p, options) {
    const showPmAction = !!(options && options.showPmAction && currentUserIsPM);
    const pastDue = isPastDue(p);
    const cardClass = pastDue ? "plan-card past-due" : "plan-card";
    const step = computeCurrentStep(p);
    const copilotBadge = (p.createdUsing || "").toLowerCase() === "copilot"
      ? '<span class="badge badge-created-using">Copilot</span>'
      : "";
    const rt = (p.releaseType || "").toLowerCase();
    const sdkTypeBadge = rt.includes("beta") || rt.includes("preview")
      ? '<span class="badge badge-sdk-beta">Beta</span>'
      : rt.includes("ga") || rt.includes("stable")
        ? '<span class="badge badge-sdk-stable">Stable</span>'
        : "";
    const isTerminal = step.status === "Released" || step.status === "Completed";
    const finishedBadge = (p.state === "Finished")
      ? `<span class="badge badge-finished-indicator">✔ ${esc(step.status)}</span>`
      : "";
    const stepHTML = (step.status && !isTerminal)
      ? `<span class="step-badge ${step.statusClass}">${esc(step.status)}</span>`
      : "";
    const actionHTML = (step.action && !isTerminal && !(showPmAction && p._pmAction))
      ? `<span class="action-badge">Action required from: ${esc(step.action)}</span>`
      : "";
    const dupHTML = p._duplicateOf
      ? `<span class="badge badge-duplicate">⚠️ Duplicate of ${esc(String(p._duplicateOf))}</span>`
      : "";
    const isExpanded = !!(store().ui.expandedPlans[p.id]);
    const summaryClass = isExpanded ? "card-summary expanded" : "card-summary";
    const detailsClass = isExpanded ? "card-details open" : "card-details";
    return `
    <div class="${cardClass}" data-plan-id="${p.id}">
      <div class="${summaryClass}"${isExpanded ? ' data-pr-loaded="1"' : ""}>
        <span class="card-chevron">&#9654;</span>
        <div class="card-title">
          ${esc(p.title)} ${copilotBadge} ${sdkTypeBadge}
        </div>
        <div class="card-meta">
          ${p.releaseMonth ? `<span>${esc(p.releaseMonth)}</span>` : ""}
          ${p.submittedBy ? `<span class="card-submitter">${esc(p.submittedBy)}</span>` : ""}
          ${stepHTML}${actionHTML}${finishedBadge}${dupHTML}
          ${apiReadinessBadge(p)}
          ${pastDue ? '<span class="badge badge-pastdue">Past Due</span>' : ""}
        </div>
        <button class="plan-share-btn" data-plan-id="${esc(String(p.releasePlanId || p.id))}" title="Share this release plan">&#x1F517;</button>
        <button class="plan-refresh-btn" data-plan-id="${esc(String(p.id))}" title="Refresh this release plan">&#x21bb;</button>
      </div>
      <div class="${detailsClass}">${detailHTML(p, { showPmAction })}</div>
    </div>`;
  }

  // ── PR detail labels (checks, approval, mergeable) ──────────
  function prDetailLabels(l) {
    if (!l.prDetails) return "";
    const d = l.prDetails;
    const isMerged = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase().includes("merged");
    const isClosed = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase() === "closed";
    const isOpenOrDraft = !isMerged && !isClosed;
    let labels = "";
    if (isOpenOrDraft && d.failedChecks && d.failedChecks.length) {
      const checksUrl = l.sdkPrUrl ? l.sdkPrUrl.replace(/\/$/, "") + "/checks" : "";
      const checksLink = checksUrl ? ` <a href="${esc(checksUrl)}" target="_blank" rel="noopener" style="font-size:.75rem;">View checks</a>` : "";
      labels += `<span class="pr-label pr-label-failed">${d.failedChecks.length} check(s) failed${checksLink}</span>`;
    }
    if (!isMerged && d.isApproved && d.approvedBy && d.approvedBy.length) {
      const tipText = "Approved by: " + esc(d.approvedBy.join(", "));
      labels += `<span class="pr-label pr-label-approved" title="${tipText}">Approved</span>`;
    }
    if (isOpenOrDraft && d.mergeable && d.mergeableState === "clean") {
      labels += '<span class="pr-label pr-label-mergeable">Ready to merge</span>';
    }
    return labels;
  }

  // ── Per-language action button for SDK table ──────────────────
  function langActionBtn(type, lang, plan, langData) {
    const planId = plan.releasePlanId || "";
    const specPath = plan.specProjectPath || plan.typeSpecPath || "";
    const prUrl = langData.sdkPrUrl || "";
    const pkg = langData.packageName || "";
    // Encode data attributes for the popup handler
    const attrs = `data-action-type="${esc(type)}" data-lang="${esc(lang)}" data-plan-id="${esc(planId)}" data-spec-path="${esc(specPath)}" data-pr-url="${esc(prUrl)}" data-pkg="${esc(pkg)}"`;
    const labels = {
      [ACTION_TYPES.GENERATE]: "⚡ Generate SDK",
      [ACTION_TYPES.FIX_CHECKS]: "🔧 Fix Checks",
      [ACTION_TYPES.RELEASE]: "🚀 Release",
      [ACTION_TYPES.MERGE]: "✅ Merge PR",
      [ACTION_TYPES.LINK_PR]: "🔗 Link PR",
      [ACTION_TYPES.MARK_READY]: "📋 Mark Ready",
    };
    const classes = {
      [ACTION_TYPES.GENERATE]: "action-btn-generate",
      [ACTION_TYPES.FIX_CHECKS]: "action-btn-fix",
      [ACTION_TYPES.RELEASE]: "action-btn-release",
      [ACTION_TYPES.MERGE]: "action-btn-merge",
      [ACTION_TYPES.LINK_PR]: "action-btn-link",
      [ACTION_TYPES.MARK_READY]: "action-btn-ready",
    };
    return `<button class="lang-action-btn ${classes[type] || ""}" ${attrs}>${labels[type] || type}</button>`;
  }

  // Action popup content builder
  function buildActionPopupContent(type, lang, planId, specPath, prUrl, pkg) {
    let title = "";
    let body = "";
    const agentLink = '<a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Azure SDK Tools agent</a>';
    const repoUrls = {
      javascript: "https://github.com/Azure/azure-sdk-for-js.git",
      java: "https://github.com/Azure/azure-sdk-for-java.git",
      python: "https://github.com/Azure/azure-sdk-for-python.git",
      ".net": "https://github.com/Azure/azure-sdk-for-net.git",
      go: "https://github.com/Azure/azure-sdk-for-go.git"
    };
    const repoUrl = repoUrls[(lang || "").toLowerCase()] || "";
    const repoNote = repoUrl
      ? `<p style="font-size:.82rem;color:#605e5c;margin-top:8px;">Clone <a href="${esc(repoUrl)}" target="_blank" rel="noopener"><code>${esc(repoUrl)}</code></a> and open Copilot CLI or VS Code from the repo root.</p>`
      : '<p style="font-size:.82rem;color:#605e5c;margin-top:8px;">Clone the relevant SDK repo and open Copilot CLI or VS Code from the repo root.</p>';

    switch (type) {
      case ACTION_TYPES.GENERATE:
        title = `Generate SDK for ${lang}`;
        body = `<p>The SDK pull request has not been generated for <strong>${esc(lang)}</strong> yet.</p>
          <p>Use the ${agentLink} to generate the SDK:</p>
          ${repoNote}
          <div class="guide-prompt"><code>Generate ${esc(lang)} SDK${pkg ? ` for package ${esc(pkg)}` : ""}${specPath ? ` for TypeSpec project ${esc(specPath)}` : ""} for release plan ${esc(planId)}</code></div>
          ${specPath ? `<p style="font-size:.82rem;color:#605e5c;">TypeSpec path: <code>${esc(specPath)}</code></p>` : ""}`;
        break;
      case ACTION_TYPES.FIX_CHECKS:
        title = `Fix Check Failures — ${lang}${pkg ? ` (${pkg})` : ""}`;
        body = `<p>The SDK pull request for <strong>${esc(lang)}</strong>${pkg ? ` package <code>${esc(pkg)}</code>` : ""} has failing CI checks.</p>
          <p>Use the ${agentLink} to diagnose and fix the failures:</p>
          ${repoNote}
          <div class="guide-prompt"><code>Fix CI check failures on ${esc(lang)} SDK pull request ${esc(prUrl)}${pkg ? ` for package ${esc(pkg)}` : ""}</code></div>
          <p style="margin-top:8px;">Alternatively, clone the repo locally, checkout the PR branch, and run the build/tests to identify and fix the issues.</p>`;
        break;
      case ACTION_TYPES.RELEASE:
        title = `Release ${lang} Package${pkg ? ` — ${pkg}` : ""}`;
        body = `<p>The SDK pull request for <strong>${esc(lang)}</strong>${pkg ? ` package <code>${esc(pkg)}</code>` : ""} has been merged. The package is ready to be released.</p>
          <p>Use the ${agentLink} to trigger the release:</p>
          <div class="guide-prompt"><code>Release ${esc(lang)} SDK package${pkg ? ` ${esc(pkg)}` : ""} for release plan ${esc(planId)}</code></div>
          <p style="font-size:.82rem;color:#605e5c;margin-top:8px;">This will trigger the release pipeline for the package.</p>`;
        break;
      case ACTION_TYPES.MERGE:
        title = `Merge PR — ${lang}${pkg ? ` (${pkg})` : ""}`;
        body = `<p>The SDK pull request for <strong>${esc(lang)}</strong>${pkg ? ` package <code>${esc(pkg)}</code>` : ""} is approved and all checks are passing. It is ready to be merged.</p>
          <p><a href="${esc(prUrl)}" target="_blank" rel="noopener">Open the PR on GitHub</a> and merge it.</p>`;
        break;
      case ACTION_TYPES.LINK_PR:
        title = `Link SDK PR — ${lang}${pkg ? ` (${pkg})` : ""}`;
        body = `<p>The SDK pull request for <strong>${esc(lang)}</strong>${pkg ? ` package <code>${esc(pkg)}</code>` : ""} is <strong>closed</strong> without being merged.</p>
          <p><strong>Steps:</strong></p>
          <ol>
            <li>Check if a different/replacement PR has been created for this package in the SDK repository.</li>
            <li>If a new PR exists, link it to this release plan using the ${agentLink}.</li>
            <li>If no new PR exists, generate a new SDK PR using the agent.</li>
          </ol>
          <p><strong>To link an existing PR:</strong></p>
          ${repoNote}
          <div class="guide-prompt"><code>Link ${esc(lang)} SDK pull request &lt;PR URL&gt; to release plan ${esc(planId)}</code></div>
          <p style="font-size:.82rem;color:#605e5c;margin-top:8px;">Replace <code>&lt;PR URL&gt;</code> with the URL of the correct ${esc(lang)} SDK pull request.</p>
          <p><strong>To generate a new PR instead:</strong></p>
          <div class="guide-prompt"><code>Generate ${esc(lang)} SDK${pkg ? ` for package ${esc(pkg)}` : ""} for release plan ${esc(planId)}</code></div>`;
        break;
      case ACTION_TYPES.MARK_READY:
        title = `Mark PR Ready for Review — ${lang}`;
        body = `<p>The SDK pull request for <strong>${esc(lang)}</strong>${pkg ? ` — package <code>${esc(pkg)}</code>` : ""} is currently in <strong>draft</strong> status.</p>
          <p><strong>Required action:</strong> Mark the pull request as ready for review on GitHub so the SDK team can begin their review.</p>
          <p><strong>Steps:</strong></p>
          <ol>
            <li>Open the SDK pull request:<br><a href="${esc(prUrl)}" target="_blank" rel="noopener">${esc(prUrl)}</a></li>
            <li>Click the <strong>"Ready for review"</strong> button at the bottom of the PR page.</li>
          </ol>
          <p style="font-size:.82rem;color:#605e5c;margin-top:8px;">Once marked ready, the SDK team will review the PR and provide feedback or approve it.</p>`;
        break;
    }
    return { title, body };
  }

  /** Generates the "Action Required" guidance section with step-by-step instructions. */
  function actionRequiredHTML(p) {
    const step = computeCurrentStep(p);
    const isTerminal = step.status === "Released" || step.status === "Completed";
    if (isTerminal || p.state === "Finished") return "";

    const specPath = p.specProjectPath || p.typeSpecPath || "";
    const specPrUrl = (p.apiSpec && p.apiSpec.specPrUrl) || "";
    const planId = p.releasePlanId || "";
    let actionContent = "";

    if (step.status === "API Spec Not Available") {
      if (!specPrUrl) {
        actionContent = `<div class="action-item">
          <strong>Create API Spec PR:</strong>
          <ol>
            <li>Clone the <code>azure-rest-api-specs</code> repo</li>
            <li>Open <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">copilot-cli</a> from the cloned repo path, or open the cloned repo in VS Code and open GitHub Copilot chat</li>
            <li>Run the following prompt:
              <div class="action-prompt"><code>Create or update API spec for my service, create a spec PR, and update the spec PR in release plan ${esc(planId)}</code></div>
            </li>
          </ol>
        </div>`;
      }
    } else if (step.status === "API Spec In Progress") {
      actionContent = `<div class="action-item">
        <strong>Get API Spec Reviewed & Merged:</strong>
        <p>The spec PR <a href="${esc(specPrUrl)}" target="_blank" rel="noopener">${esc(specPrUrl)}</a> needs to be reviewed and merged before SDK generation can proceed.</p>
      </div>`;
    } else if (step.status === "SDK To Be Generated" || step.status === "SDK Generation Failed") {
      actionContent = `<div class="action-item">
        <strong>Generate SDKs:</strong>
        <ol>
          <li>Clone the <code>azure-rest-api-specs</code> repo</li>
          <li>Open <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">copilot-cli</a> from the cloned repo path, or open the cloned repo in VS Code and open GitHub Copilot chat</li>
          <li>Run the following prompt:
            <div class="action-prompt"><code>Generate SDK for all languages from my TypeSpec project ${esc(specPath)} and link SDK pull requests to the release plan ${esc(planId)}</code></div>
          </li>
        </ol>
      </div>`;
    } else if (step.status === "SDK PR In Draft") {
      actionContent = `<div class="action-item">
        <strong>Mark SDK pull request as ready for review:</strong>
        <p>One or more SDK pull requests are in <strong>draft</strong> status. Mark them as ready for review on GitHub so the SDK team can begin their review.</p>
      </div>`;
    } else if (step.status === "SDK Review In Progress") {
      actionContent = `<div class="action-item">
        <strong>SDK PRs Under Review:</strong>
        <p>SDK pull requests are currently being reviewed. Please ensure all required reviews are addressed.</p>
      </div>`;
    } else if (step.status === "SDK To Be Merged") {
      actionContent = `<div class="action-item">
        <strong>Merge SDK PRs:</strong>
        <p>SDK pull requests are approved and ready to be merged.</p>
      </div>`;
    } else if (step.status === "SDK Ready To Be Released") {
      // Build list of languages with merged PRs but not yet released
      const langs = p.languages || {};
      const langKeys = Object.keys(langs);
      const toRelease = langKeys.filter(k => {
        if (isLangExcluded(langs[k].exclusionStatus)) return false;
        const st = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
        const rel = (langs[k].releaseStatus || "").toLowerCase();
        return (st.includes("merged") || st.includes("completed")) && !rel.includes("completed") && !rel.includes("released");
      });
      const langList = toRelease.length ? toRelease.join(", ") : "all pending languages";
      const pkgNames = toRelease.map(k => langs[k].packageName).filter(Boolean).join(", ");
      actionContent = `<div class="action-item">
        <strong>Release SDKs (${esc(langList)}):</strong>
        <ol>
          <li>Open <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">copilot-cli</a> from the <code>azure-rest-api-specs</code> or SDK language repo path, or open the repo in VS Code and open GitHub Copilot chat</li>
          <li>Run a prompt like:
            <div class="action-prompt"><code>Release SDK ${esc(pkgNames || "package-name")} in ${esc(langList)}</code></div>
          </li>
          <li>After the build stage completes, approve the release stage in the release pipeline</li>
        </ol>
      </div>`;
    }

    // Check for SDK PRs with failed checks
    {
      const langs = p.languages || {};
      const langsWithFailedChecks = Object.keys(langs).filter(k => {
        if (isLangExcluded(langs[k].exclusionStatus)) return false;
        // Only show check failures for open/draft PRs, not merged
        const st = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
        if (st.includes("merged") || st === "closed") return false;
        const d = langs[k].prDetails;
        return d && d.failedChecks && d.failedChecks.length > 0;
      });
      if (langsWithFailedChecks.length) {
        for (const lang of langsWithFailedChecks) {
          const l = langs[lang];
          const prUrl = l.sdkPrUrl || "";
          const prNum = prUrl.match(/\/pull\/(\d+)/);
          const prNumber = prNum ? prNum[1] : "";
          const pkgName = l.packageName || "package";
          // Determine repo name from PR URL
          const repoMatch = prUrl.match(/github\.com\/[^/]+\/([^/]+)\//);
          const repoName = repoMatch ? repoMatch[1] : "azure-sdk-for-" + lang.toLowerCase();
          actionContent += `<div class="action-item action-item-warning" style="margin-top:10px;">
            <strong>⚠️ Fix check failures for ${esc(lang)}:</strong>
            <ol>
              <li>Clone the <code>${esc(repoName)}</code> repo and checkout the PR:
                <div class="action-prompt"><code>gh pr checkout ${esc(prNumber)}</code></div>
              </li>
              <li>Open <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">copilot-cli</a> from the repo, or open the repo in VS Code and open GitHub Copilot chat</li>
              <li>Run the following prompt:
                <div class="action-prompt"><code>Run validation for SDK package ${esc(pkgName)} and fix any build errors (Apply API spec customization if customization is required to resolve build errors). If change log requires update then update the change log. Push changes to PR if validation is successful</code></div>
              </li>
            </ol>
          </div>`;
        }
      }
    }

    // Check for SDK PRs in closed (not merged) status
    {
      const langs = p.languages || {};
      const closedPrLangs = Object.keys(langs).filter(k => {
        if (isLangExcluded(langs[k].exclusionStatus)) return false;
        if (!langs[k].sdkPrUrl) return false;
        const st = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
        return st === "closed";
      });
      if (closedPrLangs.length) {
        const langList = closedPrLangs.join(", ");
        actionContent += `<div class="action-item action-item-warning" style="margin-top:10px;">
          <strong>⚠️ SDK PR closed for ${esc(langList)}:</strong>
          <p>The SDK pull request has been closed without merging. You need to either:</p>
          <ul>
            <li><strong>Regenerate the SDK</strong> for ${esc(langList)} using the <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Azure SDK Tools agent</a></li>
            <li><strong>Link a different PR</strong> (if one exists) to the release plan using the prompt:
              <div class="action-prompt"><code>Link SDK pull request &lt;PR link&gt; to release plan ${esc(planId)}</code></div>
            </li>
          </ul>
        </div>`;
      }
    }

    // Additional prompt: if package details are missing but spec info is available
    const hasSpecInfo = specPrUrl || specPath;
    if (hasSpecInfo) {
      const langs = p.languages || {};
      const missingPkgDetails = Object.keys(langs).some(k => !isLangExcluded(langs[k].exclusionStatus) && !langs[k].packageName);
      if (missingPkgDetails) {
        actionContent += `<div class="action-item" style="margin-top:10px;">
          <strong>Update SDK details in release plan:</strong>
          <p>Package details are missing for some languages. Run the following prompt using <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">copilot-cli</a> or VS Code GitHub Copilot chat:</p>
          <div class="action-prompt"><code>Update SDK details in release plan ${esc(planId)} using the TypeSpec project path ${esc(specPath)}</code></div>
        </div>`;
      }
    }

    if (!actionContent) return "";

    // Determine who action is required from
    const actionFrom = [];
    {
      const langs = p.languages || {};
      let needsServiceTeam = false;
      let needsReviewTeam = false;
      for (const k of Object.keys(langs)) {
        if (isLangExcluded(langs[k].exclusionStatus)) continue;
        const st = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
        const d = langs[k].prDetails;
        // Failed checks on open/draft PR or closed PR → service team
        if (st === "closed") {
          needsServiceTeam = true;
        } else if (!st.includes("merged") && d && d.failedChecks && d.failedChecks.length > 0) {
          needsServiceTeam = true;
        }
        // Open PR → review team
        if (st === "open") {
          needsReviewTeam = true;
        }
      }
      // Non-PR actions (spec not ready, SDK not generated) → service team
      if (step.status === "API Spec Not Available" || step.status === "API Spec In Progress" ||
          step.status === "SDK To Be Generated" || step.status === "SDK Generation Failed" ||
          step.status === "SDK Ready To Be Released") {
        needsServiceTeam = true;
      }
      if (needsServiceTeam) actionFrom.push(p.submittedBy ? `Service Team (${p.submittedBy})` : "Service Team");
      if (needsReviewTeam) actionFrom.push("SDK PR Reviewer");
    }
    const actionFromHTML = actionFrom.length
      ? `<div class="action-from-label">Action required from: <strong>${esc(actionFrom.join(" & "))}</strong></div>`
      : "";

    return `<div class="action-required-section">
      <h4>⚡ Action Required</h4>
      ${actionFromHTML}
      ${actionContent}
    </div>`;
  }

  // ── Release Plan Stage Progress Bar ──────────────────────────
  function computeStages(p) {
    const plane = classifyPlane(p);
    const isPrivatePreview = isPrivatePreviewPlan(p);
    const specPrUrl = (p.apiSpec && p.apiSpec.specPrUrl) || "";
    const apiReady = (p.apiReadiness || "").toLowerCase() === "completed";
    const langs = p.languages || {};
    const langKeys = Object.keys(langs);
    const activeLangs = langKeys.filter(k => !isLangExcluded(langs[k].exclusionStatus));
    // A language counts as "SDK generated" if it has a PR URL, or release is completed, or PR status is merged
    function isLangGenerated(k) {
      const l = langs[k];
      if (l.sdkPrUrl) return true;
      const rs = (l.releaseStatus || "").toLowerCase();
      if (rs.includes("completed") || rs.includes("released")) return true;
      const ps = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
      if (ps.includes("merged") || ps.includes("completed")) return true;
      return false;
    }
    const langsWithPr = activeLangs.filter(k => langs[k].sdkPrUrl);
    const langsGenerated = activeLangs.filter(isLangGenerated);

    const prStatuses = langsWithPr.map(k => {
      const l = langs[k];
      return (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
    });
    const allGenerated = activeLangs.length > 0 && langsGenerated.length === activeLangs.length;
    const anyCheckFailure = langsWithPr.some(k => {
      const d = langs[k].prDetails;
      return d && d.failedChecks && d.failedChecks.length > 0;
    });
    const allPrMerged = langsGenerated.length > 0 && activeLangs.every(k => {
      const l = langs[k];
      const ps = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
      const rs = (l.releaseStatus || "").toLowerCase();
      return ps.includes("merged") || ps.includes("completed") || rs.includes("completed") || rs.includes("released");
    });
    const releaseStatuses = activeLangs.map(k => (langs[k].releaseStatus || "").toLowerCase());
    const allReleased = activeLangs.length > 0 && releaseStatuses.every(s => s.includes("completed") || s.includes("released"));

    // Stages for both management and data plane
    // "done" | "current" | "upcoming" | "error"
    const stages = [];

    // Stage 1: Define TypeSpec & Create Spec PR
    const s1Done = !!specPrUrl;
    stages.push({ label: "Create Spec PR", status: s1Done ? "done" : "current", icon: "📝" });

    // Stage 2: API Spec Review
    const s2Done = s1Done && apiReady;
    const s2Current = s1Done && !apiReady;
    stages.push({ label: "API Spec Review", status: s2Done ? "done" : s2Current ? "current" : "upcoming", icon: "🔍" });

    // For private preview: Create Spec PR → API Spec Review → Merge Spec
    if (isPrivatePreview) {
      const specApproval = (p.specApprovalStatus || "").toLowerCase();
      const specApproved = s2Done || specApproval.includes("approved");
      // Stage 2 override: use spec approval status for private preview
      stages[1] = {
        label: "API Spec Review",
        status: (s1Done && specApproved) ? "done" : s1Done ? "current" : "upcoming",
        icon: "🔍"
      };
      // Stage 3: Merge Spec — completed when spec PR is merged
      const specMerged = apiReady; // apiReadiness "completed" means PR merged
      const s3ppDone = specApproved && specMerged;
      const s3ppCurrent = specApproved && !specMerged;
      stages.push({
        label: "Merge Spec",
        status: s3ppDone ? "done" : s3ppCurrent ? "current" : "upcoming",
        icon: "🔀"
      });
      return stages;
    }

    // Stage 3: Generate SDK
    const noFailures = !anyCheckFailure;
    const s3Done = s2Done && allGenerated && noFailures;
    const s3Error = s2Done && langsGenerated.length > 0 && anyCheckFailure;
    const s3Current = s2Done && !s3Done && !s3Error;
    stages.push({
      label: "Generate SDK",
      status: s3Done ? "done" : s3Error ? "error" : s3Current ? "current" : "upcoming",
      icon: "⚙️"
    });

    // Stage 4: SDK Review & Merge
    const s4Done = s3Done && allPrMerged;
    const s4Current = s3Done && !allPrMerged;
    stages.push({
      label: "SDK Review & Merge",
      status: s4Done ? "done" : s4Current ? "current" : "upcoming",
      icon: "✅"
    });

    // Stage 5: Release SDK — active if at least one SDK is merged but not all released
    const anyMerged = langsWithPr.some(k => {
      const s = (langs[k].sdkPrGitHubStatus || langs[k].prStatus || "").toLowerCase();
      return s.includes("merged") || s.includes("completed");
    });
    const s5Done = allReleased && activeLangs.length > 0;
    const s5Current = !s5Done && anyMerged && !allReleased;
    stages.push({
      label: "Release SDK",
      status: s5Done ? "done" : s5Current ? "current" : "upcoming",
      icon: "🚀"
    });

    return stages;
  }

  function stageBarHTML(p) {
    const stages = computeStages(p);
    if (!stages.length) return "";
    if (p.state === "Finished") {
      // Mark all stages as done for finished plans
      stages.forEach(s => { s.status = "done"; });
    }
    const items = stages.map((s, i) => {
      const cls = `stage-item stage-${s.status}`;
      const connector = i < stages.length - 1 ? '<div class="stage-connector"></div>' : "";
      return `<div class="${cls}">
        <div class="stage-circle">${s.icon}</div>
        <div class="stage-label">${s.label}</div>
      </div>${connector}`;
    }).join("");
    return `<div class="stage-bar">${items}</div>`;
  }

  /** Generates the expanded detail HTML for a release plan card (stages, metadata, language table). */
  function detailHTML(p, options) {
    const showPmAction = !!(options && options.showPmAction);
    const specPath = p.specProjectPath || p.typeSpecPath || "";
    const step = computeCurrentStep(p);
    let html = stageBarHTML(p);

    html += '<div class="detail-meta">';
    // Current step highlight (hide for completed/released)
    const detailTerminal = step.status === "Released" || step.status === "Completed";
    if (step.status && !detailTerminal) {
      html += `<div class="detail-row detail-step-highlight">
        <strong>Current stage:</strong> <span class="step-badge ${step.statusClass}">${esc(step.status)}</span>`;
      if (step.action) {
        if (!(showPmAction && p._pmAction)) {
          html += ` <strong>Action required from:</strong> <span class="action-badge">${esc(step.action)}</span>`;
        }
      }
      html += `</div>`;
    }
    if (specPath) html += `<div class="detail-row"><strong>Spec Project Path:</strong> ${esc(specPath)}</div>`;
    // Work item link — always show using work item id
    {
      const wiUrl = `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${p.id}`;
      const label = p.releasePlanId ? `#${esc(String(p.releasePlanId))}` : `WI ${esc(String(p.id))}`;
      html += `<div class="detail-row"><strong>Release Plan:</strong> <a href="${esc(wiUrl)}" target="_blank" rel="noopener">${label}</a> <span class="wi-warning">⚠️ Do not modify directly — use the <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">azsdk agent</a></span></div>`;
    }
    if (p.typeSpecPath && p.specProjectPath && p.typeSpecPath !== p.specProjectPath) {
      html += `<div class="detail-row" style="font-size:.8rem;color:#605e5c;"><em>DevOps TypeSpec Path: ${esc(p.typeSpecPath)}</em></div>`;
    }
    // Horizontal metadata row
    const metaItems = [];
    if (p.submittedBy) metaItems.push(`<span><strong>Submitted By:</strong> ${esc(p.submittedBy)}</span>`);
    if (p.releaseMonth) metaItems.push(`<span><strong>Release Month:</strong> ${esc(p.releaseMonth)}</span>`);
    if (p.releaseType) metaItems.push(`<span><strong>SDK Release Type:</strong> ${esc(p.releaseType)}</span>`);
    if (p.createdDate) metaItems.push(`<span><strong>Created On:</strong> ${shortDate(p.createdDate)}</span>`);
    if (p.lastActivity) metaItems.push(`<span><strong>Last Activity:</strong> ${shortDate(p.lastActivity)}</span>`);
    if (metaItems.length) html += `<div class="detail-meta-row">${metaItems.join("")}</div>`;
    if (showPmAction && p._pmAction) html += `<div class="pm-action"><strong>Possible PM action:</strong> ${p._pmAction}</div>`;
    html += "</div>";

    // Expandable Product Details section
    if (p.productName) {
      html += `<div class="detail-group product-collapsible">
        <h4 class="product-toggle" style="cursor:pointer;user-select:none;">
          <span class="product-caret">&#9654;</span> Product: ${esc(p.productName)}
        </h4>
        <div class="product-details" style="display:none;">`;
      if (p.serviceName) html += `<div class="detail-row"><strong>Service Name:</strong> ${esc(p.serviceName)}</div>`;
      if (p.productId) {
        const treeUrl = `https://microsoftservicetree.com/products/${encodeURIComponent(p.productId)}`;
        html += `<div class="detail-row"><strong>Product ID:</strong> <a href="${esc(treeUrl)}" target="_blank" rel="noopener">${esc(p.productId)}</a></div>`;
      }
      if (p.productLifecycle) html += `<div class="detail-row"><strong>Product Lifecycle:</strong> ${esc(p.productLifecycle)}</div>`;
      if (p.ownerPM) html += `<div class="detail-row"><strong>Owner / PM:</strong> ${esc(p.ownerPM)}</div>`;
      html += `</div></div>`;
    }

    // SDK Languages table (hide for private preview)
    {
      const isPrivPrev = isPrivatePreviewPlan(p);
      if (isPrivPrev) {
        html += `<div class="detail-group private-preview-notice"><p>SDKs are not generated or released for private preview release plans.</p></div>`;
      } else {
      const langs = p.languages || {};
      const langKeys = Object.keys(langs);
      if (langKeys.length) {
        // Determine if any SDK PR exists to decide expanded vs collapsed
        const hasAnyPr = langKeys.some(k => langs[k].sdkPrUrl && !isLangExcluded(langs[k].exclusionStatus));
        const sdkDisplay = hasAnyPr ? "" : "display:none;";
        const sdkCaret = hasAnyPr ? "&#9660;" : "&#9654;";
        html += `<div class="detail-group sdk-collapsible">
        <h4 class="sdk-toggle" style="cursor:pointer;user-select:none;">
          <span class="sdk-caret">${sdkCaret}</span> SDK Details
        </h4>
        <div class="sdk-details-content" style="${sdkDisplay}">
        <table class="sdk-table"><thead><tr>
          <th>Language</th><th>Package</th><th>SDK PR</th><th>PR Status</th>
          <th>APIView</th><th>Release Status</th><th>Version</th><th>Package Link</th><th>Action Required</th>
        </tr></thead><tbody>`;
        for (const lang of langKeys) {
          const l = langs[lang];
          const excluded = isLangExcluded(l.exclusionStatus);
          const exLabel = exclusionLabel(l.exclusionStatus);
          const rowClass = exLabel ? ` class="${exLabel.cls}"` : "";
          const prLink = l.sdkPrUrl
            ? `<a href="${esc(l.sdkPrUrl)}" target="_blank" rel="noopener">PR</a>`
            : "—";
          const prLabels = l.sdkPrUrl ? prDetailLabels(l) : "";
          let releaseDisplay = l.releaseStatus || "";
          if (exLabel) releaseDisplay = exLabel.text;

          // Determine display version: use releasedVersion when released, pkgVersion otherwise
          const isReleased = (l.releaseStatus || "").toLowerCase() === "released";
          const displayVersion = isReleased ? (l.releasedVersion || "") : (l.pkgVersion || "");

          // Package labels: namespace approval + new package + API review (version now in its own column)
          let pkgLabels = "";
          if (l.isNewPackage) {
            pkgLabels += '<span class="pr-label pr-label-new">New</span>';
            if (classifyPlane(p) !== "mgmt" && l.namespaceApproval && l.namespaceApproval.toLowerCase() !== "approved") {
              pkgLabels += `<span class="pr-label pr-label-ns-pending" title="Namespace: ${esc(l.namespaceApproval)}">${esc(l.namespaceApproval)}</span>`;
            }
          }
          if (l.apiReviewStatus && l.apiReviewStatus.toLowerCase() !== "pending") {
            const arLower = l.apiReviewStatus.toLowerCase();
            const arClass = arLower === "approved" ? "pr-label-approved" : "pr-label-api-pending";
            pkgLabels += `<span class="pr-label ${arClass}">API: ${esc(l.apiReviewStatus)}</span>`;
          }

          // APIView column — loaded on-demand when card is expanded
          let apiViewCell = "—";
          if (l.sdkPrUrl) {
            if (l.prDetails && l.prDetails.apiViewUrl) {
              apiViewCell = `<a href="${esc(l.prDetails.apiViewUrl)}" target="_blank" rel="noopener">APIView</a>`;
            } else {
              apiViewCell = `<span class="apiview-placeholder">…</span>`;
            }
          }

          // Action column — determine per-language action
          let actionCell = "";
          if (!excluded && p.state !== "Finished") {
            const prSt = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
            const relSt = (l.releaseStatus || "").toLowerCase();
            const hasPr = !!l.sdkPrUrl;
            const isMerged = prSt.includes("merged") || prSt === "completed";
            const isDraft = prSt === "draft";
            const isOpen = prSt === "open" || isDraft;
            const isClosed = prSt === "closed";
            const hasFailedChecks = l.prDetails && l.prDetails.failedChecks && l.prDetails.failedChecks.length > 0;
            const isApproved = l.prDetails && l.prDetails.isApproved;
            const isMergeable = l.prDetails && l.prDetails.mergeable && l.prDetails.mergeableState === "clean";

            if (!hasPr) {
              actionCell = langActionBtn(ACTION_TYPES.GENERATE, lang, p, l);
            } else if (isClosed && !isMerged) {
              actionCell = langActionBtn(ACTION_TYPES.LINK_PR, lang, p, l);
            } else if (isDraft && !relSt.includes("released")) {
              actionCell = langActionBtn(ACTION_TYPES.MARK_READY, lang, p, l);
            } else if (isOpen && hasFailedChecks) {
              actionCell = langActionBtn(ACTION_TYPES.FIX_CHECKS, lang, p, l);
            } else if (isMerged && !relSt.includes("released")) {
              actionCell = langActionBtn(ACTION_TYPES.RELEASE, lang, p, l);
            } else if (isOpen && isApproved && isMergeable) {
              actionCell = langActionBtn(ACTION_TYPES.MERGE, lang, p, l);
            }
          }

          // Package feed link with icon and label — only show when released
          const feedUrl = isReleased ? getPackageFeedUrl(lang, l.packageName, displayVersion, p) : "";
          let feedLinkCell = "—";
          if (feedUrl) {
            const feedInfo = getPackageFeedInfo(lang);
            feedLinkCell = `<a href="${esc(feedUrl)}" target="_blank" rel="noopener" title="View on ${feedInfo.name}">${feedInfo.icon} ${feedInfo.name}</a>`;
          }

          html += `<tr${rowClass}>
            <td><strong>${esc(lang)}</strong></td>
            <td>${esc(l.packageName) || "—"} ${pkgLabels}</td>
            <td>${prLink} ${prLabels}</td>
            <td>${l.sdkPrUrl ? statusSpan(l.sdkPrGitHubStatus || l.prStatus) : ""}</td>
            <td class="apiview-cell">${apiViewCell}</td>
            <td>${statusSpan(releaseDisplay)}</td>
            <td>${displayVersion ? esc(displayVersion) : (isReleased ? '<span class="version-na">Not available</span>' : "—")}</td>
            <td>${feedLinkCell}</td>
            <td class="action-cell">${actionCell}</td>
          </tr>`;
        }
        html += "</tbody></table></div>";
        // Previous SDK PRs placeholder (loaded on-demand when card is expanded)
        html += `<div class="previous-sdk-prs" data-plan-id="${p.id}" style="margin-top:6px;"></div>`;
        html += "</div>";
      }
      } // end else (not private preview)
    }

    // API Spec + Spec Approval — horizontal row
    {
      const specItems = [];
      if (p.apiSpec) {
        const s = p.apiSpec;
        if (s.specPrUrl) specItems.push(`<span><strong>Spec PR:</strong> <a href="${esc(s.specPrUrl)}" target="_blank" rel="noopener">PR</a></span>`);
        if (s.apiVersion) specItems.push(`<span><strong>API Version:</strong> ${esc(s.apiVersion)}</span>`);
      }
      if (p.apiReadiness && p.apiReadiness !== "unknown") {
        const approvalLabel = p.apiReadiness === "completed" ? "Approved (PR Merged)" : "Pending (PR Open)";
        const approvalCls = p.apiReadiness === "completed" ? "status-completed" : "status-inprogress";
        specItems.push(`<span><strong>Spec Approval:</strong> <span class="${approvalCls}">${approvalLabel}</span></span>`);
      } else if (p.specApprovalStatus) {
        specItems.push(`<span><strong>Spec Approval:</strong> ${statusSpan(p.specApprovalStatus)}</span>`);
      }
      if (specItems.length) {
        html += `<div class="detail-meta-row">${specItems.join("")}</div>`;
      }
      // Spec PR labels — shown on a separate wrapping row
      if (p.specPrLabels && p.specPrLabels.length) {
        const labelPills = p.specPrLabels.map(label => {
          const bg = label.color ? `#${esc(label.color)}` : "#e1e4e8";
          const textColor = label.color ? contrastTextColor(label.color) : "#24292e";
          return `<span class="spec-pr-label" style="background:${bg};color:${textColor}">${esc(label.name)}</span>`;
        }).join(" ");
        html += `<div class="detail-meta-row spec-pr-labels-row"><strong>Spec PR Labels:</strong> ${labelPills}</div>`;
      }
      // Previous spec PRs (collapsible, separate line)
      if (p.apiSpec && p.apiSpec.previousSpecPrUrls && p.apiSpec.previousSpecPrUrls.length) {
        html += `<div class="detail-row"><details class="previous-prs-details"><summary style="cursor:pointer;font-size:.85rem;color:#605e5c;">Previous Spec PRs (${p.apiSpec.previousSpecPrUrls.length})</summary><ul style="margin:4px 0;padding-left:20px;">`;
        for (const u of p.apiSpec.previousSpecPrUrls) {
          html += `<li><a href="${esc(u)}" target="_blank" rel="noopener">${esc(u)}</a></li>`;
        }
        html += `</ul></details></div>`;
      }
    }

    // Action Required guidance (hidden in PM tab view, replaced by PM action)
    if (!(showPmAction && p._pmAction)) {
      html += actionRequiredHTML(p);
    }

    // Link a different SDK PR(standalone section, outside action required)
    if (p.releasePlanId && p.state !== "Finished") {
      const planId = p.releasePlanId || "";
      html += `<div class="action-note">
        <strong>💡 Link a different SDK PR:</strong> Use the <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Azure SDK Tools agent</a> in Copilot CLI or VS Code and run:
        <div class="action-prompt"><code>Link SDK pull request &lt;PR link&gt; to release plan ${esc(planId)}</code></div>
      </div>`;
    }

    // azsdk agent documentation link
    html += `<div class="detail-agent-link">📘 <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">How to use Azure SDK Tools agent to manage release plan and SDK?</a></div>`;

    return html;
  }

  // ── Lazy load PR details on card expand ─────────────────────
  async function lazyLoadPrDetails(detailsEl, cardEl) {
    const prLinks = detailsEl.querySelectorAll("td a[href*='github.com']");
    const urls = [...new Set([...prLinks].map(a => a.href).filter(Boolean))];
    if (!urls.length) return;

    try {
      const res = await fetch("/api/pr-details", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ urls }),
      });
      if (!res.ok) return;
      const data = await res.json();
      const details = data.details || {};
      const planId = cardEl && parseInt(cardEl.dataset.planId, 10);
      const plan = planId && getPlans().find(p => p.id === planId);

      for (const link of prLinks) {
        const info = details[link.href];
        if (!info) continue;
        const row = link.closest("tr");
        if (!row) continue;

        if (info.gitHubStatus) {
          const statusTd = row.children[3];
          if (statusTd) statusTd.innerHTML = statusSpan(info.gitHubStatus);
          if (plan) {
            const langName = row.children[0] && row.children[0].textContent.trim();
            const langData = langName && plan.languages && plan.languages[langName];
            if (langData) langData.sdkPrGitHubStatus = info.gitHubStatus;
          }
        }

        if (info.prDetails) {
          const d = info.prDetails;
          if (plan) {
            const langName = row.children[0] && row.children[0].textContent.trim();
            const langData = langName && plan.languages && plan.languages[langName];
            if (langData) langData.prDetails = d;
          }
          let labels = "";
          const lazyMerged = (info.gitHubStatus || "").toLowerCase().includes("merged");
          const lazyClosed = (info.gitHubStatus || "").toLowerCase() === "closed";
          const lazyOpenOrDraft = !lazyMerged && !lazyClosed;
          if (lazyOpenOrDraft && d.failedChecks && d.failedChecks.length) {
            const checksUrl = link.href ? link.href.replace(/\/$/, "") + "/checks" : "";
            const checksLink = checksUrl ? ` <a href="${esc(checksUrl)}" target="_blank" rel="noopener" style="font-size:.75rem;">View checks</a>` : "";
            labels += `<span class="pr-label pr-label-failed">${d.failedChecks.length} check(s) failed${checksLink}</span>`;
          }
          if (!lazyMerged && d.isApproved && d.approvedBy && d.approvedBy.length) {
            const tipText = "Approved by: " + esc(d.approvedBy.join(", "));
            labels += `<span class="pr-label pr-label-approved" title="${tipText}">Approved</span>`;
          }
          if (lazyOpenOrDraft && d.mergeable && d.mergeableState === "clean") {
            labels += '<span class="pr-label pr-label-mergeable">Ready to merge</span>';
          }
          if (labels) {
            const prTd = link.closest("td");
            if (prTd) {
              // Remove any existing pr-labels to avoid duplicates (initial render may have added them)
              prTd.querySelectorAll(".pr-label").forEach(el => el.remove());
              prTd.insertAdjacentHTML("beforeend", " " + labels);
            }
          }
        }

        const apiViewTd = row.children[4];
        if (apiViewTd && apiViewTd.classList.contains("apiview-cell")) {
          const apiViewUrl = info.prDetails && info.prDetails.apiViewUrl;
          apiViewTd.innerHTML = apiViewUrl
            ? `<a href="${esc(apiViewUrl)}" target="_blank" rel="noopener">APIView</a>`
            : "Not available";
        }
      }

      if (plan && cardEl) {
        const step = computeCurrentStep(plan);
        cardEl.querySelectorAll(".card-meta .step-badge").forEach(el => {
          el.className = `step-badge ${step.statusClass}`;
          el.textContent = step.status;
        });
        cardEl.querySelectorAll(".card-meta .action-badge").forEach(el => {
          el.textContent = step.action || "";
          el.style.display = step.action ? "" : "none";
        });
        detailsEl.querySelectorAll(".detail-step-highlight .step-badge").forEach(el => {
          el.className = `step-badge ${step.statusClass}`;
          el.textContent = step.status;
        });
        detailsEl.querySelectorAll(".detail-step-highlight .action-badge").forEach(el => {
          el.textContent = step.action || "";
          el.style.display = step.action ? "" : "none";
        });

        const oldAction = detailsEl.querySelector(".action-required-section");
        const isInPmTab = currentUserIsPM && !!(cardEl && cardEl.closest("#tab-pm-view"));
        if (isInPmTab && plan._pmAction) {
          if (oldAction) oldAction.remove();
        } else {
          const newActionHTML = actionRequiredHTML(plan);
          if (oldAction) {
            if (newActionHTML) oldAction.outerHTML = newActionHTML;
            else oldAction.remove();
          } else if (newActionHTML) {
            const insertBefore = detailsEl.querySelector(".action-note") || detailsEl.querySelector(".detail-agent-link");
            if (insertBefore) insertBefore.insertAdjacentHTML("beforebegin", newActionHTML);
            else detailsEl.insertAdjacentHTML("beforeend", newActionHTML);
          }
        }
      }
    } catch (err) {
      console.warn("Failed to load PR details:", err);
    }
  }

  // ── Lazy load previous SDK PRs from work item history ──────
  async function lazyLoadPreviousSdkPrs(detailsEl, planId) {
    const container = detailsEl.querySelector(`.previous-sdk-prs[data-plan-id="${planId}"]`);
    if (!container || container.dataset.loaded) return;
    try {
      const res = await fetch(`/api/previous-sdk-prs/${planId}`);
      if (!res.ok) return;
      container.dataset.loaded = "1"; // mark loaded only on success
      const data = await res.json();
      const prev = data.previousPrs || {};
      const hasAny = Object.values(prev).some(arr => arr.length > 0);
      if (!hasAny) return;
      let html = `<details class="previous-prs-details"><summary style="cursor:pointer;font-size:.85rem;color:#605e5c;">📂 Previous SDK Pull Requests</summary><div style="padding:4px 0 4px 8px;">`;
      for (const [lang, urls] of Object.entries(prev)) {
        if (!urls.length) continue;
        html += `<div style="margin-bottom:6px;"><strong>${esc(lang)}:</strong><ul style="margin:2px 0;padding-left:20px;">`;
        for (const u of urls) {
          html += `<li><a href="${esc(u)}" target="_blank" rel="noopener">${esc(u)}</a></li>`;
        }
        html += `</ul></div>`;
      }
      html += `</div></details>`;
      container.innerHTML = html;
    } catch (err) {
      console.warn("Failed to load previous SDK PRs:", err);
    }
  }

  // ── Helpers ─────────────────────────────────────────────────
  function badgeFor(state, plan) {
    if (plan && isPartiallyReleased(plan)) return "badge-partial";
    if (state === "In Progress") return "badge-inprogress";
    if (state === "Finished") return "badge-finished";
    return "badge-new";
  }

  function statusSpan(val) {
    if (!val) return "—";
    const lower = val.toLowerCase().replace(/\s+/g, "");
    let cls = "";
    if (
      lower.includes("completed") ||
      lower.includes("released") ||
      lower.includes("approved") ||
      lower.includes("merged")
    )
      cls = "status-completed";
    else if (
      lower.includes("inprogress") ||
      lower.includes("pending") ||
      lower.includes("active")
    )
      cls = "status-inprogress";
    else if (
      lower.includes("failed") ||
      lower.includes("excluded") ||
      lower.includes("blocked")
    )
      cls = "status-failed";
    else if (lower.includes("notstarted")) cls = "status-notstarted";
    return `<span class="${cls}">${esc(val)}</span>`;
  }

  function esc(s) {
    if (!s) return "";
    const el = document.createElement("span");
    el.textContent = s;
    return el.innerHTML;
  }

  /** Returns "#000" or "#fff" for readable text on a given hex background color. */
  function contrastTextColor(hexColor) {
    const hex = hexColor.replace(/^#/, "");
    if (hex.length < 6) return "#000";
    const r = parseInt(hex.substring(0, 2), 16);
    const g = parseInt(hex.substring(2, 4), 16);
    const b = parseInt(hex.substring(4, 6), 16);
    // W3C perceived brightness formula
    const luminance = (r * 299 + g * 587 + b * 114) / 1000;
    return luminance > 128 ? "#000" : "#fff";
  }

  function showLoading(show, message) {
    store().loading = show;
    if (message) store().loadingMessage = message;
  }

  function hideLoading() {
    store().loading = false;
    store().loadingMessage = "Loading release plans\u2026";
  }

  function showError(msg) {
    store().error = msg;
  }

  function hideError() {
    store().error = '';
  }

  function updateTimestamp(iso) {
    if (!iso) return;
    const d = new Date(iso);
    $("#last-updated").textContent = "Last refreshed " + d.toLocaleTimeString();
  }

  // ── Countdown ───────────────────────────────────────────────
  function resetCountdown() {
    refreshCountdown = AUTO_REFRESH_INTERVAL;
    if (countdownTimer) clearInterval(countdownTimer);
    countdownTimer = setInterval(() => {
      refreshCountdown--;
      if (refreshCountdown <= 0) {
        fetchPlans();
      }
    }, 1000);
  }

  // ── Section collapse (event delegation) ─────────────────────
  // Use a CSS class instead of the hidden attribute for reliability
  function toggleSection(header) {
    const targetId = header.getAttribute("data-target");
    if (!targetId) return;
    const target = document.getElementById(targetId);
    if (!target) return;
    const section = header.parentElement;
    const caret = header.querySelector(".caret");
    const ui = store().ui;
    const isCollapsed = !!ui.collapsedSections[targetId];
    if (isCollapsed) {
      target.style.display = "";
      target.removeAttribute("hidden");
      section.classList.remove("collapsed");
      if (caret) caret.innerHTML = "&#9660;";
      delete ui.collapsedSections[targetId];
    } else {
      target.style.display = "none";
      target.setAttribute("hidden", "");
      section.classList.add("collapsed");
      if (caret) caret.innerHTML = "&#9654;";
      ui.collapsedSections[targetId] = true;
    }
  }

  // ── Global event delegation ─────────────────────────────────
  // Single document-level handler replaces per-card listener attachment.
  // Each handler checks its closest matching ancestor and delegates.
  document.addEventListener("click", (e) => {
    // Ignore clicks on links
    if (e.target.closest("a")) return;

    // Section header collapse/expand
    const header = e.target.closest(".section-header");
    if (header) { toggleSection(header); return; }

    // Per-language action button → open modal with instructions
    const actionBtn = e.target.closest(".lang-action-btn");
    if (actionBtn) {
      e.stopPropagation();
      const { actionType, lang, planId, specPath, prUrl, pkg } = actionBtn.dataset;
      const { title, body } = buildActionPopupContent(actionType, lang, planId, specPath, prUrl, pkg);
      store().openModal(title, body);
      return;
    }

    // Share button → open share modal
    const shareBtn = e.target.closest(".plan-share-btn");
    if (shareBtn) {
      e.stopPropagation();
      const planId = shareBtn.dataset.planId;
      if (!planId) return;
      const shareUrl = `${window.location.origin}/?releasePlan=${encodeURIComponent(planId)}`;
      const body = `
        <p>Copy the link below to share this release plan:</p>
        <div class="share-link-row">
          <input type="text" class="share-link-input" value="${shareUrl.replace(/"/g, '&quot;')}" readonly onclick="this.select()" />
          <button class="share-copy-btn" title="Copy to clipboard" onclick="navigator.clipboard.writeText(this.previousElementSibling.value).then(() => { this.textContent = '✅'; setTimeout(() => { this.textContent = '📋'; }, 1500); })">📋</button>
        </div>`;
      store().openModal("Share Release Plan", body);
      return;
    }

    // Refresh button → reload single plan from server
    const refreshBtn = e.target.closest(".plan-refresh-btn");
    if (refreshBtn) {
      e.stopPropagation();
      handlePlanRefresh(refreshBtn);
      return;
    }

    // Product details toggle (collapsible product section inside card)
    const productToggle = e.target.closest(".product-toggle");
    if (productToggle) {
      e.stopPropagation();
      const card = productToggle.closest(".plan-card");
      const planId = card && card.dataset.planId;
      const details = productToggle.nextElementSibling;
      const caret = productToggle.querySelector(".product-caret");
      const ui = store().ui;
      if (planId && ui.expandedProduct[planId]) {
        details.style.display = "none";
        if (caret) caret.innerHTML = "&#9654;";
        delete ui.expandedProduct[planId];
      } else {
        details.style.display = "";
        if (caret) caret.innerHTML = "&#9660;";
        if (planId) ui.expandedProduct[planId] = true;
      }
      return;
    }

    // SDK details toggle (collapsible SDK table inside card)
    const sdkToggle = e.target.closest(".sdk-toggle");
    if (sdkToggle) {
      e.stopPropagation();
      const card = sdkToggle.closest(".plan-card");
      const planId = card && card.dataset.planId;
      const content = sdkToggle.nextElementSibling;
      const caret = sdkToggle.querySelector(".sdk-caret");
      const ui = store().ui;
      if (planId && ui.expandedSdk[planId]) {
        content.style.display = "none";
        if (caret) caret.innerHTML = "&#9654;";
        delete ui.expandedSdk[planId];
      } else {
        content.style.display = "";
        if (caret) caret.innerHTML = "&#9660;";
        if (planId) ui.expandedSdk[planId] = true;
      }
      return;
    }

    // Release plan card expand/collapse (lazy-loads PR details on first open)
    const cardSummary = e.target.closest(".card-summary");
    if (cardSummary) {
      const card = cardSummary.closest(".plan-card");
      const planId = card && card.dataset.planId;
      const details = cardSummary.nextElementSibling;
      const ui = store().ui;
      const wasExpanded = planId && ui.expandedPlans[planId];
      if (wasExpanded) {
        details.classList.remove("open");
        cardSummary.classList.remove("expanded");
        if (planId) delete ui.expandedPlans[planId];
      } else {
        details.classList.add("open");
        cardSummary.classList.add("expanded");
        if (planId) ui.expandedPlans[planId] = true;
        if (planId && !ui.loadedDetails[planId]) {
          ui.loadedDetails[planId] = true;
          lazyLoadPrDetails(details, card);
          lazyLoadPreviousSdkPrs(details, planId);
        }
      }
      return;
    }

    // PR tab card expand/collapse (lazy-loads PR details on first open)
    const prSummary = e.target.closest(".pr-card-summary");
    if (prSummary) {
      const prCard = prSummary.closest(".pr-card");
      const prUrl = prCard && prCard.dataset.prUrl;
      const details = prSummary.nextElementSibling;
      const ui = store().ui;
      const wasExpanded = prUrl && ui.expandedPrs[prUrl];
      if (wasExpanded) {
        details.classList.remove("open");
        prSummary.classList.remove("expanded");
        if (prUrl) delete ui.expandedPrs[prUrl];
      } else {
        details.classList.add("open");
        prSummary.classList.add("expanded");
        if (prUrl) ui.expandedPrs[prUrl] = true;
        if (!prSummary.dataset.prLoaded) {
          prSummary.dataset.prLoaded = "1";
          lazyLoadPrCardDetails(prCard);
        }
      }
      return;
    }
  });

  // ── Search & Filter reactivity ───────────────────────────────
  // Alpine x-model updates the store; we watch for changes via Alpine effect
  // Register in alpine:init (fires after our store is created in head script)
  let searchTimeout = null;
  let effectInitialized = false;
  document.addEventListener('alpine:init', () => {
    if (effectInitialized) return;
    effectInitialized = true;
    Alpine.effect(() => {
      // Access reactive properties to register dependency
      const _search = store().filters.search;
      const _plane = store().filters.plane;
      const _month = store().filters.month;
      const _prLang = store().filters.prLang;
      const _prStatus = store().filters.prStatus;
      // Skip re-render if plans not loaded yet
      if (!getPlans().length) return;
      // Debounce render to avoid excessive re-renders
      clearTimeout(searchTimeout);
      searchTimeout = setTimeout(() => {
        render(getPlans());
        if (currentUserIsPM) renderPMView(getPlans());
        renderFilteredPRs();
        syncFiltersToUrl();
        // If filter looks like a plan ID and no results found locally, try server lookup
        const filter = store().filters.search.trim();
        if (/^\d+$/.test(filter)) {
          const found = getPlans().some(p => String(p.releasePlanId) === filter || String(p.id) === filter);
          if (!found) {
            fetch(`/api/release-plans?releasePlan=${encodeURIComponent(filter)}`)
              .then(res => res.ok ? res.json() : null)
              .then(data => {
                if (data && data.plans && data.plans.length) {
                  const plans = getPlans();
                  for (const plan of data.plans) {
                    if (!plans.some(p => p.id === plan.id)) plans.push(plan);
                  }
                  render(getPlans());
                  progressiveLoadPRStatuses(getPlans());
                }
              })
              .catch(() => {});
          }
        }
      }, DEBOUNCE_DELAY_MS);
    });
  });

  // ══════════════════════════════════════════════════════════════
  // ── PM View Tab ────────────────────────────────────────────────
  // ══════════════════════════════════════════════════════════════
  const TIER1_DATA = [".net", "java", "javascript", "python"];
  const TIER1_MGMT = [".net", "java", "javascript", "python", "go"];

  function isMissingTier1(p) {
    if (p.state === "Finished") return false;
    if (isPrivatePreviewPlan(p)) return false;
    const langs = p.languages || {};
    const plane = classifyPlane(p);
    // For dataplane, only flag tier 1 missing for GA release plans (skip preview)
    if (plane === "data") {
      const rpt = (p.releasePlanType || "").toLowerCase();
      if (!rpt.includes("ga")) return false;
    }
    // Skip beta SDK release types
    const sdkType = (p.releaseType || "").toLowerCase();
    if (sdkType.includes("beta")) return false;
    const tier1 = plane === "mgmt" ? TIER1_MGMT : TIER1_DATA;

    // Only flag tier 1 missing if at least one language already has SDK generated
    const anyGenerated = Object.keys(langs).some(k => {
      if (isLangExcluded(langs[k].exclusionStatus)) return false;
      const l = langs[k];
      if (l.sdkPrUrl) return true;
      const rs = (l.releaseStatus || "").toLowerCase();
      if (rs.includes("completed") || rs.includes("released")) return true;
      const ps = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
      if (ps.includes("merged") || ps.includes("completed")) return true;
      return false;
    });
    if (!anyGenerated) return false;

    const missing = [];
    for (const t1 of tier1) {
      const key = Object.keys(langs).find(k => k.toLowerCase() === t1);
      if (!key) { missing.push(t1); continue; }
      const l = langs[key];
      if (isLangExcluded(l.exclusionStatus)) continue; // excluded is fine
      if (!l.sdkPrUrl) missing.push(t1);
    }
    return missing.length > 0 ? missing : false;
  }

  function isInactive(p) {
    if (p.state === "Finished") return false;
    if (!p.lastActivity) return false;
    const actDate = new Date(p.lastActivity);
    if (isNaN(actDate.getTime())) return false;
    const threeMonthsAgo = new Date();
    threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
    return actDate < threeMonthsAgo;
  }

  function renderPMView(plans) {
    // Server-verified PM check — prevents rendering even if tab is unhidden via DevTools
    if (!currentUserIsPM) return;

    const planeFilter = getGlobalPlaneFilter();
    const monthFilter = getMonthFilter();
    let filtered = planeFilter ? plans.filter(p => classifyPlane(p) === planeFilter) : plans;
    if (monthFilter) filtered = filtered.filter(p => (p.releaseMonth || "").toLowerCase() === monthFilter.toLowerCase());

    const inactive = [];
    const tier1Missing = [];
    const partial = [];
    const approaching = [];
    const pastDue = [];
    const recentlyFinished = [];

    const now = new Date();
    const thisMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    const nextMonth = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const endOfNextMonth = new Date(now.getFullYear(), now.getMonth() + 2, 0); // last day of next month
    const twoMonthsAgo = new Date();
    twoMonthsAgo.setMonth(twoMonthsAgo.getMonth() - 2);

    for (const p of filtered) {
      delete p._pmAction; // reset

      // Recently finished (last 2 months)
      if (p.state === "Finished") {
        const changed = new Date(p.changedDate);
        if (!isNaN(changed.getTime()) && changed >= twoMonthsAgo) {
          recentlyFinished.push(p);
        }
        continue;
      }

      // Approaching SDK release target (current month or next month, not yet finished)
      if (p.releaseMonth) {
        const target = parseReleaseMonth(p.releaseMonth);
        if (target.getFullYear() !== 9999 && target >= thisMonth && target <= endOfNextMonth) {
          p._pmAction = `SDK release target is <strong>${esc(p.releaseMonth)}</strong>. Ensure SDK generation and release are on track.`;
          approaching.push(p);
        }
      }

      // Past due — release month is before current month and plan not finished
      if (isPastDue(p)) {
        p._pmAction = `SDK release target was <strong>${esc(p.releaseMonth)}</strong>. This release plan is past due — follow up with the service team.`;
        pastDue.push(p);
      }

      const isPartial = isPartiallyReleased(p);

      if (isInactive(p) && !isPartial) {
        const planId = p.releasePlanId || p.id;
        const hasSpecPr = p.apiSpec && p.apiSpec.specPrUrl;
        const abandonPrompt = `Abandon release plan ${esc(String(planId))}`;
        let actionHtml = hasSpecPr
          ? "This release plan has been inactive for over 3 months."
          : "This release plan has no spec PR and has been inactive for over 3 months.";
        actionHtml += `<div class="pm-action-steps">
          <strong>To abandon this release plan:</strong>
          <ol>
            <li>Clone an Azure SDK or <a href="https://github.com/Azure/azure-rest-api-specs" target="_blank" rel="noopener">azure-rest-api-specs</a> repo and open Copilot CLI or VS Code from the repo root.</li>
            <li>Copy and run the following prompt:<br><code class="action-prompt-inline">${abandonPrompt}</code></li>
          </ol>
        </div>`;
        p._pmAction = actionHtml;
        inactive.push(p);
      }
      const missing = isMissingTier1(p);
      if (missing) {
        p._missingTier1 = missing;
        const wiUrl = `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${p.id}`;
        p._pmAction = `SDK PRs are missing for tier 1 languages: <strong>${missing.join(", ")}</strong>. Reach out to service team to confirm if SDK generation is pending.
        <div class="pm-action-steps">
          <strong>To exclude a language from this release plan:</strong>
          <ol>
            <li>Open the <a href="${esc(wiUrl)}" target="_blank" rel="noopener">DevOps work item</a> for this release plan and go to the SDK Details tab.</li>
            <li>Identify the language and update its Release Exclusion Status to <strong>Approved</strong>.</li>
          </ol>
        </div>`;
        tier1Missing.push(p);
      }
      if (isPartial && !missing) {
        p._pmAction = "Reach out to service team and confirm the plan to release remaining languages.";
        partial.push(p);
      }
    }

    pastDue.sort((a, b) => parseReleaseMonth(a.releaseMonth) - parseReleaseMonth(b.releaseMonth));

    renderPMSection("list-pm-approaching", approaching);
    renderPMSection("list-pm-pastdue", pastDue);
    renderPMSection("list-pm-inactive", inactive);
    renderPMSection("list-pm-tier1", tier1Missing);
    renderPMSection("list-pm-partial", partial);
    renderPMSection("list-pm-finished", recentlyFinished);

    // Update section counts
    const sections = [
      { id: "section-pm-approaching", count: approaching.length },
      { id: "section-pm-pastdue", count: pastDue.length },
      { id: "section-pm-inactive", count: inactive.length },
      { id: "section-pm-tier1", count: tier1Missing.length },
      { id: "section-pm-partial", count: partial.length },
      { id: "section-pm-finished", count: recentlyFinished.length },
    ];
    for (const s of sections) {
      const sec = document.getElementById(s.id);
      if (sec) {
        const badge = sec.querySelector(".section-count");
        if (badge) badge.textContent = `(${s.count})`;
      }
    }
  }

  function renderPMSection(listId, plans) {
    const el = document.getElementById(listId);
    if (!el) return;
    if (!plans.length) {
      el.innerHTML = '<div class="empty-msg">None found ✓</div>';
      return;
    }
    el.innerHTML = plans.map(p => cardHTML(p, { showPmAction: true })).join("");
  }

  // ══════════════════════════════════════════════════════════════
  // ── Tab switching (handled by Alpine store) ───────────────────
  // ══════════════════════════════════════════════════════════════

  // ══════════════════════════════════════════════════════════════
  // ── SDK Pull Requests Tab ─────────────────────────────────────
  // ══════════════════════════════════════════════════════════════
  let prExpandedUrls = new Set();
  let prLoadGeneration = 0; // increments on each load to cancel stale runs

  function langBadgeClass(lang) {
    const l = lang.toLowerCase().replace(/\./g, "");
    if (l === "net") return "lang-badge-dotnet";
    if (l === "java") return "lang-badge-java";
    if (l === "javascript") return "lang-badge-javascript";
    if (l === "python") return "lang-badge-python";
    if (l === "go") return "lang-badge-go";
    return "";
  }

  // Extract candidate PRs without filtering by GitHub status (status fetched progressively).
  function extractCandidatePRs(plans) {
    const seen = new Set();
    const prs = [];
    for (const p of plans) {
      if (!p.languages) continue;
      if (p.state === "Finished") continue;
      if (isPrivatePreviewPlan(p)) continue;
      for (const [lang, l] of Object.entries(p.languages)) {
        if (isLangExcluded(l.exclusionStatus)) continue;
        if (!l.sdkPrUrl) continue;
        const relSt = (l.releaseStatus || "").toLowerCase();
        if (relSt.includes("released") || relSt === "completed") continue;
        // Skip if DevOps status already indicates closed/merged/completed
        const devopsSt = (l.prStatus || "").toLowerCase();
        if (devopsSt === "closed" || devopsSt.includes("merged") || devopsSt === "completed") continue;
        const dedupeKey = `${l.sdkPrUrl}|${lang}`;
        if (seen.has(dedupeKey)) continue;
        seen.add(dedupeKey);
        const prMatch = l.sdkPrUrl.match(/github\.com\/([^/]+\/[^/]+)\/pull\/(\d+)/);
        const repo = prMatch ? prMatch[1] : "";
        const prNumber = prMatch ? prMatch[2] : "";
        prs.push({
          prUrl: l.sdkPrUrl, language: lang, packageName: l.packageName || "",
          releasePlanId: p.releasePlanId || "", planTitle: p.title || "", planId: p.id,
          plane: classifyPlane(p), submittedBy: p.submittedBy || "",
          releaseMonth: p.releaseMonth || "", releaseMonthDate: parseReleaseMonth(p.releaseMonth),
          prStatus: l.sdkPrGitHubStatus || l.prStatus || "", releaseStatus: l.releaseStatus || "",
          repo, prNumber, prDetails: l.prDetails || null, _statusLoaded: false,
        });
      }
    }
    prs.sort((a, b) => {
      const d = a.releaseMonthDate - b.releaseMonthDate;
      if (d !== 0) return d;
      return (a.releasePlanId || "").localeCompare(b.releasePlanId || "");
    });
    return prs;
  }

  // Progressively fetch GitHub PR statuses and build the PR list.
  async function progressiveLoadPRStatuses(plans) {
    const gen = ++prLoadGeneration;
    const candidates = extractCandidatePRs(plans);
    setPrs([]);

    const prLoading = document.getElementById("pr-loading");
    const prList = document.getElementById("pr-list");
    if (prLoading) { prLoading.style.display = ""; prLoading.querySelector("p").textContent = `Fetching PR statuses (0/${candidates.length})…`; }
    if (prList) prList.innerHTML = "";

    // Collect unique PR URLs to fetch
    const uniqueUrls = [...new Set(candidates.map(c => c.prUrl))];
    const statusCache = new Map(); // url -> status string

    // Fetch in small batches of 10, throttle re-renders to every 500ms
    const BATCH = PR_STATUS_BATCH_SIZE;
    let fetched = 0;
    let lastRender = 0;
    let needsRender = false;
    for (let i = 0; i < uniqueUrls.length; i += BATCH) {
      if (gen !== prLoadGeneration) return; // cancelled
      const batch = uniqueUrls.slice(i, i + BATCH);
      try {
        const res = await fetch("/api/pr-statuses", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ urls: batch }),
        });
        if (res.ok) {
          const data = await res.json();
          const statuses = data.statuses || {};
          for (const [url, st] of Object.entries(statuses)) {
            if (st) statusCache.set(url, st);
          }
        }
      } catch { /* continue */ }
      fetched += batch.length;
      if (gen !== prLoadGeneration) return;

      // Update progress text
      if (prLoading) prLoading.querySelector("p").textContent = `Fetching PR statuses (${Math.min(fetched, uniqueUrls.length)}/${uniqueUrls.length})…`;

      // Process candidates that now have a status and add eligible ones
      for (const c of candidates) {
        if (c._statusLoaded) continue;
        const st = statusCache.get(c.prUrl);
        if (!st) continue;
        c._statusLoaded = true;
        c.prStatus = st;
        const stLower = st.toLowerCase();
        if (stLower === "open" || stLower === "draft") {
          getPrs().push(c);
          needsRender = true;
        }
      }
      // Throttle re-renders to at most every 500ms
      const now = Date.now();
      if (needsRender && (now - lastRender > RENDER_THROTTLE_MS)) {
        renderFilteredPRs();
        lastRender = now;
        needsRender = false;
      }
    }

    // Final pass: add any candidates whose URL wasn't fetched (network error) if they look open
    for (const c of candidates) {
      if (!c._statusLoaded) {
        const stLower = (c.prStatus || "").toLowerCase();
        if (stLower === "open" || stLower === "draft") {
          getPrs().push(c);
        }
      }
    }

    if (gen !== prLoadGeneration) return;
    if (prLoading) prLoading.style.display = "none";
    renderFilteredPRs();
  }



  function filterPRs(prs) {
    const langFilter = store().filters.prLang || "";
    const statusFilter = store().filters.prStatus || "";
    const textFilter = store().filters.search.toLowerCase();
    const planeFilter = getGlobalPlaneFilter();

    return prs.filter(pr => {
      if (planeFilter && pr.plane !== planeFilter) return false;
      if (langFilter && pr.language !== langFilter) return false;
      if (statusFilter) {
        const st = (pr.prStatus || "").toLowerCase();
        if (st !== statusFilter) return false;
      }
      if (textFilter) {
        const searchable = `${pr.language} ${pr.repo} ${pr.prNumber} ${pr.packageName} ${pr.planTitle} ${pr.releasePlanId} ${pr.prStatus}`.toLowerCase();
        if (!searchable.includes(textFilter)) return false;
      }
      return true;
    });
  }

  function prStatusBadge(status) {
    const st = (status || "").toLowerCase();
    if (st.includes("merged")) return '<span class="badge badge-finished">Merged</span>';
    if (st === "draft") return '<span class="badge badge-new">Draft</span>';
    if (st === "open") return '<span class="badge badge-inprogress">Open</span>';
    if (st === "closed") return '<span class="badge badge-pastdue">Closed</span>';
    return status ? `<span class="badge badge-new">${esc(status)}</span>` : "";
  }

  function prCardHTML(pr) {
    const displayName = pr.packageName
      ? `${esc(pr.packageName)}`
      : `${esc(pr.repo)}#${esc(pr.prNumber)}`;
    const wiUrl = `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${pr.planId}`;
    return `
    <div class="pr-card" data-pr-url="${esc(pr.prUrl)}">
      <div class="pr-card-summary">
        <span class="pr-card-chevron">&#9654;</span>
        <div class="pr-card-title">
          <span class="lang-badge ${langBadgeClass(pr.language)}">${esc(pr.language)}</span>
          ${displayName}
        </div>
        <div class="pr-card-meta">
          ${prStatusBadge(pr.prStatus)}
          ${pr.prDetails && pr.prDetails.isApproved ? '<span class="pr-label pr-label-approved">Approved</span>' : ""}
          ${pr.releaseMonth ? `<span>${esc(pr.releaseMonth)}</span>` : ""}
          <span class="si-plan"><a href="/?releasePlan=${esc(String(pr.releasePlanId))}" title="View release plan #${esc(String(pr.releasePlanId))}">#${esc(String(pr.releasePlanId))}</a></span>
        </div>
      </div>
      <div class="pr-card-details">
        <div class="pr-detail-loading">Loading PR details…</div>
      </div>
    </div>`;
  }

  function prDetailHTML(pr, info) {
    let html = "";
    const title = (info && info.prDetails && info.prDetails.title) || "";
    if (title) html += `<div class="pr-detail-row"><strong>Title:</strong> ${esc(title)}</div>`;
    html += `<div class="pr-detail-row"><strong>PR:</strong> <a href="${esc(pr.prUrl)}" target="_blank" rel="noopener">${esc(pr.repo)}#${esc(pr.prNumber)}</a></div>`;
    html += `<div class="pr-detail-row"><strong>Package:</strong> ${esc(pr.packageName) || "—"}</div>`;
    html += `<div class="pr-detail-row"><strong>Language:</strong> ${esc(pr.language)}</div>`;

    if (info && info.gitHubStatus) {
      html += `<div class="pr-detail-row"><strong>Status:</strong> ${statusSpan(info.gitHubStatus)}</div>`;
    }

    const wiUrl = `https://dev.azure.com/azure-sdk/Release/_workitems/edit/${pr.planId}`;
    html += `<div class="pr-detail-row"><strong>Release Plan:</strong> <a href="/?releasePlan=${esc(String(pr.releasePlanId))}">#${esc(String(pr.releasePlanId))}</a> — ${esc(pr.planTitle)} <a href="${esc(wiUrl)}" target="_blank" rel="noopener" style="font-size:.78rem;color:#605e5c;">(DevOps)</a></div>`;
    if (pr.releaseMonth) html += `<div class="pr-detail-row"><strong>Target Release:</strong> ${esc(pr.releaseMonth)}</div>`;
    if (pr.releaseStatus) html += `<div class="pr-detail-row"><strong>Release Status:</strong> ${statusSpan(pr.releaseStatus)}</div>`;

    if (info && info.prDetails) {
      const d = info.prDetails;
      const st = (info.gitHubStatus || "").toLowerCase();
      const prIsMerged = st.includes("merged");
      const prIsClosed = st === "closed";
      const prIsOpenOrDraft = !prIsMerged && !prIsClosed;
      // Failed checks — only for open/draft PRs
      if (prIsOpenOrDraft && d.failedChecks && d.failedChecks.length) {
        const checksUrl = pr.prUrl ? pr.prUrl.replace(/\/$/, "") + "/checks" : "";
        const checksLink = checksUrl ? ` <a href="${esc(checksUrl)}" target="_blank" rel="noopener">View checks →</a>` : "";
        html += `<div class="pr-detail-row" style="color:var(--red);"><strong>⚠️ ${d.failedChecks.length} check(s) failed</strong>${checksLink}</div>`;
      }
      // Approval
      if (!prIsMerged && d.isApproved && d.approvedBy && d.approvedBy.length) {
        html += `<div class="pr-detail-row"><strong>✅ Approved by:</strong> ${esc(d.approvedBy.join(", "))}</div>`;
      }
      // Reviewers
      if (!prIsMerged && d.requestedReviewers && d.requestedReviewers.length) {
        html += `<div class="pr-detail-row"><strong>Requested Reviewers:</strong> ${esc(d.requestedReviewers.join(", "))}</div>`;
      }
      // Mergeable
      if (prIsOpenOrDraft && d.mergeable && d.mergeableState === "clean") {
        html += `<div class="pr-detail-row"><strong>✅ Ready to merge</strong></div>`;
      }
      // APIView
      if (d.apiViewUrl) {
        html += `<div class="pr-detail-row"><strong>APIView:</strong> <a href="${esc(d.apiViewUrl)}" target="_blank" rel="noopener">View API Changes</a></div>`;
      }
      // Action required — context-specific guidance for the PR tab
      const serviceTeamLabel = pr.submittedBy ? `Service Team (${esc(pr.submittedBy)})` : "Service Team";
      if (!prIsMerged) {
        if (prIsOpenOrDraft && d.failedChecks && d.failedChecks.length) {
          html += `<div class="action-required-section" style="margin-top:10px;"><h4>⚡ Action Required</h4><div class="action-from-label">Action required from: <strong>${serviceTeamLabel}</strong></div><div class="action-item action-item-warning"><strong>Fix check failures:</strong> Clone the repo, checkout the PR, and use the <a href="https://aka.ms/azsdk/agent" target="_blank" rel="noopener">Azure SDK Tools agent</a> to resolve build errors.</div></div>`;
        } else if (prIsClosed) {
          html += `<div class="action-required-section" style="margin-top:10px;"><h4>⚡ Action Required</h4><div class="action-from-label">Action required from: <strong>${serviceTeamLabel}</strong></div><div class="action-item action-item-warning"><strong>PR Closed:</strong> Regenerate the SDK or link a different PR to the release plan.</div></div>`;
        } else if (st === "draft") {
          html += `<div class="action-required-section" style="margin-top:10px;"><h4>⚡ Action Required</h4><div class="action-from-label">Action required from: <strong>${serviceTeamLabel}</strong></div><div class="action-item"><strong>Mark as ready for review:</strong> This PR is in draft status. Mark it as ready for review when the SDK changes are complete.</div></div>`;
        } else if (d.isApproved && st === "open") {
          html += `<div class="action-required-section" style="margin-top:10px;"><h4>⚡ Action Required</h4><div class="action-from-label">Action required from: <strong>${serviceTeamLabel}</strong></div><div class="action-item"><strong>Merge the SDK pull request:</strong> This PR has been approved by the SDK team. <a href="${esc(pr.prUrl)}" target="_blank" rel="noopener">Open the PR on GitHub</a> and merge it.</div></div>`;
        }
      }
      // Latest comment
      if (d.latestComment) {
        const c = d.latestComment;
        const dateStr = c.createdAt ? new Date(c.createdAt).toLocaleString() : "";
        html += `<div class="pr-latest-comment"><strong>${esc(c.author)}</strong> <span style="color:#888;font-size:.75rem;">${esc(dateStr)}</span><br>${esc(c.body)}</div>`;
      }
    }

    return html;
  }

  function renderFilteredPRs() {
    // Remember expanded PR cards
    document.querySelectorAll(".pr-card-summary.expanded").forEach(el => {
      const card = el.closest(".pr-card");
      if (card && card.dataset.prUrl) prExpandedUrls.add(card.dataset.prUrl);
    });

    const allPrs = getPrs();
    const filtered = filterPRs(allPrs);
    store().prCount = `${filtered.length} of ${allPrs.length} PRs`;

    const container = document.getElementById("pr-list");
    if (!filtered.length) {
      container.innerHTML = '<p style="padding:8px;color:#605e5c;font-size:.88rem;">No pull requests found.</p>';
      return;
    }
    container.innerHTML = filtered.map(prCardHTML).join("");

    // Restore expanded cards (handlers are managed via document-level event delegation)
    if (prExpandedUrls.size) {
      container.querySelectorAll(".pr-card").forEach(card => {
        if (prExpandedUrls.has(card.dataset.prUrl)) {
          const summary = card.querySelector(".pr-card-summary");
          const details = card.querySelector(".pr-card-details");
          if (summary && details) {
            details.classList.add("open");
            summary.classList.add("expanded");
            if (!summary.dataset.prLoaded) {
              summary.dataset.prLoaded = "1";
              lazyLoadPrCardDetails(card);
            }
          }
        }
      });
    }
  }

  async function lazyLoadPrCardDetails(cardEl) {
    const prUrl = cardEl.dataset.prUrl;
    if (!prUrl) return;
    const detailsEl = cardEl.querySelector(".pr-card-details");
    if (!detailsEl) return;

    const pr = getPrs().find(p => p.prUrl === prUrl);
    if (!pr) return;

    // If prDetails already loaded (from a previous expand), render directly
    if (pr.prDetails) {
      const info = { gitHubStatus: pr.prStatus, prDetails: pr.prDetails };
      detailsEl.innerHTML = prDetailHTML(pr, info);
      return;
    }

    try {
      const res = await fetch("/api/pr-details", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ urls: [prUrl] }),
      });
      if (!res.ok) {
        detailsEl.innerHTML = '<div class="pr-detail-loading" style="color:var(--red);">Failed to load details.</div>';
        return;
      }
      const data = await res.json();
      const info = (data.details || {})[prUrl];
      if (info) {
        if (info.gitHubStatus) pr.prStatus = info.gitHubStatus;
        if (info.prDetails) pr.prDetails = info.prDetails;
      }
      detailsEl.innerHTML = prDetailHTML(pr, info);

      // Update card summary badges with newly loaded data
      const summaryEl = cardEl.querySelector(".pr-card-summary");
      if (summaryEl) {
        const metaEl = summaryEl.querySelector(".pr-card-meta");
        if (metaEl) {
          // Update status badge
          const existingBadge = metaEl.querySelector(".badge");
          if (existingBadge && info && info.gitHubStatus) {
            existingBadge.outerHTML = prStatusBadge(info.gitHubStatus);
          }
          // Add or remove "Approved" label
          const existingApproved = metaEl.querySelector(".pr-label-approved");
          if (pr.prDetails && pr.prDetails.isApproved && !existingApproved) {
            const statusEl = metaEl.querySelector(".badge");
            if (statusEl) statusEl.insertAdjacentHTML("afterend", ' <span class="pr-label pr-label-approved">Approved</span>');
          } else if (existingApproved && !(pr.prDetails && pr.prDetails.isApproved)) {
            existingApproved.remove();
          }
        }
      }
    } catch (err) {
      console.warn("Failed to load PR card details:", err);
      detailsEl.innerHTML = '<div class="pr-detail-loading" style="color:var(--red);">Error loading details.</div>';
    }
  }

  // ── Expose functions for Alpine templates ──────────────────
  // These are called from x-html / x-bind in index.html templates
  window.dashHelpers = {
    cardHTML,
    detailHTML,
    prCardHTML,
    prDetailHTML,
    computeCurrentStep,
    isPastDue,
    isPartiallyReleased,
    esc,
    contrastTextColor,
  };

  // ── Init ────────────────────────────────────────────────────
  fetchPlans();
})();
