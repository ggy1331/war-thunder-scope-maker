using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScopeMaker {
public class Part {
 public string Kind = "line", Name = L.T("Линия"), Text = "1000", Original = "";
 public double X = -0.2, Y = 0, X2 = 0.2, Y2 = 0, Size = 1;
 public double Start = 0, End = 360;
 public bool Visible = true, Move = false, Mils = false;
 public double StrokeWidth = 1;
 public int Align = 0;
 public bool Highlight = false;
 public List<double[]> Tiles;
}
public class Project {
 public int Version = 2;
 public string Title = L.T("Новый прицел"), Base = "";
 public int Color = System.Drawing.Color.FromArgb(92, 245, 156).ToArgb();
 public double LineWidth = 1.5;
 public List<Part> Parts = new List<Part>();
 public string BackgroundKey = "", BackgroundFile = "";
 public int BackgroundDim = 0;
 public bool PreviewGrid = true;
}
public class BlkNode {
 public string Name = "", Type = "", Value = "";
 public bool Block;
 public List<BlkNode> Children = new List<BlkNode>();
 public string Get(string name, string fallback) { var n = Children.LastOrDefault(x=>x.Name==name && !x.Block); return n==null ? fallback : n.Value; }
 public void Set(string name,string type,string value) { Children.RemoveAll(x=>x.Name==name && !x.Block); Children.Add(new BlkNode{Name=name,Type=type,Value=value}); }
 public override string ToString() { if(!Block)return Name+":"+Type+" = "+Value+";\n"; return Name+" {\n"+String.Join("",Children.Select(x=>x.ToString()).ToArray())+"}\n"; }
}
public static class Blk {
 static List<string> Lex(string s) {
  var tokens=new List<string>(); int i=0;
  while(i<s.Length) {
   char c=s[i]; if(Char.IsWhiteSpace(c)){i++;continue;}
   if(c=='/'&&i+1<s.Length&&s[i+1]=='/'){while(i<s.Length&&s[i]!='\n')i++;continue;}
   if(c=='/'&&i+1<s.Length&&s[i+1]=='*'){int end=s.IndexOf("*/",i+2,StringComparison.Ordinal);if(end<0)throw new FormatException(L.T("Незакрытый комментарий"));i=end+2;continue;}
   if(c=='"'||c=='\''){char quote=c;int start=i++;bool closed=false;while(i<s.Length){if(s[i]=='~'){i+=2;continue;}if(s[i++]==quote){closed=true;break;}}if(!closed)throw new FormatException("Незакрытая строка");tokens.Add(s.Substring(start,i-start));continue;}
   if("{}:=;,".IndexOf(c)>=0){tokens.Add(c.ToString());i++;continue;}
   int a=i;while(i<s.Length&&!Char.IsWhiteSpace(s[i])&&"{}:=;,\"".IndexOf(s[i])<0)i++;if(i==a)throw new FormatException(L.T("Неизвестный символ"));tokens.Add(s.Substring(a,i-a));
  } return tokens;
 }
 public static List<BlkNode> Parse(string s){var t=Lex(s);int i=0;return Read(t,ref i,false);}
 static List<BlkNode> Read(List<string> t,ref int i,bool nested){
  var result=new List<BlkNode>();
  while(i<t.Count){if(t[i]==";"){i++;continue;}if(t[i]=="}"){if(!nested)throw new FormatException(L.T("Лишняя закрывающая скобка"));i++;return result;}
   var n=new BlkNode{Name=t[i++]};if(i>=t.Count)throw new FormatException(L.T("Не завершён параметр ")+n.Name);
   if(t[i]=="{"){i++;n.Block=true;n.Children=Read(t,ref i,true);}
   else {Need(t,ref i,":");if(i>=t.Count)throw new FormatException(L.T("Нет типа"));n.Type=t[i++];Need(t,ref i,"=");var v=new List<string>();
    while(i<t.Count&&t[i]!=";"&&t[i]!="}"){if(v.Count>0&&i+1<t.Count&&(t[i+1]==":"||t[i+1]=="{"))break;v.Add(t[i++]);}
    if(v.Count==0)throw new FormatException(L.T("Нет значения ")+n.Name);n.Value=String.Join(" ",v.ToArray());
   }result.Add(n);
  }if(nested)throw new FormatException(L.T("Не закрыт блок"));return result;
 }
 static void Need(List<string> t,ref int i,string v){if(i>=t.Count||t[i++]!=v)throw new FormatException(L.T("Ожидалось ")+v);}
 public static string Dump(IEnumerable<BlkNode> n){return String.Join("",n.Select(x=>x.ToString()).ToArray());}
 public static string Num(double d){return d.ToString("G17",CultureInfo.InvariantCulture);}
 public static double[] Values(string s,int count){var a=s.Split(',').Select(x=>Double.Parse(x.Trim(),CultureInfo.InvariantCulture)).ToArray();if(a.Length!=count||a.Any(x=>Double.IsNaN(x)||Double.IsInfinity(x)||Math.Abs(x)>1000000))throw new FormatException(L.T("Недопустимые координаты: ожидалось ")+count+L.T(", получено «")+s+"»");return a;}
 static bool Yes(string s){return s=="yes"||s=="true"||s=="1"||s=="on";}
 public static string Quote(string s){return "\""+(s??"").Replace("~","~~").Replace("\"","~\"").Replace("\r","~r").Replace("\n","~n").Replace("\t","~t")+"\"";}
 static string Unquote(string s){if(s.Length>=2&&(s[0]=='"'||s[0]=='\'')&&s[s.Length-1]==s[0])s=s.Substring(1,s.Length-2);var b=new StringBuilder();for(int i=0;i<s.Length;i++){if(s[i]=='~'&&i+1<s.Length){char c=s[++i];b.Append(c=='n'?'\n':c=='r'?'\r':c=='t'?'\t':c);}else b.Append(s[i]);}return b.ToString();}
 public static Project Import(string text){
  var nodes=Parse(text);
  if(nodes.Any(x=>!x.Block&&x.Name=="crosshair"&&x.Type=="t"))throw new FormatException("Это пресет настроек интерфейса WT, а не файл геометрии прицела. Выберите соответствующую встроенную сетку в списке шаблонов WT.");
  if(nodes.Count==1&&nodes[0].Block&&nodes[0].Children.Any(x=>new[]{"drawLines","drawTexts","drawCircles","drawQuads","ballistics"}.Contains(x.Name)))nodes=nodes[0].Children;
  var p=new Project{Base=Dump(nodes),Title="Импортированный прицел",Color=System.Drawing.Color.Black.ToArgb(),LineWidth=1};
  var width=nodes.LastOrDefault(x=>x.Name=="lineSizeMult");if(width!=null)p.LineWidth=Values(width.Value,1)[0];
  var color=nodes.LastOrDefault(x=>x.Name=="crosshairColor");if(color!=null){var v=Values(color.Value,4);p.Color=System.Drawing.Color.FromArgb((int)v[3],(int)v[0],(int)v[1],(int)v[2]).ToArgb();}
  foreach(var b in nodes.Where(x=>x.Block&&new[]{"drawLines","drawCircles","drawTexts","drawQuads"}.Contains(x.Name)))foreach(var n in b.Children){
   string expected=b.Name=="drawLines"?"line":b.Name=="drawCircles"?"circle":b.Name=="drawTexts"?"text":"quad";
   if(!n.Block||n.Name!=expected)continue;
   var part=new Part{Kind=expected,Name=expected=="line"?"Линия":expected=="circle"?"Окружность":expected=="text"?"Текст":"Фигура",Original=n.ToString(),Move=Yes(n.Get("move","no")),Mils=Yes(n.Get("thousandth","no")),StrokeWidth=Values(n.Get("size","1"),1)[0],Align=(int)Values(n.Get("align","0"),1)[0],Highlight=Yes(n.Get("highlight","no"))};
   double[] v;
   if(expected=="line"){v=Values(n.Get("line","0,0,0,0"),4);part.X=v[0];part.Y=v[1];part.X2=v[2];part.Y2=v[3];}
   else if(expected=="quad"){v=Values(n.Get("tl","0,0"),2);part.X=v[0];part.Y=v[1];v=Values(n.Get("br","0,0"),2);part.X2=v[0];part.Y2=v[1];}
   else {v=Values(n.Get("pos","0,0"),2);part.X=v[0];part.Y=v[1];part.Size=Values(n.Get(expected=="text"?"size":"diameter","1"),1)[0];if(expected=="text")part.Text=Unquote(n.Get("text","\"\""));else {v=Values(n.Get("segment","0,360"),2);part.Start=v[0];part.End=v[1];}}
   p.Parts.Add(part);
  }return p;
 }
 public static string Export(Project p){
  var nodes=Parse(p.Base);var groups=new Dictionary<string,BlkNode>();
  foreach(string kind in new[]{"line","circle","text","quad"}){
   string name=kind=="line"?"drawLines":kind=="circle"?"drawCircles":kind=="text"?"drawTexts":"drawQuads";
   var g=new BlkNode{Name=name,Block=true};
   foreach(var old in nodes.Where(x=>x.Name==name&&x.Block))g.Children.AddRange(old.Children.Where(x=>!x.Block||x.Name!=kind));
   nodes.RemoveAll(x=>x.Name==name&&x.Block);groups[kind]=g;
  }
  bool colorSpecified=nodes.Any(x=>!x.Block&&x.Name=="crosshairColor"),widthSpecified=nodes.Any(x=>!x.Block&&x.Name=="lineSizeMult");
  nodes.RemoveAll(x=>!x.Block&&(x.Name=="crosshairColor"||x.Name=="lineSizeMult"));
  var c=System.Drawing.Color.FromArgb(p.Color);if(colorSpecified||p.Color!=System.Drawing.Color.Black.ToArgb())nodes.Add(new BlkNode{Name="crosshairColor",Type="c",Value=c.R+", "+c.G+", "+c.B+", "+c.A});if(widthSpecified||p.LineWidth!=1)nodes.Add(new BlkNode{Name="lineSizeMult",Type="r",Value=Num(p.LineWidth)});
  foreach(var s in p.Parts.Where(x=>x.Visible)){
   if(s.Kind=="image") { if(s.Tiles!=null)foreach(var tile in s.Tiles){var q=new BlkNode{Name="quad",Block=true};q.Set("move","b",s.Move?"yes":"no");q.Set("thousandth","b",s.Mils?"yes":"no");string[] corners={"tl","tr","br","bl"};double[] xs={tile[0],tile[2],tile[2],tile[0]},ys={tile[1],tile[1],tile[3],tile[3]};for(int j=0;j<4;j++)q.Set(corners[j],"p2",Num(s.X+xs[j]*s.Size)+", "+Num(s.Y+ys[j]*s.Size));groups["quad"].Children.Add(q);}continue; }
   var n=String.IsNullOrEmpty(s.Original)?new BlkNode{Name=s.Kind,Block=true}:Parse(s.Original)[0];
   if(String.IsNullOrEmpty(s.Original)||n.Get("move",null)!=null||s.Move)n.Set("move","b",s.Move?"yes":"no");
   if(String.IsNullOrEmpty(s.Original)||n.Get("thousandth",null)!=null||s.Mils)n.Set("thousandth","b",s.Mils?"yes":"no");
   if((s.Kind=="line"||s.Kind=="circle")&&(n.Get("size",null)!=null||s.StrokeWidth!=1))n.Set("size","r",Num(s.StrokeWidth));
   if(s.Kind=="text"){if(n.Get("align",null)!=null||s.Align!=0)n.Set("align","i",s.Align.ToString(CultureInfo.InvariantCulture));if(n.Get("highlight",null)!=null||s.Highlight)n.Set("highlight","b",s.Highlight?"yes":"no");}
   if(s.Kind=="line")n.Set("line","p4",Num(s.X)+", "+Num(s.Y)+", "+Num(s.X2)+", "+Num(s.Y2));
   else if(s.Kind=="quad"){
    // Preserve arbitrary imported quadrilaterals; newly generated figures are rectangles.
    if(String.IsNullOrEmpty(s.Original)){n.Set("tl","p2",Num(s.X)+", "+Num(s.Y));n.Set("tr","p2",Num(s.X2)+", "+Num(s.Y));n.Set("br","p2",Num(s.X2)+", "+Num(s.Y2));n.Set("bl","p2",Num(s.X)+", "+Num(s.Y2));}
   }else {n.Set("pos","p2",Num(s.X)+", "+Num(s.Y));if(s.Kind=="text"){n.Set("text","t",Quote(s.Text));n.Set("size","r",Num(s.Size));}else {n.Set("diameter","r",Num(s.Size));n.Set("segment","p2",Num(s.Start)+", "+Num(s.End));}}
   groups[s.Kind].Children.Add(n);
  }
  foreach(var g in groups.Values)if(g.Children.Count>0)nodes.Add(g);
  return "// War Thunder Scope Maker | 0.6.3 pre-alpha\n"+Dump(nodes);
 }
}
public class Canvas : Control {
 public MainWindow OwnerWindow;
 public Canvas(){DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.FromArgb(17,23,30);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(OwnerWindow!=null)OwnerWindow.Render(e.Graphics,ClientRectangle);}
}
public partial class MainWindow : Form {
 public Project Doc;
 Canvas canvas=new Canvas(); ListBox layers=new ListBox(); Panel props=new Panel(); Label status=new Label();
 Dictionary<string,NumericUpDown> numbers=new Dictionary<string,NumericUpDown>(); TextBox text=new TextBox(); CheckBox visible=new CheckBox(),move=new CheckBox(),mils=new CheckBox();
 ComboBox templates=new ComboBox(); NumericUpDown zoom=new NumericUpDown(); bool syncing=false,dirty=false,dragging=false;
 Point lastMouse; string projectPath="",savedSnapshot; Image background; Stack<string> undo=new Stack<string>(),redo=new Stack<string>();
 Color panel=Ui.Paper,ink=Ui.Ink,accent=Ui.Accent;
 JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=16000000};
 public MainWindow(){InitializeModernUi();}

 void AddButton(Control parent,string caption,Action action){var b=new Button{Text=caption,AutoSize=true,Height=30,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=ink,Padding=new Padding(7,1,7,1),Margin=new Padding(0,0,7,5)};b.FlatAppearance.BorderColor=Color.FromArgb(64,79,96);b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"War Thunder Scope Maker",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};parent.Controls.Add(b);}
 public static Project Preset(int index){var p=new Project{Base=Compatibility.Universal+"drawCentralLineVert:b = no;\ndrawCentralLineHorz:b = no;\ndrawDistanceCorrection:b = no;\ncrosshair_distances {}\ncrosshair_hor_ranges {}\n",Title=index==0?L.T("Минимальный крест"):index==1?L.T("Кольцо и перекрестие"):L.T("Шкала с делениями")};p.Parts.Add(new Part{X=-0.55,X2=-0.025,Name=L.T("Горизонталь слева")});p.Parts.Add(new Part{X=0.025,X2=0.55,Name=L.T("Горизонталь справа")});p.Parts.Add(new Part{X=0,Y=-0.55,X2=0,Y2=-0.025,Name=L.T("Вертикаль сверху")});p.Parts.Add(new Part{X=0,Y=0.025,X2=0,Y2=0.55,Name=L.T("Вертикаль снизу")});if(index==1)p.Parts.Add(new Part{Kind="circle",Name=L.T("Центральное кольцо"),X=0,Y=0,Size=0.2});if(index==2)for(int i=1;i<=6;i++){double y=-i*0.07;p.Parts.Add(new Part{X=-0.025,Y=y,X2=0.025,Y2=y,Name=L.T("Деление ")+i});p.Parts.Add(new Part{Kind="text",Name=L.T("Метка ")+i,X=0.055,Y=y,Text=(i*4).ToString(),Size=0.6});}return p;}
 Part Selected {get{return layers.SelectedIndex>=0&&layers.SelectedIndex<Doc.Parts.Count?Doc.Parts[layers.SelectedIndex]:null;}}
 void SetDocument(Project p,string path){CancelDrawing();Doc=p;RestoreBackground();projectPath=path;dirty=false;undo.Clear();redo.Clear();RefreshLayers(0);UpdateProps();savedSnapshot=json.Serialize(Doc);dirty=false;Status(backgroundIssue.Length>0?backgroundIssue:L.T("Выберите инструмент слева и рисуйте мышкой. Shift — ровные линии / квадрат. Esc — выбор."));}
 void RefreshLayers(int selected){layers.BeginUpdate();layers.Items.Clear();foreach(var p in Doc.Parts)layers.Items.Add((p.Visible?"● ":"○ ")+L.T(p.Name));if(Doc.Parts.Count>0)layers.SelectedIndex=Math.Max(0,Math.Min(selected,Doc.Parts.Count-1));layers.EndUpdate();canvas.Invalidate();}
 void Status(string message){status.Text=L.T(message);Text="War Thunder Scope Maker — "+L.T(Doc.Title)+(dirty?" *":"")+" · PRE-ALPHA 0.6.3";UpdateModernHeader();}
 void Changed(){dirty=savedSnapshot==null||json.Serialize(Doc)!=savedSnapshot;canvas.Invalidate();Status(Doc.Parts.Count+L.T(" элементов · предпросмотр приблизительный · Ctrl+S сохранить · Ctrl+Z отменить"));}
 void Remember(){undo.Push(json.Serialize(Doc));redo.Clear();}
 void Undo(bool forward){var from=forward?redo:undo;var to=forward?undo:redo;if(from.Count==0)return;to.Push(json.Serialize(Doc));Doc=json.Deserialize<Project>(from.Pop());RestoreBackground();dirty=true;RefreshLayers(layers.SelectedIndex);UpdateProps();Changed();}
 void UpdateProps(){syncing=true;var p=Selected;foreach(var n in numbers.Values)n.Enabled=p!=null;string[] keys={"X","Y","X2","Y2","Size","Start","End"};if(p!=null){double[] vals={p.X,p.Y,p.X2,p.Y2,p.Size,p.Start,p.End};for(int i=0;i<keys.Length;i++)numbers[keys[i]].Value=(decimal)Math.Max(-1000000,Math.Min(1000000,vals[i]));text.Text=p.Text;visible.Checked=p.Visible;move.Checked=p.Move;mils.Checked=p.Mils;bool locked=p.Kind=="quad"&&!String.IsNullOrEmpty(p.Original);foreach(string k in keys)numbers[k].Enabled=!locked;numbers["X2"].Enabled=numbers["Y2"].Enabled=p.Kind=="line"||(p.Kind=="quad"&&!locked);numbers["Size"].Enabled=p.Kind=="circle"||p.Kind=="text"||p.Kind=="image";numbers["Start"].Enabled=numbers["End"].Enabled=p.Kind=="circle";mils.Enabled=!locked;move.Enabled=true;}text.Enabled=p!=null&&p.Kind=="text";visible.Enabled=p!=null;var w=(NumericUpDown)props.Controls["width"];w.Value=(decimal)Math.Max(0.1,Math.Min(20,Doc.LineWidth));syncing=false;RefreshNativeProps();LayoutInspector();}
 void PropertyChanged(object sender,EventArgs e){if(syncing||L.Applying||Selected==null)return;Remember();var p=Selected;p.X=(double)numbers["X"].Value;p.Y=(double)numbers["Y"].Value;p.X2=(double)numbers["X2"].Value;p.Y2=(double)numbers["Y2"].Value;p.Size=(double)numbers["Size"].Value;p.Start=(double)numbers["Start"].Value;p.End=(double)numbers["End"].Value;p.Text=text.Text;p.Visible=visible.Checked;p.Move=move.Checked;p.Mils=mils.Checked;Changed();}
 void AddPart(string kind){Remember();Doc.Parts.Add(new Part{Kind=kind,Name=kind=="line"?L.T("Линия"):kind=="circle"?L.T("Окружность"):L.T("Текст"),X=kind=="line"?-0.15:0,Size=kind=="circle"?0.25:1});RefreshLayers(Doc.Parts.Count-1);Changed();}
 void Duplicate(){if(Selected==null)return;int count=Doc.Parts.Sum(s=>s.Kind=="image"&&s.Tiles!=null?s.Tiles.Count:1),added=Selected.Kind=="image"&&Selected.Tiles!=null?Selected.Tiles.Count:1;if(count+added>20000)throw new FormatException(L.T("Лимит: 20 000 экспортируемых фигур."));Remember();var p=json.Deserialize<Part>(json.Serialize(Selected));p.Name+=L.T(" (копия)");Doc.Parts.Add(p);RefreshLayers(Doc.Parts.Count-1);Changed();}
 void Delete(){if(Selected==null)return;int i=layers.SelectedIndex;Remember();Doc.Parts.RemoveAt(i);RefreshLayers(i);UpdateProps();Changed();}
 bool ConfirmLoss(){if(dirty&&savedSnapshot!=null)dirty=json.Serialize(Doc)!=savedSnapshot;if(!dirty)return true;var r=MessageBox.Show(this,L.T("Сохранить изменения проекта?"),L.T("Несохранённый прицел"),MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);if(r==DialogResult.Cancel)return false;if(r==DialogResult.Yes)return SaveProject();return true;}
 bool SaveProject(){using(var d=new SaveFileDialog{Filter=L.T("Проект Scope Maker|*.wtsm"),FileName=String.IsNullOrEmpty(projectPath)?"MySight.wtsm":projectPath}){if(d.ShowDialog(this)!=DialogResult.OK)return false;SaveProjectTo(d.FileName);return true;}}
 void SaveProjectTo(string path){ValidateChildren();string snapshot=json.Serialize(Doc);File.WriteAllText(path,snapshot,new UTF8Encoding(false));projectPath=path;savedSnapshot=snapshot;dirty=false;Status(L.T("Проект сохранён: ")+Path.GetFileName(path));}
 void Validate(Project p){if(p==null||(p.Version!=1&&p.Version!=2)||p.Parts==null||p.Parts.Count>20000)throw new FormatException(L.T("Неподдерживаемый или слишком большой проект"));if(p.Parts.Any(x=>x==null||!new[]{"line","circle","text","quad","image"}.Contains(x.Kind)))throw new FormatException(L.T("Неизвестный элемент проекта"));if(p.Parts.Sum(x=>x.Kind=="image"&&x.Tiles!=null?x.Tiles.Count:1)>20000)throw new FormatException(L.T("Больше 20 000 экспортируемых фигур"));foreach(var s in p.Parts)if(s.Kind=="image"&&(s.Tiles==null||s.Tiles.Any(t=>t==null||t.Length!=4||t.Any(v=>Double.IsInfinity(v)||Double.IsNaN(v)))))throw new FormatException(L.T("Повреждённые данные рисунка"));Compatibility.Migrate(p);Blk.Parse(Blk.Export(p));}
 void OpenProject(){if(!ConfirmLoss())return;using(var d=new OpenFileDialog{Filter=L.T("Проект Scope Maker|*.wtsm")})if(d.ShowDialog(this)==DialogResult.OK){var p=json.Deserialize<Project>(File.ReadAllText(d.FileName));Validate(p);SetDocument(p,d.FileName);}}
 void ImportBlk(){if(!ConfirmLoss())return;using(var d=new OpenFileDialog{Filter=L.T("Прицел War Thunder|*.blk")})if(d.ShowDialog(this)==DialogResult.OK){var p=Blk.Import(File.ReadAllText(d.FileName));p.Title=Path.GetFileNameWithoutExtension(d.FileName);SetDocument(p,"");savedSnapshot=null;dirty=true;Status(L.T("Импортировано. Баллистика и дополнительные параметры сохранены; их предпросмотр пока не поддерживается."));}}
 void ExportBlk(){Validate(Doc);if(!Directory.Exists(Preferences.Current.ExportDirectory)&&SightFolders.Find().Count>1){string selected=SightFolders.Choose(this);if(selected==null)return;var next=Preferences.Current.Copy();next.ExportDirectory=selected;Preferences.Save(next);Preferences.Current=next;}using(var d=new SaveFileDialog{Filter=L.T("Прицел War Thunder|*.blk"),FileName="scope_maker.blk",InitialDirectory=Preferences.ExportStartDirectory(),RestoreDirectory=true})if(d.ShowDialog(this)==DialogResult.OK){string data=Blk.Export(Doc);Blk.Parse(data);File.WriteAllText(d.FileName,data,new UTF8Encoding(false));Status(L.T("Экспортирован ")+Path.GetFileName(d.FileName)+L.T(" · установите в UserSights/<vehicle_id> или all_tanks"));}}
 void SetBackground(){ChooseBackground();}
 void SetColor(){using(var d=new ColorDialog{Color=Color.FromArgb(Doc.Color),FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){Remember();Doc.Color=d.Color.ToArgb();Changed();}}
 void ImportImage(){using(var d=new OpenFileDialog{Filter=L.T("Рисунок|*.png;*.jpg;*.jpeg;*.bmp")})if(d.ShowDialog(this)==DialogResult.OK)using(var image=new Bitmap(d.FileName))using(var options=new ImageImportDialog(image)){if(options.ShowDialog(this)!=DialogResult.OK)return;int count=Doc.Parts.Sum(p=>p.Kind=="image"&&p.Tiles!=null?p.Tiles.Count:1);if(count+options.Imported.Tiles.Count>20000)throw new FormatException(L.T("Превышен лимит 20 000 экспортируемых фигур: уменьшите детализацию."));Remember();Doc.Parts.Add(options.Imported);RefreshLayers(Doc.Parts.Count-1);drawingTool.SelectedIndex=0;Changed();Status(L.T("Рисунок добавлен как один объект. Перемещайте мышкой; размер меняется справа."));}}
 float ViewZoom {get{return (float)zoom.Value/100f;}}
 float Scale(Rectangle r){return Math.Min(r.Width,r.Height)*0.44f*ViewZoom;}
 RectangleF BackgroundFrame(Rectangle viewport,Size image){float fit=Math.Min((float)viewport.Width/image.Width,(float)viewport.Height/image.Height)*ViewZoom;float width=image.Width*fit,height=image.Height*fit;return new RectangleF((viewport.Width-width)/2,(viewport.Height-height)/2,width,height);}
 void CanvasWheel(object sender,MouseEventArgs e){if(drawing||e.Delta==0)return;zoom.Value=Math.Max(zoom.Minimum,Math.Min(zoom.Maximum,zoom.Value+(e.Delta>0?10:-10)));}
 PointF Screen(double x,double y,bool mils,Rectangle r){double factor=mils?0.04:1;return new PointF(r.Width/2f+(float)(x*factor)*Scale(r),r.Height/2f+(float)(y*factor)*Scale(r));}
 public void Render(Graphics g,Rectangle r){
  var inkColor=Color.FromArgb(Doc.Color);bool darkInk=inkColor.GetBrightness()<0.25;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(darkInk&&background==null?Color.FromArgb(205,210,204):Color.FromArgb(17,23,30));if(background!=null){var frame=BackgroundFrame(r,background.Size);g.DrawImage(background,frame);if(Doc.BackgroundDim>0)using(var veil=new SolidBrush(Color.FromArgb(Math.Min(70,Doc.BackgroundDim)*255/100,0,0,0)))g.FillRectangle(veil,frame);}
  if(Doc.PreviewGrid)using(var grid=new Pen(Color.FromArgb(background==null?32:18,158,185,213))) {float step=Scale(r)*0.1f;if(step>3){for(float x=r.Width/2f%step;x<r.Width;x+=step)g.DrawLine(grid,x,0,x,r.Height);for(float y=r.Height/2f%step;y<r.Height;y+=step)g.DrawLine(grid,0,y,r.Width,y);}grid.Color=Color.FromArgb(75,158,185,213);g.DrawLine(grid,r.Width/2f,0,r.Width/2f,r.Height);g.DrawLine(grid,0,r.Height/2f,r.Width,r.Height/2f);}
  var color=Color.FromArgb(Doc.Color);using(var pen=new Pen(color,(float)Math.Max(0.2,Doc.LineWidth)))using(var brush=new SolidBrush(color)){
   RenderNativeBase(g,r,pen);foreach(var p in Doc.Parts.Concat(draft)){if(!p.Visible)continue;PointF a=Screen(p.X,p.Y,p.Mils,r),b=Screen(p.X2,p.Y2,p.Mils,r);pen.Width=(float)Math.Max(0.2,Math.Min(200,Doc.LineWidth*p.StrokeWidth));try{
    if(p.Kind=="line")g.DrawLine(pen,a,b);
    else if(p.Kind=="circle"){float d=(float)Math.Abs(p.Size*(p.Mils?0.04:1))*Scale(r);if(d>0&&d<100000){if(Math.Abs(p.End-p.Start)>=360)g.DrawEllipse(pen,a.X-d/2,a.Y-d/2,d,d);else g.DrawArc(pen,a.X-d/2,a.Y-d/2,d,d,(float)p.Start,(float)(p.End-p.Start));}}
    else if(p.Kind=="text"){using(var font=new Font("Segoe UI",Math.Max(1,Math.Min(200,(float)Math.Abs(p.Size)*12*(float)NativeFontScale())))){var size=g.MeasureString(p.Text??"",font);float x=p.Align==1?a.X:p.Align==2?a.X-size.Width:a.X-size.Width/2;g.DrawString(p.Text??"",font,brush,x,a.Y-size.Height/2);}}
    else if(p.Kind=="quad"){if(!String.IsNullOrEmpty(p.Original)){var n=Blk.Parse(p.Original)[0];var points=new List<PointF>();foreach(string k in new[]{"tl","tr","br","bl"}){var v=Blk.Values(n.Get(k,"0,0"),2);points.Add(Screen(v[0],v[1],p.Mils,r));}g.FillPolygon(brush,points.ToArray());}else g.FillRectangle(brush,Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));}
    else if(p.Kind=="image"&&p.Tiles!=null){var smoothing=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;foreach(var tile in p.Tiles){var tl=Screen(p.X+tile[0]*p.Size,p.Y+tile[1]*p.Size,p.Mils,r);var br=Screen(p.X+tile[2]*p.Size,p.Y+tile[3]*p.Size,p.Mils,r);g.FillRectangle(brush,Math.Min(tl.X,br.X),Math.Min(tl.Y,br.Y),Math.Abs(br.X-tl.X),Math.Abs(br.Y-tl.Y));}g.SmoothingMode=smoothing;}
   }catch(ArgumentException){} }
  }
  if(Selected!=null){var a=Screen(Selected.X,Selected.Y,Selected.Mils,r);using(var p=new Pen(Color.FromArgb(255,190,90),1)){g.DrawRectangle(p,a.X-6,a.Y-6,12,12);if(Selected.Kind=="line"){a=Screen(Selected.X2,Selected.Y2,Selected.Mils,r);g.DrawRectangle(p,a.X-4,a.Y-4,8,8);}}}
  if(HasNativeScales())using(var b=new SolidBrush(Color.FromArgb(145,154,169)))using(var f=new Font("Segoe UI",8.5f))g.DrawString(L.T("Предпросмотр без баллистических шкал"),f,b,16,r.Height-27);
 }
 void CanvasDown(object sender,MouseEventArgs e){canvas.Focus();if(e.Button!=MouseButtons.Left)return;if(BeginDrawing(e))return;int best=-1;double nearest=18;
  for(int i=Doc.Parts.Count-1;i>=0;i--){var p=Doc.Parts[i];if(!p.Visible)continue;var a=Screen(p.X,p.Y,p.Mils,canvas.ClientRectangle);var b=Screen(p.X2,p.Y2,p.Mils,canvas.ClientRectangle);double dist=Distance(a,e.Location);if(p.Kind=="line"){double dx=b.X-a.X,dy=b.Y-a.Y;double t=(dx*dx+dy*dy)==0?0:Math.Max(0,Math.Min(1,((e.X-a.X)*dx+(e.Y-a.Y)*dy)/(dx*dx+dy*dy)));dist=Distance(new PointF((float)(a.X+t*dx),(float)(a.Y+t*dy)),e.Location);}if(p.Kind=="circle")dist=Math.Min(dist,Math.Abs(dist-Math.Abs(p.Size*(p.Mils?0.04:1))*Scale(canvas.ClientRectangle)/2));if(p.Kind=="quad"&&e.X>=Math.Min(a.X,b.X)&&e.X<=Math.Max(a.X,b.X)&&e.Y>=Math.Min(a.Y,b.Y)&&e.Y<=Math.Max(a.Y,b.Y))dist=0;if(p.Kind=="image"&&p.Tiles!=null){foreach(var tile in p.Tiles){var tl=Screen(p.X+tile[0]*p.Size,p.Y+tile[1]*p.Size,p.Mils,canvas.ClientRectangle);var br=Screen(p.X+tile[2]*p.Size,p.Y+tile[3]*p.Size,p.Mils,canvas.ClientRectangle);if(e.X>=Math.Min(tl.X,br.X)-3&&e.X<=Math.Max(tl.X,br.X)+3&&e.Y>=Math.Min(tl.Y,br.Y)-3&&e.Y<=Math.Max(tl.Y,br.Y)+3){dist=0;break;}}}if(dist<nearest){best=i;nearest=dist;}}
  if(best>=0)layers.SelectedIndex=best;if(Selected==null||best<0)return;Remember();lastMouse=e.Location;dragging=true;canvas.Capture=true;canvas.Cursor=Cursors.SizeAll;
 }
 void CanvasMove(object sender,MouseEventArgs e){if(drawing){PreviewDrawing(e);return;}if(!dragging||Selected==null||e.Button!=MouseButtons.Left)return;var p=Selected;double factor=p.Mils?0.04:1;double dx=(e.X-lastMouse.X)/Scale(canvas.ClientRectangle)/factor,dy=(e.Y-lastMouse.Y)/Scale(canvas.ClientRectangle)/factor;TranslatePart(p,dx,dy);lastMouse=e.Location;UpdateProps();Changed();}
 void KeysDown(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){CancelDrawing();drawingTool.SelectedIndex=0;e.SuppressKeyPress=true;}if(drawing)return;if(e.Control&&e.KeyCode==Keys.S){SaveProject();e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.Z){Undo(false);e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.Y){Undo(true);e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Delete&&!(ActiveControl is TextBox)&&!(ActiveControl is NumericUpDown)){Delete();e.SuppressKeyPress=true;}}
 protected override void Dispose(bool disposing){if(disposing){if(background!=null)background.Dispose();tips.Dispose();}base.Dispose(disposing);}
 public void Screenshot(string path){Show();Application.DoEvents();using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(path);}dirty=false;Close();}
}
public static class Program {
 [STAThread] public static void Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Preferences.Initialize(args.Length>0&&args[0]=="--self-test");
  try{if(args.Length>1&&(args[0]=="--dark-en-screenshot"||args[0]=="--settings-en-screenshot")){Preferences.Current=new AppPreferences{Theme="dark",Language="en",Accent="#14B8A6"};Ui.Configure(Preferences.Current);L.ApplyCulture();}if(args.Length>1&&(args[0]=="--settings-screenshot"||args[0]=="--settings-en-screenshot")){using(var dialog=new PreferencesDialog(Preferences.Current))dialog.Screenshot(args[1]);return;}if(args.Length>1&&args[0]=="--write-branding"){Branding.Export(args[1]);return;}if(args.Length>1&&args[0]=="--background-dialog-screenshot"){using(var d=new BackgroundDialog(new Project{BackgroundKey="1_day"}))d.Screenshot(args[1]);return;}if(args.Length>2&&args[0]=="--audit-folder"){Compatibility.Audit(args[1],args[2]);return;}if(args.Length>0&&args[0]=="--self-test"){Tests(args.Length>1?args[1]:"test-results.txt");return;}if(args.Length>1&&args[0]=="--import-screenshot"){using(var input=FeatureTests.Fixture())using(var d=new ImageImportDialog(input))d.Screenshot(args[1]);return;}using(var window=new MainWindow()){if(args.Length>1&&(args[0]=="--background-screenshot"||args[0]=="--dark-en-screenshot")){window.BackgroundScreenshot(args[1]);return;}if(args.Length>2&&args[0]=="--template-screenshot"){window.LoadTemplateForScreenshot(args[1],args[2]);return;}if(args.Length>1&&args[0]=="--screenshot"){window.Screenshot(args[1]);return;}Application.Run(window);}}
  catch(Exception ex){if(args.Length>0){File.WriteAllText("test-error.txt",ex.ToString());Environment.ExitCode=1;}else MessageBox.Show(ex.ToString(),L.T("Scope Maker — ошибка"));}
 }
 static void Check(bool condition,string label,List<string> results){if(!condition)throw new Exception("FAIL: "+label);results.Add("PASS: "+label);}
 static void Tests(string path){var r=new List<string>();
  foreach(int index in new[]{0,1,2}){var p=MainWindow.Preset(index);var q=Blk.Import(Blk.Export(p));Check(q.Parts.Count==p.Parts.Count,"Template "+index+" BLK round trip",r);}
  string fixture="// comment\ncustom:r=2; ballistics { bullet { speed:r=1430; bulletType:t=\"apds\"; } } drawLines { line { line:p4=-1,0,1,0; move:b=yes; radialAngle:r=35; } } drawTexts { text { text:t=\"a // b { ~\"quoted~\"\"; pos:p2=0.1,0.2; align:i=2; } }";
  var imported=Blk.Import(fixture);var exported=Blk.Export(imported);Check(imported.Parts.Count==2,"Inline blocks, comments and escaped text",r);Check(exported.Contains("radialAngle:r = 35")&&exported.Contains("bulletType:t")&&exported.Contains("align:i = 2"),"Preserve unedited fields and ballistics",r);Check(Blk.Import(exported).Parts[1].Text==imported.Parts[1].Text,"Text escaping round trip",r);
  imported.Parts[0].Visible=false;Check(Blk.Import(Blk.Export(imported)).Parts.Count==1,"Hidden elements omitted from export",r);
  bool rejected=false;try{Blk.Parse("drawLines { line { line:p4=0,0,1,1; }");}catch(FormatException){rejected=true;}Check(rejected,"Reject malformed BLK",r);
  FeatureTests.Run(r);Compatibility.TestLibrary(r);BackgroundLibrary.Tests(r);MainWindow.ZoomChecks(r);MainWindow.ModernChecks(r);MainWindow.PreferencesChecks(r);MainWindow.WorkflowChecks(r);
  var ser=new JavaScriptSerializer();var original=MainWindow.Preset(2);Check(ser.Deserialize<Project>(ser.Serialize(original)).Parts.Count==original.Parts.Count,"Project persistence",r);
  File.WriteAllLines(path,r.ToArray());
 }
}
}
