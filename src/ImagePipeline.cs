using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ScopeMaker {
public static class ImageSource {
 public static Bitmap Load(string path){var file=new FileInfo(path);if(file.Length>256L*1024*1024)throw new FormatException(L.T("Файл слишком большой: предел 256 МБ."));using(var image=Image.FromFile(path))return Prepare(image);}
 public static Bitmap Prepare(Image source){if((long)source.Width*source.Height>64000000)throw new FormatException(L.T("Изображение больше 64 мегапикселей. Уменьшите исходник перед импортом."));double scale=Math.Min(1,4096.0/Math.Max(source.Width,source.Height));int w=Math.Max(1,(int)Math.Round(source.Width*scale)),h=Math.Max(1,(int)Math.Round(source.Height*scale));var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);try{using(var g=Graphics.FromImage(bitmap))using(var attributes=new ImageAttributes()){attributes.SetWrapMode(WrapMode.TileFlipXY);g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,new Rectangle(0,0,w,h),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);}return bitmap;}catch{bitmap.Dispose();throw;}}
}
}
