# 卡牌图片目录

每张具体卡牌使用与 C# 类型同名的 PNG 文件。

```text
E:\work\csharp\StS2_Mods\STS2_GuZhenRen\GuZhenRen\images\cards\{CardTypeName}.png
```

Godot 资源路径：

```text
res://GuZhenRen/images/cards/{CardTypeName}.png
```

缺少文件时，`CardImageCatalog` 会保留 `[卡图缺失]` 警告。
完整预期文件名见 `CARD_IMAGE_NAMES.txt`。
