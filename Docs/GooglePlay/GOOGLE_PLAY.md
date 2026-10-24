# Zoo Defense — Đưa lên Google Play

## A. Chuẩn bị trên máy Mac (làm 1 lần)
1. **Unity Hub › Installs › 6000.0.58f2 › Add modules › Android Build Support**, tick cả **OpenJDK** và **Android SDK & NDK Tools**.
2. Đóng Unity Editor.

## B. Build file .aab
```bash
cd ~/Desktop/ZooDefender
chmod +x Tools/android_build.sh
./Tools/android_build.sh
```
- Lần đầu script tạo **khoá upload** ở `BuildConfig/android_signing/` (file `.keystore` + mật khẩu trong `keystore.properties`).
  **Sao lưu ngay thư mục này** (ổ cá nhân / trình quản lý mật khẩu). Thư mục đã nằm trong `.gitignore`.
- Kết quả: `Builds/Android/ZooDefense-1.0-<versionCode>.aab`. Mỗi lần build `versionCode` tự tăng.
- Đổi version hiển thị: `BuildConfig/android.json` › `"version"`.
- Trong Editor: menu **Zoo Defense › Android › Build App Bundle (.aab)** (khoá lấy từ biến môi trường, nên dùng script là tiện nhất).

## C. Tạo app trên Play Console
Play Console › **Create app**: tên *Zoo Defense*, ngôn ngữ mặc định English (United States), **Game**, **Free**, đồng ý các cam kết.
Package name sẽ là `com.hungnq.zoodefense` (lấy từ file .aab đầu tiên, không đổi được về sau).

### Tài khoản cá nhân mới: bắt buộc thử nghiệm kín
Tài khoản developer **cá nhân** tạo sau 13/11/2023 phải chạy **Closed testing** với **ít nhất 12 người thử, liên tục 14 ngày** trước khi được xin lên Production.
Tài khoản **tổ chức** (organization) không bị yêu cầu này.

Thứ tự đề xuất:
1. **Internal testing**: tải .aab, thêm email của bạn, cài thử ngay (vài phút, không cần duyệt).
2. **Closed testing**: tải cùng .aab, thêm danh sách ≥ 12 email Gmail, gửi link tham gia, giữ họ cài app 14 ngày.
3. Sau 14 ngày: **Apply for production** (trả lời vài câu về đợt thử), rồi phát hành Production.

## D. Nội dung cần điền (Dashboard › Set up your app)
| Mục | Trả lời |
|---|---|
| Privacy policy | https://nutrica.site/zoodefense/privacy/ |
| App access | All functionality is available without special access |
| Ads | Yes, my app contains ads |
| Content rating (IARC) | Game; bạo lực: hoạt hình nhẹ (cartoon / fantasy, không máu); không cờ bạc tiền thật; có mua trong app; người chơi tương tác: chỉ emote có sẵn, không chat tự do; không chia sẻ vị trí |
| Target audience | 13–15, 16–17, 18+ (không chọn dưới 13 để tránh chương trình Families và giới hạn quảng cáo) |
| News app | No |
| Data safety | Bảng bên dưới |
| Government app / Financial features / Health | No / None / No |
| Category | Game › Strategy · Tags: Tower defense, PvP, Casual |
| Contact | founder@nutrica.site · Website https://nutrica.site |

### Data safety
- Thu thập / chia sẻ dữ liệu: **Yes**. Mã hoá khi truyền: **Yes**. Người dùng yêu cầu xoá dữ liệu: **Yes** (trong game: Settings › Account › Delete account; web: https://nutrica.site/zoodefense/support/).

| Loại dữ liệu | Thu thập | Chia sẻ (bên thứ ba) | Mục đích | Bắt buộc |
|---|---|---|---|---|
| Device or other IDs (advertising ID, player ID) | Có | Có (mạng quảng cáo) | App functionality, Advertising, Analytics | Bắt buộc |
| App interactions | Có | Có (mạng quảng cáo) | Advertising, Analytics | Bắt buộc |
| Approximate location (từ IP, bởi SDK quảng cáo) | Có | Có | Advertising | Bắt buộc |
| Crash logs, Diagnostics | Có | Có | Analytics | Bắt buộc |
| Purchase history | Có | Không | App functionality | Bắt buộc |
| Other user-generated content (tiến trình game, bản sao lưu) | Có | Không | App functionality | Tuỳ chọn (chỉ khi liên kết tài khoản) |

## E. Trang cửa hàng (Main store listing)
Có sẵn trong `Docs/GooglePlay/`:
- `play_listing.json`: tên, mô tả ngắn (≤ 80 ký tự), mô tả đầy đủ cho en-US, ja-JP, ko-KR, de-DE, fr-FR, es-419, es-ES, pt-BR.
- `icon_512.png` (512×512), `feature_graphic_1024x500.png`, `phone_01…08.png` (1080×2160, tỷ lệ 2:1 theo quy định của Play).

## F. Sau khi tải bản đầu tiên
1. **Gói nạp**: Monetize › Products › **In-app products** › tạo 11 sản phẩm với đúng mã như iOS (bảng ở `Docs/SHOP_PRODUCTS.md`), giá giống bản iOS. Chỉ tạo được sau khi đã tải lên 1 bản có quyền BILLING (bản build này đã có).
2. **Quảng cáo**: LevelPlay dashboard › thêm app Android `com.hungnq.zoodefense` › tạo ad unit Rewarded + Interstitial › điền `levelPlayAppKeyAndroid`, `rewardedAdUnitAndroid`, `interstitialAdUnitAndroid` trong `Assets/ZooDefense/Resources/ZooMonetization.json` rồi build lại. Chưa điền thì bản Android chạy không có quảng cáo.
3. **Kiểm tra hoá đơn Google**: hiện server chấp nhận mọi giao dịch Google Play (chưa xác minh chữ ký). Lấy *Licensing key* (Monetize › Monetization setup) để bật xác minh chữ ký trên server.
4. Mua thử: thêm Gmail của bạn vào **Settings › License testing** để mua không mất tiền.
