using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(KetCauVXT.AcadLink.LayerCleanCmd))]

namespace KetCauVXT.AcadLink
{
    using Autodesk.AutoCAD.ApplicationServices;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
    using Exception = System.Exception;

    /// <summary>
    /// VXT_DONLAYER – DỌN LAYER RÁC: xoá layer không có nghĩa trong bản vẽ đang mở.
    ///  • Layer TRỐNG: không còn đối tượng nào (kể cả trong định nghĩa block, thuộc tính) → chọn sẵn.
    ///  • Layer TÊN RÁC: XREF$0$…, Layer1, New Layer…, Copy of…, … (2), …_copy, A$C…, tên chỉ gồm chữ số → người dùng tự tích;
    ///    nếu còn đối tượng, đối tượng được chuyển sang layer chọn (mặc định 0) trước khi xoá.
    ///  • Layer 0, Defpoints, layer xref luôn giữ; layer hiện hành được chuyển hiện hành về 0 trước khi xoá.
    ///  • Bảng xác nhận: chỉ khi bấm "XÁC NHẬN XOÁ" mới thực hiện; một lệnh = một bước Undo (gõ U để hoàn tác).
    /// </summary>
    public class LayerCleanCmd
    {
        private static readonly Regex JunkName = new Regex(
            @"\$\d+\$|^layer\s*\d+$|^new layer|^copy of |\(\d+\)$|[_\- ]copy(\s*\d*)?$|^a\$c|^\d+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool IsJunkName(string name) => !string.IsNullOrEmpty(name) && JunkName.IsMatch(name.Trim());

        internal sealed class Info
        {
            public string Name;
            public int Count;
            public bool Junk, Standard;
            public string Kind =>
                Count == 0 ? (Junk ? "trống + tên rác" : Standard ? "trống (layer chuẩn VXT)" : "trống")
                           : "tên rác, còn đối tượng";
        }

        [CommandMethod("VXT_DONLAYER", CommandFlags.Modal)]
        public void DonLayer() => Run();

        [CommandMethod("VXT_DL", CommandFlags.Modal)]
        public void DonLayerShort() => Run();

        private static void Run()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            try
            {
                ed.WriteMessage("\nVXT_DONLAYER: đang đếm đối tượng theo layer…");
                var counts = CountByLayer(db);
                var infos = new List<Info>();
                var all = new List<string>();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (rec.IsDependent || rec.IsErased) continue;
                        all.Add(rec.Name);
                        if (rec.Name == "0" || rec.Name.Equals("DEFPOINTS", StringComparison.OrdinalIgnoreCase)) continue;
                        counts.TryGetValue(id, out int n);
                        bool junk = IsJunkName(rec.Name);
                        if (n == 0 || junk)
                            infos.Add(new Info { Name = rec.Name, Count = n, Junk = junk, Standard = rec.Name.StartsWith("VXT-", StringComparison.OrdinalIgnoreCase) });
                    }
                    tr.Commit();
                }
                if (infos.Count == 0) { ed.WriteMessage("\nBản vẽ không có layer trống hay layer tên rác nào."); return; }
                infos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                all.Sort(StringComparer.OrdinalIgnoreCase);

                List<Info> chosen; string target;
                using (var f = new LayerCleanForm(infos, all))
                {
                    if (AcApp.ShowModalDialog(f) != System.Windows.Forms.DialogResult.OK) { ed.WriteMessage("\nĐã huỷ, không xoá layer nào."); return; }
                    chosen = f.Chosen; target = f.Target;
                }
                Apply(doc, chosen, target);
            }
            catch (Exception ex) { ed.WriteMessage("\nVXT_DONLAYER lỗi: " + ex.Message); }
        }

        /// <summary>Số đối tượng theo layer trong mọi block không phải xref (Model, Layout, định nghĩa block) kể cả thuộc tính.</summary>
        private static Dictionary<ObjectId, int> CountByLayer(Database db)
        {
            var d = new Dictionary<ObjectId, int>();
            void Add(ObjectId id) { d.TryGetValue(id, out int n); d[id] = n + 1; }
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId bid in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                    if (btr.IsFromExternalReference || btr.IsDependent) continue;
                    foreach (ObjectId eid in btr)
                    {
                        if (!(tr.GetObject(eid, OpenMode.ForRead, false, true) is Entity e)) continue;
                        Add(e.LayerId);
                        if (e is BlockReference br)
                            foreach (ObjectId aid in br.AttributeCollection)
                                if (tr.GetObject(aid, OpenMode.ForRead, false, true) is AttributeReference ar) Add(ar.LayerId);
                    }
                }
                tr.Commit();
            }
            return d;
        }

        private static void Apply(Document doc, List<Info> chosen, string target)
        {
            var ed = doc.Editor; var db = doc.Database;
            int moved = 0, deleted = 0;
            var kept = new List<string>();
            using (doc.LockDocument())
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(target)) target = "0";
                    var targetId = lt[target];
                    var src = new HashSet<ObjectId>(chosen.Where(c => c.Count > 0 && lt.Has(c.Name)).Select(c => lt[c.Name]));
                    // mở khoá tạm layer nguồn + layer nhận
                    var relock = new List<LayerTableRecord>();
                    foreach (var id in src.Concat(new[] { targetId }))
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (rec.IsLocked) { rec.UpgradeOpen(); rec.IsLocked = false; relock.Add(rec); }
                    }
                    if (src.Count > 0)
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        foreach (ObjectId bid in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                            if (btr.IsFromExternalReference || btr.IsDependent) continue;
                            foreach (ObjectId eid in btr)
                            {
                                if (!(tr.GetObject(eid, OpenMode.ForRead, false, true) is Entity e)) continue;
                                if (src.Contains(e.LayerId)) { try { e.UpgradeOpen(); e.LayerId = targetId; moved++; } catch (Exception) { } }
                                if (e is BlockReference br)
                                    foreach (ObjectId aid in br.AttributeCollection)
                                        if (tr.GetObject(aid, OpenMode.ForRead, false, true) is AttributeReference ar && src.Contains(ar.LayerId))
                                        { try { ar.UpgradeOpen(); ar.LayerId = targetId; moved++; } catch (Exception) { } }
                            }
                        }
                    }
                    foreach (var rec in relock) if (!src.Contains(rec.ObjectId)) rec.IsLocked = true;   // layer nhận khoá lại; layer nguồn sắp xoá
                    tr.Commit();
                }

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var ids = new ObjectIdCollection();
                    var want = new List<ObjectId>();
                    foreach (var c in chosen)
                    {
                        if (!lt.Has(c.Name)) continue;
                        var id = lt[c.Name];
                        if (id == db.Clayer) db.Clayer = lt["0"];
                        ids.Add(id); want.Add(id);
                    }
                    if (ids.Count > 0) db.Purge(ids);   // chỉ giữ lại id không còn bị tham chiếu
                    var ok = new HashSet<ObjectId>(ids.Cast<ObjectId>());
                    foreach (var id in want)
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (!ok.Contains(id)) { kept.Add(rec.Name + " (còn được tham chiếu)"); continue; }
                        try { rec.UpgradeOpen(); rec.IsLocked = false; rec.Erase(); deleted++; }
                        catch (Exception ex) { kept.Add(rec.Name + " (" + ex.Message + ")"); }
                    }
                    tr.Commit();
                }
            }
            var sb = new StringBuilder($"\nĐã xoá {deleted} layer");
            if (moved > 0) sb.Append($", chuyển {moved} đối tượng sang layer \"{target}\"");
            sb.Append(".");
            if (kept.Count > 0) sb.Append("\n  Giữ lại: " + string.Join("; ", kept.Take(20)) + (kept.Count > 20 ? $"; … (+{kept.Count - 20})" : ""));
            sb.Append("\n  Gõ U để hoàn tác.");
            ed.WriteMessage(sb.ToString());
            try { VxtPalette.RefreshCounts(); } catch (Exception) { }
        }
    }
}

namespace KetCauVXT.AcadLink
{
    using System.Windows.Forms;
    using Drawing = System.Drawing;

    /// <summary>Bảng DỌN LAYER RÁC: danh sách layer trống / tên rác, tích chọn, layer nhận đối tượng, nút XOÁ → bảng XÁC NHẬN XOÁ.</summary>
    internal sealed class LayerCleanForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly ComboBox _target = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly Label _msg = new Label { AutoSize = true, ForeColor = Drawing.Color.DimGray };
        private readonly List<LayerCleanCmd.Info> _infos;

        public List<LayerCleanCmd.Info> Chosen { get; private set; }
        public string Target { get; private set; } = "0";

        public LayerCleanForm(List<LayerCleanCmd.Info> infos, List<string> allLayers)
        {
            _infos = infos;
            Text = "Việt Const – dọn layer rác (VXT_DONLAYER)";
            Width = 900; Height = 640; StartPosition = FormStartPosition.CenterScreen; MinimizeBox = false;
            Font = new Drawing.Font("Segoe UI", 9f);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 8, 10, 4) };
            top.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Drawing.Size(860, 0),
                Text = "Layer TRỐNG (không còn đối tượng, kể cả trong block) được tích sẵn. Layer TÊN RÁC (XREF$0$…, Layer1, Copy of…, …(2)…) còn đối tượng: tự tích – "
                     + "đối tượng sẽ được chuyển sang layer chọn bên dưới trước khi xoá. Layer 0, Defpoints, xref luôn giữ."
            });
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            Button B(string t, Action a) { var b = new Button { Text = t, AutoSize = true }; b.Click += (s, e) => a(); row.Controls.Add(b); return b; }
            B("Chọn layer trống", () => Mark(i => i.Count == 0));
            B("Chọn layer tên rác", () => Mark(i => i.Junk));
            B("Chọn tất cả", () => Mark(i => true));
            B("Bỏ chọn tất cả", () => Mark(i => false));
            top.Controls.Add(row);

            _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Xoa", HeaderText = "Xoá", FillWeight = 7 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Layer", HeaderText = "Layer", ReadOnly = true, FillWeight = 45 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Loai", HeaderText = "Loại", ReadOnly = true, FillWeight = 30 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SoDt", HeaderText = "Số đối tượng", ReadOnly = true, FillWeight = 14 });
            foreach (var it in infos)
            {
                int i = _grid.Rows.Add(it.Count == 0 && !it.Standard, it.Name, it.Kind, it.Count.ToString(CultureInfo.InvariantCulture));
                _grid.Rows[i].Tag = it;
                if (it.Count > 0) _grid.Rows[i].DefaultCellStyle.ForeColor = Drawing.Color.FromArgb(0xB4, 0x53, 0x09);
            }
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) => UpdateMsg();
            Controls.Add(_grid);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 6, 10, 0) };
            var tg = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            tg.Controls.Add(new Label { Text = "Chuyển đối tượng của layer còn đối tượng sang:", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
            _target.Items.Add("0");
            foreach (var n in allLayers) if (n != "0") _target.Items.Add(n);
            _target.SelectedIndex = 0;
            tg.Controls.Add(_target);
            bottom.Controls.Add(tg);
            bottom.Controls.Add(_msg);
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 6, 10, 10) };
            var cancel = new Button { Text = "Huỷ", AutoSize = true, MinimumSize = new Drawing.Size(90, 30) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var go = new Button { Text = "XOÁ LAYER ĐÃ CHỌN", AutoSize = true, MinimumSize = new Drawing.Size(190, 30), Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), BackColor = Drawing.Color.FromArgb(0xB9, 0x1C, 0x1C), ForeColor = Drawing.Color.White, FlatStyle = FlatStyle.Flat };
            go.Click += (s, e) => Go();
            btns.Controls.Add(cancel);
            btns.Controls.Add(go);
            Controls.Add(bottom);
            Controls.Add(btns);
            Controls.Add(top);
            CancelButton = cancel;
            UpdateMsg();
        }

        private IEnumerable<(DataGridViewRow Row, LayerCleanCmd.Info Info)> Rows()
        {
            foreach (DataGridViewRow r in _grid.Rows) if (r.Tag is LayerCleanCmd.Info i) yield return (r, i);
        }

        private List<LayerCleanCmd.Info> Checked()
        {
            try { _grid.EndEdit(); } catch (Exception) { }
            return Rows().Where(x => x.Row.Cells["Xoa"].Value is bool b && b).Select(x => x.Info).ToList();
        }

        private void Mark(Func<LayerCleanCmd.Info, bool> pred)
        {
            foreach (var x in Rows()) x.Row.Cells["Xoa"].Value = pred(x.Info);
            UpdateMsg();
        }

        private void UpdateMsg()
        {
            var c = Checked();
            int withObj = c.Count(i => i.Count > 0);
            _msg.Text = $"{_infos.Count} layer trống / tên rác; đang tích {c.Count} layer"
                      + (withObj > 0 ? $" ({withObj} layer còn {c.Sum(i => i.Count)} đối tượng sẽ chuyển sang layer chọn ở trên)." : ".");
        }

        private void Go()
        {
            var c = Checked();
            string target = _target.SelectedItem as string ?? "0";
            if (c.Count == 0) { MessageBox.Show(this, "Chưa tích layer nào để xoá.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (c.Any(i => i.Count > 0) && c.Any(i => i.Name.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, $"Layer nhận đối tượng \"{target}\" đang nằm trong danh sách xoá – hãy chọn layer khác.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var f = new LayerCleanConfirmForm(c, target))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            Chosen = c; Target = target;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Bảng XÁC NHẬN XOÁ LAYER: liệt kê layer sẽ xoá và việc với đối tượng; chỉ khi bấm "XÁC NHẬN XOÁ" mới thực hiện.</summary>
    internal sealed class LayerCleanConfirmForm : Form
    {
        public LayerCleanConfirmForm(List<LayerCleanCmd.Info> items, string target)
        {
            Text = "Xác nhận xoá layer";
            Width = 760; Height = 520; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
            Font = new Drawing.Font("Segoe UI", 9f);
            int n = items.Sum(i => i.Count);
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 8, 10, 4) };
            top.Controls.Add(new Label { AutoSize = true, Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), Text = $"Sẽ xoá {items.Count} layer." });
            top.Controls.Add(new Label { AutoSize = true, Text = n > 0 ? $"{n} đối tượng trên các layer này sẽ chuyển sang layer \"{target}\" trước khi xoá." : "Các layer này không còn đối tượng nào." });
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("Layer", "Layer");
            grid.Columns.Add("Loai", "Loại");
            grid.Columns.Add("ViecLam", "Việc sẽ làm");
            foreach (var i in items)
                grid.Rows.Add(i.Name, i.Kind, i.Count > 0 ? $"chuyển {i.Count} đối tượng → \"{target}\", rồi xoá" : "xoá");
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 6, 10, 10) };
            var back = new Button { Text = "Quay lại", AutoSize = true, MinimumSize = new Drawing.Size(100, 30) };
            back.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var ok = new Button { Text = "XÁC NHẬN XOÁ", AutoSize = true, MinimumSize = new Drawing.Size(170, 30), Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), BackColor = Drawing.Color.FromArgb(0xB9, 0x1C, 0x1C), ForeColor = Drawing.Color.White, FlatStyle = FlatStyle.Flat };
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            btns.Controls.Add(back);
            btns.Controls.Add(ok);
            btns.Controls.Add(new Label { AutoSize = true, Text = "Gõ U trong AutoCAD để hoàn tác.", Margin = new Padding(0, 8, 16, 0), ForeColor = Drawing.Color.DimGray });
            Controls.Add(grid);
            Controls.Add(btns);
            Controls.Add(top);
            CancelButton = back;
        }
    }
}
