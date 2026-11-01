using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using VXT.KetCau.Core.Framing;
using VXT.KetCau.Core.Io;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.Revit
{
    /// <summary>Trạng thái theo từng tài liệu Revit đang mở; tự lưu ra file *.vxtkc.json cạnh file .rvt.</summary>
    public static class Session
    {
        static readonly Dictionary<string, ProjectState> states = new Dictionary<string, ProjectState>();
        static readonly Dictionary<string, WorkflowResult> results = new Dictionary<string, WorkflowResult>();

        static string Key(Document doc) => string.IsNullOrEmpty(doc.PathName) ? "untitled:" + doc.Title : doc.PathName;

        static string FilePath(Document doc)
        {
            if (!string.IsNullOrEmpty(doc.PathName)) return Path.ChangeExtension(doc.PathName, ".vxtkc.json");
            return Path.Combine(Path.GetTempPath(), "VXT_" + doc.Title + ".vxtkc.json");
        }

        public static ProjectState Get(Document doc)
        {
            var k = Key(doc);
            if (states.TryGetValue(k, out var s)) return s;
            var f = FilePath(doc);
            try { s = File.Exists(f) ? ModelIo.Load<ProjectState>(f) : null; } catch { s = null; }
            s = s ?? new ProjectState();
            states[k] = s;
            return s;
        }

        public static void Save(Document doc)
        {
            try { ModelIo.Save(Get(doc), FilePath(doc)); } catch { /* không chặn thao tác nếu không ghi được */ }
        }

        public static WorkflowResult Result(Document doc) => results.TryGetValue(Key(doc), out var r) ? r : null;
        public static void SetResult(Document doc, WorkflowResult r) => results[Key(doc)] = r;

        /// <summary>Cấu tạo lại toàn bộ (thiết kế + bố trí thép + thống kê) từ mô hình hiện có.</summary>
        public static WorkflowResult Detail(Document doc)
        {
            var st = Get(doc);
            if (st.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu. Hãy chạy 'Kiến trúc → Kết cấu' hoặc 'Đọc kết cấu Revit' trước.");
            st.Model.Settings = st.Settings;
            var wf = new StructuralWorkflow(st.Model) { SoilBearing = st.Framing.SoilBearing };
            bool hasForces = st.Model.Beams.Exists(b => b.Forces != null) || st.Model.Columns.Exists(c => c.Forces != null);
            if (!hasForces) wf.PreliminaryForces();
            var r = wf.DetailAll();
            if (!hasForces) r.Warnings.Insert(0, "Đang dùng NỘI LỰC SƠ BỘ (chưa có kết quả ETABS/SAP2000) — chỉ dùng tham khảo");
            SetResult(doc, r);
            Save(doc);
            return r;
        }
    }
}
