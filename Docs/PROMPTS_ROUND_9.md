# Zoo Defense — Asset đợt 9: 6 quái gửi mới, 6 quái thường mới, 4 icon phép

Thả file đúng tên vào `Assets/ZooDefense/Resources/ZooArt/…` là game dùng ngay. Thiếu file thì game vẫn chạy và vẽ hình tạm.
Khi gen ảnh, nhớ **đính kèm 2 ảnh mẫu nét vẽ**: `Enemies/slime.png` và `Enemies/send_golem.png` (cho quái), `UI/spell_freeze.png` (cho phép).

Có ảnh quái là mình làm luôn cơ chế, chỉ số và tên 8 ngôn ngữ, rồi cân bằng bằng bot. Quái gửi **không gacha**: tất cả mở sẵn, chỉ thêm lựa chọn.

| Loại | File | Thư mục | Kích thước |
|---|---|---|---|
| Ảnh đứng | `{id}.png` | `ZooArt/Enemies/` | 560 × 560 |
| Sheet đi bộ (tuỳ chọn) | `{id}.png` | `ZooArt/EnemyWalks/` | 1024 × 1024, 4 × 4 khung |
| Icon phép | `spell_{id}.png` | `ZooArt/UI/` | 512 × 512 |

**Cách gen nhanh:** gen 1 sheet 3 cột × 2 hàng cho mỗi nhóm 6 quái, mình sẽ tự cắt. Hoặc gen từng con một.

---

## A. Khối STYLE chung cho quái (dán đầu mọi prompt quái)

```text
ZOO DEFENSE — monster sprite. Match the attached Zoo Defense monster art EXACTLY: cute-goofy chibi cartoon monster for a kids-friendly mobile tower defense, big head, huge round eyes, small body, thick dark-brown #3B2412 outline (6-8 px at 560 px), flat cel shading with one shadow tone and one soft highlight, saturated candy colours, clean sticker silhouette readable at 80 px. 3/4 view walking to the RIGHT, whole body visible, feet near the bottom of the frame. Mischievous but never scary or gory.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no ground, no shadow, no frame, no floating particles away from the body.
```

**Sheet 6 con (nếu gen một lần):** thêm dòng này.
```text
One image 1680x1120, 3 COLUMNS x 2 ROWS, SIX different monsters, each centered in its own 560x560 cell with at least 60 px of transparent margin. Same size and style for all six.
```

**Sheet đi bộ (tuỳ chọn, mỗi con 1 sheet):**
```text
Use the attached monster image as the EXACT character. One image 1024x1024, 4 COLUMNS x 4 ROWS, SIXTEEN frames of a looping walk cycle to the RIGHT, read left-to-right then top-to-bottom. Each frame centered in its own 256x256 cell, feet on the same baseline, same size in every frame. FULL ALPHA TRANSPARENT BACKGROUND, no ground, no shadow.
```

---

## B. 6 quái gửi mới (gửi sang bàn đối thủ)

Thứ tự trong sheet: hàng trên 1–3, hàng dưới 4–6.

| # | id | Tên | Ý tưởng cơ chế | Chống lại |
|---|---|---|---|---|
| 1 | `send_splitter` | Slime Tách Đôi | Chết thì tách thành 2 slime nhỏ (mỗi con 35% máu) | Đội đánh 1 mục tiêu |
| 2 | `send_mole` | Chuột Chũi Đào Hầm | Cứ 4 giây chui xuống đất 1,5 giây, không bị đánh trúng | Đội đánh chậm, đòn mạnh |
| 3 | `send_mushroom` | Nấm Tiên Chữa Lành | Mỗi 2 giây hồi 5% máu cho quái quanh nó | Đội không có sát thương dồn |
| 4 | `send_wisp` | Ma Trơi Sương Mù | Toả sương: quái gần nó tàng hình, đến khi bị soi | Đội không có thú thấy tàng hình |
| 5 | `send_bomb` | Bọ Bom | Chết thì nổ, làm choáng 2 thú gần nhất 1 giây | Đội dồn thú ở đầu đường |
| 6 | `send_pixies` | Bầy Tiên Tí Hon | 6 con nhỏ, rất nhanh, máu ít | Đội chỉ đánh 1 mục tiêu |

**1. `send_splitter` — Slime Tách Đôi**
```text
Subject: a round bouncy TWIN SLIME — one jelly blob that is clearly about to split in two: a soft crease down the middle, two little faces side by side sharing one body, each with its own goofy grin. Bright lime green #8CE04A jelly with a glossy highlight and a few tiny bubbles inside.
```

**2. `send_mole` — Chuột Chũi Đào Hầm**
```text
Subject: a chubby MOLE MINER popping halfway out of a small dirt mound, wearing a tiny yellow miner's helmet with a lamp, big pink nose, huge claws holding a small pickaxe, squinting happy eyes. Brown-grey fur #7A6A5E, dirt clods around the mound edge (touching the body only).
```

**3. `send_mushroom` — Nấm Tiên Chữa Lành**
```text
Subject: a little MUSHROOM SPRITE healer: a walking mushroom with a big pink-and-white spotted cap #FF8FB8, tiny legs, rosy cheeks, holding a small leaf wand with a glowing green plus-shaped sparkle at the tip. Gentle, kind smile.
```

**4. `send_wisp` — Ma Trơi Sương Mù**
```text
Subject: a floating FOG WISP — a soft ghostly flame of pale lavender-grey mist #C9C2E8 with two round sleepy eyes and a sly little smile, wrapped in a curling scarf of fog that trails behind it. Semi-transparent edges but a solid readable silhouette and outline.
```

**5. `send_bomb` — Bọ Bom**
```text
Subject: a round BOMB BEETLE — a chunky beetle whose shiny black-and-red shell looks like a cartoon bomb, a short sparkling fuse sticking out of its back, six stubby legs, wide nervous grin with gritted teeth. Shell #2B2B3A with red #E5484D stripes, bright yellow spark at the fuse tip.
```

**6. `send_pixies` — Bầy Tiên Tí Hon**
```text
Subject: a tiny mischievous PIXIE (one of a swarm): small round body, huge eyes, cheeky tongue-out grin, pointed ears, four little dragonfly wings buzzing, leaving a short glitter trail touching its feet. Mint #7BE3C2 and pink #FF9AD5 colours. Drawn smaller and lighter than the other monsters but filling the same frame.
```

---

## C. 6 quái thường mới (quái đi theo đợt)

Hệ của quái đi theo đợt được game hiện bằng vòng màu dưới chân, nên ảnh **không nên mang màu của hệ nào**. Hãy giữ tông tím / xám như các quái hiện có.

| # | id | Tên | Ý tưởng cơ chế |
|---|---|---|---|
| 1 | `spore_puff` | Nấm Bào Tử | Quái thường, chết thì rải bào tử làm chậm thú đánh nó 1 giây |
| 2 | `dash_lizard` | Thằn Lằn Phóng | Mỗi 3 giây phóng nhanh gấp đôi trong 0,5 giây |
| 3 | `pebble_golem` | Golem Sỏi | Chậm, giáp 20% |
| 4 | `shadow_blob` | Bóng Ma Nhỏ | Cứ 5 giây tàng hình 1 giây |
| 5 | `balloon_bat` | Dơi Bóng Bay | Bay, không bị đẩy lùi hay kéo lại |
| 6 | `shield_beetle` | Bọ Khiên | Mang khiên trước: giảm 50% sát thương diện rộng |

Thêm câu này vào cuối mỗi prompt quái thường:
```text
Keep the palette neutral like the existing Zoo Defense monsters (soft purples, greys, muted browns). Do NOT make it look fire-, water-, grass- or light-themed, because the game shows the element with a coloured ring under its feet.
```

**1. `spore_puff` — Nấm Bào Tử**
```text
Subject: a squat PUFFBALL MUSHROOM creature with a fat round cap full of little pores, puffing a small cloud of spores from the top, tiny stubby feet, sleepy grumpy face.
```

**2. `dash_lizard` — Thằn Lằn Phóng**
```text
Subject: a skinny speedy LIZARD sprinting, leaning forward, long curly tail, tiny goggles pushed up on its head, wide excited grin, legs in a running pose.
```

**3. `pebble_golem` — Golem Sỏi**
```text
Subject: a short round PEBBLE GOLEM made of stacked smooth river stones, a moss tuft on its head, glowing round eyes, heavy stomping pose, determined little frown.
```

**4. `shadow_blob` — Bóng Ma Nhỏ**
```text
Subject: a small wobbly SHADOW BLOB ghost with a wavy bottom edge, two big shiny eyes, a toothy sneaky grin, half of its body fading into soft sparkles (still a solid outline).
```

**5. `balloon_bat` — Dơi Bóng Bay**
```text
Subject: a round chubby BAT that is inflated like a balloon, tiny flapping wings, a little balloon string tied to its foot, surprised O-shaped mouth, floating pose.
```

**6. `shield_beetle` — Bọ Khiên**
```text
Subject: a sturdy BEETLE holding a big round wooden shield in front of it with both front legs, peeking over the top with brave squinting eyes, small horn on its head.
```

---

## D. 4 icon phép mới (đã có trong game, hiện đang dùng hình tạm)

Cắt theo thứ tự: hàng trên `spell_poison_fog`, `spell_star_mark`; hàng dưới `spell_shield_dome`, `spell_lucky_star`. Mỗi ô 512 × 512.

```text
ZOO DEFENSE — SPELL ICON SET. Match the attached Zoo Defense spell icon style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight, saturated friendly colours, cute and readable at 64px.
One image 1024x1024, 2 COLUMNS x 2 ROWS grid, EXACTLY FOUR separate icons, each centered in its own 512x512 cell with at least 80px of transparent gap between icons. Each icon is a single bold object, no frame, no circle background, no badge, because the game puts it on its own coloured round button.
TOP ROW, left to right:
1) POISON FOG — a puffy violet poison cloud #A66BEB with three small bubbling green drops and a tiny cute skull-shaped puff on top (cute, not scary).
2) STAR MARK — a golden five-point star #FFC54A inside a bright target ring, with three small sparkle rays.
BOTTOM ROW, left to right:
3) SHIELD DOME — a glassy cyan dome #5CC8EA like a bubble shield over a tiny paw print, with a white glossy highlight arc.
4) LUCKY STAR — a smiling green-and-gold shooting star #5FD884 with a short sparkly tail and a tiny "+1" arrow shape made of stars (no letters or numbers, just an upward arrow).
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no numbers.
```

---

## E. Huy hiệu hạng C · B · A · S · SS · SSS (thay chữ vẽ bằng code)

**File:** `ZooArt/UI/rank_c.png`, `rank_b.png`, `rank_a.png`, `rank_s.png`, `rank_ss.png`, `rank_sss.png`.
**Kích thước:** mỗi file **512 × 256** (tỉ lệ 2:1), nền trong suốt, chữ nằm giữa.

Có file là game tự dùng: góc thẻ thú (cao 44 px) và banner gacha (cao 56 px). Huy hiệu đứng yên, không còn hiệu ứng phóng to thu nhỏ.

**Cách gen:** gen 1 sheet **2 cột × 3 hàng** (1024 × 768), mình sẽ tự cắt. Thứ tự: hàng 1 `C`, `B`; hàng 2 `A`, `S`; hàng 3 `SS`, `SSS`.

```text
ZOO DEFENSE — RARITY RANK BADGES for a cute mobile game. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight, readable at 40px tall.
One image 1024x768, 2 COLUMNS x 3 ROWS grid, EXACTLY SIX separate badges, each centered in its own 512x256 cell with at least 50px of transparent gap. Each badge is the rank letters only, written in fat bubbly rounded bold letters, slightly tilted upward, each letter with the brown outline, an inner colour gradient and a white glossy highlight on top. The badges get clearly more precious from C to SSS:
ROW 1: "C" — plain soft grey-blue #9AA7B8 letter, no decoration. "B" — fresh green #4CC77A letter, one tiny sparkle.
ROW 2: "A" — bright sky-blue #3D9BFF letter with a thin white inner rim, two sparkles. "S" — royal purple #A35BFF letter with a thin gold rim and a small star.
ROW 3: "SS" — hot orange-gold #FF9A2E letters with a thick gold rim, small wings on both sides, three sparkles. "SSS" — rainbow holographic letters (pink #FF6FB5 → gold #FFD23F → cyan #46D7FF) with a thick gold rim, a tiny crown above the middle S, small wings on both sides and a few sparkles touching the letters. The most precious-looking of all.
Letters only — EXACTLY the text C, B, A, S, SS, SSS, no other text, no plate or banner behind the letters. FULL ALPHA TRANSPARENT BACKGROUND, no frame, no shadow under the badges.
```

> Mẹo: AI hay vẽ sai chữ. Nếu chữ bị méo, gen riêng từng chữ bằng prompt trên và thêm câu `ONLY the letters "SS"` (thay bằng chữ cần gen). Hoặc gõ chữ bằng font tròn đậm (Baloo 2 / Fredoka Bold) rồi nhờ AI chỉ trang trí lại.

---

## F. Icon đổi (thay nhãn chữ "ĐỔI" ở thẻ quái gửi và thẻ phép)

**File:** `ZooArt/UI/icon_swap.png`, **256 × 256**, nền trong suốt. Có file là game tự dùng (cỡ 62 px ở góc thẻ). Trước khi người chơi đổi lần đầu, phía sau icon có một quầng sáng vàng đứng yên.

```text
ZOO DEFENSE — single UI icon. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shape, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight, saturated colours, readable at 40px.
Subject: a SWAP icon — two fat rounded curved arrows chasing each other in a circle (one pointing clockwise at the top, one at the bottom), sitting on a small round sunny-yellow #FFD23F coin-like button with a lighter rim. Arrows are white with a soft cream shadow. Friendly and clear, like a "change / exchange" button in a kids' game.
Centered, fills about 85% of a 256x256 square. FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters.
```

---

## Trạng thái (25/10/2026)

Đã đưa vào game toàn bộ 23 ảnh trong `Zoo Defender/round-9/Assets` (huy hiệu hạng được cắt bớt phần trong suốt thừa).

**Quái gửi** (mở sẵn, không gacha; bot chọn ngẫu nhiên 4 con):

| Quái | Giá | Thu nhập/đợt | Cơ chế |
|---|---|---|---|
| Slime Tách Đôi | 70 | +4 | Chết thì tách 2 con, mỗi con 35% máu |
| Chuột Chũi | 70 | +3 | Cứ 4 giây chui xuống đất 1,5 giây (phép vẫn đánh trúng) |
| Nấm Tiên | 55 | +4 | Cứ 2 giây hồi 5% máu cho quái quanh nó |
| Ma Trơi | 60 | +4 | Tàng hình, giấu lại quái quanh nó mỗi 2 giây |
| Bọ Bom | 75 | +3 | Chết thì làm choáng 2 thú 1 giây (khiên chặn được) |
| Bầy Tiên | 45 | +5 | 6 con nhỏ, rất nhanh |

Cho bot đánh với nhau, các bộ quái gửi có một con trong 12 con thắng khoảng 46–54%.

**Quái thường** (thêm vào nhóm quái đi theo đợt):
- Nấm Bào Tử: chết thì hồi 10% máu cho quái quanh.
- Thằn Lằn: lướt nhanh gấp đôi.
- Golem Sỏi: giáp 15%.
- Bóng Ma: định kỳ tàng hình lại.
- Dơi Bóng Bay: không bị đẩy hay kéo lùi.
- Bọ Khiên: giảm 50% sát thương diện rộng.

Độ dài trận trung vị 4:57.

Màn đội hình có thêm mô tả cho từng quái gửi. `ProtocolVersion = 10`, khoá replay `zoo_replays_v9`.

---

## G. Sheet đi bộ cho 12 quái mới

**File:** `ZooArt/EnemyWalks/{id}.png`. Mỗi file **1024 × 1024**, lưới **4 cột × 4 hàng = 16 khung**, mỗi khung 256 × 256, nền trong suốt.

`EnemyWalks/catalog.json` đã có sẵn dòng cho 12 con (tốc độ khung hình, điểm neo, cỡ hiển thị), nên thả file đúng tên vào là game tự dùng. Thiếu file thì quái vẫn đứng yên và trượt đi như bây giờ.

**Cách gen:** mỗi con gen **1 lần riêng**, và **đính kèm ảnh đứng của chính con đó** (`ZooArt/Enemies/{id}.png`) để AI giữ đúng nhân vật. Mỗi lần dán khối CHUNG bên dưới, rồi thêm 1 dòng chuyển động của con đó.

### Khối CHUNG

```text
ZOO DEFENSE — monster WALK CYCLE sprite sheet. Use the attached monster image as the EXACT character: same design, colours, proportions, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one soft highlight. Do not redesign it, do not add accessories.
One image 1024x1024, a 4 COLUMNS x 4 ROWS grid of 256x256 cells, SIXTEEN frames of ONE seamless looping walk cycle, read left-to-right then top-to-bottom; frame 16 must flow smoothly back into frame 1.
The character faces and moves to the RIGHT in every frame, 3/4 side view like the attached image. It stays in the SAME place in each cell (walking on the spot, no travel across the cell), about 160-175 px wide, horizontally centered, with its feet (or the lowest point of its body) on the SAME baseline in every cell, about 30 px above the bottom edge of the cell. Same size in all 16 frames.
Keep each frame fully inside its own cell with at least 12 px transparent margin; nothing crosses into neighbouring cells.
FULL ALPHA TRANSPARENT BACKGROUND, no grid lines, no frame borders, no numbers, no text, no ground, no shadow, no motion lines, no dust clouds.
```

### Chuyển động của từng con (thêm 1 dòng sau khối CHUNG)

| id | Tên | Dòng thêm vào prompt |
|---|---|---|
| `send_splitter` | Slime Tách Đôi | `Motion: a bouncy jelly hop — squash wide on landing, stretch tall when rising, the two faces wobble slightly out of sync; the body never leaves the baseline by more than 20 px.` |
| `send_mole` | Chuột Chũi Đào Hầm | `Motion: the mole waddles forward inside its little dirt mound, swinging the pickaxe on its shoulder, helmet lamp bobbing; small dirt clods on the mound edge shift each frame (touching the body only).` |
| `send_mushroom` | Nấm Tiên Chữa Lành | `Motion: a happy tip-toe skip on two tiny legs, the big spotted cap bouncing a little later than the body, the leaf wand swinging gently with its green plus sparkle.` |
| `send_wisp` | Ma Trơi Sương Mù | `Motion: floating (no legs) — a slow gentle up-down drift of about 12 px, the fog scarf rippling and trailing behind, flame tip flickering; the lowest point of the mist stays near the baseline.` |
| `send_bomb` | Bọ Bom | `Motion: a quick nervous six-legged scuttle, legs alternating in tripods, the shell rocking slightly, the fuse spark flickering on its back (spark size changes, stays attached).` |
| `send_pixies` | Tiên Tí Hon | `Motion: flying — fast buzzing wings (blurred on some frames), body bobbing up and down about 15 px and tilting forward, a cheeky look; the feet hover just above the baseline.` |
| `spore_puff` | Nấm Bào Tử | `Motion: a slow grumpy waddle on stubby feet, the puffy cap squashing a little each step, a tiny puff of spores rising from the top every 4 frames (touching the cap).` |
| `dash_lizard` | Thằn Lằn Phóng | `Motion: a fast sprint — legs in a full running cycle, body low and leaning forward, the long curly tail whipping up and down, goggles bouncing.` |
| `pebble_golem` | Golem Sỏi | `Motion: a heavy slow stomp — one foot lifts and lands per half cycle, the stacked stones shift and wobble slightly, the moss tuft bounces on landing.` |
| `shadow_blob` | Bóng Ma Nhỏ | `Motion: a sneaky slide — the wavy bottom edge ripples forward like a wave, the body leans and wobbles, sparkles on the fading side twinkle (always touching the body).` |
| `balloon_bat` | Dơi Bóng Bay | `Motion: floating like a balloon — small wings flapping fast, the round body bobbing up and down about 15 px, the balloon string swinging below; the string's end stays near the baseline.` |
| `shield_beetle` | Bọ Khiên | `Motion: a steady six-legged march behind the big round shield, the shield bobbing slightly with each step, the head peeking over the top.` |

### Mẹo

- **Kiểm tra trước khi thả vào game:** xem lướt 16 khung xem nhân vật có đứng **cùng chỗ, cùng cỡ** không. Nếu lệch, cứ gửi sheet cho mình để căn lại.
- **Nếu AI không vẽ nổi 16 khung đẹp,** gen **8 khung (4 cột × 2 hàng, ảnh 1024 × 512)** cũng được. Báo mình để chỉnh `catalog.json` cho con đó.
- **Nếu nền không trong suốt,** gửi sheet cho mình, mình sẽ tách nền.
