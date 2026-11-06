// Independent native-paint prototype. Interface facts are bound to the verified PE.
// No screenshot processing, object-table cloning, or retained per-widget mutations.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <vector>
#include <string>
#include <algorithm>
#include "semantic-policy.h"
#include "semantic-layout.h"
#include "paint-targets.generated.h"
static unsigned char*module;
static DWORD guiThread;
static wchar_t configPath[MAX_PATH],reportPath[MAX_PATH],receiptPath[MAX_PATH];
static wchar_t revision[80]={};
static bool pendingReceipt;
static bool enabled=false;
static thread_local bool inside=false;
static FILETIME writeTime={};
static DWORD lastPoll;
static bool forceReload=true;
static void*rootObject;
static void*rootIdentity;
static UINT_PTR configTimer;
static unsigned long paintCount;
static void*imageAsset;
static int imageWidth,imageHeight;
static std::wstring imagePath;
static unsigned colors[4]={0xffd3e6e0,0xffe7edf7,0xfff2eff7,0xffe0efe8};
static int opacity[4]={35,20,10,35};
static unsigned primaryText=0xff1c2128,secondaryText=0xff414a56;
static unsigned selectionColor=0xff39c5bb,selectedText=0xff103b3a;
static bool readable(const void*p,size_t length){
    auto at=(uintptr_t)p;if(!at||at+length<at)return false;MEMORY_BASIC_INFORMATION m;
    return VirtualQuery(p,&m,sizeof m)&&m.State==MEM_COMMIT&&!(m.Protect&(PAGE_NOACCESS|PAGE_GUARD))&&at+length<=(uintptr_t)m.BaseAddress+m.RegionSize;
}
template<class T>static T get(const void*p,T fallback=T{}){return readable(p,sizeof(T))?*(const T*)p:fallback;}
static void log(const char*what,int a=0,int b=0){FILE*f=_wfopen(reportPath,L"a");if(f){fprintf(f,"%s %d %d tid=%lu\n",what,a,b,GetCurrentThreadId());fclose(f);}}
static void receipt(const wchar_t*state){
    wchar_t temp[MAX_PATH];if(swprintf(temp,MAX_PATH,L"%ls.tmp",receiptPath)<0)return;
    FILE*f=_wfopen(temp,L"w, ccs=UTF-8");if(!f)return;
    fwprintf(f,L"protocol=1\npid=%lu\nrevision=%ls\nstate=%ls\nwallLoaded=%d\n",GetCurrentProcessId(),revision,state,imageAsset?1:0);fclose(f);
    MoveFileExW(temp,receiptPath,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH);
}
using Paint=uintptr_t(*)(void*,void*);
static Paint originalRow;
static bool installed;
struct Patched {void**slot;void*original;const PaintTarget*target;unsigned observed=0;};
static std::vector<Patched>patched;
static const char*typeOf(void*object){auto table=get<uintptr_t>(object);if(table<(uintptr_t)module)return "unknown";unsigned rva=(unsigned)(table-(uintptr_t)module);for(const auto&t:objectTypes)if(t.primary==rva)return t.name;return "unknown";}
static void*parentOf(void*object){auto data=get<unsigned char*>((unsigned char*)object+8);return data?get<void*>(data+16):nullptr;}
using Geometry=semantic::Geometry;
static bool geometry(void*object,Geometry&g){auto d=get<unsigned char*>((unsigned char*)object+0x28);if(!readable(d,0x24))return false;g.x=get<int>(d+0x14);g.y=get<int>(d+0x18);int64_t w=(int64_t)get<int>(d+0x1c)-g.x+1,h=(int64_t)get<int>(d+0x20)-g.y+1;if(w<=0||h<=0||w>16777215||h>16777215)return false;g.width=(int)w;g.height=(int)h;return semantic::validGeometry(g);}
struct Location {Geometry local{},root{};int x=0,y=0;void*rootObject=nullptr;void*rowObject=nullptr;semantic::Region region=semantic::Region::Base;bool valid=false,rowPath=false,rowText=false;};
static Location locate(void*object){
    Location l;semantic::LayoutNode nodes[48]{};void*objects[48]{};size_t count=0;
    void*node=object;for(int depth=0;node&&depth<48;depth++){
        const char*name=typeOf(node);if(!strcmp(name,"unknown")||semantic::protectedBranch(name))return l;
        auto role=semantic::classify(name);
        if(l.region==semantic::Region::Base&&role!=semantic::Region::Base&&role!=semantic::Region::Preserve)l.region=role;
        Geometry g;if(!geometry(node,g))return l;
        nodes[count]={(unsigned)(get<uintptr_t>(node)-(uintptr_t)module),g};objects[count++]=node;
        if(nodes[count-1].type==0x983c628){
            auto layout=semantic::locateRoot(nodes,count);l.local=layout.local;l.root=layout.root;l.x=layout.x;l.y=layout.y;l.valid=layout.valid;if(l.valid)l.rootObject=objects[layout.rootIndex];l.rowPath=semantic::sessionRowPrefix(nodes,count);
            // Observed label -> QWidget -> QWidget -> label container -> row.
            l.rowText=count>=9&&(nodes[0].type==0x9883ab8||nodes[0].type==0x93ab1e8)&&nodes[1].type==0x8ffbb98&&nodes[2].type==0x8ffbb98&&nodes[3].type==0x9d261e8&&semantic::sessionRowPrefix(nodes+4,count-4);
            if(l.rowText)l.rowObject=objects[4];return l;
        }
        void*next=parentOf(node);if(next==node)return l;node=next;
    }
    return l;
}
// Verified constructor/wrapper ABI: the wrapper owns a raster reference after the QImage dies.
static void releaseImage(void*p){if(readable(p,16)&&InterlockedDecrement((LONG*)((unsigned char*)p+8))==0){auto table=get<uintptr_t*>(p);auto dtor=get<void(*)(void*)>(table+1);if(dtor)dtor(p);}}
struct StringBlock {int refs,length,capacity,reserved;intptr_t offset;};
static void*loadImage(const wchar_t*file){
    size_t len=wcslen(file);if(!len||len>=1024)return nullptr;
    std::vector<uint64_t>storage((sizeof(StringBlock)+(len+1)*2+7)/8);auto*b=(StringBlock*)storage.data();b->refs=-1;b->length=(int)len;b->capacity=0;b->offset=sizeof(StringBlock);memcpy((char*)b+b->offset,file,(len+1)*2);void*string=b;
    alignas(16) unsigned char qimage[64]={},wrapper[64]={};
    ((void*(*)(void*,void*,const char*))(module+0x4fd250))(qimage,&string,nullptr);
    ((void*(*)(void*,void*))(module+0x70b60))(wrapper,qimage);
    ((void(*)(void*))(module+0x4fdbc0))(qimage);
    void*out=get<void*>(wrapper);return readable(out,40)?out:nullptr;
}
static bool reload(){
    DWORD now=GetTickCount();if(!forceReload&&now-lastPoll<500)return false;lastPoll=now;WIN32_FILE_ATTRIBUTE_DATA a;
    if(!GetFileAttributesExW(configPath,GetFileExInfoStandard,&a)||(!forceReload&&CompareFileTime(&writeTime,&a.ftLastWriteTime)==0))return false;
    forceReload=false;
    writeTime=a.ftLastWriteTime;enabled=GetPrivateProfileIntW(L"theme",L"enabled",0,configPath)==1;
    GetPrivateProfileStringW(L"theme",L"revision",L"",revision,80,configPath);bool failed=false;
    wchar_t path[1024];GetPrivateProfileStringW(L"theme",L"wallpaper",L"",path,1024,configPath);
    if(enabled&&imagePath!=path){
        void*next=path[0]?loadImage(path):nullptr;
        if(!path[0]||next){void*old=imageAsset;imageAsset=next;imagePath=path;imageWidth=next?get<int>((unsigned char*)next+32):0;imageHeight=next?get<int>((unsigned char*)next+36):0;releaseImage(old);}
        else{enabled=false;failed=true;log("image-load-failed");}
    }
    const wchar_t*keys[]={L"navigation",L"list",L"canvas",L"input"};
    for(int i=0;i<4;i++){wchar_t text[32],key[48];GetPrivateProfileStringW(L"theme",keys[i],L"",text,32,configPath);unsigned c;if(wcslen(text)==7&&swscanf(text,L"#%x",&c)==1)colors[i]=0xff000000|c;swprintf(key,48,L"%lsOpacity",keys[i]);opacity[i]=std::clamp((int)GetPrivateProfileIntW(L"theme",key,20,configPath),0,100);}
    const wchar_t*textKeys[]={L"text",L"secondaryText",L"selection",L"selectedText"};unsigned*textValues[]={&primaryText,&secondaryText,&selectionColor,&selectedText};
    for(int i=0;i<4;i++){wchar_t value[32];unsigned c;GetPrivateProfileStringW(L"theme",textKeys[i],L"",value,32,configPath);if(wcslen(value)==7&&swscanf(value,L"#%x",&c)==1)*textValues[i]=0xff000000|c;}
    pendingReceipt=!failed;receipt(failed?L"failed":L"pending");
    log(enabled?"config-enabled":"config-disabled",imageWidth,imageHeight);
    return true;
}
static void CALLBACK tick(HWND,UINT,UINT_PTR,DWORD){
    if(inside||GetCurrentThreadId()!=guiThread)return;inside=true;
    try{if(reload()&&rootObject&&get<void*>((unsigned char*)rootObject+8)==rootIdentity&&!strcmp(typeOf(rootObject),"mmui::MainView")){
        ((void(*)(void*))(module+0x5b7260))(rootObject);log("root-update-requested",enabled);
    }}catch(...){enabled=false;log("config-exception");}inside=false;
}
static bool render(void*canvas,const Location&l,bool selected=false){
    if(!readable(canvas,8))return false;
    if(imageAsset&&imageWidth>0&&imageHeight>0&&!selected){
    float scale=std::max((float)l.root.width/imageWidth,(float)l.root.height/imageHeight);
    float left=(l.root.width-imageWidth*scale)/2-l.x,top=(l.root.height-imageHeight*scale)/2-l.y;
    float imageRect[]={left,top,left+imageWidth*scale,top+imageHeight*scale};
    alignas(8) unsigned char sampling[24]={};sampling[16]=1;
    ((void(*)(void*,void*,const float*,const void*,const void*))(module+0x53fa30))(canvas,imageAsset,imageRect,sampling,nullptr);
    }
    int index=l.region==semantic::Region::Navigation?0:l.region==semantic::Region::List?1:l.region==semantic::Region::Input?3:2;
    if(selected||!imageAsset||opacity[index]){
        alignas(16) unsigned char paint[80]={};
        ((void*(*)(void*))(module+0x532fd0))(paint);
        unsigned color=selected?selectionColor:(colors[index]&0xffffff)|((unsigned)(imageAsset?opacity[index]*255/100:255)<<24);
        ((void(*)(void*,unsigned))(module+0x533980))(paint,color);
        float rect[]={0,0,(float)l.local.width,(float)l.local.height};
        ((void(*)(void*,const float*,const void*))(module+0x53cd00))(canvas,rect,paint);
        ((void(*)(void*))(module+0x5332b0))(paint);
    }
    return true;
}
static void paintColor(void*object,unsigned color){
    auto*field=(unsigned*)((unsigned char*)object+0x234);if(*field==color)return;
    // Verified setter split: update the native color/cache, invalidate the
    // paragraph with notify=false. Scheduling QWidget updates inside paint
    // would create a perpetual repaint loop when the temporary color restores.
    *field=color;((void(*)(void*))(module+0x1e9f0c0))((unsigned char*)object+0x188);
    auto holder=get<void*>((unsigned char*)object+0x3e0);auto paragraph=get<void*>(holder);
    ((void(*)(void*,int,bool))(module+0x6249740))(paragraph,1,false);
}
static bool canColor(void*object){
    auto holder=get<void*>((unsigned char*)object+0x3e0);auto paragraph=get<void*>(holder);auto table=get<unsigned char*>(paragraph);
    return readable((unsigned char*)object+0x234,4)&&readable(paragraph,0x240)&&get<void*>(table+0x78)==module+0x44e7bd0;
}
static uintptr_t rowDispatch(void*self,void*canvas){
    if(GetCurrentThreadId()!=guiThread||inside||!enabled)return originalRow(self,canvas);
    auto*object=(unsigned char*)self-0x120;
    if(strcmp(typeOf(object),"mmui::ChatSessionCell"))return originalRow(self,canvas);
    inside=true;Location l=locate(object);bool changed=false;
    try{if(l.valid&&l.rowPath&&l.region==semantic::Region::List)changed=render(canvas,l,get<unsigned char>(object+0x210)==1);}catch(...){enabled=false;}
    static unsigned rowCount=0;if(changed&&++rowCount==1)log("session-row-background",l.x,l.y);
    inside=false;return changed?0:originalRow(self,canvas);
}
static uintptr_t dispatch(void*self,void*canvas){
    auto table=get<void**>(self);if(!table)return 0;Patched*entry=nullptr;for(auto&r:patched)if(r.slot==table+1){entry=&r;break;}if(!entry)return 0;
    auto original=(Paint)entry->original;
    if(inside||GetCurrentThreadId()!=guiThread)return original(self,canvas);
    inside=true;void*object=(unsigned char*)self-192;Location l=locate(object);
    bool changed=false;
    try{
        if(l.valid){rootObject=l.rootObject;rootIdentity=get<void*>((unsigned char*)rootObject+8);}
        if(l.valid&&pendingReceipt){pendingReceipt=false;receipt(enabled?L"applied":L"disabled");}
        if(entry->observed++==0){log(entry->target->name,l.valid,l.local.width);}
        if(enabled&&l.valid&&l.rowText&&entry->target->callback==0x1ea62d0&&canColor(object)){
            unsigned saved=get<unsigned>((unsigned char*)object+0x234),desired=get<unsigned char>((unsigned char*)l.rowObject+0x210)==1?selectedText:entry->target->primary==0x9883ab8?primaryText:secondaryText;
            auto setter=paintColor;
            // The original native text painter sees the chosen color. Restore
            // immediately so selection, future pages and theme-off keep native state.
            struct Restore {void*object;unsigned color;decltype(setter)fn;~Restore(){fn(object,color);}} restore{object,saved,setter};
            setter(object,desired);uintptr_t result=original(self,canvas);setter(object,saved);inside=false;return result;
        }
        // An attached custom paint resource is not a plain background surface.
        bool custom=get<void*>((unsigned char*)object+0x160)!=nullptr;
        bool namedRegion=l.region!=semantic::Region::Base||!strcmp(entry->target->name,"mmui::MainView");
        if(enabled&&l.valid&&namedRegion&&!custom&&entry->target->callback!=0x1ea62d0&&semantic::classify(entry->target->name)!=semantic::Region::Preserve)changed=render(canvas,l);
        if(changed&&++paintCount%240==1)log("native-background",(int)paintCount,l.local.width);
    }catch(...){enabled=false;log("exception-disabled");}
    inside=false;return changed?0:original(self,canvas);
}
static bool setSlot(void**slot,void*expected,void*desired){
    DWORD old;if(!VirtualProtect(slot,8,PAGE_READWRITE,&old))return false;
    void*found=InterlockedCompareExchangePointer(slot,desired,expected);DWORD unused;VirtualProtect(slot,8,old,&unused);return found==expected;
}
static bool restoreSlots(){
    bool ok=true;for(auto&r:patched){if(get<void*>(r.slot)==(void*)dispatch)setSlot(r.slot,(void*)dispatch,r.original);if(get<void*>(r.slot)==(void*)dispatch)ok=false;}
    auto rowSlot=(void**)(module+0x9b64d50);if(get<void*>(rowSlot)==(void*)rowDispatch)setSlot(rowSlot,(void*)rowDispatch,(void*)originalRow);
    return get<void*>(rowSlot)!=(void*)rowDispatch&&ok;
}
extern "C" __declspec(dllexport) void semantic_start(const wchar_t*folder){
    if(installed)return;
    if(guiThread&&guiThread!=GetCurrentThreadId())return;
    module=(unsigned char*)GetModuleHandleW(L"Weixin.dll");if(!module)return;
    if(wcslen(folder)>MAX_PATH-30)return;
    guiThread=GetCurrentThreadId();swprintf(configPath,MAX_PATH,L"%ls\\theme.ini",folder);swprintf(reportPath,MAX_PATH,L"%ls\\semantic.log",folder);swprintf(receiptPath,MAX_PATH,L"%ls\\theme.receipt",folder);
    for(const auto&t:paintTargets){auto slot=(void**)(module+t.table+8);void*expected=module+t.callback;
        if(get<void*>(slot)!=expected){log("slot-validation-failed",t.table);return;}}
    auto rowSlot=(void**)(module+0x9b64d50);originalRow=(Paint)(module+0x6013f30);
    if(get<void*>(rowSlot)!=(void*)originalRow){log("row-slot-validation-failed");return;}
    // Fully construct the immutable lookup before another thread can enter a hook.
    if(patched.empty()){patched.reserve(sizeof(paintTargets)/sizeof(paintTargets[0]));for(const auto&t:paintTargets)patched.push_back({(void**)(module+t.table+8),module+t.callback,&t});}
    for(auto&r:patched){
        if(!setSlot(r.slot,r.original,(void*)dispatch)){log(restoreSlots()?"install-rolled-back":"rollback-incomplete");return;}}
    if(!setSlot(rowSlot,(void*)originalRow,(void*)rowDispatch)){log(restoreSlots()?"row-install-rolled-back":"rollback-incomplete");return;}
    configTimer=SetTimer(nullptr,0,250,tick);if(!configTimer){log(restoreSlots()?"timer-install-rolled-back":"rollback-incomplete");return;}
    forceReload=true;
    installed=true;
    log("native-paint-ready",(int)patched.size());
    log("config-timer",1);
}
// Called only on the GUI thread. The module remains loaded until process exit.
extern "C" __declspec(dllexport) int semantic_stop(){
    if(GetCurrentThreadId()!=guiThread||inside)return 0;
    enabled=false;if(configTimer){KillTimer(nullptr,configTimer);configTimer=0;}
    bool restored=restoreSlots();
    if(rootObject&&get<void*>((unsigned char*)rootObject+8)==rootIdentity&&!strcmp(typeOf(rootObject),"mmui::MainView"))((void(*)(void*))(module+0x5b7260))(rootObject);
    releaseImage(imageAsset);imageAsset=nullptr;imagePath.clear();forceReload=true;installed=!restored;
    log(restored?"native-paint-stopped":"native-paint-disabled-restore-incomplete");return restored?1:0;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(h);return TRUE;}
