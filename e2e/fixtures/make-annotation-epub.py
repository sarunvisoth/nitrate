"""Regenerates tests/fixtures/media/books/nitrate-annotation-fixture.epub (standard library only).

Run from the repo root: python3 e2e/fixtures/make-annotation-epub.py
"""
import zipfile
from pathlib import Path

OUT = Path("tests/fixtures/media/books/nitrate-annotation-fixture.epub")

PARAS = [
    "The harbour woke slowly under a pale sky, and the fishermen counted their nets in silence.",
    "Along the quay the luminous harbor lanterns still glowed, though the morning had already come.",
    "A gull argued with the wind above the lighthouse, losing every point it tried to make.",
    "Mara walked the length of the pier twice before she noticed the letter tucked under her door.",
]
MORE = [
    "The second chapter begins where the tide turns, carrying small boats out past the breakwater.",
    "Nobody in the village could remember who first painted the doors of the chapel a bright green.",
    "By noon the market was loud with bargaining, and the smell of bread drifted down to the water.",
]


def chapter(title: str, paragraphs: list[str]) -> str:
    body = "\n".join(f"    <p>{p} {p} {p}</p>" for p in paragraphs)
    return f"""<?xml version="1.0" encoding="utf-8"?>
<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="en">
  <head><title>{title}</title></head>
  <body>
    <h1>{title}</h1>
{body}
  </body>
</html>
"""


FILES = {
    "META-INF/container.xml": """<?xml version="1.0"?>
<container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
  <rootfiles>
    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml" />
  </rootfiles>
</container>
""",
    "OEBPS/content.opf": """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="uid">
  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
    <dc:identifier id="uid">urn:uuid:5b3b0e7a-7c1e-4d0e-9c7a-3f0a8e2b1a01</dc:identifier>
    <dc:title>Nitrate Annotation Fixture</dc:title>
    <dc:creator>Nitrate Tests</dc:creator>
    <dc:language>en</dc:language>
    <meta property="dcterms:modified">2026-09-26T00:00:00Z</meta>
  </metadata>
  <manifest>
    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav" />
    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml" />
    <item id="ch2" href="ch2.xhtml" media-type="application/xhtml+xml" />
  </manifest>
  <spine>
    <itemref idref="ch1" />
    <itemref idref="ch2" />
  </spine>
</package>
""",
    "OEBPS/nav.xhtml": """<?xml version="1.0" encoding="utf-8"?>
<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="en">
  <head><title>Contents</title></head>
  <body>
    <nav epub:type="toc"><ol>
      <li><a href="ch1.xhtml">The Harbour</a></li>
      <li><a href="ch2.xhtml">The Tide</a></li>
    </ol></nav>
  </body>
</html>
""",
    "OEBPS/ch1.xhtml": chapter("The Harbour", PARAS * 3),
    "OEBPS/ch2.xhtml": chapter("The Tide", MORE * 3),
}

OUT.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(OUT, "w") as epub:
    epub.writestr(zipfile.ZipInfo("mimetype"), "application/epub+zip", compress_type=zipfile.ZIP_STORED)
    for name, content in FILES.items():
        epub.writestr(name, content, compress_type=zipfile.ZIP_DEFLATED)
print(f"wrote {OUT} ({OUT.stat().st_size} bytes)")
