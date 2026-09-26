// Prismedia stub. foliate-js is used here only for reflowable EPUB. PDFs are rendered
// server-side to page images and shown in the comic reader, so foliate never opens a PDF.
// This stub keeps makeBook()'s dynamic import resolvable without bundling pdf.js. See PROVENANCE.md.
export const makePDF = async () => {
  throw new Error("PDF rendering is handled server-side, not by foliate-js");
};
