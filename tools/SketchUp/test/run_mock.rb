# encoding: UTF-8
# Chạy THẬT plugin Việt Const (vxt_vietconst/main.rb) trên API SketchUp giả lập:
#   ruby tools/SketchUp/test/run_mock.rb <thư mục kết quả>
# Dựng nhà mẫu 2 tầng 8 × 6 m (inch, như SketchUp): sàn, tường trong group lồng có dời gốc, cột là component,
# cửa đi / cửa sổ, đối tượng ẩn và tag tắt (không được xuất), bàn ăn component xoay 90° → xuất mô hình + đưa vào thư viện.
require 'json'
require 'fileutils'
$LOAD_PATH.unshift File.join(__dir__, 'stubs')
require_relative 'sketchup_mock'
require_relative '../vxt_vietconst/main'

out = ARGV[0] || File.join(__dir__, 'out')
FileUtils.mkdir_p(out)
IN = 1 / 0.0254   # m → inch
def i(v) = v * IN

fails = []
check = lambda do |name, ok|
  puts "#{ok ? 'ĐẠT' : 'SAI'}  #{name}"
  fails << name unless ok
end

model = Sketchup::Model.new('NhaMau')
Sketchup.active_model = model
ents = model.active_entities
tag_san = Sketchup::Layer.new('San')
tag_tuong = Sketchup::Layer.new('Tuong')
tag_off = Sketchup::Layer.new('Phuong an cu', false)

# sàn (group, tag San)
gs = Sketchup::Group.new(Geom::Transformation.new, tag_san)
gs.entities.add_box(0, 0, i(-0.1), i(8), i(6), 0)
[[0, 0, 6, 6], [7, 0, 8, 6], [6, 0, 7, 1], [6, 3.5, 7, 6], [2, -1.2, 6, 0]].each { |x0, y0, x1, y1| gs.entities.add_box(i(x0), i(y0), i(3.5), i(x1), i(y1), i(3.6)) }
gs.entities.add_box(0, 0, i(6.8), i(8), i(6), i(6.9))
ents << gs

# tường: group gốc dời tới (8, 0) – bên trong toạ độ âm, có group con lồng cho tầng 2
gw = Sketchup::Group.new(Geom::Transformation.translation([i(8), 0, 0]), tag_tuong)
W = ->(e, x0, y0, z0, x1, y1, z1) { e.add_box(i(x0 - 8), i(y0), i(z0), i(x1 - 8), i(y1), i(z1)) }
t = 0.22
W[gw.entities, 0, 6 - t, 0, 8, 6, 3.5]; W[gw.entities, 0, t, 0, t, 6 - t, 3.5]; W[gw.entities, 8 - t, t, 0, 8, 6 - t, 3.5]
W[gw.entities, 0, 0, 0, 3.0, t, 3.5]; W[gw.entities, 4.0, 0, 0, 8, t, 3.5]; W[gw.entities, 3.0, 0, 2.2, 4.0, t, 3.5]   # cửa đi 3…4
g2 = Sketchup::Group.new(Geom::Transformation.translation([0, 0, i(3.6)]))
W[g2.entities, 0, 6 - t, 0, 8, 6, 3.2]; W[g2.entities, 0, t, 0, t, 6 - t, 3.2]; W[g2.entities, 8 - t, t, 0, 8, 6 - t, 3.2]
W[g2.entities, 0, 0, 0, 5.0, t, 3.2]; W[g2.entities, 6.2, 0, 0, 8, t, 3.2]
W[g2.entities, 5.0, 0, 0, 6.2, t, 0.9]; W[g2.entities, 5.0, 0, 2.1, 6.2, t, 3.2]                                       # cửa sổ 5…6,2
gw.entities << g2
ents << gw

# cột 300 × 300: component (định nghĩa gốc ở tâm đáy) đặt tại (4, 3)
dcol = Sketchup::ComponentDefinition.new('Cot 30x30')
dcol.entities.add_box(i(-0.15), i(-0.15), 0, i(0.15), i(0.15), i(3.5))
ents << Sketchup::ComponentInstance.new(dcol, Geom::Transformation.translation([i(4), i(3), 0]))

# cầu thang: 19 mặt bậc mỏng trong ô x 6…7, y 1…3,5
gst = Sketchup::Group.new
(1..19).each { |k| zt = 0.18 * k * 3.6 / 3.42; gst.entities.add_box(i(6), i(1 + (k - 1) * 0.13), i(zt - 0.04), i(7), i(1 + k * 0.13), i(zt)) }
ents << gst

# không được xuất: khối ẩn và khối trên tag đang tắt (to, nằm ngoài nhà)
hid = Sketchup::Group.new; hid.entities.add_box(i(20), i(20), 0, i(30), i(30), i(10)); hid.hidden = true; ents << hid
off = Sketchup::Group.new(Geom::Transformation.new, tag_off); off.entities.add_box(i(-30), i(-30), 0, i(-20), i(-20), i(10)); ents << off

# bàn ăn: component (gốc ở góc), đặt xoay 90° tại (2, 2, 0) – tag "Nội thất"
dban = Sketchup::ComponentDefinition.new('Bàn ăn 6 ghế')
dban.entities.add_box(0, 0, i(0.72), i(1.6), i(0.9), i(0.75))
[[0.05, 0.05], [1.5, 0.05], [1.5, 0.8], [0.05, 0.8]].each { |x, y| dban.entities.add_box(i(x), i(y), 0, i(x + 0.05), i(y + 0.05), i(0.72)) }
ban = Sketchup::ComponentInstance.new(dban, Geom::Transformation.translation([i(2), i(2), 0]) * Geom::Transformation.rotation_z(90), Sketchup::Layer.new('Nội thất'))
ents << ban
# bồn cầu: component không tag → nhóm đoán theo tên
dwc = Sketchup::ComponentDefinition.new('Bon cau 1 khoi')
dwc.entities.add_box(0, 0, 0, i(0.4), i(0.7), i(0.4)); dwc.entities.add_box(0, 0, i(0.4), i(0.4), i(0.18), i(0.75))
wc = Sketchup::ComponentInstance.new(dwc, Geom::Transformation.translation([i(1), i(4), 0]))
ents << wc

# ghế: component co giãn ×2 theo X khi đặt (thư viện giữ tỷ lệ co giãn của instance)
dghe = Sketchup::ComponentDefinition.new('Ghe dau')
dghe.entities.add_box(0, 0, 0, i(0.45), i(0.5), i(0.9))
ghe = Sketchup::ComponentInstance.new(dghe, Geom::Transformation.translation([i(5), i(4), 0]) * Geom::Transformation.scaling(2, 1, 1))
ents << ghe

# ---- 1. xuất cả mô hình ----
$VXT_MOCK_SAVEPATH = File.join(out, 'NhaMau')            # plugin tự thêm đuôi .vxtmesh.json
VXT::VietConst.export(false)
path = File.join(out, 'NhaMau.vxtmesh.json')
check.call('xuất mô hình: có file *.vxtmesh.json', File.exist?(path))
data = JSON.parse(File.read(path))
v = data['Vertices']; f = data['Faces']
xs = v.map { |q| q[0] }; ys = v.map { |q| q[1] }; zs = v.map { |q| q[2] }
check.call('đơn vị m: cao độ thấp nhất −0,1', (zs.min + 0.1).abs < 1e-4)
check.call('đơn vị m: cao độ cao nhất +6,9', (zs.max - 6.9).abs < 1e-4)
check.call('group lồng + dời gốc: tường đúng vị trí x 0…8', xs.min.abs < 1e-4 && (xs.max - 8).abs < 1e-4)
check.call('ban công y −1,2 … sau nhà y 6', (ys.min + 1.2).abs < 1e-4 && (ys.max - 6).abs < 1e-4)
check.call('không xuất khối ẩn / tag tắt', xs.max < 8.01 && xs.min > -0.01 && ys.max < 6.01)
check.call('chỉ số mặt hợp lệ', f.all? { |q| q.length == 3 && q.all? { |k| k >= 0 && k < v.length } && q.uniq.length == 3 })
check.call('mỗi mặt chữ nhật → 2 tam giác', f.length == data['Tags'].length && f.length.even?)
check.call('ghi tag theo đối tượng (Tuong, San)', data['Tags'].include?('Tuong') && data['Tags'].include?('San'))
check.call('đỉnh trùng được gộp', v.length < f.length * 3 / 2)
cx = v.select { |q| (q[2] - 1.0).abs < 1e-3 }.map { |q| q[0] }
check.call('component cột đặt tại (4, 3): mặt x = 3,85 / 4,15', v.any? { |q| (q[0] - 3.85).abs < 1e-4 && (q[1] - 2.85).abs < 1e-4 } && v.any? { |q| (q[0] - 4.15).abs < 1e-4 })
check.call('bàn xoay 90° tại (2, 2): x từ 1,1 tới 2,0', v.any? { |q| (q[0] - 1.1).abs < 1e-4 && (q[2] - 0.75).abs < 1e-4 } && v.any? { |q| (q[0] - 2.0).abs < 1e-4 && (q[2] - 0.75).abs < 1e-4 })

# ---- 2. đưa component vào thư viện ----
appdata = File.join(out, 'AppData')
FileUtils.rm_rf(appdata)
ENV['APPDATA'] = appdata
model.selection.replace([ban, wc, ban, ghe])                  # chọn trùng → chỉ 1 mục
VXT::VietConst.export_library
lib = File.join(appdata, 'VietConst', 'ThuVien3D')
fb = File.join(lib, 'Nội thất', 'Bàn ăn 6 ghế.vxtlib.json')
fw = File.join(lib, 'Thiết bị vệ sinh', 'Bon cau 1 khoi.vxtlib.json')
check.call('thư viện: bàn ăn vào nhóm theo tag "Nội thất"', File.exist?(fb))
check.call('thư viện: bồn cầu tự vào nhóm "Thiết bị vệ sinh"', File.exist?(fw))
check.call('thư viện: 3 mục (chọn trùng không ghi lặp)', Dir.glob(File.join(lib.tr('\\', '/'), '**', '*.vxtlib.json')).length == 3)
g = JSON.parse(File.read(File.join(lib, 'Nội thất', 'Ghe dau.vxtlib.json')))
check.call('ghế co giãn ×2: rộng 0,9 m', (g['Width'] - 0.9).abs < 1e-4 && (g['Depth'] - 0.5).abs < 1e-4)
b = JSON.parse(File.read(fb))
check.call('bàn ăn: kích thước thật 1,6 × 0,9 × 0,75 (không theo góc xoay khi đặt)', (b['Width'] - 1.6).abs < 1e-4 && (b['Depth'] - 0.9).abs < 1e-4 && (b['Height'] - 0.75).abs < 1e-4)
bx = b['Vertices'].map { |q| q[0] }; bz = b['Vertices'].map { |q| q[2] }
check.call('bàn ăn: gốc tâm đáy (x −0,8…0,8, z từ 0)', (bx.min + 0.8).abs < 1e-4 && (bx.max - 0.8).abs < 1e-4 && bz.min.abs < 1e-6)
check.call('bàn ăn: bộ môn kiến trúc, Id theo nhóm', b['Discipline'] == 'KienTruc' && b['Id'] == 'Nội thất/Bàn ăn 6 ghế')
w = JSON.parse(File.read(fw))
check.call('bồn cầu: bộ môn điện nước, cao 0,75', w['Discipline'] == 'DienNuoc' && (w['Height'] - 0.75).abs < 1e-4)
check.call('thông báo cho người dùng', $VXT_MOCK_LOG.any? { |s| s.include?('Đã đưa 3 mục') })

puts
puts fails.empty? ? "PLUGIN SKETCHUP: TẤT CẢ ĐẠT (#{out})" : "PLUGIN SKETCHUP: #{fails.length} SAI"
exit(fails.empty? ? 0 : 1)
