# Zoo Defense — Asset tùy chọn (đợt 5): thanh tab, gacha, nút kết quả

Các màn mới đã chạy được mà không cần những file này: chỗ nào thiếu ảnh thì code tự vẽ thay hoặc dùng ảnh có sẵn. Khi anh thả đúng tên file vào `Assets/ZooDefense/Resources/ZooArt/UI/`, game tự dùng ảnh thật.

PNG nền trong suốt. Khi gen, đính kèm `ui_icons.png`, `star.png` và `chest_gold.png` làm mẫu nét vẽ.

| # | File | Dùng ở | Kích thước | Đang tạm dùng |
|---|---|---|---|---|
| 1 | `nav_shop`, `nav_deck`, `nav_battle`, `nav_pass`, `nav_settings` | Icon 5 tab ở thanh dưới | 256 × 256 mỗi icon | rương vàng / thú đầu đội / cúp / XP / bánh răng vẽ |
| 2 | `gacha_egg` | Quả trứng rung rồi nổ khi quay thú | 640 × 640 | trứng tím sọc vẽ bằng code |
| 3 | `card_back` | Mặt sau thẻ thưởng trước khi lật | 400 × 560 (tỷ lệ 5 : 7) | khung tím + ngôi sao + "?" |
| 4 | `icon_ad` | Nút "x2 vàng – xem quảng cáo" | 128 × 128 | ô trắng + tam giác xanh |
| 5 | `icon_replay` | Nút "Chơi tiếp" | 256 × 256 | cúp |
| 6 | `vs_badge` | Huy hiệu VS ở màn ghép trận | 640 × 640 | đĩa vàng + chữ VS vẽ bằng code |

---

## 1. Icon 5 tab (1 ảnh, 5 cột × 1 hàng)

Cắt theo thứ tự trái sang phải: `nav_shop`, `nav_deck`, `nav_battle`, `nav_pass`, `nav_settings`.

```text
ZOO DEFENSE — BOTTOM NAVIGATION ICON SET. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight, saturated friendly colours, readable at 64px.
One image, 5 COLUMNS x 1 ROW, EXACTLY FIVE separate icons, each centered in its own square cell with at least 80px transparent gap between them. Each icon is a single bold object, no frame, no circle, no badge, no text.
1) SHOP — a small striped market stall awning (red and cream stripes) over a pile of gold paw coins and a blue gem.
2) DECK — three fanned cartoon cards, the front card shows a cute orange fox face, gold card borders.
3) BATTLE — two crossed cartoon wooden swords with gold guards behind a small round shield with a paw print.
4) SEASON PASS — a purple star-shaped medal with a white paw print, hanging from a short gold-and-purple ribbon.
5) SETTINGS — a chunky light-grey cog wheel with rounded teeth and a darker centre hole.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no numbers, no sparkles floating between icons, no background shadow.
```

## 2. Quả trứng gacha

```text
ZOO DEFENSE — MAGIC GACHA EGG. Match the attached Zoo Defense style: thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight, cute and premium.
One single large cartoon egg, slightly taller than wide, standing upright: glossy violet shell #9B5CFF with two wavy bands (gold #FFD23F and pink #FF7AC8) around the middle, a big gold star emblem with a white paw print on the front, a few tiny cracks with warm golden light leaking out near the top, a big white highlight on the upper left. Centered, filling most of a square canvas.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no characters, no sparkles or particles around it, no ground shadow, no glow outside the outline.
```

## 3. Mặt sau thẻ thưởng

Game tự vẽ khung viền tím quanh ảnh này, nên ảnh chỉ cần phần ruột thẻ, phủ kín toàn bộ.

```text
ZOO DEFENSE — REWARD CARD BACK, portrait 5:7, artwork fills the WHOLE canvas edge to edge (no outer frame, no rounded transparent corners). Match the attached Zoo Defense style: flat cel shading, dark-brown outlines on the emblem.
Deep violet background #4A2A80 with a subtle lighter radial glow in the center and a faint repeating pattern of small paw prints and tiny stars in slightly lighter violet. In the exact center: a gold round emblem with a white paw print and a small gold star above it. Symmetrical, calm, readable at small size.
No text, no letters, no numbers, no characters, no border frame.
```

## 4. Icon quảng cáo

```text
ZOO DEFENSE — "WATCH AD" ICON. Match the attached Zoo Defense UI style: thick dark-brown outline, flat cel shading with one glossy highlight.
One small rounded-square TV / video badge, cream white #FFF6E0 face with a bold green #4CC46B play triangle in the center, tiny antenna-free, very simple and readable at 40px.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles.
```

## 5. Icon chơi tiếp

```text
ZOO DEFENSE — "PLAY AGAIN" ICON. Match the attached Zoo Defense UI style: thick dark-brown outline, flat cel shading with one glossy highlight.
One chunky circular refresh arrow (a thick round arrow looping clockwise with a big arrowhead), bright sky blue #5BC2FF with a white highlight, a small orange paw print in the middle of the loop. Simple, bold, readable at 64px.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no background shadow.
```

## 6. Huy hiệu VS (màn ghép trận)

```text
ZOO DEFENSE — "VS" BATTLE BADGE. Match the attached Zoo Defense UI style: chunky rounded shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight.
One round badge: a golden yellow #FFC21A shield-circle with a jagged starburst rim in orange #F2A31B, the letters "VS" spelled exactly like that in big bold white bubbly letters with a thick dark-brown outline across the center, a tiny blue paw print on the left and a tiny red paw print on the right. Centered on a square canvas.
FULL ALPHA TRANSPARENT BACKGROUND, no other text, no particles, no glow outside the outline, no background shadow.
```
