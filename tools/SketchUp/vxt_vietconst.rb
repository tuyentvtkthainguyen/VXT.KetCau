# encoding: UTF-8
# Việt Const – xuất mô hình SketchUp sang Việt Const (*.vxtmesh.json)
# Cài: Window → Extension Manager → Install Extension → chọn VietConst_SketchUp.rbz
# Chỉ ghi file trên máy, không kết nối mạng, không thu thập dữ liệu người dùng.
require 'sketchup.rb'
require 'extensions.rb'

module VXT
  module VietConst
    unless file_loaded?(__FILE__)
      ext = SketchupExtension.new('Việt Const – Xuất mô hình', 'vxt_vietconst/main')
      ext.description = 'Xuất mô hình (sàn, tường, cột, mái, cầu thang) sang Việt Const để nhận mặt bằng các tầng và triển khai kết cấu. Chỉ ghi file trên máy.'
      ext.version = '1.0.0'
      ext.creator = 'Việt Const'
      Sketchup.register_extension(ext, true)
      file_loaded(__FILE__)
    end
  end
end
