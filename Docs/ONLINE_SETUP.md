# Zoo Defense — Chơi online 1v1

## Đã có trong code
- **Đăng nhập ẩn danh** (mỗi máy một mã người chơi, giữ giữa các lần mở game).
- **Ghép trận theo đấu trường**: tìm phòng 2 người cùng đấu trường (và cùng phiên bản game); không có thì tự tạo phòng và chờ. Chờ một mình ~5 s thì tìm lại; quá ~10 s không thấy ai → đấu bot như cũ.
- **Kết nối qua Relay** (không cần server riêng, không lộ IP).
- **Lockstep**: hai máy chỉ gửi lệnh (đặt thú, gộp, gửi quái…), không gửi trạng thái. Trễ nhập 0,2 s. Mỗi giây so mã trạng thái để phát hiện lệch.
- Đối thủ thoát / mất mạng 15 s → bạn thắng do đối thủ bỏ cuộc. Trận online không tạm dừng được.

## Bạn cần làm trong Unity Editor (1 lần)
1. Mở project → Unity tự tải gói `com.unity.services.multiplayer 1.1.8` (đã thêm vào `Packages/manifest.json`).
2. **Edit › Project Settings › Services** → chọn tổ chức → **Use an existing project** → chọn *Zoo Defense* → Link.
3. Kiểm tra Console không có lỗi đỏ. Khi chưa có gói hoặc chưa link, game vẫn chạy và chỉ đấu bot.

## Thử 2 người trên 1 máy
- **Window › Multiplayer › Multiplayer Play Mode** (gói `com.unity.multiplayer.playmode`), bật Player 2 → Play.
- Cả hai bấm **Đấu xếp hạng** cùng lúc, cùng đấu trường. Console hiện `[Online] session … (host, waiting)` rồi `(joined)`.
- Hoặc build ra 2 điện thoại.

## Tắt online
`PlayerProfile.onlinePvp = false` → luôn đấu bot.

## Bước sau (chưa làm)
- Cloud Code kiểm tra replay để chống gian lận cúp.
- Cloud Save cho hồ sơ / kinh tế.
