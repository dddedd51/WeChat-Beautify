using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
internal static class ThemePresets {
    internal static void Mint(GlassSettings s){
        s.Accent="#39C5BB";s.Dark=false;s.Blur=8;s.Shade=0;s.SetCrop(new System.Windows.Rect(0,0,1,1));
        s.Colors=new ThemeColors();
        string[] light={"#BDE9E4","#E6F5F2","#EFF8F6","#F5FAF8","#39C5BB","#AEF0DF","#FFFFFF","#163D3B","#466460","#153E39","#4E6D68","#103B3A","#155C55","#326D66","#168E85"};
        string[] dark={"#143D3B","#1C3736","#192D2D","#1B3533","#39C5BB","#28554D","#2B4140","#E4F7F3","#AECAC4","#ECFAF6","#ABCBC4","#103B3A","#ACE7DD","#C1E6DD","#39C5BB"};
        for(int i=0;i<ThemeColors.Keys.Length;i++){s.Colors.Set(false,ThemeColors.Keys[i],light[i]);s.Colors.Set(true,ThemeColors.Keys[i],dark[i]);}
        foreach(bool mode in new[]{false,true})foreach(string key in ThemeColors.Regions)s.Colors.SetOpacity(mode,key,mode?92:key=="navigation"?62:key=="input"?76:48);
        string dir=Path.Combine(Files.Data,"backgrounds");Directory.CreateDirectory(dir);string file=Path.Combine(dir,"qingyu-39c5bb-v1.png");
        if(!File.Exists(file)){
            using(var bitmap=new Bitmap(1600,1000,PixelFormat.Format24bppRgb)){
                for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){
                    double u=x/1599d,v=y/999d;
                    double glow=Math.Exp(-((u-.82)*(u-.82)/.09+(v-.18)*(v-.18)/.20));
                    double wave=(Math.Sin(u*5.5+v*2.4)+1)/2*.11;
                    double amount=.10+glow*.35+wave;
                    bitmap.SetPixel(x,y,Color.FromArgb((int)(248*(1-amount)+57*amount),(int)(252*(1-amount)+197*amount),(int)(250*(1-amount)+187*amount)));
                }
                string stage=file+".tmp";bitmap.Save(stage,ImageFormat.Png);File.Move(stage,file,true);
            }
        }
        s.Wallpaper=file;s.WallpaperName="青屿 · 39C5BB";
    }
}
