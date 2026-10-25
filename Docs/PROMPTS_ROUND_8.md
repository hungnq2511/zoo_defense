# Zoo Defense — Asset đợt 8: Bùa hệ + 15 thú mới (5 SSS)

Thả file đúng tên vào `Assets/ZooDefense/Resources/ZooArt/…` là game tự dùng. Thiếu file thì game vẫn chạy (vẽ tạm).
Khi gen, **đính kèm 2 ảnh mẫu nét vẽ**: `Units/rainbow_unicorn_7.png` và `Units/aegis_ram_4.png`.

---

## PHẦN 1 — Asset còn thiếu trong game hiện tại

| Ưu tiên | File | Thư mục | Kích thước | Ghi chú |
|---|---|---|---|---|
| **Nên có** | `icon_hex.png` | `ZooArt/UI/` | 256 × 256 | Nút **Bùa hệ** mới (đang dùng hình thoi tím tạm). Prompt ngay dưới. |
| Tuỳ chọn | `nav_shop`, `nav_deck`, `nav_battle`, `nav_pass`, `nav_settings` | `ZooArt/UI/` | 256 × 256 | Prompt đã có ở `PROMPTS_ROUND_5.md` mục 1 |
| Tuỳ chọn | `gacha_egg`, `card_back`, `icon_ad`, `icon_replay`, `vs_badge` | `ZooArt/UI/` | xem `PROMPTS_ROUND_5.md` | Prompt ở `PROMPTS_ROUND_5.md` mục 2–6 |
| Tuỳ chọn | Sheet đánh `{id}_{1,4,7}_cast.png` cho **20 thú gốc** | `ZooArt/UnitCasts/` | 1024 × 512 | 10 thú mở rộng đã có; 20 thú gốc đang dùng động tác nhún bằng code. Prompt mẫu ở PHẦN 2 mục D. |

Huy hiệu hạng C–SSS, hào quang, talent đều vẽ bằng code — **không cần asset**.

### `icon_hex.png` — Bùa hệ (256 × 256)
```text
ZOO DEFENSE — single UI icon. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shape, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight, saturated colours, readable at 48px.
Subject: a magic HEX TALISMAN — a round purple amulet with a glowing violet gem in the centre, three tiny element orbs orbiting it (a red flame, a blue water drop, a green leaf), one curl of purple magic smoke rising from the top. Slightly mischievous, cute, not scary.
Centered, fills about 80% of a 256x256 square. FULL ALPHA TRANSPARENT BACKGROUND, no frame, no circle background, no text.
```

---

## PHẦN 2 — 15 thú mới

### Danh sách

| # | id (tên file) | Tên Việt | Tên Anh | Hạng | Hệ | Vai trò | Ý tưởng kỹ năng (mình sẽ code khi có art) |
|---|---|---|---|---|---|---|---|
| 1 | `celestial_qilin` | Kỳ Lân Thiên Giới | Celestial Qilin | **SSS** | Ánh sáng | Diện rộng | Mỗi đòn thứ 6 gọi mưa sao rơi xuống 3 chỗ đông quái nhất |
| 2 | `abyss_leviathan` | Thủy Long Vực Thẳm | Abyss Leviathan | **SSS** | Nước | Sát thương | Đòn thứ 5 xuyên cả đường đi; đánh mạnh hơn lên quái nhiều máu |
| 3 | `solar_lion` | Sư Tử Mặt Trời | Solar Lion | **SSS** | Lửa | Sát thương | Đốt lan sang quái gần; gây thêm sát thương lên quái đang cháy |
| 4 | `worldtree_tortoise` | Rùa Cổ Thụ | World-Tree Tortoise | **SSS** | Cỏ | Hỗ trợ | Mỗi đợt cho kẹo; cứ 4 đợt +1 cấp cho thú ngẫu nhiên; khiên chặn khoá ô |
| 5 | `ninetail_star_fox` | Hồ Ly Cửu Vĩ Tinh Tú | Nine-Tail Star Fox | **SSS** | Ánh sáng | Khống chế | Mê hoặc: quái đi lùi 1 giây, boss bị chậm |
| 6 | `thunder_griffin` | Ưng Sư Sấm Sét | Thunder Griffin | SS | Ánh sáng | Sát thương | Sét nảy sang 3 quái |
| 7 | `glacier_wolf` | Sói Băng Hà | Glacier Wolf | SS | Nước | Khống chế | Hú: đóng băng quái quanh mục tiêu |
| 8 | `magma_rhino` | Tê Giác Dung Nham | Magma Rhino | SS | Lửa | Diện rộng | Húc: nổ lớn + đẩy lùi |
| 9 | `bloom_peacock` | Công Hoa Nở | Bloom Peacock | SS | Cỏ | Hỗ trợ | Xoè đuôi: thú xung quanh +tốc đánh |
| 10 | `crystal_pangolin` | Tê Tê Pha Lê | Crystal Pangolin | S | Ánh sáng | Diện rộng | Lăn pha lê, phá giáp |
| 11 | `lantern_jellyfish` | Sứa Đèn Lồng | Lantern Jellyfish | S | Nước | Khống chế | Lộ quái tàng hình, làm chậm diện rộng |
| 12 | `flame_tiger_cub` | Hổ Con Hỏa Văn | Flame Tiger Cub | S | Lửa | Sát thương | Vồ: chí mạng lên quái gửi |
| 13 | `bamboo_red_panda` | Gấu Mèo Trúc | Bamboo Red Panda | A | Cỏ | Sát thương | Đánh nhanh bằng gậy trúc |
| 14 | `comet_hamster` | Hamster Sao Chổi | Comet Hamster | A | Ánh sáng | Diện rộng | Ném sao chổi nhỏ nổ lan |
| 15 | `puddle_duckling` | Vịt Con Vũng Nước | Puddle Duckling | B | Nước | Khống chế | Bắn bong bóng làm chậm |

### Cách gen và cắt (mỗi thú 1 lần gen)

- Mỗi prompt ra **1 ảnh 1152 × 384, 3 cột**: trái = cấp 1, giữa = cấp 4, phải = cấp 7.
- Cắt thành 3 ảnh 384 × 384, đặt tên `{id}_1.png`, `{id}_4.png`, `{id}_7.png`, bỏ vào `ZooArt/Units/`.
- Ghép **khối STYLE chung** (A) + **khối của từng thú** (B hoặc C) thành 1 prompt.
- Thú SSS ghép thêm **khối PRIME** (A2).

### A. Khối STYLE chung (dán đầu mọi prompt thú)
```text
ZOO DEFENSE — animal unit evolution sheet. Match the attached Zoo Defense animal art EXACTLY: chibi kawaii mobile-game mascot, big head (about 55% of the height), huge glossy eyes with two white highlights, small round body, short stubby legs, thick dark-brown #3B2412 outline (6-8 px at 384 px), flat cel shading with one shadow tone and one soft highlight, saturated candy colours, clean sticker silhouette readable at 96 px. 3/4 view facing slightly LEFT, standing, whole body visible, feet near the bottom of the cell.
One image 1152x384, 3 COLUMNS x 1 ROW, the SAME animal in three evolution stages, each centered in its own 384x384 cell with at least 40 px transparent margin:
LEFT = level 1 (baby, simple, cute), MIDDLE = level 4 (a bit bigger, first armor pieces), RIGHT = level 7 (final form, ornate armor and crown, most impressive). Keep the same face, colour palette and pose family across all three.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no ground shadow, no floor, no particles floating away from the body, no frame.
```

### A2. Khối PRIME — chỉ thêm cho 5 thú SSS
```text
PRIME / MYTHIC TIER (the rarest animals in the game): it must look clearly more precious than every other animal. Gold filigree trim, inlaid glowing gems, iridescent holographic accents on mane / fins / tails, flowing magical hair that curls gracefully, a thin glowing halo ring or crown behind the head at level 7, a few tiny sparkles TOUCHING the body only. Rich but readable colours, strong silhouette. Still chibi and adorable — majestic and heroic, never scary or realistic.
```

### B. 5 thú SSS

**1. `celestial_qilin` — Kỳ Lân Thiên Giới (Ánh sáng)**
```text
Animal: a CELESTIAL QILIN (Chinese mythical kirin) — NOT a unicorn: two small antlers instead of a single horn, dragon-scale patches, cloud-shaped mane.
Palette: pearl white body, gold scales, mint-green and gold cloud mane, amber crystal antlers, sky-blue eyes.
Level 1: baby qilin, tiny antler nubs, fluffy cloud-puff tail, little gold scale patch on the back, cloud tufts at the ankles.
Level 4: gold scale armor plates on chest and back, antlers grown and glowing amber, mane longer like swirling clouds, small jade pendant.
Level 7: celestial emperor form — ornate gold-and-jade armor with filigree, glowing crown of light between the antlers, halo ring of eight tiny stars behind the head, mane flowing in gold-to-mint gradient clouds, cloud-flame tufts on all four ankles.
```

**2. `abyss_leviathan` — Thủy Long Vực Thẳm (Nước)**
```text
Animal: a baby ABYSS LEVIATHAN — a chibi sea dragon / sea serpent standing on short legs, fin ears, curled tail.
Palette: deep navy and midnight-blue body, glowing bioluminescent cyan stripes and freckles, pearl white belly, aqua eyes.
Level 1: round baby sea dragon, small fin ears, a single pearl on the forehead, curly tail with a little fin.
Level 4: silver-and-coral scale armor on chest and shoulders, tail tip shaped like a trident, glowing cyan rune marks along the back.
Level 7: abyssal emperor — crown of black pearls and aquamarine, long translucent fins flowing like silk, ornate silver-teal armor with inlaid sea gems, halo of tiny glowing bubbles behind the head.
```

**3. `solar_lion` — Sư Tử Mặt Trời (Lửa)**
```text
Animal: a SOLAR LION cub whose mane is living flame.
Palette: warm golden-orange fur, mane of orange-yellow fire with a red core, ruby red paws, bright amber eyes.
Level 1: lion cub, small flickering flame mane, tail tip is a little flame, proud smile.
Level 4: bronze-gold chest plate with a sun emblem, flame mane bigger and wilder, gold bracers on the front paws.
Level 7: Sun King — radiant golden sun-disc halo behind the mane, ruby-studded gold armor with filigree, short flame cape, small golden crown, mane blazing in yellow-orange-red layers.
```

**4. `worldtree_tortoise` — Rùa Cổ Thụ (Cỏ)**
```text
Animal: a WORLD-TREE TORTOISE — a chibi tortoise carrying a magical tree on its shell.
Palette: mossy green shell, warm tan skin, glowing spring-green leaves, pink blossoms, gold accents, gentle green eyes.
Level 1: baby tortoise with one tiny glowing sapling growing from the shell, a single pink flower.
Level 4: shell carved with glowing green runes and moss, a small tree with three glowing golden fruits, vines with tiny flowers around the legs.
Level 7: ancient guardian — shell rimmed in gold like a little mountain, a luminous world tree with a wide crown of leaves and crystal fruits, leaf-and-gold crown on its head, vines with blossoms draping down, wise happy face.
```

**5. `ninetail_star_fox` — Hồ Ly Cửu Vĩ Tinh Tú (Ánh sáng)**
```text
Animal: a NINE-TAILED STAR FOX (kitsune) whose tails look like the night sky.
Palette: white and soft lavender fur, tails filled with deep violet galaxy pattern and tiny stars, silver and pale-gold accents, violet eyes.
Level 1: fox kit with THREE fluffy tails tipped with starlight, small crescent-moon mark on the forehead.
Level 4: SIX galaxy tails, silver moon pendant on a ribbon, small star earrings, tails glowing softly.
Level 7: NINE galaxy tails fanned out behind the body like a peacock fan, crescent-moon tiara with gems, silver-violet robe trim with gold filigree on the chest, thin halo of stars behind the head.
```

### C. 10 thú SS · S · A · B (không thêm khối PRIME)

**6. `thunder_griffin` — Ưng Sư Sấm Sét (SS, Ánh sáng)**
```text
Animal: a THUNDER GRIFFIN chick — eaglet head and wings, lion-cub body.
Palette: white and lemon-yellow feathers, golden lion fur, electric-blue lightning-bolt markings, gold beak.
Level 1: fluffy griffin chick, tiny wings, little lightning mark on the chest.
Level 4: silver-gold chest armor, bigger wings with crackling blue feather tips, lightning crest on the head.
Level 7: storm knight — ornate silver-gold armor, large wings edged with electric blue, lightning-bolt crown, sparks touching the wing tips.
```

**7. `glacier_wolf` — Sói Băng Hà (SS, Nước)**
```text
Animal: a GLACIER WOLF pup.
Palette: pale ice-blue and white fur, deep-blue ice crystals, aurora green-violet highlights, light-blue eyes.
Level 1: wolf pup with fluffy fur, one small ice crystal on the forehead.
Level 4: frost armor on shoulders and paws, icicle spikes along the back, frosty scarf.
Level 7: glacier alpha — crystal ice armor, icicle crown, mane shimmering with aurora colours, frost cape.
```

**8. `magma_rhino` — Tê Giác Dung Nham (SS, Lửa)**
```text
Animal: a chunky MAGMA RHINO calf.
Palette: obsidian charcoal skin with glowing orange lava cracks, ember-red horn, gold rivets.
Level 1: round rhino calf with a small glowing horn and a few lava cracks.
Level 4: rock shoulder plates with lava seams, bigger horn glowing orange.
Level 7: volcano warlord — shoulder armor shaped like little volcanoes, molten-gold horn, lava-cracked chest plate.
```

**9. `bloom_peacock` — Công Hoa Nở (SS, Cỏ)**
```text
Animal: a BLOOM PEACOCK chick whose feathers are flower petals.
Palette: emerald and teal body, pink and peach blossom petals, leaf-green "eye" patterns, gold jewelry.
Level 1: peachick with a crest of three small petals.
Level 4: short fan tail of petals and leaves, golden vine necklace.
Level 7: garden queen — full fan tail of blossoms with leaf-eye patterns, golden vine crown and jewelry.
```

**10. `crystal_pangolin` — Tê Tê Pha Lê (S, Ánh sáng)**
```text
Animal: a CRYSTAL PANGOLIN.
Palette: pale pink-lilac crystal scales, cream belly, prism rainbow glints.
Level 1: baby pangolin with soft crystal scales.
Level 4: bigger faceted crystal scales, gold-banded tail.
Level 7: gem-armored pangolin with a prism-tipped tail and a small crystal crown.
```

**11. `lantern_jellyfish` — Sứa Đèn Lồng (S, Nước)**
```text
Animal: a cute LANTERN JELLYFISH with a face, floating just above the ground.
Palette: glowing pink-and-blue bell like a paper lantern, soft white tentacles.
Level 1: small round jellyfish with short tentacles.
Level 4: bell decorated with lantern ribs and gold tassels, longer ribbon tentacles.
Level 7: lantern-festival queen — crown-shaped bell, golden ornaments, long flowing ribbon tentacles.
```

**12. `flame_tiger_cub` — Hổ Con Hỏa Văn (S, Lửa)**
```text
Animal: a FLAME TIGER CUB.
Palette: bright orange fur with red-and-yellow flame-shaped stripes, white muzzle, gold accents.
Level 1: playful tiger cub, tail tip is a small flame.
Level 4: red headband and light leather armor, flame stripes glowing.
Level 7: samurai tiger — red-and-gold armor, flame tail, small kabuto-style helmet.
```

**13. `bamboo_red_panda` — Gấu Mèo Trúc (A, Cỏ)**
```text
Animal: a RED PANDA holding a bamboo staff.
Palette: russet red fur, cream face markings, bamboo green, leaf accents.
Level 1: red panda cub hugging a short bamboo stick.
Level 4: leaf shoulder pads, longer bamboo staff.
Level 7: bamboo master — leaf armor, bamboo spear, small bamboo-leaf hat.
```

**14. `comet_hamster` — Hamster Sao Chổi (A, Ánh sáng)**
```text
Animal: a round COMET HAMSTER with puffed star-dust cheeks.
Palette: golden-cream fur, sparkly cheek pouches, cobalt-blue and gold accents.
Level 1: tiny round hamster with a star on the forehead.
Level 4: little space goggles, star badge.
Level 7: comet pilot — round astronaut-style helmet with a comet tail crest, star armor.
```

**15. `puddle_duckling` — Vịt Con Vũng Nước (B, Nước)**
```text
Animal: a PUDDLE DUCKLING.
Palette: bright yellow down, blue raincoat, orange beak and feet.
Level 1: duckling in a little blue raincoat holding a bubble.
Level 4: rain hat and bubble wand.
Level 7: rain captain — captain's coat, rain cape, bubble shield.
```

### D. Asset phụ cho 15 thú (tuỳ chọn)

**Đạn (`ZooArt/Projectiles/{id}.png`, 256 × 256):** gen 1 ảnh 1280 × 768 (5 cột × 3 hàng), cắt theo thứ tự bảng 1→15.
```text
ZOO DEFENSE — projectile sprite sheet. Same chunky cartoon style as the attached art: thick dark-brown #3B2412 outline, flat cel shading, saturated colours.
One image 1280x768, 5 COLUMNS x 3 ROWS, FIFTEEN separate projectiles, each centered in its own 256x256 cell, each pointing to the RIGHT, filling about 60% of its cell:
1 golden star meteor with a mint cloud trail, 2 glowing cyan water orb with a pearl core, 3 small sun fireball with a red core, 4 spinning golden leaf with a tiny blossom, 5 violet crescent-moon blade with stars,
6 electric-blue lightning feather, 7 jagged ice shard, 8 lava boulder with glowing cracks, 9 pink flower petal dart, 10 faceted lilac crystal shard,
11 glowing pink-blue bubble, 12 flame claw slash, 13 bamboo leaf dart, 14 tiny comet with a star head, 15 soap bubble.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no extra particles between cells.
```

**Sheet đánh (`ZooArt/UnitCasts/{id}_{1|4|7}_cast.png`, 1024 × 512):** mỗi sheet là 1 dạng cấp; dùng ảnh tĩnh cấp đó làm tham chiếu. Cũng dùng được cho 20 thú gốc còn thiếu.
```text
ZOO DEFENSE — unit attack animation sheet. Use the attached animal image as the EXACT character (same design, colours, outline, size and 3/4 left-facing view).
One image 1024x512, 4 COLUMNS x 2 ROWS, eight frames of one short attack, read left-to-right then top-to-bottom: 1 idle, 2 anticipation (crouch / lean back), 3 wind-up, 4 strike (lunge forward, mouth open, small attack flash touching the body), 5 follow-through, 6 recoil, 7 settle, 8 back to idle.
Each frame centered in its own 256x256 cell, feet on the same baseline in every frame, character the same size in every frame.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no ground, no shadow.
```

---

## Lưu ý

- Thêm 5 SSS mới thì cơ hội ra **đúng một** con SSS cụ thể giảm (tỉ lệ SSS 0,3% chia cho 6 con). Có thể đưa vào game theo từng mùa (mỗi mùa 1–2 con, có banner "thú tăng tỉ lệ").
- Gửi art lên là mình thêm thú vào game: chỉ số, kỹ năng riêng của 5 SSS, tên 8 ngôn ngữ, cân bằng lại bằng bot.
