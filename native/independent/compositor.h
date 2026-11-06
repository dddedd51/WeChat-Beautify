#pragma once
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <stdint.h>
#include <vector>
#include <algorithm>
#include <cmath>
#include <cstring>

namespace beautify {
struct Color { unsigned char b,g,r,a; };
struct Theme {
    bool enabled=false,dark=false;
    Color regions[4]={{244,238,232,255},{244,238,232,255},{244,238,232,255},{244,238,232,255}};
    int opacity[4]={0,0,0,0};
    Color outgoing={237,217,187,255},incoming={249,245,242,255},selection={224,170,120,255};
    Color text={40,33,28,255},secondary={131,112,97,255},chat={40,33,28,255},link={180,95,38,255};
    std::vector<Color> wallpaper;int wallW=0,wallH=0;
};
struct Metrics { int nav=0,list=0,input=0;size_t changed=0;bool accepted=false; };
// DXGI stores either RGBA or BGRA; normalize before pixel classification.
inline void convertPixels(const unsigned char*src,int stride,Color*dst,int w,int h,bool rgba){
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){
        const unsigned char*p=src+(size_t)y*stride+x*4;
        dst[(size_t)y*w+x]=rgba?Color{p[2],p[1],p[0],p[3]}:Color{p[0],p[1],p[2],p[3]};
    }
}
inline int distance(Color a,Color b){return std::max({abs(a.b-b.b),abs(a.g-b.g),abs(a.r-b.r)});}
inline Color mix(Color a,Color b,int alpha){Color c;c.b=(unsigned char)((a.b*(255-alpha)+b.b*alpha+127)/255);c.g=(unsigned char)((a.g*(255-alpha)+b.g*alpha+127)/255);c.r=(unsigned char)((a.r*(255-alpha)+b.r*alpha+127)/255);c.a=255;return c;}
inline bool neutral(Color c){return std::max({c.r,c.g,c.b})-std::min({c.r,c.g,c.b})<=9;}
inline Color pixel(const unsigned char*p,int stride,int x,int y){return *(const Color*)(p+(size_t)y*stride+x*4);}
// Uses only original buffer pixels. Works on a separate output, never edits the app's source.
inline Metrics compose(const unsigned char*source,int stride,Color*out,int w,int h,const Theme&t){
    Metrics m;
    if(w<700||h<450||w>8192||h>8192)return m;
    for(int y=0;y<h;y++)memcpy(out+(size_t)y*w,source+(size_t)y*stride,(size_t)w*4);
    if(!t.enabled||t.wallW<=0||t.wallH<=0||t.wallpaper.size()!=(size_t)t.wallW*t.wallH)return m;
    // Detect three stable vertical fields in the lower middle, excluding title and toolbar.
    int scanY=h*62/100;
    auto sample=[&](int x){Color s[5];for(int k=0;k<5;k++)s[k]=pixel(source,stride,x,std::min(h-1,scanY+k*3));std::sort(s,s+5,[](Color a,Color b){return a.r<b.r;});return s[2];};
    Color nav=sample(18);int navEnd=0,listEnd=0;
    if(!neutral(nav))return m;
    for(int x=38;x<std::min(130,w/6);x++)if(distance(sample(x),nav)>6){navEnd=x;break;}
    if(!navEnd)return m;
    Color list=sample(navEnd+30);
    if(!neutral(list))return m;
    for(int x=navEnd+160;x<std::min(w*55/100,600);x++)if(distance(sample(x),list)>5){
        Color after=sample(std::min(w-1,x+8));if(neutral(after)&&distance(after,list)>5){listEnd=x;break;}
    }
    if(!listEnd||w-listEnd<300)return m;
    Color canvas=sample(listEnd+30);if(!neutral(canvas))return m;
    m.nav=navEnd;m.list=listEnd;m.input=h-145;m.accepted=true;
    // Find a wide horizontal input boundary; retain proportional fallback only for rendering background.
    for(int y=h*56/100;y<h-65;y++){
        int edge=0;for(int x=listEnd+18;x<w-25;x+=12){Color a=pixel(source,stride,x,y),b=pixel(source,stride,x,y-3);if(distance(a,b)>7&&neutral(a)&&neutral(b))edge++;}
        if(edge>(w-listEnd-45)/12*8/10){m.input=y;break;}
    }
    const int n=w*h;std::vector<unsigned char>mask(n,0);std::vector<int>queue;queue.reserve(n/2);
    Color originals[4]={nav,list,canvas,canvas};
    auto region=[&](int x,int y){return x<navEnd?0:x<listEnd?1:y>=m.input?3:2;};
    // Flood only broad neutral surfaces connected to safe seeds. Isolated image interiors stay unchanged.
    auto seed=[&](int x,int y){if(x<0||x>=w||y<35||y>=h-4)return;int i=y*w+x,k=region(x,y);if(!mask[i]&&distance(pixel(source,stride,x,y),originals[k])<=3){mask[i]=(unsigned char)(k+1);queue.push_back(i);}};
    for(int y=48;y<h-8;y+=36){seed(navEnd/2,y);seed(navEnd+8,y);seed(listEnd+10,y);seed(w-12,y);}
    for(int x=listEnd+15;x<w-10;x+=45)seed(x,h-22);
    for(size_t qi=0;qi<queue.size();qi++){
        int i=queue[qi],x=i%w,y=i/w;
        for(int d=0;d<4;d++){
            int nx=x+(d==0)-(d==1),ny=y+(d==2)-(d==3);
            if(nx<2||nx>=w-2||ny<33||ny>=h-3)continue;
            int j=ny*w+nx,k=region(nx,ny);if(mask[j])continue;
            if(distance(pixel(source,stride,nx,ny),originals[k])<=3){mask[j]=(unsigned char)(k+1);queue.push_back(j);}
        }
    }
    float ratio=std::max((float)w/t.wallW,(float)h/t.wallH);float ox=(t.wallW-w/ratio)/2,oy=(t.wallH-h/ratio)/2;
    auto background=[&](int x,int y,int k){int sx=std::clamp((int)(ox+x/ratio),0,t.wallW-1),sy=std::clamp((int)(oy+y/ratio),0,t.wallH-1);return mix(t.wallpaper[(size_t)sy*t.wallW+sx],t.regions[k],t.opacity[k]*255/100);};
    // Text is already flattened onto the old background. Work on complete small
    // neutral components, including enclosed glyph counters, instead of only
    // replacing the outermost antialiased pixel. Large/colored components remain
    // original (avatars, bubbles, photos and panels cannot be identified as text).
    std::vector<unsigned char>seen(n,0),glyph(n,0);
    std::vector<Color>foreground(n);std::vector<int>component;
    const int glyphLimit=std::clamp(navEnd*3/5,30,72);
    for(int sy=35;sy<h-4;sy++)for(int sx=3;sx<w-3;sx++){
        int begin=sy*w+sx;if(mask[begin]||seen[begin])continue;
        int field=region(sx,sy),minX=sx,maxX=sx,minY=sy,maxY=sy,touch=0,colored=0;
        Color ink=originals[field];int darkest=ink.r+ink.g+ink.b;
        component.clear();component.push_back(begin);seen[begin]=1;
        for(size_t at=0;at<component.size();at++){
            int i=component[at],x=i%w,y=i/w;Color c=pixel(source,stride,x,y);
            minX=std::min(minX,x);maxX=std::max(maxX,x);minY=std::min(minY,y);maxY=std::max(maxY,y);
            if(!neutral(c))colored++;
            int brightness=c.r+c.g+c.b;if(brightness<darkest){darkest=brightness;ink=c;}
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                if(!dx&&!dy)continue;int nx=x+dx,ny=y+dy;
                if(nx<2||nx>=w-2||ny<34||ny>=h-3||region(nx,ny)!=field)continue;
                int j=ny*w+nx;if(mask[j]){touch++;continue;}
                if(!seen[j]){seen[j]=1;component.push_back(j);}
            }
        }
        if(field==1&&minX<navEnd+53)continue; // Contact avatar column.
        int widthLimit=field==1?listEnd-navEnd-53:glyphLimit*3;
        if(maxY-minY+1>glyphLimit||maxX-minX+1>widthLimit||!touch||
           colored>0||darkest>originals[field].r+originals[field].g+originals[field].b-24)continue;
        // Do not flatten solid gray rectangular controls into the wallpaper.
        int core=0;for(int i:component)if(distance(pixel(source,stride,i%w,i/w),ink)<4)core++;
        if(component.size()>12&&core*100>component.size()*80)continue;
        for(int i:component){glyph[i]=1;foreground[i]=ink;}
    }
    for(int y=34;y<h-3;y++)for(int x=2;x<w-2;x++){
        int i=y*w+x,k=region(x,y);Color src=pixel(source,stride,x,y);
        if(mask[i]){out[i]=background(x,y,k);m.changed++;continue;}
        if(glyph[i]){
            Color ink=foreground[i],old=originals[k];
            int light=old.r+old.g+old.b,value=src.r+src.g+src.b,low=ink.r+ink.g+ink.b;
            int alpha=std::clamp((light-value)*255/std::max(1,light-low),0,255);
            out[i]=mix(background(x,y,k),ink,alpha);m.changed++;
        }
    }
    return m;
}
}
