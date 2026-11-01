namespace KetCauVXT.Model;

public enum SectionShape
{
    /// <summary>Chữ nhật bê tông cốt thép (cột, dầm)</summary>
    RectConcrete,
    /// <summary>Chữ I tổ hợp hàn (có thể thay đổi chiều cao – khung tiền chế)</summary>
    WeldedI,
    /// <summary>Tiết diện khai báo trực tiếp đặc trưng (xà gồ C/Z, thép hộp...)</summary>
    General
}

/// <summary>Đặc trưng hình học tiết diện theo trục địa phương 1-2-3 (giống SAP/ETABS).
/// Trục 2 song song chiều cao h (t3), trục 3 song song bề rộng b (t2).</summary>
public struct SectionProps
{
    public double A, As2, As3, I33, I22, J, W33, W22, Depth, Width;
    /// <summary>Khoảng cách trọng tâm tới mép dưới (theo trục 2)</summary>
    public double Yc;
    public double i33 => Math.Sqrt(I33 / A);
    public double i22 => Math.Sqrt(I22 / A);
}

/// <summary>Tiết diện thanh (frame section). Mọi kích thước đơn vị m.</summary>
public class FrameSection
{
    public string Name { get; set; } = "";
    public SectionShape Shape { get; set; }
    public string Material { get; set; } = "B25";

    // --- Chữ nhật BTCT ---
    /// <summary>Bề rộng b (song song trục 3), m</summary>
    public double B { get; set; }
    /// <summary>Chiều cao h (song song trục 2), m</summary>
    public double H { get; set; }

    // --- Chữ I tổ hợp ---
    /// <summary>Chiều cao tại đầu I (m). Với tiết diện không đổi HEnd = HStart.</summary>
    public double HStart { get; set; }
    public double HEnd { get; set; }
    public double BfTop { get; set; }
    public double TfTop { get; set; }
    public double BfBot { get; set; }
    public double TfBot { get; set; }
    public double Tw { get; set; }

    // --- Tổng quát ---
    public double GA { get; set; }
    public double GI33 { get; set; }
    public double GI22 { get; set; }
    public double GJ { get; set; }
    public double GW33 { get; set; }
    public double GW22 { get; set; }
    public double GDepth { get; set; }
    public double GWidth { get; set; }

    // --- Hệ số điều chỉnh độ cứng (như "Property modifiers" của ETABS) ---
    public double ModI33 { get; set; } = 1.0;
    public double ModI22 { get; set; } = 1.0;
    public double ModJ { get; set; } = 1.0;
    public double ModA { get; set; } = 1.0;

    public bool IsTapered => Shape == SectionShape.WeldedI && Math.Abs(HEnd - HStart) > 1e-6;

    public override string ToString() => Name;

    /// <summary>Đặc trưng tiết diện tại vị trí tương đối s ∈ [0,1] dọc thanh (chỉ khác nhau với tiết diện vát).</summary>
    public SectionProps Props(double s = 0.5)
    {
        switch (Shape)
        {
            case SectionShape.RectConcrete: return RectProps(B, H);
            case SectionShape.WeldedI:
                double h = HStart + (HEnd - HStart) * Math.Clamp(s, 0, 1);
                return IProps(h, BfTop, TfTop, BfBot, TfBot, Tw);
            default:
                return new SectionProps
                {
                    A = GA, I33 = GI33, I22 = GI22, J = GJ, W33 = GW33, W22 = GW22,
                    As2 = GA * 0.5, As3 = GA * 0.5, Depth = GDepth, Width = GWidth, Yc = GDepth / 2
                };
        }
    }

    public static SectionProps RectProps(double b, double h)
    {
        double a = Math.Max(b, h), c = Math.Min(b, h);
        double beta = (1.0 / 3.0) * (1 - 0.63 * c / a + 0.052 * Math.Pow(c / a, 5));
        return new SectionProps
        {
            A = b * h, As2 = 5.0 / 6.0 * b * h, As3 = 5.0 / 6.0 * b * h,
            I33 = b * h * h * h / 12.0, I22 = h * b * b * b / 12.0, J = beta * a * c * c * c,
            W33 = b * h * h / 6.0, W22 = h * b * b / 6.0, Depth = h, Width = b, Yc = h / 2
        };
    }

    public static SectionProps IProps(double h, double bft, double tft, double bfb, double tfb, double tw)
    {
        double hw = h - tft - tfb;
        double a1 = bft * tft, a2 = hw * tw, a3 = bfb * tfb;
        double A = a1 + a2 + a3;
        double y1 = h - tft / 2, y2 = tfb + hw / 2, y3 = tfb / 2;
        double yc = (a1 * y1 + a2 * y2 + a3 * y3) / A;
        double I33 = bft * Math.Pow(tft, 3) / 12 + a1 * Math.Pow(y1 - yc, 2)
                   + tw * Math.Pow(hw, 3) / 12 + a2 * Math.Pow(y2 - yc, 2)
                   + bfb * Math.Pow(tfb, 3) / 12 + a3 * Math.Pow(y3 - yc, 2);
        double I22 = tft * Math.Pow(bft, 3) / 12 + hw * Math.Pow(tw, 3) / 12 + tfb * Math.Pow(bfb, 3) / 12;
        double J = (bft * Math.Pow(tft, 3) + hw * Math.Pow(tw, 3) + bfb * Math.Pow(tfb, 3)) / 3.0;
        double bmax = Math.Max(bft, bfb);
        return new SectionProps
        {
            A = A, As2 = h * tw, As3 = 5.0 / 6.0 * (a1 + a3), I33 = I33, I22 = I22, J = J,
            W33 = I33 / Math.Max(yc, h - yc), W22 = I22 / (bmax / 2), Depth = h, Width = bmax, Yc = yc
        };
    }

    // ------------------------------------------------------------------
    public static FrameSection Rect(string name, double bMm, double hMm, string concrete = "B25") => new()
    {
        Name = name, Shape = SectionShape.RectConcrete, Material = concrete, B = bMm * U.mm, H = hMm * U.mm
    };

    /// <summary>Chữ I tổ hợp (mm): h1 đầu I, h2 đầu J, bf×tf cánh đối xứng, tw bụng.</summary>
    public static FrameSection WeldedISection(string name, double h1, double h2, double bf, double tf, double tw, string steel = "SS400") => new()
    {
        Name = name, Shape = SectionShape.WeldedI, Material = steel,
        HStart = h1 * U.mm, HEnd = h2 * U.mm, BfTop = bf * U.mm, TfTop = tf * U.mm,
        BfBot = bf * U.mm, TfBot = tf * U.mm, Tw = tw * U.mm
    };
}

/// <summary>Tiết diện sàn.</summary>
public class SlabSection
{
    public string Name { get; set; } = "S100";
    public string Material { get; set; } = "B25";
    /// <summary>Chiều dày, m</summary>
    public double Thickness { get; set; } = 0.10;
    public override string ToString() => Name;
}
