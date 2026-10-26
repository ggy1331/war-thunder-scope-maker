using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScopeMaker {
public class GameTemplate { public string File,Name,Group; }
public static class Compatibility {
 public const string Universal="matchExpClass { exp_tank:b=yes; exp_heavy_tank:b=yes; exp_tank_destroyer:b=yes; exp_SPAA:b=yes; }\n";
 public static void Migrate(Project project){if(project.Version!=1)return;foreach(var s in project.Parts){s.Y=-s.Y;s.Y2=-s.Y2;s.Start=-s.Start;s.End=-s.End;if(!String.IsNullOrEmpty(s.Original)){var original=Blk.Parse(s.Original)[0];if((s.Kind=="line"||s.Kind=="circle")&&original.Get("size",null)!=null)s.StrokeWidth=Blk.Values(original.Get("size","1"),1)[0];s.Align=(int)Blk.Values(original.Get("align","0"),1)[0];s.Highlight=new[]{"yes","true","1","on"}.Contains(original.Get("highlight","no"));}if(s.Tiles!=null)foreach(var tile in s.Tiles){tile[1]=-tile[1];tile[3]=-tile[3];}if(s.Kind=="quad"&&!String.IsNullOrEmpty(s.Original)){var n=Blk.Parse(s.Original)[0];foreach(string key in new[]{"tl","tr","br","bl","center","radialCenter"})if(n.Get(key,null)!=null){var v=Blk.Values(n.Get(key,"0,0"),2);n.Set(key,"p2",Blk.Num(v[0])+", "+Blk.Num(-v[1]));}s.Original=n.ToString();}}project.Version=2;}
 public static void Audit(string folder,string report){
  int passed=0,presets=0;var failures=new List<object>();var cache=new Dictionary<string,string>();
  using(var hash=System.Security.Cryptography.SHA256.Create())foreach(string file in Directory.EnumerateFiles(folder,"*.blk",SearchOption.AllDirectories)){
   byte[] bytes=System.IO.File.ReadAllBytes(file);string key=BitConverter.ToString(hash.ComputeHash(bytes)),outcome;
   if(!cache.TryGetValue(key,out outcome)){try{string source=System.IO.File.ReadAllText(file);var nodes=Blk.Parse(source);if(nodes.Any(n=>n.Name=="crosshair"&&n.Type=="t"))outcome="PRESET";else{var project=Blk.Import(source);var again=Blk.Import(Blk.Export(project));if(project.Parts.Count!=again.Parts.Count)throw new Exception("Geometry count changed on round trip");outcome="PASS";}}catch(Exception ex){outcome=ex.Message;}cache[key]=outcome;}
   if(outcome=="PASS")passed++;else if(outcome=="PRESET")presets++;else failures.Add(new{File=file,Error=outcome});
  }
  System.IO.File.WriteAllText(report,new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=passed,Presets=presets,Failed=failures.Count,UniqueFiles=cache.Count,Failures=failures}),new UTF8Encoding(false));
 }
 public static void TestLibrary(List<string> results){string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates","game");var manifest=Path.Combine(directory,"manifest.json");if(!System.IO.File.Exists(manifest))throw new Exception("Game template library is missing");var entries=new JavaScriptSerializer().Deserialize<List<GameTemplate>>(System.IO.File.ReadAllText(manifest));int parts=0;foreach(var item in entries){var p=Blk.Import(System.IO.File.ReadAllText(Path.Combine(directory,item.File)));var q=Blk.Import(Blk.Export(p));if(p.Parts.Count!=q.Parts.Count)throw new Exception("Template round trip failed: "+item.Name);for(int i=0;i<p.Parts.Count;i++){var a=p.Parts.OrderBy(x=>x.Kind).ElementAt(i);var b=q.Parts.OrderBy(x=>x.Kind).ElementAt(i);if(a.Kind!=b.Kind||Math.Abs(a.X-b.X)>1e-12||Math.Abs(a.Y-b.Y)>1e-12||a.StrokeWidth!=b.StrokeWidth||a.Align!=b.Align)throw new Exception("Template geometry changed: "+item.Name);}parts+=p.Parts.Count;}
  results.Add("PASS: "+entries.Count+" local game templates / "+parts+" elements round-trip without geometry loss");
  var old=new Project{Version=1,Parts=new List<Part>{new Part{Y=0.2,Y2=0.4}}};Migrate(old);if(old.Version!=2||old.Parts[0].Y!=-0.2)throw new Exception("Legacy coordinate migration failed");results.Add("PASS: Legacy projects migrate to native WT vertical coordinates");
  var plain=Blk.Export(Blk.Import("drawLines { line { line:p4=0,-1,0,1; size:r=2.5; } }"));if(plain.Contains("move:b")||plain.Contains("thousandth:b")||plain.Contains("crosshairColor"))throw new Exception("Implicit game defaults were overwritten");results.Add("PASS: Unspecified game defaults stay unspecified after import/export");
  bool rejected=false;try{Blk.Import("crosshair:t=\"us_modern_sight_with_range\"; rangefinder { visible:b=yes; }");}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("HUD preset misidentified as sight geometry");results.Add("PASS: HUD presets are distinguished from sight geometry");
  string sample=L.T("\\ path \"quoted\" ~\nстрока");var textProject=new Project{Parts=new List<Part>{new Part{Kind="text",Text=sample}}};if(Blk.Import(Blk.Export(textProject)).Parts[0].Text!=sample)throw new Exception("Native BLK string escaping failed");
  if(Blk.Import("drawTexts { text { text:t=\"\\\"; pos:p2=0,0; } }").Parts[0].Text!="\\")throw new Exception("Literal backslash corrupted");
  results.Add("PASS: Dagor tilde escaping and literal backslash glyphs round-trip");
 }
}
public partial class MainWindow {
 List<GameTemplate> gameTemplates=new List<GameTemplate>();
 string nativeCacheSource;List<BlkNode> nativeCache;
 NumericUpDown strokeWidth=new NumericUpDown();ComboBox textAlign=new ComboBox(),angularUnits=new ComboBox();CheckBox textHighlight=new CheckBox(),centralVert=new CheckBox(),centralHorz=new CheckBox();
 List<BlkNode> NativeNodes(){if(nativeCache==null||nativeCacheSource!=Doc.Base){nativeCacheSource=Doc.Base;nativeCache=Blk.Parse(Doc.Base);}return nativeCache;}
 string NativeValue(string name,string fallback){var n=NativeNodes().LastOrDefault(x=>!x.Block&&x.Name==name);return n==null?fallback:n.Value;}
 bool NativeBool(string name){return new[]{"yes","true","on","1"}.Contains(NativeValue(name,"no"));}
 double NativeFontScale(){double value;return Double.TryParse(NativeValue("fontSizeMult","1"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:1;}
 bool HasNativeScales(){return NativeNodes().Any(n=>n.Block&&(n.Name=="ballistics"||n.Name=="crosshair_distances"||n.Name=="crosshair_hor_ranges")&&n.Children.Count>0);}
 void RenderNativeBase(Graphics g,Rectangle r,Pen pen){if(NativeBool("drawCentralLineVert"))g.DrawLine(pen,r.Width/2f+pan.X,0,r.Width/2f+pan.X,r.Height);if(NativeBool("drawCentralLineHorz"))g.DrawLine(pen,0,r.Height/2f+pan.Y,r.Width,r.Height/2f+pan.Y);}
 void LoadGameTemplates(){string manifest=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates","game","manifest.json");if(!System.IO.File.Exists(manifest))return;gameTemplates=json.Deserialize<List<GameTemplate>>(System.IO.File.ReadAllText(manifest));foreach(var item in gameTemplates)templates.Items.Add("WT · "+item.Name+(item.Group=="all_tanks"?"":" / "+item.Group));}
 Project ChosenTemplate(){if(templates.SelectedIndex<3)return Preset(templates.SelectedIndex);var item=gameTemplates[templates.SelectedIndex-3];var p=Blk.Import(System.IO.File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates","game",item.File)));p.Title=TemplateTitle(templates.SelectedIndex);return p;}
 public void LoadTemplateForScreenshot(string name,string path){int index=gameTemplates.FindIndex(x=>x.Name==name);if(index<0)throw new Exception("Template not found");templates.SelectedIndex=index+3;SetDocument(ChosenTemplate(),"");Screenshot(path);}
 void SetNative(string name,string type,string value){if(syncing||L.Applying)return;Remember();var nodes=Blk.Parse(Doc.Base);nodes.RemoveAll(n=>!n.Block&&n.Name==name);nodes.Add(new BlkNode{Name=name,Type=type,Value=value});Doc.Base=Blk.Dump(nodes);Changed();}
 void SetupGameProperties(int y){
  props.Controls.Add(new Label{Text=L.T("ПАРАМЕТРЫ WT"),ForeColor=accent,Location=new Point(16,y),AutoSize=true});y+=34;
  props.Controls.Add(new Label{Text=L.T("Толщина элемента"),Location=new Point(16,y),AutoSize=true});strokeWidth.Location=new Point(16,y+23);strokeWidth.Width=215;strokeWidth.Minimum=0.1M;strokeWidth.Maximum=100;strokeWidth.DecimalPlaces=2;strokeWidth.Increment=0.1M;strokeWidth.Value=1;strokeWidth.ValueChanged+=delegate{if(syncing||Selected==null)return;Remember();Selected.StrokeWidth=(double)strokeWidth.Value;Changed();};props.Controls.Add(strokeWidth);y+=65;
  props.Controls.Add(new Label{Text=L.T("Выравнивание текста"),Location=new Point(16,y),AutoSize=true});textAlign.Location=new Point(16,y+23);textAlign.Width=215;textAlign.ForeColor=Color.Black;textAlign.DropDownStyle=ComboBoxStyle.DropDownList;textAlign.Items.AddRange(new object[]{L.T("По центру"),L.T("От точки вправо"),L.T("От точки влево")});textAlign.SelectedIndex=0;textAlign.SelectedIndexChanged+=delegate{if(syncing||Selected==null)return;Remember();Selected.Align=textAlign.SelectedIndex;Changed();};props.Controls.Add(textAlign);y+=65;
  textHighlight.Text=L.T("Подсветка текста в игре");textHighlight.Location=new Point(16,y);textHighlight.Width=225;textHighlight.CheckedChanged+=delegate{if(syncing||Selected==null)return;Remember();Selected.Highlight=textHighlight.Checked;Changed();};props.Controls.Add(textHighlight);y+=36;
  props.Controls.Add(new Label{Text=L.T("Система тысячных"),Location=new Point(16,y),AutoSize=true});angularUnits.Location=new Point(16,y+23);angularUnits.Width=215;angularUnits.ForeColor=Color.Black;angularUnits.DropDownStyle=ComboBoxStyle.DropDownList;angularUnits.Items.AddRange(new object[]{L.T("СССР — 1/6000"),"NATO — 1/6400",L.T("Радианная — 1/6283")});angularUnits.SelectedIndex=0;angularUnits.SelectedIndexChanged+=delegate{if(syncing)return;SetNative("thousandth","t","\""+new[]{"ussr","nato","real"}[angularUnits.SelectedIndex]+"\"");};props.Controls.Add(angularUnits);y+=65;
  centralVert.Text=L.T("Стандартная вертикаль");centralHorz.Text=L.T("Стандартная горизонталь");foreach(var cb in new[]{centralVert,centralHorz}){cb.Location=new Point(16,y);cb.Width=225;props.Controls.Add(cb);y+=30;}centralVert.CheckedChanged+=delegate{SetNative("drawCentralLineVert","b",centralVert.Checked?"yes":"no");};centralHorz.CheckedChanged+=delegate{SetNative("drawCentralLineHorz","b",centralHorz.Checked?"yes":"no");};
  layers.SelectedIndexChanged+=delegate{RefreshNativeProps();};
 }
 void RefreshNativeProps(){if(Doc==null)return;bool old=syncing;syncing=true;var p=Selected;strokeWidth.Enabled=p!=null&&(p.Kind=="line"||p.Kind=="circle");strokeWidth.Value=(decimal)Math.Max(0.1,Math.Min(100,p==null?1:p.StrokeWidth));textAlign.Enabled=textHighlight.Enabled=p!=null&&p.Kind=="text";textAlign.SelectedIndex=Math.Max(0,Math.Min(2,p==null?0:p.Align));textHighlight.Checked=p!=null&&p.Highlight;string units=NativeValue("thousandth","\"ussr\"").Trim('"');angularUnits.SelectedIndex=units=="nato"?1:units=="real"?2:0;centralVert.Checked=NativeBool("drawCentralLineVert");centralHorz.Checked=NativeBool("drawCentralLineHorz");syncing=old;}
}
}
