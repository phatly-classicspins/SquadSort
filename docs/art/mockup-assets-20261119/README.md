# Bộ asset 3D — Squad Sort mockup

Đã có **16 file FBX thật** và 2 scene Blender có thể chỉnh sửa. Không phải ảnh nhân vật/khay giả 3D.

## Nội dung

- 10 nhân vật: tím, xanh dương, xanh lá, cam, vàng, đỏ, hồng, nâu, xanh ngọc và nhân vật ẩn màu xám.
- Khay hàng bốn nhân vật, mở một đầu (`models/queue-four`).
- Ô chờ bảy ô dạng bậc thang (`models/waiting-board`), là bố cục mẫu.
- 4 mảnh lắp ghép: nền ô, viền thẳng, góc ngoài, góc trong.
- Nền sân vườn và atlas UI PNG nền trong suốt của gói trước.

Mỗi thư mục model có FBX, `build.py`, ảnh render hero và bốn hướng, cùng dữ liệu kiểm tra/provenance. `models/model-manifest.json` liệt kê chính xác file, số tam giác và kích thước.

## File Blender và preview

- `preview/character-family-studio.blend`: cả 10 nhân vật, mỗi nhân vật trong collection riêng; camera và đèn studio có sẵn.
- `preview/container-studio.blend`: khay và các mảnh ô chờ.
- Hai PNG cùng tên là render từ chính các file FBX đã mở lại trong Blender, không dùng image generation để giả hình model.

## Quy ước nhập Unity

Nguồn dùng mét, FBX import scale 1; Y up, mặt trước +Z theo quy trình export của dự án. Pivot hình học ở đáy và tâm footprint. Material có tên `M_<palette_key>` để remap sang shader của SKU; roughness nguồn 0.35. Màu nằm trong `design-tokens.json`.

Nhân vật có Body, Eye_L, Eye_R, Mouth, Arm_L/R, Foot_L/R và Accessory riêng. **Chưa có armature/rig hay animation clip.** Nhân vật đỏ dùng Accessory làm anchor rỗng vì thiết kế không có phụ kiện rời.

Ô chờ dùng pitch 1.15 m, mặt đứng tại 0.04 m. Bỏ mảnh viền tại cửa nối vào khay; không dùng cả viền kín của bố cục mẫu cho mọi level. Khay rộng 1.28 m nên không xếp khay ở tất cả các ô liền kề pitch 1.15 m; bố trí cách ô hoặc thiết kế khoảng cách phù hợp.

Scene/renderer scale trong rig 1 pixel = 1 world unit, layer/host, prefab và shader của game chưa được tích hợp. Hai file `.blend` và các FBX mở độc lập được; các `build.py` dùng helper `mg` của Tools/pf/model-gen.py trong repository này.

## Kiểm tra và giới hạn

- Model-gen geometry: 16/16 pass (kích thước, pivot, tam giác, bộ phận, material, scale dương).
- Mở lại FBX trong Blender: 16/16 pass (mesh, kích thước, tam giác và bộ phận được bảo toàn).
- Engine-free project gate: passed using a temporary PF_GATE_CONF override to SqadSort.SkuHeadlessTests.csproj; the unchanged default config still points to missing MyGame.SkuHeadlessTests.csproj. Đây là gate tổng của checkout hiện tại, không phải kiểm thử render Unity.
- Container outline score chưa đạt mục tiêu tự động 0.88; chi tiết trong MODELS.md. Mesh đã được xem qua ảnh render.

Skipped: Unity import và kiểm tra tích hợp runtime.
Skipped: Unity EditMode.
Skipped: Unity PlayMode suite.
Skipped: Unity batchmode.

Không sửa scene, prefab hay gameplay C# trong tác vụ tạo asset này. Thiết kế blueprint đã được duyệt; mesh xuất hiện tại chờ người dùng xem lại.
