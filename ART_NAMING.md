# Tên file asset (Zoo Defense)

## Trạng thái asset (18/10/2026)

Đã đủ toàn bộ asset hình tĩnh mà game dùng.

| Nhóm | Số lượng | Nguồn |
|---|---|---|
| Thú (20 × cấp 1/4/7) | 60/60 | Zoo Defense Animation Pack v1 |
| Quái gửi | 6/6 | Zoo Defense Animation Pack v1 |
| Quái theo đợt (`slime`, `imp`, `rock_beetle`, `armored_knight`) | 4/4 | Missing Assets, `wave_enemies.png` |
| Boss (`boss_summoner`, `boss_stunner`, `boss_healer`) | 3/3 | Missing Assets, `bosses.png` |
| Nền đấu trường `arena_1` … `arena_5` | 5/5 | Missing Assets (941×1672, giữ nguyên ảnh gốc) |
| Biểu tượng UI (`candy`, `heart`, `trophy`, `gold`) | 4/4 | Missing Assets, `ui_icons.png` |
| Chuyển động theo loài | 26 profile | quái đợt và boss dùng chuyển động chung |

Quái đợt và boss vẽ tông tím trung tính; hệ của từng con (Lửa/Nước/Cỏ, ngẫu nhiên mỗi lần xuất hiện) hiện bằng chấm màu dưới chân.

Ảnh kiểm tra ở kích thước thật trong game: `Docs/asset_check_128px.png`, `Docs/enemy_lineup.png`.

### Quy ước khung hình (để thay hoặc thêm asset mới khớp với game)

- Khung vuông, nền trong suốt. Thú: 384×384. Quái: 560×560.
- Nhân vật căn giữa theo chiều ngang, **điểm chạm chân ở 88% chiều cao khung** (cách đáy 12%). Unity tự đặt pivot ở điểm này để chuyển động nhún, nghiêng xoay quanh chân.
- Không vẽ bóng chân, hạt rời hay hào quang; những thứ đó làm bằng hiệu ứng trong game.

Thả file PNG (nền trong suốt) vào đúng thư mục dưới `Assets/ZooDefense/Resources/ZooArt/`. Unity tự nhập thành Sprite. Thiếu file nào thì game dùng hình tạm (hình tròn màu + chữ viết tắt).

Kiểm tra nhanh: menu **Zoo Defense > Art > Check Art** liệt kê file còn thiếu.

## Thú – `ZooArt/Units/`

Mỗi thú có thể có 3 ngoại hình: `{id}_1.png` (cấp 1–3), `{id}_4.png` (cấp 4–6), `{id}_7.png` (cấp 7). Chỉ có `{id}_1.png` thì dùng cho mọi cấp. Khuyến nghị 512×512.

| id | Tên | Hệ | Vai trò | Độ hiếm |
|---|---|---|---|---|
| `fire_fox` | Fire Fox | Fire | Damage | Common |
| `firework_rooster` | Firework Rooster | Fire | Area | Common |
| `ember_squirrel` | Ember Squirrel | Fire | Damage | Rare |
| `baby_dragon` | Baby Dragon | Fire | Area | Epic |
| `phoenix_chick` | Phoenix Chick | Fire | Support | Legendary |
| `bubble_frog` | Bubble Frog | Water | Control | Common |
| `ice_penguin` | Ice Penguin | Water | Control | Common |
| `otter_cannon` | Otter Cannon | Water | Area | Rare |
| `ink_octopus` | Ink Octopus | Water | Control | Rare |
| `wave_dolphin` | Wave Dolphin | Water | Area | Epic |
| `spike_hedgehog` | Spike Hedgehog | Grass | Area | Common |
| `carrot_bunny` | Carrot Bunny | Grass | Damage | Common |
| `vine_sloth` | Vine Sloth | Grass | Control | Common |
| `chubby_panda` | Chubby Panda | Grass | Support | Rare |
| `leaf_gecko` | Leaf Gecko | Grass | Control | Rare |
| `sun_sparrow` | Sun Sparrow | Light | Damage | Common |
| `lucky_cat` | Lucky Cat | Light | Support | Rare |
| `night_owl` | Night Owl | Light | Support | Epic |
| `meteor_deer` | Meteor Deer | Light | Area | Epic |
| `rainbow_unicorn` | Rainbow Unicorn | Light | Damage | Legendary |

## Quái – `ZooArt/Enemies/`

File `{id}.png`, khuyến nghị 512×512, nhìn thẳng xuống (đi về phía người xem).

| id | Tên | Loại |
|---|---|---|
| `slime` | Slime | Normal |
| `imp` | Imp | Normal |
| `rock_beetle` | Rock Beetle | Normal |
| `armored_knight` | Armored Knight | Elite |
| `boss_summoner` | Summoner King | Boss |
| `boss_stunner` | Thunder Toad | Boss |
| `boss_healer` | Mushroom Witch | Boss |
| `send_rat` | Speedy Rat | Sent |
| `send_ghost` | Sneaky Ghost | Sent |
| `send_turtle` | Shield Turtle | Sent |
| `send_crow` | Candy Crow | Sent |
| `send_golem` | Brick Golem | Sent |
| `send_king` | Mini Monster King | Sent |

Nút gửi quái dùng hình của quái tương ứng:

| Gửi quái | Dùng hình |
|---|---|
| Rat Rush (`rat_rush`) | `Enemies/send_rat.png` |
| Sneaky Ghost (`sneaky_ghost`) | `Enemies/send_ghost.png` |
| Shield Turtle (`shield_turtle`) | `Enemies/send_turtle.png` |
| Candy Crow (`candy_crow`) | `Enemies/send_crow.png` |
| Brick Golem (`brick_golem`) | `Enemies/send_golem.png` |
| Mini Monster King (`mini_king`) | `Enemies/send_king.png` |

## Nền đấu trường – `ZooArt/Backgrounds/`

`arena_1.png` … `arena_5.png` (Grassy Meadow, Deep Forest, Volcano, Ice Island, Cloud Palace), khung dọc 9:16, khuyến nghị 1080×1920.

## Biểu tượng UI – `ZooArt/UI/`

`candy.png`, `heart.png`, `trophy.png`, `gold.png` (vuông, khuyến nghị 256×256).
