# Zoo Defense — Asset tùy chọn (đợt 7): icon chế độ chơi

Không có các file này game vẫn chạy: mỗi chế độ dùng tạm một icon có sẵn (ngôi sao, tim, rương, kẹo, thiên thạch, cúp) đặt trên một đĩa màu. Khi thả đúng tên file vào `Assets/ZooDefense/Resources/ZooArt/UI/`, game tự dùng icon thật ở màn Chế độ chơi, thanh mục tiêu trong trận và màn kết quả.

| File | Chế độ | Kích thước |
|---|---|---|
| `mode_daily.png` | Thử thách ngày | 256 × 256 |
| `mode_endless.png` | Sinh tồn vô tận | 256 × 256 |
| `mode_campaign.png` | Phiêu lưu | 256 × 256 |
| `mode_mayhem.png` | Đấu trường luật lạ | 256 × 256 |
| `mode_raid.png` | Raid boss | 256 × 256 |
| `mode_tournament.png` | Giải đấu | 256 × 256 |

Gen 1 ảnh gồm 6 cột × 1 hàng, rồi cắt theo thứ tự trái sang phải như bảng. Đính kèm `ui_icons.png`, `star.png` và `chest_gold.png` làm mẫu nét vẽ.

```text
ZOO DEFENSE — GAME MODE ICON SET. Match the attached Zoo Defense UI style exactly: chunky rounded cartoon shapes, thick dark-brown #3B2412 outline, flat cel shading with one shadow tone and one glossy highlight, saturated colours, readable at 64px.
One image, 6 COLUMNS x 1 ROW, EXACTLY SIX separate icons, each centered in its own square cell with at least 80px transparent gap. Each icon is one bold object, no frame, no circle, no text.
1) DAILY CHALLENGE — a tear-off calendar page with a big gold star on it, orange top binding.
2) ENDLESS — a glowing purple infinity loop made of a curled cartoon tail with a small paw at one end.
3) ADVENTURE — a rolled treasure map with a red dotted path and a small green flag on top.
4) MAYHEM ARENA — a pink jester party hat with a swirly candy on top, two tiny sparkles.
5) BOSS RAID — a giant cracked monster skull-shaped helmet in red, two crossed paw-print banners behind it.
6) TOURNAMENT — a shiny gold cup with two handles and a blue ribbon, a paw print on the cup.
FULL ALPHA TRANSPARENT BACKGROUND, no text, no letters, no numbers, no particles between icons, no background shadow.
```
