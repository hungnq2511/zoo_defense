# Zoo Defense — Gói cửa hàng & mã sản phẩm

Game dùng Unity IAP 5. Tạo các sản phẩm sau trên App Store Connect (và Google Play) với **đúng Product ID**. Tất cả đều là **Consumable** (game tự giới hạn gói 1 lần / mỗi tuần), nên không cần chức năng khôi phục bắt buộc của Apple — nút "Khôi phục giao dịch" trong cửa hàng chỉ để nhận lại giao dịch bị gián đoạn.

| Product ID | Loại | Giá | Nội dung |
|---|---|---|---|
| gems_small | Consumable | $0.99 | 80 KC (x2 lần đầu) |
| gems_medium | Consumable | $4.99 | 500 KC (x2 lần đầu) |
| gems_large | Consumable | $9.99 | 1.200 KC (x2 lần đầu) |
| gems_huge | Consumable | $19.99 | 2.600 KC (x2 lần đầu) |
| gems_mega | Consumable | $49.99 | 7.000 KC (x2 lần đầu) |
| gems_ultra | Consumable | $99.99 | 15.000 KC (x2 lần đầu) |
| bundle_starter | Consumable | $1.99 | 500 KC + 5.000 vàng + 1 rương Phép — mua 1 lần |
| bundle_weekly | Consumable | $4.99 | 800 KC + 8.000 vàng + 2 rương Vàng — mỗi tuần 1 lần |
| bundle_champion | Consumable | $9.99 | 1.500 KC + 20.000 vàng + 3 rương Phép — từ 300 cúp, 1 lần |
| bundle_legend | Consumable | $19.99 | 4.000 KC + 50.000 vàng + 6 rương Phép — từ 1.000 cúp, 1 lần |
| monthly_card | Consumable | $4.99 | 300 KC ngay + 100 KC/ngày × 30 ngày (mua thêm cộng dồn 30 ngày) |

Gói mua bằng kim cương (không cần tạo trên store): **Ưu đãi rương bạc** 90 KC/ngày (rương Bạc + 1.500 vàng + 100 XP pass); vàng: 1.000 / 3.000 / 10.000 vàng = 60 / 160 / 480 KC.

Chỉnh số liệu: `Scripts/Core/Meta/MetaShop.cs` (gói, thẻ tháng, vàng) và `MetaConfig.cs` (gói kim cương).
