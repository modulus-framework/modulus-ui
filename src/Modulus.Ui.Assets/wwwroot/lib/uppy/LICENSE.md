Modulus bundles Uppy as one classic script (IIFE) because Uppy's packages ship ESM only.

Bundled packages (all MIT, (c) Transloadit; https://uppy.io):
  @uppy/core 6.0.2                   the Uppy engine (restrictions, events, uploads)
  @uppy/dashboard 6.0.0              the drop area, file list, previews and progress
  @uppy/xhr-upload 6.0.0             uploads each file with XMLHttpRequest (progress, one POST per file)

uppy.min.css is @uppy/core/dist/style.min.css followed by @uppy/dashboard/dist/style.min.css.

Built with esbuild (the entry exposes window.ModulusUppy = { Uppy, Dashboard, XHRUpload }):

  npm install --save-exact @uppy/core@6.0.2 @uppy/dashboard@6.0.0 @uppy/xhr-upload@6.0.0 esbuild@0.28.2
  uppy-entry.mjs:
    import { Uppy } from '@uppy/core';
    import Dashboard from '@uppy/dashboard';
    import XHRUpload from '@uppy/xhr-upload';
    window.ModulusUppy = { Uppy, Dashboard, XHRUpload };
  esbuild uppy-entry.mjs --bundle --format=iife --minify --charset=utf8 --legal-comments=inline --outfile=uppy.js

The licence text is in LICENSE (MIT, Transloadit).
