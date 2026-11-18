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
    def %(o) = x * o.x + y * o.y + z * o.z
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

# ---------------------------------------------------------------------------------------------
# Bổ sung cho dựng phương án (phuong_an.rb): add_group / add_face / pushpull, vật liệu, tag, cảnh, camera, ảnh, lưu.
MB_YESNO = 4
IDYES = 6
MF_CHECKED = 8
MF_UNCHECKED = 0

class Numeric
  def m = self / 0.0254
end

module Sketchup
  class Color
    attr_reader :red, :green, :blue
    def initialize(r, g, b) = (@red, @green, @blue = r, g, b)
  end

  class Material
    attr_accessor :name, :color, :alpha
    def initialize(name) = (@name, @alpha = name, 1.0)
  end

  class NamedList
    include Enumerable
    def initialize(klass) = (@klass, @items = klass, [])
    def [](n) = @items.find { |x| x.name == n }
    def add(n) = (self[n] || (@items << @klass.new(n)).last)
    def each(&b) = @items.each(&b)
    def length = @items.length
    def erase(x) = @items.delete(x)
  end

  class Entity
    attr_accessor :parent_entities
    def valid? = !@erased
    def erase! = (@erased = true; parent_entities&.list&.delete(self))
  end

  class Face
    def points = @pts
    def normal
      nx = ny = nz = 0.0
      @pts.each_with_index do |a, i|
        b = @pts[(i + 1) % @pts.length]
        nx += (a.y - b.y) * (a.z + b.z); ny += (a.z - b.z) * (a.x + b.x); nz += (a.x - b.x) * (a.y + b.y)
      end
      l = Math.sqrt(nx * nx + ny * ny + nz * nz)
      Geom::Vector3d.new(nx / l, ny / l, nz / l)
    end
    def reverse! = (@pts = @pts.reverse; self)
    # đẩy theo pháp tuyến d (inch): thêm mặt đối diện + mặt bên
    def pushpull(d)
      n = normal
      top = @pts.map { |p| Geom::Point3d.new(p.x + n.x * d, p.y + n.y * d, p.z + n.z * d) }
      ents = parent_entities
      ents.add_face(top.reverse)
      @pts.each_with_index { |a, i| b = @pts[(i + 1) % @pts.length]; ents.add_face([a, b, top[(i + 1) % top.length], top[i]]) }
      nil
    end
  end

  class Entities
    def add_face(pts)
      return nil if pts.length < 3
      f = Face.new(pts); f.parent_entities = self; self << f; f
    end
    def add_group
      g = Group.new; g.parent_entities = self; self << g; g
    end
  end

  class Group
    attr_accessor :name, :material
  end

  class Camera
    attr_accessor :eye, :target, :up, :perspective, :height, :fov
    def initialize(eye, target, up) = (@eye, @target, @up, @perspective, @fov = eye, target, up, true, 35)
    def perspective? = @perspective
  end

  class Page
    attr_reader :name, :camera
    def initialize(name, camera) = (@name, @camera = name, camera)
  end

  class Pages
    include Enumerable
    attr_accessor :selected_page
    def initialize(model) = (@model, @items = model, [])
    def [](n) = @items.find { |p| p.name == n }
    def add(n) = (@items << Page.new(n, @model.active_view.camera)).last
    def erase(p) = @items.delete(p)
    def each(&b) = @items.each(&b)
    def length = @items.length
  end

  class View
    attr_accessor :camera
    def write_image(opts)
      File.binwrite(opts[:filename], "\x89PNG\r\n\x1a\n".b + [opts[:width], opts[:height]].pack('N2'))
      $VXT_MOCK_LOG << "write_image #{opts[:filename]}"
      true
    end
  end

  class Model
    attr_reader :saved_path, :operations
    def materials = (@materials ||= NamedList.new(Material))
    def layers = (@layers ||= NamedList.new(Layer))
    def pages = (@pages ||= Pages.new(self))
    def active_view = (@view ||= View.new)
    def shadow_info = (@shadow ||= {})
    def start_operation(n, *_a) = ((@operations ||= []) << "start #{n}")
    def commit_operation = (@operations << 'commit')
    def abort_operation = (@operations << 'abort')
    def save(path) = (@saved_path = path; File.write(path, 'skp'); true)
  end
end

module UI
  @timers = []
  def self.messagebox(s, _type = nil) = ($VXT_MOCK_LOG << s; IDYES)
  def self.start_timer(_s, _r, &b) = (@timers << b; @timers.length)
  def self.stop_timer(id) = (@timers[id - 1] = nil)
  def self.timers = @timers
  def self.openpanel(*_a) = $VXT_MOCK_OPENPATH
  class Menu
    def set_validation_proc(_i, &_b) = nil
  end
end
