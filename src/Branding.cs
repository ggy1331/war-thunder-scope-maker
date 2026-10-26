using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ScopeMaker {
public static class Branding {
 static Image emblem;
 static Image Emblem {get{if(emblem==null){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","scope-maker-emblem.png");if(File.Exists(path))using(var source=Image.FromFile(path))emblem=new Bitmap(source);}return emblem;}}
 public static void Draw(Graphics g,RectangleF target,Color accent){var state=g.Save();g.TranslateTransform(target.X,target.Y);g.ScaleTransform(target.Width/512,target.Height/512);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;using(var tile=Ui.Round(new RectangleF(0,0,512,512),64))using(var fill=new SolidBrush(Color.FromArgb(16,16,23)))g.FillPath(fill,tile);DrawMark(g,accent);using(var f=new Font("Segoe UI",43,FontStyle.Bold,GraphicsUnit.Pixel))using(var fill=new SolidBrush(Color.White))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("WT Scope Maker",f,fill,new RectangleF(8,436,496,64),format);g.Restore(state);}
 static void DrawMark(Graphics g,Color accent){using(var pen=new Pen(accent,9)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawEllipse(pen,72,36,368,368);g.DrawLine(pen,256,12,256,60);g.DrawLine(pen,256,380,256,428);g.DrawLine(pen,48,220,100,220);g.DrawLine(pen,412,220,464,220);}DrawEmblem(g,new RectangleF(136,56,240,328));}
 static void DrawEmblem(Graphics g,RectangleF bounds){if(Emblem!=null)g.DrawImage(Emblem,bounds);}
 public static void DrawHeader(Graphics g,Rectangle bounds){var state=g.Save();float height=bounds.Height-17;float scale=height/432;g.TranslateTransform(bounds.X+(bounds.Width-512*scale)/2,bounds.Y);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;DrawMark(g,Ui.Accent);g.Restore(state);using(var f=new Font("Segoe UI",10,FontStyle.Bold))using(var brush=new SolidBrush(Ui.Ink))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("WT Scope Maker",f,brush,new RectangleF(bounds.X,bounds.Bottom-17,bounds.Width,17),format);}
 public static Bitmap Render(int size,Color accent){var image=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);Draw(g,new RectangleF(0,0,size,size),accent);}return image;}
 public static void Export(string directory){Directory.CreateDirectory(directory);using(var image=Render(1024,Color.FromArgb(82,105,235)))image.Save(Path.Combine(directory,"scope-maker-logo.png"),ImageFormat.Png);var frames=new List<byte[]>();int[] sizes={16,24,32,48,64,128,256};foreach(int size in sizes)using(var image=Render(size,Color.FromArgb(82,105,235)))using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);frames.Add(stream.ToArray());}using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,"scope-maker.ico")))){writer.Write((short)0);writer.Write((short)1);writer.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)1);writer.Write((short)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}foreach(var frame in frames)writer.Write(frame);}
  string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","scope-maker-emblem.png");string svg="<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 512 512\"><rect width=\"512\" height=\"512\" rx=\"64\" fill=\"#101017\"/><g fill=\"none\" stroke=\"#5269EB\" stroke-width=\"9\" stroke-linecap=\"round\"><circle cx=\"256\" cy=\"220\" r=\"184\"/><path d=\"M256 12v48 M256 380v48 M48 220h52 M412 220h52\"/></g><image x=\"136\" y=\"56\" width=\"240\" height=\"328\" xlink:href=\"data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(source))+"\"/><text x=\"256\" y=\"481\" text-anchor=\"middle\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"43\" font-weight=\"700\" fill=\"white\">WT Scope Maker</text></svg>";File.WriteAllText(Path.Combine(directory,"scope-maker-logo.svg"),svg,new UTF8Encoding(false));
 }
 public static void SetWindowIcon(Form form){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","scope-maker.ico");if(File.Exists(path)){form.Icon=new Icon(path);form.ShowIcon=true;}}
}
public class UiMenuColors : ProfessionalColorTable {
 public override Color ToolStripDropDownBackground {get{return Ui.Paper;}} public override Color ImageMarginGradientBegin {get{return Ui.Paper;}} public override Color ImageMarginGradientMiddle {get{return Ui.Paper;}} public override Color ImageMarginGradientEnd {get{return Ui.Paper;}} public override Color MenuBorder {get{return Ui.Line;}} public override Color MenuItemBorder {get{return Ui.Line;}} public override Color MenuItemSelected {get{return Ui.Selection;}}
}
}
