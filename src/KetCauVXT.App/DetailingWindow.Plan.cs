using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.App;

/// <summary>Liên kết hai chiều cây thư mục cấu kiện ↔ không gian vẽ mặt bằng kết cấu; nhận mặt bằng kết cấu từ AutoCAD (lệnh VXT_MB_KC);
/// hình mặt bằng lanh tô – ô văng các tầng trong ô xem trước.</summary>
public sealed partial class DetailingWindow
{
    private PlanLevel _preferLevel;
    private bool _syncPlan;
    private DateTime _lastPlanPick = DateTime.Now;
    private readonly DispatcherTimer _planPickTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private void InitPlanLink()
    {
        _plan.ProjectDir = () => _path != null ? Path.GetDirectoryName(_path) : null;
        _plan.Picked += (m, lv) =>
        {
            if (_syncPlan) return;
            _syncPlan = true;
            try { _preferLevel = lv; RefreshTree(m); }
            finally { _preferLevel = null; _syncPlan = false; }
        };
        // mặt bằng kết cấu từ AutoCAD: lệnh VXT_MB_KC ghi %AppData%\KetCauVXT\AcadPick\plan_*.json
        _planPickTimer.Tick += (s, e) =>
        {
            if (SelfTestOut != null || !IsVisible) return;
            string f = StructuralPlanImport.LatestExchange(_lastPlanPick);
            if (f == null) return;
            try { _lastPlanPick = File.GetLastWriteTime(f); } catch { _lastPlanPick = DateTime.Now; }
            _planPickTimer.Stop();
            try
            {
                var lv = _plan.CurrentLevel;
                if (lv == null) return;
                if (MessageBox.Show(this, "Nhận mặt bằng kết cấu từ AutoCAD (lệnh VXT_MB_KC) vào mặt bằng \"" + lv.Name + "\"?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                var log = new List<string>();
                var ents = StructuralPlanImport.ParseExchange(File.ReadAllText(f), log);
                _tabs.SelectedIndex = 4;
                _plan.ImportEntities(ents, "AutoCAD", log);
            }
            catch (Exception ex) { SetStatus("Không nhận được mặt bằng từ AutoCAD: " + ex.Message); }
            finally { _planPickTimer.Start(); }
        };
        _planPickTimer.Start();
        Closed += (s, e) => _planPickTimer.Stop();
    }

    /// <summary>Lá cây ứng với cấu kiện m nằm dưới nút mặt bằng _preferLevel (khi chọn từ mặt bằng); null nếu không có.</summary>
    private TreeViewItem PreferLeaf(IDetailMember m)
    {
        if (m == null || _preferLevel == null) return null;
        TreeViewItem any = null;
        foreach (var g in _tree.Items.OfType<TreeViewItem>())
        {
            if (g.Tag is not PlanTreeGroup pg) continue;
            foreach (var leaf in g.Items.OfType<TreeViewItem>())
                if (ReferenceEquals(leaf.Tag, m))
                {
                    if (pg.Level == _preferLevel) { g.IsExpanded = true; return leaf; }
                    any ??= leaf;
                }
        }
        return any;
    }

    /// <summary>Chọn cấu kiện trên cây → đánh dấu đúng vị trí trên mặt bằng của nút cha (nếu là nút mặt bằng).</summary>
    private void OnTreeMember(TreeViewItem ti, IDetailMember m)
    {
        if (_syncPlan || _plan == null || m == null) return;
        _syncPlan = true;
        try
        {
            var lv = (ti?.Parent as TreeViewItem)?.Tag is PlanTreeGroup pg ? pg.Level : null;
            _plan.Highlight(m, lv);
        }
        catch { }
        finally { _syncPlan = false; }
    }

    /// <summary>Mặt bằng lanh tô – ô văng của các tầng (xem trước).</summary>
    private List<Figure> LintelPlanFigures()
    {
        try
        {
            string dir = _path != null ? Path.GetDirectoryName(_path) : null;
            return LintelPlanDrawing.Sheets(_prj, Results(), dir).Where(x => x.Plan != null).Select(x => x.Plan).ToList();
        }
        catch { return new List<Figure>(); }
    }
}
