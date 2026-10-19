# Zoo Defense — Quảng cáo (LevelPlay) & mua hàng (Unity IAP)

## Đã có trong code
- **Quảng cáo thưởng**: Quà miễn phí (kim cương) trong cửa hàng, x2 vàng ở màn kết quả.
- **Quảng cáo toàn màn hình** khi bấm Tiếp tục ở màn kết quả: từ trận thứ 3, cách ít nhất 2 trận và 4 phút; **không bao giờ** hiện cho người đã từng nạp tiền.
- **iOS**: hỏi quyền theo dõi (ATT) trước khi bật quảng cáo; câu mô tả đã thêm vào Info.plist.
- **Mua hàng thật**: 11 sản phẩm (xem SHOP_PRODUCTS.md), giá hiển thị theo tiền tệ của App Store, cấp quà → lưu → mới xác nhận giao dịch; giao dịch bị gián đoạn sẽ được cấp ở lần mở sau; không cấp trùng.
- Chưa có gói / chưa điền key → game vẫn chạy với quảng cáo thử và cửa hàng thử (mua miễn phí).

## Bạn cần làm
1. Mở Unity → tự tải **Ads Mediation 9.5.1** và **In-App Purchasing 5.4.4**. Khi hỏi cài **Mobile Dependency Resolver** → Import.
2. Unity tự bật `ZOO_LEVELPLAY` / `ZOO_IAP` (menu Zoo Defense › Monetization nếu cần làm lại).
3. **LevelPlay** (platform.ironsrc.com): thêm app iOS (và Android), tạo ad unit **Rewarded** + **Interstitial**, điền vào `Assets/ZooDefense/Resources/ZooMonetization.json`:
   `levelPlayAppKeyIos`, `rewardedAdUnitIos`, `interstitialAdUnitIos` (và bản Android).
4. **Ads Mediation › Network Manager**: cài thêm mạng quảng cáo (Unity Ads, AdMob…). AdMob cần điền key ở Ads Mediation › Developer Settings.
5. **App Store Connect**: ký Paid Apps Agreement, tạo 11 sản phẩm Consumable, tạo tài khoản Sandbox để thử mua.
6. Máy Mac cần **CocoaPods** (`brew install cocoapods`) — script TestFlight tự chạy `pod install`.
7. Thử toàn bộ mạng quảng cáo: đặt `"testSuite": true` trong ZooMonetization.json, build Development lên máy thật → bảng Test Suite hiện ra. **Tắt lại trước khi phát hành.**

## Quyền riêng tư (đã làm)
- Lần đầu mở game: popup hỏi có cá nhân hoá quảng cáo không (cho mọi người chơi). Đồng ý → GDPR consent = true, CCPA opt-out = false, rồi mới hỏi quyền theo dõi của Apple. Không → quảng cáo không cá nhân hoá, không bán/chia sẻ dữ liệu, không hỏi quyền theo dõi.
- Đổi lại trong Cài đặt › Quyền riêng tư. Link chính sách: `privacyPolicyUrl` trong ZooMonetization.json.

## Kiểm tra hoá đơn trên server (đã làm)
- Cloud Code script `validate_purchase` (bản gốc: `CloudCode/validate_purchase.js`) đã phát hành trên môi trường production.
- Kiểm tra chữ ký Apple (chuỗi chứng chỉ tới Apple Root CA – G3), đúng app `site.nutrica.zoodefense`, đúng gói, đúng mã giao dịch, chưa hoàn tiền, và mỗi giao dịch chỉ một người chơi dùng được (sổ `iap_ledger` trong Cloud Save › Game data).
- Bật/tắt trong game: `serverValidation` trong ZooMonetization.json. Đổi bundle id thì sửa `BUNDLE_ID` trong script rồi phát hành lại.
- Google Play: chưa kiểm tra (trả về `unverified_google`) — cần tài khoản dịch vụ Google khi làm bản Android.
- Sổ có 1 dòng thử `tx_1` tạo lúc kiểm tra, có thể xoá.
