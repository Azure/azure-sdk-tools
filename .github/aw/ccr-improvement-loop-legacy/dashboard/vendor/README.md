# Vendored Chart.js

`chart.umd.min.js` is a **pinned, committed** copy of Chart.js, loaded by the
dashboard via a local `<script>` tag. There is **no runtime CDN dependency** — the
dashboard works fully offline and under a GitHub Pages subpath.

- **Version:** 4.4.9
- **Source:** https://cdn.jsdelivr.net/npm/chart.js@4.4.9/dist/chart.umd.min.js
- **License:** MIT (Chart.js) — https://github.com/chartjs/Chart.js/blob/master/LICENSE.md

## Refreshing

To bump the version, download the new pinned build and replace the file, then update
the version + URL above:

```
curl -sSfL "https://cdn.jsdelivr.net/npm/chart.js@<new-version>/dist/chart.umd.min.js" \
  -o chart.umd.min.js
```

This is a deliberate, reviewable change — keep it in its own commit.
