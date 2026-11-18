using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;

namespace KetCauVXT.RevitExport
{
    /// <summary>Một thay đổi đề xuất. Người dùng chọn / bỏ trước khi áp dụng.</summary>
    internal sealed class ChangeItem
    {
        public string Group = "";
        public string Text = "";
        public string Note = "";
        public bool Checked = true;
        /// <summary>Thứ tự áp dụng (nhỏ trước): 0 đơn vị, vật liệu; 1 loại; 2 đổi tên; 3 đổi loại / tạo cấu kiện; 4 thông tin, nét in.</summary>
        public int Order = 3;
        public Action<Document> Apply;
        /// <summary>Đổi tên: phần tử + tên mới (áp dụng hai pha để tránh trùng tên).</summary>
        public Element RenameTarget;
        public string NewName;
    }

    /// <summary>Hộp thoại xem trước: danh sách thay đổi có ô chọn, áp dụng các mục được chọn.</summary>
    internal sealed class ChangeDialog : System.Windows.Forms.Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly List<ChangeItem> _items;

        public ChangeDialog(string title, string intro, List<ChangeItem> items)
        {
            _items = items;
            Text = title;
            Width = 1000; Height = 640;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            var lbl = new Label { Text = intro, Dock = DockStyle.Top, Height = 54, Padding = new Padding(8, 8, 8, 0) };
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Áp dụng", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nhóm", Width = 150, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Thay đổi", Width = 470, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
            foreach (var it in items) _grid.Rows.Add(it.Checked, it.Group, it.Text, it.Note);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var ok = new Button { Text = "Áp dụng các mục đã chọn", Width = 190, Height = 30, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Huỷ", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            var none = new Button { Text = "Bỏ chọn hết", Width = 110, Height = 30 };
            var all = new Button { Text = "Chọn hết", Width = 90, Height = 30 };
            none.Click += (s, e) => { foreach (DataGridViewRow r in _grid.Rows) r.Cells[0].Value = false; };
            all.Click += (s, e) => { foreach (DataGridViewRow r in _grid.Rows) r.Cells[0].Value = true; };
            bottom.Controls.Add(ok); bottom.Controls.Add(cancel); bottom.Controls.Add(none); bottom.Controls.Add(all);
            AcceptButton = ok; CancelButton = cancel;
            Controls.Add(_grid); Controls.Add(lbl); Controls.Add(bottom);
            FormClosing += (s, e) =>
            {
                _grid.EndEdit();
                for (int i = 0; i < _items.Count && i < _grid.Rows.Count; i++) _items[i].Checked = _grid.Rows[i].Cells[0].Value is bool b && b;
            };
        }

        /// <summary>Hiện danh sách; OK → áp dụng các mục đã chọn trong một Transaction. Trả về báo cáo (null = huỷ).</summary>
        public static string Run(Document doc, string title, string intro, List<ChangeItem> items)
        {
            if (items.Count == 0) { AgentMode.Report = "Không có gì cần thay đổi."; if (!AgentMode.Quiet) MessageBox.Show("Không có gì cần thay đổi – mô hình đã thống nhất với Việt Const.", title); return ""; }
            // Chế độ tự áp dụng chỉ dành cho lệnh kiến trúc do agent gọi; các lệnh kết cấu luôn hiện hộp thoại để người dùng duyệt.
            if (!AgentMode.AutoApply)
                using (var dlg = new ChangeDialog(title, intro, items))
                    if (dlg.ShowDialog() != DialogResult.OK) { AgentMode.Report = "Người dùng huỷ."; return null; }
            var chosen = items.Where(i => i.Checked).OrderBy(i => i.Order).ToList();
            if (chosen.Count == 0) return "";
            int ok = 0; var errs = new List<string>(); var warns = new List<string>();
            using (var t = new Transaction(doc, title))
            {
                t.Start();
                if (AgentMode.AutoApply) BuildArchitectureCommand.Swallow(t, warns);
                // đổi tên hai pha: tên tạm trước, tên thật sau – tránh trùng tên giữa các phần tử đổi chéo nhau
                var ren = chosen.Where(i => i.RenameTarget != null && !string.IsNullOrEmpty(i.NewName)).ToList();
                int k = 0;
                foreach (var r in ren) { try { r.RenameTarget.Name = "~vxt" + (k++) + "_" + r.RenameTarget.Id; } catch (Exception ex) { errs.Add(r.Text + ": " + ex.Message); } }
                foreach (var it in chosen)
                {
                    try
                    {
                        if (it.RenameTarget != null && !string.IsNullOrEmpty(it.NewName))
                        {
                            string n = it.NewName;
                            if (NameTaken(doc, it.RenameTarget, n)) n = n + " (" + it.RenameTarget.Id + ")";
                            it.RenameTarget.Name = n;
                        }
                        else it.Apply?.Invoke(doc);
                        ok++;
                    }
                    catch (Exception ex) { errs.Add(it.Text + ": " + ex.Message); }
                }
                t.Commit();
            }
            string rep = $"Đã áp dụng {ok}/{chosen.Count} thay đổi." + (errs.Count > 0 ? "\n\nKhông làm được:\n• " + string.Join("\n• ", errs.Take(25)) : "")
                         + (warns.Count > 0 ? "\n\nCảnh báo Revit:\n• " + string.Join("\n• ", warns.Take(25)) : "");
            AgentMode.Report = rep;
            if (!AgentMode.Quiet) MessageBox.Show(rep, title, MessageBoxButtons.OK, errs.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            return rep;
        }

        private static bool NameTaken(Document doc, Element e, string name)
        {
            if (e is Level) return new FilteredElementCollector(doc).OfClass(typeof(Level)).Any(x => x.Id != e.Id && x.Name == name);
            if (e is Grid) return new FilteredElementCollector(doc).OfClass(typeof(Grid)).Any(x => x.Id != e.Id && x.Name == name);
            if (e is FamilySymbol fs) return fs.Family.GetFamilySymbolIds().Any(id => id != e.Id && doc.GetElement(id)?.Name == name);
            if (e is ElementType et) return new FilteredElementCollector(doc).OfClass(e.GetType()).Any(x => x.Id != e.Id && x.Name == name);
            return false;
        }
    }
}
