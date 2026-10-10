using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HitMeUpdate {
 // Session-only font registration: no registry writes or system installation.
 public sealed class SessionFont : IDisposable {
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern int AddFontResourceEx(string name,uint flags,IntPtr reserved);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool RemoveFontResourceEx(string name,uint flags,IntPtr reserved);
  string path;
  public static SessionFont Load(string file) {
   if(!File.Exists(file))throw new Exception("한글 폰트 파일이 없습니다. 패치를 다시 풀어 주세요.");
   if(AddFontResourceEx(file,0,IntPtr.Zero)==0)throw new Exception("한글 폰트를 불러오지 못했습니다.");
   return new SessionFont {path=file};
  }
  public void Dispose() {
   if(path!=null){RemoveFontResourceEx(path,0,IntPtr.Zero);path=null;}
  }
 }
}
