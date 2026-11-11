using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Drawing = System.Drawing;

namespace KetCauVXT.AcadLink
{
    /// <summary>Bảng lệnh Việt Const (PaletteSet, lệnh VXT): nút gán nhóm, khai báo tầng, xem, xoá, chuẩn layer, gửi dữ liệu + số đối tượng đã gán.</summary>
    internal static class VxtPalette
    {
        private static Autodesk.AutoCAD.Windows.PaletteSet _ps;
        private static VxtPaletteControl _ctl;

        public static void Show()
        {
            try
            {
                if (_ps == null)
                {
                    _ps = new Autodesk.AutoCAD.Windows.PaletteSet("Việt Const", new Guid("3f6a2c1e-8d4b-4f7a-9c2e-5b1d7e0a9f31"));
                    _ctl = new VxtPaletteControl();
                    _ps.Add("Liên kết", _ctl);
                    _ps.MinimumSize = new Drawing.Size(260, 520);
                }
                _ps.Visible = true;
                _ctl.RefreshCounts();
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nKhông mở được bảng lệnh: " + ex.Message +
                    "\nDùng trực tiếp các lệnh VXT_TUONG, VXT_COT, VXT_DAM, VXT_SAN, VXT_TRUC, VXT_CUA, VXT_THANG, VXT_TENPHONG, VXT_TANG, VXT_XEM, VXT_XOA, VXT_LAYER, VXT_GUI, VXT_KT (lấy kích thước → Triển khai hồ sơ), VXT_MB_KC (gửi mặt bằng kết cấu → Triển khai hồ sơ), VXT_CHUANLAYER / VXT_CL (chuẩn hoá layer bản vẽ về layer VXT-…).");
            }
        }

        public static void RefreshCounts()
        {
            try { if (_ps != null && _ps.Visible) _ctl?.RefreshCounts(); }
            catch (System.Exception) { }
        }
    }

    internal sealed class VxtPaletteControl : UserControl
    {
        private readonly ListBox _list = new ListBox();

        public VxtPaletteControl()
        {
            Dock = DockStyle.Fill;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(6) };
            Controls.Add(flow);
            var title = new Label { Text = "Tích chọn đối tượng → gán nhóm", AutoSize = true, Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), Margin = new Padding(3, 3, 3, 6) };
            flow.Controls.Add(title);
            var cats = new[]
            {
                ("Tường", "VXT_TUONG"), ("Cột", "VXT_COT"), ("Dầm", "VXT_DAM"), ("Sàn", "VXT_SAN"), ("Lưới trục", "VXT_TRUC"),
                ("Cửa", "VXT_CUA"), ("Cầu thang", "VXT_THANG"), ("Tên phòng", "VXT_TENPHONG"), ("Bỏ qua", "VXT_BOQUA"),
            };
            foreach (var c in cats) flow.Controls.Add(Btn(c.Item1, c.Item2, false));
            flow.Controls.Add(new Label { Text = "Tầng, kiểm tra, chuẩn layer", AutoSize = true, Font = new Drawing.Font(Font, Drawing.FontStyle.Bold), Margin = new Padding(3, 10, 3, 6) });
            flow.Controls.Add(Btn("Khai báo tầng (tên, cao độ, điểm gốc, vùng)", "VXT_TANG", false));
            flow.Controls.Add(Btn("Xem theo nhóm (tô sáng)", "VXT_XEM", false));
            flow.Controls.Add(Btn("Xoá gán nhóm", "VXT_XOA", false));
            flow.Controls.Add(Btn("Tạo / chuyển đổi layer kết cấu", "VXT_LAYER", false));
            flow.Controls.Add(Btn("Chuẩn hoá layer bản vẽ → VXT-… (đọc trong Việt Const)", "VXT_CHUANLAYER", false));
            flow.Controls.Add(Btn("Đọc cao độ tầng từ mặt đứng / mặt cắt", "VXT_MATDUNG", false));
            flow.Controls.Add(Btn("GỬI SANG VIỆT CONST", "VXT_GUI", true));
            flow.Controls.Add(Btn("NHẬN KẾT QUẢ từ Việt Const (tên, tiết diện, thép, móng)", "VXT_NHAN", true));
            flow.Controls.Add(Btn("Chèn bản vẽ chi tiết (không khung tên) – tự dàn trang", "VXT_CHEN", false));
            flow.Controls.Add(Btn("Lấy kích thước → Triển khai hồ sơ", "VXT_KT", false));
            flow.Controls.Add(Btn("Gửi mặt bằng kết cấu → Triển khai hồ sơ", "VXT_MB_KC", false));
            flow.Controls.Add(Btn("⟳ Cập nhật Việt Const (tải bản mới)", "VXT_CAPNHAT", false));
            var refresh = new Button { Text = "Làm mới số liệu", Width = 230, Height = 26, Margin = new Padding(3, 10, 3, 3) };
            refresh.Click += (s, e) => RefreshCounts();
            flow.Controls.Add(refresh);
            _list.Width = 230; _list.Height = 220; _list.IntegralHeight = false; _list.HorizontalScrollbar = true;
            flow.Controls.Add(_list);
        }

        private Button Btn(string text, string cmd, bool bold)
        {
            var b = new Button { Text = text, Width = 230, Height = bold ? 34 : 26, Margin = new Padding(3, 2, 3, 2), TextAlign = Drawing.ContentAlignment.MiddleLeft };
            if (bold) b.Font = new Drawing.Font(Font, Drawing.FontStyle.Bold);
            b.Click += (s, e) => Send(cmd);
            return b;
        }

        private static void Send(string cmd)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            // huỷ lệnh đang chạy rồi gọi lệnh mới
            doc.SendStringToExecute("\x03\x03" + cmd + " ", true, false, true);
        }

        public void RefreshCounts()
        {
            _list.Items.Clear();
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { _list.Items.Add("(chưa mở bản vẽ)"); return; }
            var db = doc.Database;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Dictionary<string, string> map;
                    var scan = Commands.Scan(db, tr, out map);
                    var storeys = DwgStore.GetStoreys(db, tr);
                    tr.Commit();
                    _list.Items.Add("Đối tượng đã gán nhóm:");
                    foreach (var c in Cat.All) _list.Items.Add($"  {Cat.Display(c)}: {scan[c].Count}");
                    if (map.Count > 0)
                    {
                        _list.Items.Add("Layer gán nhóm:");
                        foreach (var kv in map.OrderBy(k => k.Key)) _list.Items.Add($"  {kv.Key} → {Cat.Display(kv.Value)}");
                    }
                    _list.Items.Add($"Tầng ({storeys.Count}):");
                    foreach (var s in storeys.OrderBy(s => s.Elevation)) _list.Items.Add($"  {s.Name}  {s.Elevation:+0.000;-0.000;±0.000}  h={s.Height:0.00}");
                    if (storeys.Count == 0) _list.Items.Add("  (chưa khai báo – VXT_TANG)");
                }
            }
            catch (System.Exception ex) { _list.Items.Add("Lỗi: " + ex.Message); }
        }
    }
}
