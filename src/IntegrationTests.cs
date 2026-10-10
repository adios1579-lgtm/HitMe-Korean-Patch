using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using Microsoft.Win32;
using HitMeUpdate;

class IntegrationTests {
 static int Count(RegistryKey root) {
  using(var k=root.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts"))return k==null?0:k.ValueCount;
 }
 static void Main(string[] args) {
  int before=Count(Registry.CurrentUser),machine=Count(Registry.LocalMachine);
  using(var f=SessionFont.Load(args[0])) {
   using(var font=new Font("Noto Sans KR",24)) {
    if(!font.FontFamily.Name.StartsWith("Noto Sans KR"))throw new Exception("Font fallback: "+font.FontFamily.Name);
    Console.WriteLine("PASS: bundled font family "+font.FontFamily.Name);
   }
   if(Count(Registry.CurrentUser)!=before||Count(Registry.LocalMachine)!=machine)throw new Exception("Registry changed");
  }
  if(Count(Registry.CurrentUser)!=before||Count(Registry.LocalMachine)!=machine)throw new Exception("Registry changed after removal");
  Console.WriteLine("PASS: font registry value counts unchanged (user="+before+", machine="+machine+")");
  string root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
  string target=Path.Combine(root,"HitMe.exe");File.Copy(args[2],target);
  var info=App.LaunchStartInfo(root);info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;
  using(var p=Process.Start(info)){if(!p.WaitForExit(10000))throw new Exception("Fixture timeout");}
  if(File.ReadAllText(Path.Combine(root,"fixture-cwd.txt"))!=root)throw new Exception("Wrong CWD");
  Console.WriteLine("PASS: synthetic game executable launched with exact working directory");
  info.Arguments="--hold";
  using(var p=Process.Start(info)) {
   try {
    Thread.Sleep(300);if(!Engine.Running(root))throw new Exception("Running process not detected");
    if(Engine.Running(Path.Combine(root,"other")))throw new Exception("Unrelated path matched");
    Console.WriteLine("PASS: running HitMe fixture detection; other folder excluded");
   } finally {if(!p.HasExited){p.Kill();p.WaitForExit();}}
  }
  // Only these files created by the test are removed. No installed game is used.
  File.Delete(target);File.Delete(Path.Combine(root,"fixture-cwd.txt"));Directory.Delete(root);
  Console.WriteLine("No actual game launched; no persistent font installation.");
 }
}
