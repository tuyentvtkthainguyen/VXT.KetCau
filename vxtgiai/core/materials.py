"""Số liệu vật liệu. Đơn vị MPa.

Bê tông, cốt thép: TCVN 5574:2018 (Bảng 7, 8, 10, 13, 15).
Thép kết cấu: fy, fu theo TCVN 5575; E = 206000 MPa theo TCVN 5575:2024.
"""
CONCRETE = {
    "B15": dict(Rb=8.5, Rbt=0.75, Rbs=11.0, Rbts=1.10, Eb=24000, cr=2.4),
    "B20": dict(Rb=11.5, Rbt=0.90, Rbs=15.0, Rbts=1.35, Eb=27500, cr=2.0),
    "B25": dict(Rb=14.5, Rbt=1.05, Rbs=18.5, Rbts=1.55, Eb=30000, cr=1.8),
    "B30": dict(Rb=17.0, Rbt=1.15, Rbs=22.0, Rbts=1.75, Eb=32500, cr=1.6),
    "B35": dict(Rb=19.5, Rbt=1.30, Rbs=25.5, Rbts=1.95, Eb=34500, cr=1.5),
    "B40": dict(Rb=22.0, Rbt=1.40, Rbs=29.0, Rbts=2.10, Eb=36000, cr=1.4),
}
REBAR = {
    "CB240-T": dict(Rs=210, Rsc=210, Rsw=170, Es=200000),
    "CB300-V": dict(Rs=260, Rsc=260, Rsw=210, Es=200000),
    "CB400-V": dict(Rs=350, Rsc=350, Rsw=280, Es=200000),
    "CB500-V": dict(Rs=435, Rsc=400, Rsw=300, Es=200000),
}
STEEL = {
    "CCT34": dict(fy=220, fu=340),
    "CCT38": dict(fy=240, fu=380),
    "CCT42": dict(fy=260, fu=420),
    "SS400": dict(fy=245, fu=400),
    "Q345": dict(fy=345, fu=470),
}
E_STEEL = 206000.0
BOLTS = {  # cấp bền: fub, fvb, ftb  (MPa) - cường độ tính toán theo TCVN 5575:2012, bảng 11
    "5.6": dict(fvb=190, ftb=210),
    "5.8": dict(fvb=200, ftb=200),
    "8.8": dict(fvb=320, ftb=400),
    "10.9": dict(fvb=400, ftb=500),
}
BOLT_AREA = {12: (113, 84.3), 14: (154, 115), 16: (201, 157), 18: (254, 192), 20: (314, 245), 22: (380, 303), 24: (452, 352), 27: (572, 459), 30: (706, 560)}
ELECTRODE = {"N42": 180, "N46": 200, "N50": 215}


def xi_R(Rs, Es=200000.0):
    es = Rs / Es
    return 0.8 / (1 + es / 0.0035)


def bar_area(d):
    import math
    return math.pi * d ** 2 / 4
