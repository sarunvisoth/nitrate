// pdfjs-dist ships types at the package root but not for the deep build entry we import
// (to get the ESM build + worker URL). The reader uses it through a loose runtime handle.
declare module "pdfjs-dist/build/pdf.mjs";
declare module "pdfjs-dist/build/pdf.worker.min.mjs?url" {
  const url: string;
  export default url;
}
