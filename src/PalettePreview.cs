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
    internal event Action CompositionChanged;
    internal Composition Composition {get;private set;}
    BitmapSource original;Point dragAnchor;Rect dragStart;bool dragging;
    internal bool Editing=>Composition!=null;
    internal void BeginComposition(BitmapSource image,Rect crop){original=image;Composition=new Composition(image.PixelWidth/(double)image.PixelHeight,crop);Cursor=Cursors.SizeAll;InvalidateVisual();}
    internal void EndComposition(){ReleaseMouseCapture();Composition=null;original=null;Cursor=Cursors.Hand;InvalidateVisual();}
    internal void ZoomComposition(double zoom){if(!Editing)return;Composition.SetZoom(zoom);Changed();}
    internal void ResetComposition(){if(!Editing)return;Composition.Reset();Changed();}
    void Changed(){InvalidateVisual();CompositionChanged?.Invoke();}
    internal void Update(GlassSettings value,BitmapSource image){settings=value;wallpaper=image;InvalidateVisual();}
    Brush Ink(string key){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Colors.Effective(settings,key)));}
    void Box(DrawingContext dc,Rect r,string key,bool region=false){var brush=(SolidColorBrush)Ink(key);if(region){int a=wallpaper==null&&!Editing?100:settings.Colors.Opacity(settings.Dark,key);brush=new SolidColorBrush(Color.FromArgb((byte)((a*255+50)/100),brush.Color.R,brush.Color.G,brush.Color.B));}dc.DrawRectangle(brush,null,r);hits.Add(Tuple.Create(r,key));}
    void Words(DrawingContext dc,string text,double x,double y,string key,double size=13){var f=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,Ink(key),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(f,new Point(x,y));hits.Add(Tuple.Create(new Rect(x,y,f.Width,f.Height),key));}
    protected override void OnRender(DrawingContext dc){
        base.OnRender(dc);hits.Clear();double w=ActualWidth,h=ActualHeight;if(w<200||h<150)return;
        double scale=Math.Min(w/760,h/400),ox=(w-760*scale)/2,oy=(h-400*scale)/2;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(231,242,236)),null,new Rect(RenderSize));
        dc.PushTransform(new TranslateTransform(ox,oy));dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Dark?"#1C2128":"#E8EEF4")),null,new Rect(0,0,760,400));
        if(Editing){var c=Composition.Crop;dc.PushClip(new RectangleGeometry(new Rect(0,0,760,400)));dc.DrawImage(original,new Rect(-c.X*760/c.Width,-c.Y*400/c.Height,760/c.Width,400/c.Height));dc.Pop();}
        else if(wallpaper!=null){double factor=Math.Max(760d/wallpaper.PixelWidth,400d/wallpaper.PixelHeight),iw=wallpaper.PixelWidth*factor,ih=wallpaper.PixelHeight*factor;dc.PushClip(new RectangleGeometry(new Rect(0,0,760,400)));dc.DrawImage(wallpaper,new Rect((760-iw)/2,(400-ih)/2,iw,ih));dc.Pop();}
        Box(dc,new Rect(0,0,58,400),"navigation",true);Box(dc,new Rect(58,0,197,400),"list",true);Box(dc,new Rect(255,58,505,242),"canvasBackground",true);Box(dc,new Rect(255,300,505,100),"input",true);
        Words(dc,"美",17,18,"accent",24);Words(dc,"▣",18,83,"accent",24);Words(dc,"☷",18,140,"icons",23);
        Words(dc,"⌕  搜索",78,22,"secondary");Box(dc,new Rect(68,62,177,64),"selection");Words(dc,"主题预览助手",80,74,"selectedText");Words(dc,"选中的会话",80,99,"selectedText",11);
        Words(dc,"周末计划",80,152,"text");Words(dc,"慢一点，也很好。",80,178,"secondary",11);Words(dc,"灵感收藏",80,221,"text");Words(dc,"随手记下新的想法",80,247,"secondary",11);
        Words(dc,"主题预览助手",274,20,"text",15);Words(dc,"14:20",475,65,"canvas",11);
        Box(dc,new Rect(276,104,230,44),"incoming");Words(dc,"接收消息：背景和文字都清晰",288,119,"chat",12);
        Box(dc,new Rect(480,171,258,65),"outgoing");Words(dc,"消息内容保留微信原来的样式",492,184,"chat",12);Words(dc,"示例链接",492,209,"link",12);
        Words(dc,"这里仅展示示例消息",443,269,"canvas",11);Words(dc,"☺   +",276,314,"icons",17);Words(dc,"输入消息…",276,357,"text");
        if(Editing){
            var pen=new Pen(new SolidColorBrush(Color.FromRgb(23,133,126)),1.5){DashStyle=DashStyles.Dash};
            foreach(var zone in new[]{new Rect(0,0,58,400),new Rect(58,0,197,400),new Rect(255,0,505,58),new Rect(255,58,505,242),new Rect(255,300,505,100)})dc.DrawRectangle(null,pen,zone);
            Guide(dc,"导航",6,368);Guide(dc,"会话列表",119,367);Guide(dc,"标题栏",664,18);Guide(dc,"聊天区域",648,267);Guide(dc,"输入与工具",638,368);
        }
        if(Selected!=null)foreach(var hit in hits)if(hit.Item2==Selected)dc.DrawRectangle(null,new Pen(Brushes.DeepSkyBlue,2),hit.Item1);
        dc.Pop();dc.Pop();dc.Pop();
    }
    void Guide(DrawingContext dc,string label,double x,double y){var t=new FormattedText(label,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225,20,95,88)),null,new Rect(x-4,y-2,t.Width+8,22),4,4);dc.DrawText(t,new Point(x,y));}
    Point FramePoint(Point p){double scale=Math.Min(ActualWidth/760,ActualHeight/400);return new Point((p.X-(ActualWidth-760*scale)/2)/scale,(p.Y-(ActualHeight-400*scale)/2)/scale);}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);if(ActualWidth<=0||ActualHeight<=0)return;var p=FramePoint(e.GetPosition(this));if(!new Rect(0,0,760,400).Contains(p))return;if(Editing){dragAnchor=p;dragStart=Composition.Crop;dragging=true;CaptureMouse();e.Handled=true;return;}for(int i=hits.Count-1;i>=0;i--)if(hits[i].Item1.Contains(p)){Pick?.Invoke(hits[i].Item2);break;}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!Editing||!dragging)return;var p=FramePoint(e.GetPosition(this));Composition.Pan(dragStart,(p.X-dragAnchor.X)/760,(p.Y-dragAnchor.Y)/400);Changed();}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);dragging=false;ReleaseMouseCapture();}
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);dragging=false;}
    protected override void OnMouseWheel(MouseWheelEventArgs e){if(!Editing)return;ZoomComposition(Composition.Zoom*(e.Delta>0?1.1:1/1.1));e.Handled=true;}
}
