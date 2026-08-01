#!/usr/bin/env python3
from __future__ import annotations
import argparse, json, pathlib, re, sys

ROOT=pathlib.Path(__file__).resolve().parents[1]
MOD=ROOT/'GuZhenRen'
errors=[]; warnings=[]

def err(x): errors.append(x)
def warn(x): warnings.append(x)

for p in ROOT.rglob('*.json'):
    try: json.loads(p.read_text(encoding='utf-8'))
    except Exception as e: err(f'Invalid JSON {p.relative_to(ROOT)}: {e}')

manifest=json.loads((ROOT/'GuZhenRen.json').read_text(encoding='utf-8'))
if manifest.get('min_game_version')!='0.110.0': err('manifest min_game_version must be 0.110.0')
if 'pck_name' in manifest: err('pck_name is not part of the 0.110 ModManifest')
deps=manifest.get('dependencies',[])
if deps!=[{'id':'STS2-RitsuLib','min_version':'0.5.2'}]: err('manifest dependencies must require only STS2-RitsuLib >= 0.5.2')

proj=(ROOT/'GuZhenRen.csproj').read_text(encoding='utf-8')
for required in ['<Sts2ApiCompat>0.110.0</Sts2ApiCompat>','Include="STS2.RitsuLib"','>0.5.2</RitsuLibVersion>']:
    if required not in proj: err(f'csproj missing {required}')
for forbidden in ['Compat.0.107.1','MinionLib']:
    if forbidden in proj: err(f'csproj still contains {forbidden}')

banned_files=[
'GuZhenRenCode/Aperture/ApertureRewardPatch.cs','GuZhenRenCode/Interop/MinionLibSupport.cs',
'GuZhenRenCode/Patch/GuRankInitializationPatch.cs','GuZhenRenCode/Patch/LocalizationCompatibilityPatch.cs',
'GuZhenRenCode/Patch/XuYingCompatibilityPatch.cs','GuZhenRenCode/Powers/Core/IHealAmountModifier.cs',
'GuZhenRenCode/UI/OrbPreview.cs','GuZhenRenCode/Patch/GuRestSiteMultiUsePatch.cs',
'GuZhenRenCode/RestSite/GuRestSiteMultiUseCoordinator.cs']
for rel in banned_files:
    if (ROOT/rel).exists(): err(f'dead file remains: {rel}')

combined='\n'.join(p.read_text(encoding='utf-8',errors='replace') for p in ROOT.rglob('*.cs'))
for forbidden in ['IGuMultiUseRestSiteOption','GuRestSiteMultiUseCoordinator']:
    if forbidden in combined: err(f'forbidden/dead construct remains: {forbidden}')
for pattern in [r'new\s+(?:System\.)?Random\s*\(', r'Random\.Shared', r'GD\.Rand', r'new\s+RandomNumberGenerator\s*\(']:
    code_without_line_comments=re.sub(r'//.*', '', combined)
    if re.search(pattern, code_without_line_comments): err(f'nondeterministic RNG pattern remains: {pattern}')
if 'GuRestSiteChoicePatch.Initialize' not in combined: err('multiplayer rest-site choice patch is not registered')
ncard=(ROOT/'GuZhenRenCode/Patch/NCardXuYingEnergyIconPatch.cs').read_text(encoding='utf-8')
if '[typeof(PileType)]' not in ncard: err('NCard patch is not bound to 0.110 UpdateEnergyCostVisuals(PileType)')
if 'ModelVisibility.Visible' not in ncard: err('NCard patch can leak hidden card identity')

langs=[p for p in (MOD/'localization').iterdir() if p.is_dir()]
files={lang.name:{p.name for p in lang.glob('*.json')} for lang in langs}
if files.get('eng')!=files.get('zhs'): err(f'locale table mismatch: {files}')
for name in sorted(files.get('eng',set())):
    tables={lang.name:json.loads((lang/name).read_text(encoding='utf-8')) for lang in langs}
    if set(tables['eng'])!=set(tables['zhs']): err(f'locale key mismatch in {name}')
    for lang,data in tables.items():
        for k,v in data.items():
            if isinstance(v,str) and ('[TODO' in v or '模板角色' in v or '模板卡牌' in v):
                err(f'template text remains: {lang}/{name}:{k}')
if 'rest_site_ui.json' not in files.get('eng',set()): err('rest-site localization must use rest_site_ui.json')
if 'rest_site_options.json' in files.get('eng',set()): err('obsolete rest_site_options.json remains')

for p in [*MOD.rglob('*.tscn'),*MOD.rglob('*.tres')]:
    text=p.read_text(encoding='utf-8',errors='replace')
    for m in re.finditer(r'path="res://([^"]+)"',text):
        if not (ROOT/m.group(1)).exists(): err(f'missing resource: {p.relative_to(ROOT)} -> {m.group(1)}')

# Simple brace sanity after removing comments/strings is intentionally conservative.
for p in ROOT.rglob('*.cs'):
    text=p.read_text(encoding='utf-8',errors='replace')
    if text.count('{')!=text.count('}'):
        err(f'brace count mismatch: {p.relative_to(ROOT)}')

parser=argparse.ArgumentParser(); parser.add_argument('--game-source',type=pathlib.Path); args=parser.parse_args()
if args.game_source:
    game=args.game_source
    critical={
      'src/Core/Multiplayer/Game/RestSiteSynchronizer.cs':['private async Task<bool> ChooseOption(Player player, int optionIndex)','public void LocalOptionHovered(RestSiteOption? option)','public int? GetHoveredOptionIndex(ulong playerId)'],
      'src/Core/Entities/RestSite/RestSiteOption.cs':['new LocString("rest_site_ui"','public static List<RestSiteOption> Generate(Player player)'],
      'src/Core/Nodes/Cards/NCard.cs':['UpdateEnergyCostVisuals(PileType'],
      'src/Core/Modding/ModDependency.cs':['JsonPropertyName("min_version")'],
      'src/Core/Entities/Players/Player.cs':['PopulateStartingDeck'],
      'src/Core/Rewards/CardReward.cs':['void Populate']}
    for rel,patterns in critical.items():
        p=game/rel
        if not p.exists(): err(f'game source missing critical file {rel}'); continue
        text=p.read_text(encoding='utf-8',errors='replace')
        for pat in patterns:
            if pat not in text: err(f'0.110 API signature not found: {rel}: {pat}')
else:
    warn('game-source API signature checks were not run; pass --game-source PATH')

placeholder_manifest = ROOT / 'PLACEHOLDER_ASSETS.md'
if placeholder_manifest.exists():
    listed = [
        line for line in placeholder_manifest.read_text(encoding='utf-8').splitlines()
        if line.startswith('- `')
    ]
    warn(
        f'{len(listed)} placeholder textures remain and must be replaced before release'
    )

print(f'Errors: {len(errors)}')
for x in errors: print('ERROR:',x)
print(f'Warnings: {len(warnings)}')
for x in warnings: print('WARNING:',x)
sys.exit(1 if errors else 0)
