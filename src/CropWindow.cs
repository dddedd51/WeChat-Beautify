using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class CropMath {
    internal static Rect Clamp(Rect r) {
        double w=Math.Max(.01,Math.Min(1,r.Width)),h=Math.Max(.01,Math.Min(1,r.Height));
        return new Rect(Math.Max(0,Math.Min(1-w,r.X)),Math.Max(0,Math.Min(1-h,r.Y)),w,h);
    }
    internal static Rect Zoom(Rect r,double factor){
        double w=Math.Max(.02,Math.Min(1,r.Width*factor)),h=Math.Max(.02,Math.Min(1,r.Height*factor));
        double k=Math.Min(w/r.Width,h/r.Height);return Clamp(new Rect(r.X+(r.Width-r.Width*k)/2,r.Y+(r.Height-r.Height*k)/2,r.Width*k,r.Height*k));
    }
    internal static Rect Fit(double ratio,double imageRatio,Rect around){
        if(ratio<=0)return Clamp(around);
        double normalized=ratio/imageRatio,w=around.Width,h=w/normalized;
        if(h>around.Height){h=around.Height;w=h*normalized;}
        return Clamp(new Rect(around.X+(around.Width-w)/2,around.Y+(around.Height-h)/2,w,h));
    }
}
internal sealed class CropCanvas : FrameworkElement {
    readonly BitmapSource image;Rect selected;Rect display;Point anchor;Rect start;
    bool dragging,moving;double ratio;public event Action Changed;
    internal Rect Selected {get{return selected;}set{selected=CropMath.Clamp(value);InvalidateVisual();if(Changed!=null)Changed();}}
    internal double Ratio{get{return ratio;}set{ratio=value;if(value>0)Selected=CropMath.Fit(value,image.PixelWidth/(double)image.PixelHeight,selected);}}
    internal CropCanvas(BitmapSource source,Rect selection){image=source;selected=selection;Focusable=true;MinHeight=240;Cursor=Cursors.Cross;ClipToBounds=true;}
    protected override void OnRender(DrawingContext dc){
        base.OnRender(dc);dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(14,20,29)),null,new Rect(RenderSize));
        double scale=Math.Min(Math.Max(1,ActualWidth-36)/image.PixelWidth,Math.Max(1,ActualHeight-36)/image.PixelHeight);
        display=new Rect((ActualWidth-image.PixelWidth*scale)/2,(ActualHeight-image.PixelHeight*scale)/2,image.PixelWidth*scale,image.PixelHeight*scale);
        dc.DrawImage(image,display);Rect box=ToDisplay(selected);
        var outside=Geometry.Combine(new RectangleGeometry(display),new RectangleGeometry(box),GeometryCombineMode.Exclude,null);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(145,0,0,0)),null,outside);
        var pen=new Pen(new SolidColorBrush(Color.FromRgb(137,204,255)),2);dc.DrawRectangle(null,pen,box);
        var thin=new Pen(new SolidColorBrush(Color.FromArgb(160,255,255,255)),1);
        for(int i=1;i<3;i++){dc.DrawLine(thin,new Point(box.X+box.Width*i/3,box.Top),new Point(box.X+box.Width*i/3,box.Bottom));dc.DrawLine(thin,new Point(box.Left,box.Y+box.Height*i/3),new Point(box.Right,box.Y+box.Height*i/3));}
        foreach(var p in new[]{box.TopLeft,box.TopRight,box.BottomLeft,box.BottomRight})dc.DrawRectangle(Brushes.White,null,new Rect(p.X-4,p.Y-4,8,8));
    }
    Rect ToDisplay(Rect r){return new Rect(display.X+r.X*display.Width,display.Y+r.Y*display.Height,r.Width*display.Width,r.Height*display.Height);}
    Point Normal(Point p){return new Point(Math.Max(0,Math.Min(1,(p.X-display.X)/display.Width)),Math.Max(0,Math.Min(1,(p.Y-display.Y)/display.Height)));}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){
        base.OnMouseLeftButtonDown(e);Point p=e.GetPosition(this);if(!display.Contains(p))return;
        Focus();anchor=Normal(p);start=selected;moving=selected.Contains(anchor)&&(Keyboard.Modifiers&ModifierKeys.Shift)==0;dragging=true;CaptureMouse();e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e){
        base.OnMouseMove(e);if(!dragging)return;Point p=Normal(e.GetPosition(this));
        if(moving){Selected=CropMath.Clamp(new Rect(start.X+p.X-anchor.X,start.Y+p.Y-anchor.Y,start.Width,start.Height));return;}
        double dx=p.X-anchor.X,dy=p.Y-anchor.Y,w=Math.Abs(dx),h=Math.Abs(dy);
        if(ratio>0){double nr=ratio/(image.PixelWidth/(double)image.PixelHeight);if(h*nr>w)h=w/nr;else w=h*nr;}
        if(w<.01||h<.01)return;Selected=new Rect(dx>=0?anchor.X:anchor.X-w,dy>=0?anchor.Y:anchor.Y-h,w,h);
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);dragging=false;ReleaseMouseCapture();}
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);dragging=false;}
    protected override void OnMouseWheel(MouseWheelEventArgs e){Selected=CropMath.Zoom(selected,e.Delta>0?.9:1/.9);e.Handled=true;}
}
internal sealed class CropWindow : Window {
    readonly CropCanvas canvas;readonly BitmapSource source;readonly System.Windows.Controls.Image preview;
    readonly TextBlock dimensions;readonly Slider zoom;bool syncing;
    internal Rect Result{get;private set;}
    internal CropWindow(Window owner,string path,Rect initial,double targetRatio){
        Owner=owner;Title="裁剪与定位背景";Width=1000;Height=700;MinWidth=800;MinHeight=570;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(23,34,49));Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();source=bitmap;
        var root=new Grid{Margin=new Thickness(22)};Content=root;
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var header=new StackPanel{Margin=new Thickness(0,0,0,14)};header.Children.Add(new TextBlock{Text="把你想看的部分，留在画面里",FontSize=22,FontWeight=FontWeights.SemiBold});
        header.Children.Add(new TextBlock{Text="拖动选框移动位置 · 滚轮或滑杆缩放 · 按住 Shift 拖出新选框",Foreground=Brushes.LightSteelBlue,Margin=new Thickness(0,8,0,0)});root.Children.Add(header);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(225)});Grid.SetRow(body,1);root.Children.Add(body);
        canvas=new CropCanvas(source,initial);body.Children.Add(canvas);
        var side=new StackPanel{Margin=new Thickness(18,0,0,0)};Grid.SetColumn(side,1);body.Children.Add(side);
        side.Children.Add(new TextBlock{Text="裁剪比例",Margin=new Thickness(0,0,0,8)});
        var ratios=new ComboBox{Margin=new Thickness(0,0,0,20),Height=32,Foreground=Brushes.Black};
        string[] names={"自由框选","匹配当前预览","16 : 9","4 : 3","1 : 1"};foreach(string n in names)ratios.Items.Add(n);ratios.SelectedIndex=0;
        double[] values={0,targetRatio,16d/9,4d/3,1};ratios.SelectionChanged+=(s,e)=>canvas.Ratio=values[ratios.SelectedIndex];side.Children.Add(ratios);
        side.Children.Add(new TextBlock{Text="缩放选区",Margin=new Thickness(0,0,0,8)});
        zoom=new Slider{Minimum=1,Maximum=10,Value=1,Margin=new Thickness(0,0,0,20)};
        zoom.ValueChanged+=(s,e)=>{if(!syncing){double wanted=1/zoom.Value;canvas.Selected=CropMath.Zoom(canvas.Selected,wanted/Math.Max(canvas.Selected.Width,canvas.Selected.Height));}};side.Children.Add(zoom);
        side.Children.Add(new TextBlock{Text="裁剪结果",Margin=new Thickness(0,0,0,8)});
        preview=new System.Windows.Controls.Image{Height=145,Stretch=Stretch.Uniform};side.Children.Add(preview);
        dimensions=new TextBlock{Foreground=Brushes.LightSteelBlue,Margin=new Thickness(0,10,0,18),TextWrapping=TextWrapping.Wrap};side.Children.Add(dimensions);
        side.Children.Add(new TextBlock{Text="预览和微信窗口比例不同时，背景仍会居中铺满。可选「匹配当前预览」减少二次裁切。",FontSize=12,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};Grid.SetRow(buttons,2);root.Children.Add(buttons);
        var reset=Button("重置为整张图片");reset.Click+=(s,e)=>{ratios.SelectedIndex=0;canvas.Selected=new Rect(0,0,1,1);};buttons.Children.Add(reset);
        var cancel=Button("取消");cancel.IsCancel=true;buttons.Children.Add(cancel);
        var ok=Button("使用这个区域");ok.IsDefault=true;ok.Background=new SolidColorBrush(Color.FromRgb(129,187,233));ok.Click+=(s,e)=>{Result=canvas.Selected;DialogResult=true;};buttons.Children.Add(ok);
        canvas.Changed+=Refresh;Loaded+=(s,e)=>Refresh();
    }
    Button Button(string text){return new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(8,0,0,0),Foreground=Brushes.Black};}
    void Refresh(){Rect r=canvas.Selected;int x=(int)(r.X*source.PixelWidth),y=(int)(r.Y*source.PixelHeight),w=Math.Max(1,Math.Min(source.PixelWidth-x,(int)Math.Round(r.Width*source.PixelWidth))),h=Math.Max(1,Math.Min(source.PixelHeight-y,(int)Math.Round(r.Height*source.PixelHeight)));
        preview.Source=new CroppedBitmap(source,new Int32Rect(x,y,w,h));dimensions.Text=w+" × "+h+" 像素\n位置 "+Math.Round(r.X*100)+"%, "+Math.Round(r.Y*100)+"%";
        syncing=true;zoom.Value=Math.Min(10,1/Math.Max(r.Width,r.Height));syncing=false;
    }
}
