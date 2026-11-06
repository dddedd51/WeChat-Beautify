using System;
using System.Drawing;
using System.Linq;
internal sealed class ContrastReport { internal bool Pass;internal int RecommendedShade=-1;internal string Summary,Details; }
internal static class Readability {
    internal static double Luminance(Color c){return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
    static double Linear(byte v){double s=v/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
    internal static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    internal static string SelectionInk(string accent){Color c=ColorTranslator.FromHtml(accent);return Contrast(c,Color.White)>=Contrast(c,Color.Black)?"#FFFFFF":"#000000";}
    static Color Mask(Color c,int shade,bool dark){int t=dark?20:245;return Color.FromArgb((c.R*(100-shade)+t*shade+50)/100,(c.G*(100-shade)+t*shade+50)/100,(c.B*(100-shade)+t*shade+50)/100);}
    static Color C(GlassSettings s,string key){return ColorTranslator.FromHtml(s.Colors.Effective(s,key));}
    internal static Color Composite(Color bottom,Color top,int opacity){int a=(opacity*255+50)/100;return Color.FromArgb((bottom.R*(255-a)+top.R*a+127)/255,(bottom.G*(255-a)+top.G*a+127)/255,(bottom.B*(255-a)+top.B*a+127)/255);}
    internal static ContrastReport Analyze(GlassSettings s){
        bool image=!String.IsNullOrEmpty(s.Wallpaper);
        double selected=Contrast(C(s,"selectedText"),C(s,"selection"));
        Color[] samples;
        if(image){
            var baseline=new GlassSettings{Wallpaper=s.Wallpaper,Blur=s.Blur,Shade=0,Dark=s.Dark,CropX=s.CropX,CropY=s.CropY,CropW=s.CropW,CropH=s.CropH};
            samples=new Color[4096];using(var bitmap=new Bitmap(Wallpaper.Process(baseline)))for(int y=0;y<64;y++)for(int x=0;x<64;x++)samples[y*64+x]=bitmap.GetPixel(Math.Min(bitmap.Width-1,(int)((x+.5)*bitmap.Width/64)),Math.Min(bitmap.Height-1,(int)((y+.5)*bitmap.Height/64)));
        }else samples=new[]{C(s,"list")};
        Func<string,int,double> min=(ink,shade)=>samples.Min(c=>Contrast(C(s,ink),image?Composite(Mask(c,shade,s.Dark),C(s,"list"),s.Colors.Opacity(s.Dark,"list")):C(s,"list")));
        double primary=min("text",s.Shade),secondary=min("secondary",s.Shade);
        var report=new ContrastReport{Pass=primary>=4.5&&secondary>=4.5&&selected>=4.5};
        if(image&&selected>=4.5&&!report.Pass)for(int shade=s.Shade+1;shade<=85;shade++)if(min("text",shade)>=4.5&&min("secondary",shade)>=4.5){report.RecommendedShade=shade;break;}
        report.Summary=report.Pass?"列表配色采样通过 · 4.5:1":"列表文字对比需要调整";
        report.Details=String.Format("名称 {0:0.00}:1  ·  摘要 {1:0.00}:1  ·  选中项 {2:0.00}:1\n",primary,secondary,selected)+(report.RecommendedShade>=0?"建议遮罩 "+report.RecommendedShade+"%。\n":"")+"按整图采样估算；消息、图标与其他原生内容请在微信中检查。";
        return report;
    }
}
