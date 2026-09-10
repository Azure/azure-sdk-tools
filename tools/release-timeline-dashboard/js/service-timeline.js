/**
 * service-timeline.js
 * Service timeline renderer — multi-PR swim lanes per language, release windows,
 * window selector, contextual metrics, gap compaction for year-long timelines.
 */
const ServiceTimeline = (() => {
  let data = null;
  let timeRange = null;
  let contentWidth = 0;
  let zoomLevel = 1;
  const padding = { left: 20, right: 20 };
  let hiddenEventTypes = new Set();
  let hiddenActors = new Set();
  let allActors = [];
  let selectedWindow = null; // null = all-up, or a releaseWindows index
  let focusRange = null; // null = full timeline, or { start, end } for window focus
  let expandedLaneKeys = new Set();

  // Gap compaction — 7-day threshold for year-long timelines
  const GAP_THRESHOLD_MS = 7 * 86400000;
  const COMPRESSED_GAP_PX = 44;
  const MIN_SEGMENT_PX = 100;
  let segments = [];

  // High-signal event types for Smart View preset
  const SMART_VIEW_TYPES = new Set([
    'review_changes_requested', 'author_nag', 'manual_fix', 'idle_gap',
    'review_approved', 'pr_created', 'pr_merged', 'ready_for_review',
    'release_pipeline_completed', 'release_pipeline_failed', 'release_pending'
  ]);

  const EVENT_LAYOUT = {
    pr_created: { offset: -38, priority: 0 },
    ready_for_review: { offset: -28, priority: 1 },
    review_approved: { offset: -18, priority: 2 },
    pr_merged: { offset: -10, priority: 3 },
    pr_closed: { offset: -10, priority: 4 },
    release_pipeline_completed: { offset: -2, priority: 5 },
    review_changes_requested: { offset: 10, priority: 10 },
    release_pipeline_failed: { offset: 18, priority: 11 },
    release_pending: { offset: 18, priority: 12 },
    release_pipeline_started: { offset: 26, priority: 13 },
    manual_fix: { offset: 34, priority: 14 },
    author_nag: { offset: 42, priority: 15 },
    commit_pushed: { offset: 20, priority: 16 },
    review_comment: { offset: 28, priority: 17 },
    reviewer_responds: { offset: 28, priority: 18 },
    issue_comment: { offset: 36, priority: 19 },
    bot_comment: { offset: 44, priority: 20 },
    convert_to_draft: { offset: 44, priority: 21 },
    label_added: { offset: 52, priority: 22 },
    ci_status: { offset: 52, priority: 23 },
    tool_call: { offset: 52, priority: 24 }
  };

  /* ── Public ─────────────────────────────────────────────── */

  function render(timelineData) {
    data = timelineData;
    timeRange = { start: new Date(data.startDate), end: new Date(data.endDate) };
    hiddenActors.clear();
    selectedWindow = null;
    focusRange = null;
    zoomLevel = 1;
    expandedLaneKeys.clear();

    // Default to Smart View for service timelines (fewer markers = more readable)
    const allTypes = [
      'pr_created', 'pr_merged', 'ready_for_review', 'review_approved', 'review_changes_requested',
      'review_comment', 'issue_comment', 'author_nag', 'manual_fix',
      'commit_pushed', 'bot_comment', 'ci_status', 'idle_gap',
      'release_pipeline_started', 'release_pipeline_completed',
      'release_pipeline_failed', 'release_pending', 'tool_call'
    ];
    hiddenEventTypes.clear();
    allTypes.forEach(t => { if (!SMART_VIEW_TYPES.has(t)) hiddenEventTypes.add(t); });

    // Default to the latest API version window, or latest spec PR window
    const windows = data.releaseWindows || [];
    if (windows.length > 0) {
      // Prefer the latest API version window (label starts with "API ")
      let best = -1;
      for (let i = windows.length - 1; i >= 0; i--) {
        if (windows[i].label && windows[i].label.startsWith('API ')) { best = i; break; }
      }
      if (best === -1) best = windows.length - 1; // fallback: latest window
      selectedWindow = best;
      const win = windows[selectedWindow];
      const winStart = new Date(win.startDate).getTime();
      const winEnd = new Date(win.endDate).getTime();
      const padding_ms = (winEnd - winStart) * 0.15;
      focusRange = { start: new Date(winStart - padding_ms), end: new Date(winEnd + padding_ms) };
    }

    renderServiceHeader();
    renderWindowSelector();
    renderSummaryCards();
    renderFilters();
    renderTimeline();
    renderInsights();

    hide('header-info');
    show('service-header');
    show('service-window-selector');
    show('summary-cards');
    show('filters');
    show('timeline-container');
    show('insights-panel');
    hide('empty-state');

    // Scroll the pre-selected pill into view on initial load (delay to ensure layout)
    setTimeout(() => {
      const pillsEl = document.querySelector('.window-pills');
      const activePill = pillsEl && pillsEl.querySelector('.window-pill.active');
      if (pillsEl && activePill) {
        const pr = pillsEl.getBoundingClientRect();
        const ar = activePill.getBoundingClientRect();
        const pillLeftInScroll = ar.left - pr.left + pillsEl.scrollLeft;
        pillsEl.scrollLeft = pillLeftInScroll - (pr.width - ar.width) / 2;
      }
    }, 0);

    // Re-fit card grid now that summary-cards is visible (clientWidth is available)
    const cardsEl = document.getElementById('summary-cards');
    if (cardsEl) fitCardGrid(cardsEl, cardsEl.children.length);
  }

  /* ── Service Header ─────────────────────────────────────── */

  function renderServiceHeader() {
    const el = document.getElementById('service-header');
    if (!el) return;
    const specCount = data.specPRs.length;
    const sdkCount = Object.values(data.sdkPRs).reduce((s, a) => s + a.length, 0);
    const langs = Object.keys(data.sdkPRs).join(', ');
    const days = DataLoader.computeDurationDays(data.startDate, data.endDate);
    const generated = data.generatedAt
      ? new Date(data.generatedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
      : '—';

    const specPathLink = data.specPath
      ? `<a href="https://github.com/Azure/azure-rest-api-specs/tree/main/${encodeURI(data.specPath)}" target="_blank" rel="noopener" title="View in azure-rest-api-specs">📂 ${escapeHtml(data.specPath)}</a>`
      : '';

    el.innerHTML = `
      <h2>${escapeHtml(data.service)} — Full Service Timeline</h2>
      <div class="service-header-meta">
        ${specPathLink}
        <span>📅 ${DataLoader.formatDateRange(data.startDate, data.endDate)} (${Math.round(days)}d)</span>
        <span>📋 ${specCount} spec PRs · ${sdkCount} SDK PRs · ${data.releaseWindows?.length || 0} release windows</span>
        <span>🌐 ${langs}</span>
        <span class="generated-stamp" title="Data generated at ${data.generatedAt}">Data as of ${generated}</span>
      </div>
    `;
  }

  /* ── Window Selector ────────────────────────────────────── */

  function renderWindowSelector() {
    const el = document.getElementById('service-window-selector');
    if (!el) return;

    const windows = data.releaseWindows || [];
    el.innerHTML = '';

    const label = document.createElement('span');
    label.className = 'filters-label';
    label.textContent = '📦 Release Windows:';
    el.appendChild(label);

    const pills = document.createElement('div');
    pills.className = 'window-pills';

    // All-up pill
    const allPill = document.createElement('button');
    allPill.className = `window-pill ${selectedWindow === null ? 'active' : ''}`;
    const visibleSpec = data.specPRs.length;
    const visibleSdk = Object.values(data.sdkPRs).reduce((s, a) => s + a.length, 0);
    allPill.innerHTML = `<span class="pill-label">All</span><span class="pill-sub">${visibleSpec} spec · ${visibleSdk} SDK</span>`;
    allPill.addEventListener('click', () => selectWindow(null));
    pills.appendChild(allPill);

    for (let i = windows.length - 1; i >= 0; i--) {
      const w = windows[i];
      const sdkCount = Object.values(w.sdkPRNumbers || {}).flat().length;
      const specCount = (w.specPRNumbers || []).length;
      const isApiVersion = w.label && w.label.startsWith('API ');
      const pill = document.createElement('button');
      pill.className = `window-pill ${selectedWindow === i ? 'active' : ''} ${isApiVersion ? 'api-version' : ''}`;
      const sub = specCount > 1 ? `${specCount} specs · ${sdkCount} SDKs` : `${sdkCount} SDK PRs`;
      pill.innerHTML = `<span class="pill-label">${escapeHtml(w.label)}</span><span class="pill-sub">${sub}</span>`;
      pill.addEventListener('click', () => selectWindow(i));
      pills.appendChild(pill);
    }

    el.appendChild(pills);
  }

  function selectWindow(idx) {
    selectedWindow = idx;

    // Preserve pill container scroll position across re-render
    const pillsEl = document.querySelector('.window-pills');
    const savedPillScroll = pillsEl?.scrollLeft || 0;

    renderWindowSelector();

    // Restore pill scroll position (don't auto-center the active pill)
    const newPillsEl = document.querySelector('.window-pills');
    if (newPillsEl) newPillsEl.scrollLeft = savedPillScroll;

    renderSummaryCards();
    renderInsights();

    if (idx !== null) {
      // Focus mode: zoom to window time range
      const win = data.releaseWindows[idx];
      if (win) {
        const winStart = new Date(win.startDate).getTime();
        const winEnd = new Date(win.endDate).getTime();
        const padding_ms = (winEnd - winStart) * 0.15; // 15% padding on each side
        focusRange = {
          start: new Date(winStart - padding_ms),
          end: new Date(winEnd + padding_ms)
        };
      }
    } else {
      focusRange = null;
    }

    // Re-render timeline with new zoom/filter state
    renderTimeline();
  }

  // (Window focus is now handled by re-rendering with filtered PRs in renderTimeline)

  /* ── Summary Cards (contextual) ─────────────────────────── */

  function renderSummaryCards() {
    const container = document.getElementById('summary-cards');
    container.innerHTML = '';

    const fmt = (v, suffix = 'd') => v != null ? `${v}${suffix}` : '—';

    let cards;
    if (selectedWindow !== null) {
      // Per-window metrics — match single-PR view as closely as possible
      const win = data.releaseWindows[selectedWindow];
      const s = win?.summary || {};
      const windowPRs = getWindowPRs();
      const windowSDKPRs = windowPRs.filter(pr => !win.specPRNumbers.includes(pr.number));
      const sdkCount = windowSDKPRs.length;
      const specCount = (win?.specPRNumbers || []).length;

      // Compute slowest/fastest SDK PR in this window
      let slowestSDK = null, fastestSDK = null;
      for (const pr of windowSDKPRs) {
        if (!pr.createdAt || !pr.mergedAt) continue;
        const days = DataLoader.computeDurationDays(pr.createdAt, pr.mergedAt);
        if (!slowestSDK || days > slowestSDK.days) slowestSDK = { days, language: pr.language || '?' };
        if (!fastestSDK || days < fastestSDK.days) fastestSDK = { days, language: pr.language || '?' };
      }

      // Compute PR edits (manual PRs, non-merge commits minus initial)
      let prEdits = 0;
      for (const pr of windowSDKPRs) {
        if (pr.generationFlow === 'manual') {
          const commits = (pr.events || []).filter(e =>
            e.type === 'commit_pushed' &&
            !(e.description || '').toLowerCase().includes('merge')
          );
          prEdits += Math.max(0, commits.length - 1);
        }
      }

      const specLabel = (s.specPRCount || 1) > 1 ? `Spec PRs (${s.specPRCount})` : 'Spec PR';
      cards = [
        { label: 'Window', value: escapeHtml(win?.label || ''), sub: `${DataLoader.formatDateRange(win.startDate, win.endDate)}`, cls: 'info', smallValue: true },
        { label: specLabel, value: fmt(s.specPRDays), sub: 'API review', cls: 'info' },
        { label: 'Pipeline Gap', value: fmt(s.pipelineGapDays), sub: 'Merge → SDK PRs', cls: s.pipelineGapDays > 7 ? 'critical' : 'warning' },
      ];

      if (slowestSDK) cards.push({ label: 'Slowest SDK', value: fmt(slowestSDK.days), sub: slowestSDK.language, cls: 'warning' });
      if (fastestSDK) cards.push({ label: 'Fastest SDK', value: fmt(fastestSDK.days), sub: fastestSDK.language, cls: 'positive' });

      cards.push(
        { label: 'Total', value: fmt(s.totalDurationDays), sub: 'End to end', cls: 'info' },
        { label: 'Review Wait', value: fmt(s.totalReviewWaitDays), sub: `${s.totalReviewWaitCycles || 0} wait cycles`, cls: s.totalReviewWaitDays > 7 ? 'critical' : s.totalReviewWaitDays > 3 ? 'warning' : 'info' },
        { label: 'Nags', value: `${s.totalNags || 0}`, sub: 'Author nudges', cls: s.totalNags > 0 ? 'warning' : 'positive' },
        { label: 'Manual Fixes', value: `${s.totalManualFixes || 0}`, sub: 'On auto PRs', cls: s.totalManualFixes > 0 ? 'warning' : 'positive' },
        { label: 'PR Edits', value: `${prEdits}`, sub: 'On manual PRs', cls: prEdits > 0 ? 'warning' : 'positive' },
        { label: 'Reviewers', value: `${s.totalUniqueReviewers ?? '—'}`, sub: 'Unique people', cls: 'info' },
      );

      // Release data (if available in PR objects)
      const releasedPRs = windowSDKPRs.filter(p => p.release?.status === 'released');
      const pendingPRs = windowSDKPRs.filter(p => p.release?.status === 'pending');
      if (releasedPRs.length > 0) {
        const gaps = releasedPRs.map(p => p.release.releaseGapDays).filter(g => g != null);
        const avgGap = gaps.length > 0 ? Math.round(gaps.reduce((a, b) => a + b, 0) / gaps.length * 10) / 10 : null;
        if (avgGap != null) cards.push({ label: 'Release Gap', value: fmt(avgGap), sub: 'Avg merge → publish', cls: avgGap > 3 ? 'warning' : 'positive' });
      }
      if (pendingPRs.length > 0) {
        cards.push({ label: 'Pending', value: `${pendingPRs.length}`, sub: 'Unreleased packages', cls: 'critical' });
      }
    } else {
      // All-up metrics
      const s = data.summary || {};
      const langEntries = Object.entries(s.languageBreakdown || {});
      const slowest = langEntries.sort((a, b) => (b[1].avgDays || 0) - (a[1].avgDays || 0))[0];
      const fastest = langEntries.sort((a, b) => (a[1].avgDays || 0) - (b[1].avgDays || 0))[0];

      cards = [
        { label: 'Spec PRs', value: `${s.totalSpecPRs || 0}`, sub: 'Total specs', cls: 'info' },
        { label: 'SDK PRs', value: `${s.totalSDKPRs || 0}`, sub: `${Object.keys(data.sdkPRs).length} languages`, cls: 'info' },
        { label: 'Avg Cycle', value: fmt(s.avgCycleTimeDays), sub: 'Per release window', cls: s.avgCycleTimeDays > 30 ? 'warning' : 'info' },
        { label: 'Avg Pipe Gap', value: fmt(s.avgPipelineGapDays), sub: 'Spec merge → SDK', cls: s.avgPipelineGapDays > 7 ? 'critical' : 'warning' },
        { label: 'Avg Review Wait', value: fmt(s.avgReviewWaitDays), sub: 'Per PR', cls: s.avgReviewWaitDays > 5 ? 'warning' : 'info' },
        { label: 'Nags', value: `${s.totalNags || 0}`, sub: 'Total nudges', cls: s.totalNags > 5 ? 'warning' : 'positive' },
        { label: 'Automation', value: s.automationRate != null ? `${Math.round(s.automationRate * 100)}%` : '—', sub: 'Of SDK PRs', cls: s.automationRate >= 0.8 ? 'positive' : 'warning' },
      ];

      if (slowest && slowest[1].avgDays != null) {
        cards.push({ label: 'Slowest', value: fmt(slowest[1].avgDays), sub: slowest[0], cls: 'warning' });
      }
      if (fastest && fastest[1].avgDays != null) {
        cards.push({ label: 'Fastest', value: fmt(fastest[1].avgDays), sub: fastest[0], cls: 'positive' });
      }

      if (s.topReviewers?.length > 0) {
        const login = s.topReviewers[0].login;
        cards.push({
          label: 'Top Reviewer',
          value: escapeHtml(login),
          sub: `${s.topReviewers[0].reviewCount} reviews`,
          cls: 'info',
          link: `https://github.com/${encodeURIComponent(login)}`
        });
      }

      if (s.totalToolCalls > 0) {
        const rate = s.toolCallSuccessRate != null ? `${Math.round(s.toolCallSuccessRate * 100)}%` : '—';
        cards.push({ label: 'Tool Calls', value: `${s.totalToolCalls}`, sub: `${rate} success`, cls: s.toolCallSuccessRate < 0.8 ? 'warning' : 'positive' });
      }
    }

    for (const card of cards) {
      const el = document.createElement('div');
      el.className = `summary-card ${card.cls}`;
      const valueClass = card.smallValue ? 'card-value card-value-sm' : 'card-value';
      const valueHtml = card.link
        ? `<a href="${card.link}" target="_blank" rel="noopener" class="card-value-link">${card.value}</a>`
        : card.value;
      el.innerHTML = `
        <div class="card-label">${card.label}</div>
        <div class="${valueClass}">${valueHtml}</div>
        <div class="card-sub">${card.sub}</div>
      `;
      container.appendChild(el);
    }

    // Smart card grid: fit all in one row, or split evenly across 2 rows.
    // Avoid the case where 1-2 cards spill onto row 2 and expand to full width.
    fitCardGrid(container, cards.length);
  }

  /* ── Filters ────────────────────────────────────────────── */

  function renderFilters() {
    const container = document.getElementById('filter-buttons');
    container.innerHTML = '';

    const types = [
      'pr_created', 'pr_merged', 'ready_for_review', 'review_approved', 'review_changes_requested',
      'review_comment', 'issue_comment', 'author_nag', 'manual_fix',
      'commit_pushed', 'bot_comment', 'ci_status', 'idle_gap',
      'release_pipeline_started', 'release_pipeline_completed',
      'release_pipeline_failed', 'release_pending', 'tool_call'
    ];

    // Toggle all
    const allBtn = document.createElement('button');
    const allOn = hiddenEventTypes.size === 0;
    allBtn.className = `filter-btn toggle-all ${allOn ? 'active' : ''}`;
    allBtn.textContent = allOn ? 'Hide All' : 'Show All';
    allBtn.addEventListener('click', () => {
      if (hiddenEventTypes.size === 0) types.forEach(t => hiddenEventTypes.add(t));
      else hiddenEventTypes.clear();
      updateEventVisibility();
      renderFilters();
    });
    container.appendChild(allBtn);

    // Smart View
    const isSmartView = [...SMART_VIEW_TYPES].every(t => !hiddenEventTypes.has(t)) &&
      types.filter(t => !SMART_VIEW_TYPES.has(t)).every(t => hiddenEventTypes.has(t));
    const smartBtn = document.createElement('button');
    smartBtn.className = `filter-btn preset-btn ${isSmartView ? 'active' : ''}`;
    smartBtn.textContent = '⚡ Smart View';
    smartBtn.title = 'Show only high-signal events';
    smartBtn.addEventListener('click', () => {
      hiddenEventTypes.clear();
      types.forEach(t => { if (!SMART_VIEW_TYPES.has(t)) hiddenEventTypes.add(t); });
      updateEventVisibility();
      renderFilters();
    });
    container.appendChild(smartBtn);

    for (const type of types) {
      const info = DataLoader.getEventTypeInfo(type);
      const btn = document.createElement('button');
      btn.className = `filter-btn ${hiddenEventTypes.has(type) ? '' : 'active'}`;
      const swatch = `<span class="legend-swatch"><span class="event-marker ${type}"></span></span>`;
      btn.innerHTML = `${swatch} ${info.label}`;
      btn.addEventListener('click', () => {
        if (hiddenEventTypes.has(type)) { hiddenEventTypes.delete(type); btn.classList.add('active'); }
        else { hiddenEventTypes.add(type); btn.classList.remove('active'); }
        updateEventVisibility();
      });
      container.appendChild(btn);
    }

    renderActorFilters();
    renderBarLegend();
  }

  function renderBarLegend() {
    const container = document.getElementById('bar-legend');
    if (!container) return;
    const items = [
      { cls: 'pr-bar merged', label: 'Merged PR' },
      { cls: 'pr-bar open', label: 'Open PR' },
      { cls: 'pr-bar closed', label: 'Closed PR' },
      { cls: 'release-bar released', label: 'Release' },
      { cls: 'idle-gap warning', label: 'Idle gap' },
    ];
    container.innerHTML =
      '<span class="filters-label">Legend:</span>' +
      items.map(item =>
        `<span class="bar-legend-item">${item.label}<span class="bar-legend-swatch ${item.cls}"></span></span>`
      ).join('');
    container.classList.remove('hidden');
  }

  // Known bot actors
  const BOT_ACTOR_NAMES = new Set([
    'azure-sdk', 'copilot-agent', 'copilot', 'azure-pipelines', 'github-actions[bot]',
    'copilot-pull-request-reviewer[bot]', 'azure-pipelines[bot]', 'unknown', 'system', 'developer'
  ]);
  function isActorBot(name) {
    if (!name) return false;
    const lower = name.toLowerCase();
    return BOT_ACTOR_NAMES.has(lower) || lower.endsWith('[bot]');
  }

  function renderActorFilters() {
    let section = document.getElementById('actor-filters');
    if (!section) {
      section = document.createElement('section');
      section.id = 'actor-filters';
      section.className = 'filters actor-filters';
      const filtersEl = document.getElementById('filters');
      filtersEl.parentNode.insertBefore(section, filtersEl.nextSibling);
    }
    section.classList.remove('hidden');

    const actorCounts = {};
    const events = DataLoader.getAllEvents(data);
    for (const ev of events) {
      if (!ev.actor) continue;
      actorCounts[ev.actor] = (actorCounts[ev.actor] || 0) + 1;
    }
    allActors = Object.keys(actorCounts).sort((a, b) => actorCounts[b] - actorCounts[a]);

    const humanActors = allActors.filter(a => !isActorBot(a));
    const botActors = allActors.filter(a => isActorBot(a));

    section.innerHTML = '';

    const QUICK_COUNT = 8;

    // People section
    renderActorGroup(section, '👤', 'People', humanActors, actorCounts, QUICK_COUNT, 'people');

    // Bots section
    if (botActors.length > 0) {
      renderActorGroup(section, '🤖', 'Bots', botActors, actorCounts, 3, 'bots');
    }
  }

  function renderActorGroup(container, icon, label, actors, counts, quickCount, groupId) {
    const wrapper = document.createElement('div');
    wrapper.className = 'actor-group';
    wrapper.style.position = 'relative';

    const header = document.createElement('div');
    header.className = 'actor-group-header';

    const labelSpan = document.createElement('span');
    labelSpan.className = 'filters-label';
    labelSpan.textContent = `${icon} ${label} (${actors.length}):`;
    header.appendChild(labelSpan);

    // Show All / Hide All toggle
    const allHidden = actors.every(a => hiddenActors.has(a));
    const toggleBtn = document.createElement('button');
    toggleBtn.className = `filter-btn toggle-all ${allHidden ? '' : 'active'}`;
    toggleBtn.textContent = allHidden ? 'Show All' : 'Hide All';
    toggleBtn.addEventListener('click', () => {
      if (allHidden) actors.forEach(a => hiddenActors.delete(a));
      else actors.forEach(a => hiddenActors.add(a));
      updateEventVisibility();
      renderActorFilters();
    });
    header.appendChild(toggleBtn);

    wrapper.appendChild(header);

    const btnContainer = document.createElement('div');
    btnContainer.className = 'filter-buttons actor-filter-buttons';
    wrapper.appendChild(btnContainer);

    const quickActors = actors.slice(0, quickCount);
    for (const actor of quickActors) {
      btnContainer.appendChild(makeActorBtn(actor, counts[actor]));
    }

    // More button to open popover for this group
    if (actors.length > quickCount) {
      const moreBtn = document.createElement('button');
      moreBtn.className = 'filter-btn expand-btn';
      moreBtn.textContent = `+${actors.length - quickCount} more…`;
      moreBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        toggleActorPopover(wrapper, actors, counts, groupId);
      });
      btnContainer.appendChild(moreBtn);
    }

    container.appendChild(wrapper);
  }

  function toggleActorPopover(anchor, actors, counts, groupId) {
    const popoverId = `actor-popover-${groupId}`;
    let popover = document.getElementById(popoverId);
    if (popover) { popover.remove(); return; }

    popover = document.createElement('div');
    popover.id = popoverId;
    popover.className = 'actor-popover';

    const search = document.createElement('input');
    search.type = 'text';
    search.placeholder = `Search ${groupId}…`;
    search.className = 'actor-search';
    popover.appendChild(search);

    const list = document.createElement('div');
    list.className = 'actor-popover-list';
    popover.appendChild(list);

    function renderList(filter) {
      list.innerHTML = '';
      const items = filter
        ? actors.filter(a => a.toLowerCase().includes(filter.toLowerCase()))
        : actors;
      for (const actor of items) {
        const row = document.createElement('label');
        row.className = 'actor-popover-item';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.checked = !hiddenActors.has(actor);
        cb.addEventListener('change', () => {
          if (cb.checked) hiddenActors.delete(actor);
          else hiddenActors.add(actor);
          updateEventVisibility();
          document.querySelectorAll('.actor-btn').forEach(btn => {
            const name = btn.title?.split(':')[0];
            if (name === actor) btn.classList.toggle('active', cb.checked);
          });
        });
        row.appendChild(cb);
        const nameSpan = document.createElement('span');
        nameSpan.className = 'actor-popover-name';
        nameSpan.textContent = actor;
        row.appendChild(nameSpan);
        const countSpan = document.createElement('span');
        countSpan.className = 'actor-popover-count';
        countSpan.textContent = counts[actor];
        row.appendChild(countSpan);
        list.appendChild(row);
      }
      if (items.length === 0) {
        list.innerHTML = '<div class="actor-popover-empty">No matching actors</div>';
      }
    }

    renderList('');
    search.addEventListener('input', () => renderList(search.value));

    anchor.appendChild(popover);
    search.focus();

    const closeHandler = (e) => {
      if (!popover.contains(e.target) && e.target !== popover) {
        popover.remove();
        document.removeEventListener('click', closeHandler);
      }
    };
    setTimeout(() => document.addEventListener('click', closeHandler), 0);

    const escHandler = (e) => {
      if (e.key === 'Escape') {
        popover.remove();
        document.removeEventListener('keydown', escHandler);
        document.removeEventListener('click', closeHandler);
      }
    };
    document.addEventListener('keydown', escHandler);
  }

  function makeActorBtn(actor, count) {
    const btn = document.createElement('button');
    const isActive = !hiddenActors.has(actor);
    btn.className = `filter-btn actor-btn ${isActive ? 'active' : ''}`;
    btn.innerHTML = `${escapeHtml(actor)} <span class="actor-count">${count}</span>`;
    btn.title = `${actor}: ${count} events`;
    btn.addEventListener('click', () => {
      if (hiddenActors.has(actor)) { hiddenActors.delete(actor); btn.classList.add('active'); }
      else { hiddenActors.add(actor); btn.classList.remove('active'); }
      updateEventVisibility();
    });
    return btn;
  }

  function updateEventVisibility() {
    document.querySelectorAll('.event-marker').forEach(el => {
      const type = el.dataset.eventType;
      const actor = el._eventData?.actor;
      const typeHidden = hiddenEventTypes.has(type);
      const actorHidden = actor && hiddenActors.has(actor);
      if (typeHidden || actorHidden) el.classList.add('hidden');
      else el.classList.remove('hidden');
    });
    document.querySelectorAll('.idle-gap').forEach(el => {
      if (hiddenEventTypes.has('idle_gap')) el.classList.add('hidden');
      else el.classList.remove('hidden');
    });
  }

  /* ── Gap Compaction ─────────────────────────────────────── */

  function getWindowPRNumbers() {
    if (selectedWindow === null) return null;
    const win = data.releaseWindows[selectedWindow];
    if (!win) return null;
    const nums = new Set();
    for (const n of win.specPRNumbers) nums.add(n);
    for (const arr of Object.values(win.sdkPRNumbers || {})) {
      for (const n of arr) nums.add(n);
    }
    return nums;
  }

  function getWindowPRs() {
    const nums = getWindowPRNumbers();
    if (!nums) return DataLoader.getAllPRs(data);
    return DataLoader.getAllPRs(data).filter(pr => nums.has(pr.number));
  }

  function buildSegments() {
    const prsToUse = focusRange ? getWindowPRs() : DataLoader.getAllPRs(data);
    const ts = new Set();

    for (const pr of prsToUse) {
      if (!pr.createdAt) continue;
      ts.add(new Date(pr.createdAt).getTime());
      if (pr.mergedAt) ts.add(new Date(pr.mergedAt).getTime());
      if (pr.closedAt) ts.add(new Date(pr.closedAt).getTime());
      for (const ev of (pr.events || [])) {
        ts.add(new Date(ev.timestamp).getTime());
        if (ev.endTimestamp) ts.add(new Date(ev.endTimestamp).getTime());
      }
    }

    const sorted = [...ts].sort((a, b) => a - b);
    segments = [];

    if (sorted.length < 2) {
      segments = [{ type: 'active', startMs: timeRange.start.getTime(), endMs: timeRange.end.getTime() }];
      assignPixelRanges();
      return;
    }

    let cStart = sorted[0], cEnd = sorted[0];
    for (let i = 1; i < sorted.length; i++) {
      if (sorted[i] - cEnd > GAP_THRESHOLD_MS) {
        segments.push({ type: 'active', startMs: cStart, endMs: cEnd });
        segments.push({
          type: 'gap', startMs: cEnd, endMs: sorted[i],
          durationDays: Math.round((sorted[i] - cEnd) / 86400000 * 10) / 10
        });
        cStart = sorted[i];
      }
      cEnd = sorted[i];
    }
    segments.push({ type: 'active', startMs: cStart, endMs: cEnd });

    // Pad active segments
    for (const seg of segments) {
      if (seg.type === 'active') {
        const dur = Math.max(seg.endMs - seg.startMs, 3600000);
        const pad = Math.max(4 * 3600000, Math.min(dur * 0.06, 24 * 3600000));
        seg.startMs -= pad;
        seg.endMs += pad;
      }
    }

    // Clamp gap boundaries
    for (let i = 1; i < segments.length - 1; i++) {
      if (segments[i].type === 'gap') {
        segments[i].startMs = segments[i - 1].endMs;
        segments[i].endMs = segments[i + 1].startMs;
      }
    }

    assignPixelRanges();
  }

  function assignPixelRanges() {
    const activeSegs = segments.filter(s => s.type === 'active');
    const gapCount = segments.filter(s => s.type === 'gap').length;

    const minNeeded = padding.left + padding.right +
      activeSegs.length * MIN_SEGMENT_PX + gapCount * COMPRESSED_GAP_PX;
    if (contentWidth < minNeeded) contentWidth = minNeeded;

    const usable = contentWidth - padding.left - padding.right;
    const activePixels = usable - gapCount * COMPRESSED_GAP_PX;

    let totalDuration = 0;
    for (const s of activeSegs) totalDuration += Math.max(s.endMs - s.startMs, 1);

    let px = padding.left;
    for (const seg of segments) {
      seg.startPx = px;
      if (seg.type === 'active') {
        const frac = Math.max(seg.endMs - seg.startMs, 1) / totalDuration;
        seg.endPx = px + Math.max(frac * activePixels, MIN_SEGMENT_PX);
      } else {
        seg.endPx = px + COMPRESSED_GAP_PX;
      }
      px = seg.endPx;
    }
    contentWidth = segments[segments.length - 1].endPx + padding.right;
  }

  function timeToX(timestamp) {
    const t = new Date(timestamp).getTime();
    for (const seg of segments) {
      if (t >= seg.startMs && t <= seg.endMs) {
        const range = seg.endMs - seg.startMs;
        const frac = range === 0 ? 0.5 : (t - seg.startMs) / range;
        return seg.startPx + frac * (seg.endPx - seg.startPx);
      }
    }
    if (t < segments[0].startMs) return segments[0].startPx;
    return segments[segments.length - 1].endPx;
  }

  /* ── Zoom Controls ──────────────────────────────────────── */

  function renderZoomControls() {
    const spacer = document.querySelector('.timeline-labels .lane-label-spacer');
    if (!spacer) return;
    let controls = document.getElementById('zoom-controls');
    if (!controls) {
      controls = document.createElement('div');
      controls.id = 'zoom-controls';
      controls.className = 'zoom-controls';
      spacer.appendChild(controls);
    }
    controls.innerHTML = `
      <button class="zoom-btn" data-action="out" title="Zoom out">−</button>
      <span class="zoom-level">${Math.round(zoomLevel * 100)}%</span>
      <button class="zoom-btn" data-action="in" title="Zoom in">+</button>
      <button class="zoom-btn" data-action="reset" title="Reset">↺</button>
    `;
    controls.querySelectorAll('.zoom-btn').forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const action = btn.dataset.action;
        if (action === 'reset') { zoomLevel = 1; renderTimeline(); }
        else adjustZoom(action === 'in' ? 1 : -1);
      });
    });
  }

  function adjustZoom(direction) {
    const steps = [0.5, 0.75, 1, 1.25, 1.5, 2, 3, 5];
    let idx = steps.findIndex(s => Math.abs(s - zoomLevel) < 0.01);
    if (idx === -1) idx = 2;
    idx = Math.max(0, Math.min(steps.length - 1, idx + direction));
    zoomLevel = steps[idx];
    renderTimeline();
  }

  function toggleLaneExpansion(laneKey) {
    if (!laneKey) return;
    if (expandedLaneKeys.has(laneKey)) expandedLaneKeys.delete(laneKey);
    else expandedLaneKeys.add(laneKey);
    renderTimeline();
  }

  function renderLaneGroup(container, labelsContainer, langName, langClass, prs, isSpec, laneKey) {
    renderMultiPRLane(container, labelsContainer, langName, langClass, prs, isSpec, { laneKey });
    if (!expandedLaneKeys.has(laneKey) || prs.length <= 1) return;

    const sorted = [...prs].sort((a, b) => {
      if (!a.createdAt) return 1;
      if (!b.createdAt) return -1;
      return new Date(a.createdAt) - new Date(b.createdAt);
    });

    for (const pr of sorted) {
      renderExpandedPRLane(container, labelsContainer, langName, langClass, pr, isSpec, laneKey);
    }
  }

  /* ── Timeline Rendering ─────────────────────────────────── */

  function renderTimeline() {
    const lanesContainer = document.getElementById('lanes');
    const labelsContainer = document.getElementById('lane-labels');

    // Preserve scroll positions across re-render
    const scrollEl = document.querySelector('.timeline-scroll');
    const savedScrollLeft = scrollEl?.scrollLeft || 0;
    const savedPageScrollY = window.scrollY;

    lanesContainer.innerHTML = '';
    labelsContainer.innerHTML = '';

    // Clean up old focus notices
    document.querySelectorAll('.focus-notice').forEach(el => el.remove());

    const availWidth = scrollEl.clientWidth;
    contentWidth = Math.max(availWidth * zoomLevel, 800);

    buildSegments();
    lanesContainer.style.minWidth = contentWidth + 'px';

    renderZoomControls();
    renderTimeAxis('time-axis');
    renderTimeAxis('time-axis-bottom');

    // In focus mode, only show PRs belonging to the selected window.
    const windowNums = getWindowPRNumbers();
    const filterPRs = (prs) => {
      if (windowNums) return prs.filter(pr => windowNums.has(pr.number));
      return prs;
    };

    // Spec PRs lane
    const specPRs = filterPRs(data.specPRs);
    if (specPRs.length > 0 || !windowNums) {
      renderLaneGroup(lanesContainer, labelsContainer, 'Spec PRs', 'spec', specPRs, true, 'spec');
    }

    // SDK PR lanes per language
    const langOrder = ['Python', 'Java', 'Go', '.NET', 'JavaScript'];
    for (const lang of langOrder) {
      const prs = filterPRs(data.sdkPRs[lang] || []);
      if (prs.length === 0 && windowNums) continue; // hide empty lanes in focus mode
      if (!data.sdkPRs[lang] || data.sdkPRs[lang].length === 0) continue;
      renderLaneGroup(lanesContainer, labelsContainer, lang, lang.toLowerCase().replace('.', ''), prs, false, `lang:${lang}`);
    }
    // Any remaining languages not in standard order
    for (const [lang, allPrs] of Object.entries(data.sdkPRs)) {
      if (langOrder.includes(lang) || allPrs.length === 0) continue;
      const prs = filterPRs(allPrs);
      if (prs.length === 0 && windowNums) continue;
      renderLaneGroup(lanesContainer, labelsContainer, lang, lang.toLowerCase(), prs, false, `lang:${lang}`);
    }

    addGridlines(lanesContainer);

    // Show hidden PR count in focus mode
    if (windowNums) {
      const totalPRs = DataLoader.getAllPRs(data).length;
      const shownPRs = getWindowPRs().length;
      const hidden = totalPRs - shownPRs;
      if (hidden > 0) {
        const notice = document.createElement('div');
        notice.className = 'focus-notice';
        notice.textContent = `${hidden} PRs outside this window are hidden`;
        const container = document.getElementById('timeline-container');
        container.parentNode.insertBefore(notice, container);
      }
    }

    // Sync label/lane heights to prevent subpixel border drift
    syncLaneHeights();

    // Restore scroll positions
    if (scrollEl) scrollEl.scrollLeft = savedScrollLeft;
    window.scrollTo(window.scrollX, savedPageScrollY);
  }

  function syncLaneHeights() {
    const labels = document.querySelectorAll('#lane-labels .service-lane-label');
    const lanes = document.querySelectorAll('#lanes .service-lane');
    const count = Math.min(labels.length, lanes.length);
    for (let i = 0; i < count; i++) {
      labels[i].style.height = '';
      lanes[i].style.height = '';
    }
    for (let i = 0; i < count; i++) {
      const h = Math.max(labels[i].offsetHeight, lanes[i].offsetHeight);
      labels[i].style.height = h + 'px';
      lanes[i].style.height = h + 'px';
    }
  }

  function renderTimeAxis(elementId) {
    const axis = document.getElementById(elementId);
    axis.innerHTML = '';
    axis.style.width = contentWidth + 'px';

    for (const seg of segments) {
      if (seg.type === 'active') {
        const segDays = (seg.endMs - seg.startMs) / 86400000;
        let intervalDays;
        if (segDays <= 7) intervalDays = 1;
        else if (segDays <= 30) intervalDays = 7;
        else if (segDays <= 90) intervalDays = 14;
        else intervalDays = 30;

        const tickDate = new Date(seg.startMs);
        tickDate.setUTCHours(0, 0, 0, 0);
        while (tickDate.getTime() < seg.startMs) {
          tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
        }
        while (tickDate.getTime() <= seg.endMs) {
          const x = timeToX(tickDate.toISOString());
          const tick = document.createElement('div');
          tick.className = 'time-tick';
          if (tickDate.getUTCDate() === 1 || intervalDays >= 14) tick.classList.add('major');
          tick.style.left = x + 'px';
          tick.textContent = DataLoader.formatDate(tickDate.toISOString());
          axis.appendChild(tick);
          tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
        }
      } else {
        const midPx = (seg.startPx + seg.endPx) / 2;
        const breakEl = document.createElement('div');
        breakEl.className = 'time-break';
        breakEl.style.left = midPx + 'px';
        breakEl.textContent = `⋯ ${seg.durationDays}d`;
        breakEl.title = `${seg.durationDays} day gap (compressed)`;
        axis.appendChild(breakEl);
      }
    }
  }

  /* ── Multi-PR Lane ──────────────────────────────────────── */

  function getVisibleSDKPRs() {
    const nums = getWindowPRNumbers();
    const all = Object.values(data.sdkPRs || {}).flat();
    return nums ? all.filter(pr => nums.has(pr.number)) : all;
  }

  function buildSpecToSdkHtml(pr) {
    if (!pr.mergedAt) return '';
    const visibleSdkPRs = getVisibleSDKPRs().filter(sdk => sdk.createdAt);
    if (visibleSdkPRs.length === 0) return '';

    const laterSdkPRs = visibleSdkPRs.filter(sdk => new Date(sdk.createdAt) >= new Date(pr.mergedAt));
    if (laterSdkPRs.length === 0) return '';

    const firstSdkPR = laterSdkPRs.reduce((earliest, sdk) =>
      new Date(sdk.createdAt) < new Date(earliest.createdAt) ? sdk : earliest
    );
    const gapDays = DataLoader.computeDurationDays(pr.mergedAt, firstSdkPR.createdAt);
    return `<span class="spec-to-sdk" title="Time from spec merge to first SDK PR opened">→ SDK: ${gapDays}d</span>`;
  }

  function buildSinglePRLaneMeta(pr, langName, langClass, isSpec) {
    const prDays = pr.mergedAt ? DataLoader.computeDurationDays(pr.createdAt, pr.mergedAt) : null;
    const durationColorClass = prDays != null
      ? prDays < 3 ? 'dur-fast' : prDays < 7 ? 'dur-ok' : prDays < 14 ? 'dur-slow' : 'dur-critical'
      : '';
    const prDaysDisplay = prDays != null
      ? `<span class="duration-value ${durationColorClass}">${prDays}d</span>`
      : (pr.state === 'open' ? '<span class="status-pulse">⏳ open</span>' : pr.state === 'closed' ? '✖ closed' : '—');
    const durationTooltip = pr.mergedAt
      ? `PR open ${prDays} days (${DataLoader.formatDate(pr.createdAt)} → ${DataLoader.formatDate(pr.mergedAt)})`
      : pr.state === 'open' ? `PR still open (since ${DataLoader.formatDate(pr.createdAt)})` : 'PR not merged';
    const flowIcon = !isSpec && pr.generationFlow
      ? pr.generationFlow === 'automated'
        ? '<span class="flow-badge automated" title="Automated (pipeline-generated)">🤖</span>'
        : '<span class="flow-badge manual" title="Manual (human-authored)">👤</span>'
      : '';
    const draftBadge = pr.isDraft
      ? '<span class="draft-badge" title="Draft PR — not yet ready for review">draft</span>'
      : '';
    const reviewWaitHtml = pr.reviewWaitDays != null && pr.reviewWaitDays > 0
      ? `<span class="review-wait-badge" title="Time waiting for reviewer response (${pr.reviewWaitCycles || 0} cycles)">⏳ ${pr.reviewWaitDays}d wait</span>`
      : '';
    const specToSdkHtml = isSpec ? buildSpecToSdkHtml(pr) : '';
    const releaseStatus = pr.release
      ? pr.release.status === 'released'
        ? `<span class="release-badge released" title="Released ${DataLoader.formatDate(pr.release.releasedAt)}">${pr.release.releaseGapDays ? '📦 ' + pr.release.releaseGapDays + 'd' : '📦 <1d'}</span>`
        : pr.release.status === 'pending'
          ? '<span class="release-badge pending status-pulse">⏳ pending</span>'
          : '<span class="release-badge failed" title="Release pipeline failed">❌ failed</span>'
      : '';
    const releaseLinkHtml = pr.release?.pipelineUrl
      ? ` <a href="${pr.release.pipelineUrl}" target="_blank" rel="noopener" title="${escapeHtml(pr.release.pipelineName || 'Release pipeline')}">🔗</a>`
      : '';

    return `
      <div class="lane-repo">
        <a href="${pr.url}" target="_blank" rel="noopener" class="meta-pr-link" data-pr-idx="0">#${pr.number}</a>${releaseLinkHtml}
        <span class="meta-pr-info" data-pr-idx="0" aria-label="Open PR details">ℹ</span>
        ${draftBadge}
      </div>
      <div class="lane-meta">
        <span class="lane-language ${langClass}">${escapeHtml(langName)}</span>
        ${flowIcon}
        <span title="${durationTooltip}">${prDaysDisplay}</span>
        ${reviewWaitHtml}
        ${specToSdkHtml}
        ${releaseStatus}
      </div>
    `;
  }

  function wireLanePRMeta(label, prs) {
    label.querySelectorAll('.meta-pr-info[data-pr-idx]').forEach(el => {
      const pr = prs[parseInt(el.dataset.prIdx, 10)];
      if (!pr) return;
      el.addEventListener('click', (e) => {
        e.stopPropagation();
        UI.showPRDetail(pr);
      });
    });
    label.querySelectorAll('.meta-pr-link').forEach(el => {
      const idx = parseInt(el.dataset.prIdx || '0', 10);
      const pr = prs[idx];
      if (!pr) return;
      el.addEventListener('mouseenter', (e) => UI.showPRTooltip(e, pr));
      el.addEventListener('mouseleave', () => UI.hideTooltip());
    });
  }

  function renderMultiPRLane(container, labelsContainer, langName, langClass, prs, isSpec, options = {}) {
    const laneIndex = container.children.length;
    const laneKey = options.laneKey || '';
    const isExpanded = laneKey && expandedLaneKeys.has(laneKey) && prs.length > 1;

    // Label
    const label = document.createElement('div');
    label.className = `lane-label ${isSpec ? 'spec-lane' : ''} service-lane-label`;
    label.dataset.laneIndex = laneIndex;

    const activePRs = prs;
    const prCount = activePRs.length;
    const mergedCount = activePRs.filter(p => p.state === 'merged').length;
    const openCount = activePRs.filter(p => p.state === 'open').length;

    // Build PR links for the meta section — links go to GitHub, info button opens sidebar
    let prLinksHtml = '';
    if (activePRs.length > 0 && activePRs.length <= 3) {
      prLinksHtml = activePRs.map((pr, i) => {
        const prUrl = pr.url || `https://github.com/${pr.repo || ''}/pull/${pr.number}`;
        return `<a href="${prUrl}" target="_blank" rel="noopener" class="meta-pr-link" data-pr-idx="${i}">#${pr.number}</a>` +
          `<span class="meta-pr-info" data-pr-idx="${i}" aria-label="Open PR details">ℹ</span>`;
      }).join(' ');
    } else if (activePRs.length > 3) {
      prLinksHtml = `<span class="meta-pr-expand" title="Click to see all PRs">${prCount} PRs — click to see all</span>`;
    }

    const laneToggleHtml = activePRs.length > 1
      ? `<button class="lane-toggle-btn ${isExpanded ? 'active' : ''}" data-lane-toggle="${escapeHtml(laneKey)}">${isExpanded ? 'Collapse lanes' : 'Expand lanes'}</button>`
      : '';

    // Enhanced per-PR meta when there's only 1 PR (matches single PR view detail)
    let detailMeta = '';
    if (activePRs.length === 1) {
      const pr = activePRs[0];
      const days = pr.mergedAt ? DataLoader.computeDurationDays(pr.createdAt, pr.mergedAt) : null;
      const durationColorClass = days != null
        ? days < 3 ? 'dur-fast' : days < 7 ? 'dur-ok' : days < 14 ? 'dur-slow' : 'dur-critical'
        : '';
      const daysDisplay = days != null ? `<span class="duration-value ${durationColorClass}">${days}d</span>` :
        (pr.state === 'open' ? '<span class="status-pulse">⏳ open</span>' : '');
      const flowIcon = !isSpec && pr.generationFlow
        ? pr.generationFlow === 'automated' ? '<span class="flow-badge automated" title="Automated">🤖</span>'
          : '<span class="flow-badge manual" title="Manual">👤</span>'
        : '';
      const reviewWaitHtml = pr.reviewWaitDays != null && pr.reviewWaitDays > 0
        ? `<span class="review-wait-badge" title="${pr.reviewWaitCycles || 0} cycles">⏳ ${pr.reviewWaitDays}d wait</span>` : '';
      const releaseStatus = pr.release
        ? pr.release.status === 'released'
          ? `<span class="release-badge released" title="Released ${DataLoader.formatDate(pr.release.releasedAt)}">${pr.release.releaseGapDays ? '📦 ' + pr.release.releaseGapDays + 'd' : '📦 <1d'}</span>`
          : pr.release.status === 'pending'
            ? '<span class="release-badge pending status-pulse">⏳ pending</span>'
            : '<span class="release-badge failed" title="Release pipeline failed">❌ failed</span>'
        : '';
      const releaseLinkHtml = pr.release?.pipelineUrl
        ? ` <a href="${pr.release.pipelineUrl}" target="_blank" title="${escapeHtml(pr.release.pipelineName || 'Release pipeline')}">🔗</a>` : '';
      detailMeta = `${flowIcon} ${daysDisplay} ${reviewWaitHtml} ${releaseStatus}${releaseLinkHtml}`;
    } else if (activePRs.length > 1) {
      // Aggregate release status for multi-PR lanes
      const released = activePRs.filter(p => p.release?.status === 'released');
      const pending = activePRs.filter(p => p.release?.status === 'pending');
      const failed = activePRs.filter(p => p.release?.status === 'failed');
      const parts = [];
      if (released.length > 0) {
        const gaps = released.map(p => p.release.releaseGapDays).filter(g => g != null);
        const avgGap = gaps.length > 0 ? (gaps.reduce((a, b) => a + b, 0) / gaps.length).toFixed(1) : null;
        parts.push(`<span class="release-badge released" title="${released.length} released${avgGap ? ', avg ' + avgGap + 'd' : ''}">📦 ${released.length}</span>`);
      }
      if (pending.length > 0) {
        parts.push(`<span class="release-badge pending status-pulse" title="${pending.length} pending release">⏳ ${pending.length} pending</span>`);
      }
      if (failed.length > 0) {
        parts.push(`<span class="release-badge failed" title="${failed.length} failed pipeline">❌ ${failed.length}</span>`);
      }
      detailMeta = parts.join(' ');
    }

    label.innerHTML = `
      <div class="lane-repo">
        <span class="lane-language ${langClass}">${escapeHtml(langName)}</span>
      </div>
      <div class="lane-meta">
        <span>${prCount} PRs</span>
        <span class="pr-counts">${mergedCount}✓ ${openCount > 0 ? openCount + '⏳' : ''}</span>
        ${laneToggleHtml}
        ${prLinksHtml ? `<span class="meta-pr-links">${prLinksHtml}</span>` : ''}
        ${detailMeta}
      </div>
    `;

    wireLanePRMeta(label, prs);

    const toggleEl = label.querySelector('.lane-toggle-btn');
    if (toggleEl) {
      toggleEl.addEventListener('click', (e) => {
        e.stopPropagation();
        toggleLaneExpansion(laneKey);
      });
    }

    // Handle "click to see all" expansion
    const expandEl = label.querySelector('.meta-pr-expand');
    if (expandEl) {
      expandEl.addEventListener('click', (e) => {
        e.stopPropagation();
        let dropdown = expandEl.parentElement.querySelector('.meta-pr-dropdown');
        if (dropdown) { dropdown.remove(); return; }
        dropdown = document.createElement('div');
        dropdown.className = 'meta-pr-dropdown';
        for (const pr of activePRs) {
          const item = document.createElement('span');
          item.className = 'meta-pr-dropdown-item';
          item.textContent = `#${pr.number}: ${(pr.title || '').slice(0, 50)}${(pr.title || '').length > 50 ? '…' : ''}`;
          item.addEventListener('click', (ev) => {
            ev.stopPropagation();
            dropdown.remove();
            UI.showPRDetail(pr);
          });
          dropdown.appendChild(item);
        }
        expandEl.parentElement.appendChild(dropdown);
        const close = (ev) => {
          if (!dropdown.contains(ev.target) && ev.target !== expandEl) {
            dropdown.remove();
            document.removeEventListener('click', close);
          }
        };
        setTimeout(() => document.addEventListener('click', close), 0);
      });
    }

    labelsContainer.appendChild(label);

    // Lane
    const lane = document.createElement('div');
    lane.className = `lane ${isSpec ? 'spec-lane' : ''} service-lane`;
    lane.dataset.laneIndex = laneIndex;

    // Hover sync
    lane.addEventListener('mouseenter', () => label.classList.add('hover'));
    lane.addEventListener('mouseleave', () => label.classList.remove('hover'));
    label.addEventListener('mouseenter', () => { lane.classList.add('hover'); label.classList.add('hover'); });
    label.addEventListener('mouseleave', () => { lane.classList.remove('hover'); label.classList.remove('hover'); });

    const content = document.createElement('div');
    content.className = 'lane-content';
    content.style.width = contentWidth + 'px';

    // Gap break indicators
    for (const seg of segments) {
      if (seg.type === 'gap') {
        const breakEl = document.createElement('div');
        breakEl.className = 'lane-break';
        breakEl.style.left = seg.startPx + 'px';
        breakEl.style.width = (seg.endPx - seg.startPx) + 'px';
        content.appendChild(breakEl);
      }
    }

    // Sort PRs by creation date
    const sorted = [...prs].sort((a, b) => {
      if (!a.createdAt) return 1;
      if (!b.createdAt) return -1;
      return new Date(a.createdAt) - new Date(b.createdAt);
    });

    // Render each PR as a bar with events
    for (const pr of sorted) {
      if (!pr.createdAt) continue;
      renderPRBar(content, pr, isSpec);
    }

    lane.appendChild(content);
    container.appendChild(lane);
  }

  function renderExpandedPRLane(container, labelsContainer, langName, langClass, pr, isSpec, laneKey) {
    const laneIndex = container.children.length;

    const label = document.createElement('div');
    label.className = `lane-label ${isSpec ? 'spec-lane' : ''} service-lane-label expanded-child-label`;
    label.dataset.laneIndex = laneIndex;
    label.dataset.parentLaneKey = laneKey;
    label.innerHTML = buildSinglePRLaneMeta(pr, langName, langClass, isSpec);
    wireLanePRMeta(label, [pr]);
    labelsContainer.appendChild(label);

    const lane = document.createElement('div');
    lane.className = `lane ${isSpec ? 'spec-lane' : ''} service-lane expanded-child-lane`;
    lane.dataset.laneIndex = laneIndex;
    lane.dataset.parentLaneKey = laneKey;

    lane.addEventListener('mouseenter', () => label.classList.add('hover'));
    lane.addEventListener('mouseleave', () => label.classList.remove('hover'));
    label.addEventListener('mouseenter', () => { lane.classList.add('hover'); label.classList.add('hover'); });
    label.addEventListener('mouseleave', () => { lane.classList.remove('hover'); label.classList.remove('hover'); });

    const content = document.createElement('div');
    content.className = 'lane-content';
    content.style.width = contentWidth + 'px';

    for (const seg of segments) {
      if (seg.type === 'gap') {
        const breakEl = document.createElement('div');
        breakEl.className = 'lane-break';
        breakEl.style.left = seg.startPx + 'px';
        breakEl.style.width = (seg.endPx - seg.startPx) + 'px';
        content.appendChild(breakEl);
      }
    }

    renderPRBar(content, pr, isSpec);
    lane.appendChild(content);
    container.appendChild(lane);
  }

  function renderPRBar(content, pr, isSpec) {
    const barStart = timeToX(pr.createdAt);
    const barEnd = timeToX(pr.mergedAt || pr.closedAt || data.endDate);

    // PR duration bar
    const bar = document.createElement('div');
    bar.className = `pr-bar pr-bar-multi ${pr.state === 'merged' ? 'merged' : ''} ${pr.state === 'open' ? 'open' : ''} ${pr.state === 'closed' && !pr.mergedAt ? 'closed' : ''}`;
    bar.dataset.prNumber = pr.number;
    bar.style.left = barStart + 'px';
    bar.style.width = Math.max(barEnd - barStart, 4) + 'px';

    // Tooltip with PR summary on hover
    const days = pr.mergedAt ? DataLoader.computeDurationDays(pr.createdAt, pr.mergedAt) : null;
    bar.title = `#${pr.number}: ${(pr.title || '').slice(0, 60)}${days ? ` (${days}d)` : pr.state === 'open' ? ' (open)' : ''}`;

    // Subtle PR number label inside bar if wide enough
    if (barEnd - barStart > 40) {
      const numLabel = document.createElement('span');
      numLabel.className = 'pr-bar-label';
      numLabel.textContent = `#${pr.number}`;
      bar.appendChild(numLabel);
    }

    content.appendChild(bar);

    // Release segment bar (extends from merge to release, striped)
    if (pr.release && pr.mergedAt && pr.release.releasedAt) {
      const mergeX = timeToX(pr.mergedAt);
      let releaseEnd = timeToX(pr.release.releasedAt);
      const status = pr.release.status || 'released';
      if (releaseEnd <= mergeX) releaseEnd = mergeX + 8;
      const barWidth = Math.max(releaseEnd - mergeX, 4);
      const releaseBar = document.createElement('div');
      releaseBar.className = `release-bar ${status}`;
      releaseBar.style.left = mergeX + 'px';
      releaseBar.style.width = barWidth + 'px';
      releaseBar.title = status === 'released'
        ? `Released ${DataLoader.formatDate(pr.release.releasedAt)} (${pr.release.releaseGapDays != null ? pr.release.releaseGapDays + 'd' : 'same day'} after merge)`
        : status === 'failed' ? `❌ Release pipeline failed` : `⏳ Release pending`;
      content.appendChild(releaseBar);
    }

    // Event markers on the bar
    const markers = [];
    for (const event of (pr.events || [])) {
      if (event.type === 'idle_gap') {
        renderIdleGap(content, event);
        continue;
      }
      const x = timeToX(event.timestamp);
      const marker = document.createElement('div');
      marker.className = `event-marker ${event.type}`;
      if (hiddenEventTypes.has(event.type)) marker.classList.add('hidden');
      marker.dataset.eventType = event.type;
      marker.style.left = x + 'px';
      marker._eventData = event;
      marker._prData = pr;
      marker._x = x;
      applyMarkerLayout(marker);

      marker.addEventListener('mouseenter', (e) => UI.showTooltip(e, event, pr));
      marker.addEventListener('mouseleave', () => UI.hideTooltip());
      marker.addEventListener('click', () => UI.showDetail(event, pr));

      content.appendChild(marker);
      markers.push(marker);
    }

    // Resolve overlapping markers (vertical stagger like single-PR view)
    resolveCollisions(markers);
  }

  function renderIdleGap(content, event) {
    if (!event.timestamp || !event.endTimestamp) return;
    const x1 = timeToX(event.timestamp);
    const x2 = timeToX(event.endTimestamp);
    const width = Math.max(x2 - x1, 2);

    const gap = document.createElement('div');
    gap.className = `idle-gap ${event.details?.durationHours > 48 ? 'warning' : ''}`;
    gap.style.left = x1 + 'px';
    gap.style.width = width + 'px';
    gap.title = `Idle: ${DataLoader.formatDuration(event.details?.durationHours || 0)}`;
    content.appendChild(gap);
  }

  /* ── Collision Resolution ───────────────────────────────── */

  function getMarkerLayout(type) {
    return EVENT_LAYOUT[type] || { offset: 36, priority: 99 };
  }

  function applyMarkerLayout(marker) {
    const layout = getMarkerLayout(marker.dataset.eventType);
    marker._baseOffset = layout.offset;
    marker._priority = layout.priority;
    marker.style.top = `calc(50% + ${layout.offset}px)`;
    marker.classList.toggle('priority-above', layout.offset < 0);
    marker.classList.toggle('priority-below', layout.offset >= 0);
  }

  function resolveCollisions(markers) {
    if (markers.length === 0) return;
    markers.forEach(applyMarkerLayout);
    if (markers.length < 2) return;
    const MIN_SPACING = 14;
    const sorted = [...markers].sort((a, b) => a._x - b._x);
    const clusters = [];
    let cluster = [sorted[0]];
    for (let i = 1; i < sorted.length; i++) {
      if (sorted[i]._x - sorted[i - 1]._x < MIN_SPACING) {
        cluster.push(sorted[i]);
      } else {
        if (cluster.length > 1) clusters.push(cluster);
        cluster = [sorted[i]];
      }
    }
    if (cluster.length > 1) clusters.push(cluster);

    for (const group of clusters) {
      const placedOffsets = [];
      const ordered = [...group].sort((a, b) =>
        a._priority - b._priority || a._baseOffset - b._baseOffset || a._x - b._x
      );
      for (const marker of ordered) {
        let offset = marker._baseOffset;
        const step = offset < 0 ? -8 : 8;
        while (placedOffsets.some(existing => Math.abs(existing - offset) < 8)) {
          offset += step;
        }
        marker.style.top = `calc(50% + ${offset}px)`;
        placedOffsets.push(offset);
      }
    }
  }

  function addGridlines(container) {
    for (const seg of segments) {
      if (seg.type !== 'active') continue;
      const segDays = (seg.endMs - seg.startMs) / 86400000;
      const intervalDays = segDays <= 7 ? 1 : segDays <= 30 ? 7 : segDays <= 90 ? 14 : 30;

      const tickDate = new Date(seg.startMs);
      tickDate.setUTCHours(0, 0, 0, 0);
      while (tickDate.getTime() < seg.startMs) {
        tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
      }
      while (tickDate.getTime() <= seg.endMs) {
        const x = timeToX(tickDate.toISOString());
        const line = document.createElement('div');
        line.className = 'gridline';
        line.style.left = x + 'px';
        container.appendChild(line);
        tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
      }
    }
  }

  /* ── Insights ───────────────────────────────────────────── */

  function renderInsights() {
    const container = document.getElementById('insights-list');
    if (!container) return;
    container.innerHTML = '';

    const insights = data.insights || [];
    if (insights.length === 0) {
      container.innerHTML = '<div class="insight-item info">No specific insights detected.</div>';
      return;
    }

    for (const insight of insights) {
      const el = document.createElement('div');
      el.className = `insight-item ${insight.severity || 'info'}`;
      const icon = insight.severity === 'warning' ? '⚠️' : insight.severity === 'critical' ? '🔴' : 'ℹ️';
      el.innerHTML = `<span class="insight-icon">${icon}</span> ${escapeHtml(insight.description)}`;
      container.appendChild(el);
    }
  }

  /* ── Utilities ──────────────────────────────────────────── */

  function show(id) { document.getElementById(id)?.classList.remove('hidden'); }
  function hide(id) { document.getElementById(id)?.classList.add('hidden'); }

  function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  /**
   * Smart card grid: compute the right column count so cards either fit
   * in one row, or split evenly across exactly two full rows.
   */
  function fitCardGrid(container, count) {
    if (count <= 0) return;
    const width = container.clientWidth;
    const minCardW = 120, gap = 8;
    // Max cards that fit in one row
    const maxPerRow = Math.floor((width + gap) / (minCardW + gap));
    if (count <= maxPerRow) {
      // All fit in one row
      container.style.gridTemplateColumns = `repeat(${count}, 1fr)`;
    } else {
      // Split across 2 rows; use ceil(count/2) columns
      const cols = Math.ceil(count / 2);
      container.style.gridTemplateColumns = `repeat(${cols}, 1fr)`;
    }
  }

  return { render, escapeHtml, selectWindow };
})();
