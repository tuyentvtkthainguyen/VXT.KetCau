# encoding: UTF-8
# Việt Const – DỰNG PHƯƠNG ÁN KIẾN TRÚC TRONG SKETCHUP (*.vxtphuongan.json) để render phối cảnh.
# Tường (tách quanh lỗ cửa), kính, cánh cửa, sàn chừa lỗ thang, cầu thang 2 vế dạng khối, nền sân, vật liệu theo mô tả mặt tiền,
# cảnh (scene) phối cảnh mặt tiền / mặt đứng / góc sau, xuất ảnh. Cầu nối agent qua thư mục (mặc định TẮT).
# Tương thích Ruby 2.7 (SketchUp 2021–2023): không dùng cú pháp chỉ có ở Ruby 3.
# Chỉ đọc / ghi file trên máy, không kết nối mạng, không thu thập dữ liệu người dùng.
require 'json'
require 'fileutils'

module VXT
  module VietConst
    module PhuongAn
      M = 1 / 0.0254 # m → inch

      DEFAULT_MATS = {
        'Tường ngoài' => '#EDEAE4', 'Mặt tiền' => '#D9CDBA', 'Tường trong' => '#F4F3EF', 'Kính' => '#6F8FA8',
        'Khung cửa' => '#4E3B2C', 'Cánh cửa' => '#7A5A40', 'Sàn' => '#BDBAB4', 'Mái' => '#8A8F94', 'Lan can' => '#2F2F2F',
        'Cầu thang' => '#CFC8BC', 'Nền' => '#9DAA8A', 'Cửa cuốn' => '#8C9096',
      }.freeze

      class << self
        attr_reader :last_report
      end

      def self.load(path)
        data = JSON.parse(File.read(path, encoding: 'UTF-8'))
        raise ArgumentError, 'Phương án không có cao độ (Levels).' if (data['Levels'] || []).empty?
        data
      end

      def self.pt(x, y, z)
        Geom::Point3d.new(x * M, y * M, z * M)
      end

      def self.color(hex)
        h = hex.to_s.delete('#')
        return nil unless h =~ /\A\h{6}\z/
        Sketchup::Color.new(h[0, 2].to_i(16), h[2, 2].to_i(16), h[4, 2].to_i(16))
      end

      # vật liệu: ưu tiên mô tả mặt tiền (Part chứa từ khoá), sau đó màu mặc định
      def self.material(model, key, facade)
        name = "VXT #{key}"
        mat = model.materials[name]
        return mat if mat
        mat = model.materials.add(name)
        list = facade && facade['Materials'] || []
        fm = list.find { |m| part_matches?(m['Part'].to_s, key) }
        fm ||= list.find { |m| part_matches?(m['Part'].to_s, 'Tường ngoài') } if key == 'Mặt tiền'   # không có điểm nhấn → như tường ngoài
        mat.color = color(fm && fm['Color']) || color(DEFAULT_MATS[key]) || color('#CCCCCC')
        mat.alpha = 0.4 if key == 'Kính'
        mat
      end

      def self.part_matches?(part, key)
        p = part.downcase
        case key
        when 'Mặt tiền' then p.include?('điểm nhấn') || p.include?('mặt tiền')
        when 'Tường ngoài' then p.include?('tường ngoài') || p == 'tường'
        when 'Khung cửa' then p.include?('khung')
        when 'Cánh cửa' then p.include?('cánh') || p.include?('cửa gỗ')
        else p.include?(key.downcase)
        end
      end

      def self.level_z(data, name)
        lv = data['Levels'].find { |l| l['Name'].to_s.casecmp?(name.to_s) }
        lv ? lv['Elevation'].to_f : nil
      end

      def self.next_height(data, name)
        z = level_z(data, name) or return 3.3
        up = data['Levels'].map { |l| l['Elevation'].to_f }.select { |e| e > z + 1e-6 }.min
        up ? up - z : 3.3
      end

      def self.bounds(data)
        xs = []; ys = []
        (data['Walls'] || []).each { |w| xs.push(w['X1'], w['X2']); ys.push(w['Y1'], w['Y2']) }
        (data['Floors'] || []).each { |f| (f['Outline'] || []).each { |p| xs << p[0]; ys << p[1] } }
        return [0.0, 0.0, 10.0, 10.0] if xs.empty?
        [xs.min.to_f, ys.min.to_f, xs.max.to_f, ys.max.to_f]
      end

      def self.top_z(data)
        top = data['Levels'].max_by { |l| l['Elevation'].to_f }
        h = (data['Walls'] || []).select { |w| w['Level'] == top['Name'] }.map { |w| w['Height'].to_f }.max || 1.0
        top['Elevation'].to_f + h
      end

      # hộp định hướng theo trục tường: từ A theo d, đoạn u0…u1, dày t, cao z0…z1 (m)
      def self.wall_box(ents, ax, ay, dx, dy, u0, u1, t, z0, z1)
        return if u1 - u0 < 0.005 || z1 - z0 < 0.005
        nx = -dy * t / 2; ny = dx * t / 2
        q = [[ax + dx * u0 + nx, ay + dy * u0 + ny], [ax + dx * u1 + nx, ay + dy * u1 + ny],
             [ax + dx * u1 - nx, ay + dy * u1 - ny], [ax + dx * u0 - nx, ay + dy * u0 - ny]]
        prism(ents, q, z0, z1)
      end

      # lăng trụ đứng từ đa giác (m) – mặt đáy hướng xuống, đẩy lên
      def self.prism(ents, poly, z0, z1)
        f = ents.add_face(poly.map { |x, y| pt(x, y, z0) })
        return nil unless f
        f.reverse! if f.normal.z > 0
        f.pushpull(-(z1 - z0) * M)
        f
      end

      def self.dist_seg(x, y, w)
        x1, y1, x2, y2 = w['X1'].to_f, w['Y1'].to_f, w['X2'].to_f, w['Y2'].to_f
        dx = x2 - x1; dy = y2 - y1; l2 = dx * dx + dy * dy
        t = l2 < 1e-12 ? 0 : [[((x - x1) * dx + (y - y1) * dy) / l2, 0].max, 1].min
        Math.hypot(x1 + t * dx - x, y1 + t * dy - y)
      end

      def self.thickness(w)
        t = (w['Layers'] || []).sum { |l| l['Thickness'].to_f }
        t > 0.02 ? t : (w['TypeName'].to_s[/\d{2,3}/].to_f / 1000).then { |v| v > 0.05 ? v : 0.11 }
      end

      def self.door?(o)
        !(o['Category'].to_s =~ /win|sổ/i)
      end

      # ------------------------------------------------------------------ dựng
      def self.build(path, model = Sketchup.active_model, replace: true)
        data = load(path)
        facade = data['Facade'] || {}
        report = []
        model.start_operation('Việt Const – dựng phương án', true)
        begin
          ents = model.active_entities
          old = ents.to_a.select { |e| e.respond_to?(:name) && e.name.to_s.start_with?('VXT Phương án') }
          if replace then old.each(&:erase!) elsif !old.empty? then raise 'Đã có khối "VXT Phương án" – chọn dựng lại (thay thế).' end
          root = ents.add_group
          root.name = "VXT Phương án – #{data['ProjectName']}"
          tags = Hash.new { |h, k| h[k] = (model.layers[k] || model.layers.add(k)) }
          x0, y0, x1, y1 = bounds(data)
          front = (facade['FrontDirectionDeg'] || 270).to_f * Math::PI / 180
          fn = [Math.cos(front), Math.sin(front)]
          ops = data['Openings'] || []
          top = data['Levels'].max_by { |l| l['Elevation'].to_f }['Name']
          nwall = 0; nop = 0

          (data['Walls'] || []).each do |w|
            z = level_z(data, w['Level']) or (report << "Tường #{w['Id']}: không có cao độ #{w['Level']}"; next)
            ax, ay, bx, by = w['X1'].to_f, w['Y1'].to_f, w['X2'].to_f, w['Y2'].to_f
            len = Math.hypot(bx - ax, by - ay)
            next if len < 0.05
            dx = (bx - ax) / len; dy = (by - ay) / len
            t = thickness(w)
            h = w['Height'].to_f > 0.05 ? w['Height'].to_f : next_height(data, w['Level'])
            zb = z + w['BaseOffset'].to_f
            # phân loại: tường biên (ngoài) / mặt tiền / lan can mái
            on = ->(v0, v1, ref) { (v0 - ref).abs < 0.3 && (v1 - ref).abs < 0.3 }
            side = if on[ax, bx, x0] then [-1, 0] elsif on[ax, bx, x1] then [1, 0] elsif on[ay, by, y0] then [0, -1] elsif on[ay, by, y1] then [0, 1] end
            key = if side.nil? then 'Tường trong'
                  elsif w['Level'] == top && h <= 1.6 then 'Lan can'
                  elsif side[0] * fn[0] + side[1] * fn[1] > 0.7 then 'Mặt tiền'
                  else 'Tường ngoài' end
            g = root.entities.add_group
            g.name = "Tường #{w['Id']}"
            g.layer = tags['VXT Tường']
            g.material = material(model, key, facade)
            # lỗ cửa trên tường này
            spans = ops.select { |o| o['Level'].to_s.casecmp?(w['Level'].to_s) && dist_seg(o['X'].to_f, o['Y'].to_f, w) <= t / 2 + 0.2 }.map do |o|
              u = (o['X'].to_f - ax) * dx + (o['Y'].to_f - ay) * dy
              ow = o['Width'].to_f
              sill = door?(o) ? o['Sill'].to_f : (o['Sill'].to_f > 0.01 ? o['Sill'].to_f : 0.9)
              { u0: [u - ow / 2, 0].max, u1: [u + ow / 2, len].min, s: sill, top: [sill + o['Height'].to_f, h].min, o: o }
            end.select { |s| s[:u1] - s[:u0] > 0.1 }.sort_by { |s| s[:u0] }
            e = t / 2 # kéo dài hai đầu nửa bề dày → góc kín
            cur = -e
            spans.each do |s|
              wall_box(g.entities, ax, ay, dx, dy, cur, s[:u0], t, zb, zb + h)
              wall_box(g.entities, ax, ay, dx, dy, s[:u0], s[:u1], t, zb, zb + s[:s]) if s[:s] > 0.01
              wall_box(g.entities, ax, ay, dx, dy, s[:u0], s[:u1], t, zb + s[:top], zb + h) if s[:top] < h - 0.01
              cur = [cur, s[:u1]].max
              # kính / cánh cửa
              fr = root.entities.add_group
              fr.name = "Khung #{s[:o]['Id']}"; fr.layer = tags['VXT Cửa']; fr.material = material(model, 'Khung cửa', facade)
              pg = root.entities.add_group
              pg.name = "#{door?(s[:o]) ? 'Cửa đi' : 'Cửa sổ'} #{s[:o]['Id']}"
              pg.layer = tags['VXT Cửa']
              tn = s[:o]['TypeName'].to_s
              fam = s[:o]['Family'].to_s
              kind = if "#{tn} #{fam}" =~ /cuốn|cuon/i then 'Cửa cuốn'
                     elsif !door?(s[:o]) || "#{tn} #{fam}" =~ /kính|kinh|lùa|lua|nhôm|nhom|glass|alu/i || key == 'Mặt tiền' then 'Kính'
                     else 'Cánh cửa' end
              pg.material = material(model, kind, facade)
              wall_box(pg.entities, ax, ay, dx, dy, s[:u0] + 0.03, s[:u1] - 0.03, 0.03, zb + s[:s] + 0.03, zb + s[:top] - 0.03)
              if kind == 'Kính' # cửa nhôm kính: chia cánh + ô fix trên, đố nhôm
                ow = s[:u1] - s[:u0]
                leaves = ow <= 1.0 ? 1 : (ow <= 1.9 ? 2 : 4)
                (1...leaves).each { |k| um = s[:u0] + ow * k / leaves; wall_box(fr.entities, ax, ay, dx, dy, um - 0.025, um + 0.025, 0.06, zb + s[:s], zb + s[:top]) }
                hgt = s[:top] - s[:s]
                if hgt > 2.35 && door?(s[:o]) # ô fix phía trên cửa đi cao
                  zt2 = zb + s[:top] - 0.45
                  wall_box(fr.entities, ax, ay, dx, dy, s[:u0], s[:u1], 0.06, zt2 - 0.025, zt2 + 0.025)
                end
              end
              [[s[:u0], s[:u0] + 0.05], [s[:u1] - 0.05, s[:u1]]].each { |a, b| wall_box(fr.entities, ax, ay, dx, dy, a, b, 0.07, zb + s[:s], zb + s[:top]) }
              wall_box(fr.entities, ax, ay, dx, dy, s[:u0], s[:u1], 0.07, zb + s[:top] - 0.05, zb + s[:top])
              wall_box(fr.entities, ax, ay, dx, dy, s[:u0], s[:u1], 0.07, zb + s[:s], zb + s[:s] + 0.05) if s[:s] > 0.01
              nop += 1
            end
            wall_box(g.entities, ax, ay, dx, dy, cur, len + e, t, zb, zb + h)
            nwall += 1
          end

          stairs = data['Stairs'] || []
          nfl = 0
          (data['Floors'] || []).each do |f|
            z = level_z(data, f['Level']) or next
            poly = (f['Outline'] || []).map { |p| [p[0].to_f, p[1].to_f] }
            next if poly.length < 3
            th = f['Thickness'].to_f > 0 ? f['Thickness'].to_f : 0.1
            g = root.entities.add_group
            g.name = "Sàn #{f['Id']} – #{f['Level']}"
            g.layer = tags['VXT Sàn']
            g.material = material(model, f['Level'] == top ? 'Mái' : 'Sàn', facade)
            face = g.entities.add_face(poly.map { |x, y| pt(x, y, z) })
            next unless face
            face.reverse! if face.normal.z < 0
            stairs.select { |s| s['TopLevel'].to_s.casecmp?(f['Level'].to_s) }.each do |s|
              hole = g.entities.add_face((s['Outline'] || []).map { |p| pt(p[0].to_f, p[1].to_f, z) })
              hole.erase! if hole && hole.valid?
            end
            face.pushpull(-th * M) if face.valid?
            nfl += 1
          end

          stairs.each do |s|
            zb = level_z(data, s['BaseLevel']); zt = level_z(data, s['TopLevel'])
            next unless zb && zt && zt - zb > 1.5 && (s['Outline'] || []).length >= 3
            g = root.entities.add_group
            g.name = "Cầu thang #{s['Id']}"; g.layer = tags['VXT Cầu thang']; g.material = material(model, 'Cầu thang', facade)
            build_stair(g.entities, s, zb, zt)
          end

          # nền sân quanh nhà
          site = data['Site'] || {}
          mx = 6.0
          gg = root.entities.add_group
          gg.name = 'Nền'; gg.layer = tags['VXT Nền']; gg.material = material(model, 'Nền', facade)
          gz = data['Levels'].map { |l| l['Elevation'].to_f }.min
          sw = [site['Width'].to_f, x1 - x0].max; sd = [site['Depth'].to_f, y1 - y0].max
          nf = gg.entities.add_face([pt(x0 - mx, y0 - mx, gz - 0.15), pt(x0 + sw + mx, y0 - mx, gz - 0.15), pt(x0 + sw + mx, y0 + sd + mx, gz - 0.15), pt(x0 - mx, y0 + sd + mx, gz - 0.15)])
          nf.reverse! if nf && nf.normal.z < 0

          make_scenes(model, data)
          model.commit_operation
          @last_report = "Đã dựng #{nwall} tường, #{nop} cửa, #{nfl} sàn, #{stairs.length} cầu thang; 3 cảnh phối cảnh." +
                         (report.empty? ? '' : "\nGhi chú:\n- " + report.first(30).join("\n- "))
          @last_report
        rescue StandardError
          model.abort_operation
          raise
        end
      end

      # cầu thang 2 vế dạng khối bậc (m)
      def self.build_stair(ents, s, zb, zt)
        return build_runs(ents, s, zb, zt) unless (s['Runs'] || []).empty?
        o = s['Outline'].map { |p| [p[0].to_f, p[1].to_f] }
        x0, x1 = o.map(&:first).minmax; y0, y1 = o.map(&:last).minmax
        along_y = (y1 - y0) >= (x1 - x0)
        l = along_y ? y1 - y0 : x1 - x0
        wd = along_y ? x1 - x0 : y1 - y0
        h = zt - zb
        n = [(h / 0.165).round, 6].max; n += 1 if n.odd?
        half = n / 2
        w = [[s['Width'].to_f > 0.6 ? s['Width'].to_f : 0.9, (wd - 0.1) / 2].min, 0.6].max
        land = [w, 0.9].max
        tread = [[(l - land) / half, 0.3].min, 0.2].max
        rise = h / n
        at = ->(u, v) { along_y ? [x0 + v, y0 + u] : [x0 + u, y0 + v] }
        rect = ->(u0, u1, v0, v1) { [at[u0, v0], at[u1, v0], at[u1, v1], at[u0, v1]] }
        if s['Shape'].to_s =~ /thẳng|1 vế/i || wd < 1.6 # 1 vế thẳng hết ô thang
          tr = [l / n, 0.3].min
          n.times { |k| prism(ents, rect[k * tr, (k + 1) * tr, 0, wd], zb + k * rise, zb + (k + 1) * rise) }
          return
        end
        half.times do |k| # vế 1: đi ra xa
          prism(ents, rect[k * tread, (k + 1) * tread, 0, w], zb + k * rise, zb + (k + 1) * rise)
        end
        zl = zb + half * rise
        prism(ents, rect[half * tread, l, 0, wd], zl - 0.12, zl) # chiếu nghỉ
        half.times do |k| # vế 2: quay về
          u1 = half * tread - k * tread
          prism(ents, rect[u1 - tread, u1, wd - w, wd], zl + k * rise, zl + (k + 1) * rise)
        end
      end

      # cầu thang theo các vế ghi sẵn: đường tim vế theo hướng đi lên, Risers bậc đứng; chiếu nghỉ theo Landings
      def self.build_runs(ents, s, zb, zt)
        runs = s['Runs']
        total = runs.sum { |r| r['Risers'].to_i }
        return if total < 2
        rise = (zt - zb) / total
        r0 = runs[0]
        tread = Math.hypot(r0['X2'].to_f - r0['X1'].to_f, r0['Y2'].to_f - r0['Y1'].to_f) / [r0['Risers'].to_i - 1, 1].max
        z = zb
        runs.each_with_index do |r, i|
          x1 = r['X1'].to_f; y1 = r['Y1'].to_f
          len = Math.hypot(r['X2'].to_f - x1, r['Y2'].to_f - y1)
          ux = (r['X2'].to_f - x1) / len; uy = (r['Y2'].to_f - y1) / len
          w = (r['Width'] || 1.0).to_f
          n = r['Risers'].to_i
          # bậc k: mặt bậc từ k·tread tới (k+1)·tread, cao tới z + (k+1)·rise; bậc đứng cuối cùng lên chiếu nghỉ / sàn
          (n - 1).times do |k|
            wall_box(ents, x1, y1, ux, uy, k * tread, (k + 1) * tread, w, z + k * rise, z + (k + 1) * rise)
          end
          z += n * rise
          lp = (s['Landings'] || [])[i]
          if lp && lp.length >= 3 && i < runs.length - 1
            prism(ents, lp.map { |p| [p[0].to_f, p[1].to_f] }, z - 0.12, z)
          end
        end
      end

      # ------------------------------------------------------------------ cảnh + ảnh
      def self.camera_for(data, kind)
        x0, y0, x1, y1 = bounds(data)
        zt = top_z(data); zb = data['Levels'].map { |l| l['Elevation'].to_f }.min
        c = [(x0 + x1) / 2, (y0 + y1) / 2, (zb + zt) / 2]
        size = Math.sqrt((x1 - x0)**2 + (y1 - y0)**2 + (zt - zb)**2)
        deg = ((data['Facade'] || {})['FrontDirectionDeg'] || 270).to_f
        deg += 180 if kind == :back
        a = deg * Math::PI / 180
        n = [Math.cos(a), Math.sin(a)]; sd = [-n[1], n[0]]
        k = kind == :elevation ? 0 : 0.55
        eye = [c[0] + n[0] * size * 1.15 + sd[0] * size * k, c[1] + n[1] * size * 1.15 + sd[1] * size * k,
               kind == :elevation ? c[2] : c[2] + size * 0.25]
        eye = [c[0] + n[0] * size * 1.6, c[1] + n[1] * size * 1.6, c[2]] if kind == :elevation
        cam = Sketchup::Camera.new(pt(*eye), pt(*c), Geom::Vector3d.new(0, 0, 1))
        if kind == :elevation
          cam.perspective = false
          cam.height = (zt - zb + 2) * M
        else
          cam.fov = 45
        end
        cam
      end

      SCENES = [['VXT 1 – Phối cảnh mặt tiền', :front], ['VXT 2 – Mặt đứng chính', :elevation], ['VXT 3 – Phối cảnh góc sau', :back]].freeze

      def self.make_scenes(model, data)
        SCENES.each do |name, kind|
          old = model.pages[name]
          model.pages.erase(old) if old
          model.active_view.camera = camera_for(data, kind)
          model.pages.add(name)
        end
        model.pages.selected_page = model.pages[SCENES[0][0]]
        model.active_view.camera = camera_for(data, :front)
        si = model.shadow_info
        if si
          si['DisplayShadows'] = true
          si['Light'] = 80; si['Dark'] = 45
        end
      end

      # ảnh của 1 cảnh (1…3, hoặc tên cảnh) – trả về đường dẫn
      def self.export_image(path, scene = 1, width = 1920, height = 1080, model = Sketchup.active_model)
        page = scene.is_a?(Integer) ? model.pages[SCENES[[scene, 1].max - 1][0]] : model.pages[scene.to_s]
        model.pages.selected_page = page if page
        model.active_view.camera = page.camera if page && page.respond_to?(:camera) && page.camera
        FileUtils.mkdir_p(File.dirname(File.expand_path(path)))
        ok = model.active_view.write_image(filename: path, width: width, height: height, antialias: true, transparent: false)
        raise "SketchUp không ghi được ảnh #{path}" unless ok
        path
      end

      # ------------------------------------------------------------------ cầu nối agent (thư mục, mặc định tắt)
      COMMANDS = %w[trang-thai dung-phuong-an anh luu].freeze

      def self.agent_root
        base = ENV['LOCALAPPDATA'] || File.join(Dir.home, '.local', 'share')
        File.join(base, 'VietConst', 'Agent', 'sketchup')
      end

      def self.agent_enabled?
        !!@timer
      end

      def self.toggle_agent
        if @timer
          UI.stop_timer(@timer); @timer = nil
        else
          msg = "Cho phép agent (Claude, Việt Const) chạy các lệnh SketchUp: #{COMMANDS.join(', ')}?\n\n" \
                "Lệnh đặt trong thư mục #{agent_root}\\in. Mọi thứ chạy trên máy, không gửi dữ liệu ra ngoài."
          return unless UI.messagebox(msg, MB_YESNO) == IDYES
          FileUtils.mkdir_p(File.join(agent_root, 'in')); FileUtils.mkdir_p(File.join(agent_root, 'out'))
          @timer = UI.start_timer(2, true) { process_queue }
        end
        write_status
      end

      def self.process_queue(model = Sketchup.active_model)
        return if @busy
        @busy = true
        Dir.glob(File.join(agent_root, 'in', '*.json').tr('\\', '/')).sort.first(10).each do |f|
          id = File.basename(f, '.json')
          body = begin
            s = File.read(f, encoding: 'UTF-8'); File.delete(f); s
          rescue StandardError
            next
          end
          run_one(model, id, body)
        end
      ensure
        @busy = false
        write_status
      end

      def self.run_one(model, id, body)
        started = Time.now
        res = { 'id' => id, 'lenh' => '', 'ok' => false, 'bao_cao' => '', 'file' => '' }
        begin
          job = JSON.parse(body)
          lenh = res['lenh'] = job['lenh'].to_s.strip.downcase
          file = job['file'].to_s
          raise "Lệnh \"#{lenh}\" không có trong danh sách cho phép: #{COMMANDS.join(', ')}" unless COMMANDS.include?(lenh)
          case lenh
          when 'trang-thai'
            res['bao_cao'] = "Mô hình: #{model.title}; cảnh: #{model.pages.map(&:name).join(', ')}"
          when 'dung-phuong-an'
            raise 'Thiếu "file" *.json' unless file.downcase.end_with?('.json') && File.exist?(file)
            res['bao_cao'] = build(file, model, replace: job.fetch('thay_the', true))
          when 'anh'
            raise '"file" phải là .png / .jpg' unless file =~ /\.(png|jpe?g)\z/i
            res['file'] = export_image(file, job['canh'] || 1, (job['rong'] || 1920).to_i, (job['cao'] || 1080).to_i, model)
            res['bao_cao'] = "Đã lưu ảnh #{res['file']}"
          when 'luu'
            raise '"file" phải là .skp' unless file.downcase.end_with?('.skp')
            FileUtils.mkdir_p(File.dirname(File.expand_path(file)))
            raise 'SketchUp không lưu được.' unless model.save(file)
            res['file'] = file; res['bao_cao'] = "Đã lưu #{file}"
          end
          res['ok'] = true
        rescue StandardError => ex
          res['bao_cao'] = ex.message
        end
        res['bat_dau'] = started.strftime('%FT%T'); res['xong'] = Time.now.strftime('%FT%T')
        out = File.join(agent_root, 'out')
        FileUtils.mkdir_p(out)
        File.write(File.join(out, "#{id}.tmp"), JSON.pretty_generate(res), encoding: 'UTF-8')
        File.rename(File.join(out, "#{id}.tmp"), File.join(out, "#{id}.ket-qua.json"))
        res
      end

      def self.write_status
        FileUtils.mkdir_p(agent_root)
        File.write(File.join(agent_root, 'trang-thai.json'),
                   JSON.pretty_generate('dang_bat' => agent_enabled?, 'mo_hinh' => (Sketchup.active_model&.title rescue ''),
                                        'luc' => Time.now.strftime('%FT%T'), 'lenh' => COMMANDS), encoding: 'UTF-8')
      rescue StandardError
        nil
      end

      # ------------------------------------------------------------------ menu
      def self.menu_build
        path = UI.openpanel('Chọn phương án Việt Const', '', 'Phương án|*.vxtphuongan.json;*.vxtrevit.json||')
        return unless path
        UI.messagebox(build(path))
      rescue StandardError => ex
        UI.messagebox("Lỗi dựng phương án: #{ex.message}")
      end

      def self.menu_image
        path = UI.savepanel('Lưu ảnh phối cảnh', '', "#{Sketchup.active_model.title}_PhoiCanh.png")
        return unless path
        path += '.png' unless path =~ /\.(png|jpe?g)\z/i
        UI.messagebox("Đã lưu ảnh: #{export_image(path)}")
      rescue StandardError => ex
        UI.messagebox("Lỗi xuất ảnh: #{ex.message}")
      end
    end
  end
end
