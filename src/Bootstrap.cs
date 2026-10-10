using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

class Bootstrap {
 [STAThread] static void Main(string[] args) {
  try {
   string root=AppDomain.CurrentDomain.BaseDirectory;
   string dll=Path.Combine(root,"updates","HitMeUpdater.dll");
   for(string p=dll;p!=null && p.Length>=root.TrimEnd('\\').Length;p=Path.GetDirectoryName(p))
    if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
     throw new Exception("연결된 경로에서는 실행할 수 없습니다.");
   bool manual=Path.GetFileNameWithoutExtension(Application.ExecutablePath).Contains("업데이트")||Array.IndexOf(args,"--update")>=0;
   var assembly=Assembly.Load(File.ReadAllBytes(dll));
   assembly.GetType("HitMeUpdate.App").GetMethod("Run").Invoke(null,new object[]{root,manual});
  } catch(Exception e) {
   MessageBox.Show((e.InnerException??e).Message,"히트미 한글패치 실행 오류",MessageBoxButtons.OK,MessageBoxIcon.Error);
  }
 }
}
