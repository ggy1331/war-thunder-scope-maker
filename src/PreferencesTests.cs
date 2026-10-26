using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScopeMaker {
public partial class MainWindow {
 public static void PreferencesChecks(List<string> results){var initial=Preferences.Current.Copy();string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings-"+Guid.NewGuid().ToString("N")+".json");try{
  var settings=new AppPreferences{Theme="dark",Accent="#14b8a6",Language="en",ExportDirectory=AppDomain.CurrentDomain.BaseDirectory};Preferences.Save(settings,path);var loaded=Preferences.Read(path);if(loaded.Theme!="dark"||loaded.Accent!="#14B8A6"||loaded.Language!="en"||loaded.ExportDirectory!=settings.ExportDirectory)throw new Exception("Preferences did not persist");loaded.Theme="light";Preferences.Save(loaded,path);if(Preferences.Read(path).Theme!="light")throw new Exception("Atomic settings replacement failed");results.Add("PASS: Preferences persist and update theme, accent, language and export folder atomically");
  File.WriteAllText(path,"invalid JSON");if(Preferences.Read(path).Language!="ru")throw new Exception("Corrupt settings did not recover to defaults");results.Add("PASS: Corrupt preference files recover to safe defaults");
  Preferences.Current=settings;Ui.Configure(settings);L.ApplyCulture();if(Preferences.ExportStartDirectory()!=settings.ExportDirectory)throw new Exception("Default export folder not applied");Preferences.Current=new AppPreferences();Ui.Configure(Preferences.Current);L.ApplyCulture();results.Add("PASS: Export dialog uses the selected default directory");
  foreach(string hex in new[]{"#FFFFFF","#FFFF00","#14B8A6","#5269EB","#000000"}){var palette=Palette.Create(new AppPreferences{Accent=hex});double a=Palette.Luminance(palette.Accent),b=Palette.Luminance(palette.OnAccent);if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5)throw new Exception("Insufficient contrast on accent "+hex);}results.Add("PASS: Accent button text retains readable contrast for light and dark colors");
  using(var w=new MainWindow()){w.Show();Application.DoEvents();w.AddPart("text");w.text.Text="Keep this custom text — не переводить";string before=w.json.Serialize(w.Doc);int history=w.undo.Count;bool dirtyBefore=w.dirty;var next=new AppPreferences{Theme="dark",Accent="#14B8A6",Language="en"};w.ApplyPreferences(next);Application.DoEvents();
   if(w.elementTab.Text!="Layers"||w.templateTab.Text!="Templates"||w.elementHeading.Text!="Text"||w.fieldLabels["X"].Text!="Position X")throw new Exception("Main interface is not fully localized");if(w.BackColor.ToArgb()!=Ui.Surface.ToArgb()||w.props.BackColor.ToArgb()!=Ui.Paper.ToArgb())throw new Exception("Dark palette not applied to existing controls");if(w.json.Serialize(w.Doc)!=before||w.undo.Count!=history||w.dirty!=dirtyBefore)throw new Exception("Preferences changed open project or history");
   using(var gallery=new BackgroundDialog(w.Doc)){gallery.Show();Application.DoEvents();if(gallery.Text!="War Thunder backgrounds"||Descendants(gallery).OfType<ComboBox>().First().Items[0].ToString()!="Day")throw new Exception("Background dialog not localized");gallery.Close();}
   using(var image=FeatureTests.Fixture())using(var import=new ImageImportDialog(image)){import.Show();Application.DoEvents();if(!import.Text.StartsWith("Import image"))throw new Exception("Image import not localized");if(Descendants(import).OfType<ComboBox>().First().Items[0].ToString()!="Detect automatically")throw new Exception("Image import options not localized");import.Close();}
   results.Add("PASS: Dark English UI and auxiliary dialogs apply without changing artwork, unsaved state or history");
   w.ApplyPreferences(new AppPreferences());if(w.elementTab.Text!="Элементы"||w.elementHeading.Text!="Текст"||w.fieldLabels["X"].Text!="Положение X"||w.json.Serialize(w.Doc)!=before)throw new Exception("Switching back to Russian failed");results.Add("PASS: Switching English back to Russian restores live labels and preserves project content");w.dirty=false;w.Close();
  }
  using(var logo=Branding.Render(256,Color.FromArgb(82,105,235))){if(logo.Width!=256||logo.GetPixel(0,0).A!=0||logo.GetPixel(128,60).R<50||logo.GetPixel(128,60).G>40)throw new Exception("Branding output invalid");}using(var icon=new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","scope-maker.ico"))){if(icon.Width<16)throw new Exception("Invalid Windows icon");}results.Add("PASS: User-supplied emblem and caption render with transparent corners and a valid Windows icon");
 }finally{if(File.Exists(path))File.Delete(path);Preferences.Current=initial;Ui.Configure(initial);L.ApplyCulture();}}
}
}
