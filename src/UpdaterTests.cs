using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using HitMeUpdate;
class UpdaterTests {
 static int passed=0;
 static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("통과: "+name);}
 static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
 static string Write(string root,string path,string data){string p=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,data,new UTF8Encoding(false));return p;}
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"히트미 업데이트 시험 "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try {
   string a=Write(root,"Update.pac","이전 번역"),keep=Write(root,"SAVEDATA/SAVE001.DAT","사용자 세이브");
   var old=new State{schema=1,app_id=Engine.AppId,version="1.0.0",base_version="1.0.0",files=new List<Item>{new Item{path="Update.pac",size=new FileInfo(a).Length,sha256=Engine.Hash(a)}}};
   Write(root,"updates/installed.json",Engine.Json(old));
   string zip=Path.Combine(root,"fixture.zip"),stage=Write(root,"fixture/new.txt","수정 번역");
   using(var z=ZipFile.Open(zip,ZipArchiveMode.Create))z.CreateEntryFromFile(stage,"Update.pac");
   var item=new Item{path="Update.pac",size=new FileInfo(stage).Length,sha256=Engine.Hash(stage),package="hitme-update-1.1.0-001.zip"};
   var m=new Manifest{schema=1,app_id=Engine.AppId,version="1.1.0",base_version="1.0.0",release_tag="v1.1.0",notes="시험",files=new List<Item>{item},inventory=new List<Item>{item},deleted=new List<string>(),packages=new List<Package>{new Package{name=item.package,size=new FileInfo(zip).Length,sha256=Engine.Hash(zip)}}};
   Action<Package,string> fetch=(p,d)=>File.Copy(zip,d);Action<string> quiet=s=>{};
   var original=File.ReadAllBytes(a);var saved=Engine.Hash(keep);
   Reject(()=>Engine.Apply(root,m,old,fetch,quiet,1),"교체 도중 오류 감지");
   Check(File.ReadAllBytes(a).SequenceEqual(original)&&Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json"))).version=="1.0.0","실패 후 본문과 버전 복구");
   Engine.Apply(root,m,old,fetch,quiet);Check(Engine.Hash(a)==item.sha256,"한글·공백 경로 업데이트 성공");Check(Engine.Hash(keep)==saved,"사용자 세이브 보존");
   var now=Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json")));int calls=0;
   Engine.Apply(root,m,now,(p,d)=>{calls++;},quiet);Check(calls==0,"같은 버전 중복 다운로드 없음");
   foreach(string path in new[]{"../escape.dll","DAT/../../escape.dll","SAVEDATA/SAVE001.DAT","Update.pac:stream","DAT/CON.MES","/absolute.MES","Update.pac.","device/../NSL.DLL","updates/settings.json"})Reject(()=>Engine.Safe(root,path),"보호 경로 차단 "+path);
   string oldPath=item.path;item.path="SAVEDATA/SAVE001.DAT";Reject(()=>Engine.Validate(m,old,root),"원격 목록의 세이브 변경 차단");item.path=oldPath;
   m.files.Add(item);Reject(()=>Engine.Validate(m,old,root),"중복 ID/파일 경로 차단");m.files.RemoveAt(1);
   m.base_version="2.0.0";Reject(()=>Engine.Validate(m,old,root),"다른 배포 기준 버전 차단");m.base_version="1.0.0";
   File.WriteAllBytes(a,original);Write(root,"updates/installed.json",Engine.Json(old));
   string hash=m.packages[0].sha256;m.packages[0].sha256=new string('0',64);Reject(()=>Engine.Apply(root,m,old,fetch,quiet),"손상 다운로드 해시 차단");Check(File.ReadAllBytes(a).SequenceEqual(original),"손상 응답은 게임 미변경");m.packages[0].sha256=hash;
   File.WriteAllText(a,"사용자 개별 수정");Reject(()=>Engine.Apply(root,m,old,fetch,quiet),"설치 후 개별 수정 보존");File.WriteAllBytes(a,original);
   // 강제 종료를 모사: 저널과 복구본은 있고 대상만 교체된 상태.
   Write(root,".hitme-update/prior/Update.pac","이전 번역");Write(root,".hitme-update/transaction.json",Engine.Json(new Journal{committed=false,changes=new List<Change>{new Change{path="Update.pac",existed=true,prior_sha256=Engine.Hash(Path.Combine(root,".hitme-update/prior/Update.pac"))}}}));
   File.WriteAllText(a,"중간 교체");Engine.Recover(root);Check(File.ReadAllBytes(a).SequenceEqual(original),"재실행 시 중단된 교체 복구");
   // 0개 변경/삭제 없는 초기 릴리스도 정상 정보이다.
   m.files.Clear();m.packages.Clear();Reject(()=>Engine.Validate(m,old,root),"변경 파일 누락을 버전 완료로 처리하지 않음");
   m.inventory=old.files;Engine.Validate(m,old,root);Check(true,"빈 변경 목록 허용");
   m.version="1.2.0";m.release_tag="v1.2.0";
   Reject(()=>Engine.Validate(m,now,root),"예전 상태로 되돌리는 업데이트에도 실제 파일 요구");
   foreach(var status in new[]{WebExceptionStatus.ConnectFailure,WebExceptionStatus.NameResolutionFailure,WebExceptionStatus.Timeout,WebExceptionStatus.ProtocolError}) {
    int launch=0,notice=0;var answer=App.CheckRemote(()=>{throw new WebException("연결 실패 모의",status);},false,()=>launch++,e=>notice++);
    Check(answer==null&&launch==1&&notice==1,"연결 실패 시 현재 게임 실행 경로: "+status);
   }
   int manualLaunch=0;App.CheckRemote(()=>{throw new WebException("오프라인");},true,()=>manualLaunch++,e=>{});Check(manualLaunch==0,"수동 업데이트 실패는 게임을 임의 실행하지 않음");
   var baseFile=new Item{path="Script.pac",size=5,sha256=Engine.TextHash("dummy").ToLowerInvariant()};
   Write(root,"updates/base-requirements.json",Engine.Json(new BaseInfo{schema=1,app_id=Engine.AppId,files=new List<Item>{baseFile}}));
   Reject(()=>Engine.CheckBase(root),"본편 파일 없는 빈 폴더 감지");
   Write(root,"Script.pac","dummy");Engine.CheckBase(root);Check(true,"본편 로컬 파일 확인 — 외부 통신 없음");
   Engine.CheckBase(root);Check(File.Exists(Path.Combine(root,"updates/base-check.json")),"본편 확인 캐시 재사용");
   Reject(()=>Engine.Safe(root,"Script.pac"),"자동 업데이트로 본편 원본 배포 금지");
   Write(root,"Script.pac","different");Reject(()=>Engine.CheckBase(root),"본편 파일 변경 감지");
   // 기존 원본 엔진에 나중에 패치가 추가되는 경우도 사용자 수정과 구별한다.
   Write(root,"Script.pac","dummy");string core=Write(root,"HitMe.exe","original-core");
   var coreOld=new Item{path="HitMe.exe",size=new FileInfo(core).Length,sha256=Engine.Hash(core)};
   string req=Write(root,"updates/base-requirements.json",Engine.Json(new BaseInfo{schema=1,app_id=Engine.AppId,files=new List<Item>{baseFile,coreOld}}));
   var coreState=new State{schema=1,app_id=Engine.AppId,version="1.0.0",base_version="1.0.0",files=new List<Item>{new Item{path="updates/base-requirements.json",size=new FileInfo(req).Length,sha256=Engine.Hash(req)}}};
   Write(root,"updates/installed.json",Engine.Json(coreState));
   string coreNew=Write(root,"fixture/core.dll","patched-core"),reqNew=Write(root,"fixture/req.json",Engine.Json(new BaseInfo{schema=1,app_id=Engine.AppId,files=new List<Item>{baseFile}}));
   string coreZip=Path.Combine(root,"core.zip");
   using(var z=ZipFile.Open(coreZip,ZipArchiveMode.Create)){z.CreateEntryFromFile(coreNew,"HitMe.exe");z.CreateEntryFromFile(reqNew,"updates/base-requirements.json");}
   var coreItems=new List<Item>{new Item{path="HitMe.exe",size=new FileInfo(coreNew).Length,sha256=Engine.Hash(coreNew),package="hitme-update-core.zip"},new Item{path="updates/base-requirements.json",size=new FileInfo(reqNew).Length,sha256=Engine.Hash(reqNew),package="hitme-update-core.zip"}};
   var coreManifest=new Manifest{schema=1,app_id=Engine.AppId,version="1.0.1",base_version="1.0.0",release_tag="v1.0.1",files=coreItems,inventory=coreItems,deleted=new List<string>(),packages=new List<Package>{new Package{name="hitme-update-core.zip",size=new FileInfo(coreZip).Length,sha256=Engine.Hash(coreZip)}}};
   Engine.Apply(root,coreManifest,coreState,(p,d)=>File.Copy(coreZip,d),quiet);Engine.CheckBase(root);
   Check(Engine.Hash(core)==Engine.Hash(coreNew),"원본 엔진에 후속 패치 추가 및 본편 조건 갱신");
   Check(App.LaunchStartInfo(root).FileName==Path.Combine(root,"HitMe.exe")&&App.LaunchStartInfo(root).WorkingDirectory==root&&App.LaunchStartInfo(root).Arguments=="","게임 폴더 작업경로와 직접 실행 인수 — 로케일 에뮬레이터 없음");
   Reject(()=>Engine.ValidateLocal(null,root),"로컬 설치 상태 누락 차단");
   Reject(()=>Engine.CheckInstalled(root,old),"필수 패치 누락 시 오프라인 실행도 차단");
   foreach(string path in new[]{"Update.pac ","fonts/../Update.pac","C:/Update.pac","updates/installed.json","Save/Save001.dat","Config.dat","AuthTool.exe","히트미_한글판.exe"})Reject(()=>Engine.Safe(root,path),"보호/비관리 경로 차단 "+path);
   string testRoot=Path.Combine(root,"추가 시험");Directory.CreateDirectory(testRoot);
   string aa=Write(testRoot,"Update.pac","old"),bb=Write(root,"fixture/two","new");
   var state=new State{schema=1,app_id=Engine.AppId,version="1.0.0",base_version="1.0.0",files=new List<Item>{new Item{path="Update.pac",size=3,sha256=Engine.Hash(aa)}}};
   Write(testRoot,"updates/installed.json",Engine.Json(state));
   string pp="hitme-update-1.2.0-001.zip",zz=Path.Combine(root,"extra.zip");
   using(var z=ZipFile.Open(zz,ZipArchiveMode.Create)){z.CreateEntryFromFile(bb,"Update.pac");z.CreateEntryFromFile(bb,"Save/user.dat");}
   var it=new Item{path="Update.pac",size=3,sha256=Engine.Hash(bb),package=pp};
   var mm=new Manifest{schema=1,app_id=Engine.AppId,version="1.2.0",base_version="1.0.0",release_tag="v1.2.0",files=new List<Item>{it},inventory=new List<Item>{it},deleted=new List<string>(),packages=new List<Package>{new Package{name=pp,size=new FileInfo(zz).Length,sha256=Engine.Hash(zz)}}};
   Reject(()=>Engine.Apply(testRoot,mm,state,(p,d)=>File.Copy(zz,d),quiet),"ZIP 목록 외 세이브 항목 거부");
   File.Delete(zz);using(var z=ZipFile.Open(zz,ZipArchiveMode.Create))z.CreateEntryFromFile(bb,"Update.pac");
   mm.packages[0].size=new FileInfo(zz).Length;mm.packages[0].sha256=Engine.Hash(zz);
   Reject(()=>Engine.Apply(testRoot,mm,state,(p,d)=>{File.Copy(zz,d);File.WriteAllText(aa,"personal");},quiet),"다운로드 중 사용자 수정 감지");
   File.WriteAllText(aa,"old");
   Engine.Apply(testRoot,mm,state,(p,d)=>File.Copy(zz,d),quiet);
   Check(File.ReadAllText(aa)=="new","중간 버전 건너뛰기 1.0.0→1.2.0");
   var state2=Engine.Parse<State>(File.ReadAllText(Path.Combine(testRoot,"updates/installed.json")));
   File.WriteAllText(bb,"old");File.Delete(zz);using(var z=ZipFile.Open(zz,ZipArchiveMode.Create))z.CreateEntryFromFile(bb,"Update.pac");
   mm.version="1.3.0";mm.release_tag="v1.3.0";it.sha256=Engine.Hash(bb);mm.packages[0].size=new FileInfo(zz).Length;mm.packages[0].sha256=Engine.Hash(zz);
   Engine.Apply(testRoot,mm,state2,(p,d)=>File.Copy(zz,d),quiet);Check(File.ReadAllText(aa)=="old","초기 내용으로 되돌리는 누적 업데이트");
   string prior=Write(testRoot,".hitme-update/prior/Update.pac","damaged");
   Write(testRoot,".hitme-update/transaction.json",Engine.Json(new Journal{committed=false,changes=new List<Change>{new Change{path="Update.pac",existed=true,prior_sha256=new string('0',64)}}}));
   Reject(()=>Engine.Recover(testRoot),"손상된 복구 파일 거부");Check(File.ReadAllText(aa)=="old","복구 검증 실패 시 기존 대상 보존");
   Console.WriteLine("무과금·오프라인 테스트 "+passed+"개 통과. 실제 GitHub 연결/게임 실행 시험 아님.");
  } finally { if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true); }
 }
}
