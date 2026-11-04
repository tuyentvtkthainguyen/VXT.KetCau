using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using VXT.KetCau.Core.Documentation;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Input;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.AutoCAD
{
    /// <summary>Bảng điều khiển triển khai hồ sơ.</summary>
    public class HoSoPanelForm : Form
    {
        public string Action { get; private set; }

        public HoSoPanelForm(ProjectState st, string drawing)
        {
            Text = "VXT – Triển khai hồ sơ thiết kế — " + System.IO.Path.GetFileName(drawing);
            Size = new Size(560, 640); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14), WrapContents = false, AutoScroll = true };
            string a = st.Arch != null ? $"Kiến trúc: {st.Arch.Levels.Count} cao độ, {st.Arch.Walls.Count} tường, {st.Arch.Openings.Count} cửa, {st.Arch.Rooms.Count} phòng." : "Chưa có dữ liệu kiến trúc.";
            string m = st.Model != null ? $"Kết cấu: {st.Model.Columns.Count} cột, {st.Model.Beams.Count} dầm, {st.Model.Slabs.Count} ô sàn." : "Chưa có mô hình kết cấu.";
            flow.Controls.Add(new Label { Text = a + "\n" + m, AutoSize = false, Width = 510, Height = 50, ForeColor = Color.DimGray });

            void Head(string t) => flow.Controls.Add(new Label { Text = t, AutoSize = false, Width = 510, Height = 26, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft });
            void Btn(string text, string act, string hint)
            {
                var b = new Button { Text = text, Width = 510, Height = 36, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
                b.Click += (s, e) => { Action = act; DialogResult = DialogResult.OK; Close(); };
                new ToolTip().SetToolTip(b, hint);
                flow.Controls.Add(b);
            }
            Head("1. Đầu vào — chọn một cách");
            Btn("Quét mặt bằng trong bản vẽ này (khoanh từng tầng)", "QUET", "Nhiều tầng vẽ chung một file: khoanh khung từng tầng, chọn điểm gốc");
            Btn("Đọc mặt bằng từ các file DWG/DXF", "FILE", "Mỗi tầng một file");
            Btn("Nhập thông số (nhịp, tầng, tiết diện)", "THONGSO", "Không cần bản vẽ");
            Btn("Đọc mô hình ETABS / SAP2000 đang mở", "ETABS", "Lấy dầm, cột, sàn, nội lực và thép đã thiết kế");
            Head("2. Kiểm tra");
            Btn("Xem và sửa dữ liệu kiến trúc đã nhận", "KIEMTRA", "Trục, tường, cột, cửa, phòng, thang");
            Btn("Sửa tiết diện và đặt thép tay", "SUAKC", "Dầm, cột, sàn");
            Btn("Thông số thiết kế và thông tin khung tên", "THONGTIN", "Vật liệu, tải; tên công trình, khổ giấy, tỉ lệ");
            Head("3. Lập hồ sơ");
            Btn("Vẽ hồ sơ vào bản vẽ này", "VE", "Tự thiết kế nếu chưa có, xếp tờ, khung tên, tạo layout in");
            Btn("Xuất hồ sơ ra file DXF riêng", "DXF", "");
            var close = new Button { Text = "Đóng", Width = 510, Height = 32, DialogResult = DialogResult.Cancel };
            flow.Controls.Add(close);
            Controls.Add(flow);
            CancelButton = close;
        }
    }

    /// <summary>Thông tin khung tên và lựa chọn dựng hồ sơ, nhãn tiếng Việt.</summary>
    public class DocOptionsView
    {
        readonly DocOptions d;
        public DocOptionsView(DocOptions o) { d = o; }
        const string A = "1. Khung tên", B = "2. Khổ giấy, tỉ lệ", C = "3. Nội dung";
        [Category(A), DisplayName("Đơn vị thiết kế")] public string Company { get => d.Company; set => d.Company = value; }
        [Category(A), DisplayName("Công trình")] public string ProjectName { get => d.ProjectName; set => d.ProjectName = value; }
        [Category(A), DisplayName("Chủ đầu tư")] public string Owner { get => d.Owner; set => d.Owner = value; }
        [Category(A), DisplayName("Địa điểm")] public string Address { get => d.Address; set => d.Address = value; }
        [Category(A), DisplayName("Chủ trì")] public string Director { get => d.Director; set => d.Director = value; }
        [Category(A), DisplayName("Thiết kế")] public string Designer { get => d.Designer; set => d.Designer = value; }
        [Category(A), DisplayName("Kiểm tra")] public string Checker { get => d.Checker; set => d.Checker = value; }
        [Category(A), DisplayName("Ngày")] public string Date { get => d.Date; set => d.Date = value; }
        [Category(A), DisplayName("Giai đoạn")] public string Stage { get => d.Stage; set => d.Stage = value; }
        [Category(B), DisplayName("Khổ giấy"), TypeConverter(typeof(PaperList)), Description("Hình lớn hơn khổ này sẽ tự sang khổ lớn hơn")] public string Paper { get => d.Paper; set => d.Paper = value; }
        [Category(B), DisplayName("Tỉ lệ mặt bằng, mặt đứng, mặt cắt (1/...)")] public double PlanScale { get => d.PlanScale; set => d.PlanScale = value; }
        [Category(B), DisplayName("Tỉ lệ chi tiết (1/...)"), Description("Mặt cắt thép vẽ phóng ×2 so với tỉ lệ này")] public double DetailScale { get => d.DetailScale; set => d.DetailScale = value; }
        [Category(C), DisplayName("Vẽ hồ sơ kiến trúc")] public bool Arch { get => d.DrawArchitecture; set => d.DrawArchitecture = value; }
        [Category(C), DisplayName("Vẽ hồ sơ kết cấu")] public bool Struct { get => d.DrawStructure; set => d.DrawStructure = value; }
        [Category(C), DisplayName("Tạo layout in cho từng tờ")] public bool Layouts { get => d.CreateLayouts; set => d.CreateLayouts = value; }
        [Category(C), DisplayName("Cao tường chắn mái (mm)")] public double Parapet { get => d.ParapetHeight; set => d.ParapetHeight = value; }
    }

    public class PaperList : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext c) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext c) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c) => new StandardValuesCollection(new[] { "A4", "A3", "A2", "A1", "A0" });
    }

    /// <summary>Hộp thoại PropertyGrid dùng chung.</summary>
    public class ObjectEditForm : Form
    {
        public ObjectEditForm(string title, object view, int w = 620, int h = 620)
        {
            Text = title; Size = new Size(w, h); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f);
            var grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = view, PropertySort = PropertySort.Categorized, ToolbarVisible = false };
            var ok = new Button { Text = "Đồng ý", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 110 };
            var cancel = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 110 };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
            bar.Controls.Add(ok); bar.Controls.Add(cancel);
            Controls.Add(grid); Controls.Add(bar);
            AcceptButton = ok; CancelButton = cancel;
        }
    }

    /// <summary>Thông số nhập tay, nhãn tiếng Việt.</summary>
    public class ParametricView
    {
        readonly ParametricInput p;
        public ParametricView(ParametricInput x) { p = x; }
        const string A = "1. Lưới trục và tầng", B = "2. Tiết diện chung", C = "3. Tường, thang";
        [Category(A), DisplayName("Tên công trình")] public string Name { get => p.ProjectName; set => p.ProjectName = value; }
        [Category(A), DisplayName("Nhịp theo X (mm)"), Description("Cách nhau dấu cách, vd 3600 4200 hoặc 3x3600 4200. Trục đặt tên 1, 2, 3…")] public string SpansX { get => p.SpansX; set => p.SpansX = value; }
        [Category(A), DisplayName("Nhịp theo Y (mm)"), Description("Trục đặt tên A, B, C…")] public string SpansY { get => p.SpansY; set => p.SpansY = value; }
        [Category(A), DisplayName("Các tầng (tên=cao độ m)"), Description("Vd Tầng 1=0; Tầng 2=3.6; Mái=7.2. Cao độ lớn nhất là mái.")] public string Levels { get => p.Levels; set => p.Levels = value; }
        [Category(B), DisplayName("Cột b (mm)")] public double ColB { get => p.ColumnB; set => p.ColumnB = value; }
        [Category(B), DisplayName("Cột h (mm)")] public double ColH { get => p.ColumnH; set => p.ColumnH = value; }
        [Category(B), DisplayName("Dầm b (mm)"), Description("0 = tự chọn theo nhịp")] public double BeamB { get => p.BeamB; set => p.BeamB = value; }
        [Category(B), DisplayName("Dầm h (mm)"), Description("0 = tự chọn theo nhịp")] public double BeamH { get => p.BeamH; set => p.BeamH = value; }
        [Category(B), DisplayName("Sàn dày (mm)"), Description("0 = tự chọn theo ô sàn")] public double Slab { get => p.SlabThickness; set => p.SlabThickness = value; }
        [Category(C), DisplayName("Tường dày (mm)")] public double Wall { get => p.WallThickness; set => p.WallThickness = value; }
        [Category(C), DisplayName("Xây tường trên mọi trục"), Description("Sai: chỉ tường bao")] public bool AllWalls { get => p.WallsOnAllGrids; set => p.WallsOnAllGrids = value; }
        [Category(C), DisplayName("Ô thang x,y,rộng,dài (mm)"), Description("Tính từ giao trục đầu, vd 4200,5000,2400,4000. Để trống nếu không có.")] public string Stair { get => p.Stair; set => p.Stair = value; }
    }

    /// <summary>Bảng dữ liệu nhiều thẻ: mỗi thẻ một lưới sửa được, thêm và xoá hàng được.</summary>
    public class GridTabsForm : Form
    {
        protected readonly TabControl Tabs = new TabControl { Dock = DockStyle.Fill };
        protected static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public GridTabsForm(string title, string hint)
        {
            Text = title; Size = new Size(1000, 640); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f);
            var lb = new Label { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6), Text = hint };
            var ok = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 120 };
            var cancel = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 120 };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
            bar.Controls.Add(ok); bar.Controls.Add(cancel);
            Controls.Add(Tabs); Controls.Add(lb); Controls.Add(bar);
            CancelButton = cancel;
            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                try { Apply(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); e.Cancel = true; }
            };
        }

        protected virtual void Apply() { }

        protected DataGridView AddTab(string name, bool canAdd, params string[] columns)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = canAdd, AllowUserToDeleteRows = canAdd, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersWidth = 32 };
            foreach (var c in columns) g.Columns.Add(c, c);
            var tab = new TabPage(name);
            tab.Controls.Add(g);
            Tabs.TabPages.Add(tab);
            return g;
        }

        protected static string F(double v) => v.ToString("0.###", Ci);

        protected static IEnumerable<DataGridViewRow> Rows(DataGridView g) => g.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow);

        protected static string Str(DataGridViewRow r, int c) => Convert.ToString(r.Cells[c].Value)?.Trim() ?? "";

        protected static double Num(DataGridViewRow r, int c, string where)
        {
            var t = Str(r, c).Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, Ci, out var v))
                throw new FormatException($"{where}, hàng {r.Index + 1}: \"{Str(r, c)}\" không phải số.");
            return v;
        }
    }

    /// <summary>Xem và sửa dữ liệu kiến trúc đã nhận diện.</summary>
    public class ReviewArchForm : GridTabsForm
    {
        readonly ArchModel a;
        readonly DataGridView lv, gr, wl, cl, op, rm, st;

        public ReviewArchForm(ArchModel arch) : base("Kiểm tra nhận diện kiến trúc",
            "Đối chiếu với lớp VXT_NHANDIEN (màu tím) trên bản vẽ. Sửa ô, xoá hàng (chọn hàng rồi Delete), thêm hàng ở dòng cuối. Toạ độ mm, tính từ điểm gốc đã chọn.")
        {
            a = arch;
            lv = AddTab("Tầng", true, "Tên tầng", "Cao độ (m)", "Mái (1/0)");
            foreach (var l in a.Levels.OrderBy(q => q.Elevation)) lv.Rows.Add(l.Name, F(l.Elevation / 1000), l.IsRoof ? "1" : "0");
            gr = AddTab("Trục", true, "Tên", "X1", "Y1", "X2", "Y2");
            foreach (var g in a.Grids) gr.Rows.Add(g.Name, F(g.Line.A.X), F(g.Line.A.Y), F(g.Line.B.X), F(g.Line.B.Y));
            wl = AddTab("Tường", true, "Tầng", "X1", "Y1", "X2", "Y2", "Dày", "Id");
            foreach (var w in a.Walls) wl.Rows.Add(w.BaseLevel, F(w.Line.A.X), F(w.Line.A.Y), F(w.Line.B.X), F(w.Line.B.Y), F(w.Thickness), w.Id);
            wl.Columns[6].ReadOnly = true;
            cl = AddTab("Cột", true, "Tầng", "X", "Y", "b", "h");
            foreach (var c in a.Columns) cl.Rows.Add(c.BaseLevel, F(c.Center.X), F(c.Center.Y), F(c.B), F(c.H));
            op = AddTab("Cửa", true, "Tầng", "Loại (Cửa đi / Cửa sổ)", "X", "Y", "Rộng", "Cao", "Bệ");
            foreach (var o in a.Openings) op.Rows.Add(o.Level, o.Kind, F(o.Location.X), F(o.Location.Y), F(o.Width), F(o.Height), F(o.SillHeight));
            rm = AddTab("Phòng", true, "Tầng", "Tên phòng", "X tâm", "Y tâm");
            foreach (var r in a.Rooms.Where(q => q.Boundary.Count >= 3)) rm.Rows.Add(r.Level, r.Name, F(r.Boundary.Average(q => q.X)), F(r.Boundary.Average(q => q.Y)));
            st = AddTab("Thang", true, "Tầng", "X", "Y", "Rộng", "Dài");
            foreach (var s in a.Stairs.Where(q => q.Boundary.Count >= 3))
            {
                double x0 = s.Boundary.Min(q => q.X), y0 = s.Boundary.Min(q => q.Y);
                st.Rows.Add(s.BaseLevel, F(x0), F(y0), F(s.Boundary.Max(q => q.X) - x0), F(s.Boundary.Max(q => q.Y) - y0));
            }
        }

        protected override void Apply()
        {
            // Đọc hết vào danh sách tạm để lỗi nhập không làm hỏng dữ liệu cũ
            var levels = Rows(lv).Select(r => new ArchLevel { Name = Str(r, 0), Elevation = Num(r, 1, "Tầng") * 1000, IsRoof = Str(r, 2) == "1" }).ToList();
            if (levels.Count < 2) throw new FormatException("Cần ít nhất 2 cao độ.");
            var names = new HashSet<string>(levels.Select(l => l.Name));
            string Lvl(DataGridViewRow r, string where)
            {
                var n = Str(r, 0);
                if (!names.Contains(n)) throw new FormatException($"{where}, hàng {r.Index + 1}: tầng \"{n}\" không có trong thẻ Tầng.");
                return n;
            }
            var grids = Rows(gr).Select(r => new ArchGrid { Name = Str(r, 0), Line = new Seg2(new P2(Num(r, 1, "Trục"), Num(r, 2, "Trục")), new P2(Num(r, 3, "Trục"), Num(r, 4, "Trục"))) }).ToList();
            var walls = Rows(wl).Select(r => new ArchWall
            {
                BaseLevel = Lvl(r, "Tường"), Line = new Seg2(new P2(Num(r, 1, "Tường"), Num(r, 2, "Tường")), new P2(Num(r, 3, "Tường"), Num(r, 4, "Tường"))),
                Thickness = Num(r, 5, "Tường"), Id = string.IsNullOrEmpty(Str(r, 6)) ? null : Str(r, 6),
                Material = Num(r, 5, "Tường") >= 200 ? "Gạch đặc" : "Gạch rỗng",
            }).ToList();
            var cols = Rows(cl).Select(r => new ArchColumn { BaseLevel = Lvl(r, "Cột"), Center = new P2(Num(r, 1, "Cột"), Num(r, 2, "Cột")), B = Num(r, 3, "Cột"), H = Num(r, 4, "Cột") }).ToList();
            var keepSwing = a.Openings.ToList();
            var opens = Rows(op).Select(r =>
            {
                var loc = new P2(Num(r, 2, "Cửa"), Num(r, 3, "Cửa"));
                var old = keepSwing.FirstOrDefault(o => o.Location.DistanceTo(loc) < 1);
                return new ArchOpening
                {
                    Level = Lvl(r, "Cửa"), Kind = Str(r, 1), Location = loc, Width = Num(r, 4, "Cửa"), Height = Num(r, 5, "Cửa"), SillHeight = Num(r, 6, "Cửa"),
                    HostWallId = old?.HostWallId, HingeEnd = old?.HingeEnd ?? 0, SwingSide = old?.SwingSide ?? 0,
                };
            }).ToList();
            var rooms = Rows(rm).Select(r =>
            {
                double x = Num(r, 2, "Phòng"), y = Num(r, 3, "Phòng");
                string n = Str(r, 1), lvl = Lvl(r, "Phòng");
                // phòng còn nguyên vị trí giữ đường bao cũ
                var old = a.Rooms.FirstOrDefault(q => q.Level == lvl && q.Boundary.Count >= 3 && Math.Abs(q.Boundary.Average(p => p.X) - x) < 1 && Math.Abs(q.Boundary.Average(p => p.Y) - y) < 1);
                return new ArchRoom
                {
                    Level = lvl, Name = n, Usage = n,
                    Boundary = old != null ? old.Boundary.ToList() : new List<P2> { new P2(x - 50, y - 50), new P2(x + 50, y - 50), new P2(x + 50, y + 50), new P2(x - 50, y + 50) },
                };
            }).ToList();
            var stairs = Rows(st).Select(r =>
            {
                double x = Num(r, 1, "Thang"), y = Num(r, 2, "Thang"), wd = Num(r, 3, "Thang"), ln = Num(r, 4, "Thang");
                return new ArchStair { BaseLevel = Lvl(r, "Thang"), Boundary = new List<P2> { new P2(x, y), new P2(x + wd, y), new P2(x + wd, y + ln), new P2(x, y + ln) } };
            }).ToList();

            a.Levels.Clear(); a.Levels.AddRange(levels);
            a.Grids.Clear(); a.Grids.AddRange(grids);
            a.Walls.Clear(); a.Walls.AddRange(walls);
            a.Columns.Clear(); a.Columns.AddRange(cols);
            a.Openings.Clear(); a.Openings.AddRange(opens);
            a.Rooms.Clear(); a.Rooms.AddRange(rooms);
            a.Stairs.Clear(); a.Stairs.AddRange(stairs);
        }
    }

    /// <summary>Sửa tiết diện, đặt thép tay. Ô thép để trống là tự chọn theo tính toán.</summary>
    public class EditStructForm : GridTabsForm
    {
        readonly StructModel m;
        readonly DataGridView beams, cols, slabs;
        readonly List<BeamElem> bl;
        readonly List<ColumnElem> cl;
        readonly List<SlabElem> sl;

        public EditStructForm(StructModel model) : base("Tiết diện và thép đặt tay",
            "Viết thép dọc dạng 3Ø20, đai và thép sàn dạng Ø6a150. Ô thép để trống là phần mềm tự chọn theo nội lực. Thép đặt tay vẫn được so với yêu cầu nếu có nội lực.")
        {
            m = model;
            bl = m.Beams.OrderBy(b => m.Stories.FindIndex(s => s.Name == b.Story)).ToList();
            cl = m.Columns.OrderBy(c => c.BaseZ).ToList();
            sl = m.Slabs.OrderBy(s => m.Stories.FindIndex(q => q.Name == s.Story)).ToList();
            beams = AddTab("Dầm", false, "Tầng", "Vị trí", "b", "h", "Thép trên", "Thép dưới", "Đai gối", "Đai nhịp");
            foreach (var b in bl)
            {
                var sec = m.Section(b.Section);
                beams.Rows.Add(b.Story, Where(b.Start, b.End), F(sec.B), F(sec.H), b.ManualTop, b.ManualBottom, b.ManualStirrupSupport, b.ManualStirrupSpan);
            }
            cols = AddTab("Cột", false, "Từ tầng", "Đến tầng", "Vị trí", "b", "h", "Thép dọc", "Đai");
            foreach (var c in cl)
            {
                var sec = m.Section(c.Section);
                cols.Rows.Add(c.BaseStory, c.TopStory, GridRef(c.Location), F(sec.B), F(sec.H), c.ManualBars, c.ManualStirrup);
            }
            slabs = AddTab("Sàn", false, "Tầng", "Ô", "Dày", "Thép dưới", "Thép trên");
            foreach (var s in sl)
            {
                var sec = m.SlabSection(s.Section);
                slabs.Rows.Add(s.Story, s.Label, F(sec?.Thickness ?? 0), s.ManualBottom, s.ManualTop);
            }
            foreach (var g in new[] { beams, cols, slabs })
                g.Columns[0].ReadOnly = true;
            beams.Columns[1].ReadOnly = true;
            cols.Columns[1].ReadOnly = true; cols.Columns[2].ReadOnly = true;
            slabs.Columns[1].ReadOnly = true;
        }

        string GridRef(P2 p)
        {
            var near = m.Grids.Where(g => g.Line.DistanceTo(p) < 100).Select(g => g.Name).ToList();
            return near.Count >= 2 ? string.Join("/", near) : p.ToString();
        }

        string Where(P2 a, P2 b)
        {
            var on = m.Grids.FirstOrDefault(g => g.Line.DistanceTo(a) < 100 && g.Line.DistanceTo(b) < 100);
            var cross = m.Grids.Where(g => g != on && g.Line.DistanceTo(a) < 100).Select(g => g.Name).FirstOrDefault();
            var cross2 = m.Grids.Where(g => g != on && g.Line.DistanceTo(b) < 100).Select(g => g.Name).FirstOrDefault();
            return on != null && cross != null && cross2 != null ? $"trục {on.Name} ({cross}-{cross2})" : $"{a}-{b}";
        }

        static string Opt(string t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

        protected override void Apply()
        {
            foreach (var r in Rows(beams))
            {
                var t = Opt(Str(r, 4)); var bo = Opt(Str(r, 5)); var s1 = Opt(Str(r, 6)); var s2 = Opt(Str(r, 7));
                if (t != null) RebarText.ParseBars(t);
                if (bo != null) RebarText.ParseBars(bo);
                if (s1 != null) RebarText.ParseStirrup(s1);
                if (s2 != null) RebarText.ParseStirrup(s2);
                double wd = Num(r, 2, "Dầm"), ht = Num(r, 3, "Dầm");
                var e = bl[r.Index];
                var old = m.Section(e.Section);
                if (Math.Abs(old.B - wd) > 0.5 || Math.Abs(old.H - ht) > 0.5) e.Section = m.GetOrAddSection("D", wd, ht).Name;
                e.ManualTop = t; e.ManualBottom = bo; e.ManualStirrupSupport = s1; e.ManualStirrupSpan = s2;
            }
            foreach (var r in Rows(cols))
            {
                var t = Opt(Str(r, 5)); var s1 = Opt(Str(r, 6));
                double wd = Num(r, 3, "Cột"), ht = Num(r, 4, "Cột");
                if (t != null) { var g = RebarText.ParseBars(t); RebarText.SplitColumnBars(g.N, wd, ht); }
                if (s1 != null) RebarText.ParseStirrup(s1);
                var e = cl[r.Index];
                var old = m.Section(e.Section);
                if (Math.Abs(old.B - wd) > 0.5 || Math.Abs(old.H - ht) > 0.5) e.Section = m.GetOrAddSection("C", wd, ht).Name;
                e.ManualBars = t; e.ManualStirrup = s1;
            }
            foreach (var r in Rows(slabs))
            {
                var bo = Opt(Str(r, 3)); var t = Opt(Str(r, 4));
                if (bo != null) RebarText.ParseSlab(bo);
                if (t != null) RebarText.ParseSlab(t);
                double th = Num(r, 2, "Sàn");
                var e = sl[r.Index];
                var old = m.SlabSection(e.Section);
                if (old == null || Math.Abs(old.Thickness - th) > 0.5) e.Section = m.GetOrAddSlab(th).Name;
                e.ManualBottom = bo; e.ManualTop = t;
            }
        }
    }
}
