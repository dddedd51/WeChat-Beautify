using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

internal sealed class ColorsWindow:Window {
    readonly GlassSettings draft;readonly PalettePreview preview=new PalettePreview();BitmapSource image;int imageGeneration;
    readonly ListBox areas=new ListBox();readonly TextBox hex=new TextBox();readonly Border swatch=new Border();readonly Slider opacity=new Slider{Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
    readonly TextBlock heading=new TextBlock(),note=new TextBlock(),percentage=new TextBlock(),validation=new TextBlock();readonly CheckBox dark=new CheckBox{Content="编辑深色方案"};readonly StackPanel alphaPanel=new StackPanel();Button saveButton;bool syncing;string key;
    internal ThemeColors Result{get;private set;}
    internal ColorsWindow(Window owner,GlassSettings state,BitmapSource wallpaper,string initial=null){
        Owner=owner;Title="微信美化 · 分区配色";Width=1050;Height=750;MinWidth=850;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=Brush("#172231");Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
        draft=state;draft.Colors=state.Colors.Copy();image=wallpaper;
        var root=new Grid{Margin=new Thickness(24),Background=Background};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var intro=new StackPanel{Margin=new Thickness(0,0,0,18)};intro.Children.Add(new TextBlock{Text="分区配色",FontSize=23,FontWeight=FontWeights.SemiBold});intro.Children.Add(new TextBlock{Text="点击左侧区域或右侧预览选色；浅色、深色方案分别保存。",Foreground=Brush("#B6C9DF"),Margin=new Thickness(0,8,0,0)});root.Children.Add(intro);
        var body=new Grid();Grid.SetRow(body,1);root.Children.Add(body);body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(212)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});body.ColumnDefinitions.Add(new ColumnDefinition());
        areas.Background=Brush("#223247");areas.Foreground=Brushes.White;areas.BorderThickness=new Thickness(0);ScrollViewer.SetHorizontalScrollBarVisibility(areas,ScrollBarVisibility.Disabled);body.Children.Add(areas);
        for(int i=0;i<ThemeColors.Keys.Length;i++)areas.Items.Add(new ListBoxItem{Content=ThemeColors.Names[i],Tag=ThemeColors.Keys[i],Padding=new Thickness(9,10,5,10)});
        var detail=new Grid();Grid.SetColumn(detail,2);body.Children.Add(detail);detail.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});detail.RowDefinitions.Add(new RowDefinition());
        var controls=new StackPanel();detail.Children.Add(controls);dark.Foreground=Brushes.White;dark.IsChecked=draft.Dark;dark.Margin=new Thickness(0,0,0,12);controls.Children.Add(dark);heading.FontSize=17;heading.FontWeight=FontWeights.SemiBold;controls.Children.Add(heading);
        var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,6)};controls.Children.Add(row);swatch.Width=54;swatch.Height=34;swatch.CornerRadius=new CornerRadius(6);row.Children.Add(swatch);hex.Width=120;hex.FontSize=16;hex.VerticalContentAlignment=VerticalAlignment.Center;hex.Margin=new Thickness(10,0,10,0);hex.MaxLength=7;row.Children.Add(hex);var picker=Button("选择颜色…");row.Children.Add(picker);var reset=Button("恢复此项");row.Children.Add(reset);
        validation.Foreground=Brushes.LightSalmon;validation.MinHeight=20;controls.Children.Add(validation);var alphaLabel=new Grid();alphaLabel.Children.Add(new TextBlock{Text="区域不透明度"});percentage.HorizontalAlignment=HorizontalAlignment.Right;alphaLabel.Children.Add(percentage);alphaPanel.Children.Add(alphaLabel);opacity.Margin=new Thickness(0,8,0,4);alphaPanel.Children.Add(opacity);controls.Children.Add(alphaPanel);note.Foreground=Brush("#B6C9DF");note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,8,0,12);controls.Children.Add(note);
        var frame=new Border{CornerRadius=new CornerRadius(10),ClipToBounds=true,BorderBrush=Brush("#486582"),BorderThickness=new Thickness(1),Child=preview};Grid.SetRow(frame,1);detail.Children.Add(frame);preview.Cursor=System.Windows.Input.Cursors.Hand;
        var footer=new DockPanel{Margin=new Thickness(0,18,0,0)};Grid.SetRow(footer,2);root.Children.Add(footer);var allReset=Button("重置当前深浅色方案");DockPanel.SetDock(allReset,Dock.Left);footer.Children.Add(allReset);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};footer.Children.Add(actions);var cancel=Button("取消");cancel.IsCancel=true;actions.Children.Add(cancel);var save=Button("保存配色");save.IsDefault=true;actions.Children.Add(save);
        saveButton=save;areas.SelectionChanged+=(s,e)=>{if(areas.SelectedItem is ListBoxItem item){key=(string)item.Tag;Refresh();}};
        dark.Checked+=async(s,e)=>{draft.Dark=true;Refresh();await ReloadImage();};dark.Unchecked+=async(s,e)=>{draft.Dark=false;Refresh();await ReloadImage();};
        hex.TextChanged+=(s,e)=>{if(syncing||key==null)return;if(!ThemeColors.IsColor(hex.Text)){validation.Text="使用 #RRGGBB，例如 #65B5A1";save.IsEnabled=false;return;}validation.Text="";save.IsEnabled=true;draft.Colors.Set(draft.Dark,key,hex.Text);swatch.Background=Brush(hex.Text);UpdatePreview();};
        opacity.ValueChanged+=(s,e)=>{if(syncing||key==null)return;draft.Colors.SetOpacity(draft.Dark,key,(int)opacity.Value);percentage.Text=((int)opacity.Value)+"%";UpdatePreview();};
        picker.Click+=(s,e)=>{using(var d=new System.Windows.Forms.ColorDialog{FullOpen=true,Color=System.Drawing.ColorTranslator.FromHtml(draft.Colors.Get(draft,key))})if(d.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle))==System.Windows.Forms.DialogResult.OK)hex.Text=String.Format("#{0:X2}{1:X2}{2:X2}",d.Color.R,d.Color.G,d.Color.B);};
        reset.Click+=(s,e)=>{draft.Colors.Reset(draft.Dark,key);Refresh();};allReset.Click+=(s,e)=>{draft.Colors.ResetMode(draft.Dark);Refresh();};save.Click+=(s,e)=>{Result=draft.Colors.Copy();DialogResult=true;};
        preview.Pick+=value=>areas.SelectedIndex=Array.IndexOf(ThemeColors.Keys,value);areas.SelectedIndex=Math.Max(0,Array.IndexOf(ThemeColors.Keys,initial??"outgoing"));
        Closed+=(s,e)=>imageGeneration++;
    }
    async System.Threading.Tasks.Task ReloadImage(){
        if(String.IsNullOrEmpty(draft.Wallpaper))return;int generation=++imageGeneration;
        var snapshot=new GlassSettings{Wallpaper=draft.Wallpaper,Dark=draft.Dark,Blur=draft.Blur,Shade=draft.Shade,CropX=draft.CropX,CropY=draft.CropY,CropW=draft.CropW,CropH=draft.CropH};
        try{string path=await System.Threading.Tasks.Task.Run(()=>Wallpaper.Process(snapshot));if(generation!=imageGeneration)return;var next=new BitmapImage();next.BeginInit();next.CacheOption=BitmapCacheOption.OnLoad;next.UriSource=new Uri(path);next.EndInit();next.Freeze();image=next;UpdatePreview();}catch(Exception e){if(generation==imageGeneration)validation.Text="背景预览失败："+e.Message;}
    }
    void Refresh(){if(key==null)return;syncing=true;heading.Text=ThemeColors.Names[Array.IndexOf(ThemeColors.Keys,key)];hex.Text=draft.Colors.Get(draft,key);swatch.Background=Brush(hex.Text);bool region=Array.IndexOf(ThemeColors.Regions,key)>=0;alphaPanel.Visibility=region?Visibility.Visible:Visibility.Collapsed;opacity.Value=draft.Colors.Opacity(draft.Dark,key);percentage.Text=((int)opacity.Value)+"%";note.Text=region?"0% 保留背景图 / 毛玻璃，100% 使用纯色。无背景图时的透明效果仍需实机确认。":key=="chat"||key=="link"?"当前引擎的消息正文、链接颜色由收发双方共用；气泡底色可独立调整。":"留意文字与底色的对比度。保存后主界面会重新进行可读性检测。";validation.Text="";syncing=false;UpdatePreview();}
    void UpdatePreview(){saveButton.IsEnabled=ThemeColors.IsColor(hex.Text);preview.Selected=key;preview.Update(draft,image);}
    static SolidColorBrush Brush(string value){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));}
    static Button Button(string title){return new Button{Content=title,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0),Background=Brush("#DCE8F5"),Foreground=Brush("#172231")};}
    sealed class DialogOwner:System.Windows.Forms.IWin32Window {public IntPtr Handle{get;private set;}internal DialogOwner(IntPtr handle){Handle=handle;}}
}
