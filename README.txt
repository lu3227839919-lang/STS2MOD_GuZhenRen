V2 编译修正版
=============
修复 CS0200：RestSiteOption.IsEnabled 在当前 0.107.1 兼容 API 中是只读属性。

实现方式
========
1. 不再直接给 IsEnabled 赋值。
2. OnSelect 中继续强制检查次数，确保升炼、合炼每名玩家每局各最多成功 2 次。
3. 新增 GuRestSiteOptionEnabledPatch.cs：
   使用 Harmony 修改 IsEnabled getter 的返回结果，次数耗尽后按钮仍会置灰。
4. 取消选择、配方不匹配或执行失败不扣次数。
5. 次数通过 RitsuLib PlayerRunSavedData 保存。

安装
====
将压缩包内 GuZhenRenCode 覆盖项目中的同名目录，然后执行：

dotnet clean
Remove-Item .\bin, .\obj -Recurse -Force -ErrorAction SilentlyContinue
dotnet build
