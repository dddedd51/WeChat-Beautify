using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Schematic geometry; the theme colors and overlay compositing match exported settings.
internal sealed class PalettePreview:FrameworkElement {
    GlassSettings settings=new GlassSettings();BitmapSource wallpaper;
    readonly System.Collections.Generic.List<Tuple<Rect,string>> hits=new System.Collections.Generic.List<Tuple<Rect,string>>();
    internal event Action<string> Pick;internal string Selected;
    internal void Update(GlassSettings value,BitmapSource image){settings=value;wallpaper=image;InvalidateVisual();}
    Brush Ink(string key){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Colors.Get(settings,key)));}
    void Box(DrawingContext dc,Rect r,string key,bool region=false){var brush=(SolidColorBrush)Ink(key);if(region){int a=settings.Colors.Opacity(settings.Dark,key);brush=new SolidColorBrush(Color.FromArgb((byte)((a*255+50)/100),brush.Color.R,brush.Color.G,brush.Color.B));}dc.DrawRectangle(brush,null,r);hits.Add(Tuple.Create(r,key));}
    void Words(DrawingContext dc,string text,double x,double y,string key,double size=13){var f=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,Ink(key),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(f,new Point(x,y));hits.Add(Tuple.Create(new Rect(x,y,f.Width,f.Height),key));}
    protected override void OnRender(DrawingContext dc){
        base.OnRender(dc);hits.Clear();double w=ActualWidth,h=ActualHeight;if(w<200||h<150)return;
        double scale=Math.Min(w/760,h/400),ox=(w-760*scale)/2,oy=(h-400*scale)/2;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(21,31,45)),null,new Rect(RenderSize));
        dc.PushTransform(new TranslateTransform(ox,oy));dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Dark?"#1C2128":"#E8EEF4")),null,new Rect(0,0,760,400));
        if(wallpaper!=null){double factor=Math.Max(760d/wallpaper.PixelWidth,400d/wallpaper.PixelHeight),iw=wallpaper.PixelWidth*factor,ih=wallpaper.PixelHeight*factor;dc.PushClip(new RectangleGeometry(new Rect(0,0,760,400)));dc.DrawImage(wallpaper,new Rect((760-iw)/2,(400-ih)/2,iw,ih));dc.Pop();}
        Box(dc,new Rect(0,0,58,400),"navigation",true);Box(dc,new Rect(58,0,197,400),"list",true);Box(dc,new Rect(255,58,505,242),"canvasBackground",true);Box(dc,new Rect(255,300,505,100),"input",true);
        Words(dc,"美",17,18,"accent",24);Words(dc,"▣",18,83,"accent",24);Words(dc,"☷",18,140,"icons",23);
        Words(dc,"⌕  搜索",78,22,"secondary");Box(dc,new Rect(68,62,177,64),"selection");Words(dc,"主题预览助手",80,74,"selectedText");Words(dc,"选中的会话",80,99,"selectedText",11);
        Words(dc,"周末计划",80,152,"text");Words(dc,"慢一点，也很好。",80,178,"secondary",11);Words(dc,"灵感收藏",80,221,"text");Words(dc,"随手记下新的想法",80,247,"secondary",11);
        Words(dc,"主题预览助手",274,20,"text",15);Words(dc,"14:20",475,65,"canvas",11);
        Box(dc,new Rect(276,104,230,44),"incoming");Words(dc,"接收消息：背景和文字都清晰",288,119,"chat",12);
        Box(dc,new Rect(480,171,258,65),"outgoing");Words(dc,"发送消息：每个区域自由配色",492,184,"chat",12);Words(dc,"示例链接",492,209,"link",12);
        Words(dc,"这里仅展示示例消息",443,269,"canvas",11);Words(dc,"☺   +",276,314,"icons",17);Words(dc,"输入消息…",276,357,"text");
        if(Selected!=null)foreach(var hit in hits)if(hit.Item2==Selected)dc.DrawRectangle(null,new Pen(Brushes.DeepSkyBlue,2),hit.Item1);
        dc.Pop();dc.Pop();dc.Pop();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);double scale=Math.Min(ActualWidth/760,ActualHeight/400);if(scale<=0)return;var p=e.GetPosition(this);p=new Point((p.X-(ActualWidth-760*scale)/2)/scale,(p.Y-(ActualHeight-400*scale)/2)/scale);for(int i=hits.Count-1;i>=0;i--)if(hits[i].Item1.Contains(p)){if(Pick!=null)Pick(hits[i].Item2);break;}}
}
