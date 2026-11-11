using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Drawing = System.Drawing;

namespace KetCauVXT.AcadLink
{
    /// <summary>Xử lý layer gốc sau khi chuyển đối tượng sang layer chuẩn.</summary>
    internal enum OldLayerAction { Keep, Delete, Rename }

    internal sealed class OldLayerPlan
    {
        public OldLayerAction Action = OldLayerAction.Keep;
        public string NewName = "";
    }

    /// <summary>Bảng XÁC NHẬN CHUYỂN ĐỔI LAYER (VXT_CHUANLAYER): liệt kê layer gốc → layer chuẩn, chọn cách xử lý layer cũ
    /// sau khi chuyển (giữ nguyên / xoá / đổi tên). Chỉ khi bấm "XÁC NHẬN CHUYỂN ĐỔI" mới thực hiện.</summary>
    internal sealed class LayerConvertConfirmForm : Form
    {
        public sealed class Item { public string Layer, Target; public int Count; }

        private const string AKeep = "Giữ nguyên", ADelete = "Xoá layer cũ", ARename = "Đổi tên layer cũ";
        private static readonly string[] Actions = { AKeep, ADelete, ARename };

        private readonly DataGridView _grid = new DataGridView();
        private readonly ComboBox _all = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly TextBox _suffix = new TextBox { Width = 70, Text = "_CU" };
        private readonly List<Item> _items;

        /// <summary>Kết quả: cách xử lý từng layer gốc (chỉ có khi bấm XÁC NHẬN).</summary>
        public Dictionary<string, OldLayerPlan> Plans { get; private set; }

        /// <summary>Lựa chọn nhớ trong phiên AutoCAD.</summary>
        internal static OldLayerAction LastAction = OldLayerAction.Delete;
        internal static string LastSuffix = "_CU";

        public LayerConvertConfirmForm(List<Item> items, string how, int skipped)
        {
            _items = items;
            Text = "Xác nhận chuyển đổi layer";
            Width = 980; Height = 600; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = true;
            Font = new Drawing.Font("Segoe UI", 9f);

            // ---- trên: tóm tắt ----
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 8, 10, 4) };
            top.Controls.Add(new Label
            {
                AutoSize = true, Font = new Drawing.Font(Font, Drawing.FontStyle.Bold),
                Text = $"Sẽ chuyển {items.Count} layer ({items.Sum(i => i.Count)} đối tượng) – cách áp dụng: {how}."
            });
            if (skipped > 0) top.Controls.Add(new Label { AutoSize = true, Text = $"Giữ nguyên {skipped} layer đã bỏ tích \"Chuyển\"." });
            top.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = Drawing.Color.DimGray, MaximumSize = new Drawing.Size(940, 0),
                Text = "Cột \"Layer cũ sau khi chuyển\": Giữ nguyên / Xoá layer cũ / Đổi tên layer cũ (sửa ô Tên mới). "
                     + "Xoá chỉ thực hiện khi layer cũ không còn đối tượng (kể cả trong block); layer 0, Defpoints, layer đang dùng (hiện hành) và layer xref luôn được giữ."
            });
            var allRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            allRow.Controls.Add(new Label { Text = "Áp dụng cho tất cả:", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            _all.Items.AddRange(Actions);
            _all.SelectedIndex = (int)LastAction;
            allRow.Controls.Add(_all);
            allRow.Controls.Add(new Label { Text = "Hậu tố khi đổi tên:", AutoSize = true, Margin = new Padding(12, 7, 4, 0) });
            _suffix.Text = LastSuffix;
            allRow.Controls.Add(_suffix);
            var setAll = new Button { Text = "Gán cho tất cả", AutoSize = true };
            setAll.Click += (s, e) => SetAll();
            allRow.Controls.Add(setAll);
            top.Controls.Add(allRow);

            // ---- giữa: bảng ----
            _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Layer", HeaderText = "Layer gốc", ReadOnly = true, FillWeight = 24 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "→ Layer chuẩn", ReadOnly = true, FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Số đối tượng", ReadOnly = true, FillWeight = 9 });
            var act = new DataGridViewComboBoxColumn { Name = "Act", HeaderText = "Layer cũ sau khi chuyển", FillWeight = 20, FlatStyle = FlatStyle.Flat };
            act.Items.AddRange(Actions);
            _grid.Columns.Add(act);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewName", HeaderText = "Tên mới (khi đổi tên)", FillWeight = 22 });
            foreach (var it in items)
            {
                int i = _grid.Rows.Add(it.Layer, it.Target, it.Count.ToString(CultureInfo.InvariantCulture), Actions[(int)LastAction], it.Layer + LastSuffix);
                _grid.Rows[i].Tag = it;
                StyleRow(_grid.Rows[i]);
            }
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == _grid.Columns["Act"].Index) StyleRow(_grid.Rows[e.RowIndex]); };
            Controls.Add(_grid);

            // ---- dưới: nút ----
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 6, 10, 10) };
            var back = new Button { Text = "Quay lại", AutoSize = true, Height = 30, MinimumSize = new Drawing.Size(100, 30) };
            back.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var ok = new Button { Text = "XÁC NHẬN CHUYỂN ĐỔI", AutoSize = true, MinimumSize = new Drawing.Size(200, 30), Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), BackColor = Drawing.Color.FromArgb(0x1F, 0x6F, 0xB2), ForeColor = Drawing.Color.White, FlatStyle = FlatStyle.Flat };
            ok.Click += (s, e) => Confirm();
            btns.Controls.Add(back);
            btns.Controls.Add(ok);
            Controls.Add(btns);
            Controls.Add(top);
            CancelButton = back;   // Esc = quay lại; không đặt AcceptButton – phải bấm nút XÁC NHẬN
        }

        private void StyleRow(DataGridViewRow row)
        {
            bool ren = (row.Cells["Act"].Value as string) == ARename;
            row.Cells["NewName"].ReadOnly = !ren;
            row.Cells["NewName"].Style.ForeColor = ren ? _grid.DefaultCellStyle.ForeColor : Drawing.Color.Silver;
            row.Cells["Act"].Style.ForeColor = (row.Cells["Act"].Value as string) == ADelete ? Drawing.Color.Firebrick : _grid.DefaultCellStyle.ForeColor;
        }

        private void SetAll()
        {
            try { _grid.EndEdit(); } catch (Exception) { }
            string a = _all.SelectedItem as string ?? AKeep;
            string suf = _suffix.Text ?? "";
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var it = row.Tag as Item;
                row.Cells["Act"].Value = a;
                if (a == ARename && it != null) row.Cells["NewName"].Value = it.Layer + suf;
                StyleRow(row);
            }
        }

        private void Confirm()
        {
            try { _grid.EndEdit(); } catch (Exception) { }
            var plans = new Dictionary<string, OldLayerPlan>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var it = row.Tag as Item;
                if (it == null) continue;
                string a = row.Cells["Act"].Value as string ?? AKeep;
                var p = new OldLayerPlan { Action = a == ADelete ? OldLayerAction.Delete : a == ARename ? OldLayerAction.Rename : OldLayerAction.Keep };
                if (p.Action == OldLayerAction.Rename)
                {
                    p.NewName = ((row.Cells["NewName"].Value as string) ?? "").Trim();
                    string err = CheckName(p.NewName);
                    if (err == null && !names.Add(p.NewName)) err = "trùng với tên mới của dòng khác";
                    if (err == null && p.NewName.Equals(it.Layer, StringComparison.OrdinalIgnoreCase)) err = "giống tên cũ";
                    if (err != null)
                    {
                        _grid.CurrentCell = row.Cells["NewName"];
                        MessageBox.Show(this, $"Tên mới cho layer \"{it.Layer}\" không hợp lệ: {err}.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                plans[it.Layer] = p;
            }
            LastAction = (OldLayerAction)Math.Max(0, _all.SelectedIndex);
            LastSuffix = _suffix.Text ?? "_CU";
            Plans = plans;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string CheckName(string n)
        {
            if (string.IsNullOrWhiteSpace(n)) return "để trống";
            if (n.Length > 255) return "dài quá 255 ký tự";
            if (n.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0) return "có ký tự không cho phép (< > / \\ \" : ; ? * | , = `)";
            return null;
        }
    }
}
