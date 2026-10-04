Modulus bundles Tiptap as one classic script (IIFE) because Tiptap ships ESM only.

Bundled packages (all MIT, (c) 2018-present tiptap contributors — https://tiptap.dev):
  @tiptap/core 3.31.4                Editor
  @tiptap/starter-kit 3.31.4         bold, italic, underline, strike, headings,
                                     lists, blockquote, code blocks, link, undo/redo
  @tiptap/extension-image 3.31.4     <img> from a URL
  @tiptap/extension-placeholder 3.31.4

Built with esbuild; see docs/UPGRADING.md ("Upgrading MapLibre, Tiptap, Univer or jsVectorMap").

  npm install esbuild @tiptap/core @tiptap/starter-kit @tiptap/extension-image @tiptap/extension-placeholder
  tiptap-entry.mjs:
    import { Editor } from '@tiptap/core';
    import { StarterKit } from '@tiptap/starter-kit';
    import Image from '@tiptap/extension-image';
    import Placeholder from '@tiptap/extension-placeholder';
    window.ModulusTiptap = { Editor, StarterKit, Image, Placeholder };
  esbuild tiptap-entry.mjs --bundle --format=iife --minify --charset=utf8 --legal-comments=inline --outfile=tiptap.js

The bundle exposes window.ModulusTiptap; modulus-plugins.js ([data-m-editor]) starts it.
