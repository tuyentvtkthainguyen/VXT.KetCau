using System;
using System.Drawing;
using System.Windows.Forms;
using VXT.KetCau.Core.Workflow;

namespace VXT.KetCau.AutoCAD
{
    /// <summary>Bảng điều khiển: các bước theo thứ tự, kèm trạng thái dự án.</summary>
    public class PanelForm : Form
    {
        public string Action { get; private set; }

        public PanelForm(ProjectState st, bool hasResult, string drawing)
        {
            Text = "VXT Kết cấu — " + System.IO.Path.GetFileName(drawing);
            Size = new Size(520, 600); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14), WrapContents = false, AutoScroll = true };
            bool forces = st.Model != null && (st.Model.Beams.Exists(b => b.Forces != null) || st.Model.Columns.Exists(c => c.Forces != null));
            string status = st.Model == null ? "Chưa có mô hình kết cấu."
                : $"Mô hình: {st.Model.Stories.Count} tầng, {st.Model.Columns.Count} cột, {st.Model.Beams.Count} dầm, {st.Model.Slabs.Count} ô sàn.\n" +
                  (forces ? "Đã có nội lực ETABS/SAP2000." : "Chưa có nội lực (sẽ dùng nội lực sơ bộ).") + (hasResult ? "\nĐã thiết kế & cấu tạo." : "");
            flow.Controls.Add(new Label { Text = status, AutoSize = false, Width = 470, Height = 64, ForeColor = Color.DimGray });

            void Btn(string text, string act, string hint)
            {
                var b = new Button { Text = text, Width = 470, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
                b.Click += (s, e) => { Action = act; DialogResult = DialogResult.OK; Close(); };
                new ToolTip().SetToolTip(b, hint);
                flow.Controls.Add(b);
            }
            Btn("0.  Thông số thiết kế", "THONGSO", "Vật liệu, lớp bảo vệ, gió, động đất, nền, ETABS/SAP2000");
            Btn("1.  Kiến trúc (DWG) → Kết cấu", "KIENTRUC", "Đọc mặt bằng từng tầng, tự sinh cột, dầm, sàn, móng và tải");
            Btn("2.  ETABS / SAP2000 – tạo mô hình & chạy", "PHANTICH", "Tạo mô hình, tải, gió, động đất, tổ hợp; chạy và đọc nội lực");
            Btn("2'. Đọc lại nội lực ETABS/SAP2000 đang mở", "DOCNOILUC", "Sau khi bạn chỉnh tay mô hình và chạy lại");
            Btn("3.  Thiết kế & cấu tạo thép (TCVN 5574:2018)", "THIETKE", "Thiết kế, bố trí, đánh số thép; xuất thuyết minh + thống kê");
            Btn("4.  Vẽ shop drawing vào bản vẽ này", "SHOP", "Mặt đứng, mặt cắt dầm, cột, bảng thống kê — dim và ghi chú tự động");
            Btn("5.  Xuất shop drawing ra file DXF riêng", "DXF", "");
            var close = new Button { Text = "Đóng", Width = 470, Height = 34, DialogResult = DialogResult.Cancel };
            flow.Controls.Add(close);
            Controls.Add(flow);
            CancelButton = close;
        }
    }
}
