# encoding: UTF-8
# Giả lập tối thiểu API SketchUp (đơn vị nội bộ inch) để chạy thật plugin Việt Const ngoài SketchUp:
# Geom::Point3d / Vector3d / Transformation (ma trận 4×4), Face#mesh → PolygonMesh (chỉ số từ 1, âm = cạnh ẩn),
# Group / ComponentInstance / ComponentDefinition lồng nhau, Layer (tag) bật / tắt, đối tượng ẩn, UI / Sketchup.
$VXT_MOCK_LOG = []
$VXT_MOCK_SAVEPATH = nil

def file_loaded?(_f) = false
def file_loaded(_f) = nil

module Geom
  class Vector3d
    attr_reader :x, :y, :z
    def initialize(x, y, z) = (@x, @y, @z = x.to_f, y.to_f, z.to_f)
    def length = Math.sqrt(x * x + y * y + z * z)
  end

  class Point3d
    attr_reader :x, :y, :z
    def initialize(x, y, z) = (@x, @y, @z = x.to_f, y.to_f, z.to_f)
  end

  class Transformation
    attr_reader :m   # hàng: [r00 r01 r02 tx; r10 r11 r12 ty; r20 r21 r22 tz]
    def initialize(m = nil)
      @m = m || [[1.0, 0, 0, 0], [0, 1.0, 0, 0], [0, 0, 1.0, 0]]
    end
    def self.translation(v) = new([[1.0, 0, 0, v[0]], [0, 1.0, 0, v[1]], [0, 0, 1.0, v[2]]])
    def self.scaling(sx, sy, sz) = new([[sx.to_f, 0, 0, 0], [0, sy.to_f, 0, 0], [0, 0, sz.to_f, 0]])
    def self.rotation_z(deg)
      a = deg * Math::PI / 180
      new([[Math.cos(a), -Math.sin(a), 0, 0], [Math.sin(a), Math.cos(a), 0, 0], [0, 0, 1.0, 0]])
    end
    def *(o)
      case o
      when Point3d
        Point3d.new(*(0..2).map { |r| m[r][0] * o.x + m[r][1] * o.y + m[r][2] * o.z + m[r][3] })
      when Vector3d
        Vector3d.new(*(0..2).map { |r| m[r][0] * o.x + m[r][1] * o.y + m[r][2] * o.z })
      when Transformation
        rows = (0..2).map do |r|
          (0..3).map do |c|
            v = (0..2).sum { |k| m[r][k] * o.m[k][c] }
            c == 3 ? v + m[r][3] : v
          end
        end
        Transformation.new(rows)
      end
    end
  end
end

module Sketchup
  class Layer
    attr_accessor :name, :visible
    def initialize(name, visible = true) = (@name, @visible = name, visible)
    def visible? = @visible
  end
  LAYER0 = Layer.new('Layer0')

  class PolygonMesh
    def initialize(pts, polys) = (@pts, @polys = pts, polys)
    def polygons = @polys
    def point_at(i) = @pts[i - 1]
  end

  class Entity
    attr_accessor :layer, :hidden
    def hidden? = !!@hidden
  end

  # mặt đa giác phẳng (lồi) – lưới: quạt tam giác, cạnh trong đánh dấu âm như SketchUp
  class Face < Entity
    def initialize(pts, layer = LAYER0) = (@pts, @layer = pts, layer)
    def mesh(_flags = 0)
      polys = (1...(@pts.length - 1)).map { |k| [1, -(k + 1), k + 2] }
      PolygonMesh.new(@pts, polys)
    end
  end

  class Entities
    include Enumerable
    attr_reader :list, :parent
    def initialize(parent = nil) = (@list, @parent = [], parent)
    def each(&b) = @list.each(&b)
    def to_a = @list.dup
    def <<(e) = (@list << e; e)
    # hộp: 6 mặt (inch)
    def add_box(x0, y0, z0, x1, y1, z1, layer = LAYER0)
      p = ->(x, y, z) { Geom::Point3d.new(x, y, z) }
      [[p[x0, y0, z0], p[x0, y1, z0], p[x1, y1, z0], p[x1, y0, z0]], [p[x0, y0, z1], p[x1, y0, z1], p[x1, y1, z1], p[x0, y1, z1]],
       [p[x0, y0, z0], p[x1, y0, z0], p[x1, y0, z1], p[x0, y0, z1]], [p[x1, y0, z0], p[x1, y1, z0], p[x1, y1, z1], p[x1, y0, z1]],
       [p[x1, y1, z0], p[x0, y1, z0], p[x0, y1, z1], p[x1, y1, z1]], [p[x0, y1, z0], p[x0, y0, z0], p[x0, y0, z1], p[x0, y1, z1]]]
        .each { |q| self << Face.new(q, layer) }
    end
  end

  class ComponentDefinition
    attr_accessor :name
    attr_reader :entities
    def initialize(name) = (@name, @entities = name, Entities.new(self))
  end

  class Group < Entity
    attr_accessor :transformation
    attr_reader :entities, :definition
    def initialize(tr = Geom::Transformation.new, layer = LAYER0)
      @definition = ComponentDefinition.new('Group#1')
      @entities = @definition.entities
      @transformation, @layer = tr, layer
    end
  end

  class ComponentInstance < Entity
    attr_accessor :transformation
    attr_reader :definition
    def initialize(defn, tr, layer = LAYER0) = (@definition, @transformation, @layer = defn, tr, layer)
  end

  class Model
    attr_accessor :title
    attr_reader :active_entities, :selection
    def initialize(title) = (@title, @active_entities, @selection = title, Entities.new, [])
  end

  class << self
    attr_accessor :active_model, :status_text
    def register_extension(_e, _l) = true
  end

  class SketchupExtension
    attr_accessor :description, :version, :creator
    def initialize(*_a); end
  end
end
SketchupExtension = Sketchup::SketchupExtension

module UI
  class Menu
    def add_submenu(_n) = Menu.new
    def add_item(_n, &_b) = nil
    def add_separator = nil
  end
  def self.menu(_n) = Menu.new
  def self.messagebox(s) = ($VXT_MOCK_LOG << s; 1)
  def self.savepanel(*_a) = $VXT_MOCK_SAVEPATH
end
