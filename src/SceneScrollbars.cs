using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace ScopeMaker {
public partial class MainWindow {
 readonly HScrollBar sceneHorizontal=new HScrollBar();
 readonly VScrollBar sceneVertical=new VScrollBar();
 bool syncingSceneScroll;
 Control CreateSceneViewport(){
  var host=new TableLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty,ColumnCount=2,RowCount=2,BackColor=Ui.Surface};
  host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,SystemInformation.VerticalScrollBarWidth));
  host.RowStyles.Add(new RowStyle(SizeType.Percent,100));host.RowStyles.Add(new RowStyle(SizeType.Absolute,SystemInformation.HorizontalScrollBarHeight));
  sceneHorizontal.Dock=sceneVertical.Dock=DockStyle.Fill;sceneHorizontal.Margin=sceneVertical.Margin=Padding.Empty;
  sceneHorizontal.AccessibleName=L.T("Горизонтальная прокрутка сцены");sceneVertical.AccessibleName=L.T("Вертикальная прокрутка сцены");
  host.Controls.Add(canvas,0,0);host.Controls.Add(sceneVertical,1,0);host.Controls.Add(sceneHorizontal,0,1);
  sceneHorizontal.ValueChanged+=SceneScrollChanged;sceneVertical.ValueChanged+=SceneScrollChanged;
  canvas.SizeChanged+=delegate{SyncSceneScrollbars();};SyncSceneScrollbars();return host;
 }
 void SceneScrollChanged(object sender,EventArgs e){if(syncingSceneScroll)return;if(drawing||erasing||resizingImage||dragging||panning){SyncSceneScrollbars();return;}pan=new PointF(-sceneHorizontal.Value,-sceneVertical.Value);canvas.Invalidate();}
 void SyncSceneScrollbars(){syncingSceneScroll=true;try{ConfigureSceneScroll(sceneHorizontal,canvas.ClientSize.Width,pan.X);ConfigureSceneScroll(sceneVertical,canvas.ClientSize.Height,pan.Y);}finally{syncingSceneScroll=false;}}
 void ConfigureSceneScroll(ScrollBar bar,int viewport,float offset){int page=Math.Max(1,viewport);int extent=(int)Math.Ceiling(Math.Max(page*Math.Max(1,ViewZoom),Math.Abs(offset)+page/2.0));int value=(int)Math.Round(-offset);bar.Minimum=-extent;bar.Maximum=extent+page-1;bar.LargeChange=page;bar.SmallChange=32;bar.Value=Math.Max(bar.Minimum,Math.Min(extent,value));}
 public static void ScrollbarChecks(List<string> results){using(var w=new MainWindow()){w.Show();Application.DoEvents();string before=w.json.Serialize(w.Doc);int undoCount=w.undo.Count;
  w.sceneHorizontal.Value=120;w.sceneVertical.Value=-75;if(w.pan!=new PointF(-120,75))throw new Exception("Scrollbar pan mapping");
  w.BeginPan(new MouseEventArgs(MouseButtons.Middle,1,10,10,0));w.MovePan(new Point(6000,-4000));w.EndPan();if(Math.Abs(w.sceneHorizontal.Value+((PointF)w.pan).X)>.5||Math.Abs(w.sceneVertical.Value+((PointF)w.pan).Y)>.5)throw new Exception("Drag scrollbar synchronization");
  w.zoom.Value=400;w.ClientSize=new Size(1280,800);Application.DoEvents();if(Math.Abs(w.sceneHorizontal.Value+((PointF)w.pan).X)>.5||Math.Abs(w.sceneVertical.Value+((PointF)w.pan).Y)>.5)throw new Exception("Zoom/resize scrollbar synchronization");
  w.ResetView();if(w.sceneHorizontal.Value!=0||w.sceneVertical.Value!=0)throw new Exception("Scrollbar center reset");if(w.json.Serialize(w.Doc)!=before||w.dirty||w.undo.Count!=undoCount)throw new Exception("Scrollbar changed document");
  results.Add("PASS: Visible scrollbars synchronize with drag, large offsets, zoom, resize and center without changing project");w.dirty=false;w.Close();}}
}
}
