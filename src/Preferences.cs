using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScopeMaker {
public class AppPreferences {
 public int Version=1;
 public string Theme="light", Accent="#5269EB", Language="ru", ExportDirectory="";
 public AppPreferences Copy(){return new AppPreferences{Theme=Theme,Accent=Accent,Language=Language,ExportDirectory=ExportDirectory};}
 public void Validate(){if(Theme!="light"&&Theme!="dark")throw new FormatException(L.T("Неизвестная тема"));if(Language!="ru"&&Language!="en")throw new FormatException(L.T("Неизвестный язык"));if(!System.Text.RegularExpressions.Regex.IsMatch(Accent??"","^#[0-9A-Fa-f]{6}$"))throw new FormatException(L.T("Некорректный цвет акцента"));if(!String.IsNullOrWhiteSpace(ExportDirectory)){if(!Path.IsPathRooted(ExportDirectory))throw new FormatException(L.T("Выберите полный путь к папке экспорта"));Path.GetFullPath(ExportDirectory);}else ExportDirectory="";Accent=Accent.ToUpperInvariant();}
}
public static class Preferences {
 public static AppPreferences Current=new AppPreferences();
 public static string PathOverride;
 public static string FilePath {get{return PathOverride??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");}}
 public static void Initialize(bool tests){Current=tests?new AppPreferences():Read(FilePath);Ui.Configure(Current);L.ApplyCulture();}
 public static AppPreferences Read(string path){try{if(!File.Exists(path))return new AppPreferences();var p=new JavaScriptSerializer().Deserialize<AppPreferences>(File.ReadAllText(path));if(p==null||p.Version!=1)return new AppPreferences();p.Validate();return p;}catch{return new AppPreferences();}}
 public static void Save(AppPreferences settings,string path=null){settings.Validate();path=path??FilePath;string temporary=path+".tmp";try{File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(settings),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 public static string ExportStartDirectory(){string path=Current.ExportDirectory;if(!String.IsNullOrEmpty(path)&&Directory.Exists(path))return path;var found=SightFolders.Find();return found.Count==1?SightFolders.ExportTarget(found[0]):Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);}
}
public class Palette {
 public Color Paper,Surface,Input,Ink,Muted,Line,Accent,Selection,Hover,Disabled,OnAccent;
 public bool Dark;
 static Color Mix(Color a,Color b,double amount){return Color.FromArgb((int)(a.R*(1-amount)+b.R*amount),(int)(a.G*(1-amount)+b.G*amount),(int)(a.B*(1-amount)+b.B*amount));}
 static double Linear(int value){double v=value/255.0;return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4);}
 public static string Hex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
 public static double Luminance(Color c){return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
 public static Palette Create(AppPreferences p){bool dark=p.Theme=="dark";var accent=ColorTranslator.FromHtml(p.Accent);var palette=new Palette{Dark=dark,Paper=dark?Color.FromArgb(27,31,39):Color.White,Surface=dark?Color.FromArgb(19,23,30):Color.FromArgb(246,247,249),Input=dark?Color.FromArgb(37,43,54):Color.FromArgb(243,245,248),Ink=dark?Color.FromArgb(232,237,246):Color.FromArgb(34,42,56),Muted=dark?Color.FromArgb(156,168,187):Color.FromArgb(119,129,146),Line=dark?Color.FromArgb(55,63,77):Color.FromArgb(227,231,237),Accent=accent,Disabled=dark?Color.FromArgb(102,113,132):Color.FromArgb(177,184,196)};palette.Selection=Mix(palette.Paper,accent,dark?.2:.12);palette.Hover=Mix(palette.Paper,palette.Ink,dark?.08:.035);palette.OnAccent=Luminance(accent)>.179?Color.FromArgb(16,21,31):Color.White;return palette;}
}
public class PreferencesDialog : Form {
 ComboBox theme=new ModernCombo(),language=new ModernCombo();TextBox folder=new TextBox();Label hex=new Label(),note=new Label();Panel sample=new Panel();Color accent;AppPreferences draft;
 public AppPreferences Result;
 public PreferencesDialog(AppPreferences current){
  draft=current.Copy();accent=ColorTranslator.FromHtml(draft.Accent);Text=L.T("Настройки");ClientSize=new Size(620,590);MinimumSize=new Size(620,590);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);ShowIcon=false;BackColor=Ui.Paper;ForeColor=Ui.Ink;
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26,20,26,20),ColumnCount=1,RowCount=7};foreach(int h in new[]{52,76,76,105,105,75})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
  var heading=new Label{Text=L.T("Настройки приложения"),Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true};root.Controls.Add(heading,0,0);
  root.Controls.Add(Field(L.T("Тема оформления"),theme),0,1);theme.Items.AddRange(new object[]{L.T("Светлая"),L.T("Тёмная")});theme.SelectedIndex=draft.Theme=="dark"?1:0;
  root.Controls.Add(Field(L.T("Язык приложения"),language),0,2);language.Items.AddRange(new object[]{L.T("Русский"),"English"});language.SelectedIndex=draft.Language=="en"?1:0;
  var colors=new Panel{Dock=DockStyle.Fill};colors.Controls.Add(new Label{Text=L.T("Акцентный цвет"),AutoSize=true,ForeColor=Ui.Muted});var swatches=new FlowLayoutPanel{Location=new Point(0,28),Size=new Size(566,50),WrapContents=false};colors.Controls.Add(swatches);
  foreach(string value in new[]{"#5269EB","#3B82F6","#14B8A6","#22C55E","#F59E0B","#EF4444","#A855F7"}){string chosen=value;var b=new Button{Size=new Size(28,28),BackColor=ColorTranslator.FromHtml(value),FlatStyle=FlatStyle.Flat,Margin=new Padding(0,3,9,0),AccessibleName=value,Tag="accent-swatch"};b.FlatAppearance.BorderSize=0;b.Click+=delegate{accent=ColorTranslator.FromHtml(chosen);RefreshSample();};swatches.Controls.Add(b);}
  var custom=new ModernButton{Text=L.T("Свой цвет…"),Width=133};custom.Click+=delegate{using(var d=new ColorDialog{Color=accent,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){accent=d.Color;RefreshSample();}};swatches.Controls.Add(custom);hex.AutoSize=true;hex.Margin=new Padding(10,10,0,0);hex.ForeColor=Ui.Muted;swatches.Controls.Add(hex);root.Controls.Add(colors,0,3);
  var export=new Panel{Dock=DockStyle.Fill};export.Controls.Add(new Label{Text=L.T("Папка экспорта по умолчанию"),AutoSize=true,ForeColor=Ui.Muted});folder.Text=draft.ExportDirectory;folder.Location=new Point(0,27);folder.Width=398;folder.BorderStyle=BorderStyle.FixedSingle;folder.BackColor=Ui.Input;folder.ForeColor=Ui.Ink;folder.AccessibleName=L.T("Папка экспорта");export.Controls.Add(folder);var browse=new ModernButton{Text=L.T("Выбрать…"),Location=new Point(410,23),Width=110};browse.Click+=delegate{using(var d=new FolderBrowserDialog{Description=L.T("Выберите папку экспорта"),SelectedPath=Directory.Exists(folder.Text)?folder.Text:"",ShowNewFolderButton=true})if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;};export.Controls.Add(browse);var reset=new LinkLabel{Text=L.T("Сбросить папку"),Location=new Point(0,62),AutoSize=true,LinkColor=Ui.Accent};reset.LinkClicked+=delegate{folder.Text="";};export.Controls.Add(reset);var detect=new ModernButton{Text=L.T("Найти папку WT"),Location=new Point(270,58),Width=250};detect.Click+=delegate{string found=SightFolders.Choose(this);if(found!=null)folder.Text=found;};export.Controls.Add(detect);root.Controls.Add(export,0,4);
  sample.Dock=DockStyle.Fill;sample.Paint+=delegate(object s,PaintEventArgs e){bool dark=theme.SelectedIndex==1;Color bg=dark?Color.FromArgb(27,31,39):Color.FromArgb(246,247,249);e.Graphics.Clear(bg);using(var b=new SolidBrush(accent))using(var shape=Ui.Round(new RectangleF(16,13,130,36),7))e.Graphics.FillPath(b,shape);var on=Palette.Create(new AppPreferences{Accent=Palette.Hex(accent)}).OnAccent;TextRenderer.DrawText(e.Graphics,L.T("Экспорт BLK"),Font,new Rectangle(16,13,130,36),on,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(e.Graphics,L.T("Предпросмотр оформления"),Font,new Rectangle(165,13,360,36),dark?Color.White:Ui.Ink,TextFormatFlags.VerticalCenter);};root.Controls.Add(sample,0,5);theme.SelectedIndexChanged+=delegate{sample.Invalidate();};
  var bottom=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=new ModernButton{Text=L.T("Применить"),Primary=true,Width=116};var cancel=new ModernButton{Text=L.T("Отмена"),Width=100,DialogResult=DialogResult.Cancel};save.Click+=delegate{try{var p=new AppPreferences{Theme=theme.SelectedIndex==1?"dark":"light",Language=language.SelectedIndex==1?"en":"ru",Accent=Palette.Hex(accent),ExportDirectory=folder.Text.Trim()};p.Validate();Result=p;DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,L.T(ex.Message),L.T("Настройки"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}};bottom.Controls.Add(save);bottom.Controls.Add(cancel);root.Controls.Add(bottom,0,6);CancelButton=cancel;
  Ui.Theme(this);RefreshSample();
 }
 Panel Field(string caption,Control input){var panel=new Panel{Dock=DockStyle.Fill};panel.Controls.Add(new Label{Text=caption,AutoSize=true,ForeColor=Ui.Muted});input.Location=new Point(0,26);input.Width=520;panel.Controls.Add(input);return panel;}
 void RefreshSample(){hex.Text=Palette.Hex(accent);sample.Invalidate();}
 public void Screenshot(string path){Show();Application.DoEvents();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(path);}Close();}
}
public partial class MainWindow {
 void ShowPreferences(){using(var d=new PreferencesDialog(Preferences.Current))if(d.ShowDialog(this)==DialogResult.OK){Preferences.Save(d.Result);ApplyPreferences(d.Result);}}
 void ApplyPreferences(AppPreferences next){var previous=Ui.Colors;Preferences.Current=next.Copy();Ui.Configure(next);L.ApplyCulture();bool was=syncing;syncing=true;Ui.Recolor(this,previous);L.Localize(this);L.Tooltips(tips,this);foreach(var number in Descendants(this).OfType<ModernNumber>())number.RefreshFormatting();syncing=was;panel=Ui.Paper;ink=Ui.Ink;accent=Ui.Accent;RefreshLayers(layers.SelectedIndex);UpdateProps();FilterTemplates();UpdateModernHeader();foreach(var b in toolButtons){b.Invalidate();}Invalidate(true);Status(L.T("Настройки применены"));}
}
}
