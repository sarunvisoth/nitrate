"""Regenerates tests/fixtures/media/books-pdf/nitrate-pdf-annotation-fixture.pdf (standard library only).

Builds a minimal, deterministic multi-page PDF by hand (base-14 Helvetica, no embedded fonts, no
external PDF library — mirrors make-annotation-epub.py's "stdlib only" fixture approach) so pdf.js's
text layer has real, searchable text to select and search. Lives in its own folder (not
tests/fixtures/media/books/, the EPUB fixture's library root) so it gets its own initial library
scan instead of silently waiting on a root that already scanned once before this file existed.

Run from the repo root: python3 e2e/fixtures/make-annotation-pdf.py
"""
from pathlib import Path

OUT = Path("tests/fixtures/media/books-pdf/nitrate-pdf-annotation-fixture.pdf")
TITLE = "Nitrate PDF Annotation Fixture"

PAGE_WIDTH, PAGE_HEIGHT = 612, 792  # US Letter, points

PAGES = [
    ["The harbour woke slowly under a pale sky.", "Fishermen counted their nets in silence."],
    ["Along the quay the luminous lanterns still glowed.", "A gull argued with the wind above the lighthouse."],
    ["Mara walked the length of the pier twice.", "She noticed a rare paperclip tucked under her door."],
]


def escape(text: str) -> str:
    return text.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")


def content_stream(lines: list[str]) -> bytes:
    ops = ["BT", "/F1 18 Tf"]
    y = 700
    for line in lines:
        ops.append(f"1 0 0 1 72 {y} Tm")
        ops.append(f"({escape(line)}) Tj")
        y -= 40
    ops.append("ET")
    return ("\n".join(ops)).encode("latin-1")


def build_pdf(pages: list[list[str]]) -> bytes:
    objects: list[bytes] = [b""]  # 1-indexed; index 0 unused

    def add(obj: bytes) -> int:
        objects.append(obj)
        return len(objects) - 1

    font_id = add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    page_ids = []
    pages_id = len(objects) + len(pages) * 2  # placeholder: content + page object per page, then Pages itself
    content_ids = []
    for lines in pages:
        stream = content_stream(lines)
        content_ids.append(add(b"<< /Length %d >>\nstream\n" % len(stream) + stream + b"\nendstream"))
    for index, _ in enumerate(pages):
        page_ids.append(add(
            b"<< /Type /Page /Parent %d 0 R /MediaBox [0 0 %d %d] "
            b"/Resources << /Font << /F1 %d 0 R >> >> /Contents %d 0 R >>"
            % (pages_id, PAGE_WIDTH, PAGE_HEIGHT, font_id, content_ids[index])
        ))
    kids = " ".join(f"{pid} 0 R" for pid in page_ids)
    actual_pages_id = add(b"<< /Type /Pages /Kids [%s] /Count %d >>" % (kids.encode(), len(pages)))
    assert actual_pages_id == pages_id, "page id math drifted; adjust the placeholder computation"
    catalog_id = add(b"<< /Type /Catalog /Pages %d 0 R >>" % pages_id)
    info_id = add(b"<< /Title (%s) >>" % escape(TITLE).encode("latin-1"))

    out = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for i in range(1, len(objects)):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % i + objects[i] + b"\nendobj\n"
    xref_offset = len(out)
    out += b"xref\n0 %d\n" % len(objects)
    out += b"0000000000 65535 f \n"
    for offset in offsets[1:]:
        out += b"%010d 00000 n \n" % offset
    out += b"trailer\n<< /Size %d /Root %d 0 R /Info %d 0 R >>\n" % (len(objects), catalog_id, info_id)
    out += b"startxref\n%d\n%%%%EOF" % xref_offset
    return bytes(out)


OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_bytes(build_pdf(PAGES))
print(f"wrote {OUT} ({OUT.stat().st_size} bytes, {len(PAGES)} pages)")
