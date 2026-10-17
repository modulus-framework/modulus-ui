---
title: "Plugins"
description: "The rich-text editor, maps, PDF viewer, spreadsheet, uploads, image cropper, code editor, media player and sortable lists, each as a Blazor component and an MVC tag helper."
sidebar_position: 9
---

Plugins are components for jobs a plain form cannot do: a rich-text editor, a map, a PDF viewer, a spreadsheet, uploads. Each one is a Blazor component and an MVC tag helper, over one shared renderer, so a page looks and posts the same way in both.

Third-party libraries load only on the pages that use them. `modulus-plugins.js` fetches a library the first time a page needs it, so a page with no editor does not download one.

| Plugin | Blazor | MVC tag helper | What it does |
| --- | --- | --- | --- |
| Rich-text editor | `AppEditor` | `app-editor` | WYSIWYG editor on Tiptap. The HTML is cleaned with `RichText.Sanitize(images: true)`. |
| Map | `AppMap` | `app-map` | Tile map on MapLibre GL JS, with markers and popups |
| Vector map | `AppVectorMap` | `app-vector-map` | Choropleth map on jsVectorMap, with hover values and a legend |
| PDF viewer | `AppPdf` | `app-pdf` | pdf.js viewer: one page at a time, zoom, and a download link |
| Spreadsheet | `AppSpreadsheet` | `app-spreadsheet` | Excel-style multi-sheet grid with formulas, on Univer. Posts its sheets as JSON. |
| Uploader | `AppUploader` | `app-uploader` | A drop area on Uppy. Each file uploads as soon as it is dropped, with previews, progress and limits. |
| Image cropper | `AppImageCropper` | `app-image-cropper` | Cropper.js, with aspect-ratio buttons. The crop goes to a form field as JSON. |
| Code editor | `AppCodeEditor` | `app-code-editor` | CodeMirror 6, with highlighting, folding and search for JavaScript, TypeScript, JSON, CSS and HTML |
| Media player | `AppMediaPlayer` | `app-media-player` | Video and audio, with custom controls, a poster and captions |
| Sortable list | `AppSortable` | `app-sortable` | Reorder a list, drag between lists, with handles and keyboard moves. Posts the new order. |
| Lightbox | `AppLightbox` | `app-lightbox` | Full-size image viewer |
| Barcode | `AppBarcode` | `app-barcode` | Code 128 and EAN-13, drawn as SVG |

## Notes on use

- **Upload.** `app-upload` also takes `action` (upload with per-file progress), `max-size`, `max-count` and `list-type="picture"`.
- **Forms.** Plugins that edit content post a hidden form field. The editor and the code editor post their text; the cropper posts JSON; the spreadsheet posts its sheets as JSON. Read the value on the server like any other form field.
- **Sanitize HTML.** The rich-text editor and the `AppRichText` field both produce HTML. Clean it with `RichText.Sanitize` before you store or show it.
- **Print.** The spreadsheet prints from its ribbon, with Ctrl+P, or from any button with `data-m-print="{id}"`. The document editor works the same way.
- **Themes.** The spreadsheet follows the light and dark theme.

## Maps need a tile server for real use

The map plugins load tiles from `tile.openstreetmap.org`. Its usage policy does not cover heavy production traffic, so they need network access and, for real use, your own tile server.

## Liquid templates

A Liquid template can draw the same icons, barcodes and status pills without any plugin. See [Liquid templates](./fluid.md).
