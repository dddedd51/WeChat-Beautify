using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

internal sealed class TargetInfo {
    public string Exe, Dll, Version, Hash, Error;
    public bool Compatible;
}
internal static class Files {
    internal static string Root = AppDomain.CurrentDomain.BaseDirectory;
    internal static string Data { get { return Path.Combine(Root,"data"); } }
    internal static string Runtime { get { return Path.Combine(Data,"runtime"); } }
    internal static void Atomic(string file,string text,Encoding encoding) {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temp,text,encoding);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    internal static string Hash(string path) {using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    internal static Dictionary<string,string> ReadPairs(string path) {
        var d=new Dictionary<string,string>();if(!File.Exists(path))return d;
        foreach(string l in File.ReadAllLines(path)){int p=l.IndexOf('=');if(p>0)d[l.Substring(0,p).Trim()]=l.Substring(p+1).Trim();}return d;
    }
    internal static string Get(Dictionary<string,string> d,string key) {string v;return d.TryGetValue(key,out v)?v:"";}
}
internal static class Weixin {
    internal const string ExpectedHash="e0428a7b691853de496532523c2c09aa095f90941c37c9f4cfb3f137f3c66556";
    internal static TargetInfo Locate(string manual) {
        var candidates=new List<string>();if(!String.IsNullOrWhiteSpace(manual))candidates.Add(manual);
        if(String.IsNullOrWhiteSpace(manual)){
            foreach(int pid in ProcessDiscovery.Weixin())try{candidates.Add(ProcessDiscovery.ImagePath(pid));}catch{}
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tencent","Weixin","Weixin.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Tencent","Weixin","Weixin.exe"));
        }
        TargetInfo failed=null;
        foreach(string path in candidates.Distinct(StringComparer.OrdinalIgnoreCase)){
            try {
                if(!File.Exists(path))continue;
                var t=new TargetInfo{Exe=Path.GetFullPath(path)};
                if(!String.Equals(Path.GetFileName(path),"Weixin.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("请选择 Weixin.exe。");
                t.Version=FileVersionInfo.GetVersionInfo(path).FileVersion;
                t.Dll=Path.Combine(Path.GetDirectoryName(t.Exe),t.Version??"","Weixin.dll");
                if(!File.Exists(t.Dll))t.Dll=Path.Combine(Path.GetDirectoryName(t.Exe),"Weixin.dll");
                if(t.Version!="4.1.15.56")t.Error="仅适配微信 4.1.15.56 x64，当前版本："+t.Version;
                else if(!File.Exists(t.Dll))t.Error="没有找到对应版本的 Weixin.dll。";
                else {t.Hash=Files.Hash(t.Dll);t.Compatible=t.Hash==ExpectedHash&&Machine(t.Dll)==0x8664;if(!t.Compatible)t.Error="微信文件与已适配版本不一致，已拒绝加载。";}
                if(t.Compatible)return t;failed=t;
            }catch(Exception e){failed=new TargetInfo{Exe=path,Error=e.Message};}
        }
        return failed??new TargetInfo{Error="没有找到微信，请手动选择 Weixin.exe。"};
    }
    internal static ushort Machine(string path){using(var s=File.OpenRead(path))using(var r=new BinaryReader(s)){if(r.ReadUInt16()!=0x5a4d)throw new InvalidDataException("不是 PE 文件。");s.Position=60;int offset=r.ReadInt32();if(offset<64||offset>s.Length-6)throw new InvalidDataException("PE 头无效。");s.Position=offset;if(r.ReadUInt32()!=0x4550)throw new InvalidDataException("PE 签名无效。");return r.ReadUInt16();}}
    internal static int Count(){return ProcessDiscovery.Weixin().Length;}
}
internal sealed class GlassSettings {
    public int Density=65;public bool Dark;
    public ThemeColors Colors=new ThemeColors();
    public string Accent="#78AAE0", Exe="", Wallpaper="", WallpaperName="";
    public int Blur=8, Shade=28;
    public double CropX=0,CropY=0,CropW=1,CropH=1;
    internal string CropKey{get{return String.Join(",",new[]{CropX.ToString("R",CultureInfo.InvariantCulture),CropY.ToString("R",CultureInfo.InvariantCulture),CropW.ToString("R",CultureInfo.InvariantCulture),CropH.ToString("R",CultureInfo.InvariantCulture)});}}
    internal void SetCrop(System.Windows.Rect crop){CropX=crop.X;CropY=crop.Y;CropW=crop.Width;CropH=crop.Height;}
    internal System.Windows.Rect Crop{get{return new System.Windows.Rect(CropX,CropY,CropW,CropH);}}
    internal void Validate(){if(Density<20||Density>95)throw new InvalidDataException("磨砂浓度须为 20–95。");if(Blur<0||Blur>30||Shade<0||Shade>85)throw new InvalidDataException("背景效果参数超出范围。");foreach(double n in new[]{CropX,CropY,CropW,CropH})if(Double.IsNaN(n)||Double.IsInfinity(n))throw new InvalidDataException("裁剪区域无效。");if(CropX<0||CropY<0||CropW<.01||CropH<.01||CropX+CropW>1.000001||CropY+CropH>1.000001)throw new InvalidDataException("裁剪区域超出图片。");if(!System.Text.RegularExpressions.Regex.IsMatch(Accent??"","^#[0-9A-Fa-f]{6}$"))throw new InvalidDataException("强调色无效。");foreach(string p in new[]{Exe,Wallpaper,WallpaperName})if(p!=null&&p.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("路径或名称包含无效字符。");}
    internal void Save(){Validate();Files.Atomic(Path.Combine(Files.Data,"settings.ini"),"density="+Density+"\ndark="+(Dark?1:0)+"\naccent="+Accent+"\nexe="+Exe+"\nwallpaper="+Wallpaper+"\nwallpaperName="+WallpaperName+"\nblur="+Blur+"\nshade="+Shade+"\ncrop="+CropKey+"\n"+Colors.Serialize(),Encoding.UTF8);}
    internal static GlassSettings Load(){var s=new GlassSettings();var d=Files.ReadPairs(Path.Combine(Files.Data,"settings.ini"));int value;if(Int32.TryParse(Files.Get(d,"density"),out value))s.Density=Math.Max(20,Math.Min(95,value));s.Dark=Files.Get(d,"dark")=="1";string accent=Files.Get(d,"accent");if(System.Text.RegularExpressions.Regex.IsMatch(accent,"^#[0-9A-Fa-f]{6}$"))s.Accent=accent;s.Exe=Files.Get(d,"exe");s.Wallpaper=Files.Get(d,"wallpaper");s.WallpaperName=Files.Get(d,"wallpaperName");if(Int32.TryParse(Files.Get(d,"blur"),out value))s.Blur=Math.Max(0,Math.Min(30,value));if(Int32.TryParse(Files.Get(d,"shade"),out value))s.Shade=Math.Max(0,Math.Min(85,value));var crop=Files.Get(d,"crop").Split(',');if(crop.Length==4){double[] a=new double[4];bool ok=true;for(int i=0;i<4;i++)ok&=Double.TryParse(crop[i],NumberStyles.Float,CultureInfo.InvariantCulture,out a[i])&&!Double.IsNaN(a[i])&&!Double.IsInfinity(a[i]);if(ok&&a[0]>=0&&a[1]>=0&&a[2]>=.01&&a[3]>=.01&&a[0]+a[2]<=1.000001&&a[1]+a[3]<=1.000001){s.CropX=a[0];s.CropY=a[1];s.CropW=a[2];s.CropH=a[3];}}s.Colors=ThemeColors.Load(d);return s;}
}
internal static class Config {
    internal static string Build(GlassSettings s,bool enabled,string wall="") {
        bool useGlass=enabled&&String.IsNullOrEmpty(s.Wallpaper);
        if(enabled&&!String.IsNullOrEmpty(s.Wallpaper)&&String.IsNullOrEmpty(wall))throw new InvalidDataException("图片尚未处理，拒绝生成无图配置。");
        s.Validate();var b=new StringBuilder();b.AppendLine("[runtime]").AppendLine("enabled="+(enabled?1:0)).AppendLine("routes=0");
        b.AppendLine("[glass]").AppendLine("enabled="+(useGlass?1:0)).AppendLine("density="+s.Density).AppendLine("dark="+(s.Dark?1:0));
        b.AppendLine("[palette]");
        for(int i=0;i<16;i++){
            string[] slots={null,"list","canvasBackground","input","navigation","selection","outgoing","incoming"};
            string color=slots[i/2]==null?(s.Dark?"#1C2128":"#E8EEF4"):s.Colors.Get(s,slots[i/2]);
            b.AppendLine("color"+i+"="+color);
        }
        foreach(string mode in new[]{"light","dark"}){
            string ink=s.Colors.Get(s,"text");
            b.AppendLine("["+mode+"]").AppendLine("text="+ink).AppendLine("chat="+s.Colors.Get(s,"chat")).AppendLine("canvas="+s.Colors.Get(s,"canvas"))
                .AppendLine("secondary="+s.Colors.Get(s,"secondary")).AppendLine("selectedText="+s.Colors.Get(s,"selectedText"))
                .AppendLine("link="+s.Colors.Get(s,"link")).AppendLine("icons="+s.Colors.Get(s,"icons")).AppendLine("accent="+s.Colors.Get(s,"accent"))
                .AppendLine("iconMode=2").AppendLine("brightness=100").AppendLine("saturation=100").AppendLine("contrast=100")
                .AppendLine("visibility=0").AppendLine("inputFull=0").AppendLine("wall="+(enabled?wall:"")).AppendLine("inputBorder=");
            string[] regions={"list","canvasBackground","input",null,"navigation"};
            for(int i=0;i<5;i++)b.AppendLine("mask"+i+"="+(enabled&&regions[i]!=null?ThemeColors.Mask(s,regions[i]):""));for(int i=0;i<3;i++)b.AppendLine("frost"+i+"=");
        }
        return b.ToString();
    }
    internal static string Prepare(GlassSettings s,bool enabled){
        string folder=Path.Combine(Files.Data,"native-deployment","previews",DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"routes.bin"),Encoding.ASCII.GetBytes("WXRTE002").Concat(new byte[4]).ToArray());
        string wall=enabled?Wallpaper.Process(s):"";
        if(wall.Length>980)throw new IOException("图片缓存路径过长，请将应用放在较短的目录。");
        Files.Atomic(Path.Combine(folder,"runtime.ini"),Build(s,enabled,wall),Encoding.Unicode);return folder;
    }
}
internal static class Engine {
    [DllImport("user32.dll",SetLastError=true)]static extern bool PostMessageW(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
    internal static string LastReceipt="";
    internal static string Connection(){try{if(OwnedProcess())return "主题模块已加载";return Weixin.Count()>0?"普通微信正在运行 · 未加载主题":"微信未运行";}catch(Exception e){return "连接检查未完成："+e.Message;}}
    internal static void ValidateRuntime(string path){if(!File.Exists(path)||Files.Hash(path)!=RuntimeIdentity.Hash||Weixin.Machine(path)!=0x8664)throw new IOException("主题模块校验失败，请重新构建完整应用。");}
    internal static string Launch(GlassSettings s){
        var t=Weixin.Locate(s.Exe);if(!t.Compatible)throw new IOException(t.Error);
        if(OwnedProcess())return "主题微信已经运行。登录主窗口后点击「更新主题」即可。";
        if(Weixin.Count()!=0)throw new IOException("检测到普通微信仍在运行。请从微信托盘菜单选择「退出」（仅关闭窗口不会退出），然后再点「启动主题微信」。");
        string bundle=Path.Combine(Files.Root,"native-runtime","qttestability.dll");ValidateRuntime(bundle);
        Directory.CreateDirectory(Files.Runtime);string dll=Path.Combine(Files.Runtime,"qttestability.dll");File.Copy(bundle,dll,true);ValidateRuntime(dll);
        string folder=Config.Prepare(s,true);
        File.Copy(Path.Combine(folder,"runtime.ini"),Path.Combine(Files.Runtime,"runtime.ini"),true);
        File.Copy(Path.Combine(folder,"routes.bin"),Path.Combine(Files.Runtime,"routes.bin"),true);
        Files.Atomic(Path.Combine(Files.Runtime,"preview.path"),Path.Combine(folder,"runtime.ini"),Encoding.Unicode);
        LastReceipt=folder;
        s.Exe=t.Exe;s.Save();
        var start=new ProcessStartInfo(t.Exe,"--scene=taskbarpins -testability"){UseShellExecute=false,WorkingDirectory=Files.Runtime};
        start.EnvironmentVariables["QT_LOAD_TESTABILITY"]="1";
        start.EnvironmentVariables["PATH"]=Files.Runtime+Path.PathSeparator+(Environment.GetEnvironmentVariable("PATH")??"");
        using(var p=Process.Start(start)){if(p==null)throw new IOException("微信启动失败。");Files.Atomic(Path.Combine(Files.Data,"launch.log"),"utc="+DateTime.UtcNow.ToString("o")+"\npid="+p.Id+"\narguments="+start.Arguments+"\nworkingDirectory="+start.WorkingDirectory+"\n",Encoding.UTF8);}
        for(int i=0;i<80;i++){Thread.Sleep(250);if(OwnedProcess())return "主题模块已加载。请登录微信主窗口后点击「更新主题」，确认绘制回执。";}
        Files.Atomic(Path.Combine(Files.Data,"launch-diagnose.txt"),Diagnosis(t),Encoding.UTF8);
        throw new IOException("微信启动请求已发出，但没有检测到主题模块。当前不能确认主题生效。请退出所有微信后只使用本应用启动，不要再点普通微信入口。");
    }
    internal static bool OwnedProcess(){
        string error=null;
        foreach(int pid in ProcessDiscovery.Weixin()){
            string detail;if(ProcessDiscovery.TryModule(pid,Path.Combine(Files.Runtime,"qttestability.dll"),out detail))return true;
            if(detail!=null)error=detail;
        }
        if(error!=null)throw new IOException(error);
        return false;
    }
    internal static void Request(){
        var ready=Files.ReadPairs(Path.Combine(Files.Runtime,"control-ready.txt"));int pid;long h;
        if(!Int32.TryParse(Files.Get(ready,"pid"),out pid)||!Int64.TryParse(Files.Get(ready,"window"),out h))throw new IOException("等待微信登录并进入主窗口后，再更新主题。");
        uint actual;var window=new IntPtr(h);GetWindowThreadProcessId(window,out actual);if(actual!=(uint)pid)throw new IOException("主题连接已失效，请重新启动毛玻璃模式。");
        if(!ProcessDiscovery.HasModule(pid,Path.Combine(Files.Runtime,"qttestability.dll")))throw new IOException("当前微信不属于本应用的主题会话。");
        if(!PostMessageW(window,0x84e2,IntPtr.Zero,IntPtr.Zero))throw new IOException("无法发送主题更新："+Marshal.GetLastWin32Error());
    }
    internal static async Task<string> Update(GlassSettings s,bool enabled){
        if(!OwnedProcess()){
            if(enabled)throw new IOException("请先退出普通微信，再用本应用启动毛玻璃模式。");
            Directory.CreateDirectory(Files.Runtime);Files.Atomic(Path.Combine(Files.Runtime,"runtime.ini"),Config.Build(s,false),Encoding.Unicode);
            string disabled=Config.Prepare(s,false);Files.Atomic(Path.Combine(Files.Runtime,"preview.path"),Path.Combine(disabled,"runtime.ini"),Encoding.Unicode);
            return "已停用保存的主题。普通微信无需恢复。";
        }
        if(enabled&&Files.Hash(Path.Combine(Files.Runtime,"qttestability.dll"))!=RuntimeIdentity.Hash)throw new IOException("主题引擎已更新。请从托盘退出微信，再用本应用启动，以启用分区配色。原主题可先用「恢复默认」停用。");
        string folder=await Task.Run(()=>Config.Prepare(s,enabled)),control=Path.Combine(Files.Runtime,"preview.path");
        string previous=File.Exists(control)?File.ReadAllText(control):Path.Combine(Files.Runtime,"runtime.ini");
        Files.Atomic(control,Path.Combine(folder,"runtime.ini"),Encoding.Unicode);LastReceipt=folder;
        try {
            Request();
            for(int i=0;i<60;i++){
                await Task.Delay(200);
                Dictionary<string,string> g,p;try{g=Files.ReadPairs(Path.Combine(folder,"glass.receipt"));p=Files.ReadPairs(Path.Combine(folder,"applied.receipt"));}catch(IOException){continue;}
                string state=Files.Get(g,"state");
                if(!enabled&&state=="disabled") {s.Save();Files.Atomic(Path.Combine(Files.Runtime,"runtime.ini"),Config.Build(s,false),Encoding.Unicode);return "已恢复本次主题修改。退出微信后，模块将完全卸载。";}
                bool wallpaper=!String.IsNullOrEmpty(s.Wallpaper);
                if(enabled&&((!wallpaper&&(state=="failed"||state=="unsupported"))||Files.Get(p,"state")=="failed"))throw new IOException("微信未能应用主题，已请求回退。请导出诊断查看结果。");
                if(enabled&&Files.Get(p,"state")=="applied"&&(wallpaper?Files.Get(p,"wallLoaded")=="1"&&Files.Get(p,"wallRequested")=="1":state=="configured")) {s.Save();return wallpaper?"微信已确认背景图解码与重绘请求。请查看聊天区实际效果。":"Qt 透明背景和 Windows 材质已配置。请查看微信确认实际通透效果。";}
            }
            throw new IOException("等待微信回执超时。请保持主窗口可见；配置请求不代表效果成功。");
        }catch(Exception e){
            string rollback;
            try{Files.Atomic(control,previous,Encoding.Unicode);Request();rollback="已发出恢复上一个配置的请求。";}catch(Exception re){rollback="恢复请求失败："+re.Message+" 请从托盘退出微信，再用原入口启动。";}
            throw new IOException(e.Message+"\n"+rollback,e);
        }
    }
    internal static string Diagnosis(TargetInfo t){
        var b=new StringBuilder();b.AppendLine("微信美化 / experimental 0.4").AppendLine("UTC="+DateTime.UtcNow.ToString("o"))
          .AppendLine("Version="+(t==null?"":t.Version)).AppendLine("Target="+(t==null?"":t.Dll)).AppendLine("SHA256="+(t==null?"":t.Hash))
          .AppendLine("Compatible="+(t!=null&&t.Compatible)).AppendLine("WeixinProcesses="+Weixin.Count()).AppendLine("Connection="+Connection())
          .AppendLine("RuntimeHash="+RuntimeIdentity.Hash).AppendLine("VisualVerification=required");
        b.Append(ProcessDiscovery.Diagnostic());
        foreach(int pid in ProcessDiscovery.Weixin()){
            string error;bool loaded=ProcessDiscovery.TryModule(pid,Path.Combine(Files.Runtime,"qttestability.dll"),out error);
            b.AppendLine("Process="+pid+" ThemeModule="+loaded+" InspectionError="+(error??"none"));
        }
        if(LastReceipt.Length>0)foreach(string name in new[]{"glass.receipt","applied.receipt"}){string p=Path.Combine(LastReceipt,name);if(File.Exists(p))b.AppendLine(name+":").AppendLine(File.ReadAllText(p));}
        return b.ToString();
    }
}
