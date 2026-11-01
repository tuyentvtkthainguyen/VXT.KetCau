using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Revit.Util
{
    /// <summary>Tiện ích Revit: đổi đơn vị mm ↔ feet, chuyển điểm, tương thích API 2022–2026.</summary>
    public static class Rv
    {
        public const double MmPerFt = 304.8;
        public static double Ft(double mm) => mm / MmPerFt;
        public static double Mm(double ft) => ft * MmPerFt;

        public static XYZ X(P3 p) => new XYZ(Ft(p.X), Ft(p.Y), Ft(p.Z));
        public static XYZ X(P2 p, double zMm) => new XYZ(Ft(p.X), Ft(p.Y), Ft(zMm));
        public static XYZ V(P3 v) => new XYZ(v.X, v.Y, v.Z).Normalize();
        public static P2 P(XYZ p) => new P2(Mm(p.X), Mm(p.Y));
        public static P3 P3(XYZ p) => new P3(Mm(p.X), Mm(p.Y), Mm(p.Z));

        /// <summary>Đường kính danh định thanh thép (mm) — API đổi tên từ 2023.</summary>
        public static double BarDiameterMm(RebarBarType t)
        {
#if REVIT2023_OR_GREATER
            return Mm(t.BarNominalDiameter);
#else
            return Mm(t.BarNominalDiameter);
#endif
        }

        public static void SetBarDiameter(RebarBarType t, double mm)
        {
#if REVIT2023_OR_GREATER
            t.BarNominalDiameter = Ft(mm);
            t.BarModelDiameter = Ft(mm);
#else
            t.BarNominalDiameter = Ft(mm);
#endif
        }

        public static string IdText(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value.ToString();
#else
            return id.IntegerValue.ToString();
#endif
        }

        public static double ParamMm(Element e, params string[] names)
        {
            foreach (var n in names)
            {
                var p = e.LookupParameter(n);
                if (p != null && p.StorageType == StorageType.Double && p.HasValue) return Mm(p.AsDouble());
            }
            return 0;
        }

        public static bool SetParamMm(Element e, double mm, params string[] names)
        {
            foreach (var n in names)
            {
                var p = e.LookupParameter(n);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(Ft(mm)); return true; }
            }
            return false;
        }

        public static void SetComment(Element e, string text)
        {
            var p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p != null && !p.IsReadOnly) p.Set(text);
        }

        public static string GetComment(Element e) => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";

        public static IEnumerable<T> All<T>(Document doc) where T : Element
            => new FilteredElementCollector(doc).OfClass(typeof(T)).Cast<T>();

        public static IEnumerable<FamilyInstance> Instances(Document doc, BuiltInCategory cat)
            => new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().OfType<FamilyInstance>();

        /// <summary>Tên view/sheet duy nhất (Revit không cho trùng).</summary>
        public static string UniqueViewName(Document doc, string name)
        {
            var names = new HashSet<string>(All<View>(doc).Select(v => v.Name));
            string n = name.Replace("{", "(").Replace("}", ")").Replace("[", "(").Replace("]", ")").Replace(":", "-").Replace(";", ",");
            if (!names.Contains(n)) return n;
            for (int i = 2; ; i++) if (!names.Contains($"{n} ({i})")) return $"{n} ({i})";
        }
    }
}
