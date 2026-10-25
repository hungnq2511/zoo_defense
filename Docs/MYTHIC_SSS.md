# Zoo Defense — 15 thú SSS thần thoại (đợt 8)

Thêm ngày 25/10/2026. Tất cả 15 thú đều hạng **SSS**, nên game hiện có **16 thú SSS** (tính cả Kỳ Lân Cầu Vồng). Mỗi con có một kỹ năng riêng, được nâng cấp ở cấp 4 và cấp 7.

- Ảnh thú: `Resources/ZooArt/Units/{id}_{1,4,7}.png`
- Ảnh chưởng: `Resources/ZooArt/Projectiles/{id}.png`
- Icon bùa hệ: `Resources/ZooArt/UI/icon_hex.png`

## Danh sách

| id | Tên | Hệ | Vai trò | Kỹ năng |
|---|---|---|---|---|
| `celestial_qilin` | Kỳ Lân Thiên Giới | Ánh sáng | Diện rộng | **Mưa sao**: đòn thứ 6 gọi 2 sao (cấp 4: 3) rơi vào chỗ đông quái nhất, mỗi sao 100%. Cấp 7: đòn thứ 4 |
| `abyss_leviathan` | Thủy Long Vực Thẳm | Nước | Sát thương | **Giáo thủy triều**: +25% lên quái còn trên nửa máu. Đòn thứ 5 tạo sóng thần đánh cả đoạn đường phía sau mục tiêu (70%) |
| `solar_lion` | Sư Tử Mặt Trời | Lửa | Sát thương | **Lửa mặt trời**: đốt cộng dồn, lửa lan sang 1 quái (cấp 7: 2). +30% lên quái đang cháy |
| `worldtree_tortoise` | Rùa Cổ Thụ | Cỏ | Hỗ trợ | **Cây thế giới**: +20 kẹo mỗi cấp mỗi đợt. Cứ 4 đợt (cấp 7: 3 đợt) +1 cấp cho 1 thú ngẫu nhiên. Cấp 4: phủ khiên cho 2 thú |
| `ninetail_star_fox` | Hồ Ly Cửu Vĩ Tinh Tú | Ánh sáng | Khống chế | **Mê hoặc**: đòn thứ 4 làm quái đi lùi 0,7 giây (boss bị chậm). Cấp 4: thêm 1 quái. Cấp 7: đòn thứ 3 |
| `thunder_griffin` | Ưng Sư Sấm Sét | Ánh sáng | Sát thương | **Sét nảy**: sét nảy sang 2 quái (cấp 4: 3) với 60% sát thương. Cấp 7: quái bị sét trúng thì choáng ngắn |
| `glacier_wolf` | Sói Băng Hà | Nước | Khống chế | **Tru băng hà**: làm chậm 25%. Đòn thứ 5 đóng băng quái quanh mục tiêu 0,6 giây |
| `magma_rhino` | Tê Giác Dung Nham | Lửa | Diện rộng | **Húc dung nham**: đòn thứ 4 nổ rất rộng, +80% sát thương, đẩy lùi quái. Cấp 7: vụ nổ để lại lửa |
| `bloom_peacock` | Công Hoa Nở | Cỏ | Hỗ trợ | **Xoè đuôi**: 4 thú gần nhất được +28% tốc đánh và +20% sát thương (cấp 7: 6 thú) |
| `crystal_pangolin` | Tê Tê Pha Lê | Ánh sáng | Diện rộng | **Lăn pha lê**: phá 12% giáp (cấp 4: 22%). +25% lên quái có giáp, tinh anh và boss |
| `lantern_jellyfish` | Sứa Đèn Lồng | Nước | Khống chế | **Đèn lồng**: soi lộ quái tàng hình, làm chậm 20%, đánh dấu +8% sát thương nhận vào |
| `flame_tiger_cub` | Hổ Con Hỏa Văn | Lửa | Sát thương | **Vồ mồi**: +60% lên quái bị gửi tới. Đòn thứ 4 gây gấp đôi sát thương |
| `bamboo_red_panda` | Gấu Mèo Trúc | Cỏ | Sát thương | **Liên hoàn trúc**: mỗi đòn liên tiếp vào cùng một quái +12% (tối đa 5 / 6 / 8 lần) |
| `comet_hamster` | Hamster Sao Chổi | Ánh sáng | Diện rộng | **Mưa sao chổi**: mỗi đòn ném thêm 1 sao chổi (cấp 4: 2, cấp 7: 3) vào quái khác, nổ lan 55% |
| `puddle_duckling` | Vịt Con Vũng Nước | Nước | Khống chế | **Bẫy bong bóng**: làm chậm 30%. Đòn thứ 4 nhốt quái trong bong bóng: choáng 0,8 giây và nhận thêm 15% sát thương |

Cơ chế nằm ở `Board.cs`, phần "mythic". Mã kỹ năng `UnitSkill` là 20–34.

## Cân bằng (cho bot đánh với nhau)

Cách đo: lấy một bộ bài ngẫu nhiên, gắn thú cần đo vào, rồi cho đánh với bộ bài ngẫu nhiên khác. Mỗi con 1.500 trận, thẻ cấp 5.

| Thú | Tỉ lệ thắng |
|---|---|
| Kỳ Lân Cầu Vồng (mốc so sánh) | 63% |
| 15 thú mới | 60–66% |

Theo hạng (3.000 trận, bộ bài ngẫu nhiên):

| Hạng | C | B | A | S | SS | SSS |
|---|---|---|---|---|---|---|
| Tỉ lệ thắng | 47 | 48 | 51,5 | 53 | 54 | 65 |

Độ dài trận vẫn như cũ: trung vị 5:17, chỉ 2% trận dưới 2 phút.

## Gacha

- Tỉ lệ ra SSS là **2%** (đổi ngày 25/10, trước là 0,3%), bảo hiểm sau **100** lượt. SS tăng lên 3% để vẫn dễ ra hơn SSS; C giảm từ 50% xuống 46,5%. Mỗi lần ra SSS sẽ ngẫu nhiên 1 trong 16 con.
- Banner trong cửa hàng **đổi 3 thú SSS mỗi ngày**. Chỉ đổi hình, không đổi tỉ lệ.
- Nếu muốn có banner "tăng tỉ lệ" cho từng con, có thể làm sau.

## Phiên bản

- `LockstepSync.ProtocolVersion = 7`
- Khoá replay `zoo_replays_v6`
- Bản cũ không ghép trận với bản mới.

---

# Đợt 9 — 16 thú thường (hạng C)

Thêm ngày 25/10/2026. Có 4 con cho mỗi hệ, tất cả dùng kỹ năng có sẵn. 12 con có chưởng riêng; 4 con còn lại (Thú Ăn Kiến, Rái Cá, Chim Ruồi, Tắc Kè) dùng chưởng chung theo hệ.

| Hệ | Thú (id) |
|---|---|
| Cỏ | `sprout_anteater` (bắn 2 phát), `leaf_okapi` (đánh thường), `moss_kiwi` (làm chậm, thấy tàng hình), `bloom_snail` (làm chậm diện rộng) |
| Nước | `drop_platypus` (choáng), `brook_moose` (diện rộng + đẩy lùi), `frost_bear` (diện rộng + làm lạnh), `river_otter` (bắn 2 phát) |
| Ánh sáng | `moon_lemur` (hỗ trợ: kẹo mỗi đợt), `star_eagle` (soi tàng hình), `sun_alpaca` (diện rộng), `prism_hummingbird` (đánh rất nhanh) |
| Lửa | `ember_devil` (cháy cộng dồn), `blaze_bison` (diện rộng + phá giáp), `torch_toucan` (đốt), `chili_chameleon` (câm lặng, thấy tàng hình) |

- Tổng cộng 61 thú, trong đó hạng C có 25 con.
- Bot giảm trọng số chọn thẻ C từ 10 xuống 4 cho mỗi con, để bộ bài của bot không toàn thẻ C.
- Kết quả cho bot đánh với nhau (3.000 trận):

| Hạng | C | B | A | S | SS | SSS |
|---|---|---|---|---|---|---|
| Tỉ lệ thắng | 48 | 47 | 52 | 54 | 52 | 63 |

- Thú C mới thắng khoảng 45–51%.
- Trận dài trung vị 5:18.
- `ProtocolVersion = 8`, khoá replay `zoo_replays_v7`.
