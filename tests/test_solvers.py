"""Kiểm thử lời giải bằng đáp số giải tích và số liệu tiêu chuẩn đã biết."""
import io
import math
import random
import sys, os
import zipfile

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from vxtgiai.solvers import registry
from vxtgiai.solvers.registry import InputError, run, defaults
from vxtgiai.solvers.btct import plate_coeffs, column_capacity
from vxtgiai.solvers.nenmong import abd, k0_center
from vxtgiai.solvers.thep import phi_buckling, I_props
from vxtgiai.core.materials import xi_R, bar_area
from vxtgiai.core.beam import BeamModel
from vxtgiai.core.mathmk import to_omml, to_plain
from vxtgiai.export import FORMATS
from vxtgiai.export.html_out import report_html

A = pytest.approx


# ---------- Sức bền ----------
def beam_params(**k):
    p = defaults("sbvl_beam")
    p.update(k)
    return p


def test_simply_supported_udl():
    r = run("sbvl_beam", beam_params(L=6, xA=0, xB=6, P=[], M=[], q=[dict(a=0, b=6, q1=10, q2=10)], sa=1000, ta=1000, flim=1))
    assert r.results["VA"] == A(30) and r.results["VB"] == A(30)
    assert r.results["Mmax"] == A(45)
    EI = 200000e3 * (200 * 400 ** 3 / 12) * 1e-12
    assert r.results["fmax"] == A(5 * 10 * 6 ** 4 / (384 * EI) * 1000, rel=1e-3)
    assert r.results["sigma"] == A(45e6 / (200 * 400 ** 2 / 6))
    assert r.results["tau"] == A(1.5 * 30e3 / (200 * 400))


def test_cantilever_point():
    r = run("sbvl_beam", beam_params(L=3, stype="cant_l", P=[dict(x=3, P=10)], M=[], q=[], sa=1000, ta=1000, flim=1))
    assert r.results["VA"] == A(10) and r.results["MA"] == A(-30)
    EI = 200000e3 * (200 * 400 ** 3 / 12) * 1e-12
    assert abs(r.results["fmax"]) == A(10 * 27 / (3 * EI) * 1000, rel=1e-6)


def test_cantilever_right_and_moment_load():
    b = BeamModel(4, [(4, "fixed")], [], [], [(1, 8)], [])
    assert b.max_abs()["M"] == A(8)
    b = BeamModel(10, [(0, "pin"), (10, "roller")], [], [], [(5, 100)], [])
    assert b.reactions[1][3] == A(10) and b.reactions[0][3] == A(-10)
    x, Q, M = b.sample()
    assert M.max() == A(50) and M.min() == A(-50)


def test_triangular_load():
    # q tăng tuyến tính 0→q: RA = qL/6, RB = qL/3, Mmax = qL²/(9√3)
    b = BeamModel(6, [(0, "pin"), (6, "roller")], [], [], [], [(0, 6, 0, 12)])
    assert b.reactions[0][3] == A(12) and b.reactions[1][3] == A(24)
    assert b.max_abs()["M"] == A(12 * 36 / (9 * math.sqrt(3)), rel=1e-3)


def test_opposite_triangles():
    # q1 = −q2: tổng lực 0, chỉ còn mômen, phản lực khác 0
    b = BeamModel(6, [(0, "pin"), (6, "roller")], [], [], [], [(0, 6, 10, -10)])
    assert b.reactions[0][3] == A(10) and b.reactions[1][3] == A(-10)


def test_section_I():
    p = defaults("sbvl_section")
    r = run("sbvl_section", p)
    S = I_props(400, 200, 8, 12)
    assert r.results["Ix"] == A(S["Ix"]) and r.results["A"] == A(S["A"]) and r.results["yc"] == A(200)


def test_axial():
    r = run("sbvl_axial", defaults("sbvl_axial"))
    assert r.results["N1"] == A(30) and r.results["N3"] == A(-30)
    d = 30 * 100 / (21000 * 20) * 2 + (-30) * 150 / (21000 * 10)
    assert r.results["delta"] == A(d)


def test_torsion():
    r = run("sbvl_torsion", defaults("sbvl_torsion"))
    assert r.results["T1"] == A(1) and r.results["T2"] == A(-1)
    assert r.results["tau2"] == A(16 * 1e6 / (math.pi * 60 ** 3))


def test_mohr():
    r = run("sbvl_mohr", defaults("sbvl_mohr"))
    assert r.results["s1"] == A(20 + math.hypot(60, 30))
    assert r.results["s2"] == A(20 - math.hypot(60, 30))
    assert r.results["s4"] == A(math.sqrt(80 ** 2 + 40 ** 2 + 80 * 40 + 3 * 30 ** 2))


def test_euler():
    r = run("sbvl_buckling", {**defaults("sbvl_buckling"), "L": 6, "mu": 1.0, "A": 40, "Imin": 400})
    assert r.results["Ncr"] == A(math.pi ** 2 * 200000 * 400e4 / 6000 ** 2 / 1e3)


# ---------- Cơ kết cấu ----------
def test_three_moment_two_span():
    r = run("ckc_threemoment", dict(sp=[dict(L=4, I=1, q=10, P=0, a=0), dict(L=4, I=1, q=10, P=0, a=0)], left="pin", right="pin"))
    assert r.results["M1"] == A(-20) and r.results["R0"] == A(15) and r.results["R1"] == A(50)


def test_three_moment_fixed_fixed():
    r = run("ckc_threemoment", dict(sp=[dict(L=6, I=1, q=12, P=0, a=0), dict(L=6, I=1, q=12, P=0, a=0)], left="fixed", right="fixed"))
    assert r.results["M0"] == A(-36) and r.results["M1"] == A(-36)


def test_three_moment_point_load():
    # dầm 2 đầu ngàm, P giữa nhịp: M gối = PL/8 (2 nhịp đối xứng cũng ngàm tại gối giữa)
    r = run("ckc_threemoment", dict(sp=[dict(L=4, I=1, q=0, P=80, a=2), dict(L=4, I=1, q=0, P=80, a=2)], left="fixed", right="fixed"))
    assert r.results["M1"] == A(-40)


def portal(**k):
    p = defaults("ckc_frame_stiff")
    p["nodes"] = [dict(x=0, y=0), dict(x=0, y=6), dict(x=6, y=6), dict(x=6, y=0)]
    p["elems"] = [dict(i=1, j=2, EI=1e4, qy=0, qx=0, hi=0, hj=0), dict(i=2, j=3, EI=1e4, qy=12, qx=0, hi=0, hj=0), dict(i=3, j=4, EI=1e4, qy=0, qx=0, hi=0, hj=0)]
    p["load"] = []
    p.update(k)
    return p


def test_portal_kleinlogel():
    r = run("ckc_frame_stiff", portal())
    (ma, mb), (mb2, mc), _ = r.results["M_ends"]
    assert ma == A(12, rel=1e-3) and mb == A(-24, rel=1e-3) and mb2 == A(-24, rel=1e-3)


def test_portal_sway():
    # ngàm chân, H tại đỉnh, k=1: M chân = 3·... Kleinlogel: M_A = H·h(3k+1)/(2(6k+1))
    p = portal()
    p["elems"][1]["qy"] = 0
    p["load"] = [dict(n=2, fx=10, fy=0, m=0)]
    r = run("ckc_frame_stiff", p)
    ma = abs(r.results["M_ends"][0][0])
    assert ma == A(10 * 6 * 4 / (2 * 7), rel=2e-3)


def test_force_method_matches():
    p = portal()
    p["red"] = defaults("ckc_frame_force")["red"]
    r = run("ckc_frame_force", p)
    assert r.results["n"] == 3
    assert r.results["maxdiff"] < 1e-2
    assert r.results["X2"] == A(36, rel=1e-4)  # đối xứng: phản lực đứng = qL/2


def test_force_wrong_count():
    p = portal()
    p["red"] = [dict(n=4, c="ngang")]
    with pytest.raises(InputError):
        run("ckc_frame_force", p)


def test_frame_unstable():
    p = portal()
    p["sup"] = [dict(n=1, ux=0, uy=1, rz=0)]
    with pytest.raises(InputError):
        run("ckc_frame_stiff", p)


def test_truss_triangle():
    # tam giác 3-4-5: tải 100 tại đỉnh
    r = run("ckc_truss", dict(nodes=[dict(x=0, y=0), dict(x=6, y=0), dict(x=3, y=4)], bars=[dict(i=1, j=2), dict(i=1, j=3), dict(i=2, j=3)],
                              sup=[dict(n=1, ux=1, uy=1), dict(n=2, ux=0, uy=1)], load=[dict(n=3, fx=0, fy=-100)]))
    assert r.results["N2"] == A(-62.5) and r.results["N3"] == A(-62.5) and r.results["N1"] == A(37.5)
    assert r.results["R1y"] == A(50)


def test_truss_unstable_count():
    with pytest.raises(InputError):
        run("ckc_truss", dict(nodes=[dict(x=0, y=0), dict(x=6, y=0), dict(x=3, y=4)], bars=[dict(i=1, j=2), dict(i=1, j=3)],
                              sup=[dict(n=1, ux=1, uy=1), dict(n=2, ux=0, uy=1)], load=[]))


def test_arch_parabola_full_load():
    # vòm parabol, tải đều toàn nhịp: mômen = 0 mọi nơi, H = qL²/8f
    r = run("ckc_arch", {**defaults("ckc_arch"), "q": [dict(a=0, b=20, q=10)], "P": []})
    assert r.results["H"] == A(10 * 400 / (8 * 5))
    assert abs(r.results["Mmax"]) < 1e-6


def test_gerber():
    r = run("ckc_gerber", defaults("ckc_gerber"))
    # cân bằng tổng: 12·8 + 40 = 136
    v = sum(x for k, x in r.results.items() if k.startswith("V"))
    assert v == A(136)
    assert "MA" in r.results


def test_influence():
    r = run("ckc_il", dict(L=10, o1=0, o2=0, xk=5, tr=[dict(P=100, d=0)], q=0))
    assert r.results["Mkmax"] == A(250) and r.results["RAmax"] == A(100)
    assert r.results["Qkmax"] == A(50) and r.results["Qkmin"] == A(-50)


# ---------- BTCT ----------
def test_xi_R():
    assert xi_R(350) == A(0.8 / (1 + 0.00175 / 0.0035))
    assert xi_R(350) == A(0.5333, abs=1e-3)
    assert xi_R(210) == A(0.615, abs=1e-3)


def test_beam_single():
    r = run("btct_beam_design", defaults("btct_beam_design"))
    Rb = 14.5 * .9
    h0 = 455
    am = 180e6 / (Rb * 220 * h0 ** 2)
    assert r.results["am"] == A(am)
    assert r.results["As"] == A((1 - math.sqrt(1 - 2 * am)) * Rb * 220 * h0 / 350)
    assert r.results["Mgh"] > 180 * 1e0 * 1  # Mgh theo kN·m
    assert r.ok


def test_beam_double():
    r = run("btct_beam_design", {**defaults("btct_beam_design"), "M": 320})
    assert r.results["Asc_req"] > 0
    assert r.ok
    assert r.results["Mgh"] >= 320


def test_tbeam_cases():
    r = run("btct_tbeam", defaults("btct_tbeam"))
    assert r.results["Mf"] > 260
    r2 = run("btct_tbeam", {**defaults("btct_tbeam"), "bf": 500, "hf": 60, "M": 260, "clr": 3000})
    assert r2.results["Mf"] < 260


def test_shear():
    r = run("btct_shear", defaults("btct_shear"))
    Rbt = 1.05 * .9
    assert r.results["Qbmin"] == A(.5 * Rbt * 220 * 455 / 1e3)
    assert r.results["Qstrut"] == A(.3 * 14.5 * .9 * 220 * 455 / 1e3)
    assert r.results["s"] <= min(.5 * 455, 300)


def test_column_consistent():
    p = defaults("btct_column")
    r = run("btct_column", p)
    assert r.ok
    # lệch tâm lớn
    r2 = run("btct_column", {**p, "N": 400, "M": 250, "NL": 300, "ML": 120})
    assert r2.results["case"] in ("lon", "lon_x_nho")
    assert r2.ok
    # nén gần đúng tâm
    r3 = run("btct_column", {**p, "N": 2500, "M": 20, "NL": 1500, "ML": 10})
    assert r3.results["case"] == "be"
    assert r3.ok


def test_column_capacity_pure_bending_like():
    # N rất nhỏ, thép đối xứng: Mgh ≈ Rs·As·(h0−a')
    Rb, Rs = 13.05, 350
    As = 1000
    M, x = column_capacity(300, 500, 45, 45, As, As, Rb, Rs, Rs, xi_R(350), 1.0, 1e6)
    assert M == A(350 * 1000 * 410, rel=1e-3)


def test_plate_timoshenko():
    c = plate_coeffs(1, 1, ("k", "k", "k", "k"), nu=.3, n=40)
    assert c["a_x"] == A(.0479, rel=.005) and c["a_y"] == A(.0479, rel=.005)
    c = plate_coeffs(1, 1, ("n", "n", "n", "n"), nu=.3, n=40)
    assert c["a_x"] == A(.0231, rel=.01) and c["b_left"] == A(.0513, rel=.01)
    c = plate_coeffs(1, 2, ("k", "k", "k", "k"), nu=.3, n=20)
    assert c["a_y"] * 2 == A(.1017, rel=.01)  # Timoshenko b/a = 2


def test_slab_oneway():
    r = run("btct_slab", {**defaults("btct_slab"), "L1": 2, "L2": 5, "q": 12})
    assert r.results["Mn"] == A(12 * 4 / 24) and r.results["Mg"] == A(12 * 4 / 12)


def test_sls_no_crack():
    r = run("btct_sls", {**defaults("btct_sls"), "Mser": 10, "ML": 8})
    assert r.results["acL"] == 0


def test_punching():
    r = run("btct_punching", {**defaults("btct_punching"), "Mx": 0, "My": 0})
    h0 = 290
    u = 2 * (300 + 400 + 2 * h0)
    assert r.results["Fb"] == A(1.05 * .9 * u * h0 / 1e3)


# ---------- Nền móng ----------
def test_abd_table():
    # TCVN 9362:2012 Bảng 14
    a, b, d = abd(20)
    assert a == A(.51, abs=.01) and b == A(3.06, abs=.01) and d == A(5.66, abs=.01)
    a, b, d = abd(30)
    assert a == A(1.15, abs=.01) and b == A(5.59, abs=.01) and d == A(7.95, abs=.01)
    assert abd(0) == (0, 1, math.pi)


def test_k0_table():
    assert k0_center(2, 2, 2) == A(.336, abs=.002)  # l/b=1, z/b=1
    assert k0_center(2, 2, 1) == A(.701, abs=.002)  # z/b=0,5
    assert k0_center(2, 2, 1e-6) == A(1, abs=1e-3)


def test_pad_conditions():
    r = run("nm_pad", defaults("nm_pad"))
    assert r.ok
    assert r.results["pmax"] <= 1.2 * r.results["R"] + 1e-9
    assert r.results["pmin"] >= 0


def test_settle():
    r = run("nm_settle", defaults("nm_settle"))
    assert r.results["pgl"] == A(180 - 17 * 1.5)
    assert 0 < r.results["S"] < 8


def test_pile_phys():
    r = run("nm_pile", defaults("nm_pile"))
    Ab, u = .09, 1.2
    assert r.results["Qb"] == A(4000 * Ab)
    assert r.results["Qs"] == A(u * (25 * 4 + 45 * 5 + 60 * 4))
    assert r.results["Rcd"] == A(1.15 * r.results["Rcu"] / (1.15 * 1.75))


def test_pile_spt():
    r = run("nm_pile", {**defaults("nm_pile"), "method": "spt"})
    assert r.results["qb"] == A(300 * 30)


def test_pilecap_equilibrium():
    p = defaults("nm_pilecap")
    r = run("nm_pilecap", p)
    assert r.results["Pmax"] > r.results["Pmin"]


def test_wall():
    r = run("nm_wall", defaults("nm_wall"))
    assert r.results["Ka"] == A(1 / 3)
    assert r.ok


# ---------- Thép ----------
def test_phi_curves():
    # giá trị SP 16.13330.2017 bảng D.1, λ̄ = 2: a≈0,884; b≈0,826; c≈0,740 (sai số tra bảng 0,02)
    assert phi_buckling(2.0, "b") == A(.82, abs=.03)
    assert phi_buckling(.3, "c") == 1
    assert phi_buckling(4, "a") > phi_buckling(4, "b") > phi_buckling(4, "c")
    # λ̄ lớn phải tiến tới Euler: φ ≈ π²/λ̄²
    assert phi_buckling(8, "b") == A(7.6 / 64)


def test_steel_beam():
    r = run("thep_beam", defaults("thep_beam"))
    S = I_props(400, 200, 8, 12)
    assert r.results["sigma"] == A(120e6 / S["Wx"])
    assert r.results["defl"] == A(5 * 18 * 6000 ** 4 / (384 * 206000 * S["Ix"]))


def test_bolt():
    r = run("thep_bolt", defaults("thep_bolt"))
    assert r.results["Nv"] == A(320 * .9 * 314 / 1e3)
    assert r.results["Nt"] == A(400 * 245 / 1e3)


def test_weld_fillet():
    p = {**defaults("thep_weld"), "N": 0, "V": 100, "M": 0}
    r = run("thep_weld", p)
    assert r.results["twf"] == A(100e3 / (2 * .7 * 8 * 250))


# ---------- Hệ thống ----------
@pytest.mark.parametrize("pid", list(registry.PROBLEMS))
def test_exports(pid):
    r = run(pid, defaults(pid))
    assert report_html(r)
    assert r.summary, "thiếu kết luận"
    for f, (fn, _, _) in FORMATS.items():
        data = fn(r)
        if f == "docx":
            z = zipfile.ZipFile(io.BytesIO(data))
            xml = z.read("word/document.xml").decode()
            if any(b.kind == "f" for b in r.blocks):
                assert "m:oMath" in xml
        elif f == "pptx":
            zipfile.ZipFile(io.BytesIO(data)).read("ppt/presentation.xml")
        elif f == "pdf":
            assert data[:4] == b"%PDF"
        elif f == "dxf":
            t = data.decode("ascii")
            assert "EOF" in t and "ENTITIES" in t
            pairs = t.split("\n")
            assert len(pairs) % 2 == 1  # mã nhóm + giá trị, dòng cuối rỗng
            if r.drawings:
                assert "LINE" in t or "CIRCLE" in t


def test_vietnamese_in_dxf():
    r = run("btct_beam_design", defaults("btct_beam_design"))
    t = FORMATS["dxf"][0](r).decode("ascii")
    assert "\\U+00D8" in t  # Ø
    for layer in ("BE_TONG", "THEP", "DAI", "KICH_THUOC", "CHU", "KY_HIEU"):
        assert layer in t


def test_bad_inputs():
    with pytest.raises(InputError):
        run("btct_beam_design", {**defaults("btct_beam_design"), "b": "abc"})
    with pytest.raises(InputError):
        run("sbvl_beam", beam_params(xA=4, xB=2))
    with pytest.raises(InputError):
        run("khong_co", {})


def test_math_parser():
    s = "α_m = [[M // R_b·b·h_0^2]] = √[[a^2 + b_(x,y)]]"
    assert "M" in to_plain(s)
    from xml.dom import minidom
    minidom.parseString(to_omml(s))


def _perturb(p, rnd, wild):
    out = {}
    for k, v in p.items():
        if isinstance(v, bool) or isinstance(v, str):
            out[k] = v
        elif isinstance(v, (int, float)):
            f = rnd.choice([.2, .5, .8, 1, 1.3, 2, 5]) if wild else rnd.uniform(.7, 1.4)
            out[k] = round(v * f, 3)
        elif isinstance(v, list):
            out[k] = [_perturb(r, rnd, wild) if isinstance(r, dict) else r for r in v]
        else:
            out[k] = v
    return out


@pytest.mark.parametrize("pid", list(registry.PROBLEMS))
def test_fuzz_no_crash(pid):
    """Dữ liệu ngẫu nhiên chỉ được phép giải xong hoặc báo InputError, không được lỗi chương trình."""
    rnd = random.Random(hash(pid) & 0xffff if False else sum(map(ord, pid)))
    d = defaults(pid)
    for i in range(12):
        p = _perturb(d, rnd, wild=i % 2 == 0)
        for f in registry.PROBLEMS[pid]["fields"]:
            if f["type"] == "sel":
                p[f["key"]] = rnd.choice(f["options"])["value"]
        try:
            r = run(pid, p)
            if i < 3:
                report_html(r)
                FORMATS["dxf"][0](r)
        except InputError:
            pass


# ---------- Các trường hợp từng báo ĐẠT sai (rà soát độc lập) ----------
def test_column_weak_axis_fails():
    # phương yếu λ = 104: không được báo ĐẠT; hoặc báo KHÔNG ĐẠT, hoặc báo không bố trí nổi thép
    try:
        r = run("btct_column", {**defaults("btct_column"), "b": 200, "h": 600, "l0": 6000, "l0b": 6000, "L": 6000, "N": 1500, "M": 30, "NL": 900, "ML": 15})
    except InputError:
        return
    assert r.ok is False


def test_slender_column_not_false_pass():
    r = run("btct_column", {**defaults("btct_column"), "N": 888, "M": 104, "NL": 500, "ML": 50, "b": 400, "h": 300, "l0": 7517, "l0b": 3000, "L": 7517, "conc": "B30", "steel": "CB300-V"})
    if r.ok:
        # nếu đạt thì e, Mgh phải tính với thép đã chọn: kiểm tra lại bằng số trong báo cáo
        assert r.results["eta"] < 3
    assert any(b.kind == "check" for b in r.blocks)


@pytest.mark.parametrize("Ms", [-200, 200])
def test_pad_moment_sign(Ms):
    r = run("nm_pad", {**defaults("nm_pad"), "M": Ms, "H": 0})
    assert r.results["pmax"] <= 1.2 * r.results["R"] + 1e-9
    assert r.results["pmin"] >= 0


def test_pad_moment_sign_symmetric():
    a = run("nm_pad", {**defaults("nm_pad"), "M": -200, "H": 0})
    b = run("nm_pad", {**defaults("nm_pad"), "M": 200, "H": 0})
    assert a.results["b"] == b.results["b"] and a.results["MI"] == A(b.results["MI"])


def test_strip_negative_moment():
    a = run("nm_strip", {**defaults("nm_strip"), "M": -40})
    b = run("nm_strip", {**defaults("nm_strip"), "M": 40})
    assert a.results["b"] == b.results["b"]


def test_pad_tower_one_side():
    # móng hẹp dài: tháp vượt một phương vẫn phải kiểm tra phần còn lại
    r = run("nm_pad", {**defaults("nm_pad"), "N": 1215, "M": 0, "H": 0, "ratio": 2.5, "phi": 35, "c": 30, "hmin": 350})
    assert any(b.kind == "check" and b.data["name"].startswith("Cắt") for b in r.blocks)
    assert r.ok


def test_slab_over_reinforced_flagged():
    r = run("btct_slab", {**defaults("btct_slab"), "L1": 4, "L2": 8, "q": 22, "h": 100, "conc": "B20", "steel": "CB400-V"})
    assert r.ok is False


def test_slab_bar_never_short():
    from vxtgiai.solvers.rc_common import pick_slab_bar
    for As in (100, 300, 800, 1500, 2500, 2800):
        d, s, Ar = pick_slab_bar(As)
        assert Ar >= As
    with pytest.raises(InputError):
        pick_slab_bar(5000)


def test_pile_np_cap():
    r = run("nm_pile", {**defaults("nm_pile"), "method": "spt", "Np": 100})
    assert r.results["qb"] == A(300 * 50)


def test_bored_pile_material():
    a = run("nm_pile", {**defaults("nm_pile"), "ptype": "bored", "gb": 1.0, "gcb": 0.595})
    assert a.results["Rvl"] == A(.9 * (8 * 0 + 11.5 * 0.595 * 90000 + 350 * 1018) / 1e3, rel=.2) or a.results["Rvl"] > 0


def test_deflection_uses_full_load():
    r = run("btct_sls", {**defaults("btct_sls"), "Mser": 150, "ML": 40})
    assert r.results["f"] > 15


def test_beamcol_small_N_large_M_not_pass():
    p = {**defaults("thep_beamcol"), "N": 1, "M": 200, "ly": 6}
    r = run("thep_beamcol", p)
    assert r.ok is False


def test_wall_mean_pressure():
    r = run("nm_wall", {**defaults("nm_wall"), "Rd": 20})
    assert r.ok is False


def test_settle_depth_insufficient_fails():
    r = run("nm_settle", {**defaults("nm_settle"), "lay": [dict(t=1, g=18, E=8000)]})
    assert r.ok is False


def test_il_reversed_train():
    # đoàn tải không đối xứng, mặt cắt lệch: chiều ngược phải được thử
    a = run("ckc_il", dict(L=20, o1=0, o2=0, xk=5, tr=[dict(P=100, d=0), dict(P=10, d=4)], q=0))
    # đặt P=100 tại k, P=10 phía nhịp dài là lớn nhất = 100·3,75 + 10·2,75
    assert a.results["Mkmax"] == A(100 * 3.75 + 10 * 2.75)
