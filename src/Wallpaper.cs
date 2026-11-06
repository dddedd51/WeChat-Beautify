using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

// All processing is local. Cached copies are immutable, so the native decoder
// never reuses an old image after a slider change or a file is replaced.
internal static class Wallpaper {
    const long MaxBytes=40L*1024*1024, MaxPixels=32000000;
    static readonly object effectsLock=new object();
    internal static string Import(string source) {
        var info=new FileInfo(source);
        if(!info.Exists||info.Length==0||info.Length>MaxBytes)throw new InvalidDataException("图片须为 1 字节至 40 MB。");
        string ext=info.Extension.ToLowerInvariant();
        if(ext!=".png"&&ext!=".jpg"&&ext!=".jpeg"&&ext!=".bmp")throw new InvalidDataException("支持 PNG、JPG、JPEG、BMP 图片。");
        using(var stream=File.OpenRead(source))using(var image=Image.FromStream(stream,true,true)){
            if(image.Width<1||image.Height<1||(long)image.Width*image.Height>MaxPixels)throw new InvalidDataException("图片不能超过 3200 万像素。");
            try {if(Array.IndexOf(image.PropertyIdList,0x112)>=0){int v=image.GetPropertyItem(0x112).Value[0];RotateFlipType[] transforms={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};if(v>=1&&v<=8)image.RotateFlip(transforms[v]);}}catch(ArgumentException){}
            double scale=Math.Min(1,2560d/Math.Max(image.Width,image.Height));
            using(var b=new Bitmap(Math.Max(1,(int)Math.Round(image.Width*scale)),Math.Max(1,(int)Math.Round(image.Height*scale)),PixelFormat.Format32bppArgb)){
                using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,b.Width,b.Height);}
                using(var data=new MemoryStream()){
                    b.Save(data,ImageFormat.Png);byte[] bytes=data.ToArray();string hash;
                    using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                    string dir=Path.Combine(Files.Data,"backgrounds");Directory.CreateDirectory(dir);string dest=Path.Combine(dir,hash+".png");
                    if(!File.Exists(dest)){string stage=dest+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(stage,bytes);File.Move(stage,dest);}finally{if(File.Exists(stage))File.Delete(stage);}}
                    return dest;
                }
            }
        }
    }
    internal static string Process(GlassSettings s) {
        lock(effectsLock)return ProcessCore(s);
    }
    static string ProcessCore(GlassSettings s) {
        s.Validate();if(String.IsNullOrEmpty(s.Wallpaper))return "";
        if(!File.Exists(s.Wallpaper))throw new IOException("已保存的背景图不存在。请重新选择图片，或切换为分区纯色。");
        string cropHash;using(var sha=SHA256.Create())cropHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s.CropKey))).Replace("-","").Substring(0,16);
        string key=Files.Hash(s.Wallpaper)+"-c"+cropHash+"-b"+s.Blur+"-t"+s.Shade+"-d"+(s.Dark?1:0),dir=Path.Combine(Files.Data,"effects");
        Directory.CreateDirectory(dir);string dest=Path.Combine(dir,key+".png");if(File.Exists(dest))return dest;
        using(var source=new Bitmap(s.Wallpaper))using(var cropped=Crop(source,s))using(var output=Transform(cropped,s.Blur,s.Shade,s.Dark)){
            string stage=dest+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{output.Save(stage,ImageFormat.Png);if(!File.Exists(dest))File.Move(stage,dest);}finally{if(File.Exists(stage))File.Delete(stage);}
        }
        return dest;
    }
    internal static Bitmap Crop(Bitmap input,GlassSettings s){s.Validate();int x=Math.Min(input.Width-1,(int)Math.Round(s.CropX*input.Width)),y=Math.Min(input.Height-1,(int)Math.Round(s.CropY*input.Height));int w=Math.Max(1,Math.Min(input.Width-x,(int)Math.Round(s.CropW*input.Width))),h=Math.Max(1,Math.Min(input.Height-y,(int)Math.Round(s.CropH*input.Height)));return input.Clone(new Rectangle(x,y,w,h),PixelFormat.Format32bppArgb);}
    internal static Bitmap Transform(Bitmap input,int blur,int shade,bool dark) {
        if(blur<0||blur>30||shade<0||shade>85)throw new InvalidDataException("图片效果参数超出范围。");
        var output=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb);
        try{
            using(var g=Graphics.FromImage(output)){g.Clear(Color.White);g.DrawImageUnscaled(input,0,0);}
            var rect=new Rectangle(0,0,output.Width,output.Height);var data=output.LockBits(rect,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try{
                int w=output.Width,h=output.Height;var pixels=new byte[w*h*4];
                for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*w*4,w*4);
                if(blur>0){var scratch=new byte[pixels.Length];int radius=Math.Max(1,blur/2);for(int pass=0;pass<3;pass++){BlurAxis(pixels,scratch,w,h,radius,true);BlurAxis(scratch,pixels,w,h,radius,false);}}
                int tint=dark?20:245;
                for(int i=0;i<pixels.Length;i+=4){for(int c=0;c<3;c++)pixels[i+c]=(byte)((pixels[i+c]*(100-shade)+tint*shade+50)/100);pixels[i+3]=255;}
                for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*4,IntPtr.Add(data.Scan0,y*data.Stride),w*4);
            }finally{output.UnlockBits(data);}
            return output;
        }catch{output.Dispose();throw;}
    }
    static void BlurAxis(byte[] from,byte[] to,int w,int h,int radius,bool horizontal){
        int lines=horizontal?h:w,length=horizontal?w:h,step=horizontal?4:w*4,divisor=2*radius+1;
        for(int line=0;line<lines;line++){
            int origin=horizontal?line*w*4:line*4;
            for(int c=0;c<4;c++){
                int sum=0;for(int k=-radius;k<=radius;k++)sum+=from[origin+Math.Max(0,Math.Min(length-1,k))*step+c];
                for(int pos=0;pos<length;pos++){
                    to[origin+pos*step+c]=(byte)((sum+radius)/divisor);
                    sum+=from[origin+Math.Min(length-1,pos+radius+1)*step+c]-from[origin+Math.Max(0,pos-radius)*step+c];
                }
            }
        }
    }
}
