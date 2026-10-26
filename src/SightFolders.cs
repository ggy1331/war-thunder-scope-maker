using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScopeMaker {
public static class SightFolders {
 public static List<string> Find(string documents=null){
  string saves=Path.Combine(documents??Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"My Games","WarThunder","Saves");
  var found=new List<string>();
  try{foreach(string profile in Directory.GetDirectories(saves)){string path=Path.Combine(profile,"production","UserSights");if(Directory.Exists(path))found.Add(path);}}catch(IOException){}catch(UnauthorizedAccessException){}
  found.Sort(StringComparer.OrdinalIgnoreCase);return found;
 }
 public static string ExportTarget(string path){string universal=Path.Combine(path,"all_tanks");return Directory.Exists(universal)?universal:path;}
 public static string Choose(IWin32Window owner){var found=Find();if(found.Count==0){MessageBox.Show(owner,L.T("Папка UserSights не найдена. Создайте пользовательский прицел в War Thunder или выберите папку вручную."),L.T("Папка прицелов WT"),MessageBoxButtons.OK,MessageBoxIcon.Information);return null;}if(found.Count==1)return ExportTarget(found[0]);
  using(var dialog=new Form{Text=L.T("Папка прицелов WT"),ClientSize=new Size(780,175),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var caption=new Label{Text=L.T("Найдено несколько профилей WT. Выберите папку нужного аккаунта:"),Location=new Point(20,20),Size=new Size(740,35)};
   var options=new ModernCombo{Location=new Point(20,60),Width=740,AccessibleName=L.T("Профиль War Thunder")};options.Items.AddRange(found.Cast<object>().ToArray());options.SelectedIndex=0;
   var apply=new ModernButton{Text=L.T("Выбрать…"),Location=new Point(620,115),Width=140,Primary=true,DialogResult=DialogResult.OK};var cancel=new ModernButton{Text=L.T("Отмена"),Location=new Point(470,115),Width=140,DialogResult=DialogResult.Cancel};
   dialog.Controls.AddRange(new Control[]{caption,options,apply,cancel});dialog.AcceptButton=apply;dialog.CancelButton=cancel;Ui.Theme(dialog);
   return dialog.ShowDialog(owner)==DialogResult.OK?ExportTarget(found[options.SelectedIndex]):null;
  }
 }
}
}
