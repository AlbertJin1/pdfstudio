using PdfStudio.Models;
using PdfStudio.Services;
using System.Diagnostics;

namespace PdfStudio.UI;

public sealed partial class MainForm
{
    private int tempCounter;

    // ───────────────────────── infrastructure ─────────────────────────

    private async void Fire(Task task)
    {
        try { await task; }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SetStatus(string text) => statusText.Text = text;

    /// <summary>Status-bar message plus a small toast over the page grid.</summary>
    private void Notify(string text, bool good = true)
    {
        SetStatus(text);
        if (grid.Visible) grid.ShowToast(text, good);
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, Friendly(ex), "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static string Friendly(Exception ex) =>
        ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
            ? "This PDF is password-protected. Add it again and enter its password when asked."
            : ex.Message;

    private async Task<bool> RunBusy(string message, Action work)
    {
        busy = true; busyBar.Visible = true; SetStatus(message);
        header.Enabled = false; ribbon.Enabled = false; UseWaitCursor = true;
        try { await Task.Run(work); SetStatus("Done."); return true; }
        catch (Exception ex) { ShowError(ex); SetStatus("Operation failed."); return false; }
        finally
        {
            busy = false; busyBar.Visible = false; UseWaitCursor = false;
            header.Enabled = true; ribbon.Enabled = true; UpdateUi();
        }
    }

    private PageSnapshot Snapshot() => new() { Pages = pages.ToList(), Selected = grid.Selection.ToList() };

    /// <summary>Every page-list change goes through here so undo/redo, selection and preview stay consistent.</summary>
    private void Edit(Action change)
    {
        if (busy) return;
        history.Push(Snapshot());
        change();
        dirty = true;
        AfterChange();
    }

    private void AfterChange()
    {
        grid.NotifyPagesChanged();
        lastPreviewSig = "";
        UpdateUi();
        ShowPreview(true);
    }

    private PageItem? ActivePage() { var i = grid.ActiveIndex; return i >= 0 && i < pages.Count ? pages[i] : null; }

    private List<PageItem> Targets()
    {
        var sel = grid.SelectedPages();
        if (sel.Count > 0) return sel;
        return ActivePage() is { } a ? new List<PageItem> { a } : new List<PageItem>();
    }

    private void UpdateUi()
    {
        if (undoBtn is null) return;
        var has = pages.Count > 0;
        var target = has && (grid.Selection.Count > 0 || grid.ActiveIndex >= 0);
        undoBtn.Enabled = history.CanUndo; redoBtn.Enabled = history.CanRedo;
        foreach (var b in new[] { rotLBtn, rotRBtn, delBtn, arrangeBtn, annotateBtn }) b.Enabled = target;
        pagesBtn.Enabled = true;                      // "Insert blank page" works on an empty document too
        extractMenuBtn.Enabled = moreBtn.Enabled = has;
        newBtn.Enabled = has;
        editBtn.Enabled = has;
        exportBtn.Enabled = has;
        pageSizeBtn.Enabled = has;
        emptyState.Visible = !has; grid.Visible = has;
        countText.Text = has ? $"{pages.Count} page(s)  •  {grid.Selection.Count} selected" : "";
        convertBtn.Enabled = addBtn.Enabled = true;
        Text = has ? $"PDF Studio — {pages.Count} page(s)" : "PDF Studio";
        if (!has && !busy) SetStatus("Add PDFs or images to begin.");

        // show / hide the preview pane when the document goes from empty to non-empty (and back)
        if (has != hadPages)
        {
            hadPages = has;
            if (IsHandleCreated && split.Width > 0) ApplyPreviewVisibility();
        }
    }

    /// <summary>Ribbon "Page size": pick one page size (and fit mode) for the whole exported PDF.</summary>
    private void ChoosePageSize()
    {
        if (busy) return;
        using var dlg = new PageSizeDialog(exportOptions.Layout, pages.ToList());
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        exportOptions.Layout = dlg.Result;
        RefreshPageSizeButton();
        Notify(exportOptions.Layout.IsOriginal
            ? "Pages keep their own sizes."
            : $"All pages will be exported as {PageSizing.Name(exportOptions.Layout)}.");
    }

    private void RefreshPageSizeButton()
    {
        pageSizeBtn.Active = !exportOptions.Layout.IsOriginal;
        pageSizeBtn.Invalidate();
    }

    private void ShowPreview(bool force)
    {
        var p = ActivePage();
        var sig = p is null ? "" : $"{p.Id}|{p.Rotation}|{p.TextAnnotations.Count}|{p.ImageAnnotations.Count}|{p.Edits.Count}";
        if (!force && sig == lastPreviewSig) return;
        lastPreviewSig = sig;
        preview.ShowPage(p);
    }

    private static string Ext(string path) => Path.GetExtension(path).ToLowerInvariant();

    private void Reveal(string path)
    {
        try
        {
            if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
            else if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
        }
        catch { }
    }

    private static string FormatSize(string path)
    {
        try
        {
            var b = new FileInfo(path).Length;
            return b >= 1 << 20 ? $"{b / 1048576.0:0.0} MB" : $"{Math.Max(1, b / 1024)} KB";
        }
        catch { return ""; }
    }

    private static string DocumentsFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // ───────────────────────── new / fresh start ─────────────────────────

    /// <summary>Ribbon "New": removes every page and the undo history so work starts from a clean slate.</summary>
    private void NewDocument()
    {
        if (busy || pages.Count == 0) return;
        if (dirty && MessageBox.Show(this, "Remove all pages and start a new document?\n\nChanges that haven't been exported will be lost.",
                "PDF Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        pages.Clear();
        history.Clear();
        grid.ClearSelection();
        dirty = false;
        AfterChange();
        SetStatus("New document. Add PDFs or images to begin.");
    }

    // ───────────────────────── undo / redo ─────────────────────────

    private void Undo() { if (busy) return; if (history.Undo(Snapshot()) is { } s) Restore(s); }
    private void Redo() { if (busy) return; if (history.Redo(Snapshot()) is { } s) Restore(s); }

    private void Restore(PageSnapshot s)
    {
        pages.Clear(); pages.AddRange(s.Pages);
        grid.NotifyPagesChanged();
        grid.SetSelection(s.Selected);
        dirty = true; lastPreviewSig = "";
        UpdateUi(); ShowPreview(true);
    }

    // ───────────────────────── page operations ─────────────────────────

    private void Rotate(int degrees)
    {
        var t = Targets(); if (t.Count == 0) return;
        Edit(() => { foreach (var p in t) p.Rotation = PdfService.Norm(p.Rotation + degrees); });
    }

    private void RotateAll(int degrees)
    {
        if (pages.Count == 0) return;
        Edit(() => { foreach (var p in pages) p.Rotation = PdfService.Norm(p.Rotation + degrees); });
    }

    private void ResetRotation()
    {
        var t = Targets().Where(p => p.Rotation != 0).ToList();
        if (t.Count == 0) { Notify("The selected pages are not rotated."); return; }
        Edit(() => { foreach (var p in t) p.Rotation = 0; });
    }

    private void DeleteByRange()
    {
        if (pages.Count == 0) return;
        var text = InputDialog.Show(this, "Delete pages", $"Pages to delete (1–{pages.Count}), e.g. 2-4, 9  or  odd / even:");
        if (text is null) return;
        List<int> idx;
        try { idx = PageRange.Parse(text, pages.Count).Distinct().ToList(); }
        catch (FormatException ex) { MessageBox.Show(this, ex.Message, "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (idx.Count == 0) return;
        if (idx.Count == pages.Count &&
            MessageBox.Show(this, "That removes every page. Continue?", "PDF Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var ids = idx.Select(i => pages[i].Id).ToHashSet();
        Edit(() => pages.RemoveAll(p => ids.Contains(p.Id)));
        Notify($"Deleted {ids.Count} page(s). Ctrl+Z to undo.");
    }

    private void SelectSameSource()
    {
        var t = Targets(); if (t.Count == 0) return;
        var sources = t.Where(p => !p.IsBlank).Select(p => p.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sources.Count == 0) return;
        grid.SelectIndices(Enumerable.Range(0, pages.Count).Where(i => !pages[i].IsBlank && sources.Contains(pages[i].SourcePath)));
    }

    private async Task SelectBlankPagesAsync()
    {
        if (pages.Count == 0) return;
        var snapshot = pages.ToList();
        var blanks = new List<int>();
        var ok = await RunBusy("Looking for blank pages…", () =>
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                var p = snapshot[i];
                try { if (p.IsBlank || images.LooksBlank(p.SourcePath, p.SourcePageIndex)) blanks.Add(i); } catch { }
            }
        });
        if (!ok) return;
        if (blanks.Count == 0) { Notify("No blank pages found."); return; }
        grid.SelectIndices(blanks);
        Notify($"Selected {blanks.Count} blank page(s). Press Delete to remove them.");
    }

    private void DeleteSelected()
    {
        var t = Targets(); if (t.Count == 0) return;
        var first = pages.IndexOf(t[0]);
        var ids = t.Select(p => p.Id).ToHashSet();
        Edit(() => pages.RemoveAll(p => ids.Contains(p.Id)));
        if (pages.Count > 0) grid.SelectOnly(Math.Min(first, pages.Count - 1)); else grid.ClearSelection();
    }

    private void DuplicateSelected()
    {
        var t = Targets(); if (t.Count == 0) return;
        var copies = new List<PageItem>();
        Edit(() =>
        {
            foreach (var p in t)
            {
                var c = p.CloneNewId();
                pages.Insert(pages.IndexOf(p) + 1, c);
                copies.Add(c);
            }
        });
        grid.SetSelection(copies.Select(c => c.Id));
    }

    private void ReversePages()
    {
        var sel = grid.SelectedPages();
        if (sel.Count >= 2)
        {
            var idx = sel.Select(p => pages.IndexOf(p)).OrderBy(i => i).ToList();
            Edit(() =>
            {
                var items = idx.Select(i => pages[i]).Reverse().ToList();
                for (var k = 0; k < idx.Count; k++) pages[idx[k]] = items[k];
            });
        }
        else if (pages.Count >= 2) Edit(() => pages.Reverse());
    }

    private void MoveSelected(int direction)
    {
        var t = Targets(); if (t.Count == 0) return;
        var ids = t.Select(p => p.Id).ToHashSet();
        var idx = pages.Select((p, i) => (p, i)).Where(x => ids.Contains(x.p.Id)).Select(x => x.i).ToList();
        if (direction < 0 && idx[0] == 0) return;
        if (direction > 0 && idx[^1] == pages.Count - 1) return;
        Edit(() =>
        {
            if (direction < 0)
            {
                for (var i = 1; i < pages.Count; i++)
                    if (ids.Contains(pages[i].Id) && !ids.Contains(pages[i - 1].Id)) (pages[i - 1], pages[i]) = (pages[i], pages[i - 1]);
            }
            else
            {
                for (var i = pages.Count - 2; i >= 0; i--)
                    if (ids.Contains(pages[i].Id) && !ids.Contains(pages[i + 1].Id)) (pages[i], pages[i + 1]) = (pages[i + 1], pages[i]);
            }
        });
        grid.EnsureVisible(pages.IndexOf(t[0]));
    }

    private void MoveToEdge(bool toStart)
    {
        var t = Targets(); if (t.Count == 0) return;
        var ids = t.Select(p => p.Id).ToHashSet();
        var moving = pages.Where(p => ids.Contains(p.Id)).ToList();
        var rest = pages.Where(p => !ids.Contains(p.Id)).ToList();
        var result = toStart ? moving.Concat(rest).ToList() : rest.Concat(moving).ToList();
        if (result.Select(p => p.Id).SequenceEqual(pages.Select(p => p.Id))) return;
        Edit(() => { pages.Clear(); pages.AddRange(result); });
        grid.EnsureVisible(pages.IndexOf(moving[0]));
    }

    private void MoveToPosition()
    {
        var t = Targets(); if (t.Count == 0) return;
        var text = InputDialog.Show(this, "Move to page number", $"The first selected page will become this page number (1–{pages.Count}):", (pages.IndexOf(t[0]) + 1).ToString());
        if (text is null) return;
        if (!int.TryParse(text, out var n) || n < 1 || n > pages.Count) { MessageBox.Show(this, $"Enter a number from 1 to {pages.Count}.", "PDF Studio"); return; }
        var ids = t.Select(p => p.Id).ToHashSet();
        var moving = pages.Where(p => ids.Contains(p.Id)).ToList();
        var rest = pages.Where(p => !ids.Contains(p.Id)).ToList();
        var at = Math.Clamp(n - 1, 0, rest.Count);
        rest.InsertRange(at, moving);
        if (rest.Select(p => p.Id).SequenceEqual(pages.Select(p => p.Id))) return;
        Edit(() => { pages.Clear(); pages.AddRange(rest); });
        grid.EnsureVisible(pages.IndexOf(moving[0]));
    }

    /// <summary>Drag-and-drop commit: <paramref name="insertAt"/> is an index into the list as it was before the move.</summary>
    private void OnReorder(IReadOnlyList<PageItem> moving, int insertAt)
    {
        if (busy) return;
        var ids = moving.Select(m => m.Id).ToHashSet();
        var removedBefore = pages.Take(Math.Min(insertAt, pages.Count)).Count(p => ids.Contains(p.Id));
        var result = pages.Where(p => !ids.Contains(p.Id)).ToList();
        result.InsertRange(Math.Clamp(insertAt - removedBefore, 0, result.Count), moving);
        if (result.Select(p => p.Id).SequenceEqual(pages.Select(p => p.Id))) return; // dropped back where it was
        Edit(() => { pages.Clear(); pages.AddRange(result); });
        grid.EnsureVisible(pages.IndexOf(moving[0]));
        Notify($"Moved {moving.Count} page(s). Ctrl+Z to undo.");
    }

    private void SelectByRange()
    {
        if (pages.Count == 0) return;
        var text = InputDialog.Show(this, "Select pages", $"Pages to select (1–{pages.Count}), e.g. 1-3, 5, 8-  or  odd / even:");
        if (text is null) return;
        try { grid.SelectIndices(PageRange.Parse(text, pages.Count).Distinct()); }
        catch (FormatException ex) { MessageBox.Show(this, ex.Message, "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void InsertBlank()
    {
        using var dlg = new BlankPageDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var active = ActivePage();
        double w, h;
        if (dlg.PageSizeChoice == BlankSize.MatchPage && active is not null) (w, h) = active.DisplaySizePt(true);
        else
        {
            var (mw, mh) = dlg.PageSizeChoice switch
            {
                BlankSize.A3 => ImagePdfService.PageSizeMm(ImagePdfPageSize.A3), BlankSize.A5 => ImagePdfService.PageSizeMm(ImagePdfPageSize.A5),
                BlankSize.Letter => ImagePdfService.PageSizeMm(ImagePdfPageSize.Letter), BlankSize.Legal => ImagePdfService.PageSizeMm(ImagePdfPageSize.Legal),
                _ => ImagePdfService.PageSizeMm(ImagePdfPageSize.A4)
            };
            w = mw * 72 / 25.4; h = mh * 72 / 25.4;
            if (dlg.Landscape != w > h) (w, h) = (h, w);
        }
        var items = Enumerable.Range(0, dlg.Count).Select(_ => PdfService.CreateBlank(w, h)).ToList();
        var at = dlg.Position switch
        {
            BlankPosition.AtStart => 0,
            BlankPosition.AtEnd => pages.Count,
            _ => Targets().Count > 0 ? pages.IndexOf(Targets()[^1]) + 1 : pages.Count
        };
        Edit(() => pages.InsertRange(Math.Clamp(at, 0, pages.Count), items));
        grid.SetSelection(items.Select(i => i.Id));
        grid.EnsureVisible(pages.IndexOf(items[0]));
    }

    private List<PageItem> ScopeTargets(OverlayScope scope) => scope switch
    {
        OverlayScope.AllPages => pages.ToList(),
        OverlayScope.SelectedPages => Targets(),
        _ => ActivePage() is { } a ? new List<PageItem> { a } : new List<PageItem>()
    };

    /// <summary>Opens the Edit PDF window on the active page; the result is stored on the pages (undoable) and written on export.</summary>
    private void EditPdf()
    {
        if (pages.Count == 0 || busy) return;
        var start = Math.Max(0, grid.ActiveIndex);
        using var dlg = new EditorForm(pages, start);
        if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.Changed) return;
        var result = dlg.Result;
        var marginResult = dlg.MarginsResult;
        Edit(() =>
        {
            foreach (var p in pages)
            {
                if (result.TryGetValue(p.Id, out var list)) p.Edits = list.Select(o => o.Clone()).ToList();
                if (marginResult.TryGetValue(p.Id, out var mg)) p.Margins = mg.Clone();
            }
        });
        Notify("Edits saved. They are written into the PDF when you export.");
    }

    private void AddText()
    {
        if (pages.Count == 0) return;
        using var dlg = new TextOverlayDialog(grid.Selection.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var targets = ScopeTargets(dlg.Scope);
        if (targets.Count == 0) { MessageBox.Show(this, "Select a page first.", "PDF Studio"); return; }
        var ann = dlg.Annotation;
        Edit(() => { foreach (var p in targets) p.TextAnnotations.Add(ann.Clone()); });
        Notify($"Text added to {targets.Count} page(s). It is written into the PDF when you export.");
    }

    private void AddImageOverlay()
    {
        if (pages.Count == 0) return;
        using var dlg = new ImageOverlayDialog(grid.Selection.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var targets = ScopeTargets(dlg.Scope);
        if (targets.Count == 0) { MessageBox.Show(this, "Select a page first.", "PDF Studio"); return; }
        var ann = dlg.Annotation;
        Edit(() => { foreach (var p in targets) p.ImageAnnotations.Add(ann.Clone()); });
        Notify($"Image added to {targets.Count} page(s). It is written into the PDF when you export.");
    }

    private void ClearOverlays()
    {
        var t = Targets().Where(p => p.HasOverlays).ToList();
        if (t.Count == 0) { SetStatus("The selected pages have no text or images added."); return; }
        Edit(() => { foreach (var p in t) { p.TextAnnotations.Clear(); p.ImageAnnotations.Clear(); p.Edits.Clear(); } });
        Notify($"Removed added text and images from {t.Count} page(s).");
    }

    // ───────────────────────── importing ─────────────────────────

    private async Task AddPdfsFlow()
    {
        using var d = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf", Multiselect = true, Title = "Add PDF files" };
        if (d.ShowDialog(this) == DialogResult.OK) await AddFilesAsync(d.FileNames, -1);
    }

    private async Task PickImagesFlow()
    {
        using var d = new OpenFileDialog { Filter = ImagePdfService.ImageFilter, Multiselect = true, Title = "Choose images" };
        if (d.ShowDialog(this) == DialogResult.OK) await AddFilesAsync(d.FileNames, -1);
    }

    private async Task InsertPdfAfterSelection()
    {
        using var d = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf", Multiselect = true, Title = "Insert PDF pages" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        var t = Targets();
        await AddFilesAsync(d.FileNames, t.Count > 0 ? pages.IndexOf(t[^1]) + 1 : -1);
    }

    /// <summary>Adds PDFs and images (via the page-size dialog) to the workspace.</summary>
    private async Task AddFilesAsync(IEnumerable<string> files, int insertAt)
    {
        if (busy) return;
        var all = files.Where(File.Exists).ToList();
        var pdfs = all.Where(f => Ext(f) == ".pdf").ToList();
        var imgs = all.Where(ImagePdfService.IsImage).ToList();
        if (pdfs.Count + imgs.Count == 0)
        {
            SetStatus("Nothing to add — use PDF or image files.");
            return;
        }

        var added = new List<PageItem>();
        var errors = new List<string>();

        var locked = new List<string>();
        if (pdfs.Count > 0)
            await RunBusy("Reading PDFs…", () =>
            {
                foreach (var p in pdfs)
                    try { added.AddRange(pdf.LoadPages(p)); }
                    catch (Exception ex)
                    {
                        if (PdfPasswords.IsPasswordProblem(ex)) locked.Add(p);
                        else errors.Add($"{Path.GetFileName(p)} — {Friendly(ex)}");
                    }
            });

        // password-protected PDFs: ask for the password (three tries per file)
        foreach (var lp in locked)
        {
            var name = Path.GetFileName(lp);
            var unlocked = false;
            for (var attempt = 0; attempt < 3 && !unlocked; attempt++)
            {
                var pw = InputDialog.Show(this, "Password required",
                    $"“{name}” is password-protected. Enter its password" + (attempt > 0 ? " (that one was not accepted)" : "") + ":", "", true);
                if (pw is null) break;
                PdfPasswords.Set(lp, pw);
                try { added.AddRange(pdf.LoadPages(lp)); unlocked = true; thumbs.RetryFailed(); }
                catch (Exception ex) when (PdfPasswords.IsPasswordProblem(ex)) { }
                catch (Exception ex) { errors.Add($"{name} — {ex.Message}"); unlocked = true; break; }
            }
            if (!unlocked) errors.Add($"{name} — skipped (password not provided or not accepted).");
        }

        if (imgs.Count > 0)
        {
            using var dlg = new ImageToPdfDialog(imgs);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var list = dlg.Images; var opts = dlg.Options;
                if (dlg.AddToWorkspace)
                {
                    var tmp = Path.Combine(sessionDir, $"Images {++tempCounter}.pdf");
                    if (await RunBusy("Creating pages from images…", () => images.ImagesToPdf(tmp, list, opts)))
                        added.AddRange(pdf.LoadPages(tmp, "Images"));
                }
                else await SaveImagesAsPdf(list, opts);
            }
        }

        if (added.Count > 0)
        {
            var at = insertAt < 0 || insertAt > pages.Count ? pages.Count : insertAt;
            Edit(() => pages.InsertRange(at, added));
            grid.SetSelection(added.Select(p => p.Id));
            grid.EnsureVisible(pages.IndexOf(added[0]));
            Notify($"Added {added.Count} page(s).");
        }
        if (errors.Count > 0)
            MessageBox.Show(this, "Some files could not be added:\n\n" + string.Join("\n", errors), "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private async Task SaveImagesAsPdf(IReadOnlyList<string> list, ImagePdfOptions opts)
    {
        using var save = new SaveFileDialog { Filter = "PDF file (*.pdf)|*.pdf", FileName = "Images.pdf" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        var path = save.FileName;
        if (await RunBusy("Creating PDF…", () => images.ImagesToPdf(path, list, opts)))
        {
            Notify($"Saved {Path.GetFileName(path)} ({FormatSize(path)}).");
            Reveal(path);
        }
    }

    // ───────────────────────── exporting ─────────────────────────

    private static string DefaultName(IReadOnlyList<PageItem> src)
    {
        var names = src.Where(p => !p.IsBlank).Select(p => p.SourcePath).Distinct().ToList();
        return names.Count == 1 ? Path.GetFileNameWithoutExtension(names[0]) + " (edited).pdf" : "Merged document.pdf";
    }

    private async Task ExportAsync(bool selectedOnly)
    {
        var src = selectedOnly ? grid.SelectedPages() : pages.ToList();
        if (src.Count == 0) { MessageBox.Show(this, "Select at least one page first.", "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        using var dlg = new ExportDialog(exportOptions, src.Count, selectedOnly ? "Export selected" : "Export PDF", src);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var opts = dlg.Result;
        using var save = new SaveFileDialog { Filter = "PDF file (*.pdf)|*.pdf", FileName = DefaultName(src) };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        var path = save.FileName;

        var ok = await RunBusy("Building PDF…", () => pdf.Export(path, src, opts));
        opts.UserPassword = ""; opts.OwnerPassword = ""; exportOptions = opts; // never keep passwords around
        RefreshPageSizeButton();
        if (!ok) return;
        if (!selectedOnly) dirty = false;
        Notify($"Saved {Path.GetFileName(path)} — {src.Count} page(s), {FormatSize(path)}.");
        Reveal(path);
    }

    private async Task ExtractSelectedAsync()
    {
        var src = grid.SelectedPages();
        if (src.Count == 0 && ActivePage() is { } a) src = new List<PageItem> { a };
        if (src.Count == 0) { MessageBox.Show(this, "Select the pages to extract first.", "PDF Studio"); return; }
        using var save = new SaveFileDialog { Filter = "PDF file (*.pdf)|*.pdf", FileName = $"Extracted {src.Count} page(s).pdf" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        var path = save.FileName;
        if (await RunBusy("Extracting pages…", () => pdf.Export(path, src)))
        {
            Notify($"Extracted {src.Count} page(s) to {Path.GetFileName(path)}.");
            Reveal(path);
        }
    }

    private async Task SplitAsync()
    {
        if (pages.Count < 2) return;
        var firstSource = pages.FirstOrDefault(p => !p.IsBlank)?.SourcePath;
        var defFolder = Path.Combine(firstSource is null ? DocumentsFolder : Path.GetDirectoryName(firstSource) ?? DocumentsFolder, "PDF Studio Split");
        using var dlg = new SplitDialog(pages.Count, grid.ActiveIndex, defFolder);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var groups = new List<List<PageItem>>();
        switch (dlg.Mode)
        {
            case SplitMode.EachPage: groups.AddRange(pages.Select(p => new List<PageItem> { p })); break;
            case SplitMode.AtActive:
                var cut = grid.ActiveIndex;
                groups.Add(pages.Take(cut + 1).ToList()); groups.Add(pages.Skip(cut + 1).ToList()); break;
            case SplitMode.Ranges:
                foreach (var g in PageRange.ParseGroups(dlg.Ranges, pages.Count)) groups.Add(g.Select(i => pages[i]).ToList());
                break;
            default:
                var n = Math.Max(1, dlg.EveryN);
                for (var i = 0; i < pages.Count; i += n) groups.Add(pages.Skip(i).Take(n).ToList());
                break;
        }
        var folder = dlg.Folder; var baseName = dlg.BaseName;
        var digits = Math.Max(2, groups.Count.ToString().Length);
        var ok = await RunBusy("Splitting…", () =>
        {
            Directory.CreateDirectory(folder);
            for (var i = 0; i < groups.Count; i++)
                pdf.Export(Path.Combine(folder, $"{baseName}_{(i + 1).ToString().PadLeft(digits, '0')}.pdf"), groups[i]);
        });
        if (ok) { Notify($"Created {groups.Count} file(s) in {folder}."); Reveal(folder); }
    }

    private async Task OpenInViewerAsync()
    {
        if (pages.Count == 0) return;
        var src = grid.Selection.Count > 0 ? grid.SelectedPages() : pages.ToList();
        var o = exportOptions;
        var quick = new ExportOptions
        {
            Title = o.Title, Author = o.Author, Subject = o.Subject, Keywords = o.Keywords,
            PageNumbers = o.PageNumbers, NumberPosition = o.NumberPosition, NumberFormat = o.NumberFormat, StartNumber = o.StartNumber,
            NumberFontSize = o.NumberFontSize, SkipFirstPage = o.SkipFirstPage,
            Watermark = o.Watermark, WatermarkText = o.WatermarkText, WatermarkOpacityPercent = o.WatermarkOpacityPercent,
            WatermarkFontSize = o.WatermarkFontSize, WatermarkAngle = o.WatermarkAngle,
            Layout = o.Layout.Clone()
        };
        var tmp = Path.Combine(sessionDir, $"Preview {++tempCounter}.pdf");
        if (!await RunBusy("Preparing a copy to open…", () => pdf.Export(tmp, src, quick))) return;
        try { Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true }); Notify($"Opened {src.Count} page(s) in your PDF viewer."); }
        catch (Exception ex) { ShowError(ex); }
    }

    // ───────────────────────── conversions ─────────────────────────

    /// <summary>Resolves the PDF a converter should read: an external file, or the workspace exported to a temp PDF.</summary>
    private async Task<(string? Path, string BaseName)> ResolveSourcePdf(PdfSource source, string externalPath)
    {
        if (source == PdfSource.File) return (externalPath, System.IO.Path.GetFileNameWithoutExtension(externalPath));
        var pgs = source == PdfSource.WorkspaceSelected ? grid.SelectedPages() : pages.ToList();
        var tmp = System.IO.Path.Combine(sessionDir, $"Workspace {++tempCounter}.pdf");
        var ok = await RunBusy("Preparing pages…", () => pdf.Export(tmp, pgs));
        return (ok ? tmp : null, "PDF Studio export");
    }

    private async Task PdfToWordFlow()
    {
        using var dlg = new PdfToWordDialog(pages.Count, grid.Selection.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var (src, baseName) = await ResolveSourcePdf(dlg.Source, dlg.ExternalPath);
        if (src is null) return;

        using var save = new SaveFileDialog { Filter = "Word document (*.docx)|*.docx", FileName = baseName + ".docx" };
        if (save.ShowDialog(this) != DialogResult.OK) return;
        var target = save.FileName;
        var options = new PdfToWordOptions { GraphicsDpi = dlg.GraphicsDpi };
        PdfToWordResult? result = null;
        if (await RunBusy("Converting PDF → Word…", () =>
        {
            result = toWord.Convert(src, target, options, (done, total) =>
            {
                if (IsHandleCreated && !IsDisposed)
                    try { BeginInvoke(() => SetStatus($"Converting PDF → Word… page {Math.Min(done + 1, total)} of {total}")); } catch { }
            });
        }))
        {
            var note = result is { Warnings.Count: > 0 } ? $" ({result.Warnings.Count} warning(s): {result.Warnings[0]})" : "";
            Notify($"Saved {Path.GetFileName(target)} — {result?.Pages} page(s), {result?.TextLines} editable text line(s), {result?.Tables} table(s).{note}");
            Reveal(target);
        }
    }

    private async Task PdfToImagesFlow()
    {
        using var dlg = new PdfToImagesDialog(pages.Count, grid.Selection.Count,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PDF Studio"));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        List<int>? pageIdx = null;
        if (dlg.Source == PdfSource.File)
        {
            try { pageIdx = PageRange.Parse(dlg.PageRangeText, ImagePdfService.GetPageCount(dlg.ExternalPath)); }
            catch (Exception ex) { MessageBox.Show(this, Friendly(ex), "PDF Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        }
        var (src, baseName) = await ResolveSourcePdf(dlg.Source, dlg.ExternalPath);
        if (src is null) return;

        var opts = new PdfToImageOptions
        {
            Format = dlg.Format, Dpi = dlg.Dpi, Quality = dlg.Quality, Pages = pageIdx,
            Prefix = dlg.Prefix.Length > 0 ? dlg.Prefix : baseName.Replace(' ', '_')
        };
        var folder = dlg.OutputFolder;
        var written = new List<string>();
        if (await RunBusy("Rendering pages to images…", () => written.AddRange(images.PdfToImages(src, folder, opts))))
        {
            Notify($"Saved {written.Count} image(s) to {folder}.");
            if (dlg.OpenWhenDone) Reveal(written.Count > 0 ? written[0] : folder);
        }
    }

    // ───────────────────────── about & help ─────────────────────────

    private void ShowReview()
    {
        using var dlg = new ReviewDialog();
        dlg.ShowDialog(this);
        if (dlg.Sent) Notify("Thank you! Your review was sent.");
    }

    private void ShowAbout()
    {
        using var dlg = new AboutDialog();
        dlg.ShowDialog(this);
        if (dlg.OpenChangelogRequested) ShowHelp("Version history");
        else if (dlg.OpenHelpRequested) ShowHelp();
        else if (dlg.CheckUpdatesRequested) Fire(CheckForUpdatesAsync(true));
    }

    // ───────────────────────── updates ─────────────────────────

    private bool updateBusy;

    /// <summary>Looks for a newer version on the monitor server. manual = the user asked for it (so "up to date" and errors are shown).</summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (updateBusy || busy) return;
        updateBusy = true;
        try
        {
            var result = await UpdateService.CheckAsync();
            if (IsDisposed) return;
            if (result.Error is not null)
            {
                if (manual) MessageBox.Show(this, result.Error, "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (result.Update is not { } update)
            {
                if (manual) MessageBox.Show(this, $"You are running the latest version ({UpdateService.CurrentVersion}).", "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!manual && UpdateService.SkippedVersion == update.Version) return;
            if (busy) return;                                   // the user started a task meanwhile: don't interrupt it

            using var dlg = new UpdateDialog(update);
            dlg.ShowDialog(this);
            if (dlg.SkipRequested) { UpdateService.SkippedVersion = update.Version; return; }
            if (!dlg.Downloaded || dlg.DownloadedPath is null) return;

            if (dirty && pages.Count > 0 &&
                MessageBox.Show(this, "PDF Studio will restart to finish the update, and you have page changes that haven't been exported. Restart anyway?",
                                "PDF Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                try { File.Delete(dlg.DownloadedPath); } catch { }
                SetStatus("Update postponed.");
                return;
            }
            UpdateService.ApplyAndRestart(dlg.DownloadedPath);   // swaps the exe and starts the new version
            dirty = false;                                       // already confirmed above: don't ask again on close
            Close();
        }
        catch (Exception ex) { if (manual) ShowError(ex); }
        finally { updateBusy = false; }
    }

    /// <summary>The help guide is a normal (non-modal) window so you can read it while you work.</summary>
    private void ShowHelp(string? topic = null)
    {
        if (helpWindow is { IsDisposed: false })
        {
            if (topic is not null) helpWindow.OpenTopic(topic);
            helpWindow.Activate();
            return;
        }
        helpWindow = new HelpDialog(topic);
        helpWindow.FormClosed += (_, _) => helpWindow = null;
        helpWindow.Show(this);
    }
}
