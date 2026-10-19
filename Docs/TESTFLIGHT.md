# Zoo Defense — Đưa bản build lên TestFlight

## A. Chuẩn bị (làm 1 lần)
1. **Tài khoản Apple Developer Program** (99 USD/năm) — developer.apple.com.
2. **Team ID**: developer.apple.com › Account › Membership details › *Team ID* (10 ký tự) → điền vào `BuildConfig/ios.json` (`"teamId"`).
3. **Bundle ID**: mặc định `site.nutrica.zoodefense` (sửa trong `BuildConfig/ios.json` nếu muốn). Tạo tại developer.apple.com › Identifiers › “+” › App IDs › App, hoặc để Xcode tự tạo khi archive.
4. **Tạo app trên App Store Connect**: appstoreconnect.apple.com › Apps › “+” › New App → iOS, tên *Zoo Defense*, chọn đúng Bundle ID, SKU tuỳ ý (vd `zoodefense`).
5. **Xcode** bản mới nhất (App Store) → mở 1 lần để cài thành phần, rồi **Xcode › Settings › Accounts** › “+” › đăng nhập Apple ID thuộc team.
6. **Unity Hub** › Installs › 6000.0.58f2 › *Add modules* › **iOS Build Support**.

## B. Build & upload
**Cách 1 — một lệnh (Editor phải đóng):**
```bash
cd ~/Desktop/zoo_defense
./Tools/testflight.sh
```
Script: Unity xuất Xcode project → archive (tự ký bằng team của bạn) → upload. Log Unity ở `Builds/unity_ios_build.log`.

**Cách 2 — đang mở Editor:**
1. Menu **Zoo Defense › iOS › Build Xcode Project** (tự chuyển sang iOS, áp cài đặt, mỗi lần build tự tăng số build).
2. Terminal: `SKIP_UNITY=1 ./Tools/testflight.sh`
   (hoặc mở `Builds/iOS/Unity-iPhone.xcodeproj` › Product › Archive › Distribute App › App Store Connect › Upload).

Sau 5–20 phút bản build xuất hiện ở App Store Connect › TestFlight. Lần đầu: điền *Test Information*, thêm nhóm **Internal Testing** và email người thử → họ cài app **TestFlight** trên iPhone để nhận bản.

## C. Cài đặt đã được áp tự động
- IL2CPP, ARM64, iOS tối thiểu 15.0, iPhone + iPad, dọc, toàn màn hình, ẩn thanh trạng thái.
- Tự ký (Automatic signing) với Team ID; mỗi build có số build mới (`yyMMddHHmm`), phiên bản hiển thị lấy từ `version`.
- `ITSAppUsesNonExemptEncryption = NO` (game chỉ dùng HTTPS chuẩn) → không phải trả lời câu hỏi mã hoá mỗi lần upload.
- `ExportOptions.plist` (app-store-connect, upload) được tạo cạnh Xcode project.

## D. Upload không cần đăng nhập Xcode (CI / máy khác)
App Store Connect › Users and Access › Integrations › **App Store Connect API** › tạo key (quyền *App Manager*), tải `AuthKey_XXXX.p8`:
```bash
export ASC_KEY_ID=XXXX ASC_ISSUER_ID=xxxxxxxx-xxxx-... ASC_KEY_PATH=~/keys/AuthKey_XXXX.p8
./Tools/testflight.sh
```

## E. Lỗi hay gặp
| Lỗi | Cách xử lý |
|---|---|
| `No Accounts` / `No profiles for 'site.nutrica.zoodefense'` | Đăng nhập Apple ID trong Xcode › Settings › Accounts; kiểm tra teamId. |
| `iOS Build Support is not installed` | Cài module iOS trong Unity Hub (mục A6). |
| `The bundle version must be higher…` | Build lại (số build tự tăng); đừng dùng SKIP_UNITY với bản cũ đã upload. |
| `No suitable application records were found` | Chưa tạo app trên App Store Connect, hoặc bundle id khác (mục A4). |
| `Missing Info.plist value … NSCameraUsageDescription` | Báo mình: một SDK có dùng camera, cần thêm mô tả. |
