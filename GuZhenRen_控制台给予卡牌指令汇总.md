# 《蛊真人》Mod 控制台给予卡牌指令汇总

适用范围：当前已注册的《蛊真人》Mod 卡牌。

## 1. 基本语法

### 给予一张卡牌

```text
card <卡牌ID>
```

### 指定转数

两种写法均可：

```text
card <卡牌ID> <转数>
card <卡牌ID> rank=<转数>
```

例如：

```text
card GU_ZHEN_REN_CARD_XIAO_GUANG_GU 9
card GU_ZHEN_REN_CARD_XIAO_GUANG_GU rank=9
```

### 自动放置规则

- 非战斗场景：加入永久牌组。
- 战斗中给予蛊虫：进入蛊恢复牌堆，并开始计算恢复时间。
- 战斗中给予普通牌、仙元牌、杀招或衍生牌：进入普通手牌。
- 转数超过卡牌允许范围时，会自动修正。
- 血气蛊最高五转。
- 血月蛊最高七转。
- 其余当前蛊虫最高九转。

> 为了让蛊虫在下一场战斗中以完整状态出现，建议在地图、事件房间或其他非战斗场景执行给予指令。

---

## 2. 基础牌

### 催动

```text
card GU_ZHEN_REN_CARD_CHUI_DONG
```

### 冲拳

```text
card GU_ZHEN_REN_CARD_GU_ZHEN_REN_STRIKE
```

### 防御

```text
card GU_ZHEN_REN_CARD_GU_ZHEN_REN_DEFEND
```

---

## 3. 蛊虫牌

蛊虫推荐使用 `rank=` 明确指定转数。

### 小光蛊

```text
card GU_ZHEN_REN_CARD_XIAO_GUANG_GU rank=9
```

### 月光蛊

```text
card GU_ZHEN_REN_CARD_YUE_GUANG_GU rank=9
```

### 镜光蛊

```text
card GU_ZHEN_REN_CARD_JING_GUANG_GU rank=9
```

### 定光蛊

```text
card GU_ZHEN_REN_CARD_DING_GUANG_GU rank=9
```

### 流光蛊

```text
card GU_ZHEN_REN_CARD_LIU_GUANG_GU rank=9
```

### 月芒蛊

```text
card GU_ZHEN_REN_CARD_YUE_MANG_GU rank=9
```

### 镜辉蛊

```text
card GU_ZHEN_REN_CARD_JING_HUI_GU rank=9
```

### 玉皮蛊

```text
card GU_ZHEN_REN_CARD_YU_PI_GU rank=9
```

### 血气蛊

最高五转：

```text
card GU_ZHEN_REN_CARD_XUE_QI_GU rank=5
```

### 血月蛊

最高七转：

```text
card GU_ZHEN_REN_CARD_XUE_YUE_GU rank=7
```

---

## 4. 仙元牌

### 青提仙元

```text
card GU_ZHEN_REN_CARD_QING_TI_XIAN_YUAN
```

### 红枣仙元

```text
card GU_ZHEN_REN_CARD_HONG_ZAO_XIAN_YUAN
```

### 白荔仙元

```text
card GU_ZHEN_REN_CARD_BAI_LI_XIAN_YUAN
```

### 黄杏仙元

```text
card GU_ZHEN_REN_CARD_HUANG_XING_XIAN_YUAN
```

---

## 5. 仙道杀招

杀招也可以附带 `rank=`，用于测试不同转数的动态数值。

### 月霓裳

```text
card GU_ZHEN_REN_CARD_YUE_NI_CHANG rank=9
```

### 白虹贯日

```text
card GU_ZHEN_REN_CARD_BAI_HONG_GUAN_RI rank=9
```

### 镜月返照

```text
card GU_ZHEN_REN_CARD_JING_YUE_FAN_ZHAO rank=9
```

---

## 6. 小光蛊衍生牌

### 微光

```text
card GU_ZHEN_REN_CARD_WEI_GUANG
```

### 聚光

```text
card GU_ZHEN_REN_CARD_JU_GUANG rank=8
```

### 余辉

```text
card GU_ZHEN_REN_CARD_YU_HUI
```

### 极光

```text
card GU_ZHEN_REN_CARD_JI_GUANG
```

---

## 7. 月光蛊衍生牌

### 月刃

```text
card GU_ZHEN_REN_CARD_YUE_REN rank=7
```

### 残月

```text
card GU_ZHEN_REN_CARD_CAN_YUE
```

### 满月刃

```text
card GU_ZHEN_REN_CARD_MAN_YUE_REN rank=9
```

---

## 8. 镜光蛊衍生牌

### 光镜

```text
card GU_ZHEN_REN_CARD_GUANG_JING rank=7
```

### 返照

```text
card GU_ZHEN_REN_CARD_FAN_ZHAO
```

### 明镜

```text
card GU_ZHEN_REN_CARD_MING_JING rank=9
```

---

## 9. 定光蛊衍生牌

### 定光符

```text
card GU_ZHEN_REN_CARD_DING_GUANG_FU rank=7
```

### 光标

```text
card GU_ZHEN_REN_CARD_GUANG_BIAO
```

### 日晕

```text
card GU_ZHEN_REN_CARD_RI_YUN rank=9
```

---

## 10. 流光蛊衍生牌

### 流光

```text
card GU_ZHEN_REN_CARD_LIU_GUANG rank=7
```

### 流辉

```text
card GU_ZHEN_REN_CARD_LIU_HUI
```

### 白虹

```text
card GU_ZHEN_REN_CARD_BAI_HONG rank=9
```

---

## 11. 月芒蛊衍生牌

### 月芒

```text
card GU_ZHEN_REN_CARD_YUE_MANG
```

### 凝月芒

```text
card GU_ZHEN_REN_CARD_NING_YUE_MANG rank=7
```

### 残芒

```text
card GU_ZHEN_REN_CARD_CAN_MANG
```

### 天月芒

```text
card GU_ZHEN_REN_CARD_TIAN_YUE_MANG rank=9
```

---

## 12. 镜辉蛊衍生牌

### 镜辉

```text
card GU_ZHEN_REN_CARD_JING_HUI
```

### 凝镜辉

```text
card GU_ZHEN_REN_CARD_NING_JING_HUI rank=7
```

### 返辉

```text
card GU_ZHEN_REN_CARD_FAN_HUI
```

### 周天镜辉

```text
card GU_ZHEN_REN_CARD_ZHOU_TIAN_JING_HUI rank=9
```

---

## 13. 玉皮蛊衍生牌

### 玉膜

```text
card GU_ZHEN_REN_CARD_YU_MO
```

### 玉光衣

```text
card GU_ZHEN_REN_CARD_YU_GUANG_YI rank=8
```

### 折光

```text
card GU_ZHEN_REN_CARD_ZHE_GUANG
```

### 琉璃玉衣

```text
card GU_ZHEN_REN_CARD_LIU_LI_YU_YI rank=9
```

---

## 14. 常用测试组合

### 获得一套九转光道蛊虫

```text
card GU_ZHEN_REN_CARD_XIAO_GUANG_GU rank=9
card GU_ZHEN_REN_CARD_YUE_GUANG_GU rank=9
card GU_ZHEN_REN_CARD_JING_GUANG_GU rank=9
card GU_ZHEN_REN_CARD_DING_GUANG_GU rank=9
card GU_ZHEN_REN_CARD_LIU_GUANG_GU rank=9
card GU_ZHEN_REN_CARD_YUE_MANG_GU rank=9
card GU_ZHEN_REN_CARD_JING_HUI_GU rank=9
```

### 获得全部仙元牌

```text
card GU_ZHEN_REN_CARD_QING_TI_XIAN_YUAN
card GU_ZHEN_REN_CARD_HONG_ZAO_XIAN_YUAN
card GU_ZHEN_REN_CARD_BAI_LI_XIAN_YUAN
card GU_ZHEN_REN_CARD_HUANG_XING_XIAN_YUAN
```

### 获得全部仙道杀招

```text
card GU_ZHEN_REN_CARD_YUE_NI_CHANG rank=9
card GU_ZHEN_REN_CARD_BAI_HONG_GUAN_RI rank=9
card GU_ZHEN_REN_CARD_JING_YUE_FAN_ZHAO rank=9
```

### 获得血道现有蛊虫

```text
card GU_ZHEN_REN_CARD_XUE_QI_GU rank=5
card GU_ZHEN_REN_CARD_XUE_YUE_GU rank=7
```

---

## 15. 排错

成功后日志通常会显示：

```text
控制台给予 CARD.GU_ZHEN_REN_CARD_XIAO_GUANG_GU：转数 9，自动目标牌堆 Deck。
```

进入战斗后，蛊虫会被移动到专属蛊牌区域。

若高转蛊虫进入战斗后显示错误转数，检查日志中的：

```text
[蛊虫转数]
```

以及确认使用的是已包含高转战斗实例同步修复的版本。
