using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScopeMaker {
public static class EraserGeometry {
 struct Vertex {public double X,Y;public Vertex(double x,double y){X=x;Y=y;}}
 static List<Vertex> ClipPolygon(List<Vertex> polygon,Func<Vertex,double> distance){var output=new List<Vertex>();if(polygon.Count==0)return output;var previous=polygon[polygon.Count-1];double pd=distance(previous);foreach(var current in polygon){double cd=distance(current);if((pd>=0)!=(cd>=0)){double ratio=pd/(pd-cd);output.Add(new Vertex(previous.X+(current.X-previous.X)*ratio,previous.Y+(current.Y-previous.Y)*ratio));}if(cd>=0)output.Add(current);previous=current;pd=cd;}return output;}
 static double Area(List<Vertex> polygon){double area=0;for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=a.X*b.Y-b.X*a.Y;}return Math.Abs(area)/2;}
 public static List<Part> CutQuad(Part p,double l,double t,double r,double b){var node=Blk.Parse(p.Original)[0];var polygon=new List<Vertex>();foreach(string key in new[]{"tl","tr","br","bl"}){var v=Blk.Values(node.Get(key,"0,0"),2);polygon.Add(new Vertex(v[0],v[1]));}var outside=new List<List<Vertex>>();Func<Vertex,double>[] edges={v=>v.X-l,v=>r-v.X,v=>v.Y-t,v=>b-v.Y};foreach(var edge in edges){var rejected=ClipPolygon(polygon,v=>-edge(v));if(Area(rejected)>1e-16)outside.Add(rejected);polygon=ClipPolygon(polygon,edge);}if(Area(polygon)<1e-16)return new List<Part>{p};var output=new List<Part>();foreach(var piece in outside){var polygons=new List<List<Vertex>>();if(piece.Count<=4)polygons.Add(piece);else for(int i=1;i<piece.Count-1;i++)polygons.Add(new List<Vertex>{piece[0],piece[i],piece[i+1]});foreach(var vertices in polygons){if(Area(vertices)<1e-16)continue;var copy=Copy(p);var n=Blk.Parse(p.Original)[0];string[] names={"tl","tr","br","bl"};for(int i=0;i<4;i++){var v=vertices[Math.Min(i,vertices.Count-1)];n.Set(names[i],"p2",Blk.Num(v.X)+", "+Blk.Num(v.Y));}copy.Original=n.ToString();copy.X=vertices[0].X;copy.Y=vertices[0].Y;copy.X2=vertices[2].X;copy.Y2=vertices[2].Y;output.Add(copy);}}return output;}
 public static Part Copy(Part p){return new Part{Kind=p.Kind,Name=p.Name,Text=p.Text,Original=p.Original,X=p.X,Y=p.Y,X2=p.X2,Y2=p.Y2,Size=p.Size,Start=p.Start,End=p.End,Visible=p.Visible,Move=p.Move,Mils=p.Mils,StrokeWidth=p.StrokeWidth,Align=p.Align,Highlight=p.Highlight,Tiles=p.Tiles};}
 public static List<double[]> Subtract(double[] tile,double l,double t,double r,double b){
  double x=Math.Max(tile[0],l),y=Math.Max(tile[1],t),xx=Math.Min(tile[2],r),yy=Math.Min(tile[3],b);
  if(x>=xx||y>=yy)return new List<double[]>{tile};var output=new List<double[]>();
  if(tile[1]<y)output.Add(new[]{tile[0],tile[1],tile[2],y});if(yy<tile[3])output.Add(new[]{tile[0],yy,tile[2],tile[3]});if(tile[0]<x)output.Add(new[]{tile[0],y,x,yy});if(xx<tile[2])output.Add(new[]{xx,y,tile[2],yy});return output;
 }
 static bool Clip(double p,double q,ref double lo,ref double hi){if(Math.Abs(p)<1e-15)return q>=0;double k=q/p;if(p<0){if(k>hi)return false;lo=Math.Max(lo,k);}else{if(k<lo)return false;hi=Math.Min(hi,k);}return true;}
 public static List<Part> CutLine(Part p,double l,double t,double r,double b){
  double dx=p.X2-p.X,dy=p.Y2-p.Y,lo=0,hi=1;
  if(!Clip(-dx,p.X-l,ref lo,ref hi)||!Clip(dx,r-p.X,ref lo,ref hi)||!Clip(-dy,p.Y-t,ref lo,ref hi)||!Clip(dy,b-p.Y,ref lo,ref hi)||hi-lo<1e-12)return new List<Part>{p};
  var output=new List<Part>();if(lo>1e-10){var left=Copy(p);left.X2=p.X+lo*dx;left.Y2=p.Y+lo*dy;output.Add(left);}if(hi<1-1e-10){var right=Copy(p);right.X=p.X+hi*dx;right.Y=p.Y+hi*dy;output.Add(right);}return output;
 }
 public static List<Part> CutCircle(Part p,double l,double t,double r,double b){double radius=Math.Abs(p.Size)/2;if(radius<1e-12)return new List<Part>{p};double start=p.Start%360,sweep=p.End-p.Start;if(Math.Abs(sweep)>=360)sweep=sweep<0?-360:360;if(Math.Abs(sweep)<1e-12)return new List<Part>{p};var cuts=new List<double>{0,1};Action<double> add=delegate(double angle){for(int k=-3;k<=3;k++){double ratio=(angle+360*k-start)/sweep;if(ratio>0&&ratio<1)cuts.Add(ratio);}};
  foreach(double x in new[]{l,r}){double v=(x-p.X)/radius;if(v>=-1&&v<=1){double a=Math.Acos(v)*180/Math.PI;add(a);add(-a);}}foreach(double y in new[]{t,b}){double v=(y-p.Y)/radius;if(v>=-1&&v<=1){double a=Math.Asin(v)*180/Math.PI;add(a);add(180-a);}}
  cuts.Sort();var output=new List<Part>();bool removed=false;for(int i=1;i<cuts.Count;i++){double lo=cuts[i-1],hi=cuts[i];if(hi-lo<1e-10)continue;double angle=(start+sweep*(lo+hi)/2)*Math.PI/180,x=p.X+radius*Math.Cos(angle),y=p.Y+radius*Math.Sin(angle);if(x>=l&&x<=r&&y>=t&&y<=b){removed=true;continue;}var copy=Copy(p);copy.Start=start+sweep*lo;copy.End=start+sweep*hi;output.Add(copy);}return removed?output:new List<Part>{p};
 }
 public static List<Part> Apply(IList<Part> parts,double x,double y,double radius,double viewScale,double lineWidth,out bool changed){
  changed=false;var output=new List<Part>();
  foreach(var p in parts){if(!p.Visible){output.Add(p);continue;}double factor=p.Mils?.04:1,cx=x/factor,cy=y/factor,rad=radius/factor;
   if(p.Kind=="line"){double padding=Math.Max(.2,lineWidth*p.StrokeWidth)/2/viewScale/factor;var cut=CutLine(p,cx-rad-padding,cy-rad-padding,cx+rad+padding,cy+rad+padding);if(cut.Count!=1||!Object.ReferenceEquals(cut[0],p))changed=true;output.AddRange(cut);}
   else if(p.Kind=="circle"){double padding=Math.Max(.2,lineWidth*p.StrokeWidth)/2/viewScale/factor;var cut=CutCircle(p,cx-rad-padding,cy-rad-padding,cx+rad+padding,cy+rad+padding);if(cut.Count!=1||!Object.ReferenceEquals(cut[0],p))changed=true;output.AddRange(cut);}
   else if(p.Kind=="image"&&p.Tiles!=null&&Math.Abs(p.Size)>1e-12){double l=(cx-rad-p.X)/p.Size,r=(cx+rad-p.X)/p.Size,t=(cy-rad-p.Y)/p.Size,b=(cy+rad-p.Y)/p.Size;var tiles=new List<double[]>();bool touched=false;foreach(var tile in p.Tiles){if(tile[2]<=Math.Min(l,r)||tile[0]>=Math.Max(l,r)||tile[3]<=Math.Min(t,b)||tile[1]>=Math.Max(t,b)){tiles.Add(tile);continue;}var cut=Subtract(tile,Math.Min(l,r),Math.Min(t,b),Math.Max(l,r),Math.Max(t,b));if(cut.Count!=1||!Object.ReferenceEquals(cut[0],tile))touched=true;tiles.AddRange(cut);}if(touched){changed=true;if(tiles.Count>0){var copy=Copy(p);copy.Tiles=tiles;output.Add(copy);}}else output.Add(p);}
   else if(p.Kind=="quad"&&String.IsNullOrEmpty(p.Original)){var cut=Subtract(new[]{Math.Min(p.X,p.X2),Math.Min(p.Y,p.Y2),Math.Max(p.X,p.X2),Math.Max(p.Y,p.Y2)},cx-rad,cy-rad,cx+rad,cy+rad);if(cut.Count!=1||cut[0][0]!=Math.Min(p.X,p.X2)||cut[0][1]!=Math.Min(p.Y,p.Y2)||cut[0][2]!=Math.Max(p.X,p.X2)||cut[0][3]!=Math.Max(p.Y,p.Y2)){changed=true;foreach(var tile in cut){var copy=Copy(p);copy.X=tile[0];copy.Y=tile[1];copy.X2=tile[2];copy.Y2=tile[3];output.Add(copy);}}else output.Add(p);}
   else if(p.Kind=="quad"&&!String.IsNullOrEmpty(p.Original)){var cut=CutQuad(p,cx-rad,cy-rad,cx+rad,cy+rad);if(cut.Count!=1||!Object.ReferenceEquals(cut[0],p))changed=true;output.AddRange(cut);}
   else output.Add(p);
  }return output;
 }
}
public partial class MainWindow {
 ModernNumber eraserSize=new ModernNumber{Minimum=4,Maximum=160,Value=24,Width=228};Label eraserLabel=new Label();ModernButton imageSmaller,imageLarger;
 bool erasing,eraseChanged,resizingImage;Point eraseLast,eraseHover;bool showEraser;string editSnapshot;Part resizePart;double resizeStartSize,resizeDistance;PointF resizeCenter;bool editWasDirty;
 bool BeginErasing(MouseEventArgs e){if(drawingTool.SelectedIndex!=7)return false;erasing=true;eraseChanged=false;editSnapshot=json.Serialize(Doc);editWasDirty=dirty;eraseLast=e.Location;canvas.Capture=true;EraseStroke(e.Location);return true;}
 void EraseStroke(Point point){double radius=(double)eraserSize.Value/2;int steps=Math.Max(1,(int)Math.Ceiling(Distance(eraseLast,point)/Math.Max(1,radius*.6)));bool limited=false;
  for(int i=1;i<=steps;i++){double px=eraseLast.X+(point.X-eraseLast.X)*i/(double)steps,py=eraseLast.Y+(point.Y-eraseLast.Y)*i/(double)steps;double scale=Scale(canvas.ClientRectangle);bool touched;var next=EraserGeometry.Apply(Doc.Parts,(px-canvas.Width/2.0)/scale,(py-canvas.Height/2.0)/scale,radius/scale,scale,Doc.LineWidth,out touched);if(!touched)continue;if(next.Sum(p=>p.Kind=="image"&&p.Tiles!=null?p.Tiles.Count:1)>20000){limited=true;break;}Doc.Parts=next;eraseChanged=true;}
  eraseLast=point;canvas.Invalidate();if(limited)Status(L.T("Лимит фигур: уменьшите размер рисунка при импорте или сотрите более крупный участок."));
 }
 void FinishCanvasEdit(bool cancel){if(!erasing&&!resizingImage)return;bool changed=erasing?eraseChanged:json.Serialize(Doc)!=editSnapshot;erasing=resizingImage=false;resizePart=null;
  if(cancel){Doc=json.Deserialize<Project>(editSnapshot);dirty=editWasDirty;}else if(changed){undo.Push(editSnapshot);redo.Clear();}
  RefreshLayers(layers.SelectedIndex);UpdateProps();if(!cancel&&changed)Changed();else UpdateModernHeader();canvas.Capture=false;canvas.Invalidate();
 }
 RectangleF ImageBounds(Part p){if(p==null||p.Kind!="image"||p.Tiles==null||p.Tiles.Count==0)return RectangleF.Empty;double l=p.Tiles.Min(t=>t[0]),r=p.Tiles.Max(t=>t[2]),t0=p.Tiles.Min(t=>t[1]),b=p.Tiles.Max(t=>t[3]);var a=Screen(p.X+l*p.Size,p.Y+t0*p.Size,p.Mils,canvas.ClientRectangle);var z=Screen(p.X+r*p.Size,p.Y+b*p.Size,p.Mils,canvas.ClientRectangle);return RectangleF.FromLTRB(Math.Min(a.X,z.X),Math.Min(a.Y,z.Y),Math.Max(a.X,z.X),Math.Max(a.Y,z.Y));}
 bool BeginImageResize(MouseEventArgs e){if(drawingTool.SelectedIndex!=0||Selected==null||Selected.Kind!="image")return false;var bounds=ImageBounds(Selected);if(bounds.IsEmpty||Distance(new PointF(bounds.Right,bounds.Bottom),e.Location)>11)return false;resizingImage=true;resizePart=Selected;editSnapshot=json.Serialize(Doc);editWasDirty=dirty;resizeStartSize=resizePart.Size;resizeCenter=Screen(resizePart.X,resizePart.Y,resizePart.Mils,canvas.ClientRectangle);resizeDistance=Math.Max(1,Distance(resizeCenter,e.Location));canvas.Capture=true;canvas.Cursor=Cursors.SizeNWSE;return true;}
 void ResizeImageMove(Point point){resizePart.Size=Math.Max(.0001,Math.Min(1000000,Math.Abs(resizeStartSize)*Distance(resizeCenter,point)/resizeDistance));canvas.Invalidate();}
 void ScaleSelectedImage(double factor){if(Selected==null||Selected.Kind!="image")return;Remember();Selected.Size=Math.Max(.0001,Math.Min(1000000,Math.Abs(Selected.Size)*factor));UpdateProps();Changed();}
 void PaintCanvasEditing(Graphics g){if(Selected!=null&&Selected.Kind=="image"){var bounds=ImageBounds(Selected);if(!bounds.IsEmpty)using(var pen=new Pen(Ui.Accent,1)){g.DrawRectangle(pen,bounds.X,bounds.Y,bounds.Width,bounds.Height);g.FillRectangle(Brushes.White,bounds.Right-5,bounds.Bottom-5,10,10);g.DrawRectangle(pen,bounds.Right-5,bounds.Bottom-5,10,10);}}if(showEraser&&drawingTool.SelectedIndex==7){float d=(float)eraserSize.Value;using(var outline=new Pen(Color.Black,3))g.DrawRectangle(outline,eraseHover.X-d/2,eraseHover.Y-d/2,d,d);using(var pen=new Pen(Color.White,1))g.DrawRectangle(pen,eraseHover.X-d/2,eraseHover.Y-d/2,d,d);}}
}
}
