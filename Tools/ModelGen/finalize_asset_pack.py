"""Inventory the validated asset deliverables and archive source art + actual meshes."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'docs/art/mockup-assets-20261119'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main(gate_exit):
    audits=json.loads((PACK/'fbx-roundtrip-validation.json').read_text())
    assert len(audits)==16 and all(a['ok'] for a in audits)
    manifest=json.loads((PACK/'asset-manifest.json').read_text())
    models=[]
    for row in audits:
        fbx=PACK/row['path']; folder=fbx.parent
        gen=json.loads((folder/'model.gen.json').read_text())
        assert gen['facts']['ok'],row['asset']
        models.append({'id':row['asset'],'fbx':row['path'],'sha256':digest(fbx),
                       'source':(folder/'build.py').relative_to(PACK).as_posix(),
                       'hero':(folder/'hero.png').relative_to(PACK).as_posix(),
                       'views':(folder/'views.png').relative_to(PACK).as_posix(),
                       'triangles':row['tris'],'size_m':row['size'],
                       'validation':'geometry-pass-and-fbx-roundtrip-pass',
                       'visual_status':'pending-user-review','rigged':False,'clips':[]})
    manifest.update(status='3d-meshes-exported-and-validated',models_delivered=models,
                    models_pending=[],unity_import_performed=False,
                    validations={'geometry_pass':16,'fbx_roundtrip_pass':16,
                                 'engine_free_gate_exit':gate_exit},
                    source_units='metres',unity_front='+Z',unity_up='+Y',import_scale=1)
    (PACK/'asset-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    (PACK/'models/model-manifest.json').write_text(json.dumps(models,indent=2)+'\n')
    tokens=json.loads((PACK/'design-tokens.json').read_text())
    for character in tokens['character_variants']:
        character['status']='mesh-exported'
        character['fbx']=next(m['fbx'] for m in models if m['id']=='character-'+character['id'])
    tokens['material_finish']={'roughness':.35,'metallic':0,'palette_space':'sRGB hex converted to linear in source'}
    tokens['module_pitch_m']=1.15
    tokens['standing_floor_m']=.04
    (PACK/'design-tokens.json').write_text(json.dumps(tokens,ensure_ascii=False,indent=2)+'\n')
    (PACK/'validation').mkdir(exist_ok=True)
    shutil.copy2(ROOT/'Tools/ModelGen/.runtime/gate.log',PACK/'validation/engine-free-gate.log')
    for source_name,target_name in [('gate-default.log','engine-free-gate-default.log'),('gate-result.json','engine-free-gate-result.json')]:
        source=ROOT/'Tools/ModelGen/.runtime'/source_name
        if source.exists(): shutil.copy2(source,PACK/'validation'/target_name)
    (PACK/'validation/summary.json').write_text(json.dumps({'fbx_files':16,'geometry_pass':16,
        'fbx_roundtrip_pass':16,'engine_free_gate_exit':gate_exit,
        'engine_free_gate_used_temporary_project_override':True,
        'default_gate_configuration_issue':'MyGame.SkuHeadlessTests.csproj is configured but absent; actual project is SqadSort.SkuHeadlessTests.csproj',
        'unity_import':'not run','unity_editmode':'not run','unity_playmode':'not run',
        'unity_batchmode':'not run','rigging':'not authored','animation_clips':'not authored'},indent=2)+'\n')
    (PACK/'requests').mkdir(exist_ok=True)
    for request in (ROOT/'Tools/ModelGen/requests').glob('*.json'):
        data=json.loads(request.read_text())
        if data.get('out','').startswith('docs/art/mockup-assets-20261119/models/'):
            shutil.copy2(request,PACK/'requests'/request.name)
    rows=['# Models\n\nActual FBX meshes; no character or container is a billboard.\n',
          '| Asset | Triangles | W × H × D (m) |','|---|---:|---|']
    for model in models:
        size=model['size_m']
        rows.append(f"| {model['id']} | {model['triangles']} | {size['width']:.3f} × {size['height']:.3f} × {size['length']:.3f} |")
    rows += ['\nAll 16 passed the model-gen geometry checks and FBX re-import checks.',
             '\nThe purple base outline score is 0.892. Queue and waiting sample scores are 0.606 and 0.345; they did not reach the 0.88 automatic outline target. The generated reference elevations contain perspective; the container meshes were also inspected visually. These scores are not Unity runtime tests.',
             '\nThe waiting board uses the visually cleaner first round rather than a later round with triangulated shading artifacts. Its standing surface was aligned to the queue at 0.04 m and rechecked.',
             '\nBlueprints were approved by the user. Exported meshes remain pending visual review.']
    (PACK/'MODELS.md').write_text('\n'.join(rows)+'\n')
    gate_note=('passed using a temporary PF_GATE_CONF override to SqadSort.SkuHeadlessTests.csproj; '
               'the unchanged default config still points to missing MyGame.SkuHeadlessTests.csproj') if gate_exit==0 else f'did not pass (exit {gate_exit}); see validation/engine-free-gate.log'
    (PACK/'README.md').write_text(f'''# Bộ asset 3D — Squad Sort mockup\n\nĐã có **16 file FBX thật** và 2 scene Blender có thể chỉnh sửa. Không phải ảnh nhân vật/khay giả 3D.\n\n## Nội dung\n\n- 10 nhân vật: tím, xanh dương, xanh lá, cam, vàng, đỏ, hồng, nâu, xanh ngọc và nhân vật ẩn màu xám.\n- Khay hàng bốn nhân vật, mở một đầu (`models/queue-four`).\n- Ô chờ bảy ô dạng bậc thang (`models/waiting-board`), là bố cục mẫu.\n- 4 mảnh lắp ghép: nền ô, viền thẳng, góc ngoài, góc trong.\n- Nền sân vườn và atlas UI PNG nền trong suốt của gói trước.\n\nMỗi thư mục model có FBX, `build.py`, ảnh render hero và bốn hướng, cùng dữ liệu kiểm tra/provenance. `models/model-manifest.json` liệt kê chính xác file, số tam giác và kích thước.\n\n## File Blender và preview\n\n- `preview/character-family-studio.blend`: cả 10 nhân vật, mỗi nhân vật trong collection riêng; camera và đèn studio có sẵn.\n- `preview/container-studio.blend`: khay và các mảnh ô chờ.\n- Hai PNG cùng tên là render từ chính các file FBX đã mở lại trong Blender, không dùng image generation để giả hình model.\n\n## Quy ước nhập Unity\n\nNguồn dùng mét, FBX import scale 1; Y up, mặt trước +Z theo quy trình export của dự án. Pivot hình học ở đáy và tâm footprint. Material có tên `M_<palette_key>` để remap sang shader của SKU; roughness nguồn 0.35. Màu nằm trong `design-tokens.json`.\n\nNhân vật có Body, Eye_L, Eye_R, Mouth, Arm_L/R, Foot_L/R và Accessory riêng. **Chưa có armature/rig hay animation clip.** Nhân vật đỏ dùng Accessory làm anchor rỗng vì thiết kế không có phụ kiện rời.\n\nÔ chờ dùng pitch 1.15 m, mặt đứng tại 0.04 m. Bỏ mảnh viền tại cửa nối vào khay; không dùng cả viền kín của bố cục mẫu cho mọi level. Khay rộng 1.28 m nên không xếp khay ở tất cả các ô liền kề pitch 1.15 m; bố trí cách ô hoặc thiết kế khoảng cách phù hợp.\n\nScene/renderer scale trong rig 1 pixel = 1 world unit, layer/host, prefab và shader của game chưa được tích hợp. Hai file `.blend` và các FBX mở độc lập được; các `build.py` dùng helper `mg` của Tools/pf/model-gen.py trong repository này.\n\n## Kiểm tra và giới hạn\n\n- Model-gen geometry: 16/16 pass (kích thước, pivot, tam giác, bộ phận, material, scale dương).\n- Mở lại FBX trong Blender: 16/16 pass (mesh, kích thước, tam giác và bộ phận được bảo toàn).\n- Engine-free project gate: {gate_note}. Đây là gate tổng của checkout hiện tại, không phải kiểm thử render Unity.\n- Container outline score chưa đạt mục tiêu tự động 0.88; chi tiết trong MODELS.md. Mesh đã được xem qua ảnh render.\n\nSkipped: Unity import và kiểm tra tích hợp runtime.\nSkipped: Unity EditMode.\nSkipped: Unity PlayMode suite.\nSkipped: Unity batchmode.\n\nKhông sửa scene, prefab hay gameplay C# trong tác vụ tạo asset này. Thiết kế blueprint đã được duyệt; mesh xuất hiện tại chờ người dùng xem lại.\n''',encoding='utf-8')
    archive=PACK.parent/'mockup-assets-3d-v1.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for path in sorted(PACK.rglob('*')):
            if path.is_file() and path.suffix not in ('.blend1','.log'):
                z.write(path,path.relative_to(PACK))
        z.write(PACK/'validation/engine-free-gate.log','validation/engine-free-gate.log')
        default_log=PACK/'validation/engine-free-gate-default.log'
        if default_log.exists(): z.write(default_log,'validation/engine-free-gate-default.log')
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert len([n for n in z.namelist() if n.endswith('.fbx')])==16
        assert len([n for n in z.namelist() if n.endswith('.blend')])==2
    print(json.dumps({'archive':str(archive),'bytes':archive.stat().st_size,'fbx':len(models),
                      'blend':2,'character_triangle_range':[min(m['triangles'] for m in models if m['id'].startswith('character-')),
                       max(m['triangles'] for m in models if m['id'].startswith('character-'))]},indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--gate-exit',type=int,required=True)
    main(parser.parse_args().gate_exit)
