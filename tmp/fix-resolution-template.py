from pathlib import Path
import re
p=Path('Assets/Scenes/StartScene.unity')
s=p.read_text(encoding='utf-8-sig')
parts=re.split(r'(?=^--- !u!)',s,flags=re.M)
records={int(re.search(r'&(-?\d+)',part).group(1)):part for part in parts[1:]}
root=479200123
target=1060719884
target_transform=1060719880
assert 'm_Template: {fileID: 0}' in records[target]
assert 'RectTransform:' in records[target_transform]
selected=set()
def collect(transform):
    rec=records[transform]
    go=int(re.search(r'm_GameObject: \{fileID: (\d+)\}',rec).group(1))
    selected.add(go)
    for cid in re.findall(r'component: \{fileID: (\d+)\}',records[go]): selected.add(int(cid))
    block=rec.split('  m_Children:')[1].split('  m_Father:')[0]
    for child in re.findall(r'fileID: (\d+)',block): collect(int(child))
collect(root)
assert 949758536 in selected
mapping={old:900100000+i for i,old in enumerate(sorted(selected))}
assert not set(mapping.values()) & set(records)
clones=[]
for old in sorted(selected):
    rec=records[old]
    rec=re.sub(r'(?m)^(--- !u!\d+ &)-?\d+',lambda m:m.group(1)+str(mapping[old]),rec)
    rec=re.sub(r'\{fileID: (-?\d+)\}',lambda m:'{fileID: '+str(mapping.get(int(m.group(1)),int(m.group(1))))+'}',rec)
    if old==root: rec=re.sub(r'm_Father: \{fileID: \d+\}',f'm_Father: {{fileID: {target_transform}}}',rec)
    clones.append(rec)
records[target]=records[target].replace('m_Template: {fileID: 0}',f'm_Template: {{fileID: {mapping[root]}}}').replace('m_ItemText: {fileID: 0}',f'm_ItemText: {{fileID: {mapping[949758536]}}}')
records[target_transform]=records[target_transform].replace('  m_Children:\n','  m_Children:\n  - {fileID: '+str(mapping[root])+'}\n')
assert str(mapping[root]) in records[target_transform]
new=parts[0]+''.join(records[int(re.search(r'&(-?\d+)',part).group(1))] for part in parts[1:])
# Unity's SceneRoots stays last; the cloned template is parented, never a scene root.
pos=new.find('--- !u!1660057539')
new=new[:pos]+''.join(clones)+new[pos:] if pos>=0 else new+''.join(clones)
p.write_text(new,encoding='utf-8')
print(f'Cloned {len(clones)} template records; all internal references remapped.')
