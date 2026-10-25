# Zoo Defense — Nghiên cứu gameplay & đề xuất (24/10/2026)

## 1. Cách đo
Cho 2 bot khó nhất đánh nhau hàng nghìn trận, bộ bài ngẫu nhiên, 3 kiểu bộ gửi quái. Đo: độ dài trận, đợt nào mất tim,
tim mất do quái gửi hay quái đợt, tỉ lệ thắng khi có từng thú trong bộ bài.
Lưu ý: bot chơi chưa giống người thật — số liệu chỉ để tìm điểm lệch lớn, cần kiểm lại bằng analytics sau khi phát hành
(event `match_end`, `mode_end`).

## 2. Vấn đề tìm thấy và đã sửa (protocol 3)

| Vấn đề | Trước | Sau | Cách sửa |
|---|---|---|---|
| "Rush" gửi quái ngay đầu trận bằng 100 kẹo khởi điểm: đối thủ chưa kịp xếp thú | 13% trận kết thúc < 2 phút; đợt 1–2 chiếm 21% tổng tim mất | 3% trận < 2 phút; đợt 1 gần như không mất tim | Gửi quái mở từ **đợt 2** (`sendUnlockWave`). Nút gửi hiện "Đợt 2"; bot cũng tuân theo |
| Hệ Lửa áp đảo: 2 quái gửi dùng nhiều nhất (Rat Rush, Brick Golem) đều hệ Cỏ, mà Lửa khắc Cỏ | Thú Lửa thắng ~55–70% | Lửa 44–57% | Brick Golem → hệ Nước, Shield Turtle → hệ Lửa |
| Phượng Hoàng Nhí: nhiều con trên bàn cộng dồn, mỗi con +1 cấp cho thú Lửa | 70,5% thắng | 56% | Chỉ 1 lần ban phước mỗi đợt dù có nhiều con |
| Thú quá mạnh / quá yếu | Khoảng 39,4% – 70,5% | 42,4% – 57,5% | Chỉnh số (bảng dưới) |
| Trận hơi dài sau khi bỏ rush | Trung vị 5,3 phút | 5,0 phút | Đột tử từ đợt **12** (trước 13) |

Chỉnh số thú: Baby Dragon 40→34 sát thương · Rainbow Unicorn 48→40 · Aegis Ram khiên +30%→+22% ·
Chubby Panda 5→10 kẹo/cấp · Moon Moth 30→38 · Snap Shrimp 14→16 · Ice Penguin 15→20 · Snow Seal 22→25 ·
Night Owl hào quang 20%→25% · Lava Salamander 24→27, bỏng 6→8 · Leaf Gecko 16→22, tốc đánh 50→44, choáng 12→18 ·
Spike Hedgehog 20→23 · Coral Axolotl 20→24.

Kết quả chung (4.000 trận bot khó): trận dài trung vị 5,0 phút (p10 3,8 — p90 6,0), 19% trận lật kèo sau khi bị dẫn
≥4 tim, quái gửi gây ~2/3 số tim mất (PvP vẫn là trọng tâm).

**Tương thích:** `ProtocolVersion` 2 → 3, nên bản mới chỉ ghép trận với bản mới. Replay cũ trên máy bị xoá
(chuỗi lệnh cũ phát lại sẽ sai với luật mới).

## 3. Tính năng mới đã làm
- **Thống kê trận:** nút "Thống kê" ở màn kết quả (PvP và các chế độ khác) và ở danh sách Phát lại. Sát thương của
  từng thú cho cả hai người chơi, **MVP**, % đóng góp, sát thương phép, số tim lấy được bằng quái gửi, số lần gộp, số phép.
  Số liệu tính bằng cách phát lại trận nên khớp tuyệt đối.
- Mô phỏng có thêm `Board.DamageByUnit` / `SpellDamage` (không ảnh hưởng hash trạng thái).

## 3b. Bản chiến thuật (protocol 4)
**Talent cho thẻ** (cấp thẻ 9 mở bậc 1, cấp 12 mở bậc 2; mỗi bậc chọn 1 trong 2 nhánh; đổi miễn phí ngoài trận).
Nhánh theo vai trò của thú:

| Vai trò | Bậc 1 | Bậc 2 |
|---|---|---|
| Sát thương | Thợ Săn Boss (+90% vs tinh anh/boss) · Đánh Chặn (+50% vs quái gửi) | Chí Mạng (mỗi đòn thứ 3 x2) · Bậc Thầy Khắc Hệ (đòn khắc hệ +75%) |
| Diện rộng | Nổ Lan (bán kính +30%) · Tâm Nổ (+35% mục tiêu chính) | Khói Băng (chậm 12%/1 s) · Phá Giáp (-12% giáp/2 s) |
| Khống chế | Kéo Dài (chậm/choáng +60%) · Ngòi Độc (+25%) | Khắc Tinh Boss (choáng boss không giảm, +20% vs boss) · Đánh Dấu (+7% sát thương nhận vào/2 s) |
| Hỗ trợ | Hào Phóng (+3 kẹo/cấp mỗi đợt) · Kiên Cường (boss không làm choáng/khóa ô) | Cổ Vũ (thú kề +10%) · Nạp Nhanh (kỹ năng nạp nhanh 30%) |

Kiểm bằng bot (4.000 trận, thẻ cấp 12, talent ngẫu nhiên): mọi talent thắng 46,6–54,4%, không nhánh nào áp đảo.
Bot cũng có talent theo cấp thẻ của chúng. Chế độ cố định cấp thẻ (Gương, Giải đấu, Thử thách ngày) không dùng talent.

**Cơ chế chiến thuật mới trong trận**
- **Chọn mục tiêu** cho từng thú (bảng thông tin thú → nút "Bắn"): Con đầu · Chỗ đông · Con mạnh · Quái gửi.
- **Thả thú** (chạm 2 lần để xác nhận): hoàn 40% giá trị thẻ trong con thú. Dùng để đổi đội hình khi bị yểm bùa hay để chỗ gộp.
- **Bùa Đổi Hệ** (nút tím cạnh nút gửi quái, mở từ đợt 3): trả kẹo (70, rồi +35 mỗi lần) để mọi quái thường ở đợt tới của
  đối thủ mang hệ bạn chọn. Bảng chọn cho thấy sát thương của bàn đối thủ với từng hệ (hệ yếu nhất được tô xanh).
  Đối thủ được báo ngay (băng rôn + vòng tím ở ô hệ đợt tới) để kịp thả/đổi thú. Mỗi đợt tối đa 1 lần, không chồng bùa.
  Bot khó tự dùng bùa; tỉ lệ thắng của bot dùng bùa ~50% (mạnh nhưng không phá game).

**Thời lượng trận theo cấp thẻ:** trước đây cấp thẻ càng cao trận càng dài (cấp 1: 5:11 → cấp 15: 7:14).
Nay máu quái PvP theo sức mạnh thẻ trung bình của cả hai người (`waveHpCardScalePercent`): mọi cấp đều ~5:10.
Ai có thẻ cao hơn đối thủ vẫn mạnh hơn.

**Mayhem: 8 luật xoay vòng 8 tuần** — thêm Chớp Nhoáng (quái nhanh 40%, đợt ngắn, +50% kẹo đầu),
Boss Liên Hoàn (boss mỗi 2 đợt, thưởng đua boss x2), Đơn Hệ (cả tuần một hệ, khắc hệ +50%, hệ báo trước trên thẻ
chế độ), Chiến Tranh Bùa (Bùa Đổi Hệ từ đợt 2, nửa giá).

**Tương thích:** `ProtocolVersion` 4, replay cũ trên máy bị xoá (khóa `zoo_replays_v3`).

## 3c. Độ hiếm 6 bậc C · B · A · S · SS · SSS (protocol 5)

| Hạng | Thú | Gacha / lượt | Mảnh / lượt | Hào quang khi có trên bàn |
|---|---|---|---|---|
| C | Gà Trống Pháo Hoa, Ếch Bong Bóng, Chim Cánh Cụt Băng, Nhím Gai, Thỏ Cà Rốt, Lười Dây Leo, Chim Sẻ Tia Nắng, Tôm Búng Nước, Ong Mật | 50% | 15 | — |
| B | Cáo Lửa, Kỳ Nhông Dung Nham, Hải Cẩu Tuyết, Gấu Trúc Mập, Tắc Kè Lá, Heo Rừng Than, Kỳ Giông San Hô, Bướm Đêm Nguyệt Quang | 28% | 6 | — |
| A | Sóc Than Hồng, Rái Cá Pháo, Bạch Tuộc Mực, Mèo May Mắn, Cá Heo Sóng, Cú Đêm | 16% | 2 | — |
| S | Rồng Con, Hươu Sao Băng, Chuột Chũi Thợ Rèn, Voi Mầm | 4,5% | 1 | — |
| SS | Phượng Hoàng Nhí, Cừu Hộ Vệ | 1,2% | 2 | cả đội +5% sát thương |
| SSS | Kỳ Lân Cầu Vồng | 0,3% | 3 | cả đội +8% sát thương, +8% tốc đánh |

- Bảo hiểm: S trở lên trong 40 lượt, SS trở lên trong 80 lượt, SSS trong 200 lượt (mỗi bộ đếm reset khi ra hạng đó).
  Trung bình ~47 lượt (~4.200 kim cương) cho một SS, ~151 lượt (~13.600 kim cương) cho SSS; tệ nhất 18.000 kim cương.
- Rương: Bạc 0,2% SS · Vàng 0,8% SS / 0,1% SSS · Phép 4% SS / 0,8% SSS. Cửa hàng vàng không bán S trở lên.
- Mảnh để nâng cấp: chia theo hạng 1 · 3 · 8 · 20 · 40 · 80 (hạng cao cần rất ít mảnh mỗi cấp, nhưng mảnh cực hiếm).
- Hào quang không cộng dồn (chỉ tính hạng cao nhất trên bàn). Số liệu ở `rarityAuraDamagePercent` / `rarityAuraSpeedPercent`
  (chỉnh được bằng Remote Config `balance_overrides`).
- Kiểm bằng bot (cùng cấp thẻ): tỉ lệ thắng khi có thú hạng đó trong deck C 48% · B 48% · A 53% · S 55% · SS 56–58% · SSS 64–66%.
  Bot ít khi có SS/SSS và tối đa 1 con mỗi deck.
- **Lưu ý cân bằng PvP:** SSS mạnh rõ rệt (đó là mục tiêu), nhưng ghép trận theo MMR ẩn sẽ đưa người có SSS lên gặp đối thủ mạnh hơn.
  Theo dõi tỉ lệ thắng thật sau khi phát hành; nếu quá cao, giảm hào quang SSS qua Remote Config.
- Hiển thị: huy hiệu hạng trên thẻ (S+ nhấp nháy, SSS cầu vồng), banner gacha SSS giữa / SS hai bên, bảng tỉ lệ đủ 6 hạng + bảo hiểm,
  màn mở thưởng SS/SSS chậm hơn, loé màu và nhạc thắng; trong trận báo "Hào quang hạng SSS".

## 3d. Cấp thú trong trận + Tiến hoá (protocol 6)

Vấn đề: lên cấp chỉ bằng gộp 2 thú giống nhau cùng cấp, nên cấp 7 cần 64 con. Đo bằng bot (gộp liên tục như người chơi):
cấp 4 đạt 96% bàn, cấp 5 20%, cấp 6 2%, cấp 7 1%. Các nâng cấp kỹ năng "Lv4/Lv7" và hình tiến hoá cấp 7 gần như không ai thấy.

Sửa: **Tiến hoá** — trong bảng thông tin thú, nút vàng "Tiến hoá" trả kẹo để lên 1 cấp (không cần thú cặp, không cần ô trống).
Giá = giá thẻ hiện tại × phần sát thương cấp đó thêm vào × `evolvePricePercent` (40%). Ví dụ giữa trận (giá thẻ ~100):
Lv1→2 ≈ 44 · Lv3→4 ≈ 60 · Lv5→6 ≈ 108 · Lv6→7 ≈ 140 kẹo; cuối trận đắt hơn theo giá thẻ.
Nút "triệu hồi thêm con này" ở bảng thông tin được thay bằng Tiến hoá (thanh deck bên dưới vẫn chọn thú để triệu hồi).

| Cấp | Trước | Sau (bot dùng Tiến hoá) | Lần đầu đạt (trung vị) |
|---|---|---|---|
| 3 | 30% | 86% | 3:05 |
| 4 | 1% | 74% | 3:28 |
| 5 | 0% | 57% | 4:06 |
| 6 | 0% | 32% | 4:45 |
| 7 | 0% | 12% | 5:05 |

- Bot dùng Tiến hoá thắng bot không dùng 61% → đáng dùng nhưng không bắt buộc tuyệt đối.
- Máu quái tăng mỗi đợt 9% → 10% để trận vẫn ~5 phút (4:45–5:14 ở mọi cấp thẻ). Thang hạng giữ nguyên
  (C 48 · B 48 · A 52 · S 55 · SS 57 · SSS 64%).

## 4. Đề xuất tiếp theo (theo thứ tự đáng làm)
Tham khảo Rush Royale (talent của thẻ, clan, sự kiện boss mini có luật riêng), Random Dice (Crew Battles, Co-op Boss Raid,
Mirror Mode, sự kiện "Hidden Dice Mode") và bài phân tích chỉ số game thủ thành của GameAnalytics (D7 là chỉ số quan trọng;
tương tác giữa các tháp và nội dung ra đều đặn giữ người chơi).

| # | Tính năng | Vì sao | Công sức |
|---|---|---|---|
| ✓ | ~~Thêm luật Mayhem~~ · ~~Talent cho thẻ~~ | Đã làm (mục 3b) | — |
| 3 | **Chia sẻ replay bằng mã** (gửi bạn bè xem trận) | Replay chỉ là danh sách lệnh (vài KB), lưu Cloud Save là đủ; tăng tính xã hội | Vừa |
| 4 | **Hội (Clan)**: mục tiêu tuần chung, rương hội, giao hữu trong hội | Giữ chân mạnh nhất ở thể loại này; dùng UGS Lobby/Cloud Save | Lớn |
| 5 | **Thú mới hằng tháng** (mở sớm bằng gói, miễn phí sau ~1 tháng) | Gợi ý của GameAnalytics: nội dung đều đặn, không pay-to-win lâu dài | Vừa (cần art) |
| 6 | **Bot thông minh hơn ở cúp cao** (dùng phép đúng lúc, dội quái) | Người chơi mới gặp bot nhiều; bot yếu làm trận nhạt | Vừa |

Nguồn: https://about.my.games/news/244 · https://www.apkmirror.com/?p=8811773 ·
https://gameanalytics.com/blog/metrics-behind-successful-tower-defense-games/

## 5. Công cụ đo
Bộ đo bot nằm ngoài dự án (không cần cho build). Chạy lại sau mỗi lần chỉnh cân bằng; nếu cần đưa vào dự án làm
Editor menu, có thể thêm sau.
