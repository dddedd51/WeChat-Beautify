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
    public string DllVersion;
    public ushort Machine;
    public AdapterProfile Adapter;
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
                if(!File.Exists(t.Dll))t.Error="没有找到对应版本的 Weixin.dll。";
                else {
                    t.Hash=Files.Hash(t.Dll);t.Machine=Machine(t.Dll);t.DllVersion=FileVersionInfo.GetVersionInfo(t.Dll).FileVersion;
                    if(Machine(t.Exe)!=0x8664)t.Error="请选择 64 位 Windows 微信。";
                    else if(t.DllVersion!=t.Version)t.Error="微信启动程序与核心模块版本不一致，请完成微信更新后重新检测。";
                    else {t.Adapter=Adapters.Resolve(t.Version,t.Machine,t.Hash,out t.Error);t.Compatible=t.Adapter!=null;}
                }
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
    sealed class Session {internal int Pid;internal AdapterProfile Adapter;internal string Runtime {get{return Adapter.Runtime;}}}
    internal static string Connection(){try{var session=OwnedSession();if(session!=null){var r=Files.ReadPairs(Path.Combine(session.Runtime,"theme.receipt"));string state=Files.Get(r,"state");if(Files.Get(r,"pid")==session.Pid.ToString()&&Files.Get(r,"protocol")=="1"){if(state=="incompatible")return "兼容性检查未通过 · 未启用主题";if(IsReady(state))return "已连接 · "+session.Adapter.Family;}return "模块已载入 · 等待引擎回执";}return Weixin.Count()>0?"微信运行中 · 需要用新版重新启动":"微信未运行";}catch(Exception e){return "连接检查失败："+e.Message;}}
    internal static bool IsReady(string state){return state=="ready"||state=="pending"||state=="applied"||state=="disabled";}
    internal static void ValidateRuntime(string path,AdapterProfile adapter){ValidateFile(path,adapter.EntryHash);}
    static void ValidateFile(string path,string hash){if(!File.Exists(path)||Files.Hash(path)!=hash||Weixin.Machine(path)!=0x8664)throw new IOException("主题模块校验失败，请重新构建完整应用。");}
    internal static void ValidatePair(string folder,AdapterProfile adapter){ValidateFile(Path.Combine(folder,"qttestability.dll"),adapter.EntryHash);ValidateFile(Path.Combine(folder,"beautify-engine.dll"),adapter.EngineHash);}
    static Session OwnedSession(){
        string error=null;
        foreach(int pid in ProcessDiscovery.Weixin()){
            foreach(var adapter in Adapters.All.Where(a=>a.CanLoad)){
                string detail;if(ProcessDiscovery.TryModule(pid,Path.Combine(adapter.Runtime,"beautify-engine.dll"),out detail))return new Session{Pid=pid,Adapter=adapter};
                if(detail!=null)error=detail;
            }
        }
        if(error!=null)throw new IOException(error);return null;
    }
    internal static bool OwnedProcess(){return OwnedSession()!=null;}
    internal static string Launch(GlassSettings s){
        string contextProblem=LaunchContext.Problem();if(contextProblem!=null)throw new IOException(contextProblem);
        var t=Weixin.Locate(s.Exe);if(!t.Compatible)throw new IOException(t.Error);
        if(OwnedProcess())return Connection()+"。如检查未通过，请从托盘退出并使用原微信入口启动。";
        if(Weixin.Count()!=0)throw new IOException("请先从微信托盘菜单完全退出，再点「启动主题微信」。仅关闭窗口不会退出微信。");
        var adapter=t.Adapter;string bundle=adapter.Bundle(Files.Root),runtime=adapter.Runtime;ValidatePair(bundle,adapter);
        if(runtime.Length>225)throw new IOException("应用目录过长，请将应用放在较短的路径。");
        Directory.CreateDirectory(runtime);
        foreach(string name in new[]{"qttestability.dll","beautify-engine.dll"})File.Copy(Path.Combine(bundle,name),Path.Combine(runtime,name),true);
        ValidatePair(runtime,adapter);string snapshot=Config.Prepare(s,true);
        string content=File.ReadAllText(Path.Combine(snapshot,"theme.ini")),revision=Files.Get(Files.ReadPairs(Path.Combine(snapshot,"theme.ini")),"revision");
        string receiptFile=Path.Combine(runtime,"theme.receipt");if(File.Exists(receiptFile))File.Delete(receiptFile);
        Files.Atomic(Path.Combine(runtime,"theme.ini"),content,Encoding.Unicode);LastReceipt=snapshot;
        s.Exe=t.Exe;s.Save();
        var start=new ProcessStartInfo(t.Exe,"--scene=taskbarpins -testability"){UseShellExecute=false,WorkingDirectory=runtime};
        start.EnvironmentVariables["QT_LOAD_TESTABILITY"]="1";start.EnvironmentVariables["PATH"]=runtime+Path.PathSeparator+(Environment.GetEnvironmentVariable("PATH")??"");
        start.WindowStyle=ProcessWindowStyle.Normal;
        using(var p=Process.Start(start)){
            if(p==null)throw new IOException("微信启动失败。");
            string details="utc="+DateTime.UtcNow.ToString("o")+"\npid="+p.Id+"\nworkingDirectory="+runtime+"\nexecutable="+t.Exe+"\narguments="+start.Arguments+"\nadapter="+adapter.Id+"\nfamily="+adapter.Family+"\ntargetVersion="+t.Version+"\ntargetHash="+t.Hash+"\ntestedBuild="+adapter.IsTestedBuild(t.Version,t.Hash)+"\n"+LaunchContext.Diagnostic();
            Files.Atomic(Path.Combine(Files.Data,"launch.log"),details,Encoding.UTF8);
            if(p.WaitForExit(1500)){details+="earlyExit="+p.ExitCode+"\n";Files.Atomic(Path.Combine(Files.Data,"launch.log"),details,Encoding.UTF8);}
        }
        for(int i=0;i<80;i++){
            Thread.Sleep(250);Dictionary<string,string> receipt;try{receipt=Files.ReadPairs(receiptFile);}catch(IOException){continue;}
            if(Files.Get(receipt,"protocol")!="1"||Files.Get(receipt,"revision")!=revision)continue;
            string state=Files.Get(receipt,"state");
            if(state=="incompatible")throw new IOException("此微信构建未通过绘制接口兼容性检查，未启用主题。请从托盘退出，再用原微信入口启动。详情可导出诊断。");
            var session=OwnedSession();if(session==null||session.Adapter!=adapter||Files.Get(receipt,"pid")!=session.Pid.ToString())continue;
            if(state=="failed")throw new IOException("引擎已载入，但背景处理失败；请清除背景或恢复默认。");
            if(IsReady(state))return "引擎已通过结构检查，请完成登录并查看聊天主窗口。"+(adapter.IsTestedBuild(t.Version,t.Hash)?"":" 当前小版本未经实测；显示异常请恢复默认，无法操作时从托盘退出后用原微信入口启动。");
        }
        throw new IOException("微信已收到启动请求，但还没有确认引擎加载。请查看微信窗口或导出诊断。");
    }
    internal static async Task<string> Update(GlassSettings s,bool enabled){
        var session=OwnedSession();
        if(session==null){
            if(enabled)throw new IOException("尚未连接新版主题微信。请从托盘退出微信，再用本客户端启动。");
            return "当前没有本客户端的主题会话。普通微信无需恢复；旧测试会话请退出后重新启动。";
        }
        int pid=session.Pid;string runtime=session.Runtime;
        ValidatePair(runtime,session.Adapter);
        var current=Files.ReadPairs(Path.Combine(runtime,"theme.receipt"));
        if(Files.Get(current,"pid")==pid.ToString()&&Files.Get(current,"state")=="incompatible"){
            if(enabled)throw new IOException("此构建未通过兼容性检查，不能启用主题。");
            return "兼容性检查未通过，主题未启用；请从托盘退出，再用原微信入口启动。";
        }
        string snapshot=await Task.Run(()=>Config.Prepare(s,enabled));
        string config=Path.Combine(runtime,"theme.ini"),previous=File.Exists(config)?File.ReadAllText(config):Config.Build(s,false);
        string content=File.ReadAllText(Path.Combine(snapshot,"theme.ini")),revision=Files.Get(Files.ReadPairs(Path.Combine(snapshot,"theme.ini")),"revision");
        Files.Atomic(config,content,Encoding.Unicode);LastReceipt=snapshot;
        try{
            for(int i=0;i<50;i++){
                await Task.Delay(200);Dictionary<string,string> r;
                try{r=Files.ReadPairs(Path.Combine(runtime,"theme.receipt"));}catch(IOException){continue;}
                if(Files.Get(r,"protocol")!="1"||Files.Get(r,"revision")!=revision||Files.Get(r,"pid")!=pid.ToString())continue;
                string state=Files.Get(r,"state");
                if(state=="failed")throw new IOException("背景解码失败。");
                if(state==(enabled?"applied":"disabled")){
                    if(enabled&&!String.IsNullOrEmpty(s.Wallpaper)&&Files.Get(r,"wallLoaded")!="1")throw new IOException("微信没有确认图片加载。");
                    s.Save();File.Copy(Path.Combine(runtime,"theme.receipt"),Path.Combine(snapshot,"theme.receipt"),true);
                    return enabled?"微信已读取这次主题，并开始原生绘制。请查看实际效果。":"已恢复微信默认外观；从托盘退出后引擎完全卸载。";
                }
            }
            throw new IOException(enabled?"尚未收到这次主题的绘制回执。请登录并保持聊天主窗口可见。":"已写入停用配置，但尚未确认恢复。请从托盘退出微信，再用原微信入口启动。");
        }catch{
            // A failed recovery must never restore the enabled configuration.
            if(enabled)Files.Atomic(config,previous,Encoding.Unicode);throw;
        }
    }
    internal static string Diagnosis(TargetInfo t){
        var b=new StringBuilder("微信美化 0.7.1-preview.1 / version family adapters\n");
        b.AppendLine("UTC="+DateTime.UtcNow.ToString("o")).AppendLine("Version="+t?.Version).AppendLine("DllVersion="+t?.DllVersion).AppendLine("Machine="+t?.Machine.ToString("x")).AppendLine("SHA256="+t?.Hash).AppendLine("Adapter="+t?.Adapter?.Id).AppendLine("TestedBuild="+(t?.Adapter?.IsTestedBuild(t.Version,t.Hash)??false)).AppendLine("DetectionError="+t?.Error).AppendLine("Connection="+Connection());
        b.Append(ProcessDiscovery.Diagnostic());
        b.Append(LaunchContext.Diagnostic());
        foreach(var adapter in Adapters.All){
            b.AppendLine("AdapterProfile="+adapter.Id+" / "+adapter.Status+" / family="+adapter.Family+" / reference="+adapter.TargetHash);
            if(!adapter.CanLoad)continue;
            b.AppendLine("EntryHash="+adapter.EntryHash).AppendLine("EngineHash="+adapter.EngineHash);
            string file=Path.Combine(adapter.Runtime,"theme.receipt");if(File.Exists(file))b.AppendLine(File.ReadAllText(file));
        }
        return b.ToString();
    }
}
