import { getPipelineRuns, getPipelineSummaries } from "./lib/run-metadata.js";

export function mountPipelines(app, vallyApp, db) {
  app.get("/", (context) => context.html(landingHtml(getPipelineSummaries(db))));

  app.get("/all", async (context) => {
    const response = await vallyApp.request("http://localhost/", {
      headers: { host: "localhost" },
    });
    return context.html(injectBanner(await response.text(), "All pipelines"));
  });

  app.get("/p/:repository/:pipeline", async (context) => {
    const repository = context.req.param("repository");
    const pipeline = context.req.param("pipeline");
    const runs = getPipelineRuns(db, repository, pipeline);
    if (runs.length === 0) {
      return context.redirect("/");
    }

    const response = await vallyApp.request("http://localhost/", {
      headers: { host: "localhost" },
    });
    const title = `${repository} / ${pipeline}`;
    return context.html(injectBanner(injectPipelineRuns(await response.text(), runs), title));
  });

  app.get("/api/dashboard/pipelines/:repository/:pipeline/runs", (context) => {
    const runs = getPipelineRuns(
      db,
      context.req.param("repository"),
      context.req.param("pipeline")
    );
    return context.json({
      items: runs,
      page: { limit: runs.length, hasMore: false, total: runs.length },
    });
  });
}

function injectPipelineRuns(htmlText, runs) {
  const runIds = JSON.stringify(runs.map((run) => run.id));
  const script = `<script>
  (function () {
    var allowedRunIds = new Set(${runIds});
    var originalFetch = window.fetch.bind(window);
    window.fetch = function (input, init) {
      var url = typeof input === "string" ? input : (input && input.url) || "";
      var pathname;
      try { pathname = new URL(url, window.location.origin).pathname; }
      catch (error) { pathname = url; }
      var response = originalFetch(input, init);
      if (pathname === "/api/runs") {
        return response.then(function (value) {
          return value.clone().json().then(function (data) {
            if (data && Array.isArray(data.items)) {
              data.items = data.items.filter(function (run) {
                return run && allowedRunIds.has(run.id);
              });
              if (data.page) {
                data.page.total = data.items.length;
                data.page.hasMore = false;
                delete data.page.nextCursor;
              }
            }
            return new Response(JSON.stringify(data), {
              status: value.status,
              headers: { "Content-Type": "application/json" },
            });
          });
        });
      }
      return response;
    };
  })();
  </script>`;
  return htmlText.replace(/<head>/i, `<head>\n${script}`);
}

function injectBanner(htmlText, title) {
  const style = `<style>
  .dashboard-breadcrumb { display:flex; align-items:center; gap:0.75rem; min-height:52px; padding:0.65rem 2rem; background:#11131a; border-bottom:1px solid #2a2d3a; position:sticky; top:0; z-index:50; font-family:'Inter',-apple-system,system-ui,sans-serif; font-size:0.9rem; }
  .dashboard-breadcrumb a { color:#f59e0b; text-decoration:none; font-weight:600; }
  .dashboard-breadcrumb a:hover { color:#fbbf24; }
  .dashboard-breadcrumb .divider { color:#56606b; }
  .dashboard-breadcrumb .current { color:#f5f5f4; font-weight:600; overflow-wrap:anywhere; }
  @media (max-width: 700px) {
    body, .container { max-width: 100%; overflow-x: hidden; }
    .two-col { grid-template-columns: minmax(0, 1fr) !important; }
    section.card { min-width: 0; }
    section.card:has(table) { overflow-x: auto; }
    .dashboard-breadcrumb { padding:0.65rem 1rem; font-size:0.8rem; }
  }
  </style>`;
  const script = `<script>
  document.addEventListener("DOMContentLoaded", function () {
    document.title = ${JSON.stringify(`${title} | vally eval dashboard`)};
    var banner = document.createElement("div");
    banner.className = "dashboard-breadcrumb";
    banner.innerHTML = '<a href="/">Pipelines</a>' + '<span class="divider">/</span>' + '<span class="current">' + ${JSON.stringify(title)} + '</span>';
    document.body.insertBefore(banner, document.body.firstChild);
  });
  </script>`;
  return htmlText.replace(/<\/head>/i, `${style}\n${script}\n</head>`);
}

function landingHtml(summaries) {
  const byRepository = new Map();
  for (const summary of summaries) {
    const pipelines = byRepository.get(summary.repository) ?? [];
    pipelines.push(summary);
    byRepository.set(summary.repository, pipelines);
  }

  const repositoryEntries = [...byRepository.entries()].sort(([left], [right]) => {
    if (left === "azure-sdk-tools") return -1;
    if (right === "azure-sdk-tools") return 1;
    return left.localeCompare(right);
  });
  const repositoryCount = repositoryEntries.length;
  const pipelineCount = summaries.length;
  const runCount = summaries.reduce(
    (total, summary) => total + Number(summary.run_count ?? 0),
    0
  );
  const groups = repositoryEntries.map(([repository, pipelines]) => `
    <section class="repository">
      <div class="repository-header">
        <h2>${escapeHtml(repository)}</h2>
        <span>${pipelines.length} ${pipelines.length === 1 ? "pipeline" : "pipelines"}</span>
      </div>
      <div class="grid">
        ${pipelines.map((pipeline) => pipelineCard(repository, pipeline)).join("")}
      </div>
    </section>`).join("");

  const empty = summaries.length === 0
    ? '<p class="empty">No pipeline artifacts have been ingested yet. Start the local POC source or add a manifest-backed result run.</p>'
    : groups;

  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>vally eval dashboard | pipelines</title>
    <style>
      @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
      *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
      :root {
        --bg-base: #fafaf9;
        --bg-surface: #ffffff;
        --bg-elevated: #f5f5f4;
        --border: #e7e5e4;
        --border-subtle: #f0eeec;
        --text: #1c1917;
        --text-muted: #78716c;
        --text-faint: #a8a29e;
        --accent: #b45309;
        --accent-hover: #92400e;
        --radius: 8px;
      }
      body { min-height:100vh; color:var(--text); background:var(--bg-base); font-family:'Inter',-apple-system,system-ui,sans-serif; line-height:1.5; -webkit-font-smoothing:antialiased; }
      .dashboard-breadcrumb { display:flex; align-items:center; min-height:52px; padding:0.65rem 2rem; color:#f5f5f4; background:#11131a; border-bottom:1px solid #2a2d3a; font-size:0.9rem; font-weight:600; }
      .container { max-width:1440px; margin:0 auto; padding:1.5rem 2rem 3rem; }
      .page-header { margin-bottom:1.5rem; padding-bottom:1.2rem; border-bottom:1px solid var(--border); }
      h1 { font-size:1.1rem; font-weight:600; color:var(--text); }
      h1 span { color:var(--accent); }
      .sub { margin-top:0.35rem; color:var(--text-muted); font-size:0.78rem; }
      .stats-row { display:flex; gap:1.5rem; align-items:baseline; margin-bottom:2rem; padding:0.8rem 0; flex-wrap:wrap; border-bottom:1px solid var(--border); }
      .stat-inline { display:flex; align-items:baseline; gap:0.35rem; }
      .stat-inline .value { font-size:1.1rem; font-weight:700; }
      .stat-inline .label { color:var(--text-faint); font-size:0.72rem; }
      .stat-sep { width:1px; height:1.2rem; background:var(--border); align-self:center; }
      .repository { margin-bottom:1.8rem; }
      .repository-header { display:flex; align-items:baseline; justify-content:space-between; gap:1rem; margin-bottom:0.8rem; padding-bottom:0.6rem; border-bottom:1px solid var(--border); }
      .repository-header h2 { color:var(--text-muted); font-size:0.75rem; font-weight:600; text-transform:uppercase; letter-spacing:0.06em; overflow-wrap:anywhere; }
      .repository-header span { color:var(--text-faint); font-size:0.7rem; }
      .grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(270px,1fr)); gap:0.8rem; }
      .pipeline { display:flex; min-height:154px; padding:1.2rem 1.4rem; flex-direction:column; color:inherit; background:var(--bg-surface); border:1px solid var(--border-subtle); border-radius:var(--radius); text-decoration:none; transition:border-color 0.15s ease,background 0.15s ease; }
      .pipeline:hover { background:#fffdf9; border-color:#d6c7b8; }
      .pipeline:focus-visible { outline:2px solid var(--accent); outline-offset:2px; }
      .pipeline h3 { color:var(--text); font-size:0.9rem; font-weight:600; overflow-wrap:anywhere; }
      .pipeline-details { display:flex; align-items:flex-end; justify-content:space-between; gap:1rem; margin-top:1.1rem; }
      .metric { display:flex; align-items:baseline; gap:0.35rem; }
      .metric strong { font-size:1.35rem; font-weight:700; }
      .metric span { color:var(--text-faint); font-size:0.7rem; }
      .last { max-width:160px; color:var(--text-faint); font-size:0.68rem; text-align:right; }
      .go { display:flex; align-items:center; justify-content:space-between; margin-top:auto; padding-top:1rem; color:var(--accent); border-top:1px solid var(--border-subtle); font-size:0.72rem; font-weight:600; }
      .pipeline:hover .go { color:var(--accent-hover); }
      .empty { max-width:560px; padding:1.2rem 1.4rem; color:var(--text-muted); background:var(--bg-surface); border:1px solid var(--border-subtle); border-radius:var(--radius); font-size:0.78rem; }
      .footer { margin-top:2.2rem; padding-top:1rem; color:var(--text-faint); border-top:1px solid var(--border); font-size:0.72rem; }
      .footer a { color:var(--accent); text-decoration:none; font-weight:600; }
      .footer a:hover { color:var(--accent-hover); }
      @media (max-width:600px) {
        .dashboard-breadcrumb { padding:0.65rem 1rem; font-size:0.8rem; }
        .container { padding:1.2rem 1rem 2rem; }
        .stats-row { gap:0.8rem; }
        .grid { grid-template-columns:minmax(0,1fr); }
        .pipeline-details { align-items:flex-start; flex-direction:column; gap:0.35rem; }
        .last { max-width:none; text-align:left; }
      }
    </style>
  </head>
  <body>
    <div class="dashboard-breadcrumb">Pipelines</div>
    <main class="container">
      <header class="page-header">
        <h1><span>vally</span> eval dashboard</h1>
        <p class="sub">Choose a repository and pipeline to inspect its historical Vally results.</p>
      </header>
      <div class="stats-row" aria-label="Dashboard summary">
        <div class="stat-inline"><span class="value">${repositoryCount}</span><span class="label">repositories</span></div>
        <span class="stat-sep" aria-hidden="true"></span>
        <div class="stat-inline"><span class="value">${pipelineCount}</span><span class="label">pipelines</span></div>
        <span class="stat-sep" aria-hidden="true"></span>
        <div class="stat-inline"><span class="value">${runCount}</span><span class="label">runs</span></div>
      </div>
      ${empty}
      <p class="footer">Or browse <a href="/all">all runs, unfiltered</a>.</p>
    </main>
  </body>
</html>`;
}

function pipelineCard(repository, pipeline) {
  const path = `/p/${encodeURIComponent(repository)}/${encodeURIComponent(pipeline.pipeline)}`;
  const runLabel = pipeline.run_count === 1 ? "run" : "runs";
  return `<a class="pipeline" href="${path}">
    <h3>${escapeHtml(pipeline.pipeline)}</h3>
    <div class="pipeline-details">
      <p class="metric"><strong>${pipeline.run_count}</strong><span>${runLabel}</span></p>
      <p class="last">Last ingested ${escapeHtml(formatTimestamp(pipeline.last_run_at))}</p>
    </div>
    <span class="go"><span>View dashboard</span><span aria-hidden="true">&rarr;</span></span>
  </a>`;
}

function formatTimestamp(value) {
  if (!value) {
    return "unknown";
  }

  const timestamp = new Date(value);
  return Number.isNaN(timestamp.valueOf()) ? value : timestamp.toLocaleString();
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (character) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;",
  })[character]);
}