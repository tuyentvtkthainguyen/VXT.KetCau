using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Core.Detailing
{
    /// <summary>
    /// Vai trò thanh thép — thứ tự liệt kê chính là thứ tự đánh số hiệu (kiểu Kết cấu Soft):
    /// thép dọc trước (dưới, trên, mũ gối, tăng cường, giá), sau đó đai, đai C, đai lồng, thép rải.
    /// </summary>
    public enum BarRole
    {
        Bottom = 0,        // thép dọc dưới (chủ)
        Top = 1,           // thép dọc trên (chủ)
        SupportTop = 2,    // mũ gối
        Additional = 3,    // tăng cường (dưới giữa nhịp)
        Side = 4,          // thép giá / cấu tạo mặt bên
        ColumnMain = 5,    // thép dọc cột
        FootingBottom = 6, // thép đáy móng
        Stirrup = 10,      // đai
        CTie = 11,         // đai C
        InnerStirrup = 12, // đai lồng
        Distribution = 13, // thép rải sàn (lớp dưới/trên)
        SlabTop = 14,      // thép mũ sàn
    }

    public static class BarRoleText
    {
        public static string Vn(this BarRole r)
        {
            switch (r)
            {
                case BarRole.Bottom: return "Thép dưới";
                case BarRole.Top: return "Thép trên";
                case BarRole.SupportTop: return "Mũ gối";
                case BarRole.Additional: return "Tăng cường";
                case BarRole.Side: return "Thép giá";
                case BarRole.ColumnMain: return "Thép dọc cột";
                case BarRole.FootingBottom: return "Thép đáy móng";
                case BarRole.Stirrup: return "Đai";
                case BarRole.CTie: return "Đai C";
                case BarRole.InnerStirrup: return "Đai lồng";
                case BarRole.Distribution: return "Thép rải";
                case BarRole.SlabTop: return "Thép mũ sàn";
            }
            return r.ToString();
        }
        public static bool IsTransverse(this BarRole r) => (int)r >= 10;
        /// <summary>Đai, đai C, đai lồng, thép rải (sàn, đáy móng): ghi nhãn dạng Ø{d}a{s}.</summary>
        public static bool TagWithSpacing(this BarRole r) => r == BarRole.Stirrup || r == BarRole.CTie || r == BarRole.InnerStirrup || r == BarRole.Distribution || r == BarRole.SlabTop || r == BarRole.FootingBottom;
    }

    /// <summary>Mã hình dạng thanh (dùng chọn Rebar Shape trong Revit và vẽ phác trong bảng thống kê).</summary>
    public enum ShapeKind
    {
        Straight,      // ———
        LHook,         // |——— (một đầu bẻ 90°)
        UHook,         // |———| (hai đầu bẻ 90°)
        Stirrup,       // khung kín, 2 móc 135°
        CTie,          // đai C, 2 móc 135°
        Crank,         // thép mũ sàn: ⌐———¬
    }

    /// <summary>
    /// Một số hiệu thép trong một cấu kiện (một "loại thanh"), có thể có nhiều vị trí đặt.
    /// Đơn vị mm.
    /// </summary>
    public class DetailBar
    {
        public string Element { get; set; }
        public BarRole Role { get; set; }
        public int D { get; set; }
        public ShapeKind Shape { get; set; }
        /// <summary>Kích thước các đoạn theo hình dạng (mm): Straight [L]; LHook [L, móc]; UHook [móc1, L, móc2];
        /// Stirrup [a, b] (kích thước ngoài-ngoài đai); CTie [a]; Crank [móc1, L, móc2].</summary>
        public List<double> Segments { get; set; } = new List<double>();
        /// <summary>Số thanh trong một cấu kiện.</summary>
        public int Count { get; set; }
        /// <summary>Bước rải (mm) — với đai/thép rải.</summary>
        public double Spacing { get; set; }
        /// <summary>Số hiệu (gán bởi BarMarker).</summary>
        public int Mark { get; set; }
        /// <summary>Vị trí theo trục cấu kiện của lần xuất hiện đầu tiên (mm) — dùng sắp thứ tự đánh số trái→phải.</summary>
        public double OrderX { get; set; }
        /// <summary>Nhịp (0-based) chứa thanh, -1 nếu chạy suốt.</summary>
        public int Span { get; set; } = -1;
        /// <summary>Các vị trí đặt thực tế để dựng mô hình (Revit) và vẽ mặt đứng.</summary>
        public List<BarPlacement> Placements { get; } = new List<BarPlacement>();

        /// <summary>Chiều dài 1 thanh (mm), làm tròn 10 mm. Đai tính cả 2 móc 135°, mỗi móc max(75, 10d).</summary>
        public double Length
        {
            get
            {
                double l;
                switch (Shape)
                {
                    case ShapeKind.Stirrup: l = 2 * (Segments[0] + Segments[1]) + 2 * Tcvn5574.StirrupHook(D); break;
                    case ShapeKind.CTie: l = Segments[0] + 2 * Tcvn5574.StirrupHook(D); break;
                    default: l = Segments.Sum(); break;
                }
                return Math.Ceiling(l / 10 - 1e-9) * 10;
            }
        }

        public double Weight1 => Tcvn5574.UnitWeight(D) * Length / 1000; // kg / thanh

        /// <summary>Phân biệt vị trí cùng vai trò (vd sàn: "L1" lớp dưới phương 1, "L2" lớp dưới phương 2).</summary>
        public string Position { get; set; } = "";

        /// <summary>Khoá phân biệt số hiệu: vai trò/vị trí + Ø + chiều dài + hình dạng (+ kích thước đoạn).</summary>
        public string Key
        {
            get
            {
                var seg = Segments.Select(s => Math.Round(s / 10) * 10).ToList();
                // thanh đối xứng (móc L đầu trái ↔ đầu phải, móc U/mũ sàn đảo chiều) dùng chung số hiệu
                if (Shape == ShapeKind.LHook || Shape == ShapeKind.UHook || Shape == ShapeKind.Crank)
                {
                    var rev = Enumerable.Reverse(seg).ToList();
                    if (string.CompareOrdinal(string.Join(",", rev), string.Join(",", seg)) < 0) seg = rev;
                }
                return $"{(int)Role}{Position}|{D}|{Length:0}|{Shape}|{string.Join(",", seg)}";
            }
        }

        public string ShapeText => Shape == ShapeKind.Stirrup
            ? $"{Segments[0]:0}x{Segments[1]:0}"
            : string.Join("+", Segments.Select(s => s.ToString("0")));

        /// <summary>Nhãn kiểu Kết cấu Soft: dọc "3Ø22", đai/thép rải "Ø6a100".</summary>
        public string TagText(string sym = "Ø", int? countAtLocation = null, double? spacingAtLocation = null)
            => Role.TagWithSpacing()
               ? $"{sym}{D}a{(spacingAtLocation ?? Spacing):0}"
               : $"{countAtLocation ?? Count}{sym}{D}";

        public override string ToString() => $"[{Mark}] {Role.Vn()} {TagText()} L={Length:0}";
    }

    /// <summary>Vị trí đặt một nhóm thanh trong mô hình 3D.</summary>
    public class BarPlacement
    {
        /// <summary>Đường tim thanh (polyline, toạ độ toàn cục mm). Với đai: hình dạng 1 đai đặt tại vị trí đầu tiên.</summary>
        public List<P3> Path { get; set; } = new List<P3>();
        /// <summary>Số thanh của nhóm (rải đều) — 1 nếu là một thanh đơn.</summary>
        public int Count { get; set; } = 1;
        /// <summary>Phương rải (vector đơn vị) và bước rải (mm).</summary>
        public P3 Direction { get; set; }
        public double Spacing { get; set; }
        /// <summary>Pháp tuyến mặt phẳng chứa thanh (dùng cho Rebar.CreateFromCurves).</summary>
        public P3 Normal { get; set; }
        /// <summary>Vị trí theo trục cấu kiện (mm) — vẽ mặt đứng/tag.</summary>
        public double X0 { get; set; }
        public double X1 { get; set; }
        /// <summary>Thuộc nửa trên (true) hay nửa dưới tiết diện — để đặt nhãn trên/dưới dầm.</summary>
        public bool UpperHalf { get; set; }
    }
}
