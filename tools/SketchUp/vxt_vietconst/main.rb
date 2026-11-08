# encoding: UTF-8
# Việt Const – xuất lưới tam giác của mô hình (m, trục Z lên) ra *.vxtmesh.json:
#   {"Name": "...", "Category": "Mô hình", "Source": "SketchUp", "Vertices": [[x,y,z],...], "Faces": [[i,j,k],...],
#    "Tags": ["tên tag/layer của từng tam giác", ...]}
# Duyệt nhóm / component lồng nhau (áp dụng phép biến đổi), bỏ đối tượng đang ẩn và tag đang tắt.
# Không kết nối mạng, không thu thập dữ liệu: chỉ ghi file người dùng chọn.
require 'sketchup.rb'
require 'json'
require 'fileutils'

module VXT
  module VietConst
    INCH_TO_M = 0.0254

    class MeshCollector
      attr_reader :verts, :faces, :tags

      def initialize
        @verts = []
        @faces = []
        @tags = []
        @index = {}
      end

      def vid(pt)
        x = (pt.x.to_f * INCH_TO_M).round(4)
        y = (pt.y.to_f * INCH_TO_M).round(4)
        z = (pt.z.to_f * INCH_TO_M).round(4)
        key = [x, y, z]
        i = @index[key]
        return i if i
        i = @verts.length
        @verts << key
        @index[key] = i
        i
      end

      def visible?(e)
        return false if e.respond_to?(:hidden?) && e.hidden?
        if e.respond_to?(:layer) && e.layer
          return false unless e.layer.visible?
        end
        true
      end

      def tag_name(e, parent_tag)
        n = (e.respond_to?(:layer) && e.layer) ? e.layer.name.to_s : ''
        (n.empty? || n == 'Layer0' || n == 'Untagged') ? parent_tag : n
      end

      def collect(entities, tr, parent_tag)
        entities.each do |e|
          next unless visible?(e)
          case e
          when Sketchup::Face
            add_face(e, tr, tag_name(e, parent_tag))
          when Sketchup::Group
            collect(e.entities, tr * e.transformation, tag_name(e, parent_tag))
          when Sketchup::ComponentInstance
            collect(e.definition.entities, tr * e.transformation, tag_name(e, parent_tag))
          end
        end
      end

      def add_face(face, tr, tag)
        mesh = face.mesh(0)
        mesh.polygons.each do |poly|
          idx = poly.map { |k| vid(tr * mesh.point_at(k.abs)) }
          next if idx.length < 3
          (1...(idx.length - 1)).each do |k|
            a, b, c = idx[0], idx[k], idx[k + 1]
            next if a == b || b == c || a == c
            @faces << [a, b, c]
            @tags << tag
          end
        end
      end
    end

    def self.export(selection_only)
      model = Sketchup.active_model
      ents = selection_only ? model.selection.to_a : model.active_entities.to_a
      if ents.empty?
        UI.messagebox(selection_only ? 'Chưa chọn đối tượng nào.' : 'Mô hình trống.')
        return
      end
      title = model.title.to_s.empty? ? 'MoHinh' : model.title.to_s
      path = UI.savepanel('Xuất sang Việt Const', '', "#{title}.vxtmesh.json")
      return unless path
      path += '.vxtmesh.json' unless path.downcase.end_with?('.json')
      col = MeshCollector.new
      Sketchup.status_text = 'Việt Const: đang gom các mặt...'
      col.collect(ents, Geom::Transformation.new, '')
      if col.faces.empty?
        UI.messagebox('Không có mặt (face) nào để xuất.')
        return
      end
      data = { 'Name' => title, 'Category' => 'Mô hình', 'Source' => 'SketchUp',
               'Vertices' => col.verts, 'Faces' => col.faces, 'Tags' => col.tags }
      File.open(path, 'w:UTF-8') { |f| f.write(JSON.generate(data)) }
      Sketchup.status_text = ''
      UI.messagebox("Đã xuất #{col.faces.length} tam giác, #{col.verts.length} đỉnh:\n#{path}\n\n" \
                    "Trong Việt Const: BIM → Nhập mô hình SketchUp / Revit… → chọn file này.")
    rescue StandardError => ex
      UI.messagebox("Lỗi xuất mô hình: #{ex.message}")
    end

    # ---- Xuất component / group đang chọn thành mục thư viện Việt Const (%APPDATA%\VietConst\ThuVien3D\<Nhóm>\<tên>.vxtlib.json)
    #      Toạ độ theo gốc component (điểm chèn), m, Z lên; gốc thư viện = tâm đáy hộp bao; tên = tên định nghĩa; nhóm = tag hoặc đoán theo tên.
    def self.guess_category(name)
      n = name.to_s.downcase
      return 'Thiết bị vệ sinh' if n =~ /(bồn cầu|bon cau|lavabo|chậu|toilet|wc|sen|vòi|bồn tắm|bathtub|sink)/
      return 'Đèn' if n =~ /(đèn|den |lamp|light)/
      return 'Điện' if n =~ /(ổ cắm|công tắc|quạt|điều hoà|điều hòa|socket|switch|fan|air)/
      return 'Cửa' if n =~ /(cửa|door|window)/
      'Nội thất'
    end

    def self.safe(s)
      s.to_s.gsub(/[\\\/:*?"<>|]/, '_').strip
    end

    def self.export_library
      model = Sketchup.active_model
      items = model.selection.to_a.select { |e| e.is_a?(Sketchup::ComponentInstance) || e.is_a?(Sketchup::Group) }
      if items.empty?
        UI.messagebox('Chọn một hoặc nhiều component / group cần đưa vào thư viện Việt Const.')
        return
      end
      appdata = ENV['APPDATA'] || File.join(Dir.home, 'AppData', 'Roaming')
      root = File.join(appdata, 'VietConst', 'ThuVien3D')
      done = {}
      count = 0
      items.each do |e|
        defn = e.respond_to?(:definition) ? e.definition : e.entities.parent
        name = defn.name.to_s.empty? ? 'DoiTuong' : defn.name.to_s
        next if done[name]
        done[name] = true
        col = MeshCollector.new
        # theo hệ toạ độ của component (không áp dụng vị trí đặt) nhưng giữ tỷ lệ co giãn của instance
        tr = e.transformation
        sx = (tr * Geom::Vector3d.new(1, 0, 0)).length.to_f
        sy = (tr * Geom::Vector3d.new(0, 1, 0)).length.to_f
        sz = (tr * Geom::Vector3d.new(0, 0, 1)).length.to_f
        sc = Geom::Transformation.scaling(sx, sy, sz)
        col.collect(e.respond_to?(:definition) ? e.definition.entities : e.entities, sc, '')
        next if col.faces.empty?
        xs = col.verts.map { |v| v[0] }; ys = col.verts.map { |v| v[1] }; zs = col.verts.map { |v| v[2] }
        cx = (xs.min + xs.max) / 2.0; cy = (ys.min + ys.max) / 2.0; z0 = zs.min
        verts = col.verts.map { |v| [(v[0] - cx).round(4), (v[1] - cy).round(4), (v[2] - z0).round(4)] }
        tag = (e.layer && !['Layer0', 'Untagged'].include?(e.layer.name)) ? e.layer.name.to_s : ''
        cat = tag.empty? ? guess_category(name) : tag
        disc = ['Thiết bị vệ sinh', 'Đèn', 'Điện'].include?(cat) ? 'DienNuoc' : 'KienTruc'
        dir = File.join(root, safe(cat))
        FileUtils.mkdir_p(dir)
        data = { 'Id' => "#{safe(cat)}/#{safe(name)}", 'Name' => name, 'Category' => cat, 'Source' => 'SketchUp (plugin)',
                 'Vertices' => verts, 'Faces' => col.faces,
                 'Width' => (xs.max - xs.min).round(4), 'Depth' => (ys.max - ys.min).round(4), 'Height' => (zs.max - zs.min).round(4),
                 'Discipline' => disc }
        File.open(File.join(dir, safe(name) + '.vxtlib.json'), 'w:UTF-8') { |f| f.write(JSON.generate(data)) }
        count += 1
      end
      UI.messagebox("Đã đưa #{count} mục vào thư viện Việt Const:\n#{root}\n\nTrong Việt Const: BIM → Thư viện 3D… (mở lại để thấy mục mới).")
    rescue StandardError => ex
      UI.messagebox("Lỗi xuất thư viện: #{ex.message}")
    end

    unless file_loaded?(__FILE__)
      menu = UI.menu('Extensions').add_submenu('Việt Const')
      menu.add_item('Xuất cả mô hình sang Việt Const…') { export(false) }
      menu.add_item('Xuất phần đang chọn sang Việt Const…') { export(true) }
      menu.add_separator
      menu.add_item('Đưa component đang chọn vào thư viện Việt Const') { export_library }
      file_loaded(__FILE__)
    end
  end
end
