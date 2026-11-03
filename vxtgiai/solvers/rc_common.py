"""Hàm dùng chung cho tính cốt thép và vẽ thép kiểu Kết cấu Soft."""
from __future__ import annotations
import math
from ..core.drawing import Drawing
from ..core.materials import bar_area
from ..core.report import fmt

BARS = [12, 14, 16, 18, 20, 22, 25, 28, 32]


def choose_bars(As, b, cover=25, ds=8, min_n=2, bars=BARS):
    """Chọn n·Ø đủ As, ưu tiên một lớp và thừa ít. Trả về dict(n,d,As,layers,a)."""
    best = None
    for d in bars:
        s = max(25, d)
        fit = int((b - 2 * cover - 2 * ds + s) // (d + s))
        if fit < 2:
            continue
        n = max(min_n, math.ceil(As / bar_area(d) - 1e-9))
        layers = 1 if n <= fit else 2
        if n > 2 * fit:
            continue
        area = n * bar_area(d)
        # điểm: thừa thép + phạt hai lớp + phạt nhiều thanh + phạt thanh quá nhỏ
        score = area / max(As, 1) + (0.15 if layers == 2 else 0) + 0.012 * n + (0.06 if d < 14 else 0)
        if best is None or score < best[0]:
            if layers == 1:
                a = cover + ds + d / 2
            else:
                n1 = min(n, fit)
                n2 = n - n1
                a = cover + ds + d / 2 + n2 / n * (d + s)
            best = (score, dict(n=n, d=d, As=area, layers=layers, a=a, fit=fit))
    if best is None:
        return None
    return best[1]


def round10(x):
    return int(round(x / 10.0) * 10)


def stirrup_len(b, h, d, cover=25):
    """Chiều dài đai gồm 2 móc 135°, mỗi móc max(75, 10d); làm tròn 10 mm."""
    bc = b - 2 * cover
    hc = h - 2 * cover
    return round10(2 * (bc + hc) + 2 * max(75, 10 * d)), bc, hc


def spacing_for(As, d, smax=200, smin=80):
    """Bước thép sàn (mm) cho 1 m bề rộng, làm tròn xuống bội số 10; trả về (s, As thực)."""
    a = bar_area(d)
    s = 1000 * a / As if As > 0 else smax
    s = min(smax, s)
    s = int(s // 10 * 10)
    if s < smin:
        return None
    return s, 1000 * a / s


def pick_slab_bar(As, ds=(6, 8, 10, 12, 14, 16), smax=200):
    """Chọn Ø và bước sao cho As thực ≥ As. Báo lỗi nếu không bố trí nổi, không chọn thiếu."""
    for d in ds:
        q = spacing_for(As, d, smax)
        if q and q[0] >= 100:
            return d, q[0], q[1]
    d = ds[-1]
    q = spacing_for(As, d, smax, smin=70)
    if q:
        return d, q[0], q[1]
    from .registry import InputError
    raise InputError(f"Cần As = {As:.0f} mm²/m, không bố trí nổi với Ø{d} bước ≥ 70 mm. Hãy tăng chiều dày hoặc cấp bền.")


def draw_rc_section(b, h, bot, top, st, title="1-1", scale="tl: 1/25", ids=(1, 2, 3), cover=25, extra=None):
    """Mặt cắt chữ nhật: bot/top dict(n,d,layers), st dict(d,s,legs). Nhãn kiểu Kết cấu Soft."""
    th = max(62.5, max(b, h) / 14)  # chữ 2,5 mm khi in ở tỷ lệ 1/25
    D = Drawing(th, "mat_cat")
    D.cad_scale = 2.0
    D.rect(0, 0, b, h, "BE_TONG")
    ds = st["d"]
    D.rect(cover, cover, b - 2 * cover, h - 2 * cover, "DAI")
    r_i = cover + ds

    def row(spec, y, up):
        n, d = spec["n"], spec["d"]
        fit = spec.get("fit", n)
        if spec.get("layers", 1) == 2:
            n1 = min(n, fit)
            rows = [(n1, y), (n - n1, y + (1 if not up else -1) * (d + max(25, d)))]
        else:
            rows = [(n, y)]
        firsts = []
        for nn, yy in rows:
            if nn <= 0:
                continue
            x0 = r_i + d / 2
            x1 = b - r_i - d / 2
            xs = [x0 + (x1 - x0) * i / (nn - 1) if nn > 1 else (x0 + x1) / 2 for i in range(nn)]
            for x in xs:
                D.circle(x, yy, d / 2, "THEP", True)
            if nn > 1:
                D.line(xs[0], yy, xs[-1], yy, "KY_HIEU")
            firsts.append((xs[len(xs) // 2], yy))
        return firsts

    yb = r_i + bot["d"] / 2
    yt = h - r_i - top["d"] / 2
    fb = row(bot, yb, False)
    ft = row(top, yt, True)
    # nhãn: trên lên trên, dưới xuống dưới, đai sang phải
    D.tag(ft[0][0], ft[0][1], b * .55, h + th * 1.6, f"{top['n']}Ø{top['d']}", ids[1], side=1)
    D.tag(fb[0][0], fb[0][1], b * .55, -th * 1.6, f"{bot['n']}Ø{bot['d']}", ids[0], side=1)
    D.tag(b - cover, h / 2, b + th * 1.2, h / 2 + th * 1.2, f"Ø{st['d']}a{st['s']}", ids[2], side=1)
    D.dim(0, -th * 3.2, b, -th * 3.2, 0, fmt(b))
    D.dim(0, 0, 0, h, th * 1.4, fmt(h))
    D.title(b / 2, -th * 5.6, title, scale)
    return D


def beam_elevation(L_mm, b, h, sup, bot, top, st_end, st_mid, end_len, ids=(1, 2, 3), name="D1"):
    """Mặt đứng dầm một nhịp: chuỗi vùng đai trên cùng, nhãn thép, vạch cắt, kích thước."""
    th = max(125.0, L_mm / 45)  # chữ 2,5 mm khi in ở tỷ lệ 1/50
    D = Drawing(th, "mat_dung_dam")
    y0 = 0
    # bê tông + cột gối vẽ cụt
    for x in (-sup, L_mm):
        D.rect(x, -th * 2.4, sup, h + th * 4.8, "BE_TONG")
    D.line(0, 0, L_mm, 0, "BE_TONG")
    D.line(0, h, L_mm, h, "BE_TONG")
    c = 25 + st_end["d"]
    D.line(-sup + 25, c + bot["d"] / 2, L_mm + sup - 25, c + bot["d"] / 2, "THEP")
    D.line(-sup + 25, h - c - top["d"] / 2, L_mm + sup - 25, h - c - top["d"] / 2, "THEP")
    # đai
    x = 50.0
    tot_end = end_len
    nE = 0
    while x <= tot_end + 1e-6:
        D.line(x, 25, x, h - 25, "DAI")
        D.line(L_mm - x, 25, L_mm - x, h - 25, "DAI")
        nE += 2
        x += st_end["s"]
    xe = x - st_end["s"]
    x = xe + st_mid["s"]
    nM = 0
    while x < L_mm - xe - st_mid["s"] / 2:
        D.line(x, 25, x, h - 25, "DAI")
        nM += 1
        x += st_mid["s"]
    # chuỗi vùng đai trên cùng
    yz = h + th * 4.2
    zones = [(0, end_len, f"Ø{st_end['d']}a{st_end['s']}"), (end_len, L_mm - end_len, f"Ø{st_mid['d']}a{st_mid['s']}"), (L_mm - end_len, L_mm, f"Ø{st_end['d']}a{st_end['s']}")]
    for a, c_, lb in zones:
        D.dim(a, yz, c_, yz, 0, fmt(c_ - a))
        D.text((a + c_) / 2, yz - th * .9, lb, th * .85, "CHU")
        xn = (a + c_) / 2 + len(lb) * th * .28 + th * .95
        D.circle(xn, yz - th * .9, th * .55, "KY_HIEU")
        D.text(xn, yz - th * .9, str(ids[2]), th * .65, "KY_HIEU")
    # nhãn thép dọc
    D.tag(L_mm * .27, h - c - top["d"] / 2, L_mm * .30, h + th * 1.5, f"{top['n']}Ø{top['d']}", ids[1])
    D.tag(L_mm * .27, c + bot["d"] / 2, L_mm * .30, -th * 1.5, f"{bot['n']}Ø{bot['d']}", ids[0])
    # vạch cắt
    for lb, xc in (("1", end_len * .5), ("2", L_mm * .5)):
        for yy in (-th * .9, h + th * .9):
            D.line(xc, yy - th * .5, xc, yy + th * .5, "CHU")
        D.text(xc - th * .6, h + th * 1.3, lb, th, "CHU")
        D.text(xc - th * .6, -th * 1.3, lb, th, "CHU")
    # kích thước dưới
    D.dim(0, -th * 4.2, L_mm, -th * 4.2, 0, fmt(L_mm))
    D.title(L_mm / 2, -th * 7, f"{name} (SL=1; L={fmt(L_mm + sup)})", "tl: 1/50", h=th * 1.1)
    return D, nE + nM


def rebar_tables(r, name, rows, count=1):
    """rows: list dict(id, shape, d, L(mm), n). Ghi BẢNG THỐNG KÊ + TỔNG HỢP CỐT THÉP."""
    out = []
    tot = {}
    for q in rows:
        all_ = q["n"] * count
        Lm = q["L"] * all_ / 1000
        kg = 0.00617 * q["d"] ** 2 * Lm
        out.append([name, q["id"], q["shape"], q["d"], q["L"], count, q["n"], all_, f"{Lm:.2f}".replace(".", ","), f"{kg:.2f}".replace(".", ",")])
        t = tot.setdefault(q["d"], [0.0, 0.0])
        t[0] += Lm
        t[1] += kg
    r.table(["TÊN CẤU KIỆN", "SỐ HIỆU", "HÌNH DẠNG & KÍCH THƯỚC (mm)", "Ø (mm)", "CHIỀU DÀI 1 THANH (mm)", "SỐ C.KIỆN", "SỐ THANH 1 CK", "SỐ THANH T.BỘ", "TỔNG CHIỀU DÀI (m)", "TỔNG TRỌNG LƯỢNG (kg)"], out, "BẢNG THỐNG KÊ CỐT THÉP", widths=[1.3, 1.1, 3.9, 0.9, 1.5, 1.0, 1.1, 1.1, 1.4, 1.5])
    ds = sorted(tot)
    groups = {"NHÓM Ø≤10": 0.0, "NHÓM 10<Ø≤18": 0.0, "NHÓM Ø>18": 0.0}
    for d in ds:
        k = "NHÓM Ø≤10" if d <= 10 else ("NHÓM 10<Ø≤18" if d <= 18 else "NHÓM Ø>18")
        groups[k] += tot[d][1]
    total = sum(v[1] for v in tot.values())
    r.table(["Đường kính"] + [f"Ø{d}" for d in ds], [["Chiều dài (m)"] + [f"{tot[d][0]:.2f}".replace(".", ",") for d in ds], ["Trọng lượng (kg)"] + [f"{tot[d][1]:.2f}".replace(".", ",") for d in ds]], "TỔNG HỢP CỐT THÉP")
    r.table(["NHÓM Ø≤10 (kg)", "NHÓM 10<Ø≤18 (kg)", "NHÓM Ø>18 (kg)", "TỔNG CỘNG (kg)"], [[f"{groups['NHÓM Ø≤10']:.2f}".replace(".", ","), f"{groups['NHÓM 10<Ø≤18']:.2f}".replace(".", ","), f"{groups['NHÓM Ø>18']:.2f}".replace(".", ","), f"{total:.2f}".replace(".", ",")]])
    return total
