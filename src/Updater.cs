using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HitMeUpdate {
 public class Settings { public string repository {get;set;} public string app_id {get;set;} }
 public class Item { public string path {get;set;} public string sha256 {get;set;} public long size {get;set;} public string package {get;set;} }
 public class Package { public string name {get;set;} public string sha256 {get;set;} public long size {get;set;} }
 public class State { public int schema {get;set;} public string app_id {get;set;} public string version {get;set;} public string base_version {get;set;} public List<Item> files {get;set;} }
 public class Manifest { public int schema {get;set;} public string app_id {get;set;} public string version {get;set;} public string base_version {get;set;} public string release_tag {get;set;} public string notes {get;set;} public List<Item> files {get;set;} public List<Item> inventory {get;set;} public List<string> deleted {get;set;} public List<Package> packages {get;set;} }
 public class Change { public string path {get;set;} public bool existed {get;set;} public string prior_sha256 {get;set;} }
 public class Journal { public bool committed {get;set;} public List<Change> changes {get;set;} }
 public class BaseInfo { public int schema {get;set;} public string app_id {get;set;} public List<Item> files {get;set;} }
 public class Stamp { public long size {get;set;} public long ticks {get;set;} }
 public class BaseCache { public string requirements {get;set;} public Dictionary<string,Stamp> files {get;set;} }

 public static class Engine {
  public const string AppId="hitme-ko";
  static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=16*1024*1024, RecursionLimit=64 }; }
  public static T Parse<T>(string s) { return Serializer().Deserialize<T>(s); }
  public static string Json(object v) { return Serializer().Serialize(v); }
  public static string Hash(string file) { using(var s=File.OpenRead(file)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant(); }
  public static string TextHash(string s) { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-",""); }
  static bool Digest(string h) { return h!=null && Regex.IsMatch(h,"^[a-f0-9]{64}$"); }
  public static string Safe(string root,string rel,bool internalState=false,bool baseCheck=false) {
   if(String.IsNullOrEmpty(rel)||rel.Length>220||rel.Contains("\\")||rel.Contains(":")||rel.StartsWith("/")||rel.EndsWith("/")) throw new Exception("잘못된 업데이트 경로입니다.");
   var parts=rel.Split('/');
   foreach(var p in parts) if(p=="."||p==".."||p.TrimEnd(' ','.')!=p||p.Length==0||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(p,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\.|$)",RegexOptions.IgnoreCase)) throw new Exception("허용되지 않는 업데이트 경로입니다.");
   bool allowed=false;
   if(internalState && new[]{"updates/installed.json","updates/settings.json","updates/base-check.json"}.Contains(rel,StringComparer.OrdinalIgnoreCase)) allowed=true;
   if(new[]{"HitMe.exe","Update.pac","Config.pac","Movie/Opening.dat","fonts/NotoSansKR-Regular.otf","fonts/OFL.txt","사용설명서.md","updates/HitMeUpdater.dll","updates/base-requirements.json"}.Contains(rel,StringComparer.OrdinalIgnoreCase)) allowed=true;
   if(baseCheck && new[]{"Bgm.pac","Face.pac","Script.pac","Se.pac","Stand.pac","System.pac","Thumbnail.pac","Visual.pac","Voice.pac","AuthTool.exe"}.Contains(rel,StringComparer.OrdinalIgnoreCase)) allowed=true;
   if(!allowed) throw new Exception("업데이트가 보호 영역을 포함합니다: "+rel);
   string full=Path.GetFullPath(Path.Combine(root,rel.Replace('/',Path.DirectorySeparatorChar)));
   string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new Exception("설치 폴더 밖의 경로입니다.");
   string check=full;
   while(check!=null && check.Length>=prefix.Length-1) {
    if((File.Exists(check)||Directory.Exists(check)) && (File.GetAttributes(check)&FileAttributes.ReparsePoint)!=0) throw new Exception("연결된 폴더/파일에는 업데이트할 수 없습니다: "+rel);
    check=Path.GetDirectoryName(check);
   }
   return full;
  }
  public static Version Ver(string s) { Version v; if(s==null||!Regex.IsMatch(s,"^[0-9]+\\.[0-9]+\\.[0-9]+$")||!Version.TryParse(s,out v)) throw new Exception("버전 형식이 잘못되었습니다."); return v; }
  public static void ValidateLocal(State local,string root) {
   if(local==null||local.schema!=1||local.app_id!=AppId||local.files==null)throw new Exception("설치 버전 정보가 없거나 잘못되었습니다.");
   Ver(local.version);Ver(local.base_version);
   if(Ver(local.version)<Ver(local.base_version))throw new Exception("설치 기준 버전이 잘못되었습니다.");
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in local.files) {
    if(f==null)throw new Exception("설치 파일 정보가 잘못되었습니다.");
    Safe(root,f.path);
    if(!seen.Add(f.path)||!Digest(f.sha256)||f.size<0||f.size>=2L*1024*1024*1024)throw new Exception("설치 파일 목록에 오류가 있습니다.");
   }
  }
  public static void CheckInstalled(string root,State local) {
   ValidateLocal(local,root);
   foreach(string required in new[]{"HitMe.exe","Update.pac","Config.pac","Movie/Opening.dat","fonts/NotoSansKR-Regular.otf","fonts/OFL.txt","updates/HitMeUpdater.dll","updates/base-requirements.json"})
    if(!local.files.Any(f=>f.path==required))throw new Exception("필수 패치 파일이 설치 정보에 없습니다: "+required);
   foreach(var f in local.files) {
    string p=Safe(root,f.path);
    if(!File.Exists(p)||new FileInfo(p).Length!=f.size||Hash(p)!=f.sha256)throw new Exception("패치 파일이 없거나 변경되었습니다. 현재 파일을 보존하고 실행을 중단합니다: "+f.path);
   }
  }
  public static void Validate(Manifest m,State local,string root) {
   ValidateLocal(local,root);
   if(m==null||m.schema!=1||m.app_id!=AppId||m.files==null||m.inventory==null||m.packages==null||m.deleted==null) throw new Exception("지원하지 않는 업데이트 정보입니다.");
   Ver(m.version);Ver(m.base_version);Ver(local.version);
   if(m.base_version!=local.base_version || Ver(local.version)<Ver(m.base_version)) throw new Exception("이 설치본과 업데이트 기준 버전이 다릅니다. 최신 전체 배포본을 사용해 주세요.");
   if(m.release_tag!="v"+m.version) throw new Exception("릴리스 태그가 버전과 다릅니다.");
   var inv=new Dictionary<string,Item>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in m.inventory) { Safe(root,f.path); if(!Digest(f.sha256)||f.size<0||f.size>2L*1024*1024*1024||inv.ContainsKey(f.path)) throw new Exception("파일 목록에 중복/형식 오류가 있습니다."); inv.Add(f.path,f); }
   var pk=new Dictionary<string,Package>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in m.packages) { if(p.name==null||!Regex.IsMatch(p.name,"^hitme-update-[A-Za-z0-9.-]+\\.zip$")||!Digest(p.sha256)||p.size<=0||p.size>=2L*1024*1024*1024||pk.ContainsKey(p.name)) throw new Exception("업데이트 묶음 정보가 잘못되었습니다."); pk.Add(p.name,p); }
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var f in m.files) { Safe(root,f.path); Item i; if(!seen.Add(f.path)||!inv.TryGetValue(f.path,out i)||i.sha256!=f.sha256||i.size!=f.size||!pk.ContainsKey(f.package)) throw new Exception("변경 파일 대응 오류입니다."); }
   foreach(var n in m.deleted) { Safe(root,n); if(inv.ContainsKey(n)||!seen.Add(n)) throw new Exception("삭제 목록이 변경 목록과 충돌합니다."); }
   var prior=local.files.ToDictionary(f=>f.path,StringComparer.OrdinalIgnoreCase);
   foreach(var pair in inv) {
    Item was;
    if((!prior.TryGetValue(pair.Key,out was)||was.sha256!=pair.Value.sha256||was.size!=pair.Value.size)&&!seen.Contains(pair.Key))throw new Exception("필요한 파일이 변경 묶음에 누락됐습니다: "+pair.Key);
   }
   foreach(var n in prior.Keys)if(!inv.ContainsKey(n)&&!seen.Contains(n))throw new Exception("삭제 파일이 목록에 누락됐습니다: "+n);
  }
  public static bool Running(string root) {
   foreach(var p in Process.GetProcessesByName("HitMe")) { try { if(Path.GetFullPath(p.MainModule.FileName).Equals(Path.Combine(Path.GetFullPath(root),"HitMe.exe"),StringComparison.OrdinalIgnoreCase)) return true; } catch { return true; } finally { p.Dispose(); } }
   return false;
  }
  public static void Atomic(string target,string source) {
   Directory.CreateDirectory(Path.GetDirectoryName(target));string tmp=target+".hitme-update-"+Guid.NewGuid().ToString("N");
   try {
    File.Copy(source,tmp,false);
    if(File.Exists(target)) File.Replace(tmp,target,null); else File.Move(tmp,target);
   } finally { if(File.Exists(tmp))File.Delete(tmp); }
  }
  static void SaveJson(string path,object value) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".writing";
   File.WriteAllText(tmp,Json(value),new UTF8Encoding(false));
   if(File.Exists(path)) File.Replace(tmp,path,null);else File.Move(tmp,path);
  }
  public static void CheckBase(string root) {
   string requirement=Safe(root,"updates/base-requirements.json");
   if(!File.Exists(requirement))throw new Exception("본편 확인 정보가 없습니다. 한글패치를 다시 풀어 주세요.");
   var info=Parse<BaseInfo>(File.ReadAllText(requirement,Encoding.UTF8));
   if(info.schema!=1||info.app_id!=AppId||info.files==null||info.files.Count==0)throw new Exception("본편 확인 정보 형식 오류입니다.");
   string signature=Hash(requirement),cachePath=Safe(root,"updates/base-check.json",true);BaseCache cache=null;
   try{cache=Parse<BaseCache>(File.ReadAllText(cachePath,Encoding.UTF8));}catch{}
   var fresh=new BaseCache{requirements=signature,files=new Dictionary<string,Stamp>(StringComparer.OrdinalIgnoreCase)};
   foreach(var f in info.files) {
    if(f==null||!Digest(f.sha256)||f.size<0)throw new Exception("본편 파일 기준 정보 오류입니다.");
    string path=Safe(root,f.path,false,true);
    if(!File.Exists(path))throw new Exception("이 파일은 본편이 포함되지 않은 한글패치입니다.\n이용자가 준비한 너의 눈동자에 히트미 본편 설치 폴더에 패치를 덮어 풀어 주세요.\n없는 파일: "+f.path);
    var stat=new FileInfo(path);Stamp old=null;
    bool cached=cache!=null&&cache.requirements==signature&&cache.files!=null&&cache.files.TryGetValue(f.path,out old)&&old.size==stat.Length&&old.ticks==stat.LastWriteTimeUtc.Ticks;
    if(stat.Length!=f.size||(!cached&&Hash(path)!=f.sha256))throw new Exception("본편 파일이 이 패치의 기준과 다르거나 손상됐습니다.\n다른 패치가 없는 본편을 확인해 주세요.\n확인할 파일: "+f.path);
    fresh.files[f.path]=new Stamp{size=stat.Length,ticks=stat.LastWriteTimeUtc.Ticks};
   }
   SaveJson(cachePath,fresh);
  }
  static void RejectLinks(string directory) {
   if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new Exception("업데이트 임시 폴더에 연결 경로가 있습니다.");
   foreach(string p in Directory.EnumerateFileSystemEntries(directory)) {
    var attr=File.GetAttributes(p);
    if((attr&FileAttributes.ReparsePoint)!=0)throw new Exception("업데이트 임시 폴더에 연결 경로가 있습니다.");
    if((attr&FileAttributes.Directory)!=0)RejectLinks(p);
   }
  }
  static string Work(string root) {
   string p=Path.Combine(root,".hitme-update");
   if(Directory.Exists(p))RejectLinks(p);
   return p;
  }
  public static void Recover(string root) {
   string work=Work(root),jp=Path.Combine(work,"transaction.json");
   if(!File.Exists(jp)) return;
   if(Running(root)) throw new Exception("미완료 업데이트 복구를 위해 게임을 먼저 종료해 주세요.");
   var j=Parse<Journal>(File.ReadAllText(jp));
   if(j==null||j.changes==null||j.changes.Select(c=>c.path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=j.changes.Count)throw new Exception("복구 기록 오류입니다. 임시 폴더를 보존해 주세요.");
   // Validate all backups before restoring any target.
   foreach(var c in j.changes) {
    Safe(root,c.path,c.path=="updates/installed.json");
    if(!j.committed&&c.existed) {
     string prior=Path.Combine(work,"prior",c.path);
     if(!Digest(c.prior_sha256)||!File.Exists(prior)||Hash(prior)!=c.prior_sha256)throw new Exception("복구 파일 검증에 실패했습니다. 임시 폴더를 보존해 주세요.");
    }
   }
   if(!j.committed) foreach(var c in j.changes) {
    string dest=Safe(root,c.path,c.path=="updates/installed.json");
    if(c.existed) { string prior=Path.Combine(work,"prior",c.path); if(!File.Exists(prior)) throw new Exception("복구 파일이 없습니다. .hitme-update 폴더를 보존해 주세요.");Atomic(dest,prior); }
    else if(File.Exists(dest)) File.Delete(dest);
   }
   Directory.Delete(work,true);
  }
  public static void Download(string url,string dest,long maxBytes,Action<string> progress) {
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   var req=(HttpWebRequest)WebRequest.Create(url);req.UserAgent="HitMe-Korean-Patch-Updater/1";req.Timeout=15000;req.ReadWriteTimeout=45000;
   using(var response=(HttpWebResponse)req.GetResponse()) {
    string host=response.ResponseUri.Host;
    if(response.ResponseUri.Scheme!="https" || !(host=="github.com"||host.EndsWith(".githubusercontent.com",StringComparison.OrdinalIgnoreCase))) throw new Exception("안전하지 않은 다운로드 주소입니다.");
    if(response.ContentLength>maxBytes) throw new Exception("다운로드 크기가 안내와 다릅니다.");
    using(var input=response.GetResponseStream()) using(var output=new FileStream(dest,FileMode.Create,FileAccess.Write,FileShare.None)) {
     byte[] buf=new byte[131072];long total=0;int n;var tick=Stopwatch.StartNew();
     while((n=input.Read(buf,0,buf.Length))>0) {total+=n;if(total>maxBytes)throw new Exception("다운로드 크기 초과");output.Write(buf,0,n);if(tick.ElapsedMilliseconds>250){progress("다운로드 중: "+(total/1048576)+" MB");tick.Restart();}}
     output.Flush(true);
    }
   }
  }
  public static Manifest Fetch(Settings s) {
   if(s==null||s.app_id!=AppId||s.repository==null||!Regex.IsMatch(s.repository,"^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+$")) throw new Exception("업데이트 저장소 설정이 없습니다.");
   string tmp=Path.GetTempFileName();
   try {Download("https://github.com/"+s.repository+"/releases/latest/download/update-manifest.json",tmp,16*1024*1024,delegate{});return Parse<Manifest>(File.ReadAllText(tmp,Encoding.UTF8));}finally{File.Delete(tmp);}
  }
  public static void Apply(string root,Manifest m,State local,Action<Package,string> fetch,Action<string> progress,int failAfter=-1) {
   Validate(m,local,root);
   if(Ver(m.version)<=Ver(local.version)) return;
   if(Running(root)) throw new Exception("게임을 종료한 뒤 업데이트를 실행해 주세요.");
   string work=Work(root);Recover(root);if(Directory.Exists(work))Directory.Delete(work,true);Directory.CreateDirectory(work);
   var old=local.files.ToDictionary(f=>f.path,f=>f,StringComparer.OrdinalIgnoreCase);
   var originalBase=new Dictionary<string,Item>(StringComparer.OrdinalIgnoreCase);
   string basePath=Path.Combine(root,"updates","base-requirements.json");
   if(File.Exists(basePath))originalBase=Parse<BaseInfo>(File.ReadAllText(basePath,Encoding.UTF8)).files.ToDictionary(f=>f.path,f=>f,StringComparer.OrdinalIgnoreCase);
   var changed=new List<Item>();var removals=new List<string>();
   foreach(var f in m.files) {
    string dest=Safe(root,f.path);bool exists=File.Exists(dest);string current=exists?Hash(dest):null;
    if(current==f.sha256)continue;
    Item previous;
    if(exists) {
     bool known=old.TryGetValue(f.path,out previous);
     if(!known)known=originalBase.TryGetValue(f.path,out previous);
     if(!known||previous.sha256!=current)throw new Exception("개별 수정 파일이 있어 덮어쓰지 않았습니다: "+f.path);
    }
    changed.Add(f);
   }
   foreach(var n in m.deleted) {string dest=Safe(root,n);Item previous;if(File.Exists(dest)){if(!old.TryGetValue(n,out previous))continue;if(Hash(dest)!=previous.sha256)throw new Exception("개별 수정 파일을 삭제하지 않았습니다: "+n);removals.Add(n);}}
   var packagesNeeded=m.packages.Where(p=>changed.Any(f=>f.package==p.name)).ToList();
   var priorSizes=changed.Select(f=>f.path).Concat(removals).Where(n=>File.Exists(Safe(root,n))).Select(n=>new FileInfo(Safe(root,n)).Length).ToList();
   long backups=priorSizes.Sum();
   long expanded=m.files.Where(f=>packagesNeeded.Any(p=>p.name==f.package)).Sum(f=>f.size);
   long tempMax=Math.Max(changed.Count==0?0:changed.Max(f=>f.size),priorSizes.Count==0?0:priorSizes.Max());
   long needed=checked(backups+expanded+tempMax+packagesNeeded.Sum(p=>p.size)+32L*1024*1024);
   if(new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))).AvailableFreeSpace<needed)throw new Exception("업데이트에 필요한 임시 공간이 부족합니다.");
   var staged=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in m.packages.Where(p=>changed.Any(f=>f.package==p.name))) {
    progress("묶음 받는 중: "+p.name);string zip=Path.Combine(work,p.name);fetch(p,zip);
    if(new FileInfo(zip).Length!=p.size||Hash(zip)!=p.sha256)throw new Exception("다운로드 검사에 실패했습니다: "+p.name);
    var expected=m.files.Where(f=>f.package==p.name).ToDictionary(f=>f.path,StringComparer.OrdinalIgnoreCase);
    var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using(var archive=ZipFile.OpenRead(zip)) foreach(var e in archive.Entries) {
     Safe(root,e.FullName);Item f;
     if(!seen.Add(e.FullName)||!expected.TryGetValue(e.FullName,out f)||e.Length!=f.size||((e.ExternalAttributes>>16)&0xf000)==0xa000)throw new Exception("압축 파일 목록이 안내와 다릅니다.");
     string dest=Path.Combine(work,"new",f.path);Directory.CreateDirectory(Path.GetDirectoryName(dest));
     using(var input=e.Open())using(var output=new FileStream(dest,FileMode.CreateNew)) {
      byte[] buffer=new byte[131072];long total=0;int count;
      while((count=input.Read(buffer,0,buffer.Length))>0){total+=count;if(total>f.size)throw new Exception("압축 해제 크기 초과");output.Write(buffer,0,count);}
      if(total!=f.size)throw new Exception("압축 해제 크기 불일치");
     }
     if(Hash(dest)!=f.sha256)throw new Exception("파일 검사 실패: "+f.path);
     if(changed.Any(c=>c.path==f.path))staged.Add(f.path,dest);
    }
    if(seen.Count!=expected.Count)throw new Exception("압축 파일에 누락된 항목이 있습니다.");File.Delete(zip);
   }
   if(staged.Count!=changed.Count)throw new Exception("필요한 변경 파일이 모두 도착하지 않았습니다.");
   // Detect local edits made while a large download was in progress.
   foreach(var f in changed) {string dest=Safe(root,f.path);Item was;if(File.Exists(dest)&&(!old.TryGetValue(f.path,out was)&&!originalBase.TryGetValue(f.path,out was)||Hash(dest)!=was.sha256))throw new Exception("다운로드 중 파일이 변경되어 중단했습니다: "+f.path);}
   foreach(var n in removals)if(File.Exists(Safe(root,n))&&Hash(Safe(root,n))!=old[n].sha256)throw new Exception("다운로드 중 삭제 대상이 변경되어 중단했습니다: "+n);
   var next=new State{schema=1,app_id=AppId,version=m.version,base_version=m.base_version,files=m.inventory};
   string newState=Path.Combine(work,"state.json");File.WriteAllText(newState,Json(next),new UTF8Encoding(false));staged.Add("updates/installed.json",newState);
   var journal=new Journal{committed=false,changes=new List<Change>()};
   foreach(string n in staged.Keys.Concat(removals)) {
    string dest=Safe(root,n,n=="updates/installed.json");bool exists=File.Exists(dest);
    if(exists){string backup=Path.Combine(work,"prior",n);Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(dest,backup);}
    journal.changes.Add(new Change{path=n,existed=exists,prior_sha256=exists?Hash(dest):null});
   }
   string jp=Path.Combine(work,"transaction.json");SaveJson(jp,journal);
   try {
    if(Running(root))throw new Exception("게임이 실행돼 교체를 중단했습니다.");int done=0;
    foreach(var pair in staged.Where(p=>p.Key!="updates/installed.json")) {progress("적용 중: "+pair.Key);Atomic(Safe(root,pair.Key),pair.Value);if(++done==failAfter)throw new Exception("모의 교체 중단");}
    foreach(string n in removals)File.Delete(Safe(root,n));
    Atomic(Safe(root,"updates/installed.json",true),newState);
    journal.committed=true;SaveJson(jp,journal);Directory.Delete(work,true);
   } catch {Recover(root);throw;}
  }
 }
 public static class App {
  public static Manifest CheckRemote(Func<Manifest> check,bool manual,Action launch,Action<Exception> notify) {
   try{return check();}
   catch(Exception error){notify(error);if(!manual)launch();return null;}
  }
  static void Note(string root,string message) {try{Directory.CreateDirectory(Path.Combine(root,"updates"));File.WriteAllText(Path.Combine(root,"updates","last-check.txt"),DateTime.Now+" "+message,Encoding.UTF8);}catch{}}
  static void Launch(string root) {
   if(Engine.Running(root))return;
   using(var font=SessionFont.Load(Path.Combine(root,"fonts","NotoSansKR-Regular.otf"))) {
    using(var game=Process.Start(LaunchStartInfo(root))) {
     if(game==null)throw new Exception("게임 프로세스를 시작하지 못했습니다.");
     game.WaitForExit();
    }
   }
  }
  public static ProcessStartInfo LaunchStartInfo(string root) {
   return new ProcessStartInfo(Path.Combine(Path.GetFullPath(root),"HitMe.exe")){WorkingDirectory=Path.GetFullPath(root),UseShellExecute=false,Arguments=""};
  }
  public static void Run(string root,bool manual) {
   RunCore(root,manual);
  }
  static void RunCore(string root,bool manual) {
   Application.EnableVisualStyles();bool fresh;
   using(var mutex=new Mutex(true,"Local\\HitMeUpdate-"+Engine.TextHash(Path.GetFullPath(root).ToLowerInvariant()).Substring(0,24),out fresh)) {
    if(!fresh){MessageBox.Show("다른 실행기나 업데이트가 진행 중입니다.","너의 눈동자에 히트미");return;}
    try {
     Engine.Recover(root);
     var local=Engine.Parse<State>(File.ReadAllText(Engine.Safe(root,"updates/installed.json",true),Encoding.UTF8));
     Engine.CheckInstalled(root,local);
     Engine.CheckBase(root);
     bool network=true;try{network=System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();}catch{}
     if(!manual&&!network){Note(root,"네트워크 없음: 현재 버전 실행");Launch(root);return;}
     var settings=Engine.Parse<Settings>(File.ReadAllText(Engine.Safe(root,"updates/settings.json",true),Encoding.UTF8));
     Manifest m=null;
     m=CheckRemote(()=>{var found=Engine.Fetch(settings);Engine.Validate(found,local,root);Note(root,"업데이트 확인 완료: "+found.version);return found;},manual,()=>Launch(root),e=>{Note(root,"확인 실패: "+e.Message);if(manual)MessageBox.Show("업데이트를 확인하지 못했습니다.\n"+e.Message,"한글패치 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Information);});
     if(m==null)return;
     if(Engine.Ver(m.version)<=Engine.Ver(local.version)){if(manual)MessageBox.Show("현재 최신 버전입니다. ("+local.version+")","한글패치 업데이트");else Launch(root);return;}
     string notes=m.notes??"";if(notes.Length>1200)notes=notes.Substring(0,1200)+"…";
     var answer=MessageBox.Show("한글패치 새 버전이 있습니다.\n"+local.version+" → "+m.version+"\n\n"+notes+"\n\n지금 업데이트하시겠습니까?\n세이브는 유지됩니다.","한글패치 업데이트",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
     if(answer!=DialogResult.Yes){if(!manual)Launch(root);return;}
     if(Engine.Running(root)){MessageBox.Show("게임을 완전히 종료한 뒤 수동 업데이트를 실행해 주세요.","한글패치 업데이트");return;}
     Exception failure=null;
     using(var form=new Form{Text="한글패치 업데이트",Width=560,Height=150,StartPosition=FormStartPosition.CenterScreen,ControlBox=false,FormBorderStyle=FormBorderStyle.FixedDialog}) {
      var label=new Label{Left=20,Top=20,Width=510,Height=45,Text="업데이트 준비 중…"};form.Controls.Add(label);
      form.Controls.Add(new ProgressBar{Left=20,Top=75,Width=505,Style=ProgressBarStyle.Marquee});
      Action<string> progress=s=>{if(form.IsHandleCreated)form.BeginInvoke(new Action(()=>label.Text=s));};
      form.Shown+=async delegate {await Task.Run(()=>{try{Engine.Apply(root,m,local,(p,d)=>Engine.Download("https://github.com/"+settings.repository+"/releases/download/"+m.release_tag+"/"+p.name,d,p.size,progress),progress);}catch(Exception e){failure=e;}});form.Close();};
      form.ShowDialog();
     }
     if(failure!=null){Note(root,"업데이트 실패: "+failure.Message);MessageBox.Show("업데이트를 완료하지 못했습니다.\n"+failure.Message,"한글패치 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
     Note(root,"업데이트 적용 완료: "+m.version);if(manual)MessageBox.Show("업데이트가 완료되었습니다. ("+m.version+")","한글패치 업데이트");else Launch(root);
    } catch(Exception e){MessageBox.Show("실행을 진행할 수 없습니다.\n"+e.Message,"너의 눈동자에 히트미",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
 }
}
