using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScopeMaker {
public partial class MainWindow {
 ComboBox drawingTool=new ComboBox();CheckBox snapGrid=new CheckBox();
 bool drawing;PointF drawStart,drawEnd;List<PointF> stroke=new List<PointF>();List<Part> draft=new List<Part>();
 void SetupDrawingTools(Control host){
  host.Controls.Add(new Label{Text=L.T("РИСОВАНИЕ НА ХОЛСТЕ"),AutoSize=true,ForeColor=accent,Margin=new Padding(0,8,0,8)});
  drawingTool.DropDownStyle=ComboBoxStyle.DropDownList;drawingTool.Width=190;drawingTool.ForeColor=Color.FromArgb(20,28,36);drawingTool.Items.AddRange(new object[]{L.T("Выбор / перемещение"),L.T("Линия"),L.T("Окружность"),L.T("Прямоугольник"),L.T("Залитый прямоугольник"),L.T("Залитый треугольник"),L.T("Карандаш")});drawingTool.SelectedIndex=0;drawingTool.SelectedIndexChanged+=delegate{CancelDrawing();canvas.Cursor=drawingTool.SelectedIndex==0?Cursors.Default:Cursors.Cross;};host.Controls.Add(drawingTool);
  snapGrid.Text=L.T("Привязка к сетке");snapGrid.AutoSize=true;host.Controls.Add(snapGrid);host.Controls.Add(new Label{Text=L.T("Зажмите ЛКМ и тяните.\nShift — ось / квадрат.\nEsc — отмена и выбор."),Size=new Size(190,62),ForeColor=Color.FromArgb(151,166,184)});
 }
 PointF World(Point pt){float scale=Scale(canvas.ClientRectangle);float x=(pt.X-canvas.Width/2f-pan.X)/scale,y=(pt.Y-canvas.Height/2f-pan.Y)/scale;if(snapGrid.Checked){x=(float)Math.Round(x/0.05)*0.05f;y=(float)Math.Round(y/0.05)*0.05f;}return new PointF(x,y);}
 public static List<Part> MakeDrawing(int tool,PointF start,PointF end,bool constrain,IList<PointF> points){
  double x=start.X,y=start.Y,dx=end.X-x,dy=end.Y-y;
  if(constrain&&tool==1){if(Math.Abs(dx)>=Math.Abs(dy))dy=0;else dx=0;}
  if(constrain&&(tool==3||tool==4||tool==5)){double size=Math.Max(Math.Abs(dx),Math.Abs(dy));dx=(dx<0?-1:1)*size;dy=(dy<0?-1:1)*size;}
  double ex=x+dx,ey=y+dy;var parts=new List<Part>();
  if(tool==1)parts.Add(new Part{Kind="line",Name=L.T("Нарисованная линия"),X=x,Y=y,X2=ex,Y2=ey});
  if(tool==2){double diameter=Math.Min(Math.Abs(dx),Math.Abs(dy));parts.Add(new Part{Kind="circle",Name=L.T("Нарисованная окружность"),X=x+(dx<0?-1:1)*diameter/2,Y=y+(dy<0?-1:1)*diameter/2,Size=diameter});}
  if(tool==3){double[] xs={x,ex,ex,x,x},ys={y,y,ey,ey,y};for(int i=0;i<4;i++)parts.Add(new Part{Kind="line",Name=L.T("Прямоугольник / сторона ")+(i+1),X=xs[i],Y=ys[i],X2=xs[i+1],Y2=ys[i+1]});}
  if(tool==4)parts.Add(new Part{Kind="quad",Name=L.T("Нарисованный прямоугольник"),X=x,Y=y,X2=ex,Y2=ey});
  if(tool==5){var n=new BlkNode{Name="quad",Block=true};n.Set("tl","p2",Blk.Num((x+ex)/2)+", "+Blk.Num(Math.Min(y,ey)));n.Set("tr","p2",Blk.Num(ex)+", "+Blk.Num(Math.Max(y,ey)));n.Set("br","p2",Blk.Num(x)+", "+Blk.Num(Math.Max(y,ey)));n.Set("bl","p2",Blk.Num(x)+", "+Blk.Num(Math.Max(y,ey)));parts.Add(new Part{Kind="quad",Name=L.T("Нарисованный треугольник"),Original=n.ToString(),X=(x+ex)/2,Y=Math.Min(y,ey),X2=x,Y2=Math.Max(y,ey)});}
  if(tool==6&&points!=null)for(int i=1;i<points.Count;i++)parts.Add(new Part{Kind="line",Name=L.T("Карандаш / штрих ")+i,X=points[i-1].X,Y=points[i-1].Y,X2=points[i].X,Y2=points[i].Y});
  return parts;
 }
 bool BeginDrawing(MouseEventArgs e){if(drawingTool.SelectedIndex==0)return false;drawStart=drawEnd=World(e.Location);stroke.Clear();stroke.Add(drawStart);drawing=true;canvas.Capture=true;canvas.Cursor=Cursors.Cross;draft.Clear();return true;}
 void PreviewDrawing(MouseEventArgs e){drawEnd=World(e.Location);if(drawingTool.SelectedIndex==6){var p=stroke[stroke.Count-1];double distance=Math.Sqrt(Math.Pow(drawEnd.X-p.X,2)+Math.Pow(drawEnd.Y-p.Y,2));if(distance*Scale(canvas.ClientRectangle)>=2&&stroke.Count<5000)stroke.Add(drawEnd);}draft=MakeDrawing(drawingTool.SelectedIndex,drawStart,drawEnd,(ModifierKeys&Keys.Shift)!=0,stroke);canvas.Invalidate();}
 void CanvasUp(object sender,MouseEventArgs e){if(panning){EndPan();return;}if(e.Button!=MouseButtons.Left)return;if(erasing||resizingImage){if(erasing)EraseStroke(e.Location);else ResizeImageMove(e.Location);FinishCanvasEdit(false);canvas.Cursor=drawingTool.SelectedIndex==0?Cursors.Default:Cursors.Cross;return;}if(drawing){PreviewDrawing(e);double w=Math.Abs(drawStart.X-drawEnd.X)*Scale(canvas.ClientRectangle),h=Math.Abs(drawStart.Y-drawEnd.Y)*Scale(canvas.ClientRectangle);bool useful=drawingTool.SelectedIndex==1?w>3||h>3:w>3&&h>3;if(drawingTool.SelectedIndex==6)useful=stroke.Count>1;var created=draft;drawing=false;draft=new List<Part>();int count=Doc.Parts.Sum(p=>p.Kind=="image"&&p.Tiles!=null?p.Tiles.Count:1);if(useful&&created.Count>0&&count+created.Count<=20000){Remember();int first=Doc.Parts.Count;Doc.Parts.AddRange(created);RefreshLayers(first);Changed();}else if(count+created.Count>20000)Status(L.T("Лимит: 20 000 элементов на проект."));}dragging=false;canvas.Capture=false;canvas.Cursor=drawingTool.SelectedIndex==0?Cursors.Default:Cursors.Cross;canvas.Invalidate();}
 void CancelDrawing(){EndPan();FinishCanvasEdit(true);drawing=false;draft.Clear();stroke.Clear();if(canvas.Capture)canvas.Capture=false;canvas.Invalidate();}
 static double Distance(PointF a,PointF b){return Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));}
 void TranslatePart(Part p,double dx,double dy){p.X+=dx;p.Y+=dy;p.X2+=dx;p.Y2+=dy;if(p.Kind=="quad"&&!String.IsNullOrEmpty(p.Original)){var n=Blk.Parse(p.Original)[0];foreach(string key in new[]{"tl","tr","br","bl"}){var v=Blk.Values(n.Get(key,"0,0"),2);n.Set(key,"p2",Blk.Num(v[0]+dx)+", "+Blk.Num(v[1]+dy));}p.Original=n.ToString();}}
}
}
