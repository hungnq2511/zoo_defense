# Zoo Defense – bản Prototype

Project Unity 6 (2D) dựng theo giai đoạn **Prototype** trong tài liệu thiết kế: một trận PvP đầy đủ với bot, đồ họa tạm sinh bằng code. Khi có asset thật, chỉ cần thả file PNG đúng tên vào thư mục là game tự dùng.

## Mở project

1. Mở **Unity Hub → Add → Add project from disk**, chọn thư mục `ZooDefender`.
2. Mở bằng **Unity 6** (bất kỳ bản 6000.x nào). Nếu Hub báo khác phiên bản, chọn bản Unity 6 bạn đã cài và đồng ý.
3. Lần mở đầu, project tự tạo scene `Assets/ZooDefense/Scenes/Main.unity`, chuyển sang chế độ màn hình dọc. Nếu không thấy, chọn menu **Zoo Defense → Create Main Scene**.
4. Trong cửa sổ **Game**, chọn tỷ lệ dọc (ví dụ 1080×1920), rồi bấm **Play**.

Bấm Play ở bất kỳ scene nào cũng chạy được: game tự khởi tạo nếu scene chưa có `GameApp`.

> Prototype dùng Input Manager cũ. Nếu Console báo lỗi về input, vào **Edit → Project Settings → Player → Active Input Handling** và chọn **Both**.

## Đã có trong bản này

| Phần | Nội dung |
|---|---|
| Trận đấu | Lưới 5×4, đường quái vòng quanh, đợt 20 giây, tinh anh mỗi 3 đợt, boss mỗi 5 đợt (3 loại boss có kỹ năng), Sudden Death từ đợt 15 |
| Quân | 20 thú, 4 hệ khắc chế (Lửa → Cỏ → Nước → Lửa, Ánh Sáng trung tính), 4 vai trò, ghép cấp 1–7, kỹ năng: cháy, lộ quái tàng hình, tạo kẹo, tăng tốc đánh, kẹo may mắn, phượng hoàng, đẩy lùi, kéo + choáng, luôn khắc chế |
| Gửi quái | 6 quái gửi (chuột, ma tàng hình, rùa khiên, quạ trộm kẹo, golem khóa ô, vua quái mini một lần), tối đa 3 lượt mỗi đợt, giảm giá lội ngược dòng |
| Bot | 3 độ khó, ra lệnh giống người chơi thật |
| Màn hình | 5 tab dưới đáy: Cửa hàng, Đội hình, Chiến đấu (sảnh), Mùa giải, Cài đặt; Tìm trận, Trận đấu, Kết quả |
| Rương | 4 ô rương, 4 loại (Gỗ 3h, Bạc 8h, Vàng 12h, Phép 24h), mở từng rương một, mở ngay bằng kim cương, vòng rương cố định khi thắng trận xếp hạng |
| Nâng cấp thú | Cấp thẻ 1–15 bằng mảnh + vàng, mỗi cấp +8% sát thương trong trận; mảnh đầu tiên mở khóa thú mới; bot đối thủ có cấp thẻ quanh mức của bạn |
| Cửa hàng | Quà vàng miễn phí mỗi ngày, kim cương xem quảng cáo, 3 ưu đãi mảnh thú mỗi ngày, gacha x1/x10 (bảo hiểm Huyền thoại sau 40 lượt, bảng tỉ lệ), rương và vàng bằng kim cương, gói kim cương (cửa hàng giả, chưa nối IAP) |
| Nhiệm vụ & Mùa giải | 3 nhiệm vụ ngày (đổi lúc 0h UTC), thẻ mùa giải 28 ngày, 30 bậc, đường Miễn phí + Cao cấp (600 kim cương) |
| Hướng dẫn | Trận hướng dẫn đầu tiên: mua → mua thêm → ghép → gửi quái, thưởng Rương Bạc; chơi lại trong Cài đặt |
| Âm thanh | Nhạc sảnh/trận + 15 hiệu ứng; chưa có file thì tự tạo tiếng bíp tạm |
| Cài đặt | Âm lượng nhạc/hiệu ứng, ngôn ngữ, chơi lại hướng dẫn, xóa tiến trình, công cụ thử (chỉ bản dev: tua giờ, thêm tiền, thêm rương) |
| Meta | Cúp, vàng, kim cương, 5 đấu trường, deck 5 thú + 4 quái gửi, lưu cục bộ |
| Kỹ thuật | Mô phỏng tất định (số nguyên, RNG có seed), log lệnh + replay + kiểm tra hash (nền cho chống gian lận), lớp dịch vụ giả cho Ads, Analytics, Backend |

Chưa có (theo lộ trình Alpha trở đi): PvP online, máy chủ xác thực kinh tế (Cloud Code), IAP thật, quảng cáo thật, file âm thanh thật.

## Âm thanh

Đặt file vào `Assets/ZooDefense/Resources/ZooAudio/` (wav/ogg/mp3), tên viết thường: `click, buy, merge, attack, kill, leak, wave, boss, send, win, lose, chest, upgrade, reward, error`, nhạc nền `music_lobby`, `music_battle`. Thiếu file nào thì dùng tiếng tạm cho file đó.

## Kinh tế (Core/Meta)

Toàn bộ luật kinh tế nằm trong `Core/Meta` (C# thuần, không phụ thuộc Unity) để sau này chạy y hệt trên Cloud Code. Số liệu chỉnh trong `MetaConfig.cs` / `DefaultMeta.Create()`. Thời gian dùng giờ UTC; trong Cài đặt (bản dev) có nút tua +1 giờ / +1 ngày để thử rương, nhiệm vụ, ưu đãi.

## Trận đấu v2: nhiều thao tác hơn

| Tính năng | Cách chơi |
|---|---|
| Tự chọn ô | Chạm thẻ = đặt ô ngẫu nhiên; **kéo thẻ** thả vào ô = đặt đúng chỗ |
| Ô ngôi sao | 2 ô viền vàng (giống nhau cho hai bên) +30% sát thương, đổi chỗ mỗi 5 đợt |
| Liên kết hệ | Mỗi thú cùng hệ đứng kề bên: +8% sát thương |
| Nâng cấp hệ | Nút ▲ theo hệ trong đội: +10% sát thương/cấp, tối đa 5, giá tăng dần |
| Phép Người Giữ Thú | 4 phép chọn trong tab Đội hình (Đóng Băng, Thiên Thạch, Cuồng Phong, Cuồng Nộ, Mưa Kẹo, Sét Đánh), trả kẹo + hồi chiêu; phép vùng: chạm phép rồi chạm lên đường |
| Kỹ năng thú | Thú cấp 3+ nạp năng lượng, phát sáng khi đầy: chạm để dùng (Sát thương: bắn mạnh; Diện rộng: nổ lớn; Khống chế: sóng choáng; Hỗ trợ: cổ vũ + kẹo) |
| Bùa mỗi 5 đợt | Chọn 1 trong 3 bùa (12 loại), 15 giây không chọn thì tự chọn |
| Bóng bay kẹo | Bóng hồng bay ngang bàn, chạm kịp để nhận kẹo |
| Dội quái | Quái đối thủ gửi hiện ở bảng đối thủ, bấm "Dội lại" (tốn 60% giá gửi bằng điểm gửi) để gửi ngược; mỗi quái chỉ dội được 1 lần |
| Biểu cảm | Nút Chat: 6 câu nhanh |
| Bàn đối thủ | To gấp ~2 lần, kèm bảng thông tin; bấm "Xem đối thủ" hoặc chạm bàn để phóng to |

Bot cũng dùng toàn bộ thao tác mới. Thử 80 trận: bot dùng tính năng mới thắng bot kiểu cũ 79/80, nên các thao tác thật sự có giá trị. Trận trung bình kết thúc khoảng đợt 19 (~6,5 phút). Bố cục: `Docs/battle_layout_v2.png`.

## Cách chơi

- Chạm thẻ ở dưới để mua thú, thú xuất hiện ở ô trống ngẫu nhiên.
- Kéo một thú thả lên thú **cùng loại, cùng cấp** để ghép lên cấp mới; kéo vào ô trống để di chuyển.
- Diệt quái nhận **Kẹo** (mua thú) và **Điểm Gửi** (thanh tím) để gửi quái sang đối thủ.
- Hết tim là thua. Nút **II** để tạm dừng hoặc đầu hàng.

## Asset đã nhập

60 hình thú (cấp 1/4/7) và 6 quái gửi từ Zoo Defense Animation Pack v1, cùng chuyển động riêng từng loài (chờ, đi, tấn công, trúng đòn, xuất hiện, biến mất). Chi tiết và danh sách còn thiếu: [ART_NAMING.md](ART_NAMING.md).

## Ngôn ngữ

Game có tiếng Anh và tiếng Việt. Lần đầu mở, game theo ngôn ngữ của máy; nút ở góc dưới bên trái màn hình sảnh dùng để đổi ngôn ngữ, lựa chọn được lưu lại.

- Toàn bộ chữ nằm trong `Scripts/Game/Localization/LocTables.cs`, mỗi ngôn ngữ một bảng. Muốn sửa câu chữ, sửa ở đó.
- Thêm câu mới: thêm vào bảng tiếng Anh trước, rồi bảng tiếng Việt. Menu **Zoo Defense → Localization → Check Missing Translations** báo câu nào còn thiếu bản dịch.
- Phông chữ: mặc định dùng phông có sẵn của Unity. Muốn dùng phông riêng, đặt file `.ttf` có hỗ trợ tiếng Việt vào `Assets/ZooDefense/Resources/ZooArt/Fonts/Main.ttf`.

## Thêm asset thật

Thả PNG vào `Assets/ZooDefense/Resources/ZooArt/` theo tên trong [ART_NAMING.md](ART_NAMING.md). Ví dụ `ZooArt/Units/fire_fox_1.png`. Kiểm tra bằng menu **Zoo Defense → Art → Check Art**.

## Chỉnh cân bằng

- Chọn **Zoo Defense → Balance → Export Balance JSON**: tạo `Resources/ZooBalance.json` chứa toàn bộ chỉ số. Sửa file đó, game sẽ dùng nó thay cho số mặc định trong code (sau này đưa lên Remote Config).
- **Zoo Defense → Balance → Simulate 100 Bot Matches**: cho bot đấu bot, xem trận thường kết thúc ở đợt mấy. Mục tiêu hiện tại: trung vị đợt 15–18 (khoảng 5–6 phút).
- Nếu thêm thú hoặc quái mới trong code, xóa hoặc export lại file JSON, nếu không game vẫn đọc bản cũ.

## Cấu trúc mã

```
Assets/ZooDefense/Scripts/
  Core/        Logic trận thuần C#, không phụ thuộc Unity (chạy được trên server)
    Data/      Định nghĩa thú, quái, quái gửi, BalanceConfig, nội dung mặc định
    Sim/       Match, Board, lệnh, sự kiện, RNG tất định
    AI/        BotBrain
    Replay/    MatchRecord, ReplayValidator (Cloud Code sẽ dùng để xác thực trận)
    Meta/      Kinh tế: rương, nâng cấp, cửa hàng, gacha, nhiệm vụ, mùa giải (MetaService)
  Game/
    App/       GameApp (khởi động, camera, canvas, chuyển màn hình), ContentLoader
    Network/   MatchRunner (tick cố định), IMatchTransport (bot bây giờ, Relay sau)
    Presentation/ BoardView, BattleWorld (kéo thả), Art (nạp asset / hình tạm), hiệu ứng
    UI/        Các màn hình dựng bằng code
    Meta/      Hồ sơ người chơi, đấu trường, cấp bot
    Audio/     AudioService (nhạc, hiệu ứng, tiếng tạm)
    Services/  Interface Ads / Analytics / Backend + bản giả
  Editor/      Menu Zoo Defense, tự tạo scene, tự nhập ảnh thành Sprite
Assets/ZooDefense/Tests/EditMode/  Kiểm thử Core (Window → General → Test Runner)
```

## Thay đổi số so với tài liệu thiết kế

Số trong tài liệu (thẻ 100 kẹo, 2 kẹo mỗi quái) khiến trận kết thúc ở đợt 2–3 khi cho bot đấu thử, nên đã chỉnh:

| Thông số | Tài liệu | Bản này |
|---|---|---|
| Giá thẻ | 100, +10 mỗi lần mua | 20, +5 mỗi lần mua |
| Kẹo mỗi quái thường | 2 | 8–10, tăng 8% mỗi đợt |
| Kẹo thưởng cuối đợt | 20 + 5 × đợt | 40 + 15 × đợt |
| Giá Rat Rush | 5 | 8 |
| Quái gửi khi bị diệt | (chưa nói) | không cho kẹo, để gửi quái là áp lực thật |

## Bước tiếp theo gợi ý

1. Playtest với 15–20 người, chỉnh số trong `ZooBalance.json`.
2. Thay hình tạm bằng asset, bắt đầu với 5 thú trong deck mặc định, quái đợt và 6 quái gửi.
3. Alpha: thêm `RelayLockstepTransport` cho PvP online (Unity Relay), UGS Authentication, Cloud Save và Cloud Code chạy `ReplayValidator`.
