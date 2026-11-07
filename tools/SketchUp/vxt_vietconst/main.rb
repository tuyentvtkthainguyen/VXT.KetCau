# encoding: UTF-8
# Việt Const – xuất lưới tam giác của mô hình (m, trục Z lên) ra *.vxtmesh.json:
#   {"Name": "...", "Category": "Mô hình", "Source": "SketchUp", "Vertices": [[x,y,z],...], "Faces": [[i,j,k],...],
#    "Tags": ["tên tag/layer của từng tam giác", ...]}
# Duyệt nhóm / component lồng nhau (áp dụng phép biến đổi), bỏ đối tượng đang ẩn và tag đang tắt.
# Không kết nối mạng, không thu thập dữ liệu: chỉ ghi file người dùng chọn.
require 'sketchup.rb'
require 'json'

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

    unless file_loaded?(__FILE__)
      menu = UI.menu('Extensions').add_submenu('Việt Const')
      menu.add_item('Xuất cả mô hình sang Việt Const…') { export(false) }
      menu.add_item('Xuất phần đang chọn sang Việt Const…') { export(true) }
      file_loaded(__FILE__)
    end
  end
end
