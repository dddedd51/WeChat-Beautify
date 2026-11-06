using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

internal static class App {
    [STAThread]static int Main(string[] args){
        try{
            if(args.Length>0&&args[0]=="--diagnose"){Directory.CreateDirectory(Files.Data);File.WriteAllText(Path.Combine(Files.Data,"diagnose.txt"),Engine.Diagnosis(Weixin.Locate(null)));return 0;}
            if(args.Length>0&&args[0]=="--self-test")return SelfTest.Run(args.Length>1?args[1]:Path.Combine(Files.Root,"self-test"));
            bool first;using(var mutex=new Mutex(true,"Local\\WeChatLiuliController",out first)){
                if(!first){MessageBox.Show("微信美化已经打开。","微信美化");return 0;}
                var app=new Application();app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"微信美化",MessageBoxButton.OK,MessageBoxImage.Warning);e.Handled=true;};
                var controller=new Controller();app.Run(controller.Window);return 0;
            }
        }catch(Exception e){if(args.Length>0)File.WriteAllText(Path.Combine(Files.Root,"self-test-error.txt"),e.ToString());else MessageBox.Show(e.Message,"微信美化");return 1;}
    }
}
internal sealed class Controller {
    internal Window Window;
    GlassSettings settings;TargetInfo target;bool busy,initialized;string manual;
    DispatcherTimer previewTimer=new DispatcherTimer();int previewGeneration;bool previewRunning,previewPending;
    int recommendedShade=-1;readonly PalettePreview palettePreview=new PalettePreview();
    readonly ShadeUndo shadeUndo=new ShadeUndo();string draftPath,draftName;bool syncingZoom;
    T Find<T>(string name)where T:class{return Window.FindName(name)as T;}
    internal static Window LoadWindow(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))return (Window)XamlReader.Load(s);}
    internal Controller(){
        Window=LoadWindow();settings=GlassSettings.Load();manual=settings.Exe;
        PopulateCompatibility(Window);
        if(!File.Exists(Path.Combine(Files.Data,"settings.ini"))){ThemePresets.Mint(settings);settings.Save();}
        foreach(string page in new[]{"Theme","Background","Colors","Guide"}){string name=page;Find<RadioButton>("Nav"+name).Checked+=(s,e)=>ShowPage(name);}
        Find<Button>("Mint").Click+=async(s,e)=>await Work(async()=>{ClearShadeUndo();var next=Read();await Task.Run(()=>ThemePresets.Mint(next));settings=next;Find<CheckBox>("Dark").IsChecked=false;Find<Slider>("ImageBlur").Value=settings.Blur;Find<Slider>("ImageShade").Value=settings.Shade;settings.Save();Preview();SetStatus("青屿主题已准备好。点击「更新主题」应用到微信。",false);});
        Find<Border>("Preview").Child=palettePreview;palettePreview.Cursor=System.Windows.Input.Cursors.Hand;palettePreview.Pick+=OpenColors;Find<Button>("EditColors").Click+=(s,e)=>OpenColors(null);
        Find<Slider>("Density").Value=settings.Density;Find<CheckBox>("Dark").IsChecked=settings.Dark;
        Find<Slider>("ImageBlur").Value=settings.Blur;Find<Slider>("ImageShade").Value=settings.Shade;
        previewTimer.Interval=TimeSpan.FromMilliseconds(250);previewTimer.Tick+=async(s,e)=>{previewTimer.Stop();await RenderWallpaper();};
        Find<Slider>("ImageBlur").ValueChanged+=(s,e)=>Preview();Find<Slider>("ImageShade").ValueChanged+=(s,e)=>{shadeUndo.Observe((int)e.NewValue);ShowShadeUndo();Preview();};
        Find<Button>("ChooseImage").Click+=async(s,e)=>{
            var d=new OpenFileDialog{Title="选择微信背景图片",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true};
            if(d.ShowDialog(Window)==true)await Work(async()=>{string path=await Task.Run(()=>Wallpaper.Import(d.FileName));BeginComposition(path,Path.GetFileName(d.FileName),new Rect(0,0,1,1));});
        };
        Find<Button>("CropImage").Click+=(s,e)=>{try{BeginComposition(settings.Wallpaper,settings.WallpaperName,settings.Crop);}catch(Exception ex){SetStatus(ex.Message,true);}};
        Find<Button>("SaveComposition").Click+=(s,e)=>FinishComposition(true);
        Find<Button>("CancelComposition").Click+=(s,e)=>FinishComposition(false);
        Find<Button>("ResetComposition").Click+=(s,e)=>palettePreview.ResetComposition();
        Find<Slider>("CompositionZoom").ValueChanged+=(s,e)=>{if(!syncingZoom)palettePreview.ZoomComposition(e.NewValue);};
        palettePreview.CompositionChanged+=SyncComposition;
        Find<Button>("ImproveContrast").Click+=(s,e)=>{if(recommendedShade>=0){shadeUndo.Capture((int)Find<Slider>("ImageShade").Value,recommendedShade);Find<Slider>("ImageShade").Value=recommendedShade;ShowShadeUndo();Read().Save();}};
        Find<Button>("UndoContrast").Click+=(s,e)=>{if(shadeUndo.Take(out int before)){Find<Slider>("ImageShade").Value=before;Read().Save();ShowShadeUndo();SetStatus("已撤销建议遮罩。点击「更新主题」应用到微信。",false);}};
        Find<Button>("ClearImage").Click+=(s,e)=>{ClearShadeUndo();settings.Wallpaper="";settings.WallpaperName="";Read().Save();Preview();SetStatus("已切换为分区纯色。点击「更新主题」应用。",false);};
        Find<Slider>("Density").ValueChanged+=(s,e)=>Preview();Find<CheckBox>("Dark").Checked+=(s,e)=>Preview();Find<CheckBox>("Dark").Unchecked+=(s,e)=>Preview();
        Find<Button>("Blue").Click+=(s,e)=>Accent("#78AAE0","雾蓝");Find<Button>("Green").Click+=(s,e)=>Accent("#65B5A1","青玉");Find<Button>("Purple").Click+=(s,e)=>Accent("#A094D9","鸢尾");
        Find<Button>("Detect").Click+=async(s,e)=>await Detect();
        Find<Button>("Choose").Click+=async(s,e)=>{var d=new OpenFileDialog{Title="选择微信 Weixin.exe",Filter="微信程序|Weixin.exe",CheckFileExists=true};if(d.ShowDialog(Window)==true){manual=d.FileName;await Detect();}};
        Find<Button>("Launch").Click+=async(s,e)=>await Work(async()=>{var state=Read();SetStatus(await Task.Run(()=>Engine.Launch(state)),false);});
        Find<Button>("Apply").Click+=async(s,e)=>await Work(async()=>SetStatus(await Engine.Update(Read(),true),false));
        Find<Button>("Restore").Click+=async(s,e)=>await Work(async()=>SetStatus(await Engine.Update(Read(),false),false));
        Find<Button>("Report").Click+=(s,e)=>{var d=new SaveFileDialog{FileName="微信美化诊断.txt",Filter="文本|*.txt"};if(d.ShowDialog(Window)==true){File.WriteAllText(d.FileName,Engine.Diagnosis(target),Encoding.UTF8);SetStatus("诊断已导出，不包含聊天内容；包含本机路径。",false);}};
        Find<Button>("CheckUpdates").Click+=(s,e)=>OpenLink("https://github.com/dddedd51/WeChat-Beautify/releases");
        Find<Button>("OpenGitHub").Click+=(s,e)=>OpenLink("https://github.com/dddedd51/WeChat-Beautify");
        Find<Button>("OpenBilibili").Click+=(s,e)=>OpenLink("https://www.bilibili.com/video/BV1ag4i6xEco");
        Window.Loaded+=async(s,e)=>{initialized=true;Preview();await Detect();};
        Window.Closing+=(s,e)=>{if(busy){e.Cancel=true;SetStatus("请等待当前操作完成后关闭。",false);return;}previewTimer.Stop();previewGeneration++;initialized=false;try{Read().Save();}catch{}};
    }
    void ShowPage(string page){
        if(palettePreview.Editing&&page!="Background")FinishComposition(false);
        Find<ScrollViewer>("SettingsScroll").VerticalScrollBarVisibility=page=="Theme"?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
        Find<ScrollViewer>("SettingsScroll").ScrollToTop();
        Find<Grid>("Studio").Visibility=page=="Guide"?Visibility.Collapsed:Visibility.Visible;
        Find<Border>("GuidePage").Visibility=page=="Guide"?Visibility.Visible:Visibility.Collapsed;
        foreach(string p in new[]{"Theme","Background","Colors"})Find<StackPanel>(p+"Page").Visibility=page==p?Visibility.Visible:Visibility.Collapsed;
        Find<TextBlock>("PageTitle").Text=page=="Background"?"把喜欢的风景，放进对话":page=="Colors"?"每一处，都有自己的颜色":page=="Guide"?"从这里开始，轻松换装":"给微信一点自己的颜色";
    }
    void OpenLink(string url){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});SetStatus("已在浏览器打开。当前客户端：0.7.1-preview.1；请在 Releases 核对最新版本。",false);}catch(Exception e){SetStatus("无法打开浏览器："+e.Message,true);}}
    void ShowShadeUndo(){Find<Button>("UndoContrast").Visibility=shadeUndo.Available?Visibility.Visible:Visibility.Collapsed;}
    void ClearShadeUndo(){shadeUndo.Clear();ShowShadeUndo();}
    void BeginComposition(string path,string name,Rect crop){
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.EndInit();image.Freeze();
        draftPath=path;draftName=name;palettePreview.BeginComposition(image,crop);previewGeneration++;previewTimer.Stop();
        Find<Border>("CompositionTools").Visibility=Visibility.Visible;Find<TextBlock>("PreviewHelp").Text="拖动图片 · 滚轮缩放 · 分区虚线仅为构图参考";
        Find<TextBlock>("ContrastSummary").Text="构图草稿 · 尚未保存";Find<TextBlock>("ContrastDetails").Text="构图时显示清晰原图；保存后恢复模糊、遮罩与可读性检查。示意比例为 19:10，实际微信窗口大小与分栏宽度会影响最终位置。";
        Toggle(!busy);SyncComposition();SetStatus("在右侧拖动、缩放图片，确认后点「保存构图」。取消或切换页面会保留原背景。",false);
    }
    void SyncComposition(){if(!palettePreview.Editing)return;syncingZoom=true;Find<Slider>("CompositionZoom").Value=palettePreview.Composition.Zoom;Find<TextBlock>("CompositionScale").Text=palettePreview.Composition.Zoom.ToString("0.00")+"×";syncingZoom=false;}
    void FinishComposition(bool save){
        if(!palettePreview.Editing)return;
        if(save){settings.Wallpaper=draftPath;settings.WallpaperName=draftName;settings.SetCrop(palettePreview.Composition.Crop);Read().Save();ClearShadeUndo();}
        palettePreview.EndComposition();draftPath=draftName=null;Find<Border>("CompositionTools").Visibility=Visibility.Collapsed;Find<TextBlock>("PreviewHelp").Text="点击预览区域可编辑配色 · 窗口比例以实际微信为准";Toggle(!busy);Preview();SetStatus(save?"构图已保存。点击「更新主题」应用到微信。":"已取消构图，保留原背景。",false);
    }
    GlassSettings Read(){return new GlassSettings{Density=(int)Find<Slider>("Density").Value,Dark=Find<CheckBox>("Dark").IsChecked==true,Accent=settings.Accent,Exe=manual??"",Wallpaper=settings.Wallpaper,WallpaperName=settings.WallpaperName,Blur=(int)Find<Slider>("ImageBlur").Value,Shade=(int)Find<Slider>("ImageShade").Value,CropX=settings.CropX,CropY=settings.CropY,CropW=settings.CropW,CropH=settings.CropH,Colors=settings.Colors.Copy()};}
    void OpenColors(string key){if(busy)return;if(key!=null&&!ThemeColors.Supports(key)){SetStatus("此处暂保留微信原生样式；可调整背景、列表与选中项颜色。",false);return;}var editor=new ColorsWindow(Window,Read(),previewBitmap,key);if(editor.ShowDialog()==true){settings.Colors=editor.Result;Read().Save();Preview();SetStatus("分区配色已保存。点击「更新主题」应用到微信。",false);}}
    BitmapSource previewBitmap;
    void Accent(string color,string name){settings.Accent=color;settings.Colors.Set(Read().Dark,"selection",color);settings.Colors.Set(Read().Dark,"selectedText",Readability.SelectionInk(color));Find<TextBlock>("AccentLabel").Text="当前："+name;Preview();}
    void Preview(){
        if(!initialized||palettePreview.Editing)return;var s=Read();Find<TextBlock>("DensityLabel").Text=s.Density+"%";
        bool hasImage=!String.IsNullOrEmpty(s.Wallpaper);Find<TextBlock>("BlurLabel").Text=s.Blur.ToString();Find<TextBlock>("ShadeLabel").Text=s.Shade+"%";
        Find<TextBlock>("ImageName").Text=hasImage?(s.WallpaperName.Length>0?s.WallpaperName:"已保存的背景图"):"分区纯色模式";
        Find<TextBlock>("ImageName").ToolTip=Find<TextBlock>("ImageName").Text;Find<StackPanel>("ImageEffects").IsEnabled=hasImage;Find<Slider>("Density").IsEnabled=!hasImage;
        Find<Button>("ClearImage").IsEnabled=hasImage&&!busy;
        Find<Button>("CropImage").IsEnabled=hasImage&&!busy;
        Find<TextBlock>("ModeDescription").Text=hasImage?"背景仅在本机处理；修改后点击更新主题。":"没有图片时，各区域使用不透明纯色。";
        Find<TextBlock>("MaterialLabel").Text=(hasImage?"自定义背景":"分区纯色")+" · 示例消息";
        palettePreview.Update(s,previewBitmap);
        Find<TextBlock>("AccentLabel").Text="当前："+(s.Accent=="#39C5BB"?"青屿 · #39C5BB":s.Accent=="#65B5A1"?"青玉":s.Accent=="#A094D9"?"鸢尾":"雾蓝");
        recommendedShade=-1;Find<Button>("ImproveContrast").IsEnabled=false;
        previewGeneration++;previewTimer.Stop();if(hasImage){Find<TextBlock>("ContrastSummary").Text="正在检查文字对比…";previewTimer.Start();}else{previewBitmap=null;palettePreview.Update(Read(),null);ShowContrast(Readability.Analyze(s));}
    }
    async Task RenderWallpaper(){
        if(palettePreview.Editing)return;if(previewRunning){previewPending=true;return;}previewRunning=true;int generation=previewGeneration;var state=Read();
        try{var result=await Task.Run(()=>Tuple.Create(Wallpaper.Process(state),Readability.Analyze(state)));string file=result.Item1;if(generation!=previewGeneration||!initialized||String.IsNullOrEmpty(file))return;
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(file);image.EndInit();image.Freeze();previewBitmap=image;palettePreview.Update(state,image);
            ShowContrast(result.Item2);
        }catch(Exception e){if(generation==previewGeneration){previewBitmap=null;palettePreview.Update(Read(),null);SetStatus("背景预览失败："+e.Message,true);}}
        finally{previewRunning=false;if(initialized&&(previewPending||generation!=previewGeneration)){previewPending=false;previewTimer.Start();}}
    }
    void ShowContrast(ContrastReport report){recommendedShade=report.RecommendedShade;Find<TextBlock>("ContrastSummary").Text=report.Summary;Find<TextBlock>("ContrastDetails").Text=report.Details;Find<TextBlock>("ContrastSummary").Foreground=(Brush)new BrushConverter().ConvertFromString(report.Pass?"#217D5C":"#936526");Find<Button>("ImproveContrast").IsEnabled=recommendedShade>=0&&!busy;}
    internal static void PopulateCompatibility(Window window){
        ((TextBlock)window.FindName("CompatibilitySummary")).Text="4.1.15.X / 4.2.0.X · x64\n实测范围与风险见「使用指南」";
        ((TextBlock)window.FindName("CompatibilitySummary")).ToolTip=Adapters.Summary;
        ((TextBlock)window.FindName("CompatibilityGuide")).Text=Adapters.Summary+"\n\nX 表示同系列最后一段版本号。仅标注的完整版本和文件在本机实测过；其他小版本允许尝试，可能显示错误或崩溃。启动时会检查关键绘制结构，通过检查不代表已实测。\n显示异常先点「恢复默认」；若无法正常操作，从托盘完全退出，再用原微信入口启动。新系列需要独立适配。";
    }
    async Task Detect(){await Work(async()=>{
        target=await Task.Run(()=>Weixin.Locate(manual));
        if(target.Compatible){
            manual=target.Exe;bool tested=target.Adapter.IsTestedBuild(target.Version,target.Hash);
            Find<TextBlock>("Detection").Text=(tested?"✓  实测构建一致":target.Adapter.Status=="experimental"?"△  实验适配 · 待实机验证":"△  同系列试用 · 未经实测")+"\n微信 "+target.Version;
            SetStatus(tested?(Weixin.Count()>0?"微信正在运行。首次启用请先从托盘退出微信。":"已找到适配器，可以启动主题微信。"):
                "匹配 "+target.Adapter.Family+"；"+(target.Adapter.Status=="verified"?"实测基准为 ":"实验适配基准为 ")+target.Adapter.Version+"。当前构建允许尝试，可能显示错误或崩溃；异常请恢复默认，无法操作时退出并用原微信入口启动。",!tested);
        }else{Find<TextBlock>("Detection").Text="微信 "+(target.Version??"未找到")+"\n暂不可启用主题";SetStatus(target.Error,true);}
    });}
    async Task Work(Func<Task> action){if(busy)return;busy=true;Toggle(false);SetStatus("正在处理，请稍候…",false);try{await action();}catch(Exception e){SetStatus(e.Message,true);}finally{busy=false;Toggle(true);}Find<TextBlock>("Connection").Text=await Task.Run(()=>Engine.Connection());}
    void Toggle(bool enabled){bool editing=palettePreview.Editing;foreach(string name in new[]{"Launch","Apply","Detect","Choose","ChooseImage","ClearImage","CropImage","EditColors","Mint"})Find<Button>(name).IsEnabled=enabled&&!editing;foreach(string name in new[]{"Restore","Report","SaveComposition","CancelComposition"})Find<Button>(name).IsEnabled=enabled;Find<Button>("Launch").IsEnabled=enabled&&!editing&&target!=null&&target.Compatible;Find<Button>("ClearImage").IsEnabled=Find<Button>("CropImage").IsEnabled=enabled&&!editing&&!String.IsNullOrEmpty(settings.Wallpaper);Find<Button>("ImproveContrast").IsEnabled=enabled&&!editing&&recommendedShade>=0;Find<Button>("UndoContrast").IsEnabled=enabled&&!editing;Find<StackPanel>("ImageEffects").IsEnabled=enabled&&!editing&&!String.IsNullOrEmpty(settings.Wallpaper);}
    void SetStatus(string text,bool error){var label=Find<TextBlock>("Status");label.Text=text;label.ToolTip=text;label.Foreground=(Brush)new BrushConverter().ConvertFromString(error?"#A14931":"#527367");}
}
internal static class SelfTest {
    internal static int Run(string folder){
        folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);Files.Root=folder;
        var lines=new System.Collections.Generic.List<string>();
        using(var current=System.Diagnostics.Process.GetCurrentProcess()){
            Check(LaunchContext.IntegrityRid()<=0x5000,"current Windows integrity level can be read",lines);
            string exe=ProcessDiscovery.ImagePath(current.Id);
            Check(Array.IndexOf(ProcessDiscovery.Named(Path.GetFileName(exe)),current.Id)>=0,"native process enumeration finds current executable",lines);
            Check(String.Equals(ProcessDiscovery.ImagePath(current.Id),exe,StringComparison.OrdinalIgnoreCase),"native process image path matches executable",lines);
            Check(ProcessDiscovery.HasModule(current.Id,exe),"native module inspection finds current executable",lines);
            Check(!ProcessDiscovery.HasModule(current.Id,Path.Combine(folder,"absent-module.dll")),"missing module is not reported loaded",lines);
        }
        var s=new GlassSettings{Density=72,Dark=true,Accent="#65B5A1"};s.Save();var loaded=GlassSettings.Load();
        Check(loaded.Density==72&&loaded.Dark&&loaded.Accent==s.Accent,"settings round trip",lines);
        string dir=Config.Prepare(s,true),text=File.ReadAllText(Path.Combine(dir,"theme.ini"));
        Check(text.Contains("[theme]")&&text.Contains("wallpaper=\r\n"),"solid-color independent configuration",lines);
        Check(File.ReadAllBytes(Path.Combine(dir,"theme.ini"))[0]==0xff,"UTF-16 native configuration",lines);
        Check(!File.Exists(Path.Combine(dir,"routes.bin")),"independent engine needs no upstream route protocol",lines);
        Check(Config.Build(s,false).Contains("enabled=0"),"disabled configuration",lines);
        Files.Atomic(Path.Combine(folder,"atomic.txt"),"old",Encoding.UTF8);Files.Atomic(Path.Combine(folder,"atomic.txt"),"new",Encoding.UTF8);
        Check(File.ReadAllText(Path.Combine(folder,"atomic.txt"))=="new","atomic replacement",lines);
        bool rejected=false;try{Config.Build(new GlassSettings{Density=999},true);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid concentration rejected",lines);
        string foreign=Path.Combine(folder,"foreign.dll");File.WriteAllText(foreign,"not an engine");rejected=false;try{Engine.ValidateRuntime(foreign,Adapters.All[0]);}catch(IOException){rejected=true;}Check(rejected,"unknown runtime rejected",lines);
        AdapterTests(lines);
        string original=Path.Combine(folder,"测试 背景.png");
        using(var sample=new System.Drawing.Bitmap(96,64)){
            using(var g=System.Drawing.Graphics.FromImage(sample)){g.Clear(System.Drawing.Color.FromArgb(30,90,170));g.FillRectangle(System.Drawing.Brushes.Orange,0,0,48,64);}
            sample.Save(original,System.Drawing.Imaging.ImageFormat.Png);
        }
        string imported=Wallpaper.Import(original);Check(imported!=original&&File.Exists(imported),"Unicode source copied to managed PNG",lines);
        s.Wallpaper=imported;s.WallpaperName="测试 背景.png";s.Blur=0;s.Shade=0;s.Save();loaded=GlassSettings.Load();
        Check(loaded.Wallpaper==imported&&loaded.WallpaperName==s.WallpaperName&&loaded.Blur==0&&loaded.Shade==0,"wallpaper settings survive restart",lines);
        string clean=Wallpaper.Process(s);s.Blur=20;string blurred=Wallpaper.Process(s);
        using(var a=new System.Drawing.Bitmap(clean))using(var b=new System.Drawing.Bitmap(blurred)){
            Check(a.Width==96&&a.Height==64,"image dimensions preserved",lines);
            Check(a.GetPixel(46,32).ToArgb()!=b.GetPixel(46,32).ToArgb(),"blur changes real image edge pixels",lines);
        }
        s.Blur=0;s.SetCrop(new Rect(.5,0,.5,1));string half=Wallpaper.Process(s);
        using(var crop=new System.Drawing.Bitmap(half)){Check(crop.Width==48&&crop.Height==64,"crop changes actual exported dimensions",lines);Check(crop.GetPixel(10,10).B>crop.GetPixel(10,10).R,"crop selects right half pixels",lines);}
        s.Save();Check(GlassSettings.Load().Crop==s.Crop,"crop survives settings reload",lines);
        Check(CropMath.Clamp(new Rect(.9,.9,.5,.5))==new Rect(.5,.5,.5,.5),"dragging crop stays inside image",lines);
        Rect zoomed=CropMath.Zoom(new Rect(.2,.2,.5,.5),.5);Check(Math.Abs(zoomed.X-.325)<.0001&&Math.Abs(zoomed.Width-.25)<.0001,"zoom keeps crop center",lines);
        Rect fitted=CropMath.Fit(16d/9,1.5,new Rect(0,0,1,1));Check(Math.Abs(fitted.Width*1.5/fitted.Height-16d/9)<.0001,"crop aspect ratio uses image coordinates",lines);
        Check(Math.Abs(Readability.Contrast(System.Drawing.Color.Black,System.Drawing.Color.White)-21)<.00001,"WCAG black-white contrast equals 21:1",lines);
        Check(Readability.Contrast(System.Drawing.Color.Gray,System.Drawing.Color.Gray)==1,"same colors fail contrast",lines);
        var contrast=Readability.Analyze(s);Check(!contrast.Pass&&contrast.RecommendedShade>=0,"low contrast background gets concrete mask recommendation",lines);
        s.Shade=contrast.RecommendedShade;Check(Readability.Analyze(s).Pass,"recommended mask passes background and bubble samples",lines);
        var previous=s.Colors.Copy();s.Colors.Set(s.Dark,"selectedText",s.Colors.Get(s,"selection"));
        contrast=Readability.Analyze(s);Check(!contrast.Pass&&contrast.RecommendedShade<0,"same-color selection fails and cannot be repaired with wallpaper mask",lines);s.Colors=previous;
        s.Colors.Set(false,"outgoing","#123456");s.Colors.Set(true,"outgoing","#AABBCC");s.Colors.Set(false,"incoming","#FFEEDD");s.Colors.Set(false,"input","#112233");s.Colors.SetOpacity(false,"input",40);s.Save();loaded=GlassSettings.Load();
        loaded.Dark=false;Check(loaded.Colors.Get(loaded,"outgoing")=="#123456"&&loaded.Colors.Opacity(false,"input")==40,"custom palette and region opacity survive restart",lines);
        loaded.Dark=true;Check(loaded.Colors.Get(loaded,"outgoing")=="#AABBCC","dark and light palette remain independent",lines);
        var copied=loaded.Colors.Copy();copied.Set(true,"outgoing","#000000");Check(loaded.Colors.Get(loaded,"outgoing")=="#AABBCC","cancelled palette draft cannot change saved colors",lines);
        loaded.Dark=false;using(var mask=new System.Drawing.Bitmap(ThemeColors.Mask(loaded,"input"))){var pixel=mask.GetPixel(3,3);Check(pixel.A==102&&pixel.R==17&&pixel.G==34&&pixel.B==51,"region mask preserves chosen RGB and 40 percent alpha",lines);}
        string custom=Config.Build(loaded,true,clean);Check(custom.Contains("input=#112233")&&custom.Contains("inputOpacity=40")&&!custom.Contains("outgoing="),"supported native input color exports without unsupported bubble options",lines);
        copied.Reset(true,"outgoing");Check(!copied.Customized(true,"outgoing")&&copied.Customized(false,"outgoing"),"reset only affects selected appearance",lines);
        bool badColor=false;try{s.Colors.Set(false,"text","#GGGGGG");}catch(InvalidDataException){badColor=true;}Check(badColor,"invalid HEX color rejected",lines);s.Colors=new ThemeColors();
        s.SetCrop(new Rect(0,0,1,1));
        s.Blur=0;s.Shade=50;s.Dark=true;string dim=Wallpaper.Process(s);s.Dark=false;string light=Wallpaper.Process(s);
        using(var a=new System.Drawing.Bitmap(dim))using(var b=new System.Drawing.Bitmap(light))Check(a.GetPixel(10,10).R<b.GetPixel(10,10).R,"dark/light masks change brightness",lines);
        string wallpaperDir=Config.Prepare(s,true);var ini=File.ReadAllText(Path.Combine(wallpaperDir,"theme.ini"));
        Check(ini.Contains("wallpaper="+light)&&!ini.Contains("[glass]"),"independent wallpaper protocol contains decoded image path",lines);
        s.Wallpaper=Path.Combine(folder,"missing.png");rejected=false;try{Config.Prepare(s,true);}catch(IOException){rejected=true;}Check(rejected,"missing wallpaper is not silently dropped",lines);
        Config.Prepare(s,false);lines.Add("PASS: recovery works even if wallpaper is missing");
        s.Wallpaper="";Check(Config.Build(s,true).Contains("wallpaper=\r\n"),"clear wallpaper switches to solid colors",lines);
        ThemePresets.Mint(loaded);Check(loaded.Colors.Get(loaded,"selection")=="#39C5BB"&&File.Exists(loaded.Wallpaper),"Qingyu preset creates local original background and exact accent",lines);Check(Readability.Analyze(loaded).Pass,"Qingyu list and selection colors pass contrast",lines);var target=Weixin.Locate(null);lines.Add("Installed target: "+target.Version+" / hash compatible="+target.Compatible);
        var w=Controller.LoadWindow();Controller.PopulateCompatibility(w);((TextBlock)w.FindName("Detection")).Text="微信 "+target.Version+"\n"+(target.Error??"已选择 "+target.Adapter?.Family);var content=(FrameworkElement)w.Content;content.Resources=w.Resources;content.SetValue(TextElement.ForegroundProperty,w.Foreground);content.SetValue(TextElement.FontFamilyProperty,w.FontFamily);var demo=new PalettePreview();demo.Update(loaded,null);((Border)w.FindName("Preview")).Child=demo;w.Content=null;content.Measure(new Size(1170,810));content.Arrange(new Rect(0,0,1170,810));content.UpdateLayout();
        var image=new RenderTargetBitmap(1170,810,96,96,PixelFormats.Pbgra32);image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(Path.Combine(folder,"controller-preview.png")))encoder.Save(f);
        lines.Add("PASS: XAML loaded and rendered. DWM and Weixin rendering require live validation.");
        CompositionTests(lines);
        content.Measure(new Size(1064,701));content.Arrange(new Rect(0,0,1064,701));content.UpdateLayout();
        var scroll=(ScrollViewer)w.FindName("SettingsScroll");var theme=(StackPanel)w.FindName("ThemePage");
        Check(theme.ActualHeight<=scroll.ActualHeight+1,"Theme page fits minimum client area without scrolling or clipping",lines);
        SaveFrame(content,1064,701,Path.Combine(folder,"controller-minimum.png"));
        ((StackPanel)w.FindName("ThemePage")).Visibility=Visibility.Collapsed;((StackPanel)w.FindName("BackgroundPage")).Visibility=Visibility.Visible;
        var source=new BitmapImage(new Uri(loaded.Wallpaper));demo.Update(loaded,source);demo.BeginComposition(source,new Rect(0,0,1,1));demo.ZoomComposition(1.6);
        ((Border)w.FindName("CompositionTools")).Visibility=Visibility.Visible;((TextBlock)w.FindName("PreviewHelp")).Text="拖动图片 · 滚轮缩放 · 分区虚线仅为构图参考";
        ((TextBlock)w.FindName("ContrastSummary")).Text="构图草稿 · 尚未保存";((TextBlock)w.FindName("ContrastDetails")).Text="构图时显示清晰原图，保存后恢复图片效果。分区位置为参考，以实际微信为准。";
        content.Measure(new Size(1170,780));content.Arrange(new Rect(0,0,1170,780));content.UpdateLayout();SaveFrame(content,1170,780,Path.Combine(folder,"composition-preview.png"));
        demo.EndComposition();
        ((Grid)w.FindName("Studio")).Visibility=Visibility.Collapsed;((Border)w.FindName("GuidePage")).Visibility=Visibility.Visible;
        content.Measure(new Size(1080,700));content.Arrange(new Rect(0,0,1080,700));content.UpdateLayout();
        var guideImage=new RenderTargetBitmap(1080,700,96,96,PixelFormats.Pbgra32);guideImage.Render(content);var guideEncoder=new PngBitmapEncoder();guideEncoder.Frames.Add(BitmapFrame.Create(guideImage));using(var f=File.Create(Path.Combine(folder,"guide-preview.png")))guideEncoder.Save(f);
        var editor=new ColorsWindow(null,loaded,null);var editorContent=(FrameworkElement)editor.Content;editorContent.SetValue(TextElement.ForegroundProperty,editor.Foreground);editorContent.SetValue(TextElement.FontFamilyProperty,editor.FontFamily);editor.Content=null;editorContent.Measure(new Size(1000,690));editorContent.Arrange(new Rect(0,0,1000,690));editorContent.UpdateLayout();var editorImage=new RenderTargetBitmap(1000,690,96,96,PixelFormats.Pbgra32);editorImage.Render(editorContent);var editorEncoder=new PngBitmapEncoder();editorEncoder.Frames.Add(BitmapFrame.Create(editorImage));using(var f=File.Create(Path.Combine(folder,"colors-editor.png")))editorEncoder.Save(f);lines.Add("PASS: color editor and interactive palette preview rendered");
        lines.Add("No Weixin process launched, stopped or modified by these tests.");File.WriteAllLines(Path.Combine(folder,"results.txt"),lines,Encoding.UTF8);return 0;
    }
    static void Check(bool value,string label,System.Collections.Generic.List<string> lines){if(!value)throw new Exception("FAIL: "+label);lines.Add("PASS: "+label);}
    static void SaveFrame(Visual content,int width,int height,string path){var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))encoder.Save(stream);}
    static void CompositionTests(System.Collections.Generic.List<string> lines){
        foreach(double imageRatio in new[]{.5,1d,1.9,3.2}){var c=new Composition(imageRatio,new Rect(0,0,1,1));c.SetZoom(2);var before=c.Crop;c.Pan(before,.1,-.1);Check(Math.Abs(c.Crop.Width/c.Crop.Height*imageRatio-Composition.Ratio)<1e-8&&c.Crop.X<before.X&&c.Crop.Y>before.Y,"composition drag preserves export ratio and follows pointer on "+imageRatio,lines);c.Pan(c.Crop,100,-100);Check(c.Crop.X>=0&&c.Crop.Bottom<=1.000001,"composition stays inside original image",lines);c.SetZoom(99);Check(Math.Abs(c.Zoom-8)<1e-8,"composition zoom bounded",lines);c.Reset();Check(Math.Abs(c.Zoom-1)<1e-8,"composition reset returns full cover viewport",lines);}
        var undo=new ShadeUndo();undo.Capture(28,50);undo.Observe(50);Check(undo.Take(out int shade)&&shade==28&&!undo.Available,"recommendation undo restores exact prior shade once",lines);undo.Capture(28,50);undo.Observe(51);Check(!undo.Available&&!undo.Take(out shade),"manual shade edit invalidates stale undo",lines);
    }
    static void AdapterTests(System.Collections.Generic.List<string> lines){
        string h1=new string('a',64),h2=new string('b',64),h3=new string('c',64),error;
        var first=new AdapterProfile("weixin-4.1.15.56-x64","4.1.15.56",h1,"verified","",h2,h3);
        var second=new AdapterProfile("weixin-4.1.16.1-x64","4.1.16.1",h2,"experimental","",h3,h1);
        var pending=new AdapterProfile("weixin-4.2.0.13-x64","4.2.0.13",h3,"pending","接口待适配","","");
        var profiles=new[]{first,second,pending};
        Check(Adapters.Resolve(profiles,"4.1.15.56",0x8664,h1,out error)==first&&error==null&&first.IsTestedBuild("4.1.15.56",h1),"exact tested build selects its adapter",lines);
        Check(Adapters.Resolve(profiles,"4.1.15.99",0x8664,h2,out error)==first&&!first.IsTestedBuild("4.1.15.99",h2),"rolling patch version uses family adapter as untested trial",lines);
        Check(Adapters.Resolve(profiles,"4.1.15.56",0x8664,h2,out error)==first&&!first.IsTestedBuild("4.1.15.56",h2),"changed file with same version is trial, never falsely tested",lines);
        Check(Adapters.Resolve(profiles,"4.1.16.2",0x8664,h1,out error)==second,"second family selects its own adapter",lines);
        Check(Adapters.Resolve(profiles,"4.1.150.56",0x8664,h1,out error)==null&&Adapters.Resolve(profiles,"4.1.15.56.extra",0x8664,h1,out error)==null,"prefix lookalike and malformed version rejected",lines);
        Check(Adapters.Resolve(profiles,"4.1.15.56",0x14c,h1,out error)==null,"x86 cannot use an x64 adapter",lines);
        Check(Adapters.Resolve(profiles,"4.2.0.99",0x8664,h3,out error)==null&&error.Contains("待适配"),"pending family never loads native code",lines);
        Check(Adapters.Resolve(profiles,"4.3.0.1",0x8664,h1,out error)==null,"unknown family has no cross-version fallback",lines);
        var rebuilt=new AdapterProfile(first.Id,first.Version,h1,"verified","",h1,h3);
        Check(first.Runtime!=second.Runtime&&first.Runtime!=rebuilt.Runtime,"adapter runtime separates versions and changed entry binaries",lines);
        bool rejected=false;try{new AdapterProfile("../bad","4.1.15.56",h1,"verified","",h2,h3);}catch(InvalidDataException){rejected=true;}Check(rejected,"unsafe manifest identifier rejected",lines);
        Check(!Engine.IsReady("incompatible")&&!Engine.IsReady("failed")&&Engine.IsReady("ready"),"loaded DLL alone is not a ready engine",lines);
        foreach(var adapter in Adapters.All){if(!adapter.CanLoad)continue;Engine.ValidatePair(adapter.Bundle(AppDomain.CurrentDomain.BaseDirectory),adapter);Check(true,"bundled DLL pair validated for "+adapter.Family,lines);}
    }
}
