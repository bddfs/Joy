# atlas v0.1 原始生成记录

- 日期：2026-09-24。
- 模式：内置 `image_gen.imagegen`，参考图引导的新资产生成，首轮候选。
- 参考：`output/art/joy-art-board-v0.1.png`，生成前已查看，仅参考风格。
- 原始生成文件名：`exec-317664a9-2e94-45d6-b052-4b3b69ff3b0b.png`。
- 已选源文件/导出文件：`Assets/Art/subjects-atlas-v01.png`（原样复制，无栅格后处理）。
- 实际画布：1254 × 1254，RGB，无 Alpha。
- 模型及版本：工具未披露；种子：工具未披露。
- 评审状态：已选用于候选实现，未获团队批准。
- 权利与验收范围：见 `docs/art/assets.json`，不把生成结果视为完整 AT 验收。

## 原始 prompt（完整）

```text
Create a NEW production game asset atlas, using the attached board ONLY as palette, realistic material and atmosphere reference. Square 2048 x 2048 if possible. STRICT FOUR COLUMNS BY FOUR ROWS, sixteen equal square cells, arranged perfectly on a regular grid. No cell dividers, no labels, no frames, NO TEXT anywhere. A uniform flat dark midnight #080C15 backdrop across the entire sheet; NOT checkerboard. Each subject must fit entirely inside its own cell with 15% internal empty margin and NOT touch adjacent cells. The same low saturation semi-realistic cinematic painted style, warm amber highlights, slate shadows, believable wood/metal/glass materials throughout. Read cells LEFT TO RIGHT, TOP TO BOTTOM:
Row 1: (1) headquarters: a compact professional office building in dark brick, modest warm reception windows, no lettering; (2) hotel: an older tall narrow urban hotel with symmetrical façade and subdued windows, no signs; (3) north junction: a compact elevated highway interchange/concrete road crossing miniature, without cars or traffic labels; (4) hospital: clean contemporary pale concrete medical building, no cross logo or writing.
Row 2: (1) source: an old fenced industrial pumping facility with one cylindrical tank and angular utility building; (2) south junction: a low pedestrian road junction/underpass miniature, distinct from north junction; (3) cooperation site: an ordinary small commercial workshop with warm bay window, no signs; (4) EMPTY DARK CELL.
These seven buildings are detached three-quarter isometric architectural thumbnails, same scale and same camera/lighting, no circular borders or badges.
Row 3: (1) investigation card illustration: a realistic brass magnifying glass above two blank cream document sheets on dark wood, no writing; (2) lighting card illustration: a handsome brass desk lamp with cream shade casting a warm cone onto a dark tabletop; (3) mimic card illustration: an anonymous short-haired adult seen from behind, studying their own ordinary mirrored silhouette, fully clothed dark investigator coat, no ghost, monster, glowing eyes or special clue; (4) EMPTY DARK CELL.
Row 4: (1) item clock: one small antique mahogany mantel clock with a brass face and two hands, no numerals or marks or words, isolated at three-quarter view; (2) item lamp: exactly the same brass desk lamp design with cream shade as the lighting card, but unlit and isolated, full object including base visible; (3) sealing kit: one weathered dark portable professional hard-case open with two plain metal canisters, simple clasps and tools, no hazard icon, numbers or writing; (4) EMPTY DARK CELL.
Each square is one usable standalone illustration, architectural images separated by dark padding, item/card subjects large enough to remain recognizable at thumbnail size. No ornamental UI, status glows, state rings, badges, stars, rules, inventory quantities, typography or watermark. Do NOT turn this into a reference board with lettering. Preserve strict 4 x 4 equal cell layout.
```

## 编辑/迭代记录

本轮无二次生成、编辑或图片处理。请求2048×2048，工具实际返回1254×1254；Godot按照实际4×4画布以313.5×313.5区域取样，保持原图不变。

