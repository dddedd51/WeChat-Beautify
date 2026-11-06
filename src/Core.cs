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
    internal static string Build(GlassSettings s,bool enabled,string wall="",string revision="") {
        s.Validate();
        if(enabled&&!String.IsNullOrEmpty(s.Wallpaper)&&String.IsNullOrEmpty(wall))throw new InvalidDataException("图片尚未处理。");
        if(wall.IndexOfAny(new[]{'\r','\n','\0'})>=0||revision.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("配置包含无效字符。");
        var b=new StringBuilder("[theme]\r\n");
        b.AppendLine("enabled="+(enabled?1:0)).AppendLine("revision="+revision).AppendLine("wallpaper="+(enabled?wall:""));
        string[] keys={"navigation","list","canvas","input"},sources={"navigation","list","canvasBackground","input"};
        for(int i=0;i<keys.Length;i++)b.AppendLine(keys[i]+"="+s.Colors.Get(s,sources[i])).AppendLine(keys[i]+"Opacity="+s.Colors.Opacity(s.Dark,sources[i]));
        foreach(var pair in new[]{new[]{"text","text"},new[]{"secondaryText","secondary"},new[]{"selection","selection"},new[]{"selectedText","selectedText"}})b.AppendLine(pair[0]+"="+s.Colors.Get(s,pair[1]));
        return b.ToString();
    }
    internal static string Prepare(GlassSettings s,bool enabled){
        string revision=Guid.NewGuid().ToString("N"),folder=Path.Combine(Files.Data,"snapshots",revision);
        string wall=enabled?Wallpaper.Process(s):"";
        if(wall.Length>980)throw new IOException("图片缓存路径过长，请使用较短的应用目录。");
        Files.Atomic(Path.Combine(folder,"theme.ini"),Build(s,enabled,wall,revision),Encoding.Unicode);return folder;
    }
}
internal static class Engine {
    internal static string LastReceipt="";
    internal static string Runtime {get{return Path.Combine(Files.Data,"engine",RuntimeIdentity.EngineHash.Substring(0,16));}}
    internal static string Connection(){try{if(OwnedProcess())return "自研引擎已连接";return Weixin.Count()>0?"微信运行中 · 需要用新版重新启动":"微信未运行";}catch(Exception e){return "连接检查失败："+e.Message;}}
    internal static void ValidateRuntime(string path){ValidateFile(path,RuntimeIdentity.Hash);}
    static void ValidateFile(string path,string hash){if(!File.Exists(path)||Files.Hash(path)!=hash||Weixin.Machine(path)!=0x8664)throw new IOException("主题模块校验失败，请重新构建完整应用。");}
    static void ValidatePair(string folder){ValidateFile(Path.Combine(folder,"qttestability.dll"),RuntimeIdentity.Hash);ValidateFile(Path.Combine(folder,"beautify-engine.dll"),RuntimeIdentity.EngineHash);}
    static int OwnedPid(){
        string error=null;
        foreach(int pid in ProcessDiscovery.Weixin()){
            string detail;if(ProcessDiscovery.TryModule(pid,Path.Combine(Runtime,"beautify-engine.dll"),out detail))return pid;
            if(detail!=null)error=detail;
        }
        if(error!=null)throw new IOException(error);return 0;
    }
    internal static bool OwnedProcess(){return OwnedPid()!=0;}
    internal static string Launch(GlassSettings s){
        string contextProblem=LaunchContext.Problem();if(contextProblem!=null)throw new IOException(contextProblem);
        var t=Weixin.Locate(s.Exe);if(!t.Compatible)throw new IOException(t.Error);
        if(OwnedProcess())return "主题微信已连接，可直接更新主题。";
        if(Weixin.Count()!=0)throw new IOException("请先从微信托盘菜单完全退出，再点「启动主题微信」。仅关闭窗口不会退出微信。");
        string bundle=Path.Combine(Files.Root,"native-runtime");ValidatePair(bundle);
        if(Runtime.Length>225)throw new IOException("应用目录过长，请将应用放在较短的路径。");
        Directory.CreateDirectory(Runtime);
        foreach(string name in new[]{"qttestability.dll","beautify-engine.dll"})File.Copy(Path.Combine(bundle,name),Path.Combine(Runtime,name),true);
        ValidatePair(Runtime);string snapshot=Config.Prepare(s,true);
        Files.Atomic(Path.Combine(Runtime,"theme.ini"),File.ReadAllText(Path.Combine(snapshot,"theme.ini")),Encoding.Unicode);LastReceipt=snapshot;
        s.Exe=t.Exe;s.Save();
        var start=new ProcessStartInfo(t.Exe,"--scene=taskbarpins -testability"){UseShellExecute=false,WorkingDirectory=Runtime};
        start.EnvironmentVariables["QT_LOAD_TESTABILITY"]="1";start.EnvironmentVariables["PATH"]=Runtime+Path.PathSeparator+(Environment.GetEnvironmentVariable("PATH")??"");
        start.WindowStyle=ProcessWindowStyle.Normal;
        using(var p=Process.Start(start)){
            if(p==null)throw new IOException("微信启动失败。");
            string details="utc="+DateTime.UtcNow.ToString("o")+"\npid="+p.Id+"\nworkingDirectory="+Runtime+"\nexecutable="+t.Exe+"\narguments="+start.Arguments+"\n"+LaunchContext.Diagnostic();
            Files.Atomic(Path.Combine(Files.Data,"launch.log"),details,Encoding.UTF8);
            if(p.WaitForExit(1500)){details+="earlyExit="+p.ExitCode+"\n";Files.Atomic(Path.Combine(Files.Data,"launch.log"),details,Encoding.UTF8);}
        }
        for(int i=0;i<80;i++){Thread.Sleep(250);if(OwnedProcess())return "自研引擎已加载。请完成登录，进入聊天主窗口后即可看到主题。";}
        throw new IOException("微信已收到启动请求，但还没有确认引擎加载。请查看微信窗口或导出诊断。");
    }
    internal static async Task<string> Update(GlassSettings s,bool enabled){
        int pid=OwnedPid();
        if(pid==0){
            if(enabled)throw new IOException("尚未连接新版主题微信。请从托盘退出微信，再用本客户端启动。");
            return "当前没有本客户端的主题会话。普通微信无需恢复；旧测试会话请退出后重新启动。";
        }
        ValidatePair(Runtime);
        string snapshot=await Task.Run(()=>Config.Prepare(s,enabled));
        string config=Path.Combine(Runtime,"theme.ini"),previous=File.Exists(config)?File.ReadAllText(config):Config.Build(s,false);
        string content=File.ReadAllText(Path.Combine(snapshot,"theme.ini")),revision=Files.Get(Files.ReadPairs(Path.Combine(snapshot,"theme.ini")),"revision");
        Files.Atomic(config,content,Encoding.Unicode);LastReceipt=snapshot;
        try{
            for(int i=0;i<50;i++){
                await Task.Delay(200);Dictionary<string,string> r;
                try{r=Files.ReadPairs(Path.Combine(Runtime,"theme.receipt"));}catch(IOException){continue;}
                if(Files.Get(r,"revision")!=revision||Files.Get(r,"pid")!=pid.ToString())continue;
                string state=Files.Get(r,"state");
                if(state=="failed")throw new IOException("背景解码失败。");
                if(state==(enabled?"applied":"disabled")){
                    if(enabled&&!String.IsNullOrEmpty(s.Wallpaper)&&Files.Get(r,"wallLoaded")!="1")throw new IOException("微信没有确认图片加载。");
                    s.Save();File.Copy(Path.Combine(Runtime,"theme.receipt"),Path.Combine(snapshot,"theme.receipt"),true);
                    return enabled?"微信已读取这次主题，并开始原生绘制。请查看实际效果。":"已恢复微信默认外观；从托盘退出后引擎完全卸载。";
                }
            }
            throw new IOException("尚未收到这次主题的绘制回执。请登录并保持聊天主窗口可见。");
        }catch{
            Files.Atomic(config,previous,Encoding.Unicode);throw;
        }
    }
    internal static string Diagnosis(TargetInfo t){
        var b=new StringBuilder("微信美化 0.5 / independent native paint\n");
        b.AppendLine("UTC="+DateTime.UtcNow.ToString("o")).AppendLine("Version="+t?.Version).AppendLine("SHA256="+t?.Hash).AppendLine("Connection="+Connection()).AppendLine("EntryHash="+RuntimeIdentity.Hash).AppendLine("EngineHash="+RuntimeIdentity.EngineHash);
        b.Append(ProcessDiscovery.Diagnostic());
        b.Append(LaunchContext.Diagnostic());
        string file=Path.Combine(Runtime,"theme.receipt");if(File.Exists(file))b.AppendLine(File.ReadAllText(file));
        return b.ToString();
    }
}
