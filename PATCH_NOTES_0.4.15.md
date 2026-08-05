# GuZhenRen 0.4.15

## DARV / Dusty Tome 修复

- 不再仅吞掉 `DustyTome.SetupForPlayer` 的空引用异常。
- 在方源进入 DARV 时，直接将 `DustyTome.AncientCard` 设置为镜月返照。
- 在领取遗物前再次校验卡牌 ID，兼容事件恢复、QuickSL 和其他 Mod 的补丁顺序。
- Dusty Tome 成功领取后，将新加入的镜月返照初始化为九转；原版仍负责将其升级。
- 其他角色继续使用原版或 BaseLib 的 Dusty Tome 逻辑。

## 控制台提示

重复执行 `ancient DARV` 前应先完成或退出当前事件；否则游戏会提示前一个事件尚未结束。
