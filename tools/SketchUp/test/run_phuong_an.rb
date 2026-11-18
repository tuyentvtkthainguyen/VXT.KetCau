# encoding: UTF-8
# Chạy THẬT phần dựng phương án (vxt_vietconst/phuong_an.rb) trên API SketchUp giả lập:
#   ruby tools/SketchUp/test/run_phuong_an.rb <thư mục kết quả> [file *.vxtphuongan.json]
# Không đưa file → dùng nhà phố mẫu 5 × 20 m (giống PlanSpec.SampleTownhouse của Việt Const).
# Kiểm: tường tách quanh lỗ cửa, kính / khung, sàn, cầu thang khối bậc, vật liệu theo mặt tiền, 3 cảnh, ảnh, lưu, cầu nối agent.
require 'json'
require 'fileutils'
$LOAD_PATH.unshift File.join(__dir__, 'stubs')
require_relative 'sketchup_mock'
require_relative '../vxt_vietconst/main'

out = File.expand_path(ARGV[0] || File.join(__dir__, 'out_phuong_an'))
FileUtils.mkdir_p(out)
fails = []
check = lambda do |name, ok|
  puts "#{ok ? 'ĐẠT' : 'SAI'}  #{name}"
  fails << name unless ok
end

def sample
  lvn = ['Tầng 1', 'Tầng 2', 'Tầng 3', 'Mái']; el = [0, 3.6, 6.9, 10.2]
  layers = ->(t) { [{ 'Function' => 'Finish1', 'Material' => 'Vữa trát', 'Thickness' => 0.015 }, { 'Function' => 'Structure', 'Material' => 'Gạch', 'Thickness' => t - 0.03 }, { 'Function' => 'Finish2', 'Material' => 'Vữa trát', 'Thickness' => 0.015 }] }
  d = { 'ProjectName' => 'Nhà phố mẫu 5x20', 'Levels' => lvn.each_with_index.map { |n, i| { 'Name' => n, 'Elevation' => el[i] } },
        'Walls' => [], 'Openings' => [], 'Rooms' => [], 'Floors' => [], 'Stairs' => [],
        'Site' => { 'Width' => 5, 'Depth' => 20 } }
  w = 0; o = 0
  wall = ->(lv, x1, y1, x2, y2, t, h) { d['Walls'] << { 'Id' => "W#{w += 1}", 'Level' => lv, 'X1' => x1, 'Y1' => y1, 'X2' => x2, 'Y2' => y2, 'Height' => h, 'TypeName' => "Tường #{(t * 1000).round}", 'Layers' => layers[t] } }
  door = ->(lv, x, y, wd, h = 2.2) { d['Openings'] << { 'Id' => "O#{o += 1}", 'Level' => lv, 'Category' => 'Door', 'X' => x, 'Y' => y, 'Width' => wd, 'Height' => h } }
  win = ->(lv, x, y, wd, h = 1.6, s = 0.8) { d['Openings'] << { 'Id' => "O#{o += 1}", 'Level' => lv, 'Category' => 'Window', 'X' => x, 'Y' => y, 'Width' => wd, 'Height' => h, 'Sill' => s } }
  rect = ->(x0, y0, x1, y1) { [[x0, y0], [x1, y0], [x1, y1], [x0, y1]] }
  3.times do |i|
    lv = lvn[i]; h = el[i + 1] - el[i]
    wall[lv, 0, 0, 0, 20, 0.22, h]; wall[lv, 5, 0, 5, 20, 0.22, h]; wall[lv, 0, 0, 5, 0, 0.11, h]; wall[lv, 0, 20, 5, 20, 0.11, h]
    wall[lv, 2.6, 9, 2.6, 14.5, 0.11, h]; wall[lv, 3, 17, 5, 17, 0.11, h]; wall[lv, 3, 17, 3, 20, 0.11, h]
    door[lv, 4, 17, 0.7]; win[lv, 4, 20, 0.6, 0.6, 1.6]
    if i.zero?
      door[lv, 2.5, 0, 3.2, 2.7]; win[lv, 1.5, 20, 1.2]
    else
      wall[lv, 0, 4.5, 5, 4.5, 0.11, h]; door[lv, 1.5, 4.5, 0.9]; door[lv, 2.5, 0, 2.4, 2.4]; win[lv, 1.5, 20, 1.2]
      d['Floors'] << { 'Id' => "F#{i}", 'Level' => lv, 'Thickness' => 0.1, 'Outline' => rect[0, 0, 5, 20] }
    end
  end
  [[0, 0, 0, 20], [5, 0, 5, 20], [0, 0, 5, 0], [0, 20, 5, 20]].each { |a| wall['Mái', *a, 0.11, 1.1] }
  [[2.6, 9, 2.6, 14.5], [2.6, 9, 5, 9], [2.6, 14.5, 5, 14.5]].each { |a| wall['Mái', *a, 0.11, 2.8] }
  door['Mái', 2.6, 12, 0.8]
  d['Floors'] << { 'Id' => 'F-mai', 'Level' => 'Mái', 'Thickness' => 0.12, 'Outline' => rect[0, 0, 5, 20] }
  [%w[T1 Tầng\ 1 Tầng\ 2], %w[T2 Tầng\ 2 Tầng\ 3], %w[T3 Tầng\ 3 Mái]].each { |id, b, t| d['Stairs'] << { 'Id' => id, 'BaseLevel' => b, 'TopLevel' => t, 'Outline' => rect[2.6, 9, 5, 14.5], 'Width' => 1.1 } }
  d['Facade'] = { 'Style' => 'Hiện đại tối giản', 'Description' => 'Mặt tiền 5 m, cửa kính khung nhôm đen, lam gỗ.', 'FrontDirectionDeg' => 270,
                  'Materials' => [{ 'Part' => 'Tường ngoài', 'Material' => 'Sơn ngoại thất', 'Color' => '#F2F0EB' },
                                  { 'Part' => 'Khung cửa', 'Material' => 'Nhôm sơn tĩnh điện', 'Color' => '#2B2B2B' },
                                  { 'Part' => 'Kính', 'Material' => 'Kính cường lực', 'Color' => '#9FC5D8' },
                                  { 'Part' => 'Khung viền', 'Material' => 'Sơn xám than', 'Color' => '#3F4448' }],
                  'Elements' => [{ 'Id' => 'LC-T2', 'Type' => 'LanCan', 'Style' => 'Kính', 'Level' => 'Tầng 2', 'Points' => [[0.2, -1.2], [4.8, -1.2]], 'Height' => 1.1 },
                                 { 'Id' => 'KV-1', 'Type' => 'Khoi', 'Part' => 'Khung viền', 'Points' => [[-0.02, -1.3], [0.2, 0]], 'Z0' => 3.3, 'Z1' => 10.2 },
                                 { 'Id' => 'LAM-1', 'Type' => 'Lam', 'Part' => 'Lam', 'Points' => [[0.3, -1.3], [2.0, -1.25]], 'Z0' => 6.9, 'Z1' => 10.2, 'Thickness' => 0.05, 'Spacing' => 0.15 },
                                 { 'Id' => 'MAI-1', 'Type' => 'MaiDoc', 'Part' => 'Mái', 'Points' => [[2.5, 8.9], [5.1, 14.6]], 'Z0' => 13.0, 'Z1' => 13.4, 'SlopeAxis' => '+X', 'Thickness' => 0.1 }] }
  d['Beams'] = [{ 'Id' => 'D-W1', 'Level' => 'Tầng 2', 'X1' => 0, 'Y1' => 0, 'X2' => 0, 'Y2' => 20, 'Z1' => 3.6, 'Z2' => 3.6, 'B' => 0.22, 'H' => 0.35 }]
  d
end

plan = ARGV[1] || File.join(out, 'NhaPhoMau.vxtphuongan.json')
File.write(plan, JSON.pretty_generate(sample), encoding: 'UTF-8') unless ARGV[1]
data = JSON.parse(File.read(plan, encoding: 'UTF-8'))

model = Sketchup::Model.new('PhuongAn')
Sketchup.active_model = model
PA = VXT::VietConst::PhuongAn
rep = PA.build(plan, model)
puts rep
root = model.active_entities.to_a.find { |e| e.respond_to?(:name) && e.name.to_s.start_with?('VXT Phương án') }
check.call('có khối gốc "VXT Phương án"', !root.nil?)
groups = root.entities.to_a.select { |e| e.is_a?(Sketchup::Group) }
walls = groups.select { |g| g.name.start_with?('Tường ') }
check.call("dựng đủ #{data['Walls'].length} tường", walls.length == data['Walls'].length)
nop = data['Openings'].length
check.call("đủ #{nop} ô kính / cánh cửa + #{nop} khung", groups.count { |g| g.name =~ /\ACửa (đi|sổ) / } == nop && groups.count { |g| g.name.start_with?('Khung ') } == nop)
check.call('sàn: mỗi sàn 1 khối', groups.count { |g| g.name.start_with?('Sàn ') } == data['Floors'].length)
check.call('cầu thang khối bậc: 3 vế đôi', groups.count { |g| g.name.start_with?('Cầu thang') } == 3)

# toạ độ (m) của mọi đỉnh trong một group
pts = ->(g) { g.entities.to_a.select { |f| f.is_a?(Sketchup::Face) }.flat_map(&:points).map { |p| [p.x * 0.0254, p.y * 0.0254, p.z * 0.0254] } }
# tường mặt tiền tầng 1 (y = 0, cửa chính 3,2 × 2,7 ở x 0,9…4,1): có mảng trên lanh tô từ 2,7 m, không có mảng nào chắn lỗ cửa
wf = walls.find { |g| (p = pts[g]).all? { |q| q[1].abs < 0.1 } && p.map { |q| q[2] }.max < 3.61 }
check.call('tường mặt tiền T1 tìm thấy', !wf.nil?)
if wf
  faces = wf.entities.to_a.select { |f| f.is_a?(Sketchup::Face) }
  blocked = faces.any? do |f|
    xs = f.points.map { |p| p.x * 0.0254 }; zs = f.points.map { |p| p.z * 0.0254 }
    xs.min < 3.9 && xs.max > 1.1 && zs.min < 2.5 && zs.max > 0.2
  end
  check.call('lỗ cửa chính để trống (tường tách quanh lỗ)', !blocked)
  check.call('lanh tô trên cửa chính (z 2,7 → 3,6)', faces.any? { |f| f.points.all? { |p| (p.z * 0.0254 - 2.7).abs < 1e-6 } })
  check.call('tường mặt tiền dùng vật liệu "Mặt tiền"', wf.material&.name == 'VXT Mặt tiền')
end
glass = model.materials['VXT Kính']
check.call('kính: màu theo mô tả mặt tiền #9FC5D8, trong suốt', glass && glass.color.red == 0x9F && glass.alpha < 1)
check.call('tường ngoài: màu #F2F0EB', model.materials['VXT Tường ngoài']&.color&.red == 0xF2)
check.call('tag VXT Tường / Sàn / Cửa', %w[VXT\ Tường VXT\ Sàn VXT\ Cửa].all? { |t| model.layers[t] })
check.call('tường lan can mái dùng vật liệu "Tường ngoài"', walls.select { |g| pts[g].map { |q| q[2] }.min > 10.1 && pts[g].map { |q| q[2] }.max < 11.4 }.all? { |g| g.material&.name == 'VXT Tường ngoài' })
beam = groups.find { |g| g.name == 'Dầm D-W1' }
check.call('dầm: mặt trên = cao độ tầng, cao 0,35 m', beam && (pts[beam].map { |q| q[2] }.max - 3.6).abs < 0.01 && (pts[beam].map { |q| q[2] }.min - 3.25).abs < 1e-6)
fe = ->(id) { groups.find { |g| g.name == "Mặt tiền #{id}" } }
lc = fe['LC-T2']
all_pts = ->(g) { g.entities.to_a.flat_map { |e| e.is_a?(Sketchup::Group) ? pts[e] : (e.is_a?(Sketchup::Face) ? e.points.map { |p| [p.x * 0.0254, p.y * 0.0254, p.z * 0.0254] } : []) } }
check.call('lan can kính T2: cao 1,1 m trên sàn tầng 2, có tấm kính', lc && (all_pts[lc].map { |q| q[2] }.max - 4.7).abs < 1e-6 && lc.entities.to_a.any? { |e| e.is_a?(Sketchup::Group) && e.material&.name.to_s.include?('Kính') })
check.call('khung viền màu theo bảng vật liệu', fe['KV-1']&.material&.color&.red == 0x3F)
check.call('lam: nhiều thanh', fe['LAM-1'] && fe['LAM-1'].entities.to_a.count { |f| f.is_a?(Sketchup::Face) } >= 6 * 11)
mz = fe['MAI-1'] && pts[fe['MAI-1']].map { |q| q[2] }
check.call('mái dốc: thấp 13,0 → cao 13,4 (+ dày)', mz && (mz.min - 13.0).abs < 1e-6 && (mz.max - 13.5).abs < 1e-6)
st = groups.find { |g| g.name == 'Cầu thang T1' }
zs = pts[st].map { |q| q[2] }
check.call('cầu thang T1: từ 0 lên 3,6 m', zs.min.abs < 1e-6 && (zs.max - 3.6).abs < 1e-6)
check.call('cầu thang nằm trong ô thang', pts[st].all? { |q| q[0] > 2.59 && q[0] < 5.01 && q[1] > 8.99 && q[1] < 14.51 })
check.call('3 cảnh phối cảnh', model.pages.length == 3 && model.pages['VXT 2 – Mặt đứng chính'].camera.perspective? == false)
cam = model.pages['VXT 1 – Phối cảnh mặt tiền'].camera
check.call('camera mặt tiền nhìn từ phía đường (y < 0)', cam.eye.y < 0 && cam.target.y > 0)
check.call('thao tác gói trong 1 operation (undo một lần)', model.operations == ['start Việt Const – dựng phương án', 'commit'])

# dựng lại: thay thế, không nhân đôi
PA.build(plan, model)
check.call('dựng lại thay thế khối cũ', model.active_entities.to_a.count { |e| e.respond_to?(:name) && e.name.to_s.start_with?('VXT Phương án') } == 1)

img = PA.export_image(File.join(out, 'PhoiCanh.png'), 1, 1600, 900, model)
check.call('xuất ảnh cảnh 1', File.exist?(img))

# cầu nối agent (thư mục) – mặc định tắt, bật qua menu
ENV['LOCALAPPDATA'] = File.join(out, 'LocalAppData')
FileUtils.rm_rf(ENV['LOCALAPPDATA'])
check.call('agent mặc định tắt', !PA.agent_enabled?)
PA.toggle_agent
check.call('bật agent (người dùng đồng ý)', PA.agent_enabled? && File.exist?(File.join(PA.agent_root, 'trang-thai.json')))
inq = File.join(PA.agent_root, 'in')
File.write(File.join(inq, '01.json'), JSON.generate('lenh' => 'dung-phuong-an', 'file' => plan))
File.write(File.join(inq, '02.json'), JSON.generate('lenh' => 'anh', 'file' => File.join(out, 'Agent_MatDung.png'), 'canh' => 2))
File.write(File.join(inq, '03.json'), JSON.generate('lenh' => 'luu', 'file' => File.join(out, 'PhuongAn.skp')))
File.write(File.join(inq, '04.json'), JSON.generate('lenh' => 'xoa-het', 'file' => 'C:/'))
UI.timers.compact.each(&:call)
res = ->(id) { JSON.parse(File.read(File.join(PA.agent_root, 'out', "#{id}.ket-qua.json"), encoding: 'UTF-8')) }
check.call('agent: dựng phương án', res['01']['ok'] && res['01']['bao_cao'].include?('tường'))
check.call('agent: ảnh mặt đứng', res['02']['ok'] && File.exist?(File.join(out, 'Agent_MatDung.png')))
check.call('agent: lưu .skp', res['03']['ok'] && model.saved_path.end_with?('PhuongAn.skp'))
check.call('agent: từ chối lệnh ngoài danh sách', !res['04']['ok'] && res['04']['bao_cao'].include?('không có trong danh sách'))
check.call('agent: hàng lệnh đã xử lý hết', Dir.children(inq).empty?)
PA.toggle_agent
check.call('tắt agent', !PA.agent_enabled?)

puts
puts fails.empty? ? "DỰNG PHƯƠNG ÁN SKETCHUP: TẤT CẢ ĐẠT (#{out})" : "DỰNG PHƯƠNG ÁN SKETCHUP: #{fails.length} SAI"
exit(fails.empty? ? 0 : 1)
