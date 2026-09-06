> ⚠️ **本文件含已作废内容** —— 先读 `_作废设计清单_v1.md`
>
> 本文件中已失效的部分：
>
> - **prompt 里引用的精灵机制描述**（嘲讽 / 护崽等）已作废
>
> **出图 prompt 本身、美术风格描述、形象设定全部有效**，可直接使用。

---

# 火系出图 Prompt v2 —— 干净版（10 只 · tag 型 · 赛璐璐动漫立绘）

> 针对上一版"杂乱/线多/脏/AI味重"重写。工具面向 **SDXL 动漫模型 / NovelAI 这类 tag 型**（若你实际用 Midjourney，说一声我换成 --niji 自然语言+参数版）。
> 核心思路：**一个招牌火元素 + 两三色配色 + 干净背景 + 强负面词**，把颗粒/尘灰/像素糊全挡掉。**角色设定图先出干净立绘，像素化留作后期一步**（不影响 HD-2D 最终观感）。

---

## 〇、为什么之前脏（五条修正原则）

1. **一张图只留一个火元素**：之前每只堆了火星+火纹+烙痕+拖尾+护盾光…模型全渲成碎线杂点。现在每只只保留 **1 个**招牌火（如炎舞=一条焰绸、炎刑=刃上一道刑火）。
2. **限色 2–3 色**：明确写死配色，别让模型自由发挥成一团糊。
3. **干净背景 + 强剪影**：`simple dark background, high contrast, bold silhouette`——不写背景，模型就用杂乱噪点填满。
4. **负面词挡颗粒/尘灰/像素**：`sparks, embers everywhere, soot, ash smudges, film grain, noise, pixel art`——这些正是"脏/AI味"的来源。
5. **用动漫专精模型 + 低 CFG**：这是**最关键**的一条。底模用干净动漫大模型（下方推荐），CFG 压到 5–6，"deep fried / 过曝油腻"直接少一大半。通用底模（裸 SDXL / SD1.5）几乎必出 AI 泔水感。

---

## 一、通用模板（每只都套这个壳）

**① 质量前缀（放最前）**
```
masterpiece, best quality, absurdres, very aesthetic, official art,
clean lineart, cel shading, flat color, limited palette,
```
> Pony 系底模改用分数前缀：`score_9, score_8_up, score_7_up, source_anime,` 接在最前。
> NovelAI 用其自带质量预设（`best quality, amazing quality, very aesthetic`）即可，tag 写法通用。

**② 中间＝各角色 subject tags（见第二节，每只一段）**

**③ 收尾框架（放最后）**
```
solo, full body, simple dark background, high contrast, bold silhouette
```

**④ 负面词（Negative，所有角色通用）**
```
lowres, (worst quality:1.2), low quality, bad anatomy, bad hands, extra digits, extra limbs,
cluttered, busy background, cluttered background, too many details, overdetailed,
floating particles, sparks, embers everywhere, soot, ash smudges,
film grain, noise, jpeg artifacts, muddy colors, oversaturated, tangled messy lines, deep fried,
blurry, watermark, signature, text, pixel art, 3d, photorealistic
```

**⑤ 出图参数建议（SDXL）**
- 底模：**Animagine XL 4.0 / Illustrious 系 / Pony(带分数tag)** 任一（都是干净动漫向）。NovelAI 直接用 NAI Diffusion Anime V3。
- 尺寸 **832×1216**（竖构图）；采样 **DPM++ 2M Karras**，步数 **28–30**，**CFG 5–6**（低一点更干净）。
- 开 **Hires.fix** 1.5×、denoise 0.35–0.4 → 线更利落。
- 立绘要透明底/纯底：负面加 `background` 或提示 `white background / plain background`，后期好抠。

---

## 二、10 只 · subject tags（逐只 · 已精简到一个火元素）

> 用法：`质量前缀 + 下面这段 + 收尾框架`，配通用负面词。

### 01 炽芯 Pyra —— 灼伤引爆 carry（娇小少女）
```
1girl, petite energetic fire girl, short messy ember-red hair with faintly glowing tips,
amber eyes, a few subtle glowing fire-lines on her bare arms (single fire motif),
light sleeveless battle outfit with wrapped bracers, cocky grin, dynamic pose,
palette: bright orange, gold, small charcoal-black accent
```

### 13 熔垒 Magmawall —— 坦克·嘲讽（构装巨墙）
```
1other, no humans, towering magma guardian golem, body of cracked black volcanic rock
with a few glowing molten seams (single fire motif), broad wall-like silhouette, massive shoulders,
holding one heavy slab shield, stoic, palette: black-grey stone, molten orange, ash white
```

### 14 燔烬 Everember —— 焚毁引擎（缠灰半灵 · 性别可调）
```
1girl, ash priestess wrapped in charred cloth, hood half down,
one phoenix-like ember feather behind her back (single fire motif), calm entranced expression,
palette: ash white, charcoal grey, dark red ember
```

### 15 赤锻 Forgeheart —— 力量爆发玻璃炮（男战士）
```
1boy, lean muscular fire berserker, spiky crimson hair, glowing red eyes, few forge-scars,
wielding one massive warhammer with a red-hot glowing edge (single fire motif),
bare-chested with light iron guards, fierce grin, charging pose,
palette: crimson red, iron grey, white-hot orange
```

### 16 燎野 Wildfire —— 灼伤支援·铺场（游猎少女）
```
1girl, wild fire huntress, athletic medium-tall build, messy orange hair with scorched dark tips,
amber eyes, nomadic huntress outfit with fur trim, holding one burning torch (single fire motif),
confident feral smile, palette: wildfire orange, dark brown, smoke grey
```

### 17 炎舞 Flamewaltz —— 连击·连段（焰之舞者）
```
1girl, elegant fire dancer, slender graceful build, ember-gold hair in a high ponytail, golden eyes,
one flowing ribbon of flame trailing from her hand (single fire motif),
simple red-and-gold dance dress, mid-spin dance pose, graceful smile,
palette: warm orange, deep red, ivory
```

### 18 孤焰 Lonember —— 背水绝境（半兽独狼女）
```
1girl, lone wolf beast-girl, wolf ears and tail, lean wiry build,
dark red hair with ash-grey streaks, pale glowing red eyes, a few bandages,
tattered light armor and ragged cloak, one surge of flame at her side (single fire motif),
defiant feral expression, palette: dark red, ash black, pale white flame
```

### 19 炎刑 Pyrejudge —— 处决·斩杀（冷面女行刑者）
```
1girl, cold female executioner, tall slender, dark red hair tied neatly, cold crimson eyes,
black hooded executioner robe over dark half-armor,
holding one large branding blade with a glowing hot edge (single fire motif),
stern expression, upright still pose, palette: cold crimson, iron black, white-hot edge
```

### 20 燃枢 Kindler —— 资源·节奏支援（女炉工）
```
1girl, hearty female forge-stoker, sturdy athletic build,
forge-orange short hair under a headscarf, warm eyes,
blacksmith leather apron with arm guards, holding one bellows tool (single fire motif),
hearty confident grin, palette: forge orange, iron grey, coal black
```

### 21 煨煨 Hearthcub —— 护崽·第二坦（可爱火熊）
```
no humans, 1other, cute chubby fire bear cub, round plump body, soft glowing ember fur,
warm hearth-glow belly (single fire motif), big innocent eyes, small flame tuft on head,
sitting pose, adorable kawaii, palette: warm orange, cream, ember red
```
> 想要"炸毛暴走"版：加 `fur standing up, angry, heat haze around body`，但那版天生更乱，日常立绘用上面这版更干净。

---

## 三、后期像素化（HD-2D 观感这步单独做）

> 干净立绘出好后，再走像素化，**不要**在生成阶段写 pixel art：
- Aseprite / Photoshop 手动降采样重绘（最好但费工）；
- 或 SD 里用像素 LoRA（如 `pixel art` LoRA）低权重二次转，配限色板；
- 或程序化：`Pixelator` / `ImageMagick -posterize + -scale` 降到目标分辨率再限色（16–32 色板）。
- 关键：**限色板**（每只 2–3 主色 + 少量阶）是 HD-2D 干净感的来源，别用全色域。

---

## 四、待定 / 下一步

1. **先试跑 1–2 只**（建议炎舞/炽芯，主体最干净）验证新配方，再据实测微调（如某只还乱，就再砍一个元素或降 CFG）。
2. 底模没定的话我可以帮你定一版（Animagine / Illustrious / Pony / NAI 各有脾气）。
3. 参考图 / 姿势控制（ControlNet openpose）想上的话，我给每只配姿势关键词。
4. 名字/世界观仍占位，出图不受影响。
