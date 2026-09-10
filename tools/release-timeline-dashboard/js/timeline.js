/**
 * timeline.js
 * Core timeline renderer — swim lanes, time axis, event markers, idle gaps,
 * pipeline gap, gap compaction, collision resolution, zoom.
 */
const Timeline = (() => {
  let data = null;
  let timeRange = null;
  let contentWidth = 0;
  let zoomLevel = 1;
  const padding = { left: 20, right: 20 };
  let hiddenEventTypes = new Set(['bot_comment', 'label_added', 'ci_status']);
  let hiddenActors = new Set();
  let allActors = [];

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

  // Gap compaction — compress dead periods > threshold into narrow breaks
  const GAP_THRESHOLD_MS = 2 * 86400000; // 2 days
  const COMPRESSED_GAP_PX = 44;
  const MIN_SEGMENT_PX = 150;
  let segments = []; // { type:'active'|'gap', startMs, endMs, startPx, endPx, durationDays? }

  /* ── Public ─────────────────────────────────────────────── */

  function render(timelineData) {
    data = timelineData;
    timeRange = DataLoader.getTimeRange(data);
    hiddenActors.clear();
    document.querySelectorAll('.focus-notice').forEach(el => el.remove());

    renderHeader();
    renderSummaryCards();
    renderFilters();
    renderTimeline();
    renderInsights();

    show('header-info');
    hide('service-header');
    hide('service-window-selector');
    show('summary-cards');
    show('filters');
    show('timeline-container');
    show('insights-panel');
    hide('empty-state');
  }

  /* ── Header / Summary / Filters (unchanged) ────────────── */

  function renderHeader() {
    document.getElementById('timeline-title').textContent = data.title;
    const ownerEl = document.getElementById('meta-owner');
    ownerEl.textContent = `👤 ${data.owner}`;
    const days = data.summary?.totalDurationDays ||
      DataLoader.computeDurationDays(data.startDate, data.endDate);
    document.getElementById('meta-duration').textContent = `⏱ ${days} days`;
    const specLink = data.specPR?.url
      ? ` · <a href="${data.specPR.url}" target="_blank" class="header-spec-link" title="View spec PR on GitHub">#${data.specPR.number}</a>`
      : '';
    document.getElementById('meta-dates').innerHTML =
      `📅 ${DataLoader.formatDate(data.startDate)} → ${DataLoader.formatDate(data.endDate)}${specLink}`;
  }

  // Compute total active time excluding draft phases
  function computeActiveDays(d) {
    const allPRs = [d.specPR, ...(d.sdkPRs || [])].filter(p => p && p.createdAt && p.state !== 'missing');
    let earliest = Infinity, latest = 0;
    for (const pr of allPRs) {
      const start = pr.readyForReviewAt || pr.createdAt;
      const end = pr.mergedAt || pr.closedAt || d.endDate;
      const s = new Date(start).getTime();
      const e = new Date(end).getTime();
      if (s < earliest) earliest = s;
      if (e > latest) latest = e;
    }
    if (earliest === Infinity || latest === 0) return null;
    return Math.round(((latest - earliest) / 86400000) * 100) / 100;
  }

  function renderSummaryCards() {
    const container = document.getElementById('summary-cards');
    container.innerHTML = '';
    const s = data.summary || {};

    // Compute PR edits for manual PRs if not in summary
    if (s.totalPREdits === undefined && data.sdkPRs) {
      s.totalPREdits = 0;
      for (const pr of data.sdkPRs) {
        if (pr.generationFlow === 'manual') {
          const commits = pr.events.filter(e =>
            e.type === 'commit_pushed' &&
            !(e.description || '').toLowerCase().includes('merge')
          );
          // Subtract 1 for the initial commit (first is opening, rest are edits)
          s.totalPREdits += Math.max(0, commits.length - 1);
        }
      }
    }

    const fmt = (v, suffix = 'd') => v != null ? `${v}${suffix}` : '—';

    const cards = [
      { label: 'Spec PR', value: fmt(s.specPRDays), sub: 'API review', cls: 'info' },
      { label: 'Pipeline Gap', value: fmt(s.pipelineGapDays), sub: 'Merge → SDK PRs', cls: s.pipelineGapDays > 7 ? 'critical' : 'warning' },
      { label: 'Slowest SDK', value: fmt(s.slowestSDKPR?.days), sub: s.slowestSDKPR?.language || '', cls: 'warning' },
      { label: 'Fastest SDK', value: fmt(s.fastestSDKPR?.days), sub: s.fastestSDKPR?.language || '', cls: 'positive' },
      { label: 'Total', value: fmt(s.totalDurationDays || DataLoader.computeDurationDays(data.startDate, data.endDate)), sub: 'End to end', cls: 'info' },
      { label: 'Review Wait', value: fmt(s.totalReviewWaitDays), sub: `${s.totalReviewWaitCycles || 0} wait cycles`, cls: s.totalReviewWaitDays > 7 ? 'critical' : s.totalReviewWaitDays > 3 ? 'warning' : 'info' },
      { label: 'Nags', value: `${s.totalNags || 0}`, sub: 'Author nudges', cls: s.totalNags > 0 ? 'warning' : 'positive' },
      { label: 'Manual Fixes', value: `${s.totalManualFixes || 0}`, sub: 'On auto PRs', cls: s.totalManualFixes > 0 ? 'warning' : 'positive' },
      { label: 'PR Edits', value: `${s.totalPREdits || 0}`, sub: 'On manual PRs', cls: s.totalPREdits > 0 ? 'warning' : 'positive' },
      { label: 'Reviewers', value: `${s.totalUniqueReviewers != null ? s.totalUniqueReviewers : '—'}`, sub: 'Unique people', cls: 'info' },
    ];

    // Show Active Time card only if any PRs had draft phases
    if (s.hasDraftPRs) {
      // Compute active time: sum of (readyForReview or created) → (merged or now) per PR
      const activeDays = computeActiveDays(data);
      const totalDays = s.totalDurationDays || DataLoader.computeDurationDays(data.startDate, data.endDate);
      if (activeDays != null && Math.abs(activeDays - totalDays) >= 1) {
        cards.splice(5, 0, {
          label: 'Active Time',
          value: fmt(activeDays),
          sub: 'Excl. draft phases',
          cls: 'info'
        });
      }
    }

    // Add release cards if release data exists
    if (s.avgReleaseGapDays != null || s.pendingReleases != null) {
      if (s.avgReleaseGapDays != null) {
        cards.push({
          label: 'Release Gap',
          value: fmt(s.avgReleaseGapDays),
          sub: 'Avg merge → publish',
          cls: s.avgReleaseGapDays > 3 ? 'warning' : 'positive'
        });
      }
      if (s.pendingReleases != null && s.pendingReleases > 0) {
        cards.push({
          label: 'Pending',
          value: `${s.pendingReleases}`,
          sub: 'Unreleased packages',
          cls: 'critical'
        });
      }
    }

    // Add tool call cards if tool_call events exist
    const allToolCalls = DataLoader.getAllEvents(data).filter(e => e.type === 'tool_call');
    if (allToolCalls.length > 0) {
      const successCount = allToolCalls.filter(e => e.details?.success !== false).length;
      const failCount = allToolCalls.length - successCount;
      const agentCount = allToolCalls.filter(e => e.details?.clientType === 'agent').length;
      const humanCount = allToolCalls.length - agentCount;
      const successRate = Math.round(successCount * 100 / allToolCalls.length);
      cards.push({
        label: 'Tool Calls',
        value: `${allToolCalls.length}`,
        sub: `${successRate}% success · ${humanCount}👤 ${agentCount}🤖`,
        cls: failCount > 0 ? 'warning' : 'positive'
      });
    }

    for (const card of cards) {
      const el = document.createElement('div');
      el.className = `summary-card ${card.cls}`;
      el.innerHTML = `
        <div class="card-label">${card.label}</div>
        <div class="card-value">${card.value}</div>
        <div class="card-sub">${card.sub}</div>
      `;
      container.appendChild(el);
    }

    fitCardGrid(container, cards.length);
  }

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

    // Toggle all button
    const allBtn = document.createElement('button');
    const allOn = hiddenEventTypes.size === 0;
    allBtn.className = `filter-btn toggle-all ${allOn ? 'active' : ''}`;
    allBtn.textContent = allOn ? 'Hide All' : 'Show All';
    allBtn.addEventListener('click', () => {
      const nowAllOn = hiddenEventTypes.size === 0;
      if (nowAllOn) {
        types.forEach(t => hiddenEventTypes.add(t));
      } else {
        hiddenEventTypes.clear();
      }
      updateEventVisibility();
      renderFilters();
    });
    container.appendChild(allBtn);

    // Smart View preset button
    const isSmartView = [...SMART_VIEW_TYPES].every(t => !hiddenEventTypes.has(t)) &&
      types.filter(t => !SMART_VIEW_TYPES.has(t)).every(t => hiddenEventTypes.has(t));
    const smartBtn = document.createElement('button');
    smartBtn.className = `filter-btn preset-btn ${isSmartView ? 'active' : ''}`;
    smartBtn.textContent = '⚡ Smart View';
    smartBtn.title = 'Show only high-signal events: reviews, nags, fixes, releases, idle gaps';
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
      btn.dataset.type = type;
      const swatch = `<span class="legend-swatch"><span class="event-marker ${type}"></span></span>`;
      btn.innerHTML = `${swatch} ${info.label}`;
      btn.addEventListener('click', () => {
        toggleFilter(type, btn);
        // Update toggle-all button text
        const toggleBtn = container.querySelector('.toggle-all');
        if (toggleBtn) {
          const nowAll = hiddenEventTypes.size === 0;
          toggleBtn.className = `filter-btn toggle-all ${nowAll ? 'active' : ''}`;
          toggleBtn.textContent = nowAll ? 'Hide All' : 'Show All';
        }
      });
      container.appendChild(btn);
    }

    // Actor filter section
    renderActorFilters();

    // Bar legend
    renderBarLegend();
  }

  function renderBarLegend() {
    let container = document.getElementById('bar-legend');
    if (!container) return;
    // Ensure bar-legend is after actor-filters if it exists
    const actorSection = document.getElementById('actor-filters');
    if (actorSection && actorSection.nextSibling !== container) {
      actorSection.parentNode.insertBefore(container, actorSection.nextSibling);
    }
    const items = [
      { cls: 'pr-bar merged', label: 'Merged PR' },
      { cls: 'pr-bar open', label: 'Open PR' },
      { cls: 'pr-bar closed', label: 'Closed PR' },
      { cls: 'pr-bar-draft', label: 'Draft phase' },
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

  function toggleFilter(type, btn) {
    if (hiddenEventTypes.has(type)) {
      hiddenEventTypes.delete(type);
      btn.classList.add('active');
    } else {
      hiddenEventTypes.add(type);
      btn.classList.remove('active');
    }
    updateEventVisibility();
  }

  // Known bot/system actors
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

    // Collect all unique actors and their event counts
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

    // People section
    const peopleLabel = document.createElement('span');
    peopleLabel.className = 'filters-label';
    peopleLabel.textContent = '👤 People:';
    section.appendChild(peopleLabel);

    const peopleContainer = document.createElement('div');
    peopleContainer.className = 'filter-buttons actor-filter-buttons';
    section.appendChild(peopleContainer);

    // Show All / Hide All
    const allBtn = document.createElement('button');
    const allVisible = hiddenActors.size === 0;
    allBtn.className = `filter-btn toggle-all ${allVisible ? 'active' : ''}`;
    allBtn.textContent = allVisible ? 'Hide All' : 'Show All';
    allBtn.addEventListener('click', () => {
      if (hiddenActors.size === 0) {
        allActors.forEach(a => hiddenActors.add(a));
      } else {
        hiddenActors.clear();
      }
      updateEventVisibility();
      renderActorFilters();
    });
    peopleContainer.appendChild(allBtn);

    const owner = data.owner?.toLowerCase();

    for (const actor of humanActors) {
      peopleContainer.appendChild(makeActorBtn(actor, actorCounts[actor], owner));
    }

    // Bots & Systems section
    if (botActors.length > 0) {
      const botLabel = document.createElement('span');
      botLabel.className = 'filters-label bot-label';
      botLabel.textContent = '🤖 Bots & Systems:';
      section.appendChild(botLabel);

      const botContainer = document.createElement('div');
      botContainer.className = 'filter-buttons actor-filter-buttons';
      section.appendChild(botContainer);

      for (const actor of botActors) {
        botContainer.appendChild(makeActorBtn(actor, actorCounts[actor], owner));
      }
    }
  }

  function makeActorBtn(actor, count, owner) {
    const btn = document.createElement('button');
    const isActive = !hiddenActors.has(actor);
    const isOwner = actor.toLowerCase() === owner;
    btn.className = `filter-btn actor-btn ${isActive ? 'active' : ''} ${isOwner ? 'owner' : ''}`;
    btn.innerHTML = `${isOwner ? '👤 ' : ''}${escapeHtml(actor)} <span class="actor-count">${count}</span>`;
    btn.title = `${actor}: ${count} events${isOwner ? ' (PR owner)' : ''}`;
    btn.addEventListener('click', () => {
      if (hiddenActors.has(actor)) {
        hiddenActors.delete(actor);
        btn.classList.add('active');
      } else {
        hiddenActors.add(actor);
        btn.classList.remove('active');
      }
      updateEventVisibility();
      // Update toggle button
      const toggle = document.querySelector('.actor-filters .toggle-all');
      if (toggle) {
        const nowAll = hiddenActors.size === 0;
        toggle.className = `filter-btn toggle-all ${nowAll ? 'active' : ''}`;
        toggle.textContent = nowAll ? 'Hide All' : 'Show All';
      }
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

  function buildSegments() {
    const allPRs = DataLoader.getAllPRs(data);
    const ts = new Set();

    for (const pr of allPRs) {
      if (pr.state === 'missing' || !pr.createdAt) continue;
      ts.add(new Date(pr.createdAt).getTime());
      if (pr.mergedAt) ts.add(new Date(pr.mergedAt).getTime());
      if (pr.closedAt) ts.add(new Date(pr.closedAt).getTime());
      if (pr.release?.releasedAt) ts.add(new Date(pr.release.releasedAt).getTime());
      for (const ev of pr.events) {
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

    // Cluster timestamps into active segments separated by gaps > threshold
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

    // Pad active segments (8% of duration, capped 2h–12h each side)
    for (const seg of segments) {
      if (seg.type === 'active') {
        const dur = Math.max(seg.endMs - seg.startMs, 3600000);
        const pad = Math.max(2 * 3600000, Math.min(dur * 0.08, 12 * 3600000));
        seg.startMs -= pad;
        seg.endMs += pad;
      }
    }

    // Clamp gap boundaries to padded active segments
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

    // Ensure contentWidth can fit all segments
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

    // Update contentWidth to actual rendered width
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
    // Clamp to edges
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
    const steps = [0.5, 0.75, 1, 1.25, 1.5, 2, 3];
    let idx = steps.findIndex(s => Math.abs(s - zoomLevel) < 0.01);
    if (idx === -1) idx = 2;
    idx = Math.max(0, Math.min(steps.length - 1, idx + direction));
    zoomLevel = steps[idx];
    renderTimeline();
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

    const availWidth = scrollEl.clientWidth;
    contentWidth = Math.max(availWidth * zoomLevel, 600);

    // Build segments for gap compaction (this updates contentWidth)
    buildSegments();

    // Now set lanes container width to the final content width
    lanesContainer.style.minWidth = contentWidth + 'px';

    // Zoom controls
    renderZoomControls();

    // Time axes
    renderTimeAxis('time-axis');
    renderTimeAxis('time-axis-bottom');

    // Spec PR lane
    renderLane(lanesContainer, labelsContainer, data.specPR, true);

    // Pipeline gap connector
    renderPipelineGap(lanesContainer);

    // SDK PR lanes sorted by merge date (missing PRs last)
    const sdkPRs = [...data.sdkPRs].sort((a, b) => {
      const aMissing = a.state === 'missing' || !a.createdAt;
      const bMissing = b.state === 'missing' || !b.createdAt;
      if (aMissing && !bMissing) return 1;
      if (!aMissing && bMissing) return -1;
      if (aMissing && bMissing) return 0;
      const aDate = a.mergedAt || a.closedAt || a.createdAt;
      const bDate = b.mergedAt || b.closedAt || b.createdAt;
      return new Date(aDate) - new Date(bDate);
    });
    for (const pr of sdkPRs) renderLane(lanesContainer, labelsContainer, pr, false);

    // Gridlines
    addGridlines(lanesContainer);

    // Sync label/lane heights to prevent subpixel border drift
    {
      const labels = document.querySelectorAll('#lane-labels .lane-label');
      const lanes = document.querySelectorAll('#lanes .lane');
      const count = Math.min(labels.length, lanes.length);
      for (let i = 0; i < count; i++) { labels[i].style.height = ''; lanes[i].style.height = ''; }
      for (let i = 0; i < count; i++) {
        const h = Math.max(labels[i].offsetHeight, lanes[i].offsetHeight);
        labels[i].style.height = h + 'px';
        lanes[i].style.height = h + 'px';
      }
    }

    // Restore scroll positions
    if (scrollEl) scrollEl.scrollLeft = savedScrollLeft;
    window.scrollTo(window.scrollX, savedPageScrollY);
  }

  function renderTimeAxis(elementId) {
    const axis = document.getElementById(elementId);
    axis.innerHTML = '';
    axis.style.width = contentWidth + 'px';

    for (const seg of segments) {
      if (seg.type === 'active') {
        // Add date ticks within this active segment
        const segDays = (seg.endMs - seg.startMs) / 86400000;
        let intervalDays;
        if (segDays <= 3) intervalDays = 1;
        else if (segDays <= 14) intervalDays = 2;
        else if (segDays <= 45) intervalDays = 7;
        else intervalDays = 14;

        const tickDate = new Date(seg.startMs);
        tickDate.setUTCHours(0, 0, 0, 0);
        // Advance to first tick within segment
        while (tickDate.getTime() < seg.startMs) {
          tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
        }
        while (tickDate.getTime() <= seg.endMs) {
          const x = timeToX(tickDate.toISOString());
          const tick = document.createElement('div');
          tick.className = 'time-tick';
          if (tickDate.getUTCDate() === 1 || intervalDays >= 7) tick.classList.add('major');
          tick.style.left = x + 'px';
          tick.textContent = DataLoader.formatDate(tickDate.toISOString());
          axis.appendChild(tick);
          tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
        }
      } else {
        // Gap break indicator
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

  function renderLane(container, labelsContainer, pr, isSpec) {
    const isMissing = pr.state === 'missing' || (!pr.createdAt && !isSpec);
    const laneIndex = container.children.length;
    const lane = document.createElement('div');
    lane.className = `lane ${isSpec ? 'spec-lane' : ''} ${isMissing ? 'missing-lane' : ''}`;
    if (!isSpec && !isMissing && pr.release?.status === 'released') lane.classList.add('released');
    lane.dataset.laneIndex = laneIndex;

    // Label (goes to fixed labels column)
    const label = document.createElement('div');
    label.className = `lane-label ${isSpec ? 'spec-lane' : ''} ${isMissing ? 'missing-lane' : ''}`;
    if (!isSpec && !isMissing && pr.release?.status === 'released') label.classList.add('released');
    label.dataset.laneIndex = laneIndex;

    // Hover coordination between label and lane
    const addHoverSync = () => {
      lane.addEventListener('mouseenter', () => label.classList.add('hover'));
      lane.addEventListener('mouseleave', () => label.classList.remove('hover'));
      label.addEventListener('mouseenter', () => { lane.classList.add('hover'); label.classList.add('hover'); });
      label.addEventListener('mouseleave', () => { lane.classList.remove('hover'); label.classList.remove('hover'); });
    };
    const langClass = pr.language ? pr.language.toLowerCase().replace('.', '') : 'spec';
    const langText = pr.language || 'TypeSpec';

    if (isMissing) {
      label.innerHTML = `
        <div class="lane-repo"><span class="missing-pr">—</span></div>
        <div class="lane-meta">
          <span class="lane-language ${langClass}">${langText}</span>
          <span class="missing-label" title="No SDK PR was generated for this language">no PR</span>
        </div>
      `;
      labelsContainer.appendChild(label);
      const content = document.createElement('div');
      content.className = 'lane-content';
      content.style.width = contentWidth + 'px';
      const placeholder = document.createElement('div');
      placeholder.className = 'missing-placeholder';
      placeholder.textContent = 'No SDK PR generated';
      content.appendChild(placeholder);
      lane.appendChild(content);
      container.appendChild(lane);
      addHoverSync();
      return;
    }

    const prDays = pr.mergedAt
      ? DataLoader.computeDurationDays(pr.createdAt, pr.mergedAt)
      : null;
    // Color-code duration: green <3d, yellow 3-7d, orange 7-14d, red >14d
    const durationColorClass = prDays != null
      ? prDays < 3 ? 'dur-fast' : prDays < 7 ? 'dur-ok' : prDays < 14 ? 'dur-slow' : 'dur-critical'
      : '';
    const prDaysDisplay = prDays != null
      ? `<span class="duration-value ${durationColorClass}">${prDays}d</span>`
      : (pr.state === 'open' ? '<span class="status-pulse">⏳ open</span>' : pr.state === 'closed' ? '✖ closed' : '—');
    // Tooltip for the duration label showing created/merged dates
    const durationTooltip = pr.mergedAt
      ? `PR open ${prDays} days (${DataLoader.formatDate(pr.createdAt)} → ${DataLoader.formatDate(pr.mergedAt)})`
      : pr.state === 'open' ? `PR still open (since ${DataLoader.formatDate(pr.createdAt)})` : 'PR not merged';
    // For spec PR: compute time to first SDK PR opening
    let specToSdkHtml = '';
    if (isSpec && data.sdkPRs && data.sdkPRs.length > 0) {
      const sdkCreatedDates = data.sdkPRs
        .filter(sdk => sdk.createdAt)
        .map(sdk => new Date(sdk.createdAt).getTime());
      if (sdkCreatedDates.length > 0 && pr.mergedAt) {
        const firstSdkDate = Math.min(...sdkCreatedDates);
        const gapDays = DataLoader.computeDurationDays(pr.mergedAt, new Date(firstSdkDate).toISOString());
        specToSdkHtml = `<span class="spec-to-sdk" title="Time from spec merge to first SDK PR opened">→ SDK: ${gapDays}d</span>`;
      }
    }
    const releaseStatus = pr.release
      ? pr.release.status === 'released'
        ? `<span class="release-badge released" title="Released ${DataLoader.formatDate(pr.release.releasedAt)}">${pr.release.releaseGapDays ? '📦 ' + pr.release.releaseGapDays + 'd' : '📦 &lt;1d'}</span>`
        : pr.release.status === 'pending'
          ? '<span class="release-badge pending status-pulse" title="Release pending — merged but not published">⏳ pending</span>'
          : '<span class="release-badge failed" title="Release pipeline failed">❌ failed</span>'
      : '';
    // Build release link for lane label
    let releaseLinkHtml = '';
    if (pr.release?.pipelineUrl) {
      releaseLinkHtml = ` <a href="${pr.release.pipelineUrl}" target="_blank" title="Release pipeline: ${pr.release.pipelineName || ''}">🔗</a>`;
    }
    // Flow type indicator (automated=bot, manual=human)
    const flowIcon = !isSpec && pr.generationFlow
      ? pr.generationFlow === 'automated'
        ? '<span class="flow-badge automated" title="Automated (pipeline-generated)">🤖</span>'
        : '<span class="flow-badge manual" title="Manual (human-authored)">👤</span>'
      : '';
    // Draft PR indicator
    const draftBadge = pr.isDraft
      ? '<span class="draft-badge" title="Draft PR — not yet ready for review">draft</span>'
      : '';
    // Review wait per PR
    const reviewWaitHtml = pr.reviewWaitDays != null && pr.reviewWaitDays > 0
      ? `<span class="review-wait-badge" title="Time waiting for reviewer response (${pr.reviewWaitCycles || 0} cycles)">⏳ ${pr.reviewWaitDays}d wait</span>`
      : '';
    label.innerHTML = `
      <div class="lane-repo">
        <a href="${pr.url}" target="_blank" rel="noopener">#${pr.number}</a>${releaseLinkHtml}
        ${draftBadge}
      </div>
      <div class="lane-meta">
        <span class="lane-language ${langClass}">${langText}</span>
        ${flowIcon}
        <span title="${durationTooltip}">${prDaysDisplay}</span>
        ${reviewWaitHtml}
        ${specToSdkHtml}
        ${releaseStatus}
      </div>
    `;
    labelsContainer.appendChild(label);

    // Content area
    const content = document.createElement('div');
    content.className = 'lane-content';
    content.style.width = contentWidth + 'px';

    // Gap break indicators in lane
    for (const seg of segments) {
      if (seg.type === 'gap') {
        const breakEl = document.createElement('div');
        breakEl.className = 'lane-break';
        breakEl.style.left = seg.startPx + 'px';
        breakEl.style.width = (seg.endPx - seg.startPx) + 'px';
        content.appendChild(breakEl);
      }
    }

    // PR duration bar
    const barStart = timeToX(pr.createdAt);
    const barEnd = timeToX(pr.mergedAt || pr.closedAt || data.endDate);
    const bar = document.createElement('div');
    bar.className = `pr-bar ${pr.state === 'merged' ? 'merged' : ''} ${pr.state === 'open' ? 'open' : ''} ${pr.state === 'closed' && !pr.mergedAt ? 'closed' : ''}`;
    bar.style.left = barStart + 'px';
    bar.style.width = Math.max(barEnd - barStart, 4) + 'px';
    content.appendChild(bar);

    // Draft phase hatching overlay (from pr_created to ready_for_review)
    if (pr.readyForReviewAt) {
      const draftEnd = timeToX(pr.readyForReviewAt);
      const draftWidth = Math.max(draftEnd - barStart, 0);
      if (draftWidth > 0) {
        const draftBar = document.createElement('div');
        draftBar.className = 'pr-bar-draft';
        draftBar.style.left = barStart + 'px';
        draftBar.style.width = draftWidth + 'px';
        draftBar.title = `Draft phase: ${DataLoader.formatDate(pr.createdAt)} → ${DataLoader.formatDate(pr.readyForReviewAt)}`;
        content.appendChild(draftBar);
      }
    } else if (pr.isDraft) {
      // Currently still a draft — entire bar is draft
      const draftBar = document.createElement('div');
      draftBar.className = 'pr-bar-draft';
      draftBar.style.left = barStart + 'px';
      draftBar.style.width = Math.max(barEnd - barStart, 4) + 'px';
      draftBar.title = 'PR is still in draft state';
      content.appendChild(draftBar);
    }

    // Release segment bar (extends from merge to release)
    if (pr.release && pr.mergedAt && pr.release.releasedAt) {
      const mergeX = timeToX(pr.mergedAt);
      let releaseEnd = timeToX(pr.release.releasedAt);
      const status = pr.release.status || 'released';

      // Clamp: release can't render before merge (CSV dates are midnight approx)
      if (releaseEnd <= mergeX) releaseEnd = mergeX + 8;

      const barWidth = Math.max(releaseEnd - mergeX, 4);
      const releaseBar = document.createElement('div');
      releaseBar.className = `release-bar ${status}`;
      releaseBar.style.left = mergeX + 'px';
      releaseBar.style.width = barWidth + 'px';
      releaseBar.title = status === 'released'
        ? `Released ${DataLoader.formatDate(pr.release.releasedAt)} (${pr.release.releaseGapDays != null ? pr.release.releaseGapDays + 'd' : 'same day'} after merge)`
        : `❌ Release pipeline failed`;
      content.appendChild(releaseBar);
    }

    // Idle gaps
    const idleEvents = pr.events.filter(e => e.type === 'idle_gap');
    for (const gap of idleEvents) {
      const gapStart = timeToX(gap.timestamp);
      const gapEnd = timeToX(gap.endTimestamp);
      const hours = gap.details?.durationHours || 0;
      const gapEl = document.createElement('div');
      gapEl.className = `idle-gap ${hours > 72 ? 'critical' : 'warning'}`;
      gapEl.style.left = gapStart + 'px';
      gapEl.style.width = Math.max(gapEnd - gapStart, 4) + 'px';
      gapEl.dataset.eventType = 'idle_gap';
      gapEl.title = `${DataLoader.formatDuration(hours)} idle`;
      if (hiddenEventTypes.has('idle_gap')) gapEl.classList.add('hidden');
      content.appendChild(gapEl);
    }

    // Event markers
    const events = pr.events.filter(e => e.type !== 'idle_gap');
    const markers = [];
    for (const event of events) {
      const x = timeToX(event.timestamp);
      const marker = document.createElement('div');
      marker.className = `event-marker ${event.type}`;
      if (hiddenEventTypes.has(event.type)) marker.classList.add('hidden');
      // Tool call sub-classes for success/fail and agent/human
      if (event.type === 'tool_call' && event.details) {
        marker.classList.add(event.details.success === false ? 'tool-fail' : 'tool-success');
        if (event.details.clientType === 'agent') marker.classList.add('tool-agent');
      }
      marker.style.left = x + 'px';
      marker.dataset.eventType = event.type;
      marker.title = '';
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

    // Resolve overlapping markers
    resolveCollisions(markers);

    lane.appendChild(content);
    container.appendChild(lane);
    addHoverSync();
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

    const MIN_SPACING = 14; // markers closer than this (px) are a cluster

    // Sort by x position
    const sorted = [...markers].sort((a, b) => a._x - b._x);

    // Group into clusters of overlapping markers
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

    // Stagger each cluster vertically
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

  /* ── Pipeline Gap / Gridlines ───────────────────────────── */

  function renderPipelineGap(container) {
    const validPRs = data.sdkPRs.filter(pr => pr.createdAt);
    if (!validPRs.length) return;
    const specMergedAt = data.specPR.mergedAt;
    if (!specMergedAt) return;

    const earliestSDK = validPRs.reduce((earliest, pr) =>
      new Date(pr.createdAt) < new Date(earliest.createdAt) ? pr : earliest
    );

    const gapDays = DataLoader.computeDurationDays(specMergedAt, earliestSDK.createdAt);
    if (gapDays < 0.5) return;

    const x = timeToX(specMergedAt);
    const gapLine = document.createElement('div');
    gapLine.className = 'pipeline-gap';
    gapLine.style.left = x + 'px';
    gapLine.style.top = '0';
    gapLine.style.height = '100%';
    container.style.position = 'relative';
    container.appendChild(gapLine);

    // Only show label if the gap is NOT compressed (< threshold)
    const hasCompressedGap = segments.some(s => s.type === 'gap');
    if (!hasCompressedGap) {
      const label = document.createElement('div');
      label.className = 'pipeline-gap-label';
      label.textContent = `↕ Pipeline gap: ${gapDays}d`;
      label.style.left = (x + 6) + 'px';
      label.style.top = '4px';
      container.appendChild(label);
    }
  }

  function addGridlines(container) {
    // Only add gridlines within active segments
    for (const seg of segments) {
      if (seg.type !== 'active') continue;

      const segDays = (seg.endMs - seg.startMs) / 86400000;
      let intervalDays;
      if (segDays <= 3) intervalDays = 1;
      else if (segDays <= 14) intervalDays = 2;
      else if (segDays <= 45) intervalDays = 7;
      else intervalDays = 14;

      const tickDate = new Date(seg.startMs);
      tickDate.setUTCHours(0, 0, 0, 0);
      while (tickDate.getTime() < seg.startMs)
        tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);

      while (tickDate.getTime() <= seg.endMs) {
        const x = timeToX(tickDate.toISOString());
        const gridline = document.createElement('div');
        gridline.className = 'gridline';
        gridline.style.left = x + 'px';
        container.appendChild(gridline);
        tickDate.setUTCDate(tickDate.getUTCDate() + intervalDays);
      }
    }
  }

  /* ── Insights ───────────────────────────────────────────── */

  function renderInsights() {
    const list = document.getElementById('insights-list');
    list.innerHTML = '';

    if (!data.insights || !data.insights.length) {
      hide('insights-panel');
      return;
    }

    const iconMap = {
      bottleneck: '🔴',
      nag: '⏰',
      manual_fix: '🔧',
      idle: '⏳',
      positive: '✅',
      summary: '📊',
      release_delay: '📦',
      release_pending: '⚠️',
      tool_usage: '⚙️',
      observation: '💡'
    };

    // Group by severity: critical → warning → info
    const severityOrder = { critical: 0, warning: 1, info: 2 };
    const grouped = { critical: [], warning: [], info: [] };
    for (const insight of data.insights) {
      const sev = insight.severity || 'info';
      (grouped[sev] || grouped.info).push(insight);
    }

    const severityLabels = {
      critical: '🚨 Blocking Issues',
      warning: '⚠️ Warnings',
      info: 'ℹ️ Observations'
    };

    for (const sev of ['critical', 'warning', 'info']) {
      const items = grouped[sev];
      if (!items.length) continue;

      const groupHeader = document.createElement('div');
      groupHeader.className = `insight-group-header ${sev}`;
      groupHeader.textContent = `${severityLabels[sev]} (${items.length})`;
      list.appendChild(groupHeader);

      for (const insight of items) {
        const item = document.createElement('div');
        item.className = `insight-item ${sev}`;

        let refHtml = '';
        if (insight.prRef) {
          const match = insight.prRef.match(/^(.+?)#(\d+)$/);
          if (match) {
            const url = `https://github.com/${match[1]}/pull/${match[2]}`;
            refHtml = `<a href="${url}" target="_blank">${escapeHtml(insight.prRef)}</a>`;
          } else {
            refHtml = escapeHtml(insight.prRef);
          }
        }

        // Build metadata line: timestamp + prRef + duration
        const metaParts = [];
        if (insight.startDate) {
          const dateStr = insight.endDate
            ? `${DataLoader.formatDate(insight.startDate)} → ${DataLoader.formatDate(insight.endDate)}`
            : DataLoader.formatDate(insight.startDate);
          metaParts.push(`🕐 ${dateStr}`);
        }
        if (refHtml) metaParts.push(refHtml);
        if (insight.durationDays) metaParts.push(`${insight.durationDays}d`);

        const metaHtml = metaParts.length
          ? `<div class="insight-meta">${metaParts.join(' · ')}</div>`
          : '';

        item.innerHTML = `
          <span class="insight-icon">${iconMap[insight.type] || '💡'}</span>
          <div>
            <div class="insight-text">${escapeHtml(insight.description)}</div>
            ${metaHtml}
          </div>
        `;
        list.appendChild(item);
      }
    }
  }

  /* ── Utilities ──────────────────────────────────────────── */

  function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
  }

  function show(id) {
    document.getElementById(id)?.classList.remove('hidden');
  }

  function hide(id) {
    document.getElementById(id)?.classList.add('hidden');
  }

  function fitCardGrid(container, count) {
    if (count <= 0) return;
    const width = container.clientWidth;
    const minCardW = 120, gap = 8;
    const maxPerRow = Math.floor((width + gap) / (minCardW + gap));
    if (count <= maxPerRow) {
      container.style.gridTemplateColumns = `repeat(${count}, 1fr)`;
    } else {
      const cols = Math.ceil(count / 2);
      container.style.gridTemplateColumns = `repeat(${cols}, 1fr)`;
    }
  }

  return { render, escapeHtml };
})();
