"""Explicit allow-list HitMe patch/release builder. Never installs or launches game."""
import argparse,hashlib,json,os,re,shutil,struct,subprocess,zipfile
from pathlib import Path

HERE=Path(__file__).resolve().parent
CONFIG_PATH=HERE/'build-config.local.json'
if not CONFIG_PATH.exists():raise SystemExit('Copy build-config.example.json to build-config.local.json and set local paths.')
CONFIG=json.loads(CONFIG_PATH.read_text(encoding='utf8'))
WORK=Path(CONFIG['work']).resolve()
CANDIDATE=Path(CONFIG['candidate']).resolve()
ORIGINAL=Path(CONFIG['original']).resolve()
REPOSITORY='adios1579-lgtm/HitMe-Korean-Patch'
APP='hitme-ko'
BASE_FILES=['Bgm.pac','Face.pac','Script.pac','Se.pac','Stand.pac','System.pac','Thumbnail.pac','Visual.pac','Voice.pac','AuthTool.exe']
CSC=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework/v4.0.30319/csc.exe'
LAUNCHERS=['히트미_한글판.exe','한글패치_업데이트.exe']
UNMANAGED=set(LAUNCHERS+['updates/settings.json'])
FONT_SOURCE=Path(CONFIG['font']).resolve()

def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
 return h.hexdigest()

def dump(p,d):
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')

def row(name,p):return {'path':name,'size':p.stat().st_size,'sha256':sha(p)}

def compile_tools():
 import pefile
 WORK.mkdir(parents=True,exist_ok=True)
 pe=pefile.PE(str(CANDIDATE/'HitMe.exe'));res={e.id:e for e in pe.DIRECTORY_ENTRY_RESOURCE.entries}
 group=res[14].directory.entries[0].directory.entries[0].data.struct
 raw=pe.get_data(group.OffsetToData,group.Size);count=struct.unpack_from('<H',raw,4)[0]
 icons={e.id:e.directory.entries[0].data.struct for e in res[3].directory.entries}
 headers=[];images=[];off=6+16*count
 for i in range(count):
  r=raw[6+14*i:20+14*i];icon=icons[struct.unpack_from('<H',r,12)[0]];data=pe.get_data(icon.OffsetToData,icon.Size)
  headers.append(r[:8]+struct.pack('<II',len(data),off));images.append(data);off+=len(data)
 ico=WORK/'hitme.ico';ico.write_bytes(raw[:6]+b''.join(headers)+b''.join(images))
 common=[str(CSC),'/nologo','/optimize+','/platform:x86']
 refs=['System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem']
 build=HERE/'tools';build.mkdir(exist_ok=True)
 subprocess.run(common+['/target:library','/out:'+str(build/'HitMeUpdater.dll')]+['/r:'+x+'.dll' for x in refs]+[str(HERE/'src/Updater.cs'),str(HERE/'src/SessionFont.cs')],check=True)
 launcher=WORK/'Bootstrap.exe'
 subprocess.run(common+['/target:winexe','/win32icon:'+str(ico),'/out:'+str(launcher),'/r:System.Windows.Forms.dll',str(HERE/'src/Bootstrap.cs')],check=True)
 for name in LAUNCHERS:
  dest=build/name
  if (HERE/'baseline.json').exists() and dest.exists():
   # Compiler timestamps differ; no silent bootstrap migration after first publish.
   continue
  shutil.copyfile(launcher,dest)
 tests=WORK/'UpdaterTests.exe'
 subprocess.run(common+['/target:exe','/out:'+str(tests),'/r:'+str(build/'HitMeUpdater.dll')]+['/r:'+x+'.dll' for x in refs]+[str(HERE/'src/UpdaterTests.cs')],check=True)
 shutil.copyfile(build/'HitMeUpdater.dll',WORK/'HitMeUpdater.dll')
 result=subprocess.run([str(tests)],capture_output=True,text=True,encoding='utf8',errors='replace')
 (HERE/'TEST_RESULTS.txt').write_text(result.stdout+'\n'+result.stderr,encoding='utf8')
 print(result.stdout,flush=True)
 if result.returncode:raise RuntimeError('Updater tests failed; see TEST_RESULTS.txt')

def requirements():
 rows=[row(n,ORIGINAL/n) for n in BASE_FILES]
 p=HERE/'assets/base-requirements.json'
 dump(p,{'schema':1,'app_id':APP,'files':rows,'supported_edition':'Japanese Windows PC installation; exact original archive hashes required.'})
 return rows

def sources():
 # Exact current reviewed candidate; never recursively collect a game folder.
 result={n:CANDIDATE/n for n in ['HitMe.exe','Update.pac','Config.pac']}
 candidate_manifest=json.loads((CANDIDATE/'MANIFEST.json').read_text(encoding='utf8'))
 for n,p in result.items():assert sha(p)==candidate_manifest['files'][n]['sha256'],'Candidate manifest mismatch: '+n
 result['Movie/Opening.dat']=Path(CONFIG['opening']).resolve()
 result['fonts/NotoSansKR-Regular.otf']=FONT_SOURCE
 result['fonts/OFL.txt']=HERE/'assets/OFL.txt'
 result['사용설명서.md']=HERE/'USER_GUIDE.md'
 result['updates/HitMeUpdater.dll']=HERE/'tools/HitMeUpdater.dll'
 result['updates/base-requirements.json']=HERE/'assets/base-requirements.json'
 result['updates/settings.json']=HERE/'assets/settings.json'
 result.update({n:HERE/'tools'/n for n in LAUNCHERS})
 assert all(p.is_file() for p in result.values())
 assert sha(FONT_SOURCE)=='9b44892c93ef4ce72b6844b992994f7c687bcfc51f277e95facb9efd4c534310'
 assert sha(result['Movie/Opening.dat'])=='deb1612d43defc40af3add6c833cc81b6c783d02db6a8f8f0c3030392aa9cfb3'
 return dict(sorted(result.items()))

def zip_files(dest,entries):
 with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
  for name,p in entries.items():z.write(p,name)
 assert dest.stat().st_size<2*1024**3
 # Streaming decompression/hash check; no second full extraction on disk.
 with zipfile.ZipFile(dest) as z:
  assert z.namelist()==list(entries)
  for name,p in entries.items():
   h=hashlib.sha256()
   with z.open(name) as f:
    for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
   assert h.hexdigest()==sha(p),name

def release(mode,version,notes):
 assert re.fullmatch(r'\d+\.\d+\.\d+',version)
 history=HERE/'history';history.mkdir(exist_ok=True)
 assert not (history/(version+'.json')).exists(),'Never silently replace an existing version'
 compile_tools();requirements()
 dump(HERE/'assets/settings.json',{'app_id':APP,'repository':REPOSITORY})
 files=sources();allrows=[row(n,p) for n,p in files.items()];managed=[r for r in allrows if r['path'] not in UNMANAGED]
 previous=[json.loads(p.read_text(encoding='utf8')) for p in history.glob('*.json')]
 if previous:
  key=lambda s:tuple(map(int,s.split('.')))
  assert key(version)>max(key(p['version']) for p in previous)
  baseline=json.loads((HERE/'baseline.json').read_text(encoding='utf8'));base_version=baseline['version']
 else:
  assert mode=='full';base_version=version
 # Any difference relative to any previous release must remain in cumulative delta.
 prior_maps=[{f['path']:f for f in h['files']} for h in previous]
 changed=[r for r in managed if any(h.get(r['path'])!=r for h in prior_maps)] if previous else []
 current={r['path'] for r in managed}
 deleted=sorted(set().union(*(set(h) for h in prior_maps))-current) if prior_maps else []
 out=HERE/'release'/version;assert not out.exists();out.mkdir(parents=True)
 state={'schema':1,'app_id':APP,'version':version,'base_version':base_version,'files':managed}
 dump(WORK/'installed.json',state)
 # Include original-independent patch only; user supplies the game archives.
 full=files.copy();full['updates/installed.json']=WORK/'installed.json'
 zip_files(out/('hitme-korean-patch-'+version+'.zip'),full)
 groups=[];group=[];size=0
 for r in changed:
  assert r['size']<1700*1024**2
  if group and size+r['size']>64*1024**2:groups.append(group);group=[];size=0
  group.append(r);size+=r['size']
 if group:groups.append(group)
 packages=[];delta=[]
 for i,group in enumerate(groups,1):
  name='hitme-update-'+version+'-%03d.zip'%i;p=out/name
  zip_files(p,{r['path']:files[r['path']] for r in group})
  packages.append({'name':name,'size':p.stat().st_size,'sha256':sha(p)})
  delta.extend(dict(r,package=name) for r in group)
 manifest={'schema':1,'app_id':APP,'version':version,'base_version':base_version,'release_tag':'v'+version,'notes':notes,'files':delta,'inventory':managed,'deleted':deleted,'packages':packages}
 dump(out/'update-manifest.json',manifest)
 dump(out/'FILE_HASHES.json',{p.name:{'size':p.stat().st_size,'sha256':sha(p)} for p in out.iterdir() if p.is_file()})
 dump(history/(version+'.json'),state)
 if not previous:dump(HERE/'baseline.json',state)
 dump(HERE/'BUILD_STATUS.json',{'version':version,'repository':REPOSITORY,'input_candidate':str(CANDIDATE),'files':allrows,'original_requirements':str(HERE/'assets/base-requirements.json'),'package_zip_hash_checks':'PASS','game_launched':False,'github_published':False})
 print('Release prepared:',out,flush=True)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('mode',choices=['full','update','test']);p.add_argument('--version',default='1.0.0');p.add_argument('--notes',default='최초 배포: 한글 본문·이미지, 호토리 통합 및 자동/수동 업데이트 실행기.');a=p.parse_args()
 if a.mode=='test':compile_tools()
 else:release(a.mode,a.version,a.notes)
