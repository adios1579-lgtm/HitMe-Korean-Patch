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
  if(state.version!="1.0.0")throw new Exception("Expected real 1.0.0 installation");
  string before=Engine.Hash(Path.Combine(root,"Update.pac"));
  string marker=Path.Combine(root,"Save","user-protected.dat");Directory.CreateDirectory(Path.GetDirectoryName(marker));File.WriteAllText(marker,"do-not-change");
  string protectedHash=Engine.Hash(marker);
  var settings=Engine.Parse<Settings>(File.ReadAllText(Path.Combine(root,"updates/settings.json"),Encoding.UTF8));
  var remote=Engine.Fetch(settings);Engine.Validate(remote,state,root);
  if(remote.version!="1.0.1")throw new Exception("Unexpected version");
  int downloads=0;
  Engine.Apply(root,remote,state,(p,d)=>{downloads++;Engine.Download("https://github.com/"+settings.repository+"/releases/download/"+remote.release_tag+"/"+p.name,d,p.size,s=>{});},s=>Console.WriteLine(s));
  var current=Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json"),Encoding.UTF8));
  Engine.CheckInstalled(root,current);Engine.CheckBase(root);
  if(current.version!="1.0.1"||downloads!=1||Engine.Hash(marker)!=protectedHash||Engine.Hash(Path.Combine(root,"Update.pac"))!=before)throw new Exception("Upgrade invariant failed");
  Console.WriteLine("PASS: published 1.0.0 engine applied public 1.0.1 package; archive/save unchanged");
  Engine.Apply(root,remote,current,(p,d)=>{throw new Exception("Repeat download");},s=>{});
  Console.WriteLine("PASS: rerun performs no download or replacement");
  Console.WriteLine("No real game or user saves accessed.");
 }
}
