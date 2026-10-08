# Changelog

## [2.8.0] - 2026-10-09

_Device-level administrator access control through the PDF Studio Monitor._

### Added
- PDF Studio can be enabled or disabled per monitored Windows device from the administrator dashboard.
- Disabled devices show a clear in-app access lock and remain blocked until an administrator restores access.
- Access decisions are cached locally, so a previously disabled device stays locked even if the monitor is temporarily unreachable.
- New or never-seen devices remain allowed by default; PDF Studio does not require server authentication to start.
- The monitor records the access state, reason, administrator, timestamp and audit history for every policy change.
- Access is rechecked automatically every 30 seconds while PDF Studio is running.

### Changed
- The PDF Studio Monitor dashboard now includes per-device access status and Enable / Disable controls.
- Access changes are protected by the monitor's existing administrator login and CSRF protection.

All notable changes to PDF Studio are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/). Dates are in ISO 8601 format (YYYY-MM-DD).
The same entries are shown in the app under **Help (F1) › Version history**.

## [2.7.0] - 2026-10-08

_Smaller releases: the exe is zipped for upload and for in-app updates._

### Added
- The update window shows how big the download is before you start, and while downloading it shows the megabytes received, the percentage, the speed and the time left.

### Changed
- Updates now download a compressed package (about half the size of the exe) and PDF Studio unpacks it itself before restarting. If the package is unavailable or fails its check, the full exe is downloaded instead, so an update never gets stuck.
- Releasing uploads a zip of the signed exe instead of the exe itself, roughly halving the upload. The monitor verifies and unpacks it, and still keeps the plain exe for older copies.

## [2.6.6] - 2026-10-08

_One exe: everything is packaged inside PdfStudio.exe._

### Changed
- PDFium (pdfium.dll) and SkiaSharp (libSkiaSharp.dll) are now bundled inside PdfStudio.exe. Ship and update just the exe; no loose DLLs are needed beside it.

### Fixed
- PDF page previews no longer vanish after an exe-only copy or update, which is what caused the "Dll was not found" error.

## [2.6.5] - 2026-10-08

_Add images properly: they only become pages, and many at once is safe._

### Changed
- Add images now just adds the pictures to the workspace as pages (A4, fitted inside a 10 mm margin). No dialog opens and no PDF file is created or saved; the PDF is made when you export. Use Page size to change how the pages look.
- Convert > Images → PDF is the separate converter that asks for the layout and saves a PDF file. Its Result option is gone because it always saves a file.

### Fixed
- Adding many images no longer fails. Each picture is prepared on its own, so a single bad file is reported and skipped instead of stopping the rest, and memory use stays low.
- The "DLL was not found" error (PDFium) no longer appears. When pdfium.dll is missing, PDF pages show "Preview unavailable" with a clear status message; pictures you add are still drawn in the page grid, the preview and Edit PDF.

## [2.6.4] - 2026-10-08

_The monitor recognises a PC by its name, so one PC stays one install._

### Added
- Usage statistics now include the PC's name and its network (LAN) address. The monitor shows both next to every session and install.

### Fixed
- A PC no longer shows up as a new install when its settings file is lost or reset (reinstall, cleaned profile, another Windows user on the same PC). The monitor matches the PC name, returns the PC's original install id, and the app keeps using it.

## [2.6.3] - 2026-10-06

_Edit PDF: resizable side panel, a clearly visible text box and margin guides that match the page._

### Added
- Edit PDF: drag the thin bar between the page and the side panel to make the panel wider or narrower (double-click the bar to put it back). The width is remembered.
- Edit PDF › Page margins: a **Fit to page text** button (also in the Margins menu) that puts the guides exactly where the text on the page starts and ends.

### Changed
- Edit PDF: the margin guides of a page you have not adjusted yourself now follow the page's real text instead of a fixed 0.5 inch, so they line up with the page. Dragging a guide, typing a margin, Reset or Use on all pages makes them yours and they are left alone.
- Edit PDF: everything in the side panel stretches to the panel's width, so check boxes and buttons are never cut off and there is no sideways scroll bar.
- Help & user guide: topics now open through a new reader that is drawn in one pass, so the heading and first lines can no longer appear blank or half-painted.
- Help & user guide: a shimmering loading placeholder and a short fade-in play while a topic opens. Searching tints the matching paragraphs and jumps to the first one; Ctrl+C copies the topic.
- Update window: the release notes are shown as coloured Added / Changed / Fixed sections with a version header.

### Fixed
- Edit PDF: the text box being edited now has a clear frame (solid outline, soft band and four corner blocks). Before, the corner handles were half hidden behind the text box, which looked broken, and the box was hard to see on the page.
- Edit PDF: the frame can be dragged to move the text box, and the Apply / Cancel bar sits above the frame instead of on top of it.

## [2.6.2] - 2026-10-06

_PDF compression that hits a percentage or an exact size._

### Added
- Export PDF › Compress: choose how to compress. **Preset level** (as before), **Reduce by a percentage** (for example 50% makes the file half its normal size) or **Reduce to a target size (MB)**. PDF Studio tries the gentlest picture settings that reach the goal, measures the real file size and goes a little harsher if it is still over. After saving you see the before and after size, and a message if the goal cannot be reached.
- Compression now also handles pictures stored with PNG or TIFF predictors (common in PDFs made by Word, browsers and scanners' software), which were skipped before.

### Changed
- The Export window's Compress & protect tab is split into two tabs, **Compress** and **Protect**, so both fit comfortably.
- Pictures are re-encoded on several processor cores at once, so large scanned files compress much faster.

### Fixed
- Compression no longer converts pictures that could not be made smaller; a picture is only replaced when the new version is smaller (except when converting to grayscale).

## [2.6.1] - 2026-10-06

_Add PDF tile, a modern Export window and text-size fixes._

### Added
- An Add PDF placeholder tile at the end of the page thumbnails. Click it (or drop files on the grid) to add more PDFs.

### Changed
- The Export PDF window is redesigned: a cleaner tab bar, a heading and short description on every tab, consistent label and field alignment, fixed-width number fields, and the page-number and watermark fields are greyed out until the option is switched on.

### Fixed
- Edit PDF: applying bold, italic, underline, colour or a font to selected text no longer shrinks the text. The size was being read back from the editor in points instead of pixels, so every style change cut it to 75%.
- Export PDF: the labels on the Details, Watermark and other tabs were pushed to the bottom of the window and no longer lined up with their fields.
- Adding a PDF no longer selects all of its pages automatically; your current selection is left alone.

## [2.6.0] - 2026-10-05

_Reviews, usage statistics and a calmer empty window._

### Added
- **Review** button in the header opens a new review window: rate PDF Studio with 1–5 stars while a little face and the header colours react to your rating, tap the things that stood out, optionally add your name and a few words, and send it to the developer. Ctrl+Enter sends; problems (no rating, no connection) are shown inside the window and a short thank-you screen confirms the send.
- Anonymous usage statistics sent to the PDF Studio Monitor web app: how many times the app is opened and for how long. Only a random install id, the app version and the Windows version are sent - never files or file names. They are always on (there is no checkbox); the Review window states that they are included. Events that cannot be delivered offline are kept and re-sent the next time the app starts.

### Changed
- The preview pane is now hidden until at least one PDF or image page has been added, so the empty start screen is no longer split in two. It appears automatically with the first page (the Preview button still hides it).

## [2.5.1] - 2026-10-05

_Move text boxes freely; nicer drop-down menus._

### Added
- Edit PDF: a **Move** handle in the Editing text bar. Press and drag it to reposition the text box while you are typing. Cancel puts the text back where it was.
- Edit PDF: distance indicators like Canva. While you move, resize or draw, pink lines show the gap to the nearest text or object on each side (and to the page edge) with the value in your margin unit. New equal-spacing snap: the object clicks into place when the space on both sides is equal, or equal to a gap elsewhere on the page; a dashed suggestion shows it just before it snaps. Alt moves freely.
- Drop-down menus: icons next to the commands (text no longer overlaps them), a muted shortcut column, rounded corners, a softer highlight and roomier rows. The button stays highlighted while its menu is open.
- Drop-down lists (font, units, page size, format, scope…): a rounded field and a rounded, themed list with a check mark on the current choice, instead of the white system list. Works in light and dark theme.

### Fixed
- Edit PDF: pressing on a line of existing text and dragging now moves it. A plain click still edits it. Before, a click opened the editor straight away and the text could not be dragged.

## [2.5.0] - 2026-10-05

_Smart snapping, page margins and an Apply button for text editing._

### Added
- Edit PDF: an **Apply** / **Cancel** bar now floats above the text you are typing, so no keyboard shortcut is needed to finish (Ctrl+Enter also works). Cancel puts the text back exactly as it was.
- Edit PDF: **snapping**. Pictures, signatures, text, shapes and new objects click into place on the page margins, the page centre, other objects and the page's own text (left edge, end of line, same line). Pink guide lines with a label show what you snapped to; a dashed **suggestion** appears when you are close but not yet snapped. Hold Alt to move freely. The new Snap button in the toolbar turns snapping on or off.
- Edit PDF: **page margins** for the selected page. Show or hide the margin guides, drag a margin line on the page, or type exact values (inches, centimetres or points) in the new Page margins panel. Reset them, or use them on every page. Margins are guides only and are not printed.
- Edit PDF: **Align on page** buttons (Left, Centre, Right, Top, Middle, Bottom) and **Snap to best spot**, which moves the selected object to the nearest good position.

### Changed
- Edit PDF: clearer wording. The bottom button is now "Save changes", hints explain what to do next, and a short tip is shown when nothing is selected.

## [2.4.4] - 2026-10-05

_Edit PDF keeps the original text size; New button._

### Added
- New button in the ribbon (Ctrl+N): removes all pages and the undo history to start a fresh document. Asks first if there are unexported changes.

### Changed
- Ribbon: related buttons are grouped into drop-downs so the toolbar fits on one row. Pages (duplicate, insert blank / PDF, rotate 180°, reset rotation, delete by range), Arrange (move earlier / later / to start / end / to page number, reverse), Add to page (text, image, remove added items), Extract (extract, split, export selected) and More (export options, open in viewer). Rotate left / right and Delete stay as single buttons.

### Fixed
- Drop-down buttons: clicking the button again while its menu is open now closes it (it used to close and instantly re-open).
- Edit PDF: edited text now keeps the exact font size of the original. PDFs printed from a browser store a scale in the page that was ignored, so the replacement text came out about one third too large and ran past the margin.

## [2.4.3] - 2026-10-05

_Edit PDF text fix._

### Fixed
- Edit PDF: clicking existing text no longer squeezes the words together ("REWOODLANESStoreopening…"); the spaces between words are kept, so the text opens exactly as it reads in the PDF.
- Edit PDF: a line is no longer cut into pieces at commas and dots (e.g. "19,732.14" or "PRINTER,ECOTANK"), so one click selects the whole line.

## [2.4.2] - 2026-10-05

_Real Word tables in PDF → Word._

### Changed
- PDF → Word: tables are now rebuilt as real Word tables (rows, columns, merged cells, borders) with their text typed into the cells, instead of a table picture with loose text boxes on top. Cell text is ordinary, editable Word text; right-aligned and centred text (amounts, headings) keeps its alignment.
- PDF → Word: table borders are taken out of the background picture, so lines are never drawn twice.

### Fixed
- PDF → Word: text inside tables was converted to floating text boxes; it now lives inside the table cells.

## [2.4.1] - 2026-10-05

_Font accuracy release for PDF → Word and Edit PDF._

### Changed
- PDF → Word: the .docx records fallback fonts (alternate name, font class, PANOSE), so text keeps its width and appearance on PCs where the original font is not installed.
- Edit PDF: replacement text is mapped to the closest installed look-alike of the original font.

### Fixed
- PDF → Word: fonts are now identified from the font data embedded in the PDF, so converted documents use the document's real typeface instead of a generic substitute.
- PDF → Word: bold and italic faces are detected from the embedded font, replacing unreliable weight and flag values.

## [2.4.0] - 2026-10-04

_PDF → Word is now fully built in._

### Added
- Built-in PDF → Word (.docx) engine: each page keeps its exact size, text stays editable at its original position, and graphics, tables and photos are preserved behind it.

### Changed
- PDF → Word no longer depends on LibreOffice or any other external program.

## [2.3.0] - 2026-10-04

_Saved signature library._

### Added
- Saved signatures that persist between sessions and are placed in one click from the Signature menu in Edit PDF.
- Import a signature from a picture or scan; the paper background is removed automatically, with a live transparency preview.
- Draw a signature and keep it in the library; rename or delete entries from the management dialog.

### Removed
- Word → PDF conversion.

## [2.2.1] - 2026-10-04

_Text fidelity in Edit PDF._

### Changed
- The editor renders pages losslessly at 1:1 scale for a sharper, truer view.

### Fixed
- Edit PDF: replaced text now sits on the original baseline and pen origin, so it aligns exactly with the surrounding text.
- Edit PDF: edited text matches the weight and line width of the original.

## [2.2.0] - 2026-10-04

_Edit PDF window._

### Added
- Edit PDF window (Ctrl+E): edit existing text in place, add new text, and change font, size, style and colour.
- Insert pictures, logos and drawn signatures; move and resize any object with handles or exact values.
- Shapes: rectangle, ellipse, line, highlight, white-out and black box.
- Edits appear in the preview and are written to the exported PDF as vector text, with full undo / redo.

## [2.1.2] - 2026-10-03

_One page size for every page._

### Added
- Page size tool: set a single size for all pages (A3 to Tabloid, custom, or match the first / largest page), with orientation, fit options and a live preview.

### Changed
- Resized pages are redrawn as vector so text stays sharp; pages that already match are copied untouched.

## [2.1.1] - 2026-10-03

_Responsive layout._

### Changed
- Fully responsive interface: the ribbon, header and status bar adapt to the window width, down to a 600 × 460 minimum.
- The preview pane sits beside the pages on wide windows and below them on narrow ones; dialogs resize to fit small screens.

## [2.1.0] - 2026-10-03

_Security, selection and export enhancements._

### Added
- Password-protected PDFs: prompted for the password when added (kept for the session only).
- Image overlays (logo, stamp, signature) with live preview.
- Export options: subject and keywords, grayscale, and open the result in your PDF viewer.
- Selection tools: blank pages, pages from the same file, and delete by range.
- Built-in Help guide (F1) and About window.

### Fixed
- Thumbnails now render as real page previews, loading progressively while scrolling.

## [2.0.0] - 2026-10-02

_First release of the native Windows edition (C# / .NET 8)._

### Added
- Page workspace: merge many PDFs and images, drag-and-drop reorder, multi-select, rotate, duplicate, delete, insert blank pages, and full undo / redo.
- Split and extract: every N pages, each page, at the active page, or by custom ranges.
- Export options: text overlays, page numbers, text watermark, compression and password protection.
- Conversion: PDF → Word, PDF → images (PNG, JPEG, WebP, BMP, TIFF, GIF) and images → PDF with page size and fit controls.
- Live preview pane and light / dark themes.
