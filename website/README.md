# Modulus UI docs site

The documentation site for Modulus UI, built with Docusaurus (the same stack as the Modulus framework site). The pages are in `docs/`, the landing page in `src/pages/`, and the sample projects they show are in `../docs/examples/`.

## Run it locally

```bash
cd website
npm install
npm start          # http://localhost:3000/modulus-ui/
```

## Build it

```bash
npm run build      # writes build/
npm run serve      # serves build/ to check the result
```

The build fails on any broken link or anchor, so a renamed heading or a moved page is caught before it is published.

## Deploy

**GitHub Pages (automatic).** `.github/workflows/docs-site.yml` builds the site and deploys it on every push to `main` that changes `website/`. Turn on Pages in the repository settings with **Source: GitHub Actions**. The site is served at `https://modulus-framework.github.io/modulus-ui/`.

**Cloudflare Pages.** Create a Pages project from the repository with these settings:

| Setting | Value |
| --- | --- |
| Root directory | `website` |
| Build command | `npm ci && npm run build` |
| Build output directory | `build` |
| Environment variable | `DOCS_BASE_URL` = `/` |
| Environment variable | `DOCS_URL` = your project URL, such as `https://modulus-ui.pages.dev` |

With a custom domain, set `DOCS_URL` to that domain and keep `DOCS_BASE_URL` as `/`. The base URL and the site URL are read from these two variables in `docusaurus.config.ts`, so the same code builds for either host.

## Adding a page

Create a Markdown file in `docs/` with front matter, and set `sidebar_position` to place it in the sidebar:

```markdown
---
title: "My page"
description: "One sentence for search engines and link previews."
sidebar_position: 14
---
```

Code blocks name their language and, optionally, their file: ` ```csharp title="Program.cs" `. Keep the code in the tutorials identical to the files in `../docs/examples/`, so a reader can build the app from either.
