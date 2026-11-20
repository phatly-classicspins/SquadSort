# GDD — SqadSort (tên làm việc)

> Phiên bản 0.1 · 2026-11-17 · Trạng thái: **bản nháp chờ duyệt**
>
> Nguồn tham khảo: *Squad Sort* (Wind Games AS, Google Play `studio.gammate.squadsort`) và video
> gameplay YouTube `IgJV4GH3B0Y` (1:15, level 4–6). Tài liệu này lấy **thể loại và cơ chế** làm
> tham chiếu; tên, nhân vật, art và level của sản phẩm phải là **của mình** (xem §9).
>
> Ký hiệu độ tin cậy: **[Q]** = quan sát trực tiếp trong video/store · **[C]** = đã kiểm chứng khi
> chơi thử game gốc trên MuMu (level 1–25, 2026-11-17/18) · **[S]** = suy luận, chưa kiểm chứng ·
> **[Đ]** = đề xuất thiết kế của mình.

---

## 1. Tổng quan

| Mục | Nội dung |
|---|---|
| Thể loại | Casual puzzle — color sort + giải đường đi (path-unblock) |
| Nền tảng | Android / iOS, màn dọc 9:16 (1080×1920 safe rect) |
| Đối tượng | Người chơi casual mọi lứa tuổi (store rate 3+) |
| Phiên chơi | 30 giây – 2 phút / level |
| Kiếm tiền | Quảng cáo (interstitial + rewarded) + IAP (store ghi "Contains ads · In-app purchases") **[Q]** |
| Một câu pitch | *Đánh thức và dẫn từng chú nhóc màu về đúng đội của mình — mỗi bước đi đều phải tính trước.* |

### Điểm hấp dẫn cốt lõi
1. **Thỏa mãn khi gom màu** — đội 4 nhân vật cùng màu đầy ống thì ăn mừng (mũ tiệc, confetti) và biến mất **[Q]**.
2. **Suy nghĩ trước** — nhân vật chỉ di chuyển khi có đường đi; xếp sai sẽ chặn lối của chính mình **[S]**.
3. **Nhân vật có cảm xúc** — ngủ (bị chặn), tỉnh, cau mày, buồn, vui; nhân vật là UI trạng thái **[Q]**.

---

## 2. Core gameplay

### 2.1 Bàn chơi
Bàn chơi là một **đồ thị ô** gồm hai loại vùng **[Q]**:

- **Ống (Squad Tube)** — cột/hàng gồm `capacity` ô (video: **4**), **một đầu mở** nối vào quảng trường.
  Ống có thể nằm trên, dưới, trái, phải quảng trường; hướng đầu mở khác nhau theo layout.
- **Quảng trường (Plaza)** — lưới ô đi được, hình dạng tự do (video: hình chữ nhật 2×4 ở level 4,
  hình chữ thập/chữ L ở level 5–6). Là vùng đệm chung để tạm đặt nhân vật.

Mỗi ô chứa tối đa **1 nhân vật**. Nhân vật có **màu** (video: tím, xanh lá, cam, xanh dương, hồng, vàng) **[Q]**.

### 2.2 Trạng thái nhân vật
| Trạng thái | Hiển thị | Ý nghĩa |
|---|---|---|
| Ngủ | khối màu, mắt nhắm, không mặt | không thuộc **nhóm đầu ống** → không chạm được **[C]** |
| Tỉnh | có mặt, mắt mở, cảm xúc ngẫu nhiên (cười, nháy mắt, lo, cau mày) | thuộc nhóm đầu ống hoặc đang đứng trên quảng trường → chạm được **[C]** |
| Nhăn mặt | mắt nhắm chặt + viền quảng trường nháy đỏ | phản hồi khi chạm mà **không còn chỗ đi** **[C]** |
| Ăn mừng | mũ tiệc, confetti | đội vừa hoàn thành **[C]** |

### 2.2b Luật game tham chiếu — đã kiểm chứng khi chơi thử **[C]**
1. **Trọng lực về đầu kín.** Nhân vật trong ống dồn về đầu kín; đầu mở (giáp quảng trường) có thể
   còn ô trống. Khi nhóm đầu rời đi, phần còn lại **không trượt** ra phía đầu mở.
2. **Nhóm đầu ống** = chuỗi liên tiếp cùng màu tính từ phía đầu mở. Chạm **bất kỳ** nhân vật nào
   trong nhóm → **cả nhóm** đi cùng nhau.
3. **Chọn rồi thả** (prototype, §2.3): chạm nhóm để nhấc lên, rồi chạm đích. Đích là **ống hợp lệ**
   (rỗng, hoặc nhóm đầu cùng màu và còn chỗ) hoặc **ô trống trên quảng trường**.
   *Game gốc:* một chạm, game tự chọn đích (ống cùng màu → ống rỗng → quảng trường gần đầu ống).
4. **Nhân vật trên quảng trường** được nhấc từng con; thả vào ống hợp lệ thì các nhân vật cùng màu
   khác trên quảng trường tự đi theo (§2.2b-10). Quảng trường → quảng trường không phải một bước.
5. **Đi một phần.** Nếu đích chỉ đủ chỗ cho một phần nhóm, chỉ số vừa đủ chỗ đi; phần còn lại ở yên.
6. **Nhường đường.** Nhân vật trên quảng trường **tự né** sang ô trống khác để nhóm đang đi lọt qua — người chơi không bị chặn bởi việc ai đứng chắn trên quảng trường.
7. **Hoàn thành đội.** Ống có đủ 4 nhân vật cùng màu → ăn mừng → **cả ống biến mất** khỏi bàn.
   Ống đầy nhưng lẫn màu thì không có gì xảy ra.
8. **Không còn chỗ.** Chạm khi không có đích → viền quảng trường nháy đỏ, nhân vật nhăn mặt,
   không tính bước.
9. **Trạng thái đầu level** có thể có ống chưa đầy và nhân vật đứng sẵn trên quảng trường (level 7).
10. **Tự gom từ quảng trường.** Khi một nhóm vào một ống, các nhân vật **cùng màu đang ở quảng
    trường tự đi theo** vào ống đó (tới khi đầy). Ví dụ: chạm đỏ ở ống A → vào ống rỗng B, 2 đỏ trên
    quảng trường tự vào B theo. Chỉ áp dụng cho ống vừa nhận nhóm; nhân vật trên quảng trường **không**
    tự vào một ống rỗng nào đó (level 7).
11. **Nhân vật ẩn (Hidden, mở ở level 8).** Hiện khối xám có dấu "?" (đang ngủ). Lộ màu khi trở thành
    nhân vật **đầu ống**. Có popup giới thiệu "Hidden Unlocked!" lần đầu.
12. **Nhà (House, mở ở level 20).** Một khối "nhà" màu X chiếm chỗ một ống (cửa quay về quảng
    trường), bên trong là các nhân vật bị nhốt — không thấy, không chạm được, và **chắn đường**.
    Khi một **đội màu X hoàn thành**, **mọi** nhà màu X trên bàn cùng mở và biến thành ống thường chứa
    các nhân vật bên trong (thứ tự đã cố định; có thể có cả nhân vật ẩn "?"). Ví dụ level 25: 5 nhà
    xanh dương mở cùng lúc khi gom đủ 4 xanh dương. Màu nhà và màu nhân vật bên trong khác nhau.
    Popup "House Unlocked!": "Complete the matching color to open the house and free the characters inside."

### 2.3 Thao tác — chọn rồi thả (đổi 2026-11-19)
Đã thử một chạm như game gốc (chốt 2026-11-17); sau khi chơi prototype, đổi sang **hai chạm**: chạm nhóm
để nhấc lên, chạm đích để thả. Người chơi tự quyết đích, game không chọn hộ.

| Đang cầm | Chạm vào | Kết quả |
|---|---|---|
| Không | Ống có nhóm đầu tỉnh (chạm nhân vật nào trong ống hay khoảng trống của ống đều được) | Nhấc **nhóm đầu ống** lên |
| Không | Nhân vật trên quảng trường | Nhấc nhân vật đó lên |
| Không | Ống không có gì để nhấc (rỗng, nhà, đầu ống còn ẩn) / ô trống | Không có gì |
| Nhóm | Chính chỗ đang nhấc | Đặt xuống, bỏ chọn |
| Nhóm từ ống / quảng trường | **Ống hợp lệ** | Nhóm đi vào (đi một phần nếu thiếu chỗ), rồi nhân vật cùng màu trên quảng trường tự đi theo |
| Nhóm từ ống | **Ô trống** trên quảng trường | Con đầu đứng đúng ô đã chạm, các con sau đứng ô trống gần ô đó nhất |
| Nhóm | Ống không hợp lệ nhưng có nhóm nhấc được | **Đổi chọn** sang nhóm của ống đó |
| Nhóm | Nhân vật khác trên quảng trường | Đổi chọn sang nhân vật đó |
| Nhóm | Ống không hợp lệ và không nhấc được | Nhóm đang cầm lắc đầu, đặt xuống; không tính bước |
| Nhóm từ quảng trường | Ô trống | Đặt xuống (quảng trường → quảng trường không phải một bước) |

**Ống hợp lệ** = mở, còn chỗ **và** (rỗng **hoặc** nhân vật gần đầu mở nhất cùng màu, đã lộ).

**Quy tắc**
- Mỗi lần thả thành công = một bước, vào stack Undo. Nhấc lên / đổi chọn không tính bước.
- Nhóm được nhấc nổi lên một chút để báo đang được chọn.
- Model cập nhật tức thì khi thả; View chỉ diễn hoạt đi bộ. Chạm tiếp trong lúc nhân vật đang đi vẫn
  được nhận và tính trên trạng thái model mới.
- Bộ giải (solver) kiểm tra level vẫn dùng luật một chạm; mọi nước một chạm đều làm được bằng chọn rồi
  thả, nên level giải được theo solver thì cũng giải được khi chơi.

### 2.4 Hoàn thành đội & thắng/thua
- **Đội hoàn thành**: một ống chứa đủ `capacity` nhân vật cùng màu → ăn mừng ~1 s → cả ống (ống + đội) rời bàn **[Q]**, giải phóng không gian.
- **Thắng**: mọi đội hoàn thành → màn logo + pháo hoa "Tap to Skip!" **[C]** → popup
  "Level N · **PERFECT!** · Rewards: **20** coin · Continue" (có nút ✕) **[C]**. Level 1–3 (tutorial)
  chuyển thẳng sang level sau, không có popup **[C]**. Vẫn là "PERFECT!" kể cả khi đã dùng Undo/Shuffle (level 12) **[C]**.
- **Độ khó level** **[C]**: Normal (nút xanh lá, 20 coin) · **Hard** (nút tím, "Hard x3", 60 coin; ví dụ
  level 14, 24) · **Super Hard** (nút + HUD đỏ sẫm, "Super Hard x5", 100 coin; level 19). Popup thắng đổi
  màu theo độ khó. Từ level 14 mọi popup thắng có thêm nút **"x2 Coins"** (xem quảng cáo).
- **Thua — "Out of Space!"** **[C]** (level 14): khi quảng trường **đầy kín**, popup hiện ngay (kể cả
  khi vẫn còn nước đi từ ống sang ống):
  1. "Out of Space!" + hình quảng trường đầy + nút **Add Space (900 coin)** + "Hold to see the board" + ✕.
  2. Bấm ✕ → "Continue? — **You will lose a life!**" + nút **Play On (900 coin)** + ✕ (bỏ cuộc, mất 1 tim).
  3. Add Space / Play On → **thêm 1 ô vĩnh viễn vào quảng trường** cho level đó, chơi tiếp.
  Ở level 6 quảng trường đầy nhưng **không** hiện popup, có thể vì luật này chỉ bật từ một level nhất định **[S]**.
- Không có giới hạn thời gian và số bước ở các level đầu **[Q]**. Giới hạn bước là một biến thể cho level khó **[Đ]**.

### 2.5 Vòng lặp
```
Vào level → quan sát ai tỉnh → chạm nhóm → nhóm tự đi về ống/quảng trường → đội đầy → ăn mừng & dọn chỗ
   → (lặp) → thắng → thưởng → level kế tiếp
                 ↘ quảng trường đầy → Out of Space → Add Space (coin) / mất 1 tim
```

---

## 3. Booster

Thanh booster 3 ô ở đáy màn hình **[Q]**; ô mở dần theo level (ô khóa có icon ổ khóa) **[Q]**.

| Booster | Mở ở | Hành vi | Nguồn |
|---|---|---|---|
| **Undo** (mũi tên cam) | Level 5 — "Tap to undo your last move" | Hoàn tác 1 bước, kể cả nhân vật bị đẩy chỗ khi nhường đường | **[C]** |
| **Shuffle** (lốc xoáy tím) | Level 11 — "Tap, then pick a container to shuffle" | Bấm booster → quảng trường tối đi, các ống sáng lên → chọn 1 ống → nhân vật trong ống bay lên rồi rơi lại theo **thứ tự ngẫu nhiên** | **[C]** |
| **Add Container** (khung có dấu "+") | Level 15 — "Tap to add an extra container" | Thêm **1 ống rỗng** gắn vào quảng trường, đến hết level | **[C]** |

- Mỗi booster có **3 lượt** ban đầu (badge số đỏ). Lần dùng trong tutorial **không trừ lượt**; dùng
  thật thì trừ 1 (Undo 3→2, Shuffle 3→2 ở level 12, Add Container 3→2 ở level 15) **[C]**.
- Hết lượt → badge đổi thành dấu **"+"** xanh (mua thêm) **[C]**.
- Mốc Adventure cũng tặng booster (level 25: +1 Undo) **[C]**.
- Ô khóa hiện tooltip "Unlocks at Level N!" **[C]**.
- Hết lượt → mua trong Shop (bundle có kèm booster) **[C]**; mua lẻ bằng coin / rewarded **[S]**.
- Nút **Restart** ở góc trên trái **[Q]** — chưa thử xem có mất tim không **[S]**.
- Không phải booster nhưng cùng vai trò: **Add Space** (+1 ô quảng trường, 900 coin) chỉ xuất hiện trong popup thua (§2.4) **[C]**.

---

## 4. Tiến trình & độ khó

### 4.1 Thông số level (dữ liệu, không hardcode)
- Layout quảng trường (mặt nạ ô), danh sách ống (vị trí, hướng đầu mở, capacity).
- Số màu, số nhân vật mỗi màu (= capacity), trạng thái khởi đầu của từng ống.
- (tùy chọn) ống bị khóa, ô chướng ngại, giới hạn bước.

### 4.2a Nhịp mở nội dung của game gốc **[C]**
| Level | Mở mới |
|---|---|
| 1–3 | Tutorial chạm nhóm, ra quảng trường, gom nhân vật chờ (không popup thắng) |
| 4 | Popup thắng PERFECT! + 20 coin |
| 5 | Booster Undo; màn ăn mừng logo |
| 7 | Màu đỏ; nhân vật đứng sẵn trên quảng trường, ống chưa đầy |
| 8 | **Hidden** (nhân vật "?") |
| sau 9 | Màn **Home** + **Adventure** + Shop |
| 10 | Mốc Adventure: 50 coin |
| 11 | Booster Shuffle |
| 12 | Màu nâu |
| 13 | Màu xanh ngọc |
| 14 | Level **Hard** đầu tiên (×3 thưởng), quảng trường rất nhỏ → lần đầu gặp Out of Space |
| 15 | Booster Add Container; mốc Adventure: tim vô hạn 15 phút |
| 16 | Màu **xanh lá đậm** (đeo băng đô) — gần màu xanh lá thường, tăng độ khó nhận diện |
| 17 | Popup **Edit Profile** bắt buộc nhập tên (chọn avatar/khung) |
| 18 | Bàn 9 ống (6 trên + 3 dưới) |
| 19 | Level **Super Hard** đầu tiên (×5) |
| 20 | **House**; "Episode Unlocked!" chương 2 "Oops, Wrong Mix!" (level 20–49), Home đổi cảnh nền; **Weekly Contest** + Leaderboard; bắt đầu **interstitial sau mỗi level** |
| 21–25 | House nhiều màu, nhiều nhà cùng màu, nhà chứa nhân vật ẩn (level 24) |
| 25 | Mốc Adventure: +1 Undo |

Ống thường chứa 4; quảng trường từ 5–7 ô (level Hard/Super Hard) tới ~15 ô. Tới level 25 chỉ có **2 element**: Hidden (lv8) và House (lv20).

### 4.2 Đường cong độ khó **[Đ]**
| Dải level | Màu | Ống | Quảng trường | Cơ chế mới |
|---|---|---|---|---|
| 1–3 | 2 | 2–3 | 2×4 | Tutorial chạm / đội hoàn thành |
| 4–5 | 3–4 | 3–4 | 2×4 → chữ thập | Ống hai phía; Undo (lv5) |
| 6–15 | 4–5 | 4–5 | chữ L/T, ít ô hơn | Ống nhiều hướng, nhân vật ngủ sâu |
| 16–30 | 5–6 | 5–6 | hẹp | Thêm ô; level "khó" có nhãn |
| 30+ | 6+ | 6+ | đa dạng | Cơ chế đặc biệt (§4.3), nhịp sóng khó–dễ |

Mỗi level phải **giải được** — kiểm bằng solver engine-free (BFS/A* trên trạng thái bàn) trước khi
đưa vào game. Độ khó đo bằng: số bước tối thiểu, độ rộng nhánh, số "bẫy" (nước đi dẫn tới kẹt).

### 4.3 Cơ chế mở rộng (backlog) **[Đ]**
Game gốc đã có Hidden (lv8) và House (lv20, gần giống "ống khóa mở khi đội màu X hoàn thành").
Ứng viên khác cho bản của mình:
- **Ô đá** — ô quảng trường không đi được.
- **Nhân vật đôi** — chiếm 2 ô, cần đường rộng.

---

## 5. Màn hình & UX

| Màn hình / Dialog | Nội dung |
|---|---|
| Splash / Loading | Logo, tải dữ liệu |
| Home **[C]** (từ sau level 9) | Avatar, coin (có nút +), tim "5 · Full", Settings; cảnh nền phòng thí nghiệm với 3 nhân vật lớn; nút Play "Level N" (tím + "Hard x3" ở level khó) |
| Thanh tab dưới **[C]** | Shop · Leaderboard · Home · Adventure · (tab khóa, vẫn khóa ở level 25) |
| Edit Profile **[C]** (level 17) | Bắt buộc nhập tên (không có nút đóng tới khi nhập), tab Avatar (các nhân vật) · Frame (6 khung màu) · Name; nút Save |
| Adventure **[C]** | Đường tiến trình dọc theo level, mốc thưởng mỗi 5 level (lv10: 50 coin · lv15: tim vô hạn 15 phút · lv25: 1 Undo · lv30: tim vô hạn 30 phút · lv35: 150 coin), thẻ chương có ảnh ("Lab Squad", "Oops, Wrong Mix! — Level 20–49"); qua chương có màn "Episode Unlocked! — Tap to Claim" và Home đổi cảnh nền |
| Shop **[C]** | Special Offer (Starter Bundle), các Bundle (coin + bỏ quảng cáo + tim vô hạn + 3 loại booster), gói coin lẻ |
| Leaderboard **[C]** (level 20) | Popup giới thiệu **Weekly Contest** (qua level → leo hạng → rương thưởng). Tab **Weekly**: đếm ngược, bục top 3 có rương, điểm tuần. Tab **Players**: World/Country, xếp theo level cao nhất (top thấy level 1186) |
| Out of Space / Continue? **[C]** | Xem §2.4 |
| Hidden / House Unlocked! **[C]** | Popup giới thiệu element mới, có hình + một câu mô tả, nút ✕ |
| Xin quyền thông báo **[C]** | Hộp thoại Android "Allow Squad Sort to send you notifications?" sau khi mở lại app ở level 21 |
| Gameplay | HUD trên: Restart (trái), "Level N" (giữa), Settings (phải) **[Q]**; bàn chơi giữa; thanh booster đáy **[Q]** |
| Level Complete | Logo + pháo hoa "Tap to Skip!", rồi popup PERFECT! + 20 coin + Continue **[C]**; nhân đôi thưởng bằng rewarded **[Đ]** |
| Settings (popup trong level) | 3 toggle Sound · Haptic · Music, nút Resume (xanh) và Quit (đỏ), nút ✕ **[C]** |
| Đồng ý điều khoản | Hộp thoại Terms & Privacy lúc mở lần đầu **[C]** |
| Tutorial overlay | Bàn tay chỉ + bóng thoại, làm tối phần còn lại. Lần lượt: "Tap buddies to group them!" (lv1), "Tap buddies to move them out." → "Tap to group the buddies." → "Tap waiting buddy to group them." (lv2), "Tap to undo your last move" (lv5), "Tap, then pick a container to shuffle" (lv11), "Tap to add an extra container" (lv15) **[C]** |
| Tooltip booster khóa | "Unlocks at Level N!" **[C]** |

Không thấy coin trên HUD gameplay **[C]**. Level 1–9 mở thẳng vào màn chơi; Home chỉ xuất hiện sau level 9 **[C]**.

Nền là một **khung cảnh công viên nhìn từ trên** (gạch lát, bồn cây, đèn, đài phun nước, ghế) ôm quanh bàn chơi **[Q]**.

---

## 6. Art & âm thanh

- Phong cách **3D render dễ thương, khối bo tròn, màu bão hòa**, nhân vật dạng "khối kẹo dẻo" có tai/sừng/nơ phân biệt từng màu **[Q]**.
- Màu phải phân biệt được cả khi mù màu: mỗi màu đi kèm **hình dạng phụ kiện riêng** (tai mèo, sừng, nơ, lá…) **[Đ]**.
- Feedback juicy: nhân vật nhún khi được chạm, đi bộ có bounce, ống đầy thì cả đội nhảy + mũ tiệc + confetti **[Q]**.
- Âm thanh: tiếng "pop" khi tỉnh, bước chân nhẹ, hợp xướng ngắn khi đội hoàn thành, jingle thắng.
- Nhận diện của mình (tên đội, nhân vật, bảng màu, khung cảnh) do **art direction** chốt trong
  `docs/art-direction.md` (theo rule #17).

---

## 7. Kinh tế & kiếm tiền

### 7.1 Game gốc **[C]**
| Mục | Giá trị quan sát |
|---|---|
| Coin | 1 230 ở lần đầu thấy Home (sau level 9), +20 mỗi level |
| Thưởng thắng | Normal 20 · Hard 60 (×3) · Super Hard 100 (×5) coin; nút x2 bằng quảng cáo |
| Mốc Adventure | Mỗi 5 level: lv10 50 coin · lv15 tim vô hạn 15 phút · lv25 +1 Undo · lv30 tim vô hạn 30 phút · lv35 150 coin |
| Tim | Tối đa 5; mất 1 tim khi bỏ cuộc ở popup thua |
| Add Space / Play On | 900 coin cho +1 ô quảng trường |
| Booster | 3 lượt mỗi loại lúc mở khóa |
| IAP (giá store VN) | Starter Bundle ₫26 000 (1 000 coin + tim 1h + 1 mỗi booster) · Mini ₫129 000 (2 000 coin, bỏ quảng cáo, tim 3h, 1 mỗi booster) · Mega ₫1 300 000 (20 000 coin, tim 36h, ×12 booster) · Ultimate ₫2 600 000 (60 000 coin, tim 72h, ×25 booster) · Coin lẻ 1 000 → 100 000 (₫52 000 → ₫2 600 000) |

**Interstitial**: không có ở level 1–19; từ **sau level 20 thì có sau mỗi level thắng** **[C]**. Gặp cả quảng cáo video có nút Skip, quảng cáo playable (AppLovin) **không có nút đóng trên giả lập** (phải tắt app), và quảng cáo mở thẳng Google Play khi chạm nhầm.

### 7.2 Đề xuất cho bản của mình **[Đ]**
| Nguồn | Chi tiết |
|---|---|
| Coin | Thưởng mỗi level, x2 bằng rewarded; mua booster, Add Space |
| Rewarded ad | x2 thưởng, +1 booster, Add Space miễn phí 1 lần mỗi level |
| Interstitial | Sau khi thắng, từ level ~10, giãn cách tối thiểu 60 s, không sau level thua |
| IAP | Bỏ quảng cáo, gói coin, bundle booster + tim vô hạn |

Các con số là `ConfigKey` (tunable), không phải hằng số trong code.

---

## 8. Ánh xạ sang PrototypeFramework

| Khối | Assembly / vị trí | Ghi chú |
|---|---|---|
| `BoardModel`, `Cell`, `Tube`, `Squad`, luật di chuyển, pathfinding BFS, kiểm thắng/kẹt | `Game.Domain` (engine-free) | Unit test bằng `dotnet test` mỗi vòng lặp |
| Undo stack, booster, `LevelSession` controller | `Game.Application` | Booster gọi service, không publish lệnh (rule #1) |
| Chọn đích cho nhóm, nhường đường | `Game.Domain` (luật) + `Game.Application` (controller) | View chỉ diễn hoạt theo kết quả, không tự quyết (rule #3) |
| Solver + generator level | `Game.Domain` / tool offline | `IRandom` qua constructor, log seed (rule #14) |
| Dữ liệu level | JSON/CSV ngoài `Assets/` + codegen | CSV dùng parser RFC 4180 (rule #3) |
| Nhân vật, ống, quảng trường | `Game.Views`, Renderer dưới `WorldRoot` (rule #16) | View chỉ chuyển tap → tọa độ ô (rule #10), dùng EventSystem (rule #9) |
| HUD, thanh booster | uGUI dưới `GetHost(Ui)` | |
| Win / Out of Space / Continue? / Settings / Unlock popup | Dialog qua manifest + `Scaffold.Sync` (rule #13) | |
| Home / Adventure / Shop / Leaderboard | Screen qua `game.scenes.json` (rule #13) | Thanh tab là widget |
| Tim, coin, booster inventory | `Game.Application` + save | Hồi tim theo giờ thực: thời gian lấy từ một clock port, không `Time.*` trong model (rule #15) |
| Sự kiện | `SquadCompleted`, `HouseOpened`, `LevelWon`, `PlazaFilled`, `LevelAbandoned`, `BoosterUsed`, `HiddenRevealed`, `EpisodeUnlocked` | Thì quá khứ |
| Text | `LocKey` | rule #4 |

Animation đi bộ chạy theo tick cố định qua `GameplayTickDriver` (rule #15); model chỉ biết bước rời rạc.

---

## 9. Khác biệt với game tham chiếu (bắt buộc)

Để không thành bản sao: giữ **cơ chế thể loại** (sort màu + đường đi + ống/quảng trường), nhưng
**không** dùng tên "Squad Sort", logo, thiết kế nhân vật, khung cảnh công viên hay level layout của họ.
Cần chốt riêng: tên game, bộ nhân vật, bối cảnh (ví dụ: sân trường, bến xe buýt, hồ bơi…),
và một **cơ chế ký tên** riêng từ §4.3.

---

## 10. Câu hỏi mở

1. ~~Luật chạm~~ — **đã chốt: chọn rồi thả** (§2.3, đổi 2026-11-19 sau khi chơi prototype; trước đó là một chạm như game gốc).
2. ~~Nhân vật có tự trượt~~ — **không**; dồn về đầu kín (§2.2b-1).
3. ~~Điều kiện thua~~ — **quảng trường đầy = Out of Space** (§2.4). Còn chưa rõ: từ level nào luật này bật (level 6 không có).
4. ~~Booster 2/3~~ — **Shuffle** (lv11) và **Add Container** (lv15) (§3).
4b. Tab thứ 5 vẫn khóa ở level 25; element sau House (level 26+) chưa biết.
4c. Thứ tự ưu tiên khi có nhiều ống hợp lệ cùng lúc (§2.3) chưa kiểm chứng.
5. Tên game, bối cảnh và bộ nhân vật riêng (§9).
6. Có meta (trang trí công viên/sưu tập nhân vật) ở bản prototype hay để sau?
