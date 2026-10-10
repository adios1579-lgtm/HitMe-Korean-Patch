using System;
using System.IO;
using System.Text;
using HitMeUpdate;
class PackageProbe {
 static void Main(string[] args) {
  string root=Path.GetFullPath(args[0]);
  var state=Engine.Parse<State>(File.ReadAllText(Path.Combine(root,"updates/installed.json"),Encoding.UTF8));
  Engine.CheckInstalled(root,state);Engine.CheckBase(root);
  Console.WriteLine("PASS: extracted full patch inventory and original game requirements");
  if(args.Length>1) {
   var settings=Engine.Parse<Settings>(File.ReadAllText(Path.Combine(root,"updates/settings.json"),Encoding.UTF8));
   var remote=Engine.Fetch(settings);Engine.Validate(remote,state,root);
   if(remote.version!=state.version)throw new Exception("Unexpected public latest version");
   Console.WriteLine("PASS: actual updater fetched public latest manifest version "+remote.version);
  }
  Console.WriteLine("No game process launched.");
 }
}
