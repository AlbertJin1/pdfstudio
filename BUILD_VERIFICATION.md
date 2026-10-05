# Build notes (2.0.0)

This version was written without access to a Windows machine or the NuGet feed, so it has **not been compiled or run by the author**.
Please run `build_publish.bat` and report any compiler errors; they should be small (typos / API signature differences).

## First-run checklist
1. Add 2–3 PDFs → pages appear; drag one between two others → marker shows, order changes; Ctrl+Z restores.
2. Select several pages, drag them as a group; drag near the bottom edge → grid auto-scrolls.
3. Export PDF with page numbers + watermark; check a rotated page shows them upright.
4. Add text on a landscape and a rotated page; export and check position.
5. Images → PDF with WebP, GIF, TIFF and a phone JPEG (EXIF rotation); try A4 + Auto orientation + Fill.
6. PDF → Images at 150 dpi in each format.
7. Convert → PDF → Word (built in, nothing to install): the dialog opens, the .docx is produced, and opening it in Word shows the same layout as the PDF (text editable). Confirm there is no "Word → PDF" entry in the Convert menu or on the welcome card.
7b. Start-up: loader appears, spins, then closes by itself and the main window is fully drawn and focused.
8. Toggle dark theme; restart (it should be remembered).

## 2.1 additions to test
9. Thumbnails: add a multi-page PDF; tiles show previews that fade in while scrolling.
10. Add a password-protected PDF: prompt appears, pages load after the correct password.
11. Add image / text overlays, check the preview and the exported PDF.
12. Shrink the window below ~1000 px: preview hides, header goes compact, ribbon wraps.
13. F1 opens the Help guide; the (i) button opens About; footer shows the developer.

## 2.2 Edit PDF — first-run checklist (not compiled by the author)
14. Select a page, press Ctrl+E: the editor opens maximised with the page rendered.
15. Hover text → it highlights; click → type a change → click elsewhere. Export and confirm the new text replaces the old one.
16. Add text, change font / size / bold / colour in the right panel; drag it; drag a corner handle to resize.
17. Insert a picture and draw a signature; move + resize both. Try a rotated page and "Page size" resizing, then export.
18. Undo / redo, Apply changes → the main preview and exported PDF show the edits; Cancel discards.
If hover never highlights text, `PdfiumText` could not bind to pdfium (DllImport "pdfium"); the editor then says so in its hint bar and everything else still works.

## Version history - manual test
1. About (i button): the footer reads "Version 2.6.0  ·  October 5, 2026"; the banner pill reads "Version 2.6.0".
2. About > **What's new** closes About and opens Help on **Version history**; the newest release is first and marked "(current)", sub-headings read ADDED / CHANGED / FIXED / REMOVED.
3. With Help already open on another topic (or with text typed in its search box), About > What's new brings it to Version history.
4. Help search: typing "signature" lists Version history among the matching topics; check light and dark theme.
5. Build check: temporarily set `<Version>` to 9.9.9 and build; it must fail with "CHANGELOG.md has no entry for version 9.9.9". Restore it.

## Saved signatures - manual test
1. Edit PDF > Signature: menu shows "No saved signatures yet" on a fresh profile.
2. Add signature picture with (a) a transparent PNG, (b) a JPG photo of a signature on white paper. (b): tick Remove the paper background, move Cleanup; checkerboard should show only ink.
3. Save, click Signature again: the thumbnail is listed; click it, the signature lands on the page with no white box. Export and open the PDF: still transparent.
4. Close and restart PDF Studio: the signature is still in the Signature menu (no file browsing).
5. Draw a signature with "Keep it in My signatures" ticked; check it appears in the menu next time.
6. Manage saved signatures: rename (F2), delete; a signature already placed on a page still exports after its library entry is deleted.
7. Main window > Add image: "Saved signature" list fills the picture path.

## 2.5.1 - manual test
1. Edit PDF > Edit text: press on a line of text and drag: it moves (snaps, Alt = free) and the original text is covered. A plain click still opens the editor.
2. Click a text line, then drag the **Move** handle in the "Editing text" bar: the box and bar follow the pointer. Apply keeps the new place; Cancel puts it back.
3. Open any drop-down (Margins, Shapes, Pages, Arrange...): rounded corners, icons, muted shortcuts, the button stays highlighted while open; clicking it again closes the menu. Check light and dark theme.
4. Edit PDF: drag a text line or picture between two other lines: pink gap lines with values appear (above/below/left/right, or to the page edge). Centre it between the two neighbours: it clicks in and both labels turn indigo and equal. Move a little short of that: a dashed suggestion shows. Hold Alt: indicators disappear. Resize a box: gaps follow the edges.

## 2.6.0 - manual test

1. Start the app with no files: the window shows only the "Drop your files here" card (no Preview pane on the right). The Preview button in the status bar is disabled.
2. Add a PDF or image: the Preview pane slides in and shows the selected page. Click Preview: it hides; click again: it shows. New / delete all pages: it hides again.
3. Header > Review: hover and click the stars (the face and header colour change; Left/Right and 1–5 also work), tap a few chips, optional name and text, "Send review" (or Ctrl+Enter). With the server running the window shows the thank-you screen, closes by itself (or with Done) and the status bar says "Thank you! Your review was sent."; with no server the window stays open and shows a clear error at the bottom left. Pressing Send with no stars wiggles the stars and asks for a rating. There is no usage-statistics checkbox.
4. Open the app, keep it open for 2+ minutes, close it: on the dashboard one new session shows with the right "Open for" time and status "Closed". While it is open the dashboard shows "Open now".
5. Turn off ngrok, open and close the app, turn ngrok on, open the app again: the earlier session appears on the dashboard (queued in %LOCALAPPDATA%\PdfStudio\usage-queue.jsonl and re-sent).
6. For testing only: set `usage_stats=0` in `%LOCALAPPDATA%\PdfStudio\settings.txt`, restart: no new session is recorded.
