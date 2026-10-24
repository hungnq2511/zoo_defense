# Zoo Defense — Live services (Giai đoạn 0 + 1)

> **Trạng thái 24/10/2026:** đã cài trên Unity Dashboard (production): 2 script Cloud Code, 3 leaderboard, luật Access Control, 7 key Remote Config, 25 tham số + 19 event Analytics. Game Center: đã tạo 3 leaderboard trên App Store Connect (Prepare for Submission) — gắn vào phiên bản kế tiếp (1.0.1) khi nộp. Còn lại: bật Diagnostics trong Editor.

Tài liệu cài đặt dashboard cho các tính năng mới. Code đã có sẵn; phần dưới đây là những việc làm **1 lần** trên
Unity Cloud Dashboard (cloud.unity.com → project *Zoo Defense*) và App Store Connect.

Khi một dịch vụ chưa cài, game vẫn chạy bình thường (dùng giá trị mặc định / ẩn tính năng đó).

---

## 0. Gói mới (đã thêm vào `Packages/manifest.json`)
| Gói | Bản | Dùng cho |
|---|---|---|
| `com.unity.services.analytics` | 6.1.1 | Thống kê (chỉ khi người chơi đồng ý) |
| `com.unity.mobile.notifications` | 2.4.2 | Thông báo nhắc nhở cục bộ |

Mở Unity một lần để tải gói. Remote Config, Leaderboards, Cloud Code dùng REST nên **không cần gói**.

---

## 1. Analytics (Giai đoạn 0)
- Chỉ bật thu thập khi người chơi bấm **Đồng ý** ở hộp thoại quyền riêng tư (nội dung hộp thoại đã cập nhật: có nhắc
  thống kê ẩn danh). Tắt trong Cài đặt › Quyền riêng tư → dừng thu thập.
- Dashboard › **Analytics › Event Manager** → tạo các *custom event* sau (tham số kiểu như ghi):

| Event | Tham số |
|---|---|
| `app_start` | (như hiện có) |
| `tutorial_complete` | `skipped` bool |
| `match_start` | `mode` string, `ranked` bool, `online` bool, `friendly` bool |
| `match_end` | `mode` string, `ranked` bool, `online` bool, `friendly` bool, `result` string, `wave` int, `seconds` int |
| `mode_end` | `mode` string, `wave` int, `winner` int |
| `chest_open` | `chest` string, `gems` bool |
| `arena_up` | `arena` int |
| `login_reward` | `day` int |
| `reconnect` | `success` bool, `seconds` int |
| `social` | `action` string |
| `client_error` | `message` string, `where` string, `steps` string, `exception` bool |
| `online_desync` | `tick` int |
| `privacy_choice` | `personalized` bool |
| `iap` | `product` string |
| `shop_buy` | `shop_item` string (không dùng `item`: trùng tham số chuẩn của Unity) |
| `gacha_pull` | `count` int |
| `pass_premium` | — |
| `ad_rewarded_show`, `ad_interstitial_show` | `placement` string |

Event không khai báo sẽ bị dashboard loại (không lỗi trong game).

## 2. Crash reporting (Cloud Diagnostics)
- Đã bật `CrashReportingSettings › m_Enabled: 1`. Dashboard › **Cloud Diagnostics › Crash and Exception Reporting** → Enable.
- Lỗi script còn được gửi thành event `client_error` kèm 6 bước gần nhất (tối đa 8 lỗi mỗi phiên).

## 3. Remote Config
Dashboard › **Remote Config** → environment *production* → thêm key:

| Key | Kiểu | Mặc định | Ý nghĩa |
|---|---|---|---|
| `bot_wait_seconds` | float | 10 | Tìm người thật bao lâu trước khi đấu bot (3–60) |
| `reconnect_seconds` | float | 20 | Thời gian chờ kết nối lại trong trận online (5–60) |
| `ad_first_after` | int | 3 | Quảng cáo xen kẽ đầu tiên sau N trận |
| `ad_every` | int | 2 | Rồi cứ mỗi N trận |
| `ad_min_seconds` | float | 240 | Cách nhau ít nhất N giây |
| `notify_per_day` | int | 2 | Số thông báo tối đa mỗi ngày (0 = tắt) |
| `live_event` | int | -1 | Ép sự kiện của tuần số N (-1 = theo lịch) |
| `meta_overrides` | json | — | Ghi đè giá/phần thưởng (`MetaConfig`), áp dụng ở lần mở game sau |
| `balance_overrides` | json | — | Ghi đè toàn bộ cân bằng (file export), áp dụng ở lần mở game sau |

Giá trị được lưu trên máy, nên lần mở sau dùng ngay cả khi offline. Lưu ý: đổi `balance_overrides` làm thay đổi
"dấu vân tay" nội dung → chỉ người cùng bản cân bằng mới ghép trận với nhau (đúng mong muốn).

## 4. Cloud Code (2 script mới)
Dashboard › **Cloud Code › Scripts** → *Create script* (JavaScript), dán nội dung file, **Publish**:
- `submit_score` ← `CloudCode/submit_score.js`
- `social` ← `CloudCode/social.js`

(`validate_purchase` giữ nguyên.)

## 5. Leaderboards
Dashboard › **Leaderboards** → tạo 3 bảng:

| ID | Sắp xếp | Cập nhật điểm | Reset |
|---|---|---|---|
| `trophies` | Cao → thấp | **Keep latest** | Không |
| `endless_weekly` | Cao → thấp | Keep best | Lặp lại **hằng tuần**, Thứ Hai 00:00 UTC |
| `daily_challenge` | Cao → thấp | Keep best | Lặp lại **hằng ngày**, 00:00 UTC |

**Access control** (bắt buộc, chống gian lận) — đã tạo statement `deny-client-leaderboard-writes` (Deny · Write · Leaderboards › Player scores). Tương đương JSON:
```json
{
  "Sid": "deny-client-leaderboard-writes",
  "Effect": "Deny",
  "Action": ["Write"],
  "Principal": "Player",
  "Resource": "urn:ugs:leaderboards:/v1/projects/*/leaderboards/*/scores/players/*"
}
```
Người chơi vẫn đọc được bảng; chỉ script `submit_score` (quyền server) mới ghi điểm.

**Theo quốc gia:** Unity Leaderboards không lưu quốc gia và không lọc được theo quốc gia, nên bản này chưa có bảng
theo quốc gia (sẽ cần một bảng riêng cho mỗi nước hoặc backend riêng). Có: Thế giới / Quanh tôi / Bạn bè.

## 6. Game Center (iOS)
- App Store Connect › app *Zoo Defense* › **Features › Game Center** → tạo 3 *Classic leaderboard* (High score, integer):
  - `zoo.trophies` — "Trophies"
  - `zoo.endless_weekly` — "Endless (weekly)" — *recurring* hằng tuần
  - `zoo.daily_challenge` — "Daily challenge" — *recurring* hằng ngày
- Đã tạo (24/10/2026): `zoo.trophies` Classic, *Most Recent Score*, cao → thấp · `zoo.endless_weekly` Recurring 7 ngày,
  bắt đầu Thứ Hai 26/10/2026 00:00 UTC, Best Score · `zoo.daily_challenge` Recurring 1 ngày, bắt đầu 25/10/2026 00:00 UTC,
  Best Score. Mỗi bảng có bản địa hoá English (U.S.).
- Bản 1.0 đang *Waiting for Review* với build cũ nên chưa gắn. Khi tạo phiên bản 1.0.1: ở trang phiên bản, mục
  **Game Center** → bật và thêm 3 leaderboard, rồi chọn build mới.
- Bản build tự thêm capability Game Center (entitlement) và GameKit.

## 7. Bạn bè, mời bạn, phòng giao hữu
- Mã kết bạn 6 ký tự (không có O/0, I/1), tạo trên server lần đầu, không đổi.
- Hồ sơ công khai (tên, cúp, lần cuối online, lời mời phòng) lưu ở Cloud Save *public player data* key `profile`.
  Cloud Save đã có sẵn, không cần cấu hình.
- Mời bạn: người mới nhập mã của bạn → người mới +100 kim cương; bạn bấm **Nhận** trong màn Bạn bè → +100 cho mỗi người.
  Mỗi tài khoản chỉ nhập mã mời 1 lần (server kiểm tra).
- Phòng giao hữu: phiên riêng tư (Multiplayer Sessions, type `zoo_room`) vào bằng mã 6 ký tự; **không** cộng/trừ cúp,
  không tính MMR, không có phần thưởng.

## 8. Ghép trận theo MMR ẩn + kết nối lại
- Mỗi người có MMR ẩn (Elo, bắt đầu 1000; người chơi cũ được quy đổi từ cúp). Cúp chỉ quyết định đấu trường.
- Tìm người thật trong khoảng ±150 MMR, cứ 3 giây không thấy thì nới ±300, ±600, rồi mọi người. Hết thời gian
  `bot_wait_seconds` → đấu bot; độ khó và cấp thẻ của bot theo MMR.
- Mất mạng giữa trận: tự kết nối lại qua Relay (cùng allocation), hai máy gửi lại các lệnh bị lỡ và vẫn đồng bộ.
  Quá `reconnect_seconds` thì **người mất mạng thua**. Màn hình hiện "Mất kết nối, đang kết nối lại…" hoặc
  "Đối thủ đang kết nối lại…".
- Session type ranked vẫn là `zoo_pvp`; chuỗi phiên bản ghép trận đổi sang `p2_c…` (protocol 2), nên bản mới không
  ghép với bản cũ — hãy cập nhật cả hai máy khi thử.

## 9. Thông báo nhắc nhở
- Xin quyền **sau trận thắng đầu tiên hoặc lần mở rương đầu tiên** (không hỏi lúc mở app). Tắt/bật trong
  Cài đặt › Trò chơi › Thông báo nhắc nhở.
- Tối đa `notify_per_day` (mặc định 2) mỗi ngày, không gửi 22:00–09:00 giờ máy. Nội dung: rương mở xong, quà đăng nhập,
  thẻ tháng, nhắc quay lại sau 2 ngày. Tất cả bị huỷ khi người chơi mở game.

## 10. Quà đăng nhập 7 ngày
- Hiện ở sảnh 1 lần/ngày (sau hộp thoại quyền riêng tư và hướng dẫn). Lỡ ngày không mất chuỗi. Ngày 7 = rương phép.
- Đổi phần thưởng: `meta_overrides` → `{"loginRewards":[...]}` hoặc sửa `MetaRetention.cs`.

## Kiểm tra nhanh sau khi cài
1. Mở game → Console có `[Online] signed in as …`, không có lỗi đỏ.
2. Sảnh → nút **Xếp hạng** (trái) và **Bạn bè** (phải) xuất hiện; quà đăng nhập bật lên.
3. Bạn bè → mã 6 ký tự hiện ra (nếu "—": script `social` chưa publish).
4. Đánh 1 trận → Xếp hạng › Cúp có tên bạn (nếu trống: script `submit_score` chưa publish hoặc bảng chưa tạo).
5. Hai máy: máy A **Tạo phòng**, máy B nhập mã → vào trận giao hữu. Tắt Wi-Fi máy B 5 giây rồi bật lại → trận tiếp tục.
