using System.IO;
using System.Text;
using Newtonsoft.Json;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Io
{
    /// <summary>Lưu/đọc mô hình trung gian (*.vxtkc) dạng JSON — trao đổi giữa Revit, ETABS/SAP và DWG.</summary>
    public static class ModelIo
    {
        static readonly JsonSerializerSettings Opt = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            // Auto: điền vào các List chỉ-đọc có sẵn (Stories, Beams…)
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        public static void Save<T>(T model, string path) => File.WriteAllText(path, JsonConvert.SerializeObject(model, Opt), new UTF8Encoding(false));
        public static T Load<T>(string path) => JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8), Opt);
        public static StructModel LoadStruct(string path) => Load<StructModel>(path);
        public static ArchModel LoadArch(string path) => Load<ArchModel>(path);
    }
}
