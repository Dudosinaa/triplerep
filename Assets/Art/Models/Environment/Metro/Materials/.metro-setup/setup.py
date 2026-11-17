from pathlib import Path
import re, json, uuid, zipfile, hashlib, sys

ROOT = Path(__file__).resolve().parents[2]
WORK = Path(__file__).resolve().parent
TEX = ROOT / 'Textures'
MATS = ROOT / 'Materials'
PACKED = TEX / 'Packed'
NS = uuid.UUID('a604f11c-6092-02f4-8a93-d013c2b2d537')

def write(path, text):
    assert path.resolve().is_relative_to(ROOT.resolve())
    path.write_bytes(text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))

def guid(path):
    return re.search(r'^guid: (\w+)', Path(str(path)+'.meta').read_text(), re.M)[1]

def family(name):
    base = re.sub(r'\.\d+$', '', name)
    aliases = {'CableTexturePack':'CablesTexturePack', 'Techos Modulares':'Techos_Modulares',
               'CartelSalida_Izquierda':'CartelSalida'}
    if base.startswith('CartelInformativoV'): return 'CartelInformativo'
    if base.startswith('EscaleraV'): return 'Escalera'
    return aliases.get(base, base)

def prepare():
    assert not (WORK/'originals.zip').exists(), 'Backup exists; do not overwrite'
    originals = [p for d in ['Models','Materials','Textures'] for p in (ROOT/d).rglob('*')
                 if p.is_file() and WORK not in p.parents]
    baseline = {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in originals}
    write(WORK/'baseline.json', json.dumps(baseline, indent=2))
    with zipfile.ZipFile(WORK/'originals.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in originals:
            if p.suffix in ['.meta','.mat']: z.write(p,p.relative_to(ROOT))
    mapping = {p.stem:family(p.stem) for p in MATS.glob('*.mat')}
    families = sorted(set(mapping.values())-{'Black','JoinedDecals'})
    jobs=[]
    template=(TEX/'Banco_roughness.jpeg.meta').read_text()
    PACKED.mkdir(exist_ok=True)
    write(Path(str(PACKED)+'.meta'), 'fileFormatVersion: 2\nguid: '+uuid.uuid5(NS,'Packed').hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    for base in families:
        rough=TEX/(base+'_roughness.jpeg')
        assert rough.is_file(), rough
        albedo=TEX/(base+'_albedo.jpeg')
        assert albedo.is_file(), albedo
        output=PACKED/(base+'_MSAO.png')
        meta=re.sub(r'^guid: \w+', 'guid: '+uuid.uuid5(NS,base+'_MSAO').hex,template,flags=re.M)
        meta=meta.replace('sRGBTexture: 1','sRGBTexture: 0')
        # Alpha stores smoothness data, not coverage; keep alphaIsTransparency disabled.
        write(Path(str(output)+'.meta'),meta)
        jobs.append({'roughness':str(rough),'metallic':str(TEX/(base+'_metallic.jpeg')) if (TEX/(base+'_metallic.jpeg')).exists() else None,
                     'ao':str(TEX/(base+'_AO.jpeg')) if (TEX/(base+'_AO.jpeg')).exists() else None,'output':str(output)})
    write(WORK/'jobs.json',json.dumps(jobs,indent=2))
    write(WORK/'mapping.json',json.dumps(mapping,indent=2))
    print(json.dumps({'backed_up':len(originals),'pack_jobs':len(jobs)}))

def setfloat(s,key,value):
    s,n=re.subn(r'(    - '+re.escape(key)+r': )[^\r\n]+',lambda m:m[1]+str(value),s)
    assert n==1,(key,n)
    return s

def settex(s,key,path):
    value='{fileID: 2800000, guid: '+guid(path)+', type: 3}'
    s,n=re.subn(r'(    - '+re.escape(key)+r':\n        m_Texture: )[^\n]+',lambda m:m[1]+value,s)
    assert n==1,(key,n)
    return s

def apply():
    jobs=json.loads((WORK/'jobs.json').read_text())
    validation=json.loads((WORK/'packing-validation.json').read_text())
    assert len(validation)==len(jobs) and all(v['verified'] for v in validation)
    mapping=json.loads((WORK/'mapping.json').read_text())
    stats={'materials':0,'base_maps_restored':0,'packed_maps':len(jobs),'data_importers':0,'normal_importers':0,'decals':0}
    for p in TEX.glob('*.meta'):
        s=p.read_text(); before=s
        if re.search(r'_(roughness|roughnes2|metallic|AO|opacity)\.',p.name):
            s=s.replace('sRGBTexture: 1','sRGBTexture: 0')
            if s!=before:stats['data_importers']+=1
        if p.name in ['Escalera2_normal.png.meta','Escalera3_normal.png.meta']:
            s=re.sub(r'^  textureType: 0$', '  textureType: 1',s,flags=re.M).replace('sRGBTexture: 1','sRGBTexture: 0')
            stats['normal_importers']+=1
        if p.name=='JoinedDecals.png.meta':
            s=s.replace('alphaIsTransparency: 0','alphaIsTransparency: 1')
            s=s.replace('maxTextureSize: 2048','maxTextureSize: 4096')
            # BC7 retains colored decal edges and alpha on the current desktop target.
            s=re.sub(r'(buildTarget: Standalone\n    maxTextureSize: 4096\n    resizeAlgorithm: 0\n    textureFormat: )-1',r'\g<1>25',s)
            s=re.sub(r'(buildTarget: Standalone\n(?:    [^\n]+\n)*?    overridden: )0',r'\g<1>1',s)
        if s!=before:write(p,s)
    for p in sorted(MATS.glob('*.mat')):
        base=mapping[p.stem]
        if base=='Black':continue
        s=p.read_text();before=s
        keys=set(re.findall(r'^  - (_[A-Z0-9_]+)$',s,re.M))
        if base=='JoinedDecals':
            for k,v in {'_Surface':1,'_Blend':0,'_SrcBlend':5,'_DstBlend':10,'_SrcBlendAlpha':1,'_DstBlendAlpha':10,'_ZWrite':0,'_AlphaClip':0,'_BlendModePreserveSpecular':0,'_Smoothness':0.15}.items(): s=setfloat(s,k,v)
            keys.add('_SURFACE_TYPE_TRANSPARENT')
            s=s.replace('RenderType: Opaque','RenderType: Transparent')
            s=s.replace('m_CustomRenderQueue: -1','m_CustomRenderQueue: 3000')
            s=s.replace('  - MOTIONVECTORS','  - MOTIONVECTORS\n  - ShadowCaster\n  - DepthOnly')
            stats['decals']+=1
        else:
            if re.search(r'_BaseMap:\n        m_Texture: \{fileID: 0\}',s): stats['base_maps_restored']+=1
            s=settex(s,'_BaseMap',TEX/(base+'_albedo.jpeg'))
            s=settex(s,'_MainTex',TEX/(base+'_albedo.jpeg'))
            s=settex(s,'_MetallicGlossMap',PACKED/(base+'_MSAO.png'))
            if (TEX/(base+'_AO.jpeg')).exists():
                s=settex(s,'_OcclusionMap',PACKED/(base+'_MSAO.png'));keys.add('_OCCLUSIONMAP')
            keys.add('_METALLICSPECGLOSSMAP')
            s=setfloat(s,'_Smoothness',1)
            s=setfloat(s,'_SmoothnessTextureChannel',0)
            for key in ['_BaseColor','_Color']:
                s=re.sub(r'(    - '+key+r': )\{[^\n]+\}',r'\g<1>{r: 1, g: 1, b: 1, a: 1}',s)
        keytext='\n'+'\n'.join('  - '+k for k in sorted(keys)) if keys else ' []'
        s=re.sub(r'  m_ValidKeywords:.*?(?=\n  m_InvalidKeywords:)',lambda m:'  m_ValidKeywords:'+keytext,s,flags=re.S)
        if s!=before:write(p,s);stats['materials']+=1
    write(WORK/'result.json',json.dumps(stats,indent=2))
    print(json.dumps(stats))

if __name__=='__main__':
    {'prepare':prepare,'apply':apply}[sys.argv[1]]()
