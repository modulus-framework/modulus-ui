Modulus bundles Cropper.js as one classic script (IIFE), built from its ESM entry, so the page needs no module loader.

Bundled package (MIT, (c) 2015-present Chen Fengyuan; https://fengyuanchen.github.io/cropperjs):
  cropperjs 2.2.0                    the image cropper: a canvas, an image, a selection with handles

Cropper.js 2 injects its own styles, so there is no stylesheet to load.

Built with esbuild (the entry exposes window.ModulusCropper):

  npm install --save-exact cropperjs@2.2.0 esbuild@0.28.2
  cropper-entry.mjs:
    import Cropper from 'cropperjs';
    window.ModulusCropper = Cropper;
  esbuild cropper-entry.mjs --bundle --format=iife --minify --charset=utf8 --legal-comments=inline --outfile=cropper.js

The licence text is in LICENSE (MIT).
