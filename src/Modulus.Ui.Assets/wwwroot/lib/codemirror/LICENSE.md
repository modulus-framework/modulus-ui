Modulus bundles CodeMirror 6 as one classic script (IIFE) because CodeMirror 6 ships ESM only.

Bundled packages (all MIT, (c) 2018-present Marijn Haverbeke and others; https://codemirror.net):
  @codemirror/view 6.43.13           the editor view
  @codemirror/state 6.7.6            editor state, compartments
  @codemirror/commands 6.11.1        default keymap, history, indent with Tab
  @codemirror/language 6.12.4        syntax highlighting, bracket matching, folding
  @codemirror/search 6.7.2           search keymap, highlighting of matching selections
  @codemirror/autocomplete 6.20.3    completion keymap, bracket closing
  @codemirror/lang-javascript 6.2.5  JavaScript and TypeScript
  @codemirror/lang-json 6.0.2        JSON
  @codemirror/lang-css 6.3.1         CSS
  @codemirror/lang-html 6.4.12       HTML
  @codemirror/theme-one-dark 6.1.3   the dark theme

Built with esbuild (the entry exposes window.ModulusCodeMirror = { mount, languages }; the licence text is in
the LICENSE file of @codemirror/view, and every package above carries the same MIT text):

  npm install --save-exact @codemirror/view@6.43.13 @codemirror/state@6.7.6 @codemirror/commands@6.11.1 \
    @codemirror/language@6.12.4 @codemirror/search@6.7.2 @codemirror/autocomplete@6.20.3 \
    @codemirror/lang-javascript@6.2.5 @codemirror/lang-json@6.0.2 @codemirror/lang-css@6.3.1 \
    @codemirror/lang-html@6.4.12 @codemirror/theme-one-dark@6.1.3 esbuild@0.28.2
  codemirror-entry.mjs: the imports above, with mount(parent, { doc, language, readOnly, dark, label, onChange })
  esbuild codemirror-entry.mjs --bundle --format=iife --minify --charset=utf8 --legal-comments=inline --outfile=codemirror.js
