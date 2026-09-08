// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Adds a category landing page and per-category scoped vally reports on top of
// the built-in vally dashboard. The pipeline runs are named
//   <timestamp>-<category>-build<id>
// where <category> is one of the tokens below. A category report is just the
// normal vally dashboard with the run list filtered (client-side) to runs whose
// id contains that category token, so every chart, the run selector, the "All"
// aggregate, and model comparison are all scoped to a single category.

// Ordered list of the test categories. `token` is the substring that
// appears in a run id; `slug` is the URL path segment.
export const CATEGORIES = [
  {
    slug: "code-quality",
    token: "code-quality",
    title: "Code Quality",
    blurb:
      "Generated TypeSpec is compiled and graded on correctness and quality. Pipeline 8178.",
  },
  {
    slug: "skill-invocation",
    token: "skill-invocation",
    title: "Skill Invocation",
    blurb:
      "Checks whether the agent invokes the required azure-typespec-author skill while solving each task. Pipeline 8178.",
  },
  {
    slug: "without-skill",
    token: "without-skill",
    title: "Without Skill (Pure Agent)",
    blurb:
      "Baseline: the agent solves the same tasks with no skill installed. Pipeline 8210.",
  },
  {
    slug: "agentic-search",
    token: "agentic-search",
    title: "Agentic Search Only",
    blurb:
      "The agent solves the same tasks using only agentic search (code-quality eval). Pipeline 8178.",
  },
  {
    slug: "new-knowledge-base",
    token: "new-knowledge-base",
    title: "Code Quality(New KB)",
    blurb:
      "The agent solves the same tasks using the new knowledge base.",
  },
];

const bySlug = new Map(CATEGORIES.map((c) => [c.slug, c]));

/**
 * Register the landing page and category routes on `app`, delegating the actual
 * report rendering to the wrapped vally `vallyApp`.
 */
export function mountCategories(app, vallyApp) {
  app.get("/", (c) => c.html(landingHtml()));

  // Full, unfiltered dashboard (all runs) — the vally dashboard lives at "/" in
  // the inner app, which we've shadowed, so expose it here.
  app.get("/all", async (c) => {
    const inner = await vallyApp.request("/");
    const text = injectBanner(await inner.text(), {
      title: "All runs",
      subtitle: "Every ingested run, unfiltered.",
    });
    return c.html(text);
  });

  app.get("/c/:cat", async (c) => {
    const cat = bySlug.get(c.req.param("cat"));
    if (!cat) return c.redirect("/");
    const inner = await vallyApp.request("/");
    let text = await inner.text();
    text = injectFilterScript(text, cat.token);
    text = injectBanner(text, {
      title: cat.title,
      subtitle: cat.blurb,
      category: cat.title,
    });
    return c.html(text);
  });
}

// ---------------------------------------------------------------------------
// HTML helpers
// ---------------------------------------------------------------------------

// Patches window.fetch so the run-list endpoint (/api/runs) only returns runs
// whose id contains the category token. Injected into <head> so it runs before
// the dashboard's own script issues any request.
function injectFilterScript(htmlText, token) {
  const script = `<script>
  (function () {
    var TOKEN = ${JSON.stringify(token)};
    var origFetch = window.fetch.bind(window);
    window.fetch = function (input, init) {
      var url = typeof input === "string" ? input : (input && input.url) || "";
      var pathname;
      try { pathname = new URL(url, window.location.origin).pathname; }
      catch (e) { pathname = url; }
      var p = origFetch(input, init);
      if (pathname === "/api/runs") {
        return p.then(function (resp) {
          return resp.clone().json().then(function (data) {
            if (data && Array.isArray(data.items)) {
              data.items = data.items.filter(function (r) {
                return r && typeof r.id === "string" && r.id.indexOf(TOKEN) !== -1;
              });
            }
            return new Response(JSON.stringify(data), {
              status: resp.status,
              headers: { "Content-Type": "application/json" },
            });
          });
        });
      }
      return p;
    };
  })();
  </script>`;
  return htmlText.replace(/<head>/i, `<head>\n${script}`);
}

// Adds a fixed top banner with a link back to the category overview. Done via a
// small DOMContentLoaded script so it doesn't depend on the dashboard markup.
function injectBanner(htmlText, { title, subtitle, category }) {
  const label = category ? `${title} report` : title;
  const script = `<script>
  document.addEventListener("DOMContentLoaded", function () {
    document.title = ${JSON.stringify(`${title} — vally dashboard`)};
    var bar = document.createElement("div");
    bar.setAttribute("style",
      "display:flex;align-items:center;gap:12px;padding:10px 20px;" +
      "background:#11131a;border-bottom:1px solid #2a2d3a;position:sticky;top:0;z-index:50;");
    bar.innerHTML =
      '<a href="/" style="color:#7c9cff;text-decoration:none;font-weight:600;">\\u2190 Categories</a>' +
      '<span style="color:#566;">/</span>' +
      '<span style="color:#e6e6ee;font-weight:600;">' + ${JSON.stringify(label)} + '</span>';
    document.body.insertBefore(bar, document.body.firstChild);
  });
  </script>`;
  void subtitle;
  return htmlText.replace(/<\/head>/i, `${script}\n</head>`);
}

function landingHtml() {
  const cards = CATEGORIES.map(
    (c) => `
      <a class="cat-card" href="/c/${c.slug}">
        <h2>${escapeHtml(c.title)}</h2>
        <p>${escapeHtml(c.blurb)}</p>
        <span class="go">Open report &rarr;</span>
      </a>`,
  ).join("");

  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>vally eval dashboard — categories</title>
    <style>
      @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
      * { box-sizing: border-box; margin: 0; padding: 0; }
      body {
        font-family: 'Inter', system-ui, sans-serif;
        background: radial-gradient(1200px 600px at 50% -10%, #1b1f2e, #0c0d12 60%);
        color: #e6e6ee; min-height: 100vh; padding: 64px 24px;
      }
      .wrap { max-width: 1040px; margin: 0 auto; }
      header { margin-bottom: 40px; }
      h1 { font-size: 34px; font-weight: 700; letter-spacing: -0.02em; }
      h1 span { color: #7c9cff; }
      .sub { color: #9aa0b4; margin-top: 8px; font-size: 15px; }
      .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px; margin-top: 28px; }
      .cat-card {
        display: block; text-decoration: none; color: inherit;
        background: #14161f; border: 1px solid #262a39; border-radius: 14px;
        padding: 24px; transition: border-color .15s ease, transform .15s ease, background .15s ease;
      }
      .cat-card:hover { border-color: #7c9cff; transform: translateY(-2px); background: #171a26; }
      .cat-card h2 { font-size: 20px; font-weight: 600; margin-bottom: 10px; }
      .cat-card p { color: #9aa0b4; font-size: 14px; line-height: 1.55; min-height: 84px; }
      .cat-card .go { display: inline-block; margin-top: 14px; color: #7c9cff; font-weight: 600; font-size: 14px; }
      .footer { margin-top: 36px; color: #9aa0b4; font-size: 14px; }
      .footer a { color: #7c9cff; text-decoration: none; font-weight: 600; }
      .footer a:hover { text-decoration: underline; }
    </style>
  </head>
  <body>
    <div class="wrap">
      <header>
        <h1><span>vally</span> eval dashboard</h1>
        <p class="sub">TypeSpec author skill benchmark — choose a test category to view its report.</p>
      </header>
      <div class="grid">${cards}</div>
      <p class="footer">Or browse <a href="/all">all runs &rarr;</a></p>
    </div>
  </body>
</html>`;
}

function escapeHtml(s) {
  return String(s).replace(
    /[&<>"']/g,
    (ch) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        ch
      ],
  );
}
