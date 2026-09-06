# UI 素材与换肤架构 v1（可分离性 / 一次性换皮）

> 目标（冬瓜条硬性要求）：**以后找到更好的 UI，能一次性替换、尽量不动代码。**
> 本文＝换肤怎么保证 + 每个"素材槽"的名字/路径/尺寸/默认染色/免费升级来源（采购单）。
> 配套：`icons/`（22 个起步占位 SVG）、`icons/_contact_sheet.html`（对照图）。

---

## 一、换肤架构（可分离性怎么落地 · 交 B 在 Godot 建）

**一句话规矩：一次性换 UI ＝ 换 `theme.tres` + 换 `ui/` 素材文件夹，代码不动。**

| 机制 | 做法 | 保证的事 |
|---|---|---|
| **Godot Theme 资源** `theme.tres` | 所有 StyleBox（面板/卡/按钮/血条）、字体、颜色集中在一个 Theme；组件用 `theme_type_variation` 取样式，不写内联样式 | 换 Theme＝整套换肤，0 代码 |
| **素材槽（命名引用）** | 图标/边框/立绘全走**稳定路径**（见下表），不硬编码文件名；组件读 `slot 名` | 换文件即换皮 |
| **单色 + 染色** | 图标单色（`currentColor`/白），运行时按状态用 `modulate`/Theme 色染 | 一份资产供所有状态色，不复制图 |
| **9-slice**（NinePatchRect） | 边框/面板/按钮底用九宫格 | 任意尺寸不失真、换边框只换图 |
| **集中布局常量** `BattleLayout` | formation 锚点、HUD 坐标、卡牌尺寸/扇形参数统一常量 | 重排不牵连逻辑 |
| **数据驱动组件** | `card_view/combatant_view/...` 只读数据+Theme | 逻辑与皮肤零耦合 |

> 关键：**逻辑层信号 → 组件 → Theme/素材** 三段解耦。换皮只动最后一段。

---

## 二、目录与命名约定（建议）

```
res://ui/
  theme.tres                 # 全局皮肤单一来源
  icons/{slot}.svg           # 见下表（如 icons/status_burn.svg）
  frames/{slot}.png(.import) # 9-slice 边框/面板/按钮底
res://art/
  portraits/{id}.png         # 立绘槽（pyra/icey/enemy…；画师成品原地替换）
  bg/{scene}.png             # 战斗背景（winterfort…）
res://fonts/{name}           # 可商用字体
```

---

## 三、素材槽清单（manifest ＝ 采购单）

### 3.1 图标（已给占位 · `ui/icons/*.svg` · 单色可染）
| slot | 用途 | 默认染色 token | 免费升级来源（找更好图时） |
|---|---|---|---|
| `status_burn` | 灼伤 | `color/status/burn` | game-icons.net：flame |
| `status_poison` | 中毒 | `color/status/poison` | poison-bottle / drop |
| `status_shield` | 护盾 | `color/status/shield` | shield |
| `status_power` | 力量 | `color/status/power` | biceps / muscle-up |
| `status_regen` | 再生 | `#8fe0b0` | health-normal / hearts |
| `status_weak` | 虚弱 | `#8fa0b5` | broken-shield / down |
| `status_freeze` | 冻结 | `#bfe8ff` | ice-cube / frozen |
| `status_mark` | 印记 | `#b98be0` | rune-stone / eye |
| `intent_attack` | 攻击意图 | `color/enemy` | broadsword / sword |
| `intent_defend` | 防御意图 | `color/status/shield` | shield |
| `intent_buff` | 增益意图 | `#7fe0a0` | upgrade / chevron |
| `intent_debuff` | 减益意图 | `#e07a7a` | skull / down |
| `intent_special` | 特殊意图 | `color/gold/hi` | sparkles / star |
| `pile_draw` | 抽牌堆 | `color/parch` | card-draw |
| `pile_discard` | 弃牌堆 | `color/parch` | card-burn |
| `pile_exhaust` | 消耗堆 | `#b18bd6` | card-exchange / fire |
| `energy_bolt` | 能量 | `color/energy` | lightning / crystal |
| `combo_chain` | 连击 | `color/gold/hi` | chain / linked-rings |
| `btn_endturn` | 结束回合 | `color/parch` | 自绘/箭头 |
| `btn_settings` | 设置 | `color/parch` | cog |
| `cost_gem` | 费用宝石 | `color/energy` | crystal / gem |
| `target_ring` | 目标环 | `color/enemy` | targeting / crosshair |

### 3.2 边框 / 9-slice（暂用 Godot StyleBox 程序化，后可换图）
| slot | 用途 | 现状 | 升级来源 |
|---|---|---|---|
| `frame/panel` | HUD 面板底（描金） | StyleBoxFlat（深底+金边+角标） | Kenney UI Pack (CC0) / 画师九宫格 |
| `frame/card` | 卡面底 | StyleBoxFlat | 同上 |
| `frame/button` | 按钮底（常态/悬停/按下） | StyleBoxFlat×3 | 同上 |
| `bar/hp_*` | 血条底/填充 | StyleBoxFlat | 同上 |
| `orb/energy` | 能量球 | CSS/StyleBox+shader | 画师帧 |

### 3.3 立绘 / 背景（贴图槽 · 画师成品原地替换）
| slot | 现占位 | 说明 |
|---|---|---|
| `portraits/spirit_front` | pyra-2 | 1 号位立绘；**画师成品直接替换同名槽** |
| `portraits/spirit_rear` | icey | 2 号位 |
| `portraits/enemy_{n}` | enemy-1 | 敌人 |
| `bg/battle` | winterfort-2 | 战斗背景 |

> 立绘走真人画师 → 只要求**统一朝向（我方 facing right、敌 facing left）、透明底、脚底对齐**，交付后丢进 `portraits/` 同名槽即用，UI 不改。

---

## 四、升级来源（免费 / 可商用 · 采购单）
- **game-icons.net** —— ~4000 奇幻图标，**CC-BY 3.0**（挂一行署名即可商用）。状态/意图/关键词图标首选。
- **Kenney.nl** —— **CC0**（无需署名），UI 面板/按钮/九宫格边框首选。
- **itch.io / OpenGameArt** —— 免费/付费 UI kit，注意逐个看授权。
- **字体**：思源宋体 Source Han Serif（OFL）、阿里巴巴普惠体（免费商用）。
- **像素质感升级**：Retro Diffusion / PixelLab（要更贴 HD-2D 像素时重出成套）。

合规：AI 生成素材上 Steam 需**如实披露**；Gemini 图 SynthID 水印**别删**；CC-BY 记得署名、CC0 随意。

---

## 五、下一步
1. 起步图标风格是否 OK（单色描线/填充混合）？要更"厚"还是更"细"？
2. 确认后我：补齐遗漏槽 + 打包成 `ui/icons/` 结构；边框/9-slice 给 B 的 StyleBox 规格。
3. 或你想直接用 game-icons.net 的成品替换某些槽，告诉我哪些，我给精确图标名。
