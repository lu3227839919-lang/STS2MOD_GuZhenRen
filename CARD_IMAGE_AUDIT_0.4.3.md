# 0.4.3 卡牌图片审计

> 图片命名规则：每张具体卡牌使用与 C# 类型同名的 PNG。
> 本地目录：`E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images`
> Godot 路径：`res://GuZhenRen/images/{CardTypeName}.png`

当前源码共识别 **51 张具体卡牌类型**。上传源码包中存在图片 **0 张**，缺失 **51 张**。

缺图不会被静默替换成其他卡牌图片；模组启动时会逐张输出 `[卡图缺失]` 警告。

## 基础蛊、操作牌与衍生牌

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 催动 | `ChuiDong` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\ChuiDong.png` | ⚠️ 缺少图片 |
| 定光蛊 | `DingGuangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\DingGuangGu.png` | ⚠️ 缺少图片 |
| 防御 | `GuZhenRenDefend` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\GuZhenRenDefend.png` | ⚠️ 缺少图片 |
| 冲拳 | `GuZhenRenStrike` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\GuZhenRenStrike.png` | ⚠️ 缺少图片 |
| 定光符 | `DingGuangFu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\DingGuangFu.png` | ⚠️ 缺少图片 |
| 返照 | `FanZhao` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\FanZhao.png` | ⚠️ 缺少图片 |
| 光标 | `GuangBiao` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\GuangBiao.png` | ⚠️ 缺少图片 |
| 光镜 | `GuangJing` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\GuangJing.png` | ⚠️ 缺少图片 |
| 明镜 | `MingJing` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\MingJing.png` | ⚠️ 缺少图片 |
| 日晕 | `RiYun` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\RiYun.png` | ⚠️ 缺少图片 |
| 极光 | `JiGuang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JiGuang.png` | ⚠️ 缺少图片 |
| 聚光 | `JuGuang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JuGuang.png` | ⚠️ 缺少图片 |
| 微光 | `WeiGuang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\WeiGuang.png` | ⚠️ 缺少图片 |
| 余辉 | `YuHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YuHui.png` | ⚠️ 缺少图片 |
| 镜光蛊 | `JingGuangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JingGuangGu.png` | ⚠️ 缺少图片 |
| 小光蛊 | `XiaoGuangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\XiaoGuangGu.png` | ⚠️ 缺少图片 |
| 玉皮蛊 | `YuPiGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YuPiGu.png` | ⚠️ 缺少图片 |
| 琉璃玉衣 | `LiuLiYuYi` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\LiuLiYuYi.png` | ⚠️ 缺少图片 |
| 玉光衣 | `YuGuangYi` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YuGuangYi.png` | ⚠️ 缺少图片 |
| 玉膜 | `YuMo` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YuMo.png` | ⚠️ 缺少图片 |
| 折光 | `ZheGuang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\ZheGuang.png` | ⚠️ 缺少图片 |
| 月光蛊 | `YueGuangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YueGuangGu.png` | ⚠️ 缺少图片 |
| 残月 | `CanYue` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\CanYue.png` | ⚠️ 缺少图片 |
| 满月刃 | `ManYueRen` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\ManYueRen.png` | ⚠️ 缺少图片 |
| 月刃 | `YueRen` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YueRen.png` | ⚠️ 缺少图片 |

## 隐藏选择牌

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 选择目标 | `EnemyTargetChoice` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\EnemyTargetChoice.png` | ⚠️ 缺少图片 |
| 保留光辉 | `SaveGuangHuiChoice` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\SaveGuangHuiChoice.png` | ⚠️ 缺少图片 |
| 消耗光辉 | `SpendGuangHuiChoice` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\SpendGuangHuiChoice.png` | ⚠️ 缺少图片 |

## 合练蛊与衍生牌

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 镜辉蛊 | `JingHuiGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JingHuiGu.png` | ⚠️ 缺少图片 |
| 返辉 | `FanHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\FanHui.png` | ⚠️ 缺少图片 |
| 镜辉 | `JingHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JingHui.png` | ⚠️ 缺少图片 |
| 凝镜辉 | `NingJingHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\NingJingHui.png` | ⚠️ 缺少图片 |
| 周天镜辉 | `ZhouTianJingHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\ZhouTianJingHui.png` | ⚠️ 缺少图片 |
| 血月蛊 | `XueYueGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\XueYueGu.png` | ⚠️ 缺少图片 |
| 月芒蛊 | `YueMangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YueMangGu.png` | ⚠️ 缺少图片 |
| 残芒 | `CanMang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\CanMang.png` | ⚠️ 缺少图片 |
| 凝月芒 | `NingYueMang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\NingYueMang.png` | ⚠️ 缺少图片 |
| 天月芒 | `TianYueMang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\TianYueMang.png` | ⚠️ 缺少图片 |
| 月芒 | `YueMang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YueMang.png` | ⚠️ 缺少图片 |

## 仙元牌

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 白荔仙元 | `BaiLiXianYuan` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\BaiLiXianYuan.png` | ⚠️ 缺少图片 |
| 红枣仙元 | `HongZaoXianYuan` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\HongZaoXianYuan.png` | ⚠️ 缺少图片 |
| 黄杏仙元 | `HuangXingXianYuan` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\HuangXingXianYuan.png` | ⚠️ 缺少图片 |
| 青提仙元 | `QingTiXianYuan` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\QingTiXianYuan.png` | ⚠️ 缺少图片 |

## 杀招

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 月霓裳 | `YueNiChang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\YueNiChang.png` | ⚠️ 缺少图片 |

## 血道

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 血气蛊 | `XueQiGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\XueQiGu.png` | ⚠️ 缺少图片 |

## 0.4.3 新增光道牌

| 卡牌 | C# 类型 | 图片路径 | 状态 |
|---|---|---|---|
| 流光蛊 | `LiuGuangGu` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\LiuGuangGu.png` | ⚠️ 缺少图片 |
| 流光 | `LiuGuang` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\LiuGuang.png` | ⚠️ 缺少图片 |
| 流辉 | `LiuHui` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\LiuHui.png` | ⚠️ 缺少图片 |
| 白虹 | `BaiHong` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\BaiHong.png` | ⚠️ 缺少图片 |
| 白虹贯日 | `BaiHongGuanRi` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\BaiHongGuanRi.png` | ⚠️ 缺少图片 |
| 镜月返照 | `JingYueFanZhao` | `E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\JingYueFanZhao.png` | ⚠️ 缺少图片 |
