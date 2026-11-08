"""Checks serialized Unity nature references without needing the Unity Editor."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1] / "unity" / "LittleEmpires" / "Assets" / "LittleEmpires"
scene = (root / "Scenes" / "LargeIsland.unity").read_text(encoding="utf-8")
asset_dir = root / "Scenes" / "EnvironmentArtTestAssets"
asset = (asset_dir / "Island Nature Prefabs.asset").read_text(encoding="utf-8")
script_meta = (root / "Runtime" / "NaturePrefabCatalog.cs.meta").read_text(encoding="utf-8")
catalog_meta = (asset_dir / "Island Nature Prefabs.asset.meta").read_text(encoding="utf-8")

def guid(text):
    match = re.search(r"^guid: ([0-9a-f]{32})$", text, re.MULTILINE)
    assert match, "Missing Unity asset GUID"
    return match.group(1)

script_guid = guid(script_meta)
catalog_guid = guid(catalog_meta)
assert f"guid: {script_guid}, type: 3" in asset, "Nature asset lost its MonoScript reference"
assert f"naturePrefabs: {{fileID: 11400000, guid: {catalog_guid}, type: 2}}" in scene, (
    "LargeIsland scene is not linked to the persisted Quaternius nature catalog"
)

nature_dir = root / "Art" / "QuaterniusNature" / "Stylized Nature MegaKit[Standard]" / "FBX (Unity)"
models = {guid(path.read_text(encoding="utf-8")): path for path in nature_dir.glob("*.fbx.meta")}
assert len(models) > 30, "Nature pack FBX metas unavailable"

for group in ["Trees", "Bushes", "Rocks", "Grasses", "Flowers"]:
    match = re.search(rf"^  {group}:\n((?:  - .*\n)+)", asset, re.MULTILINE)
    assert match, f"Empty or missing serialized group: {group}"
    refs = re.findall(r"^  - \{fileID: (\d+), guid: ([0-9a-f]{32}), type: 3\}$",
                      match.group(1), re.MULTILINE)
    assert refs, f"No valid references: {group}"
    for file_id, model_guid in refs:
        assert file_id == "919132149155446097", f"Unexpected FBX prefab root file ID for {group}"
        assert model_guid in models, f"Broken Quaternius {group} model GUID: {model_guid}"

print("PASS: LargeIsland nature catalog and all five Quaternius asset groups are linked.")
