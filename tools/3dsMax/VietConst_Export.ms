-- =====================================================================
-- VIỆT CONST – CÔNG CỤ XUẤT TỪ 3DS MAX (MAXScript, 3ds Max 2017 trở lên)
-- Chạy: Scripting → Run Script… → chọn VietConst_Export.ms (hoặc kéo thả file vào khung nhìn).
--   • "Đưa đối tượng chọn vào thư viện": mỗi đối tượng / nhóm → %APPDATA%\VietConst\ThuVien3D\<Nhóm>\<tên>.vxtlib.json
--     (m, Z lên, gốc = tâm đáy hộp bao; nhóm = tên layer hoặc đoán theo tên) – mở lại Thư viện 3D trong Việt Const để dùng.
--   • "Xuất mô hình": cả cảnh hoặc phần chọn → *.vxtmesh.json → Việt Const: BIM → Nhập mô hình SketchUp / Revit….
-- Chỉ ghi file trên máy, không kết nối mạng, không thu thập dữ liệu.
-- =====================================================================
    fn vxtUnitToM =
    (
        local t = units.SystemType
        local f = case t of
        (
            #inches: 0.0254
            #feet: 0.3048
            #miles: 1609.344
            #millimeters: 0.001
            #centimeters: 0.01
            #meters: 1.0
            #kilometers: 1000.0
            default: 0.0254
        )
        f * units.SystemScale
    )

    fn vxtNum v = (formattedPrint v format:".4f")

    fn vxtEsc s =
    (
        local r = substituteString (s as string) "\\" "\\\\"
        substituteString r "\"" "\\\""
    )

    fn vxtSafe s =
    (
        local r = s as string
        for c in #("\\", "/", ":", "*", "?", "\"", "<", ">", "|") do r = substituteString r c "_"
        trimLeft (trimRight r)
    )

    fn vxtGuessCategory n =
    (
        local s = toLower (n as string)
        if (matchPattern s pattern:"*toilet*") or (matchPattern s pattern:"*wc*") or (matchPattern s pattern:"*lavabo*") or (matchPattern s pattern:"*sink*") or (matchPattern s pattern:"*bath*") or (matchPattern s pattern:"*bon cau*") then "Thiết bị vệ sinh"
        else if (matchPattern s pattern:"*lamp*") or (matchPattern s pattern:"*light*") or (matchPattern s pattern:"*den*") then "Đèn"
        else if (matchPattern s pattern:"*door*") or (matchPattern s pattern:"*window*") or (matchPattern s pattern:"*cua*") then "Cửa"
        else "Nội thất"
    )

    -- gom lưới (toạ độ thế giới, m) của các node hình học
    fn vxtCollect nodes verts faces =
    (
        local k = vxtUnitToM()
        for o in nodes where (superClassOf o == GeometryClass) and (not o.isHidden) and (canConvertTo o TriMeshGeometry) do
        (
            local m = snapshotAsMesh o
            local base = verts.count
            for i = 1 to (getNumVerts m) do
            (
                local p = getVert m i
                append verts [p.x * k, p.y * k, p.z * k]
            )
            for i = 1 to (getNumFaces m) do
            (
                local f = getFace m i
                append faces [base + (f.x as integer) - 1, base + (f.y as integer) - 1, base + (f.z as integer) - 1]
            )
            delete m
        )
    )

    fn vxtWriteJson path props verts faces =
    (
        local f = createFile path encoding:#utf8 writeBOM:false
        format "{" to:f
        for p in props do format "\"%\": %, " p[1] p[2] to:f
        format "\"Vertices\": [" to:f
        for i = 1 to verts.count do
        (
            local v = verts[i]
            format "%[%, %, %]" (if i > 1 then "," else "") (vxtNum v.x) (vxtNum v.y) (vxtNum v.z) to:f
        )
        format "], \"Faces\": [" to:f
        for i = 1 to faces.count do
        (
            local t = faces[i]
            format "%[%, %, %]" (if i > 1 then "," else "") (t.x as integer) (t.y as integer) (t.z as integer) to:f
        )
        format "]}" to:f
        close f
    )

    fn vxtExportLibrary =
    (
        local sel = for o in selection where (not isGroupMember o) or (isGroupHead o) collect o
        if sel.count == 0 then ( messageBox "Chọn đối tượng hoặc nhóm cần đưa vào thư viện Việt Const." ; return 0 )
        local root = (systemTools.getEnvVariable "APPDATA") + "\\VietConst\\ThuVien3D"
        makeDir root all:true
        local n = 0
        for o in sel do
        (
            local nodes = if isGroupHead o then (for c in o.children collect c) else #(o)
            local verts = #(); local faces = #()
            vxtCollect nodes verts faces
            if faces.count > 0 then
            (
                local x0 = verts[1].x, x1 = verts[1].x, y0 = verts[1].y, y1 = verts[1].y, z0 = verts[1].z, z1 = verts[1].z
                for v in verts do ( x0 = amin x0 v.x; x1 = amax x1 v.x; y0 = amin y0 v.y; y1 = amax y1 v.y; z0 = amin z0 v.z; z1 = amax z1 v.z )
                local cx = (x0 + x1) / 2.0, cy = (y0 + y1) / 2.0
                for i = 1 to verts.count do verts[i] = [verts[i].x - cx, verts[i].y - cy, verts[i].z - z0]
                local lay = o.layer.name
                local cat = if (lay == "0" or lay == "" or lay == "0 (default)") then vxtGuessCategory o.name else lay
                local disc = if (cat == "Thiết bị vệ sinh" or cat == "Đèn" or cat == "Điện") then "DienNuoc" else "KienTruc"
                local dir = root + "\\" + (vxtSafe cat)
                makeDir dir all:true
                local nm = vxtSafe o.name
                local props = #(#("Id", "\"" + (vxtEsc ((vxtSafe cat) + "/" + nm)) + "\""), #("Name", "\"" + (vxtEsc o.name) + "\""),
                                #("Category", "\"" + (vxtEsc cat) + "\""), #("Source", "\"3ds Max (công cụ Việt Const)\""),
                                #("Width", vxtNum (x1 - x0)), #("Depth", vxtNum (y1 - y0)), #("Height", vxtNum (z1 - z0)),
                                #("Discipline", "\"" + disc + "\""))
                vxtWriteJson (dir + "\\" + nm + ".vxtlib.json") props verts faces
                n += 1
            )
        )
        messageBox ("Đã đưa " + (n as string) + " mục vào thư viện Việt Const:\n" + root + "\n\nTrong Việt Const: BIM → Thư viện 3D…")
        n
    )

    fn vxtExportModel onlySel =
    (
        local nodes = if onlySel then (selection as array) else (objects as array)
        if nodes.count == 0 then ( messageBox "Không có đối tượng." ; return 0 )
        local path = getSaveFileName caption:"Xuất mô hình sang Việt Const" types:"Việt Const (*.vxtmesh.json)|*.vxtmesh.json|"
        if path == undefined then return 0
        local verts = #(); local faces = #()
        vxtCollect nodes verts faces
        local props = #(#("Name", "\"" + (vxtEsc (getFilenameFile maxFileName)) + "\""), #("Category", "\"Mô hình\""), #("Source", "\"3ds Max\""))
        vxtWriteJson path props verts faces
        messageBox ("Đã xuất " + (faces.count as string) + " tam giác:\n" + path + "\n\nTrong Việt Const: BIM → Nhập mô hình SketchUp / Revit…")
        faces.count
    )

    try (destroyDialog vxtVietConstRollout) catch ()
    rollout vxtVietConstRollout "Việt Const" width:260
    (
        label lb1 "Xuất sang Việt Const (m, Z lên, chỉ ghi file trên máy)" align:#left
        button bLib "Đưa đối tượng chọn vào thư viện" width:240
        button bSel "Xuất phần chọn → mô hình (*.vxtmesh.json)" width:240
        button bAll "Xuất cả cảnh → mô hình (*.vxtmesh.json)" width:240
        on bLib pressed do vxtExportLibrary()
        on bSel pressed do vxtExportModel true
        on bAll pressed do vxtExportModel false
    )
    createDialog vxtVietConstRollout
