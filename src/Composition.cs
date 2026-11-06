using System;
using System.Windows;

// A viewport into the original image. The same normalized rectangle is exported
// through Wallpaper.Crop; dragging moves the image, hence the inverse offset.
internal sealed class Composition {
    internal const double Ratio=760d/400;
    internal Rect Crop {get;private set;}
    readonly Rect full;
    internal double Zoom=>full.Width/Crop.Width;
    internal Composition(double imageRatio,Rect crop){full=CropMath.Fit(Ratio,imageRatio,new Rect(0,0,1,1));Crop=CropMath.Fit(Ratio,imageRatio,CropMath.Clamp(crop));}
    internal void Pan(Rect start,double dx,double dy){Crop=CropMath.Clamp(new Rect(start.X-dx*start.Width,start.Y-dy*start.Height,start.Width,start.Height));}
    internal void SetZoom(double zoom){zoom=Math.Max(1,Math.Min(8,zoom));double w=full.Width/zoom,h=full.Height/zoom;Crop=CropMath.Clamp(new Rect(Crop.X+(Crop.Width-w)/2,Crop.Y+(Crop.Height-h)/2,w,h));}
    internal void Reset(){Crop=full;}
}

// Manual edits invalidate this one-step undo, so it cannot restore an unrelated
// recommendation after the user has moved on to another shade or theme.
internal sealed class ShadeUndo {
    int? previous;int applied;
    internal bool Available=>previous.HasValue;
    internal void Capture(int before,int after){previous=before;applied=after;}
    internal void Observe(int value){if(value!=applied)previous=null;}
    internal void Clear(){previous=null;}
    internal bool Take(out int value){value=previous.GetValueOrDefault();bool available=Available;Clear();return available;}
}
