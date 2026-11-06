#define NOMINMAX
#include "compositor.h"
#include <cassert>
#include <cstdio>
int main(int argc,char**argv){
    using namespace beautify;int w=900,h=680;std::vector<Color>input(w*h),output(w*h);
    unsigned char padded[24]={1,2,3,255,4,5,6,128,0,0,0,0,7,8,9,64,10,11,12,32};Color converted[4];
    convertPixels(padded,12,converted,2,2,true);
    assert(converted[0].b==3&&converted[0].r==1&&converted[3].r==10&&converted[3].a==32);
    convertPixels(padded,12,converted,2,2,false);
    assert(converted[0].b==1&&converted[0].r==3&&converted[3].b==10&&converted[3].a==32);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++)input[y*w+x]=x<60?Color{222,222,222,255}:x<300?Color{238,238,238,255}:Color{250,250,250,255};
    // Isolated image island surrounded by a colored boundary must be preserved.
    for(int y=160;y<250;y++)for(int x=500;x<620;x++)input[y*w+x]=Color{70,80,200,255};
    for(int y=168;y<242;y++)for(int x=508;x<612;x++)input[y*w+x]=Color{250,250,250,255};
    // Gray text ring: both soft edges and the enclosed counter must be recomposited.
    Color grayInk={150,150,150,255};
    for(int y=100;y<116;y++)for(int x=180;x<192;x++){
        int edge=std::min({x-180,191-x,y-100,115-y});
        if(edge<3)input[y*w+x]=mix(Color{238,238,238,255},grayInk,edge==0?64:edge==1?180:255);
    }
    auto original=input;Theme t;t.enabled=true;t.wallW=t.wallH=16;t.wallpaper.assign(256,Color{230,225,215,255});
    auto m=compose((unsigned char*)input.data(),w*4,output.data(),w,h,t);
    assert(m.accepted&&m.nav==60&&m.list==300&&m.changed>300000);
    assert(distance(output[400*w+600],t.wallpaper[0])==0);
    assert(memcmp(input.data(),original.data(),input.size()*4)==0);
    assert(distance(output[100*w+184],mix(t.wallpaper[0],grayInk,64))<=1);
    assert(distance(output[102*w+184],grayInk)==0);
    assert(distance(output[107*w+184],t.wallpaper[0])==0);
    for(int y=160;y<250;y++)for(int x=500;x<620;x++)assert(memcmp(&input[y*w+x],&output[y*w+x],4)==0);
    t.enabled=false;compose((unsigned char*)input.data(),w*4,output.data(),w,h,t);assert(memcmp(input.data(),output.data(),input.size()*4)==0);
    assert(!compose((unsigned char*)input.data(),w*4,output.data(),300,300,t).accepted);
    puts("PASS: RGBA/BGRA with row padding, detected columns, background replacement, isolated image preservation, source immutability, disable restores, small window rejection");
    if(argc>=4){
        w=atoi(argv[2]);h=atoi(argv[3]);assert(w>=700&&w<=4096&&h>=450&&h<=4096);
        input.resize((size_t)w*h);output.resize(input.size());FILE*f=fopen(argv[1],"rb");assert(f);
        assert(fread(input.data(),4,input.size(),f)==input.size());fclose(f);t.enabled=true;
        auto actual=compose((unsigned char*)input.data(),w*4,output.data(),w,h,t);
        printf("Screenshot: accepted=%d nav=%d list=%d input=%d changed=%zu\n",actual.accepted,actual.nav,actual.list,actual.input,actual.changed);
        assert(actual.accepted&&actual.changed>200000);
        if(argc==5){f=fopen(argv[4],"wb");assert(f);assert(fwrite(output.data(),4,output.size(),f)==output.size());fclose(f);}
    }
}
