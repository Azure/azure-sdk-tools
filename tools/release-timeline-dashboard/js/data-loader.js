/**
 * data-loader.js
 * Loads and validates timeline JSON data (per-release and service-timeline formats).
 */
const DataLoader = (() => {
  const REQUIRED_FIELDS = ['title', 'owner', 'startDate', 'endDate', 'specPR', 'sdkPRs'];
  const SERVICE_REQUIRED_FIELDS = ['type', 'service', 'startDate', 'endDate', 'specPRs', 'sdkPRs'];
  const VALID_EVENT_TYPES = [
    'pr_created', 'pr_merged', 'pr_closed', 'commit_pushed',
    'review_approved', 'review_changes_requested', 'review_comment',
    'issue_comment', 'bot_comment', 'author_nag', 'reviewer_responds',
    'label_added', 'manual_fix', 'idle_gap', 'ci_status',
    'release_pipeline_started', 'release_pipeline_completed',
    'release_pipeline_failed', 'release_pending',
    'tool_call'
  ];

  function isServiceTimeline(data) {
    return data.type === 'service-timeline';
  }

  function validate(data) {
    const errors = [];
    if (isServiceTimeline(data)) {
      for (const field of SERVICE_REQUIRED_FIELDS) {
        if (!(field in data)) errors.push(`Missing required field: ${field}`);
      }
      if (!Array.isArray(data.specPRs)) errors.push('specPRs must be an array');
      if (typeof data.sdkPRs !== 'object') errors.push('sdkPRs must be an object');
    } else {
      for (const field of REQUIRED_FIELDS) {
        if (!(field in data)) errors.push(`Missing required field: ${field}`);
      }
      if (data.specPR && !data.specPR.events) errors.push('specPR must have an events array');
      if (data.sdkPRs && !Array.isArray(data.sdkPRs)) errors.push('sdkPRs must be an array');
    }
    return errors;
  }

  function getAllPRs(data) {
    if (isServiceTimeline(data)) {
      return [...data.specPRs, ...Object.values(data.sdkPRs).flat()];
    }
    return [data.specPR, ...data.sdkPRs];
  }

  function getAllEvents(data) {
    const prs = getAllPRs(data);
    const events = [];
    for (const pr of prs) {
      for (const event of (pr.events || [])) {
        events.push({ ...event, pr });
      }
    }
    return events.sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
  }

  function getTimeRange(data) {
    return {
      start: new Date(data.startDate),
      end: new Date(data.endDate)
    };
  }

  function getEventTypeInfo(type) {
    const info = {
      pr_created:                { icon: '🟢', label: 'PR Created',            color: 'green' },
      pr_merged:                 { icon: '🟣', label: 'PR Merged',             color: 'purple' },
      pr_closed:                 { icon: '⚫', label: 'PR Closed',             color: 'gray' },
      commit_pushed:             { icon: '🔵', label: 'Commit Pushed',         color: 'blue' },
      review_approved:           { icon: '✅', label: 'Approved',              color: 'green' },
      review_changes_requested:  { icon: '🔴', label: 'Changes Requested',    color: 'red' },
      review_comment:            { icon: '💬', label: 'Review Comment',        color: 'yellow' },
      issue_comment:             { icon: '💬', label: 'Comment',               color: 'gray' },
      bot_comment:               { icon: '🤖', label: 'Bot Comment',           color: 'gray' },
      author_nag:                { icon: '⏰', label: 'Author Nudge',          color: 'orange' },
      reviewer_responds:         { icon: '💬', label: 'Reviewer Response',     color: 'blue' },
      label_added:               { icon: '🏷️', label: 'Label Added',           color: 'teal' },
      manual_fix:                { icon: '🔧', label: 'Manual Fix',            color: 'orange' },
      idle_gap:                  { icon: '⏳', label: 'Idle Gap',              color: 'red' },
      ci_status:                 { icon: '⚙️', label: 'CI Status',             color: 'gray' },
      release_pipeline_started:  { icon: '🚀', label: 'Release Started',       color: 'teal' },
      release_pipeline_completed:{ icon: '📦', label: 'Released',              color: 'teal' },
      release_pipeline_failed:   { icon: '❌', label: 'Release Failed',        color: 'red' },
      release_pending:           { icon: '⏳', label: 'Release Pending',       color: 'orange' },
      tool_call:                 { icon: '⚙️', label: 'Tool Call',             color: 'cyan' },
      ready_for_review:          { icon: '🚀', label: 'Ready for Review',     color: 'green' },
      convert_to_draft:          { icon: '📝', label: 'Converted to Draft',   color: 'gray' }
    };
    return info[type] || { icon: '❓', label: type, color: 'gray' };
  }

  function computeDurationDays(startDate, endDate) {
    const ms = new Date(endDate) - new Date(startDate);
    return Math.round((ms / (1000 * 60 * 60 * 24)) * 10) / 10;
  }

  function formatDate(dateStr, opts) {
    const d = new Date(dateStr);
    const format = { month: 'short', day: 'numeric' };
    if (opts?.year) format.year = '2-digit';
    return d.toLocaleDateString('en-US', format);
  }

  function formatDateTime(dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit'
    });
  }

  function formatDateRange(startStr, endStr) {
    const s = new Date(startStr);
    const e = new Date(endStr);
    const crossYear = s.getUTCFullYear() !== e.getUTCFullYear();
    const opts = crossYear ? { year: true } : {};
    return `${formatDate(startStr, opts)} → ${formatDate(endStr, opts)}`;
  }

  function formatDuration(hours) {
    if (hours < 1) return `${Math.round(hours * 60)}m`;
    if (hours < 24) return `${Math.round(hours)}h`;
    const days = Math.round(hours / 24 * 10) / 10;
    return `${days}d`;
  }

  async function loadFromUrl(url) {
    // fetch() doesn't work with file:// protocol; fall back to XMLHttpRequest
    if (window.location.protocol === 'file:') {
      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('GET', url, true);
        xhr.onload = () => {
          if (xhr.status === 0 || xhr.status === 200) {
            try {
              const data = JSON.parse(xhr.responseText);
              const errors = validate(data);
              if (errors.length) return reject(new Error(`Validation errors:\n${errors.join('\n')}`));
              resolve(data);
            } catch (e) { reject(e); }
          } else {
            reject(new Error(`Failed to load: ${xhr.status}`));
          }
        };
        xhr.onerror = () => reject(new Error(
          'Cannot load data files from file:// protocol.\n\n' +
          'Run a local server:\n  python3 -m http.server 4173\n\n' +
          'Then open http://localhost:4173'
        ));
        xhr.send();
      });
    }
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Failed to fetch: ${response.status}`);
    const data = await response.json();
    const errors = validate(data);
    if (errors.length) throw new Error(`Validation errors:\n${errors.join('\n')}`);
    return data;
  }

  function loadFromString(jsonStr) {
    const data = JSON.parse(jsonStr);
    const errors = validate(data);
    if (errors.length) throw new Error(`Validation errors:\n${errors.join('\n')}`);
    return data;
  }

  return {
    validate,
    isServiceTimeline,
    getAllPRs,
    getAllEvents,
    getTimeRange,
    getEventTypeInfo,
    computeDurationDays,
    formatDate,
    formatDateRange,
    formatDateTime,
    formatDuration,
    loadFromUrl,
    loadFromString,
    VALID_EVENT_TYPES
  };
})();
