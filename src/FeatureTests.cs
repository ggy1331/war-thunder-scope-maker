using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScopeMaker {
public static class FeatureTests {
 static void Check(bool ok,string name,List<string> r){if(!ok)throw new Exception("FAIL: "+name);r.Add("PASS: "+name);}
 public static Bitmap Fixture(){var b=new Bitmap(640,480);using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=new Pen(Color.Black,2)){g.DrawEllipse(p,150,70,340,340);g.DrawLine(p,70,240,270,240);g.DrawLine(p,370,240,570,240);g.DrawLine(p,320,20,320,190);g.DrawLine(p,320,290,320,460);for(int i=1;i<=7;i++){g.DrawLine(p,320+i*28,231,320+i*28,249);g.DrawLine(p,320-i*28,231,320-i*28,249);}}using(var f=new Font("Segoe UI",12)){g.DrawString("4     8     12     16",f,Brushes.Black,355,260);}}return b;}
 public static void Run(List<string> r){
  using(var b=new Bitmap(8,8)){using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.FillRectangle(Brushes.Black,2,2,4,4);}using(var result=ImageConverter.Convert(b,new ImageSettings())){Check(result.Tiles.Count==1,"Merge both horizontal and vertical image runs",r);Check(result.Width==4&&result.Height==4,"Trim empty image margins",r);}}
  using(var b=new Bitmap(32,32)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Black);g.FillRectangle(Brushes.White,8,8,16,16);}using(var result=ImageConverter.Convert(b,new ImageSettings()))Check(result.Tiles.Count==1&&result.Detected=="светлый рисунок","Auto-detect light drawing on dark background",r);}
  using(var b=new Bitmap(16,16)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.White,4,4,8,8);}using(var result=ImageConverter.Convert(b,new ImageSettings()))Check(result.Tiles.Count==1&&result.Detected=="по прозрачности","Preserve white artwork on transparent background",r);}
  using(var b=new Bitmap(512,512)){using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.FillRectangle(Brushes.Black,256,0,1,512);}using(var result=ImageConverter.Convert(b,new ImageSettings{Resolution=512,Trim=false}))Check(result.Width==512&&result.Tiles.Count==1&&Math.Abs(result.Tiles[0][2]-result.Tiles[0][0]-1.0/512)<0.000001,"Preserve a single-pixel 512px line",r);}
  using(var b=new Bitmap(8,8)){using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.FillRectangle(Brushes.Gray,1,1,3,3);g.FillRectangle(Brushes.Black,4,4,2,2);}using(var low=ImageConverter.Convert(b,new ImageSettings{Automatic=false,Threshold=50,Trim=false}))using(var high=ImageConverter.Convert(b,new ImageSettings{Automatic=false,Threshold=200,Trim=false}))Check(low.Tiles.Count==2&&high.Tiles.Count==1,"Manual threshold controls faint strokes",r);}
  using(var b=new Bitmap(12,12)){using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.FillRectangle(Brushes.Black,3,3,6,6);g.FillRectangle(Brushes.White,5,5,2,2);g.FillRectangle(Brushes.Black,0,0,1,1);}using(var result=ImageConverter.Convert(b,new ImageSettings{Clean=true,Trim=false})){Check(result.Preview.GetPixel(0,0).R==17&&result.Preview.GetPixel(5,5).R==17&&result.Preview.GetPixel(3,3).R==92,"Noise cleanup preserves enclosed holes",r);}}
  using(var b=new Bitmap(10,10)){using(var g=Graphics.FromImage(b))g.Clear(Color.White);using(var result=ImageConverter.Convert(b,new ImageSettings()))Check(result.Tiles.Count==0,"Blank images produce no geometry",r);}
  using(var source=Fixture())using(var result=ImageConverter.Convert(source,new ImageSettings{Resolution=768,Trim=false})){
   var image=new Part{Kind="image",Name="Тестовый рисунок",Tiles=result.Tiles,X=0.2,Y=-0.1,Size=0.8};var project=MainWindow.Preset(0);project.Parts=new List<Part>{image};var exported=Blk.Import(Blk.Export(project));Check(exported.Parts.Count==result.Tiles.Count,"Grouped image expands into BLK quadrilaterals",r);
   var first=exported.Parts[0];Check(Math.Abs(first.X-(0.2+result.Tiles[0][0]*0.8))<0.000002,"Image position and scale applied on export",r);var json=new JavaScriptSerializer{MaxJsonLength=16000000};var restored=json.Deserialize<Project>(json.Serialize(project));Check(restored.Parts[0].Tiles.Count==image.Tiles.Count,"Image grouping survives project persistence",r);
  }
  var a=new PointF(-0.2f,0.3f);var z=new PointF(0.4f,-0.1f);
  Check(MainWindow.MakeDrawing(1,a,z,true,null)[0].Y==a.Y,"Shift constrains freehand line to an axis",r);
  var rect=MainWindow.MakeDrawing(3,a,z,true,null);Check(rect.Count==4&&Math.Abs(Math.Abs(rect[0].X2-rect[0].X)-Math.Abs(rect[1].Y2-rect[1].Y))<0.000001,"Shift produces a square with closed outline",r);
  var circle=MainWindow.MakeDrawing(2,a,z,false,null)[0];Check(circle.Size>0&&circle.Kind=="circle","Drawing a circle from its bounding box",r);
  var triangle=MainWindow.MakeDrawing(5,a,z,false,null);var triangleProject=MainWindow.Preset(0);triangleProject.Parts=triangle;Check(Blk.Import(Blk.Export(triangleProject)).Parts[0].Kind=="quad","Hand-drawn triangle exports as filled geometry",r);
  Check(MainWindow.MakeDrawing(6,a,z,false,new[]{a,new PointF(0,0),z}).Count==2,"Pencil creates connected line segments",r);
  MainWindow.DrawingChecks(r);
 }
}
public partial class MainWindow {
 public static void DrawingChecks(List<string> r){using(var w=new MainWindow()){w.Show();Application.DoEvents();int before=w.Doc.Parts.Count;w.drawingTool.SelectedIndex=4;w.CanvasDown(w.canvas,new MouseEventArgs(MouseButtons.Left,1,100,100,0));w.CanvasMove(w.canvas,new MouseEventArgs(MouseButtons.Left,0,200,180,0));w.CanvasUp(w.canvas,new MouseEventArgs(MouseButtons.Left,1,200,180,0));if(w.Doc.Parts.Count!=before+1)throw new Exception("Drawing gesture did not create shape");r.Add("PASS: Canvas mouse gesture commits a filled rectangle");w.Undo(false);if(w.Doc.Parts.Count!=before)throw new Exception("Drawing undo failed");w.Undo(true);if(w.Doc.Parts.Count!=before+1)throw new Exception("Drawing redo failed");r.Add("PASS: Drawing gesture supports undo and redo");w.CanvasDown(w.canvas,new MouseEventArgs(MouseButtons.Left,1,100,100,0));w.CanvasMove(w.canvas,new MouseEventArgs(MouseButtons.Left,0,200,180,0));w.CancelDrawing();if(w.Doc.Parts.Count!=before+1||w.draft.Count!=0)throw new Exception("Cancelled gesture leaked geometry");r.Add("PASS: Cancelled gesture does not alter the project");w.dirty=false;w.Close();}}
}
}
