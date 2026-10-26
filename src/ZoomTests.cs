using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ScopeMaker {
public partial class MainWindow {
 public static void ZoomChecks(List<string> results){using(var w=new MainWindow()){
  w.ApplyBackground(new BackgroundChoice{Key="1_day",Grid=false});w.dirty=false;
  string original=w.json.Serialize(w.Doc);
  foreach(var viewport in new[]{new Rectangle(0,0,800,600),new Rectangle(0,0,1400,500)}){
   w.zoom.Value=100;var normal=w.BackgroundFrame(viewport,w.background.Size);
   // A landmark at 3/4 width and 1/4 height is aligned to a reticle point at 100%.
   double worldX=(normal.Width*0.25)/w.Scale(viewport),worldY=(-normal.Height*0.25)/w.Scale(viewport);
   foreach(int level in new[]{25,100,200,400}){w.zoom.Value=level;var frame=w.BackgroundFrame(viewport,w.background.Size);var point=w.Screen(worldX,worldY,false,viewport);float imageX=frame.Left+frame.Width*0.75f,imageY=frame.Top+frame.Height*0.25f;if(Math.Abs(imageX-point.X)>0.001||Math.Abs(imageY-point.Y)>0.001)throw new Exception("Background landmark drifted relative to reticle at "+level+"%");if(Math.Abs(frame.Left+frame.Width/2-viewport.Width/2f)>0.001||Math.Abs(frame.Top+frame.Height/2-viewport.Height/2f)>0.001)throw new Exception("Zoom center shifted");}
  }
  results.Add("PASS: Background landmarks stay aligned with reticle at 25-400% and different canvas sizes");
  w.zoom.Value=100;w.CanvasWheel(w.canvas,new MouseEventArgs(MouseButtons.None,0,0,0,120));if(w.zoom.Value!=110)throw new Exception("Wheel zoom in failed");w.CanvasWheel(w.canvas,new MouseEventArgs(MouseButtons.None,0,0,0,-120));if(w.zoom.Value!=100)throw new Exception("Wheel zoom out failed");w.zoom.Value=400;w.CanvasWheel(w.canvas,new MouseEventArgs(MouseButtons.None,0,0,0,120));if(w.zoom.Value!=400)throw new Exception("Maximum zoom exceeded");w.zoom.Value=25;w.CanvasWheel(w.canvas,new MouseEventArgs(MouseButtons.None,0,0,0,-120));if(w.zoom.Value!=25)throw new Exception("Minimum zoom exceeded");
  if(w.json.Serialize(w.Doc)!=original||w.dirty)throw new Exception("View zoom changed the project");results.Add("PASS: Wheel zoom respects limits and leaves project and exported sight unchanged");w.dirty=false;
 }}
}
}
