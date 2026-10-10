using System;
using System.IO;
using System.Linq;
using System.Text;
using HitMeUpdate;
class PublicUpgradeProbe {
 static void Main(string[] args) {
  string root=Path.GetFullPath(args[0]);
  var state=Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json"),Encoding.UTF8));
  Engine.CheckInstalled(root,state);Engine.CheckBase(root);
  string from=args.Length>1?args[1]:"1.0.0", to=args.Length>2?args[2]:"1.0.1";
  string localRelease=args.Length>3?Path.GetFullPath(args[3]):null;
  if(state.version!=from)throw new Exception("Unexpected installed version");
  string oldEngine=Engine.Hash(Path.Combine(root,"updates/HitMeUpdater.dll"));
  string before=Engine.Hash(Path.Combine(root,"Update.pac"));
  string marker=Path.Combine(root,"Save","user-protected.dat");Directory.CreateDirectory(Path.GetDirectoryName(marker));File.WriteAllText(marker,"do-not-change");
  string protectedHash=Engine.Hash(marker);
  var settings=Engine.Parse<Settings>(File.ReadAllText(Path.Combine(root,"updates/settings.json"),Encoding.UTF8));
  var remote=localRelease==null?Engine.Fetch(settings):Engine.Parse<Manifest>(File.ReadAllText(Path.Combine(localRelease,"update-manifest.json"),Encoding.UTF8));Engine.Validate(remote,state,root);
  if(remote.version!=to)throw new Exception("Unexpected remote version");
  int downloads=0;
  long bytes=0;
  Engine.Apply(root,remote,state,(p,d)=>{downloads++;bytes+=p.size;if(localRelease==null)Engine.Download("https://github.com/"+settings.repository+"/releases/download/"+remote.release_tag+"/"+p.name,d,p.size,s=>{});else File.Copy(Path.Combine(localRelease,p.name),d);},s=>Console.WriteLine(s));
  var current=Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json"),Encoding.UTF8));
  Engine.CheckInstalled(root,current);Engine.CheckBase(root);
  if(current.version!=to||downloads!=remote.packages.Count||Engine.Hash(marker)!=protectedHash)throw new Exception("Upgrade invariant failed");
  foreach(var f in state.files.Where(f=>!remote.files.Any(n=>n.path==f.path)&&!remote.deleted.Contains(f.path)))
   if(Engine.Hash(Path.Combine(root,f.path))!=f.sha256)throw new Exception("Untouched file changed: "+f.path);
  Engine.Apply(root,remote,current,(p,d)=>{throw new Exception("Repeat download");},s=>{});
  var report=new {date="2026-10-10",from=from,to=to,used_published_previous_engine=true,old_engine_sha256=oldEngine,new_engine_sha256=Engine.Hash(Path.Combine(root,"updates/HitMeUpdater.dll")),source=localRelease==null?"Public GitHub latest manifest and release ZIP":"Local release preflight",downloaded_packages=downloads,downloaded_bytes=bytes,game_update_pac_unchanged=Engine.Hash(Path.Combine(root,"Update.pac"))==before,synthetic_save_preserved=true,unchanged_inventory_preserved=true,repeat_downloads=0,original_base_requirements="PASS",actual_game_launched=false};
  File.WriteAllText(Path.Combine(root,"upgrade-result.json"),Engine.Json(report),new UTF8Encoding(false));
  Console.WriteLine("PASS: "+from+" -> "+to+"; full inventory, protected save and no repeat download");
  Console.WriteLine("No real game or user saves accessed.");
 }
}
