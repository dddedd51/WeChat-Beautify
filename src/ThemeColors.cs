using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class ThemeColors {
    internal static readonly string[] Regions={"navigation","list","canvasBackground","input"};
    internal static readonly string[] Keys={"navigation","list","canvasBackground","input","selection","outgoing","incoming","text","secondary","chat","canvas","selectedText","link","icons","accent"};
    internal static readonly string[] Names={"左侧导航栏","会话列表与搜索区","聊天消息区域","输入区域","选中的会话","发送气泡","接收气泡","会话名称与界面文字","摘要与辅助文字","消息正文（收发共用）","聊天区说明文字","选中会话文字","消息链接（收发共用）","普通图标","强调色图标"};
    readonly Dictionary<string,string> colors=new Dictionary<string,string>();
    readonly Dictionary<string,int> opacity=new Dictionary<string,int>();
    static string Id(bool dark,string key){return (dark?"dark.":"light.")+key;}
    internal static bool IsColor(string value){return Regex.IsMatch(value??"","^#[0-9a-fA-F]{6}$");}
    internal ThemeColors Copy(){var c=new ThemeColors();foreach(var p in colors)c.colors[p.Key]=p.Value;foreach(var p in opacity)c.opacity[p.Key]=p.Value;return c;}
    internal bool Customized(bool dark,string key){return colors.ContainsKey(Id(dark,key))||opacity.ContainsKey(Id(dark,key));}
    internal void Set(bool dark,string key,string color){if(Array.IndexOf(Keys,key)<0||!IsColor(color))throw new InvalidDataException("请输入 #RRGGBB 格式的颜色。");colors[Id(dark,key)]=color.ToUpperInvariant();}
    internal void SetOpacity(bool dark,string key,int value){if(Array.IndexOf(Regions,key)<0||value<0||value>100)throw new InvalidDataException("区域不透明度须为 0–100。");opacity[Id(dark,key)]=value;}
    internal void Reset(bool dark,string key){colors.Remove(Id(dark,key));opacity.Remove(Id(dark,key));}
    internal void ResetMode(bool dark){foreach(string key in Keys)Reset(dark,key);}
    internal int Opacity(bool dark,string key){int value;return opacity.TryGetValue(Id(dark,key),out value)?value:0;}
    internal string Get(GlassSettings s,string key){string c;if(colors.TryGetValue(Id(s.Dark,key),out c))return c;
        switch(key){
            case "navigation":case "list":case "canvasBackground":case "input":return s.Dark?"#1C2128":"#E8EEF4";
            case "selection":case "accent":return s.Accent;
            case "outgoing":return s.Dark?"#36546C":"#BBD9ED";
            case "incoming":return s.Dark?"#343D48":"#F2F5F9";
            case "secondary":return s.Dark?"#B1BDCA":"#617083";
            case "link":return s.Dark?"#B8DCFF":"#285A87";
            case "selectedText":return Readability.SelectionInk(Get(s,"selection"));
            default:return s.Dark?"#EAF0F6":"#202C3B";
        }
    }
    internal string Serialize(){var b=new StringBuilder();foreach(var p in colors)b.AppendLine("color."+p.Key+"="+p.Value);foreach(var p in opacity)b.AppendLine("opacity."+p.Key+"="+p.Value);return b.ToString();}
    internal static ThemeColors Load(Dictionary<string,string> data){var c=new ThemeColors();foreach(bool dark in new[]{false,true})foreach(string key in Keys){string value=Files.Get(data,"color."+Id(dark,key));if(IsColor(value))c.Set(dark,key,value);int n;if(Array.IndexOf(Regions,key)>=0&&Int32.TryParse(Files.Get(data,"opacity."+Id(dark,key)),out n)&&n>=0&&n<=100)c.SetOpacity(dark,key,n);}return c;}
    internal static string Mask(GlassSettings s,string key){int opacity=s.Colors.Opacity(s.Dark,key);if(opacity==0)return "";string color=s.Colors.Get(s,key),dir=Path.Combine(Files.Data,"color-masks");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"v2-"+color.Substring(1)+"-"+opacity+".png");if(!File.Exists(path))using(var b=new Bitmap(8,8,PixelFormat.Format32bppArgb)){var pixel=Color.FromArgb((opacity*255+50)/100,ColorTranslator.FromHtml(color));for(int y=0;y<8;y++)for(int x=0;x<8;x++)b.SetPixel(x,y,pixel);string stage=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{b.Save(stage,ImageFormat.Png);if(!File.Exists(path))File.Move(stage,path);}finally{if(File.Exists(stage))File.Delete(stage);}}return path;}
}
