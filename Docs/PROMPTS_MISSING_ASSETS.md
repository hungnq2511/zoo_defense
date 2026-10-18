# Zoo Defense — Prompt cho asset còn thiếu (đợt 3)

Cùng phong cách với bộ đã duyệt: chibi 2D, viền nâu đậm, cel shading 1 tông bóng + 1 highlight, nền trong suốt.
Khi tạo, **đính kèm ảnh tham chiếu** `missing-assets/PNG/ui_icons.png` (cho icon, rương, chưởng) hoặc một bảng thú đã duyệt (cho icon app, màn chờ, HLV).
Tạo xong chỉ cần để vào thư mục `Zoo Defender`, Claude sẽ cắt, chuẩn hóa và nhập vào game.

| # | Bảng | Ra file trong game |
|---|---|---|
| 1 | Icon tiền tệ (2) | `ZooArt/UI/gems.png`, `xp.png` |
| 2 | Rương (4) | `ZooArt/UI/chest_wood/silver/gold/magic.png` |
| 3 | Chưởng (4 hệ) | `ZooArt/Projectiles/fire/water/grass/light.png` |
| 4 | Icon app | Player Settings → Icon |
| 5 | Màn chờ | Splash / loading |
| 6 | HLV Gấu Trúc (thêm) | Hướng dẫn |
| 7 | Âm thanh + nhạc | `Resources/ZooAudio/` |
| 8 | Phông chữ | `ZooArt/Fonts/Main.ttf` |

---

## 1. Icon tiền tệ — kim cương + XP mùa giải

```text
ZOO DEFENSE — UI ICON SET 2. Match the attached approved Zoo Defense UI icons (candy, heart, trophy, gold coins) EXACTLY in style: chunky rounded shapes, thick dark-brown outline of the same weight, flat cel shading with one shadow tone and one glossy highlight, saturated colours, readable at 48–64px.
One landscape contact sheet, 2 COLUMNS x 1 ROW, EXACTLY TWO single game icons, each centered in its own equal square cell, at least 100px empty transparent gap between them. FULL ALPHA TRANSPARENT BACKGROUND. No text, no numbers, no letters, no background shadow, no sparkles, stars or particles floating around the icons, no glow halo outside the outline.
LEFT: GEMS (premium currency) — a cluster of TWO faceted gems, one big cut gem in front and one smaller behind, bright cyan-blue #5BD6FF with lighter facets and one white glint shape INSIDE the outline. Chunky, cute, not realistic.
RIGHT: SEASON PASS XP — a plump five-point star #9B5CFF purple with a lighter violet inner bevel and a small white paw-print emblem in the centre. Rounded star tips, no ribbon, no text.
Both icons occupy the same visual size (about 70% of their cell) and sit centered. Exactly TWO icons.
LAYOUT EXECUTION: no checker pattern drawn, no gradient background, do NOT draw any of the reference icons, characters, badges or haze.
```

## 2. Rương — Gỗ, Bạc, Vàng, Phép

```text
ZOO DEFENSE — TREASURE CHEST SET. Match the attached approved Zoo Defense UI icons in style: chunky rounded cartoon shapes, thick dark-brown outline of consistent weight, flat cel shading with one shadow tone and one restrained highlight, saturated colours, readable at 96px.
One square contact sheet, 2 COLUMNS x 2 ROWS, EXACTLY FOUR CLOSED treasure chests, one per equal square cell, all in the SAME three-quarter front view slightly from above, SAME base size and SAME shape family (rounded domed lid, front lock plate shaped like a paw print), so they read as one tier set. Each chest about 75% of its cell width, bottom edge at 85% of cell height. At least 80px empty transparent gutters between cells. FULL ALPHA TRANSPARENT BACKGROUND. No text, no numbers, no ground shadow, no floor, no coins spilling, no floating sparkles, no light rays, no glow outside the outline. Lids CLOSED.
TOP LEFT: WOODEN CHEST (common) — warm brown wooden planks #B07A4A with darker plank lines, simple iron-grey bands, small iron paw-print lock. Plain and humble.
TOP RIGHT: SILVER CHEST (rare) — pale blue-grey silver metal #B8C4D6 with darker steel bands and rivets, polished silver paw lock, slightly more ornate trim.
BOTTOM LEFT: GOLDEN CHEST (epic) — rich gold #FFC93C body with deep red velvet panels between gold bands, ornate gold corners, shiny gold paw lock with a small red gem.
BOTTOM RIGHT: MAGIC CHEST (legendary) — royal purple #B46BFF body with gold trim, curved gold corners, a glowing violet crystal set in the paw lock (glow kept INSIDE the outline as lighter colour), tiny star engravings on the lid. Clearly the most precious, same overall size.
Exactly FOUR chests.
```

## 3. Chưởng của thú — 4 hệ

Đầu đạn **hướng sang phải**, game tự xoay theo hướng bay. Hiển thị rất nhỏ (24–40 px) nên phải đơn giản, đậm màu.

```text
ZOO DEFENSE — ATTACK PROJECTILE SPRITES. Match the attached Zoo Defense icon style: thick dark-brown outline, flat cel shading with one shadow tone and one highlight, saturated element colours, extremely simple shapes readable at 32px.
One square contact sheet, 2 COLUMNS x 2 ROWS, EXACTLY FOUR projectiles, each centered in its own equal square cell, every projectile FLYING TO THE RIGHT (head on the right, tail on the left), horizontally centered, about 70% of the cell width and 40% of the cell height. At least 80px empty transparent gutters. FULL ALPHA TRANSPARENT BACKGROUND. The tail must be a SOLID SHAPE ATTACHED to the head — no detached particles, no separate sparks, no smoke puffs, no motion lines, no glow halo outside the outline, no text.
TOP LEFT: FIRE — round fireball head orange-red #FF6B3D with a warm yellow #FFC93C core, short teardrop flame tail of three tapered flame tongues attached behind.
TOP RIGHT: WATER — plump water droplet lying sideways, aqua #3DB8FF with teal #2EC4B6 shading and a white shine, short wavy attached water tail.
BOTTOM LEFT: GRASS — spinning leaf shuriken: two crossed fresh green leaves #6BCB4B with darker veins and a small seed centre, a short curved green swoosh attached behind.
BOTTOM RIGHT: LIGHT — chubby five-point star, warm cream-yellow #FFE680 with white centre, a short tapered comet tail in pale gold attached behind.
Exactly FOUR projectiles, nothing else.
```

## 4. Icon app — 1024 × 1024

Không chữ, không bo góc (App Store / Google Play tự bo). Nhân vật lớn, rõ khi thu còn 48 px.

```text
ZOO DEFENSE — MOBILE APP ICON, 1024x1024, square, full-bleed artwork that fills the entire square edge to edge (NO rounded corners drawn, NO border frame, NO transparent background, NO text, NO letters, NO logo).
Use the attached approved Zoo Defense character sheet as reference for the characters and style: cute chibi 2D, thick dark-brown outline, flat cel shading, saturated colours.
Composition: close-up hero shot. FIRE FOX (orange fox with red scarf and a fireball attached to its paw) in the foreground slightly left, brave smile, facing the viewer; CHUBBY PANDA peeking from behind on the right with a cheerful face. A cute goofy purple SLIME enemy small in the lower right corner looking surprised. Background: simple sunny zoo gate arch with green hedges and a bright sky-blue #6EC8FF gradient, kept simple and low-detail so the characters pop.
The two heroes' heads occupy about 60% of the icon and are fully inside the central 80% safe area. High contrast, readable at 48px. No small details near the edges.
```

## 5. Màn chờ khi mở game — dọc 1080 × 1920

Chừa trống phía trên cho logo (logo chữ nên làm riêng hoặc để game ghi chữ, AI thường viết sai chữ).

```text
ZOO DEFENSE — PORTRAIT LOADING SCREEN ARTWORK, 1080x1920 (9:16), full-bleed, NO text, NO letters, NO logo, NO UI.
Style: match the attached approved Zoo Defense character art — cute chibi 2D, thick dark-brown outlines, flat cel shading, saturated friendly colours.
Scene: a cheerful zoo garden seen from slightly above. Bottom half: a cobblestone path winding toward a wooden zoo gate; a small team of the defender animals stands proudly in front of the gate — FIRE FOX, BUBBLE FROG in its round glass diving helmet, CHUBBY PANDA, SPIKE HEDGEHOG and CANNON OTTER with sailor cap and shell cannon — facing the viewer. In the distance on the path, a few goofy purple slimes and an imp approach, small and funny, not scary.
Top 35% of the image: calm bright sky-blue gradient with a few soft rounded clouds and NOTHING else, reserved as empty space for the game logo.
Keep all characters inside the central 90% width (phones crop the sides). Gentle vignette-free lighting, no lens flare, no particles.
```

## 6. HLV Gấu Trúc — chân dung hướng dẫn (thêm)

```text
ZOO DEFENSE — TUTORIAL COACH PORTRAIT. Use the attached approved CHUBBY PANDA character as the exact reference for face, colours and outline style: cute chibi 2D, thick dark-brown outline, flat cel shading.
One single square image, transparent background (alpha), bust portrait from the chest up, centered, about 80% of the square. The panda wears a small red coach whistle on a cord and a green cap with a tiny paw emblem, friendly encouraging smile, one paw raised in a thumbs-up attached to the body. No text, no speech bubble, no background, no shadow, no particles.
```

---

## 7. Âm thanh

Định dạng `.wav` hoặc `.ogg`, mono, 44.1 kHz. Hiệu ứng ngắn 0.1–1 giây, cắt sạch khoảng lặng đầu file. Nhạc: `.ogg` stereo, vòng lặp liền mạch 60–120 giây.
Có thể dùng công cụ tạo âm thanh AI (ví dụ ElevenLabs Sound Effects) hoặc thư viện có giấy phép thương mại. **Kiểm tra giấy phép dùng thương mại** trước khi phát hành.

| File | Mô tả / prompt |
|---|---|
| `click` | soft cartoon UI pop click, short, bubbly, 0.1s |
| `buy` | cheerful coin-and-pop purchase sound, cute mobile game, 0.3s |
| `merge` | magical rising chime with a bouncy pop, two cute creatures merging, 0.6s |
| `attack` | tiny soft whoosh pew, very short and quiet (plays often), 0.1s |
| `kill` | small cartoon poof with a sparkly candy ding, 0.3s |
| `leak` | short sad cartoon "bonk" with a low thud, 0.4s |
| `wave` | short playful horn fanfare announcing a new wave, 0.8s |
| `boss` | deep comedic drum roll hit with a low brass stinger, cartoon villain, 1.2s |
| `send` | mischievous cartoon whoosh with a goofy "boing", 0.5s |
| `win` | happy victory jingle, xylophone and brass, cute mobile game, 2s |
| `lose` | gentle sad trombone "wah-wah", cartoon, not harsh, 1.5s |
| `chest` | wooden chest creak opening into a bright treasure sparkle burst, 1s |
| `upgrade` | ascending magical power-up arpeggio, triumphant, 0.8s |
| `reward` | bright collect jingle, coins and gems, 0.6s |
| `error` | soft low double-buzz "nope", friendly not harsh, 0.3s |

Nhạc nền:
- `music_lobby` — *cheerful relaxed zoo-themed loop, marimba, ukulele, light percussion, whistling melody, 100 BPM, cozy mobile game menu, seamless loop, no vocals*
- `music_battle` — *upbeat playful battle loop, bouncy bass, pizzicato strings, snappy drums, light brass hits, 130 BPM, cartoon tower defense, energetic but not stressful, seamless loop, no vocals*

## 8. Phông chữ

Tải **Baloo 2** (bản ExtraBold) hoặc **Nunito** (Black) từ Google Fonts (giấy phép OFL, dùng thương mại được, có đủ dấu tiếng Việt). Đổi tên file thành `Main.ttf` và đặt vào `Assets/ZooDefense/Resources/ZooArt/Fonts/`. Game tự dùng.
