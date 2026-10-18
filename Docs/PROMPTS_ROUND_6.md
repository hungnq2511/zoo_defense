# Zoo Defense — Asset tùy chọn (đợt 6): icon hệ

Không có các file này game vẫn chạy: popup Khắc hệ và ô đợt sẽ dùng chấm màu và ảnh thú mẫu. Khi thả file vào `Assets/ZooDefense/Resources/ZooArt/UI/`, game tự dùng icon thật cho popup Khắc hệ, ô đợt trên HUD và chấm hệ.

| File | Kích thước |
|---|---|
| `element_fire.png`, `element_water.png`, `element_grass.png`, `element_light.png` | 256 × 256 mỗi icon, PNG nền trong suốt |

Gen 1 ảnh gồm 4 cột × 1 hàng, rồi cắt ra theo thứ tự trái sang phải: fire, water, grass, light. Đính kèm `ui_icons.png` và `star.png` làm mẫu nét vẽ.

```text
ZOO DEFENSE — ELEMENT EMBLEM ICON SET. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight, readable at 32px.
One image, 4 COLUMNS x 1 ROW, EXACTLY FOUR separate emblems, each centered in its own square cell with at least 80px transparent gap. Each emblem is ONE simple bold symbol, no circle frame, no badge, no text.
1) FIRE — a plump three-tongue flame, orange #FF6B3D outside, yellow core.
2) WATER — a single fat water droplet, sky blue #3DB8FF, white highlight.
3) GRASS — two round leaves on a short stem, green #6BCB3D.
4) LIGHT — a chunky four-point sparkle star, warm yellow #FFD166, white center.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no particles, no background shadow.
```
