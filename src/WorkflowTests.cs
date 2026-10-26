using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ScopeMaker {
public partial class MainWindow {
 public static void WorkflowChecks(List<string> results){
  string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"workflow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
  try{
   if(SightFolders.Find(fixture).Count!=0)throw new Exception("Missing game folder should be handled");
   string root=Path.Combine(fixture,"My Games","WarThunder","Saves");string first=Path.Combine(root,"111","production","UserSights");Directory.CreateDirectory(Path.Combine(first,"all_tanks"));
   if(SightFolders.Find(fixture).Count!=1||SightFolders.ExportTarget(first)!=Path.Combine(first,"all_tanks"))throw new Exception("Single WT profile or all_tanks detection failed");
   string second=Path.Combine(root,"222","production","UserSights");Directory.CreateDirectory(second);Directory.CreateDirectory(Path.Combine(root,"333","production"));
   if(SightFolders.Find(fixture).Count!=2||SightFolders.ExportTarget(second)!=second)throw new Exception("Multiple profiles or missing all_tanks detection failed");results.Add("PASS: Sight folder discovery handles missing folders, multiple profiles and all_tanks in redirected Documents");
   using(var w=new MainWindow()){
    w.Show();Application.DoEvents();w.AddPart("text");w.text.Text="Saved artwork";string path=Path.Combine(fixture,"saved.wtsm");w.SaveProjectTo(path);
    w.layers.SelectedIndex=0;w.UpdateProps();w.zoom.Value=160;w.Changed();if(w.dirty||!w.ConfirmLoss())throw new Exception("Saved unmodified project asks to save again");
    results.Add("PASS: Saving then selecting layers, refreshing properties and zooming leaves project clean on close");
    double original=w.Doc.Parts[0].X;w.Remember();w.Doc.Parts[0].X=original+.125;w.Changed();if(!w.dirty)throw new Exception("Real edits were not tracked");w.Undo(false);if(w.dirty)throw new Exception("Undo to saved state should be clean");w.Undo(true);if(!w.dirty)throw new Exception("Redo away from saved state should be dirty");w.SaveProjectTo(path);w.Changed();if(w.dirty)throw new Exception("Second save should update snapshot");
    results.Add("PASS: Real edits prompt for saving; undo to saved content and saving again clear unsaved state");
    var loaded=w.json.Deserialize<Project>(File.ReadAllText(path));w.SetDocument(loaded,path);w.Changed();if(w.dirty||!w.ConfirmLoss())throw new Exception("Reopened saved project is dirty");
    results.Add("PASS: Reopening a saved project establishes its clean baseline");w.Close();
   }
  }finally{Directory.Delete(fixture,true);}
 }
}
}
