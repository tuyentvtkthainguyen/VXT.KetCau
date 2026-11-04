using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using VXT.KetCau.Core.Io;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.AutoCAD
{
    /// <summary>Trạng thái dự án theo từng bản vẽ; lưu ra *.vxtkc.json cạnh file .dwg.</summary>
    public static class AcadSession
    {
        static readonly Dictionary<string, ProjectState> states = new Dictionary<string, ProjectState>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, WorkflowResult> results = new Dictionary<string, WorkflowResult>(StringComparer.OrdinalIgnoreCase);
        public static string LastEnginePath;

        static string Key(Document doc) => doc.Name;

        public static string StateFile(Document doc)
        {
            if (doc.IsNamedDrawing && !string.IsNullOrEmpty(doc.Name)) return Path.ChangeExtension(doc.Name, ".vxtkc.json");
            return Path.Combine(Path.GetTempPath(), "VXT_" + Path.GetFileNameWithoutExtension(doc.Name) + ".vxtkc.json");
        }

        public static string OutputFolder(Document doc)
        {
            string dir = doc.IsNamedDrawing
                ? Path.Combine(Path.GetDirectoryName(doc.Name), Path.GetFileNameWithoutExtension(doc.Name) + "_KetCau")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VXT Ket cau", Path.GetFileNameWithoutExtension(doc.Name));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static ProjectState Get(Document doc)
        {
            if (states.TryGetValue(Key(doc), out var s)) return s;
            var f = StateFile(doc);
            try { s = File.Exists(f) ? ModelIo.Load<ProjectState>(f) : null; } catch { s = null; }
            s = s ?? new ProjectState();
            states[Key(doc)] = s;
            LastEnginePath = s.EnginePath;
            return s;
        }

        public static void Save(Document doc)
        {
            try { ModelIo.Save(Get(doc), StateFile(doc)); } catch { }
        }

        /// <summary>Dữ liệu đầu vào đổi: phải thiết kế lại.</summary>
        public static void ClearResult(Document doc) { results.Remove(Key(doc)); }

        public static WorkflowResult Result(Document doc) => results.TryGetValue(Key(doc), out var r) ? r : null;

        public static WorkflowResult Detail(Document doc)
        {
            var st = Get(doc);
            if (st.Model == null) throw new InvalidOperationException("Chưa có mô hình kết cấu. Hãy chạy bước 1 'Kiến trúc → Kết cấu' (lệnh VXTKC_KIENTRUC) trước.");
            st.Model.Settings = st.Settings;
            var wf = new StructuralWorkflow(st.Model) { SoilBearing = st.Framing.SoilBearing };
            bool hasForces = st.Model.Beams.Exists(b => b.Forces != null) || st.Model.Columns.Exists(c => c.Forces != null);
            if (!hasForces) wf.PreliminaryForces();
            var r = wf.DetailAll();
            if (!hasForces) r.Warnings.Insert(0, "Đang dùng NỘI LỰC SƠ BỘ (chưa có kết quả ETABS/SAP2000) — chỉ dùng tham khảo");
            results[Key(doc)] = r;
            Save(doc);
            return r;
        }
    }
}
