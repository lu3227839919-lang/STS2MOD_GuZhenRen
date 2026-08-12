# -*- coding: utf-8 -*-
"""蛊真人卡牌卡图审计脚本。

模拟运行时 CardImageCatalog.ValidateAssembly 的判定逻辑,静态分析
GuZhenRenCode/ 下所有 C# 卡牌类,检查 GuZhenRen/images/cards/{类名}.png
是否齐全。

判定规则(与 CardImageCatalog.ValidateAssembly 一致):
- 非抽象类
- 命名空间以 GuZhenRen 开头
- 继承链顶端是 CardModel 或 ModCardTemplate(RitsuLib 外部根)
- 类名必须有同名 PNG:res://GuZhenRen/images/cards/{CardTypeName}.png

共享图片(可选配置):某些卡牌类有意复用其它类的卡图(如宙道伴生牌
复用对应蛊牌的图、年流+ 复用年流的图),这类不算缺图。两种来源:
1. 代码中显式写 `AssetProfile => CardImageCatalog.Create(typeof(其它))`
   ——脚本自动识别;
2. 尚未写代码、但设计上确定的共享关系——在下方 SHARED_IMAGES
   字典中配置:`{被复用类: [复用它的类, ...]}`。

用法:
    python tools/audit_card_images.py [仓库根目录]
    # 默认使用脚本所在仓库根目录;也可显式传参指向其它克隆

输出:
    1. 缺少同名 PNG 的卡牌类(缺图)
    2. 已有同名 PNG 的卡牌类
    3. 显式复用其它卡图的类(AssetProfile => Create(typeof(其它)),不算缺)
    4. PNG 存在但代码中无对应类的孤儿图片
"""
import io
import os
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

ROOT = os.path.abspath(
    sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")
)
CODE = os.path.join(ROOT, "GuZhenRenCode")
IMG = os.path.join(ROOT, "GuZhenRen", "images", "cards")

# 设计上确定的共享图片(尚未在代码中显式写 AssetProfile 时用)
# 键 = 提供图片的卡类名;值 = 复用该图、不另配图的卡类名列表
# 注:宙道伴生牌(GuangYinRenRan/NianNianSuiSui/ZhouMao/SiShuiLiuNian)
# 与 NianLiuPlus 已在代码中显式写 AssetProfile => Create(typeof(对应蛊类)),
# 脚本会从代码自动识别,此处可留空。
SHARED_IMAGES = {}

if not os.path.isdir(CODE) or not os.path.isdir(IMG):
    print(f"错误:仓库根目录无效({ROOT})——找不到 GuZhenRenCode/ 或 GuZhenRen/images/cards/")
    sys.exit(2)

ns_re = re.compile(r"^\s*namespace\s+([\w.]+)\s*\{?", re.MULTILINE)
class_line_re = re.compile(
    r"^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial)\s+)*class\s+(\w+)",
    re.MULTILINE,
)

EXTERNAL_ROOTS = {"CardModel", "ModCardTemplate"}

classes = {}  # name -> {"file","base","abstract","namespace"}

for dirpath, _, files in os.walk(CODE):
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        path = os.path.join(dirpath, fn)
        rel = os.path.relpath(path, ROOT)
        try:
            text = io.open(path, encoding="utf-8").read()
        except Exception:
            text = io.open(path, encoding="gbk", errors="replace").read()
        ns = None
        for m in ns_re.finditer(text):
            ns = m.group(1)
        lines = text.split("\n")
        for m in class_line_re.finditer(text):
            name = m.group(1)
            if name in classes:
                continue
            start = m.start()
            # 从 class NAME 行开始,拼接直到遇到 '{'(最多 8 行),处理跨行基类
            line_idx = text[:start].count("\n")
            decl_parts = []
            abstract = False
            for i in range(line_idx, min(line_idx + 8, len(lines))):
                seg = lines[i]
                decl_parts.append(seg)
                if "abstract" in seg:
                    abstract = True
                if "{" in seg:
                    break
            decl = " ".join(re.sub(r"//[^\n]*", "", p) for p in decl_parts)
            decl = re.sub(r"\s+", " ", decl)
            classes[name] = {
                "file": rel,
                "base": decl,
                "abstract": abstract,
                "namespace": ns,
            }


def base_name(base_str):
    """取冒号后的第一个基类名,去掉泛型/命名空间前缀/大括号。"""
    if ":" not in base_str:
        return None
    b = base_str.split(":", 1)[1]
    b = b.split("{")[0].strip()
    b = b.split(",")[0].strip()
    b = re.sub(r"<.*>", "", b).strip()
    b = b.split(".")[-1].strip()
    return b or None


def is_cardmodel(name, classes, cache, depth=0):
    if depth > 30:
        return False
    if name in cache:
        return cache[name]
    cls = classes.get(name)
    if cls is None:
        return False
    b = base_name(cls["base"])
    if not b:
        cache[name] = False
        return False
    if b in EXTERNAL_ROOTS or "CardModel" in cls["base"] or "ModCardTemplate" in cls["base"]:
        cache[name] = True
        return True
    res = is_cardmodel(b, classes, cache, depth + 1)
    cache[name] = res
    return res


cache = {}
card_classes = []
for name, cls in sorted(classes.items()):
    if cls["abstract"]:
        continue
    if cls["namespace"] and not cls["namespace"].startswith("GuZhenRen"):
        continue
    if is_cardmodel(name, classes, cache):
        card_classes.append((name, cls))

# 显式复用其它卡图的类(AssetProfile => CardImageCatalog.Create(typeof(其它)))
explicit_reuse = {}
reuse_re = re.compile(
    r"AssetProfile\s*=>\s*CardImageCatalog\.Create\(\s*typeof\(\s*(\w+)\s*\)\s*\)"
)
for dirpath, _, files in os.walk(CODE):
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        path = os.path.join(dirpath, fn)
        text = io.open(path, encoding="utf-8", errors="replace").read()
        for m in reuse_re.finditer(text):
            prefix = text[: m.start()]
            cls_match = list(class_line_re.finditer(prefix))
            if cls_match:
                owner = cls_match[-1].group(1)
                target = m.group(1)
                if owner != target:
                    explicit_reuse.setdefault(owner, target)

# 合并:设计上确定的共享图片(SHARED_IMAGES)也视为复用
for provider, consumers in SHARED_IMAGES.items():
    for consumer in consumers:
        explicit_reuse.setdefault(consumer, provider)

imgs = {f[:-4] for f in os.listdir(IMG) if f.endswith(".png")}

# 需要"提供图片"的类:自身无同名 PNG 且没有被其它类复用为图源时,才需要配图
# 若某类已被其它类复用(作为图源),其同名 PNG 必须存在;复用的消费者类不需要 PNG。
providers = set(explicit_reuse.values())  # 被复用为图源的类
missing = []
for n, c in card_classes:
    if n in imgs:
        continue
    # 消费者(被别的类提供图)不算缺图
    if n in explicit_reuse and explicit_reuse[n] in imgs:
        continue
    missing.append((n, c, explicit_reuse.get(n)))

present = [(n, c) for n, c in card_classes if n in imgs]
extra = sorted(imgs - {n for n, _ in card_classes})

print(f"== 仓库: {ROOT}")
print(f"== 具体 CardModel/ModCardTemplate 子类: {len(card_classes)}")
print(f"== images/cards PNG: {len(imgs)}")
print(f"== 共享图片映射: {len(explicit_reuse)} 个消费者复用图源")
print()
print(f"### 缺少同名 PNG ({len(missing)})")
for n, c, reused in missing:
    tag = f"  [复用 {reused}.png,不算缺]" if reused else ""
    print(f"  MISS {n}  <- {c['base'][:70]}{tag}  @ {c['file']}")
print()
print(f"### 已有同名 PNG ({len(present)})")
for n, c in present:
    print(f"  OK   {n}  @ {c['file']}")
print()
print(f"### PNG 存在但无对应类 ({len(extra)})")
for n in extra:
    print(f"  EXTRA {n}")
