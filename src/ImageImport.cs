using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScopeMaker {
public class ImageSettings {
 public int Resolution=512, Mode=0, Threshold=128;
 public bool Automatic=true, Trim=true, Clean=false, Adaptive=false, FitBudget=true;
 public int MaxTiles=20000;
}
public class ImageResult : IDisposable {
 public List<double[]> Tiles=new List<double[]>();
 public Bitmap Preview;
 public int Threshold, Width, Height, ActualResolution; public bool Reduced, Limited;
 public string Detected;
 public void Dispose(){if(Preview!=null)Preview.Dispose();}
}
public static class ImageConverter {
 // Pixels are sampled once. Binary runs are merged vertically without changing the mask.
 public static ImageResult Convert(Bitmap input,ImageSettings settings){return Convert(input,settings,null);}
 static void CheckCancel(Func<bool> cancel){if(cancel!=null&&cancel())throw new OperationCanceledException();}
 public static ImageResult Convert(Bitmap input,ImageSettings settings,Func<bool> cancel){int resolution=Math.Max(32,Math.Min(2048,settings.Resolution));while(true){CheckCancel(cancel);var result=ConvertCore(input,settings,resolution,cancel);result.Reduced=resolution<Math.Min(settings.Resolution,Math.Max(input.Width,input.Height));if(!result.Limited||!settings.FitBudget||resolution<=32)return result;result.Dispose();resolution=Math.Max(32,(int)(resolution*.72));}}
 static List<double[]> Merge(bool[,] mask,int minX,int minY,int maxX,int maxY,bool transpose,int budget,Func<bool> cancel){var tiles=new List<double[]>();var active=new Dictionary<long,int>();int outerMin=transpose?minX:minY,outerMax=transpose?maxX:maxY,innerMin=transpose?minY:minX,innerMax=transpose?maxY:maxX;for(int row=outerMin;row<=outerMax;row++){CheckCancel(cancel);var next=new Dictionary<long,int>();int x=innerMin;while(x<=innerMax){if(!(transpose?mask[row,x]:mask[x,row])){x++;continue;}int start=x++;while(x<=innerMax&&(transpose?mask[row,x]:mask[x,row]))x++;long key=((long)start<<32)|(uint)x;int index;if(active.TryGetValue(key,out index)){if(transpose)tiles[index][2]=row+1;else tiles[index][3]=row+1;}else{index=tiles.Count;tiles.Add(transpose?new double[]{row,start,row+1,x}:new double[]{start,row,x,row+1});if(tiles.Count>budget)return tiles;}next[key]=index;}active=next;}return tiles;}
 static bool[,] AdaptiveMask(byte[] scores,int width,int height,int bias,Func<bool> cancel){var sums=new long[(width+1)*(height+1)];int stride=width+1;for(int y=0;y<height;y++){CheckCancel(cancel);long row=0;for(int x=0;x<width;x++){row+=scores[y*width+x];sums[(y+1)*stride+x+1]=sums[y*stride+x+1]+row;}}int radius=Math.Max(8,Math.Min(64,Math.Min(width,height)/20));var mask=new bool[width,height];for(int y=0;y<height;y++){CheckCancel(cancel);int top=Math.Max(0,y-radius),bottom=Math.Min(height,y+radius+1);for(int x=0;x<width;x++){int left=Math.Max(0,x-radius),right=Math.Min(width,x+radius+1);double mean=(sums[bottom*stride+right]-sums[top*stride+right]-sums[bottom*stride+left]+sums[top*stride+left])/(double)((right-left)*(bottom-top));mask[x,y]=scores[y*width+x]>Math.Min(244,mean+bias);}}return mask;}
 static ImageResult ConvertCore(Bitmap input,ImageSettings settings,int resolution,Func<bool> cancel) {
  int limit=Math.Max(32,Math.Min(2048,resolution));
  double scale=Math.Min(1.0,(double)limit/Math.Max(input.Width,input.Height));
  int width=Math.Max(1,(int)Math.Round(input.Width*scale)),height=Math.Max(1,(int)Math.Round(input.Height*scale));
  byte[] data;using(var resized=new Bitmap(width,height,PixelFormat.Format32bppArgb)) {
   using(var g=Graphics.FromImage(resized)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(input,new Rectangle(0,0,width,height),0,0,input.Width,input.Height,GraphicsUnit.Pixel);}
   var bits=resized.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
   try{data=new byte[width*height*4];for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),data,y*width*4,width*4);}finally{resized.UnlockBits(bits);}
  }
  int transparent=0;var border=new List<int>();
  for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;if(data[i+3]<16)transparent++;if(x==0||y==0||x==width-1||y==height-1)border.Add((data[i]*114+data[i+1]*587+data[i+2]*299)/1000);}
  border.Sort();int mode=settings.Mode;if(mode==0)mode=transparent>Math.Max(0,width*height/100)?3:border[border.Count/2]<128?2:1;
  byte[] scores=new byte[width*height];int[] histogram=new int[256];
  for(int i=0;i<scores.Length;i++){int a=data[i*4+3],l=(data[i*4]*114+data[i*4+1]*587+data[i*4+2]*299)/1000;int score=mode==3?a:((mode==1?255-l:l)*a+127)/255;scores[i]=(byte)score;histogram[score]++;}
  int threshold=settings.Automatic?Otsu(histogram):settings.Threshold;
  CheckCancel(cancel);bool[,] mask;if(settings.Adaptive&&mode!=3)mask=AdaptiveMask(scores,width,height,settings.Automatic?10:Math.Max(1,settings.Threshold/8),cancel);else{mask=new bool[width,height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)mask[x,y]=scores[y*width+x]>threshold;}
  if(settings.Clean)RemoveSpecks(mask,width,height,cancel);
  int minX=width,minY=height,maxX=-1,maxY=-1;
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(mask[x,y]){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
  if(!settings.Trim||maxX<0){minX=0;minY=0;maxX=width-1;maxY=height-1;}
  var result=new ImageResult{Threshold=threshold,Width=maxX-minX+1,Height=maxY-minY+1,Detected=mode==3?L.T("по прозрачности"):mode==1?L.T("тёмный рисунок"):L.T("светлый рисунок")};
  int budget=Math.Max(1,Math.Min(20000,settings.MaxTiles));var horizontal=Merge(mask,minX,minY,maxX,maxY,false,budget,cancel);var vertical=Merge(mask,minX,minY,maxX,maxY,true,budget,cancel);result.Tiles=vertical.Count<horizontal.Count?vertical:horizontal;result.Limited=result.Tiles.Count>budget;result.ActualResolution=Math.Max(width,height);
  foreach(var tile in result.Tiles){tile[0]=(tile[0]-minX)/result.Width-0.5;tile[2]=(tile[2]-minX)/result.Width-0.5;tile[1]=(tile[1]-minY-result.Height/2.0)/result.Width;tile[3]=(tile[3]-minY-result.Height/2.0)/result.Width;}
  result.Preview=new Bitmap(width,height,PixelFormat.Format32bppArgb);byte[] preview=new byte[width*height*4];
  for(int y=0;y<height;y++)for(int x=0;x<width;x++){int i=(y*width+x)*4;bool on=mask[x,y];preview[i]=(byte)(on?156:30);preview[i+1]=(byte)(on?245:23);preview[i+2]=(byte)(on?92:17);preview[i+3]=255;}
  var pb=result.Preview.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<height;y++)Marshal.Copy(preview,y*width*4,IntPtr.Add(pb.Scan0,y*pb.Stride),width*4);}finally{result.Preview.UnlockBits(pb);}
  return result;
 }
 static int Otsu(int[] histogram){long total=histogram.Sum(),sum=0,back=0,sumBack=0;for(int i=0;i<256;i++)sum+=(long)i*histogram[i];double best=-1;int low=0,high=0;for(int i=0;i<256;i++){back+=histogram[i];sumBack+=(long)i*histogram[i];long fore=total-back;if(back==0||fore==0)continue;double delta=(double)sumBack/back-(double)(sum-sumBack)/fore;double variance=(double)back*fore*delta*delta;if(variance>best){best=variance;low=high=i;}else if(variance==best)high=i;}return best<0?128:(low+high)/2;}
 static void RemoveSpecks(bool[,] mask,int width,int height,Func<bool> cancel){var seen=new bool[width,height];int[] dx={-1,0,1,-1,1,-1,0,1},dy={-1,-1,-1,0,0,1,1,1};for(int y=0;y<height;y++)for(int x=0;x<width;x++){if(!mask[x,y]||seen[x,y])continue;var queue=new Queue<Point>();var component=new List<Point>();queue.Enqueue(new Point(x,y));seen[x,y]=true;while(queue.Count>0){var p=queue.Dequeue();if(component.Count<3)component.Add(p);CheckCancel(cancel);for(int k=0;k<8;k++){int nx=p.X+dx[k],ny=p.Y+dy[k];if(nx>=0&&ny>=0&&nx<width&&ny<height&&!seen[nx,ny]&&mask[nx,ny]){seen[nx,ny]=true;queue.Enqueue(new Point(nx,ny));}}}if(component.Count<=2)foreach(var p in component)mask[p.X,p.Y]=false;}}
}
public class ImageImportDialog : Form {
 Bitmap input;string sourcePath;PictureBox before=new PictureBox(),after=new PictureBox();ComboBox mode=new ModernCombo(),resolution=new ModernCombo();TrackBar threshold=new TrackBar();CheckBox automatic=new CheckBox(),trim=new CheckBox(),clean=new CheckBox(),adaptive=new CheckBox(),fit=new CheckBox();Label info=new Label();Button accept=new ModernButton{Primary=true};Timer debounce=new Timer();ImageResult result;BackgroundWorker worker=new BackgroundWorker{WorkerSupportsCancellation=true};int revision=0,budget;volatile bool closed;
 class Request {public int Revision;public ImageSettings Settings;}
 class Completion {public int Revision;public ImageResult Result;}
 public Part Imported;
 public bool PreviewBusy {get{return worker.IsBusy||debounce.Enabled;}}
 public ImageImportDialog(Bitmap source):this(null,source,20000){}
 public ImageImportDialog(string path,int remaining):this(path,null,remaining){}
 ImageImportDialog(string path,Bitmap source,int remaining){
  sourcePath=path;budget=Math.Max(1,Math.Min(20000,remaining));if(source!=null)input=ImageSource.Prepare(source);Text=L.T("Импорт рисунка — настройка чёткости");Size=new Size(1040,790);MinimumSize=new Size(1040,760);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(16)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,170));root.RowStyles.Add(new RowStyle(SizeType.Absolute,78));Controls.Add(root);
  root.Controls.Add(new Label{Text=L.T("ИСХОДНИК"),AutoSize=true},0,0);root.Controls.Add(new Label{Text=L.T("РЕЗУЛЬТАТ КОНВЕРТАЦИИ"),AutoSize=true},1,0);
  before.Dock=after.Dock=DockStyle.Fill;before.SizeMode=after.SizeMode=PictureBoxSizeMode.Zoom;before.BackColor=Color.FromArgb(70,78,90);after.BackColor=Color.FromArgb(17,23,30);before.Image=input;root.Controls.Add(before,0,1);root.Controls.Add(after,1,1);
  var options=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,12,0,0)};root.Controls.Add(options,0,2);root.SetColumnSpan(options,2);
  options.Controls.Add(new Label{Text=L.T("Режим"),AutoSize=true,Padding=new Padding(0,5,0,0)});mode.Width=210;mode.Items.AddRange(new object[]{L.T("Определить автоматически"),L.T("Тёмный рисунок"),L.T("Светлый рисунок"),L.T("Силуэт по прозрачности")});mode.SelectedIndex=0;options.Controls.Add(mode);
  options.Controls.Add(new Label{Text=L.T("Детализация"),AutoSize=true,Padding=new Padding(12,5,0,0)});resolution.Width=96;resolution.Items.AddRange(new object[]{128,256,512,768,1024,1536,2048});resolution.SelectedIndex=2;options.Controls.Add(resolution);
  automatic.Text=L.T("Автопорог");automatic.Checked=true;automatic.AutoSize=true;options.Controls.Add(automatic);options.SetFlowBreak(automatic,true);
  options.Controls.Add(new Label{Text=L.T("Порог"),Width=52,Height=42,TextAlign=ContentAlignment.MiddleLeft});threshold.Minimum=0;threshold.Maximum=254;threshold.Value=128;threshold.Width=275;threshold.TickFrequency=32;threshold.Enabled=false;options.Controls.Add(threshold);
  trim.Text=L.T("Обрезать пустые поля");trim.Checked=true;trim.AutoSize=true;options.Controls.Add(trim);clean.Text=L.T("Убрать одиночные точки");clean.AutoSize=true;options.Controls.Add(clean);options.SetFlowBreak(clean,true);
  adaptive.Text=L.T("Неравномерный фон");adaptive.AutoSize=true;options.Controls.Add(adaptive);fit.Text=L.T("Подбирать детализацию под лимит фигур");fit.Checked=true;fit.AutoSize=true;options.Controls.Add(fit);options.SetFlowBreak(fit,true);
  options.Controls.Add(new Label{Text=L.T("Выше детализация — точнее мелкие детали. Результат одноцветный."),AutoSize=true});
  var bottom=new FlowLayoutPanel{Dock=DockStyle.Fill};root.Controls.Add(bottom,0,3);root.SetColumnSpan(bottom,2);info.Size=new Size(650,70);bottom.Controls.Add(info);accept.Text=L.T("Добавить");accept.Size=new Size(115,34);accept.Enabled=false;accept.Click+=delegate{if(result==null||!accept.Enabled||PreviewBusy)return;Imported=new Part{Kind="image",Name=L.T("Рисунок (")+result.Tiles.Count+L.T(" фигур)"),X=0,Y=0,Size=.6,Tiles=result.Tiles};DialogResult=DialogResult.OK;};bottom.Controls.Add(accept);CancelButton=new ModernButton{Text=L.T("Отмена"),DialogResult=DialogResult.Cancel,Size=new Size(90,34)};bottom.Controls.Add((Control)CancelButton);
  debounce.Interval=200;debounce.Tick+=delegate{debounce.Stop();StartPreview();};EventHandler update=delegate{revision++;accept.Enabled=false;debounce.Stop();if(worker.IsBusy)worker.CancelAsync();debounce.Start();};mode.SelectedIndexChanged+=update;resolution.SelectedIndexChanged+=update;threshold.ValueChanged+=update;trim.CheckedChanged+=update;clean.CheckedChanged+=update;adaptive.CheckedChanged+=update;fit.CheckedChanged+=update;automatic.CheckedChanged+=delegate{threshold.Enabled=!automatic.Checked;update(null,EventArgs.Empty);};
  worker.DoWork+=delegate(object sender,DoWorkEventArgs e){var request=(Request)e.Argument;try{if(input==null)input=ImageSource.Load(sourcePath);if(worker.CancellationPending||closed){e.Cancel=true;return;}var converted=ImageConverter.Convert(input,request.Settings,delegate{return worker.CancellationPending||closed;});e.Result=new Completion{Revision=request.Revision,Result=converted};}catch(OperationCanceledException){e.Cancel=true;}};
  worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){if(closed){if(!e.Cancelled&&e.Error==null&&e.Result!=null)((Completion)e.Result).Result.Dispose();ReleaseInput();worker.Dispose();return;}if(e.Cancelled){StartPreview();return;}if(e.Error!=null){accept.Enabled=false;info.Text=e.Error is OutOfMemoryException?L.T("Недостаточно памяти: уменьшите исходное изображение."):L.T(e.Error.Message);return;}var done=(Completion)e.Result;if(done.Revision!=revision){done.Result.Dispose();StartPreview();return;}before.Image=input;after.Image=done.Result.Preview;if(result!=null)result.Dispose();result=done.Result;accept.Enabled=result.Tiles.Count>0&&!result.Limited;info.Text=result.Detected+" · "+result.Width+"×"+result.Height+L.T(" · фигур: ")+(result.Limited?">"+budget:result.Tiles.Count.ToString())+" / "+budget+"\n"+(result.Reduced?L.T("Детализация автоматически снижена до ")+result.ActualResolution+" px. ":"")+(result.Tiles.Count==0?L.T("Рисунок не найден: смените режим или уменьшите порог."):result.Limited?L.T("Слишком сложное изображение: уменьшите детализацию или включите очистку точек."):L.T("Размер рисунка: маркер на холсте, кнопки в свойствах или Ctrl + колесо."));};
  Ui.Theme(this);Shown+=delegate{StartPreview();};FormClosing+=delegate{closed=true;if(worker.IsBusy)worker.CancelAsync();};
 }
 void StartPreview(){if(closed||worker.IsBusy)return;debounce.Stop();accept.Enabled=false;info.Text=L.T("Загрузка и конвертация…");worker.RunWorkerAsync(new Request{Revision=revision,Settings=new ImageSettings{Mode=mode.SelectedIndex,Resolution=(int)resolution.SelectedItem,Automatic=automatic.Checked,Threshold=threshold.Value,Trim=trim.Checked,Clean=clean.Checked,Adaptive=adaptive.Checked,FitBudget=fit.Checked,MaxTiles=budget}});}
 public void WaitForPreview(){var timer=Stopwatch.StartNew();while(PreviewBusy&&timer.ElapsedMilliseconds<30000){Application.DoEvents();System.Threading.Thread.Sleep(5);}Application.DoEvents();if(PreviewBusy)throw new TimeoutException("Image preview timed out");}
 public void Screenshot(string path){Show();Application.DoEvents();WaitForPreview();using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(path);}Close();}
 void ReleaseInput(){if(input!=null){input.Dispose();input=null;}}
 protected override void Dispose(bool disposing){if(disposing){closed=true;debounce.Dispose();before.Image=null;after.Image=null;if(result!=null){result.Dispose();result=null;}if(worker.IsBusy)worker.CancelAsync();else{ReleaseInput();worker.Dispose();}}base.Dispose(disposing);}
}
}