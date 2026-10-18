# Zoo Defense — Asset cho màn Home và Kết quả (tùy chọn)

Màn Home và Thắng/Thua mới đã chạy được ngay, toàn bộ vẽ bằng code. Khi có các ảnh dưới đây, game **tự thay** bằng ảnh thật.
Tất cả đặt vào `Assets/ZooDefense/Resources/ZooArt/UI/`, PNG nền trong suốt. Khi tạo, đính kèm `missing-assets/PNG/ui_icons.png` làm tham chiếu nét vẽ.

| File | Dùng ở | Kích thước gợi ý |
|---|---|---|
| `logo.png` | Home, trên cùng | 1520 × 340 |
| `ribbon.png` | Tên đấu trường (Home), "CHIẾN THẮNG" | 1720 × 340, giữa để trống cho chữ |
| `ribbon_defeat.png` | "THẤT BẠI" | 1720 × 340 |
| `podium.png` | Bục đứng của 5 thú ở Home | 1320 × 400 |
| `button_battle.png` | Nút CHIẾN ĐẤU | 1440 × 400, giữa để trống cho chữ |
| `rays.png` | Tia sáng sau nút Chiến đấu và màn Thắng | 1024 × 1024, trắng (game tự tô màu) |

Cũng nên làm luôn các asset đã liệt kê trong `PROMPTS_MISSING_ASSETS.md` (kim cương, XP, 4 rương) vì chúng xuất hiện ở cả hai màn này.

---

## 1. Logo "ZOO DEFENSE"

AI hay viết sai chữ: tạo vài lần và chọn bản đúng chính tả. Nếu không ưng, cứ để game dùng chữ vẽ sẵn.

```text
ZOO DEFENSE — GAME LOGO. Cute chunky cartoon mobile game logo, the words "ZOO DEFENSE" spelled exactly like that, two lines or one wide line, big rounded bubbly letters, warm sunny yellow #FFD23F to orange #FFB020 gradient fill, thick dark-brown #3B2412 outline, a white glossy highlight on the top of each letter, slight 3D bottom bevel in darker orange. Decorate with a small wooden zoo-gate arch behind the word "ZOO" and a tiny round paw-print shield replacing the "O" in "DEFENSE". Match the attached Zoo Defense style: thick dark-brown outline, flat cel shading.
FULL ALPHA TRANSPARENT BACKGROUND, wide landscape composition, no other text, no characters, no sparkles or particles floating around, no glow halo outside the outline, no drop shadow on the background.
```

## 2. Ribbon (thắng và thua)

```text
ZOO DEFENSE — UI BANNER RIBBON SET. Match the attached Zoo Defense UI icon style: chunky rounded cartoon shapes, thick dark-brown outline of consistent weight, flat cel shading with one shadow tone and one glossy highlight.
One image, 1 COLUMN x 2 ROWS, EXACTLY TWO wide horizontal ribbon banners, each centered in its own wide cell with at least 100px transparent gap between them. Each banner: a long rounded center band about 5:1 wide, with folded swallow-tail ends tucked behind on the left and right. The center band must stay EMPTY and flat (no text, no icons) because the game writes the title on it.
TOP: VICTORY ribbon — warm golden yellow #F2A31B band with a lighter top highlight strip, darker orange folded tails, two tiny gold stars attached on the band edges.
BOTTOM: DEFEAT ribbon — dusty blue #5468A8 band, darker navy tails, plain, slightly worn, no stars.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no background shadow.
```

## 3. Bục đứng của thú

```text
ZOO DEFENSE — CHARACTER DISPLAY PODIUM. Match the attached Zoo Defense style: thick dark-brown outline, flat cel shading, saturated friendly colours.
One single wide oval stage seen from a slightly elevated front view, about 3.3:1 wide: a round grassy top #8FD06A with a few tiny flowers painted flat on the surface, standing on a thick tree-stump / wooden rim #7A4E2D with carved plank lines and two small wooden fence posts at the far left and right edges. The top surface is clean and mostly flat so five cartoon animals can stand on it.
FULL ALPHA TRANSPARENT BACKGROUND, no characters, no text, no sparkles, no ground shadow, no glow.
```

## 4. Nút CHIẾN ĐẤU

```text
ZOO DEFENSE — BIG PRIMARY BUTTON. Match the attached Zoo Defense UI style: chunky rounded shapes, thick dark-brown outline, flat cel shading with one shadow tone and one glossy highlight.
One single very wide rounded-rectangle game button, about 3.6:1, bright golden orange #FFB020 face with a glossy lighter top half, a thick darker orange-brown bottom lip that makes it look raised and pressable, small decorative paw prints embossed at the far left and far right corners. The CENTER of the button must stay EMPTY and flat (no text, no icon) because the game writes the label.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no glow outside the outline.
```

## 5. Tia sáng (không bắt buộc, game đã tự tạo)

```text
Soft radial sun-burst light rays, 16 tapered rays radiating from the exact center of a square image, pure white rays fading to transparent toward the edges and near the center, smooth soft edges, no other shapes. FULL ALPHA TRANSPARENT BACKGROUND, no color, no text.
```
