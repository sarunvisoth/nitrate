// pdfjs-dist ships types at the package root but not for the deep build entry we import
// (to get the ESM build + worker URL). The reader uses it through a loose runtime handle.
// The legacy build is used deliberately (bundled core-js polyfills for very recent JS features
// pdf.js's own code assumes, e.g. Map.prototype.getOrInsertComputed) so PDFs still render on
// browsers that haven't shipped those features yet.
declare module "pdfjs-dist/legacy/build/pdf.mjs";
declare module "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url" {
  const url: string;
  export default url;
}
