# GuZhenRen / 蛊真人

> 《杀戮尖塔 2》非官方同人角色 Mod  
> An unofficial character mod for *Slay the Spire 2*

[English Overview](#english-overview)

**GuZhenRen** 为《杀戮尖塔 2》加入可操作角色 **方源**，围绕蛊虫、空窍修为、元气、仙元、升炼、合炼与杀招推演，构建一套独立于普通抽牌循环的战斗和成长体系。

本项目最初源于对《杀戮尖塔 1》同名 Mod 的移植尝试。随着开发推进，项目逐步转向结合《杀戮尖塔 2》的机制重新设计，希望在保留核心概念的同时，形成更适合新作的独立玩法。

## 项目状态

| 项目 | 当前信息 |
| --- | --- |
| Mod 版本 | **0.4.14** |
| 适配游戏版本 | **0.110.0** |
| 前置依赖 | **STS2-RitsuLib 0.5.10 或兼容版本** |
| 构建环境 | Godot 4.5.1 .NET / .NET 9 |
| 本地化 | 简体中文、English |
| 开发状态 | 持续开发中 |

### 0.4.14 更新摘要

- 修复方源进入 DARV 事件时，BaseLib 的 `DustyTome.SetupForPlayer` 扩展可能触发空引用并中断事件。
- 补齐 DARV、OROBAS、PAEL、TANX、TEZCATARA、VAKUU、NONUPEIPE 七位先古遗民的中英文角色对话。
- 卡图诊断日志只显示 `res://GuZhenRen/images/cards/...`，不再输出开发者本机目录。
- 新增 `tools/Build-SourceArchive.ps1`，发布源码时主动排除 `local.props`、构建目录、日志和旧压缩包。

### 0.4.13 更新摘要

- 修复永久牌组中的高转蛊虫进入战斗后回退为一转。
- 转数现在同时通过普通实例字段和 `SavedAttachedState` 保存，兼容战斗克隆、存档和多人快照。
- 战斗初始化会再次根据永久牌组校准卡牌转数，并输出 `[蛊虫转数]` 验证日志。

## 核心特色

### 独立蛊牌系统

蛊虫不参与普通抽牌、弃牌与洗牌循环，而是进入独立的：

- **蛊存放牌堆**：保存当前可催动的蛊虫；
- **蛊恢复牌堆**：保存催动次数耗尽、等待恢复的蛊虫。

玩家通过基础牌 **“催动”** 主动选择蛊虫。关键能力不必依赖随机抽到，但会受到原生能量、元气、仙元、催动次数与恢复周期的共同限制。

永久蛊虫容量为 **15 张**。容量已满时，获得合法新蛊虫需要替换一张已有蛊虫。

### 一至九转成长

角色空窍与蛊虫均拥有一至九转成长：

- 一至五转通过修为推进；
- 六转后进入仙道阶段；
- 蛊虫转数会改变伤害、格挡、资源消耗、恢复时间与衍生效果；
- 六转及以上蛊虫成为仙蛊，并额外消耗仙元；
- 同名仙蛊在多人游戏中遵循全队唯一规则。

### 多层资源管理

除游戏原生能量外，方源还会经营多种独立资源：

- **元气**：催动普通蛊虫的核心资源，可跨回合保留；
- **仙元**：六转及以上仙蛊的高阶消耗；
- **光辉**：光道构筑的强化资源；
- **血元**：血道机制的战斗资源。

Replay 只在一次出牌序列的首段支付元气、仙元及相关资源，不会重复扣费。

### 升炼、合炼与杀招推演

篝火新增：

- **升炼**：一次可选择 0 至 2 只蛊虫，各提升一转；选择完成后由玩家主动点击确认；
- **合炼**：按照配方消耗材料蛊，炼制新的蛊虫。

战斗中还可通过蛊牌堆进行 **杀招推演**。杀招属于普通战斗牌，不占用蛊虫容量。

当前已实现的主要合炼配方包括：

```text
三转以上月光蛊 ×1
＋三转以上小光蛊 ×2
＝月芒蛊

三转以上镜光蛊 ×1
＋三转以上定光蛊 ×1
＝镜辉蛊

月芒蛊 ×1
＋血气蛊 ×1
＝血月蛊
```

当前已实现的杀招包括：

- 月霓裳
- 白虹贯日
- 镜月返照

## 当前可玩内容

### 蛊虫

当前源码已实现 10 种蛊虫：

| 道途 | 蛊虫 |
| --- | --- |
| 光道 | 小光蛊、月光蛊、镜光蛊、定光蛊、流光蛊、月芒蛊、镜辉蛊 |
| 土道 | 玉皮蛊 |
| 血道 | 血气蛊、血月蛊 |

### 构筑方向

**光道**是目前内容最完整的体系，围绕以下机制展开：

- **折光**：交替打出不同类型的牌以获得光辉；
- **照破**：让后续攻击的单段伤害获得额外收益；
- **耀化**：选择消耗光辉强化蛊虫；
- **恢复期衍生牌**：蛊虫进入恢复后，按转数生成新的战斗牌。

土道目前以玉皮蛊的防御与反光联动为主；血道目前围绕血气、流血、血元与血印形成基础循环。更多道途、蛊虫、配方、遗物和杀招仍在开发中。

## 安装

### Steam 创意工坊

订阅本 Mod 及其前置依赖 **STS2-RitsuLib**，并确保依赖版本兼容。

### 手动安装

准备以下文件：

```text
GuZhenRen.dll
GuZhenRen.pck
GuZhenRen.json
```

将它们放入：

```text
<Slay the Spire 2>/mods/GuZhenRen/
```

同时安装 STS2-RitsuLib，并确认加载顺序满足依赖关系。

## 开发控制台给予卡牌

开启游戏完整控制台后，可直接使用原生 `card` 指令给予蛊真人卡牌：

```text
card <卡牌ID> <转数>
card <卡牌ID> rank=<转数>
```

例如：

```text
card GU_ZHEN_REN_CARD_YUE_GUANG_GU 5
card GU_ZHEN_REN_CARD_YUE_GUANG_GU rank=7
```

目标位置无需手动填写，模组会自动处理：

- 战斗中给予蛊虫牌：进入蛊恢复堆，并从当前回合开始计算恢复时间；
- 战斗中给予普通牌、杀招或其他非蛊虫牌：进入普通手牌；
- 非战斗场景：直接进入永久牌组。

如仍附带原生位置参数，最终位置也会按上述规则重新校正。超出卡牌允许范围的转数会被修正到该卡牌的合法上下限。

## 从源码构建

### 环境要求

- .NET 9 SDK
- Godot 4.5.1 .NET
- 《杀戮尖塔 2》0.110.0 或兼容版本
- STS2-RitsuLib 0.5.10 或兼容版本

### 配置

复制本地配置模板：

```powershell
Copy-Item local.props.template local.props
```

编辑 `local.props`，填写：

```xml
<Sts2Dir>游戏安装目录</Sts2Dir>
<Sts2DataDir>游戏数据目录</Sts2DataDir>
<GodotExe>Godot .NET 可执行文件路径</GodotExe>
```

### 构建命令

完整构建、导出 PCK 并复制到游戏 Mod 目录：

```powershell
dotnet restore
dotnet build GuZhenRen.sln
```

只验证 C# 编译，不导出 PCK：

```powershell
dotnet build GuZhenRen.sln -p:RunPckExport=false
```

不复制到游戏目录：

```powershell
dotnet build GuZhenRen.sln -p:RunPckExport=false -p:CopyModOnBuild=false
```

## 项目结构

```text
GuZhenRenCode/
├─ Aperture/        空窍状态、修为和转数推进
├─ Cards/           基础牌、蛊虫、仙元、合炼与杀招
├─ Characters/      角色与卡牌、遗物、药水池
├─ Combat/          元气次级资源与战斗接口
├─ Powers/          光道、血道等战斗状态
├─ Patch/           Harmony、UI、多人及兼容补丁
├─ Relics/          空窍起始遗物
└─ RestSite/        升炼、合炼与篝火流程

GuZhenRen/
├─ localization/    简体中文与英文文本
├─ materials/       卡框与材质
├─ scenes/          角色、篝火、商店与战斗 UI
└─ shaders/         自定义着色器
```

## 多人、存档与兼容性

项目已为以下内容提供基础兼容处理：

- 空窍修为、蛊虫转数与仙元状态保存；
- 蛊牌堆、材料选择和篝火操作的多人同步；
- 同名仙蛊的跨玩家唯一性检查；
- 卡牌奖励、商店、替换与合炼的合法性检查；
- 简体中文与英文文本兼容；
- 旧目标数据、恢复流程及部分 UI 状态的存档修复。

多人模式与游戏 API 仍可能随游戏和依赖库更新而需要继续适配。

## 已知限制

- 项目仍处于开发阶段，卡牌数量、数值平衡、界面表现和多人兼容性可能继续调整。
- 灾劫和十转内容尚未实现，九转是当前上限。
- 力道、兽力虚影、搏命等内容仍以机制框架或设计稿为主。
- 药水池已经注册，但当前没有专属药水。
- 部分视觉资源仍为占位内容，角色战斗动画暂不在当前开发计划内。
- 源码包可能不包含所有发布版美术资源。

## 反馈与贡献

欢迎提交：

- 错误日志与复现步骤；
- 中英文文本修正；
- 数值和平衡建议；
- 多人模式兼容问题；
- 代码修复与功能改进。

反馈问题时，请尽量附上：

```text
游戏版本
Mod 版本
RitsuLib 版本
已启用的其他 Mod
错误日志
复现步骤
```

本项目由一名大学生独立开发，当前主要精力用于玩法系统、程序实现与基础视觉资源。感谢对个人开发项目的理解、测试与建议。

## 致谢

感谢以下开源项目和社区资源为本项目提供学习与开发参考：

- [WineFox](https://steamcommunity.com/sharedfiles/filedetails/?id=3747599967)
- [The Watcher](https://steamcommunity.com/sharedfiles/filedetails/?id=3747492505)
- [《杀戮尖塔 1》蛊真人 / Reverend Insanity Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3701087103)
- STS2-RitsuLib
- 《杀戮尖塔》Mod 社区

## 免责声明

本项目是非官方同人 Mod，与 Mega Crit 及相关作品权利方不存在隶属、授权或商业合作关系。相关游戏、角色、名称与素材的权利归各自权利方所有。

---

# English Overview

**GuZhenRen** is an unofficial character mod for *Slay the Spire 2*. It adds **Fang Yuan** as a playable character and introduces a progression system built around Gu worms, Aperture cultivation, primeval essence, immortal essence, Gu refinement, fusion recipes, and killer-move derivation.

## Highlights

- A separate Gu storage pile and Gu recovery pile
- Rank 1–9 progression for Fang Yuan and Gu worms
- Primeval essence, immortal essence, radiance, and blood essence resources
- Campfire upgrading and Gu fusion
- Killer-move derivation during combat
- Light Path, Earth Path, and early Blood Path gameplay
- Simplified Chinese and English localization
- Basic multiplayer and save-state compatibility

The current source implements 10 Gu worms and 3 killer moves. Light Path is the most complete archetype, featuring Refraction, Radiance, Exposure, empowered activations, and recovery-generated cards.

## Current Requirements

| Item | Version |
| --- | --- |
| Mod | 0.4.14 |
| Slay the Spire 2 | 0.110.0 |
| STS2-RitsuLib | 0.5.10 or compatible |
| Build stack | Godot 4.5.1 .NET / .NET 9 |

## Development Status

The mod is under active development. Card content, balance, visuals, multiplayer behavior, and compatibility may change. Rank 9 is currently the maximum implemented cultivation level; tribulations and Rank 10 content are not yet available.

Bug reports and contributions are welcome. Please include the game version, mod version, RitsuLib version, enabled mods, logs, and reproduction steps whenever possible.
