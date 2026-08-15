# tools 目录说明

本目录存放项目维护辅助脚本(不参与构建/发布,git 忽略与否见 .gitignore 约定)。

## audit_card_images.py — 卡牌卡图审计

**用途**:检查哪些卡牌缺少同名卡图 PNG,以及哪些 PNG 是代码中无对应类的孤儿资源。

**原理**:模拟运行时 `CardImageCatalog.ValidateAssembly`(GuZhenRenPersonalCode/Cards/Core/CardImageCatalog.cs)的判定逻辑——
- 静态解析 `GuZhenRenPersonalCode/` 下全部 C# 类(支持跨行基类声明)
- 判定条件:非抽象 + 命名空间 `GuZhenRen` 开头 + 继承链顶端为 `CardModel` 或 `ModCardTemplate`(RitsuLib 外部根)
- 检查 `GuZhenRen/images/cards/{类名}.png` 是否存在

**用法**:
```powershell
python tools/audit_card_images.py            # 当前仓库
python tools/audit_card_images.py D:\...\STS2_GuZhenRen   # 其它克隆
```

**输出**:
1. 缺少同名 PNG 的卡牌类(标注显式复用其它卡图的例外)
2. 已有同名 PNG 的卡牌类
3. PNG 存在但代码中无对应类的孤儿图片

**注意**:新增卡牌类后应放置同名 PNG;若某卡有意复用其它卡图,应显式写
`AssetProfile => CardImageCatalog.Create(typeof(其它卡类))`,脚本会自动识别为"不算缺"。
设计上已确定、但尚未写进代码的共享关系,可在脚本头部 `SHARED_IMAGES` 字典配置
(当前已配置宙道伴生牌复用蛊牌图、年流+复用年流图)。

## audit_power_images.py — Power 图标审计

**用途**:检查哪些 Power(能力)缺少图标,以及哪些 `images/power/` 图片是孤儿资源。

**原理**:解析 `GuZhenRenPersonalCode/Powers/` 下所有 `ModPowerTemplate` 子类的
`AssetProfile`(IconPath/BigIconPath),检查 `images/power/` 下对应 PNG 是否存在。
未写 `AssetProfile` 的 Power 按默认命名规则 `{类名}-64x64.png`/`-256x256.png` 兜底检查。

**用法**:
```powershell
python tools/audit_power_images.py [仓库根目录]
```

**输出**:
1. 缺图 Power(64x64 或 256x256 缺失)
2. 有图 Power
3. 无对应 Power 类且未被任何 AssetProfile 引用的孤儿 PNG
4. 被其它 Power 显式引用为图标的 PNG(如 JuGuangPower 复用 ShanYaoPower 的图)

**注意**:每个 Power 需在类中显式写自定义地址的
`AssetProfile => new PowerAssetProfile(IconPath: ..., BigIconPath: ...)` 才有图标
(标准命名 `{类型名}-64x64.png` / `-256x256.png`,可自定义);不写则无图标,
编译期 RitsuLib 会对不存在的资源路径报 RITSU013 警告。
