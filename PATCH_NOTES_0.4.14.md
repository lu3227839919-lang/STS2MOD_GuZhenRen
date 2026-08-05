# GuZhenRen 0.4.14 更新说明

## 先古遗民兼容

- 为 `DustyTome.SetupForPlayer(Player)` 添加仅针对蛊真人角色的 Harmony finalizer。
- 只吞掉 `NullReferenceException`，其他角色和其他异常保持原样，避免隐藏无关故障。
- 该保护用于兼容 BaseLib 的 `ITomeCard` 扩展在自定义角色卡池上产生的空引用。
- 补齐七位先古遗民的中英文角色对话：DARV、OROBAS、PAEL、TANX、TEZCATARA、VAKUU、NONUPEIPE。

## 源码发布清理

- 删除卡图诊断中的“本地路径”输出，只保留 `res://` 资源路径。
- 发布源码包不包含 `local.props`。
- 新增 `tools/Build-SourceArchive.ps1`，自动排除本机配置、构建目录、日志、ZIP 和补丁文件。

## 验证日志

进入 DARV 时，若 BaseLib 扩展仍触发空引用，应看到：

```text
[先古遗民兼容] DustyTome.SetupForPlayer 在蛊真人角色上触发空引用。...已忽略第三方扩展异常...
```

事件应继续生成选项，而不是停留在空事件界面。
