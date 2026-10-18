# Zoo Defense — Asset còn thiếu (đợt 4)

Game đã chạy đủ mà không cần các file dưới đây: chỗ nào thiếu ảnh thì code tự vẽ hình thay thế. Khi anh thả đúng tên file vào đúng thư mục, game **tự dùng ảnh thật**, không cần sửa code.

Quy ước chung:
- PNG, **nền trong suốt (alpha)**, đặt vào `Assets/ZooDefense/Resources/ZooArt/UI/`.
- Khi gen, đính kèm `missing-assets/PNG/ui_icons.png` và 1–2 ảnh thú (ví dụ `Units/fire_fox_1.png`) làm tham chiếu nét vẽ.
- Prompt dạng "sheet" (nhiều ô trong 1 ảnh) thì cắt ra từng file theo bảng tên. Ô nào sai thì gen lại riêng ô đó bằng cùng prompt, chỉ giữ 1 ô.
- Font Baloo 2 không có ký tự ★ ❄ ✓, nên ngôi sao, phép và dấu tích cần có ảnh riêng.

## Tổng quan

| # | File | Dùng ở | Kích thước | Ưu tiên |
|---|---|---|---|---|
| 1 | `spell_freeze.png`, `spell_meteor.png`, `spell_gust.png`, `spell_rage.png`, `spell_candy_rain.png`, `spell_lightning.png` | Nút phép tròn trong trận, thẻ phép ở Đội hình | 512 × 512 mỗi icon | Cao |
| 2 | `board_portal.png` | Cổng quái ra (góc trên-trái bàn) | 768 × 768 | Cao |
| 3 | `board_gate.png` | Cổng sở thú, nơi mất tim (góc dưới-phải) | 768 × 768 | Cao |
| 4 | `button_summon.png` | Nút TRIỆU HỒI | 1050 × 420 (≈ 2.5 : 1) | Cao |
| 5 | `star.png` | Dấu ô ngôi sao (+30% sát thương) | 256 × 256 | Trung bình |
| 6 | `balloon.png` | Bóng bay kẹo bay ngang bàn | 512 × 640 | Trung bình |
| 7 | `logo.png` | Home | 1520 × 340 | Trung bình |
| 8 | `ribbon.png`, `ribbon_defeat.png` | Tên đấu trường, Thắng / Thua | 1720 × 340 | Trung bình |
| 9 | `podium.png` | Bục 5 thú ở Home | 1320 × 400 | Trung bình |
| 10 | `button_battle.png` | Nút CHIẾN ĐẤU ở Home | 1440 × 400 | Trung bình |
| 11 | `rays.png` | Tia sáng sau nút / màn Thắng | 1024 × 1024 | Thấp (code đã tự vẽ) |
| 12 | `send.wav` → `Resources/ZooAudio/` | Âm thanh gửi quái / dội lại | 0.4–0.8 s | Trung bình |

Mục 7–11 là các asset của màn Home / Kết quả đã có prompt từ đợt trước nhưng thư mục chưa có. Tôi chép lại prompt ở đây để anh có tất cả ở một chỗ.

---

## 1. Icon 6 phép (1 sheet, 3 cột × 2 hàng)

Cắt theo thứ tự: hàng trên `spell_freeze`, `spell_meteor`, `spell_gust`; hàng dưới `spell_rage`, `spell_candy_rain`, `spell_lightning`.

```text
ZOO DEFENSE — SPELL ICON SET. Match the attached Zoo Defense style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight, saturated friendly colours, cute and readable at 64px.
One image, 3 COLUMNS x 2 ROWS grid, EXACTLY SIX separate icons, each centered in its own square cell with at least 80px of transparent gap between icons. Each icon is a single bold object, no frame, no circle background, no badge, because the game puts it on its own coloured round button.
TOP ROW, left to right:
1) FREEZE — a chunky light-blue ice crystal / snowflake made of six rounded icy arms, pale cyan #9FE3FF with white frosty highlights.
2) METEOR — a round orange-red flaming rock #E0582F diving diagonally down-left with a short chunky flame tail, small dark crater dots on the rock.
3) GUST — three fat curly wind swirls in mint green #7BD88F, flowing left to right, with two tiny leaves caught in the wind.
BOTTOM ROW, left to right:
4) RAGE — an angry cartoon fist / paw punch in red #D43A44 with small white anger-vein mark and three short motion lines, cute not violent.
5) CANDY RAIN — a small fluffy pink cloud #FFB3DA with three wrapped swirl candies falling from it (pink, mint, yellow).
6) LIGHTNING — one thick zig-zag lightning bolt, bright yellow #FFD23F with an orange inner edge and a white highlight.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no numbers, no letters, no circles or frames around icons, no sparkles floating between icons, no drop shadow on the background.
```

## 2. Cổng quái và cổng sở thú (1 sheet, 2 cột)

Cắt: trái `board_portal.png`, phải `board_gate.png`. Mỗi ảnh nhìn từ trên xuống, hơi chéo, giống góc nhìn bàn chơi.

```text
ZOO DEFENSE — BOARD GATES SET for a top-down 2D tower defense board. Match the attached Zoo Defense style: chunky rounded shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one highlight, cute not scary.
One image, 2 COLUMNS x 1 ROW, EXACTLY TWO separate objects, each centered in its own square cell with at least 100px transparent gap between them. Both seen from a slightly elevated front view, compact square-ish footprint so each fits on one board tile.
LEFT: MONSTER SPAWN PORTAL — a small rocky cave mouth / stone archway made of rounded grey-purple boulders #7A6A92, with a swirling purple magic vortex #6E46A0 inside fading to a dark violet centre #28193C, two tiny glowing yellow cartoon eyes peeking from the dark, a few small mossy patches on the stones.
RIGHT: ZOO GATE — a cute wooden zoo entrance arch with two thick log posts #7A4E2D, a curved wooden sign board on top with a cream paw-print emblem (no text), small double wooden doors slightly open, a big glossy red cartoon heart #FF4D6D hanging in the centre of the arch, a few green leaves wrapped on the posts.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no ground, no grass patch, no path, no shadow on the background, no particles.
```

## 3. Nút TRIỆU HỒI

Game tự viết chữ "TRIỆU HỒI" và giá kẹo lên nút, nên giữa nút phải trơn.

```text
ZOO DEFENSE — BIG SUMMON BUTTON. Match the attached Zoo Defense UI style: chunky rounded shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight.
One single wide rounded-rectangle game button, about 2.5:1 wide, bright sunny yellow #FFC21A face with a glossy lighter top half, a thick darker orange-brown bottom lip that makes it look raised and pressable. Decorate only the far left and far right ends: a small white paw print with a tiny "+" sparkle shape on the left end, a small cream paw print on the right end. The CENTER 70% of the button must stay EMPTY and flat (no text, no icon) because the game writes the label and price on it.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no numbers, no particles, no glow outside the outline.
```

## 4. Ngôi sao và bóng bay kẹo (1 sheet, 2 cột)

Cắt: trái `star.png`, phải `balloon.png`.

```text
ZOO DEFENSE — SMALL GAME OBJECTS SET. Match the attached Zoo Defense style: chunky rounded shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight.
One image, 2 COLUMNS x 1 ROW, EXACTLY TWO separate objects, each centered in its own cell with at least 100px transparent gap.
LEFT: GOLD STAR — one chunky five-point star with very rounded tips, golden yellow #FFD23F with a lighter top-left highlight and an orange #F2A31B lower shade, simple and bold, readable at 32px.
RIGHT: CANDY BALLOON — one round glossy pink party balloon #FF7AC8, slightly taller than wide, a big white shine spot top-left, a small knot at the bottom and a short curly brown string, with a wrapped pink-and-white swirl candy tied to the end of the string. Portrait composition.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no particles, no glow, no background shadow.
```

## 5. Logo "ZOO DEFENSE"

AI hay viết sai chữ: gen vài lần và chọn bản đúng chính tả. Nếu không ưng, cứ để game dùng chữ vẽ sẵn.

```text
ZOO DEFENSE — GAME LOGO. Cute chunky cartoon mobile game logo, the words "ZOO DEFENSE" spelled exactly like that, two lines or one wide line, big rounded bubbly letters, warm sunny yellow #FFD23F to orange #FFB020 gradient fill, thick dark-brown #3B2412 outline, a white glossy highlight on the top of each letter, slight 3D bottom bevel in darker orange. Decorate with a small wooden zoo-gate arch behind the word "ZOO" and a tiny round paw-print shield replacing the "O" in "DEFENSE". Match the attached Zoo Defense style: thick dark-brown outline, flat cel shading.
FULL ALPHA TRANSPARENT BACKGROUND, wide landscape composition, no other text, no characters, no sparkles or particles floating around, no glow halo outside the outline, no drop shadow on the background.
```

## 6. Ruy-băng Thắng / Thua (1 sheet, 1 cột × 2 hàng)

Cắt: trên `ribbon.png`, dưới `ribbon_defeat.png`.

```text
ZOO DEFENSE — UI BANNER RIBBON SET. Match the attached Zoo Defense UI icon style: chunky rounded cartoon shapes, thick dark-brown outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight.
One image, 1 COLUMN x 2 ROWS, EXACTLY TWO wide horizontal ribbon banners, each centered in its own wide cell with at least 100px transparent gap between them. Each banner: a long rounded center band about 5:1 wide, with folded swallow-tail ends tucked behind on the left and right. The center band must stay EMPTY and flat (no text, no icons) because the game writes the title on it.
TOP: VICTORY ribbon — warm golden yellow #F2A31B band with a lighter top highlight strip, darker orange folded tails, two tiny gold stars attached on the band edges.
BOTTOM: DEFEAT ribbon — dusty blue #5468A8 band, darker navy tails, plain, slightly worn, no stars.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no background shadow.
```

## 7. Bục đứng của thú

```text
ZOO DEFENSE — CHARACTER DISPLAY PODIUM. Match the attached Zoo Defense style: thick dark-brown outline, flat cel shading, saturated friendly colours.
One single wide oval stage seen from a slightly elevated front view, about 3.3:1 wide: a round grassy top #8FD06A with a few tiny flowers painted flat on the surface, standing on a thick tree-stump / wooden rim #7A4E2D with carved plank lines and two small wooden fence posts at the far left and right edges. The top surface is clean and mostly flat so five cartoon animals can stand on it.
FULL ALPHA TRANSPARENT BACKGROUND, no characters, no text, no sparkles, no ground shadow, no glow.
```

## 8. Nút CHIẾN ĐẤU

```text
ZOO DEFENSE — BIG PRIMARY BUTTON. Match the attached Zoo Defense UI style: chunky rounded shapes, thick dark-brown outline, flat cel shading with one shadow tone and one glossy highlight.
One single very wide rounded-rectangle game button, about 3.6:1, bright golden orange #FFB020 face with a glossy lighter top half, a thick darker orange-brown bottom lip that makes it look raised and pressable, small decorative paw prints embossed at the far left and far right corners. The CENTER of the button must stay EMPTY and flat (no text, no icon) because the game writes the label.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no glow outside the outline.
```

## 9. Tia sáng (không bắt buộc)

```text
Soft radial sun-burst light rays, 16 tapered rays radiating from the exact center of a square image, pure white rays fading to transparent toward the edges and near the center, smooth soft edges, no other shapes. FULL ALPHA TRANSPARENT BACKGROUND, no color, no text.
```

## 10. Âm thanh `send.wav`

Dùng công cụ tạo SFX (ví dụ ElevenLabs Sound Effects), xuất WAV mono 44.1 kHz, đặt tên `send.wav` vào `Assets/ZooDefense/Resources/ZooAudio/`. Âm này phát khi anh gửi quái sang đối thủ và khi có quái bị dội lại.

```text
Short cute cartoon "whoosh-pop" sound for a casual mobile game: a quick rising magical swoosh followed by a soft bubbly pop, playful and light, like tossing a little monster through a portal. Duration 0.6 seconds, clean, no music, no voice, no reverb tail.
```
