using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Dwg;

namespace VXT.KetCau.Revit.Ui
{
    /// <summary>Bảng thông số hiển thị trong PropertyGrid (nhãn tiếng Việt, có mô tả).</summary>
    public class SettingsView
    {
        readonly ProjectState s;
        public SettingsView(ProjectState state) { s = state; }

        const string V = "1. Vật liệu (TCVN 5574:2018)", B = "2. Lớp bảo vệ (mm)", T = "3. Tải trọng", K = "4. Sinh khung từ kiến trúc", P = "5. Phân tích", D = "6. Thể hiện";

        [Category(V), DisplayName("Cấp bê tông"), TypeConverter(typeof(ConcreteList))] public string Concrete { get => s.Settings.Concrete; set => s.Settings.Concrete = value; }
        [Category(V), DisplayName("Thép dọc"), TypeConverter(typeof(SteelList))] public string MainSteel { get => s.Settings.MainSteel; set => s.Settings.MainSteel = value; }
        [Category(V), DisplayName("Thép đai"), TypeConverter(typeof(SteelList))] public string StirrupSteel { get => s.Settings.StirrupSteel; set => s.Settings.StirrupSteel = value; }
        [Category(V), DisplayName("Đường kính thép dầm"), Description("Các Ø được phép dùng, cách nhau dấu phẩy")] public string BeamDias { get => Join(s.Settings.BeamBarDiameters); set => s.Settings.BeamBarDiameters = Split(value); }
        [Category(V), DisplayName("Đường kính thép cột")] public string ColDias { get => Join(s.Settings.ColumnBarDiameters); set => s.Settings.ColumnBarDiameters = Split(value); }
        [Category(V), DisplayName("Đường kính thép sàn")] public string SlabDias { get => Join(s.Settings.SlabBarDiameters); set => s.Settings.SlabBarDiameters = Split(value); }
        [Category(V), DisplayName("Đường kính đai")] public string StirDias { get => Join(s.Settings.StirrupDiameters); set => s.Settings.StirrupDiameters = Split(value); }
        [Category(V), DisplayName("Chiều dài cây thép (mm)"), Description("Dùng bố trí mối nối chồng")] public double Stock { get => s.Settings.StockBarLength; set => s.Settings.StockBarLength = value; }

        [Category(B), DisplayName("Dầm")] public double CoverBeam { get => s.Settings.CoverBeam; set => s.Settings.CoverBeam = value; }
        [Category(B), DisplayName("Cột")] public double CoverCol { get => s.Settings.CoverColumn; set => s.Settings.CoverColumn = value; }
        [Category(B), DisplayName("Sàn")] public double CoverSlab { get => s.Settings.CoverSlab; set => s.Settings.CoverSlab = value; }
        [Category(B), DisplayName("Móng")] public double CoverFoot { get => s.Settings.CoverFooting; set => s.Settings.CoverFooting = value; }

        [Category(T), DisplayName("Áp lực gió W0 (kN/m²)"), Description("Tra theo vùng gió TCVN 2737:2023 cho địa điểm xây dựng. 0 = không tính gió.")] public double W0 { get => s.Settings.WindW0; set => s.Settings.WindW0 = value; }
        [Category(T), DisplayName("Dạng địa hình (A/B/C)")] public string Terrain { get => s.Settings.Terrain; set => s.Settings.Terrain = value; }
        [Category(T), DisplayName("Gia tốc nền agR (g)"), Description("Theo phụ lục TCVN 9386. 0 = không tính động đất.")] public double AgR { get => s.Settings.SeismicAgR; set => s.Settings.SeismicAgR = value; }
        [Category(T), DisplayName("Loại nền đất (A–E)")] public string Ground { get => s.Settings.SeismicGround; set => s.Settings.SeismicGround = value; }
        [Category(T), DisplayName("Hệ số ứng xử q")] public double Q { get => s.Settings.SeismicQ; set => s.Settings.SeismicQ = value; }
        [Category(T), DisplayName("Sức chịu tải nền R (kPa)"), Description("Lấy theo hồ sơ khảo sát địa chất")] public double SoilR { get => s.Framing.SoilBearing; set => s.Framing.SoilBearing = value; }

        [Category(K), DisplayName("Lớp hoàn thiện sàn (mm)"), Description("Cao độ kết cấu = cao độ kiến trúc − lớp hoàn thiện")] public double Finish { get => s.Framing.FinishThickness; set => s.Framing.FinishThickness = value; }
        [Category(K), DisplayName("Cao độ đỉnh móng (mm)"), Description("So với cốt ±0.000 của tầng thấp nhất (âm)")] public double FoundTop { get => s.Framing.FoundationTop; set => s.Framing.FoundationTop = value; }
        [Category(K), DisplayName("Tường dày tối thiểu để đặt cột (mm)")] public double MinWallCol { get => s.Framing.MinWallForColumn; set => s.Framing.MinWallForColumn = value; }
        [Category(K), DisplayName("Tường dày tối thiểu cần dầm đỡ (mm)")] public double MinWallBeam { get => s.Framing.MinWallForSecondaryBeam; set => s.Framing.MinWallForSecondaryBeam = value; }
        [Category(K), DisplayName("Giằng móng tại cốt nền")] public bool Ties { get => s.Framing.TieBeamsAtGround; set => s.Framing.TieBeamsAtGround = value; }
        [Category(K), DisplayName("Bề rộng cột = chiều dày tường")] public bool FitWall { get => s.Framing.ColumnFitsWall; set => s.Framing.ColumnFitsWall = value; }

        [Category(P), DisplayName("Phần mềm phân tích"), TypeConverter(typeof(EngineList))] public string Engine { get => s.Engine; set => s.Engine = value; }
        [Category(P), DisplayName("Gắn vào phiên đang mở"), Description("Đúng: dùng ETABS/SAP2000 đang mở. Sai: tự khởi động.")] public bool Attach { get => s.AttachToRunning; set => s.AttachToRunning = value; }
        [Category(P), DisplayName("Đường dẫn ETABS/SAP2000.exe"), Description("Để trống: dùng bản đăng ký mặc định")] public string Path { get => s.EnginePath; set => s.EnginePath = value; }
        [Category(P), DisplayName("ODA File Converter.exe"), Description("Để chuyển DWG → DXF. Để trống: tự tìm trong Program Files\\ODA")] public string Oda { get => s.OdaConverterPath; set => s.OdaConverterPath = value; }

        [Category(D), DisplayName("Ký hiệu đường kính"), TypeConverter(typeof(SymList))] public string Sym { get => s.Settings.DiaSymbol; set => s.Settings.DiaSymbol = value; }

        static string Join(int[] a) => string.Join(", ", a ?? new int[0]);
        static int[] Split(string v) => (v ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).Distinct().OrderBy(x => x).ToArray();
    }

    public class ListConverter : StringConverter
    {
        protected virtual IEnumerable<string> Items => new string[0];
        public override bool GetStandardValuesSupported(ITypeDescriptorContext c) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext c) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext c) => new StandardValuesCollection(Items.ToArray());
    }
    public class ConcreteList : ListConverter { protected override IEnumerable<string> Items => ConcreteGrade.Names; }
    public class SteelList : ListConverter { protected override IEnumerable<string> Items => SteelGrade.Names; }
    public class EngineList : ListConverter { protected override IEnumerable<string> Items => new[] { "ETABS", "SAP2000", "Sơ bộ" }; }
    public class SymList : ListConverter { protected override IEnumerable<string> Items => new[] { "Ø", "φ" }; }

    /// <summary>Hộp thoại thông số dự án.</summary>
    public class SettingsForm : Form
    {
        public SettingsForm(ProjectState st)
        {
            Text = "VXT Kết cấu — Thông số thiết kế";
            Size = new Size(620, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            var grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = new SettingsView(st), PropertySort = PropertySort.Categorized, ToolbarVisible = false };
            var ok = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 110 };
            var cancel = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 110 };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
            bar.Controls.Add(ok); bar.Controls.Add(cancel);
            Controls.Add(grid); Controls.Add(bar);
            AcceptButton = ok; CancelButton = cancel;
        }
    }

    /// <summary>Chọn các cao độ kiến trúc dùng làm tầng kết cấu.</summary>
    public class LevelPickForm : Form
    {
        readonly CheckedListBox list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        public List<string> Selected => list.CheckedItems.Cast<string>().Select(s => s.Split('|')[0].Trim()).ToList();

        public LevelPickForm(IEnumerable<(string name, double elev)> levels, string sourceTitle)
        {
            Text = "Chọn cao độ làm tầng kết cấu — " + sourceTitle;
            Size = new Size(460, 520); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f);
            foreach (var (n, z) in levels)
            {
                bool likely = !(n.ToLowerInvariant().Contains("sân") || n.ToLowerInvariant().Contains("đỉnh") || n.ToLowerInvariant().Contains("tham chiếu"));
                list.Items.Add($"{n} | {z / 1000:+0.000;-0.000;±0.000}", likely);
            }
            var lbl = new Label { Dock = DockStyle.Top, Height = 44, Text = "Bỏ chọn các cao độ phụ (cốt sân, đỉnh tường chắn mái, tham chiếu…). Cao độ cao nhất được chọn là mái.", Padding = new Padding(6) };
            var ok = new Button { Text = "Tiếp tục", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
            Controls.Add(list); Controls.Add(lbl); Controls.Add(ok); AcceptButton = ok;
        }
    }

    /// <summary>Khai báo các mặt bằng DWG/DXF và cao độ tương ứng.</summary>
    public class DwgPlansForm : Form
    {
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        public List<FloorPlanInput> Plans { get; } = new List<FloorPlanInput>();

        public DwgPlansForm()
        {
            Text = "Nhập hồ sơ kiến trúc DWG/DXF";
            Size = new Size(900, 480); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9.5f);
            grid.Columns.Add("file", "File mặt bằng");
            grid.Columns.Add("level", "Tên tầng");
            grid.Columns.Add("elev", "Cao độ sàn (m)");
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "roof", HeaderText = "Mái" });
            grid.Columns.Add("dx", "Dịch X (mm)");
            grid.Columns.Add("dy", "Dịch Y (mm)");
            grid.Columns[0].FillWeight = 260;
            var add = new Button { Text = "Thêm file…", Width = 120, Dock = DockStyle.Left };
            var del = new Button { Text = "Xoá dòng", Width = 100, Dock = DockStyle.Left };
            var ok = new Button { Text = "Đọc kiến trúc", Width = 140, Dock = DockStyle.Right, DialogResult = DialogResult.OK };
            var hint = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6), Text =
                "Mỗi tầng một file (hoặc một bản sao file đã xoá các tầng khác). Lớp nhận diện: trục (TRUC/AXIS/GRID), tường (TUONG/WALL), cột (COT/COLUMN), cửa (CUA/DOOR), thang (THANG/STAIR), sàn (SAN/SLAB). " +
                "Thêm 1 dòng cho cao độ mái (có thể dùng lại file tầng trên cùng)." };
            add.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Filter = "Bản vẽ (*.dwg;*.dxf)|*.dwg;*.dxf", Multiselect = true })
                    if (dlg.ShowDialog() == DialogResult.OK)
                        foreach (var f in dlg.FileNames)
                        {
                            int i = grid.Rows.Count;
                            grid.Rows.Add(f, i == 0 ? "Tầng 1" : $"Tầng {i + 1}", (i * 3.6).ToString("0.000", CultureInfo.InvariantCulture), false, "0", "0");
                        }
            };
            del.Click += (s, e) => { foreach (DataGridViewRow r in grid.SelectedRows) grid.Rows.Remove(r); };
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
            bar.Controls.Add(ok); bar.Controls.Add(del); bar.Controls.Add(add);
            Controls.Add(grid); Controls.Add(hint); Controls.Add(bar);
            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                Plans.Clear();
                foreach (DataGridViewRow r in grid.Rows)
                {
                    double D(int c) => double.TryParse(Convert.ToString(r.Cells[c].Value).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    Plans.Add(new FloorPlanInput { Path = Convert.ToString(r.Cells[0].Value), LevelName = Convert.ToString(r.Cells[1].Value), Elevation = D(2) * 1000,
                                                   IsRoof = r.Cells[3].Value is bool b && b, OffsetX = D(4), OffsetY = D(5) });
                }
                if (Plans.Count < 2) { MessageBox.Show("Cần ít nhất 2 cao độ (vd tầng 1 và mái).", Text); e.Cancel = true; }
            };
        }
    }

    /// <summary>Cửa sổ báo cáo cuộn được (nhật ký + cảnh báo).</summary>
    public class ReportForm : Form
    {
        public ReportForm(string title, IEnumerable<string> lines)
        {
            Text = title; Size = new Size(820, 560); StartPosition = FormStartPosition.CenterScreen;
            var tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), WordWrap = false,
                                   Text = string.Join(Environment.NewLine, lines) };
            var ok = new Button { Text = "Đóng", Dock = DockStyle.Bottom, Height = 34, DialogResult = DialogResult.OK };
            Controls.Add(tb); Controls.Add(ok); AcceptButton = ok;
        }
        public static void Show(string title, IEnumerable<string> lines) { using (var f = new ReportForm(title, lines)) f.ShowDialog(); }
    }
}
