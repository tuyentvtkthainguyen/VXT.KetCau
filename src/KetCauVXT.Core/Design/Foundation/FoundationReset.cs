using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// XOÁ MÓNG ĐÃ THIẾT KẾ ĐỂ THIẾT KẾ LẠI (giao diện: App/MainWindow.FoundationEdit.cs)
//   • Bỏ kết quả móng đơn / đài cọc / móng băng / dầm móng của các chân cột chọn (hoặc tất cả).
//   • Bỏ kích thước móng đã chốt (Footing.L, B, Height = 0 → chương trình tự chọn lại), bố trí cọc người dùng chọn,
//     phương án thép đã chọn của móng. Giữ lại: loại móng riêng từng chân cột (tuỳ chọn), ranh giới đất, lệch tâm, thông số đất nền.
// Sửa trực tiếp trên mặt bằng móng: SetSize chốt L × B × H cho chân cột → thiết kế lại dùng đúng kích thước (FootingDesigner "given").
// =====================================================================
public static class FoundationReset
{
    public sealed record ResetResult(int Footings, int PileCaps, int Strips, int StrapBeams, int Sizes, int PileLayouts, int RebarChoices)
    {
        public int Total => Footings + PileCaps + Strips + StrapBeams;
        public override string ToString() =>
            $"{Footings} móng đơn, {PileCaps} đài cọc, {Strips} móng băng, {StrapBeams} dầm móng; bỏ {Sizes} kích thước đã chốt, {PileLayouts} bố trí cọc, {RebarChoices} lựa chọn thép";
    }

    /// <summary>Xoá móng đã thiết kế của các chân cột nodes (null = mọi chân cột). keepTypes: giữ loại móng riêng đã gán cho chân cột.</summary>
    public static ResetResult Clear(Project p, DesignSummary d, IEnumerable<int> nodes = null, bool keepTypes = true)
    {
        HashSet<int> set = nodes == null ? null : new HashSet<int>(nodes);
        bool In(int n) => set == null || set.Contains(n);
        int nf = 0, nc = 0, ns = 0, nb = 0;
        var keys = new HashSet<string>();
        if (d != null)
        {
            foreach (var f in d.Footings.Where(f => In(f.NodeId))) keys.UnionWith(f.RebarKeys);
            nf = d.Footings.RemoveAll(f => In(f.NodeId));
            if (d.PileCaps != null)
            {
                foreach (var c in d.PileCaps.Where(c => c.NodeIds.Any(In))) keys.UnionWith(c.RebarKeys);
                nc = d.PileCaps.RemoveAll(c => c.NodeIds.Any(In));
            }
            if (d.StripFootings != null)
            {
                foreach (var s in d.StripFootings.Where(s => s.NodeIds.Any(In))) keys.UnionWith(s.RebarKeys);
                ns = d.StripFootings.RemoveAll(s => s.NodeIds.Any(In));
            }
            if (d.StrapBeams != null)
            {
                // dầm móng nối móng đã xoá (hoặc mọi dầm móng khi xoá tất cả)
                var gone = d.StrapBeams.Where(b => set == null || b.Links.Any(l => In(l.NodeA) || In(l.NodeB))).ToList();
                foreach (var b in gone) keys.UnionWith(b.RebarKeys);
                nb = d.StrapBeams.RemoveAll(b => gone.Contains(b));
            }
        }
        foreach (var n in p.Nodes.Where(n => In(n.Id))) { keys.Add("M:" + n.Id); keys.Add("P:" + n.Id); }
        int sizes = 0;
        foreach (var ft in p.Footings.Where(f => In(f.NodeId)).ToList())
        {
            if (ft.L > 0 || ft.B > 0 || ft.Height > 0) sizes++;
            if (keepTypes && ft.Type != null) { ft.L = 0; ft.B = 0; ft.Height = 0; }
            else p.Footings.Remove(ft);
        }
        int lay = p.Foundation.CapOverrides.RemoveAll(o => In(o.NodeId));
        int rc = p.RebarChoices.RemoveAll(r => keys.Contains(r.Member));
        return new ResetResult(nf, nc, ns, nb, sizes, lay, rc);
    }

    /// <summary>Chốt kích thước móng đơn tại chân cột (m); 0 = để chương trình tự chọn cạnh đó. Trả bản ghi Footing.</summary>
    public static Footing SetSize(Project p, int nodeId, double L, double B, double H, FoundationType? type = null)
    {
        var ft = p.Footings.FirstOrDefault(f => f.NodeId == nodeId);
        if (ft == null)
        {
            int k = 1;
            while (p.Footings.Any(f => f.Label == $"M{k}")) k++;
            ft = new Footing { Label = $"M{k}", NodeId = nodeId };
            p.Footings.Add(ft);
        }
        ft.L = Math.Max(0, Math.Round(L, 3)); ft.B = Math.Max(0, Math.Round(B, 3)); ft.Height = Math.Max(0, Math.Round(H, 3));
        if (type != null) ft.Type = type;
        return ft;
    }
}
