using System;
using System.Drawing;
using System.Linq;

internal sealed class ContrastReport {
    internal bool Pass;internal int RecommendedShade=-1;
    internal string Summary,Details;
}
internal static class Readability {
    internal static double Luminance(Color c){return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
    static double Linear(byte v){double s=v/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}
    internal static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    internal static string SelectionInk(string accent){Color c=ColorTranslator.FromHtml(accent);return Contrast(c,Color.White)>=Contrast(c,Color.Black)?"#FFFFFF":"#000000";}
    static Color Mask(Color c,int shade,bool dark){int t=dark?20:245;return Color.FromArgb((c.R*(100-shade)+t*shade+50)/100,(c.G*(100-shade)+t*shade+50)/100,(c.B*(100-shade)+t*shade+50)/100);}
    static Color C(GlassSettings s,string key){return ColorTranslator.FromHtml(s.Colors.Get(s,key));}
    internal static Color Composite(Color bottom,Color top,int opacity){int a=(opacity*255+50)/100;return Color.FromArgb((bottom.R*(255-a)+top.R*a+127)/255,(bottom.G*(255-a)+top.G*a+127)/255,(bottom.B*(255-a)+top.B*a+127)/255);}
    static double[] Ratios(Color[] samples,GlassSettings s,string ink,string region,int shade){var result=new double[samples.Length];var foreground=C(s,ink);var fill=C(s,region);int opacity=s.Colors.Opacity(s.Dark,region);for(int i=0;i<samples.Length;i++)result[i]=Contrast(foreground,Composite(Mask(samples[i],shade,s.Dark),fill,opacity));return result;}
    internal static ContrastReport Analyze(GlassSettings s) {
        double sent=Contrast(C(s,"chat"),C(s,"outgoing")),received=Contrast(C(s,"chat"),C(s,"incoming")),selected=Contrast(C(s,"selectedText"),C(s,"selection"));
        double link=Math.Min(Contrast(C(s,"link"),C(s,"outgoing")),Contrast(C(s,"link"),C(s,"incoming")));
        bool fixedPass=sent>=4.5&&received>=4.5&&selected>=4.5&&link>=4.5;
        string solid=String.Format("发送 {0:0.00}:1 · 接收 {1:0.00}:1 · 选中项 {2:0.00}:1 · 链接 {3:0.00}:1",sent,received,selected,link);
        if(String.IsNullOrEmpty(s.Wallpaper))return new ContrastReport{Summary=fixedPass?"固定配色通过；毛玻璃背景待实测":"气泡、选中项或链接对比不足",Details=solid+"\n后方内容会变化，无法仅凭主题参数判定透明区域。"};
        var baseline=new GlassSettings{Wallpaper=s.Wallpaper,Blur=s.Blur,Shade=0,Dark=s.Dark,CropX=s.CropX,CropY=s.CropY,CropW=s.CropW,CropH=s.CropH};
        Color[] samples=new Color[64*64];using(var image=new Bitmap(Wallpaper.Process(baseline))){for(int y=0;y<64;y++)for(int x=0;x<64;x++)samples[y*64+x]=image.GetPixel(Math.Min(image.Width-1,(int)((x+.5)*image.Width/64)),Math.Min(image.Height-1,(int)((y+.5)*image.Height/64)));}
        double[] a=Ratios(samples,s,"text","list",s.Shade),b=Ratios(samples,s,"secondary","list",s.Shade);
        double failA=a.Count(v=>v<4.5)/(double)a.Length,failB=b.Count(v=>v<4.5)/(double)b.Length;
        string[] inks={"text","secondary","canvas","text","icons"},regions={"list","list","canvasBackground","input","navigation"};
        Func<int,bool> allPass=shade=>{for(int i=0;i<inks.Length;i++)if(Ratios(samples,s,inks[i],regions[i],shade).Any(v=>v<4.5))return false;return true;};
        var r=new ContrastReport{Pass=fixedPass&&allPass(s.Shade)};
        for(int shade=s.Shade+1;fixedPass&&!r.Pass&&shade<=85;shade++)if(allPass(shade)){r.RecommendedShade=shade;break;}
        r.Summary=r.Pass?"采样对比通过（4.5 : 1）":"存在低对比背景区域";
        r.Details=String.Format("列表主文字 {0:0.00}:1（不足 {1:0}%）· 次文字 {2:0.00}:1（不足 {3:0}%）\n聊天说明 {4:0.00}:1 · 输入 {5:0.00}:1 · 导航 {6:0.00}:1\n",a.Min(),failA*100,b.Min(),failB*100,Ratios(samples,s,"canvas","canvasBackground",s.Shade).Min(),Ratios(samples,s,"text","input",s.Shade).Min(),Ratios(samples,s,"icons","navigation",s.Shade).Min())+solid+"\n"+(r.RecommendedShade>=0?"建议整体遮罩 "+r.RecommendedShade+"%。":r.Pass?"采样达到 4.5:1，仍需查看微信实际文字。":!fixedPass?"请调整气泡、文字、选中项或链接颜色；整体遮罩无法修复这些固定配色。":"请调整分区颜色或不透明度，也可切换深浅色方案。")+"\n按整图 64×64 采样估算每个区域，含分区蒙版；不读取聊天内容，不代表实际文字位置。";return r;
    }
}
