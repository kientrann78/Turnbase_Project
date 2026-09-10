from pathlib import Path
from collections import deque
import re, uuid, json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
source = Path(r'C:/Users/THIS PC/.codex/generated_images/01a089cd-6107-7e40-8504-1f7f5f2c9c6f/exec-ce10e211-7d1c-4dae-b507-30e8b16212ba.png')
out = ROOT/'Assets/Sprites/Warrior'
anim = ROOT/'Assets/Animation/Warrior'
out.mkdir(parents=True, exist_ok=True)
anim.mkdir(parents=True, exist_ok=True)
def guid(path):
    meta = Path(str(path)+'.meta')
    if meta.exists(): return re.search(r'guid: (\w+)',meta.read_text()).group(1)
    value = uuid.uuid4().hex
    meta.write_text('fileFormatVersion: 2\nguid: '+value+'\n')
    return value
for folder in [out,anim]: guid(folder)

rgb = np.asarray(Image.open(source).convert('RGB'))
# Flood connected neutral checkerboard regions; retain enclosed metallic highlights.
candidate = (rgb.min(axis=2)>145) & ((rgb.max(axis=2).astype(int)-rgb.min(axis=2))<24)
h,w = candidate.shape
seen = np.zeros((h,w),bool)
background = np.zeros((h,w),bool)
for y,x in zip(*np.where(candidate)):
    if seen[y,x]: continue
    q=deque([(int(y),int(x))]); seen[y,x]=True; component=[]
    while q:
        cy,cx=q.popleft(); component.append((cy,cx))
        for ny,nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
            if 0<=ny<h and 0<=nx<w and candidate[ny,nx] and not seen[ny,nx]:
                seen[ny,nx]=True; q.append((ny,nx))
    # Large checkerboard areas include holes between limbs and weapon.
    if len(component)>180:
        yy,xx=zip(*component); background[yy,xx]=True
rgba=np.dstack([rgb,np.where(background,0,255).astype('uint8')])
sheet=Image.fromarray(rgba)
sheet.save(ROOT/'Art/Warrior/Warrior-transparent.png')
template=(ROOT/'Assets/Sprites/Cursor/StoneCursorWenrexa/StoneCursorWenrexa/PNG/01.png.meta').read_text()
frame_guids=[]
frames=[]
for i in range(24):
    x,y=(i%6)*256,(i//6)*256
    frame=sheet.crop((x,y,x+256,y+256))
    path=out/f'Warrior_{i:02}.png'; frame.save(path); frames.append(frame)
    g=guid(path); frame_guids.append(g)
    meta=re.sub(r'guid: \w+', 'guid: '+g,template,count=1)
    meta=re.sub(r'  internalIDToNameTable:.*?  externalObjects:', '  internalIDToNameTable: []\n  externalObjects:',meta,flags=re.S)
    meta=meta[:meta.index('  spriteSheet:')]+'''  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    meta=meta.replace('spritePixelsToUnits: 100','spritePixelsToUnits: 64').replace('alignment: 0','alignment: 9').replace('spritePivot: {x: 0.5, y: 0.5}','spritePivot: {x: 0.42, y: 0.48}')
    Path(str(path)+'.meta').write_text(meta)

sequences={'Idle':([0,1,2,3,4,5,4,3,2,1],1.0,True),
 'Attack':([0,6,7,9,10,11,12,13,14,15,16,17],0.7,False),
 'Hit':([18,19,20,21,22,23],0.4,False),
 'Death':([20],0.4,False)}
clip_template=(ROOT/'Assets/Animation/Knight/Idle.anim').read_text()
clip_guids={}
for name,(indices,duration,loop) in sequences.items():
    clip=clip_template.replace('m_Name: Idle','m_Name: '+name)
    keys=''.join(f'    - time: {j*duration/len(indices):.7f}\n      value: {{fileID: 21300000, guid: {frame_guids[i]}, type: 3}}\n' for j,i in enumerate(indices))
    clip=re.sub(r'    curve:.*?    attribute:', '    curve:\n'+keys+'    attribute:',clip,flags=re.S)
    mapping=''.join(f'    - {{fileID: 21300000, guid: {frame_guids[i]}, type: 3}}\n' for i in indices)
    clip=re.sub(r'    pptrCurveMapping:.*?  m_AnimationClipSettings:', '    pptrCurveMapping:\n'+mapping+'  m_AnimationClipSettings:',clip,flags=re.S)
    clip=re.sub(r'm_StopTime: [^\n]+','m_StopTime: '+str(duration),clip)
    clip=clip.replace('m_LoopTime: 1','m_LoopTime: '+str(int(loop)))
    path=anim/(name+'.anim'); path.write_text(clip); clip_guids[name]=guid(path)
    if name!='Death':
        previews=[]
        for i in indices:
            bg=Image.new('RGBA',(256,256),(42,47,42,255)); bg.alpha_composite(frames[i]); previews.append(bg.convert('RGB'))
        previews[0].save(ROOT/f'Art/Warrior/{name}-preview.gif',save_all=True,append_images=previews[1:],duration=int(duration*1000/len(indices)),loop=0)

controller=(ROOT/'Assets/Animation/Knight/Knight.controller').read_text()
blocks=re.split(r'(?m)(?=^--- !u!)',controller)
state_tpl=next(b for b in blocks if b.startswith('--- !u!1102') and 'm_Name: Attack\n' in b)
transition_tpl=next(b for b in blocks if b.startswith('--- !u!1101') and 'm_ConditionEvent: Attack' in b)
return_tpl=next(b for b in blocks if b.startswith('--- !u!1101') and 'm_Conditions: []' in b)
controller=controller.replace('f2add054b4f899146bd3d1aec18a1088',clip_guids['Idle']).replace('3e99116c8fcbc154189dae3ff85f51b4',clip_guids['Attack']).replace('m_Name: Knight','m_Name: Warrior')
for j,name in enumerate(['Hit','Death'], start=1):
    stateid=str(110200+j); transid=str(110100+j); returnid=str(110110+j)
    state=state_tpl.replace('-4082245670990770391',stateid).replace('m_Name: Attack','m_Name: '+name).replace('3e99116c8fcbc154189dae3ff85f51b4',clip_guids[name]).replace('9157544724162238717',returnid)
    if name=='Death': state=state.replace('m_Transitions:\n  - {fileID: '+returnid+'}','m_Transitions: []')
    trans=transition_tpl.replace('-346591338695753932',transid).replace('-4082245670990770391',stateid).replace('m_ConditionEvent: Attack','m_ConditionEvent: '+name)
    ret=return_tpl.replace('9157544724162238717',returnid)
    controller=controller.replace('  m_ChildStateMachines: []',f'  - serializedVersion: 1\n    m_State: {{fileID: {stateid}}}\n    m_Position: {{x: 500, y: {200+j*90}, z: 0}}\n  m_ChildStateMachines: []')
    controller=controller.replace('  m_EntryTransitions: []',f'  - {{fileID: {transid}}}\n  m_EntryTransitions: []')
    controller=controller.replace('  m_AnimatorLayers:',f'  - m_Name: {name}\n    m_Type: 9\n    m_DefaultFloat: 0\n    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {{fileID: 9100000}}\n  m_AnimatorLayers:')
    controller+='\n'+state+trans+(ret if name!='Death' else '')
controller=controller.replace('m_TransitionDuration: 0.25','m_TransitionDuration: 0').replace('m_ExitTime: 0.64705884','m_ExitTime: 1').replace('m_CanTransitionToSelf: 1','m_CanTransitionToSelf: 0')
path=anim/'Warrior.controller'; path.write_text(controller); cg=guid(path)
prefab=ROOT/'Assets/Prefabs/Knight.prefab'; text=prefab.read_text()
text=text.replace('m_Controller: {fileID: 9100000, guid: 95471b720a8015e4c8554a52b4326d3b, type: 2}',f'm_Controller: {{fileID: 9100000, guid: {cg}, type: 2}}')
text=text.replace('m_Sprite: {fileID: 8906952961551753382, guid: eeb9df83cdc0453428b4715c5880161b, type: 3}',f'm_Sprite: {{fileID: 21300000, guid: {frame_guids[0]}, type: 3}}')
prefab.write_text(text)
print(json.dumps({'frames':24,'transparent_pixels':int(background.sum()),'clips':list(sequences),'controller':cg}))
