using System;
using System.IO;
using System.Threading;
class LaunchFixture {
 static void Main(string[] args) {
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-cwd.txt"),Environment.CurrentDirectory);
  if(args.Length>0&&args[0]=="--hold")Thread.Sleep(30000);
 }
}
