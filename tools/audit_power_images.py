# -*- coding: utf-8 -*-
"""Power 图标审计:解析 GuZhenRenCode/Powers 下所有继承
ModPowerTemplate 的具体能力类,检查 images/power/ 下对应
64x64/256x256 图片是否存在。

每个 Power 显式写 AssetProfile(自定义地址)声明图标路径,
脚本按 IconPath/BigIconPath 检查文件是否存在。
"""
import io
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

ROOT = os.path.abspath(
    sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")
)
CODE = os.path.join(ROOT, "GuZhenRenCode", "Powers")
IMG = os.path.join(ROOT, "GuZhenRen", "images", "power")

if not os.path.isdir(CODE) or not os.path.isdir(IMG):
    print(f"错误:找不到 GuZhenRenCode/Powers 或 GuZhenRen/images/power({ROOT})")
    sys.exit(2)

class_re = re.compile(
    r"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial)\s+)*class\s+(\w+)",
    re.MULTILINE,
)

# 判定某类是否(直接或间接)继承 ModPowerTemplate
POWER_ROOTS = {"ModPowerTemplate"}

# 收集所有类声明(name -> 类声明头文本)用于继承链判定
class_decls = {}
for dirpath, _, files in os.walk(CODE):
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        text = io.open(p, encoding="utf-8", errors="replace").read()
        lines = text.split("\n")
        for m in class_re.finditer(text):
            name = m.group(1)
            if name in class_decls:
                continue
            start = m.start()
            line_idx = text[:start].count("\n")
            parts = []
            for i in range(line_idx, min(line_idx + 8, len(lines))):
                parts.append(lines[i])
                if "{" in lines[i]:
                    break
            decl = " ".join(re.sub(r"//[^\n]*", "", x) for x in parts)
            decl = re.sub(r"\s+", " ", decl)
            class_decls[name] = decl


def first_base(decl):
    """取冒号后第一个基类名(去掉泛型/命名空间前缀/大括号)。"""
    if ":" not in decl:
        return None
    b = decl.split(":", 1)[1]
    b = b.split("{")[0].strip()
    b = b.split(",")[0].strip()
    b = re.sub(r"<.*>", "", b).strip()
    b = b.split(".")[-1].strip()
    return b or None


def is_power(name, cache, depth=0):
    if depth > 30:
        return False
    if name in cache:
        return cache[name]
    decl = class_decls.get(name)
    if decl is None:
        return False
    b = first_base(decl)
    if not b:
        cache[name] = False
        return False
    if b in POWER_ROOTS:
        cache[name] = True
        return True
    res = is_power(b, cache, depth + 1)
    cache[name] = res
    return res


# 收集所有 Power 类 + 其 AssetProfile 的 IconPath/BigIconPath
powers = {}  # name -> {file, icon, big, has_explicit, hidden}
cache = {}
for name, decl in sorted(class_decls.items()):
    if "abstract" in decl.split("{")[0]:
        continue
    if not is_power(name, cache):
        continue
    powers[name] = {
        "file": "?",
        "icon": None,
        "big": None,
        "has_explicit": False,
        "hidden": False,
    }

# 重新按文件扫描,填入文件路径、显式 AssetProfile 与隐藏标志
for dirpath, _, files in os.walk(CODE):
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        text = io.open(p, encoding="utf-8", errors="replace").read()
        for m in class_re.finditer(text):
            name = m.group(1)
            if name not in powers:
                continue
            start = m.start()
            nxt = [x.start() for x in class_re.finditer(text, start + len(name)) if x.start() > start]
            end = min(nxt) if nxt else len(text)
            body = text[start:end]
            im = re.search(r"IconPath:\s*\"([^\"]+)\"", body)
            bm = re.search(r"BigIconPath:\s*\"([^\"]+)\"", body)
            info = powers[name]
            info["file"] = os.path.relpath(p, ROOT)
            if im and bm:
                info["icon"] = im.group(1)
                info["big"] = bm.group(1)
                info["has_explicit"] = True
            # 隐藏/监听型:IsVisibleInternal => false,或注释标注隐藏/监听
            compact = re.sub(r"\s+", "", body)
            info["hidden"] = "IsVisibleInternal=>false" in compact
            head = body.split("{")[0]
            if "隐藏" in head or "监听" in head:
                info["hidden"] = True

imgs = set(os.listdir(IMG))

missing = []
ok = []
hidden_skipped = []
for name, info in sorted(powers.items()):
    icon_ok = big_ok = True
    if info["icon"]:
        icon_ok = os.path.basename(info["icon"]) in imgs
    if info["big"]:
        big_ok = os.path.basename(info["big"]) in imgs
    if info["icon"] is None and info["big"] is None:
        # 无显式 AssetProfile:回退按默认命名规则检查(设计兜底)
        icon_ok = f"{name}-64x64.png" in imgs
        big_ok = f"{name}-256x256.png" in imgs
        info["note"] = "[无显式 AssetProfile] 按 {name}-64x64/-256x256.png 兜底检查"
    else:
        info["note"] = "[显式 AssetProfile]"
    if info["hidden"]:
        # 隐藏/监听型:不强制要求图标,仅记录
        hidden_skipped.append((name, info, icon_ok, big_ok))
        continue
    if icon_ok and big_ok:
        ok.append((name, info))
    else:
        missing.append((name, info, icon_ok, big_ok))

print(f"== Power 类总数: {len(powers)}(其中隐藏/监听型 {len(hidden_skipped)})")
print(f"== images/power PNG: {len([f for f in imgs if f.endswith('.png')])}")
print()
print(f"### 缺图 Power ({len(missing)})")
for name, info, icon_ok, big_ok in missing:
    print(f"  MISS {name}  icon={'OK' if icon_ok else '缺'} big={'OK' if big_ok else '缺'}  @ {info['file']}  {info['note']}")
    if info["icon"]:
        print(f"       IconPath={info['icon']}")
    if info["big"]:
        print(f"       BigIconPath={info['big']}")
print()
print(f"### 有图 Power ({len(ok)})")
for name, info in ok:
    print(f"  OK   {name}  @ {info['file']}")
print()
print(f"### 隐藏/监听型 Power(不要求图标,{len(hidden_skipped)})")
for name, info, icon_ok, big_ok in hidden_skipped:
    tag = "有图" if (icon_ok and big_ok) else "无图(可省略)"
    print(f"  HIDDEN {name}  [{tag}]  @ {info['file']}")

# 孤儿图片:png 但无对应 Power 类且未被任何 AssetProfile 引用
power_names = set(powers.keys())
referenced = set()
for name, info in powers.items():
    for k in ("icon", "big"):
        v = info.get(k)
        if v:
            referenced.add(os.path.basename(v))
orphan = sorted(
    f
    for f in imgs
    if f.endswith(".png")
    and not any(f.startswith(n) for n in power_names)
    and f not in referenced
)
print()
print(f"### PNG 存在但无对应 Power 类且未被引用 ({len(orphan)})")
for f in orphan:
    print(f"  EXTRA {f}")
print()
print(f"### 被其它 Power 显式引用为图标(不算孤儿,{len(referenced)} 个)")
for f in sorted(referenced):
    if not any(f.startswith(n) for n in power_names):
        print(f"  REUSED {f}")
