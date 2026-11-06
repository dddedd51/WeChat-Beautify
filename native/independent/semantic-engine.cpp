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
#include "selected-adapter.h"
#include BEAUTIFY_PAINT_HEADER
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
static void receipt(const wchar_t*state,const wchar_t*reason=L""){
    wchar_t temp[MAX_PATH];if(swprintf(temp,MAX_PATH,L"%ls.tmp",receiptPath)<0)return;
    FILE*f=_wfopen(temp,L"w, ccs=UTF-8");if(!f)return;
    fwprintf(f,L"protocol=1\npid=%lu\nrevision=%ls\nstate=%ls\nreason=%ls\nwallLoaded=%d\n",GetCurrentProcessId(),revision,state,reason,imageAsset?1:0);fclose(f);
    MoveFileExW(temp,receiptPath,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH);
}
using Paint=uintptr_t(*)(void*,void*);
static Paint originalRow;
static bool installed;
struct Patched {void**slot;void*original;const PaintTarget*target;unsigned observed=0;};
static std::vector<Patched>patched;
static const char*typeOf(void*object){auto table=get<uintptr_t>(object);if(table<(uintptr_t)module)return "unknown";unsigned rva=(unsigned)(table-(uintptr_t)module);for(const auto&t:objectTypes)if(t.primary==rva)return t.name;return "unknown";}
static void*parentOf(void*object){auto data=get<unsigned char*>((unsigned char*)object+adapter::ObjectDataOffset);return data?get<void*>(data+adapter::ParentOffset):nullptr;}
using Geometry=semantic::Geometry;
static bool geometry(void*object,Geometry&g){auto d=get<unsigned char*>((unsigned char*)object+adapter::WidgetDataOffset);if(!readable(d,adapter::GeometryBottom+sizeof(int)))return false;g.x=get<int>(d+adapter::GeometryX);g.y=get<int>(d+adapter::GeometryY);int64_t w=(int64_t)get<int>(d+adapter::GeometryRight)-g.x+1,h=(int64_t)get<int>(d+adapter::GeometryBottom)-g.y+1;if(w<=0||h<=0||w>16777215||h>16777215)return false;g.width=(int)w;g.height=(int)h;return semantic::validGeometry(g);}
struct Location {Geometry local{},root{};int x=0,y=0;void*rootObject=nullptr;void*rowObject=nullptr;semantic::Region region=semantic::Region::Base;bool valid=false,rowPath=false,rowText=false;};
static Location locate(void*object){
    Location l;semantic::LayoutNode nodes[48]{};void*objects[48]{};size_t count=0;
    void*node=object;for(int depth=0;node&&depth<48;depth++){
        const char*name=typeOf(node);if(!strcmp(name,"unknown")||semantic::protectedBranch(name))return l;
        auto role=semantic::classify(name);
        if(l.region==semantic::Region::Base&&role!=semantic::Region::Base&&role!=semantic::Region::Preserve)l.region=role;
        Geometry g;if(!geometry(node,g))return l;
        nodes[count]={(unsigned)(get<uintptr_t>(node)-(uintptr_t)module),g};objects[count++]=node;
        if(adapter::IsMainWindow(nodes[count-1].type)){
            auto layout=semantic::locateRoot(nodes,count);l.local=layout.local;l.root=layout.root;l.x=layout.x;l.y=layout.y;l.valid=layout.valid;if(l.valid)l.rootObject=objects[layout.rootIndex];l.rowPath=semantic::sessionRowPrefix(nodes,count);
            // Observed label -> QWidget -> QWidget -> label container -> row.
            l.rowText=count>=9&&(nodes[0].type==adapter::PrimaryText||nodes[0].type==adapter::TextView)&&nodes[1].type==adapter::Widget&&nodes[2].type==adapter::Widget&&nodes[3].type==adapter::LabelContainer&&semantic::sessionRowPrefix(nodes+4,count-4);
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
    ((void*(*)(void*,void*,const char*))(module+adapter::ImageConstruct))(qimage,&string,nullptr);
    ((void*(*)(void*,void*))(module+adapter::ImageWrap))(wrapper,qimage);
    ((void(*)(void*))(module+adapter::ImageDestroy))(qimage);
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
        ((void(*)(void*))(module+adapter::WidgetUpdate))(rootObject);log("root-update-requested",enabled);
    }}catch(...){enabled=false;log("config-exception");}inside=false;
}
static bool render(void*canvas,const Location&l,bool selected=false){
    if(!readable(canvas,8))return false;
    if(imageAsset&&imageWidth>0&&imageHeight>0&&!selected){
    float scale=std::max((float)l.root.width/imageWidth,(float)l.root.height/imageHeight);
    float left=(l.root.width-imageWidth*scale)/2-l.x,top=(l.root.height-imageHeight*scale)/2-l.y;
    float imageRect[]={left,top,left+imageWidth*scale,top+imageHeight*scale};
    alignas(8) unsigned char sampling[24]={};sampling[16]=1;
    ((void(*)(void*,void*,const float*,const void*,const void*))(module+adapter::CanvasImage))(canvas,imageAsset,imageRect,sampling,nullptr);
    }
    int index=l.region==semantic::Region::Navigation?0:l.region==semantic::Region::List?1:l.region==semantic::Region::Input?3:2;
    if(selected||!imageAsset||opacity[index]){
        alignas(16) unsigned char paint[80]={};
        ((void*(*)(void*))(module+adapter::PaintConstruct))(paint);
        unsigned color=selected?selectionColor:(colors[index]&0xffffff)|((unsigned)(imageAsset?opacity[index]*255/100:255)<<24);
        ((void(*)(void*,unsigned))(module+adapter::PaintSetColor))(paint,color);
        float rect[]={0,0,(float)l.local.width,(float)l.local.height};
        ((void(*)(void*,const float*,const void*))(module+adapter::CanvasRect))(canvas,rect,paint);
        ((void(*)(void*))(module+adapter::PaintDestroy))(paint);
    }
    return true;
}
static void paintColor(void*object,unsigned color){
    auto*field=(unsigned*)((unsigned char*)object+adapter::TextColorOffset);if(*field==color)return;
    // Verified setter split: update the native color/cache, invalidate the
    // paragraph with notify=false. Scheduling QWidget updates inside paint
    // would create a perpetual repaint loop when the temporary color restores.
    *field=color;((void(*)(void*))(module+adapter::TextCacheInvalidate))((unsigned char*)object+adapter::TextCacheOffset);
    auto holder=get<void*>((unsigned char*)object+adapter::ParagraphHolderOffset);auto paragraph=get<void*>(holder);
    ((void(*)(void*,int,bool))(module+adapter::ParagraphInvalidate))(paragraph,1,false);
}
static bool canColor(void*object){
    auto holder=get<void*>((unsigned char*)object+adapter::ParagraphHolderOffset);auto paragraph=get<void*>(holder);auto table=get<unsigned char*>(paragraph);
    return readable((unsigned char*)object+adapter::TextColorOffset,4)&&readable(paragraph,adapter::ParagraphMinimumBytes)&&get<void*>(table+adapter::ParagraphVirtualOffset)==module+adapter::ParagraphVirtual;
}
static uintptr_t rowDispatch(void*self,void*canvas){
    if(GetCurrentThreadId()!=guiThread||inside||!enabled)return originalRow(self,canvas);
    auto*object=(unsigned char*)self-adapter::RowInterfaceOffset;
    if(strcmp(typeOf(object),"mmui::ChatSessionCell"))return originalRow(self,canvas);
    inside=true;Location l=locate(object);bool changed=false;
    try{if(l.valid&&l.rowPath&&l.region==semantic::Region::List)changed=render(canvas,l,get<unsigned char>(object+adapter::RowSelectedOffset)==1);}catch(...){enabled=false;}
    static unsigned rowCount=0;if(changed&&++rowCount==1)log("session-row-background",l.x,l.y);
    inside=false;return changed?0:originalRow(self,canvas);
}
static uintptr_t dispatch(void*self,void*canvas){
    auto table=get<void**>(self);if(!table)return 0;Patched*entry=nullptr;for(auto&r:patched)if(r.slot==table+1){entry=&r;break;}if(!entry)return 0;
    auto original=(Paint)entry->original;
    if(inside||GetCurrentThreadId()!=guiThread)return original(self,canvas);
    inside=true;void*object=(unsigned char*)self-adapter::PaintInterfaceOffset;Location l=locate(object);
    bool changed=false;
    try{
        if(l.valid){rootObject=l.rootObject;rootIdentity=get<void*>((unsigned char*)rootObject+8);}
        if(l.valid&&pendingReceipt){pendingReceipt=false;receipt(enabled?L"applied":L"disabled");}
        if(entry->observed++==0){log(entry->target->name,l.valid,l.local.width);}
        if(enabled&&l.valid&&l.rowText&&entry->target->callback==adapter::TextPaint&&canColor(object)){
            unsigned saved=get<unsigned>((unsigned char*)object+adapter::TextColorOffset),desired=get<unsigned char>((unsigned char*)l.rowObject+adapter::RowSelectedOffset)==1?selectedText:entry->target->primary==adapter::PrimaryText?primaryText:secondaryText;
            auto setter=paintColor;
            // The original native text painter sees the chosen color. Restore
            // immediately so selection, future pages and theme-off keep native state.
            struct Restore {void*object;unsigned color;decltype(setter)fn;~Restore(){fn(object,color);}} restore{object,saved,setter};
            setter(object,desired);uintptr_t result=original(self,canvas);setter(object,saved);inside=false;return result;
        }
        // An attached custom paint resource is not a plain background surface.
        bool custom=get<void*>((unsigned char*)object+adapter::CustomPaintOffset)!=nullptr;
        bool namedRegion=l.region!=semantic::Region::Base||!strcmp(entry->target->name,"mmui::MainView");
        if(enabled&&l.valid&&namedRegion&&!custom&&entry->target->callback!=adapter::TextPaint&&semantic::classify(entry->target->name)!=semantic::Region::Preserve)changed=render(canvas,l);
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
    auto rowSlot=(void**)(module+adapter::RowSlot);if(get<void*>(rowSlot)==(void*)rowDispatch)setSlot(rowSlot,(void*)rowDispatch,(void*)originalRow);
    return get<void*>(rowSlot)!=(void*)rowDispatch&&ok;
}
// Structural screening for nearby builds. This deliberately does not assert
// that function bodies or private field layouts match the tested build.
// Every check runs before the first vtable write or private function call.
static const wchar_t*preflight(){
    if(!readable(module,sizeof(IMAGE_DOS_HEADER)))return L"module-header-unreadable";
    const auto*dos=(const IMAGE_DOS_HEADER*)module;
    if(dos->e_magic!=IMAGE_DOS_SIGNATURE||dos->e_lfanew<0||dos->e_lfanew>1024*1024)return L"module-dos-header";
    const auto*nt=(const IMAGE_NT_HEADERS64*)(module+dos->e_lfanew);
    if(!readable(nt,sizeof(*nt))||nt->Signature!=IMAGE_NT_SIGNATURE||nt->FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64||nt->OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC)return L"module-machine";
    const unsigned imageSize=nt->OptionalHeader.SizeOfImage;
    const auto*sections=IMAGE_FIRST_SECTION(nt);
    const unsigned count=nt->FileHeader.NumberOfSections;
    if(!count||count>96||!readable(sections,count*sizeof(*sections)))return L"module-sections";
    auto inModule=[&](const void*p,size_t bytes){auto address=(uintptr_t)p,base=(uintptr_t)module;return address>=base&&bytes<=imageSize&&address-base<=imageSize-bytes&&readable(p,bytes);};
    auto executable=[&](const void*p){
        if(!inModule(p,1))return false;auto rva=(uintptr_t)p-(uintptr_t)module;
        for(unsigned i=0;i<count;i++)if((sections[i].Characteristics&IMAGE_SCN_MEM_EXECUTE)&&rva>=sections[i].VirtualAddress&&rva-sections[i].VirtualAddress<sections[i].Misc.VirtualSize)return true;
        return false;
    };
    auto locator=[&](unsigned table,unsigned displacement)->const unsigned*{
        if(table<8||!inModule(module+table-8,16))return nullptr;
        auto*col=get<const unsigned*>(module+table-8);
        if(!inModule(col,24)||col[0]!=1||col[1]!=displacement||col[5]!=(uintptr_t)col-(uintptr_t)module||col[3]>=imageSize)return nullptr;
        return col;
    };
    for(unsigned rva:adapter::FunctionRvas)if(!executable(module+rva))return L"function-outside-executable-section";
    for(const auto&t:objectTypes)if(!locator(t.primary,0)||!executable(get<void*>(module+t.primary)))return L"object-vtable-layout";
    for(const auto&g:adapter::MetaGuards){
        if(get<void*>(module+g.table)!=module+g.getter||!executable(module+g.getter)||!inModule(module+g.meta,16))return L"key-object-meta-layout";
        auto*strings=get<unsigned char*>(module+g.meta+8);
        if(!inModule(strings,24))return L"key-object-meta-strings";
        int length=get<int>(strings+4);intptr_t delta=get<intptr_t>(strings+16);
        if(length<1||length>150||delta<24||delta>imageSize)return L"key-object-meta-name";
        auto*name=(const char*)((uintptr_t)strings+(uintptr_t)delta);
        if(!inModule(name,length)||strlen(g.name)!=(size_t)length||memcmp(name,g.name,length))return L"key-object-meta-name";
    }
    for(const auto&t:paintTargets){
        const auto*primary=locator(t.primary,0);const auto*paint=locator(t.table,adapter::PaintInterfaceOffset);
        if(!primary||!paint||primary[3]!=paint[3])return L"paint-interface-layout";
        if(!inModule(module+t.table+8,8)||get<void*>(module+t.table+8)!=module+t.callback||!executable(module+t.callback))return L"paint-slot-mismatch";
    }
    const auto*row=locator(adapter::RowSlot-8,adapter::RowInterfaceOffset);
    const auto*primary=locator(adapter::SessionCell,0);
    if(!row||!primary||row[3]!=primary[3]||!inModule(module+adapter::RowSlot,8)||get<void*>(module+adapter::RowSlot)!=module+adapter::RowPaint)return L"row-slot-mismatch";
    return nullptr;
}
extern "C" __declspec(dllexport) void semantic_start(const wchar_t*folder){
    if(installed)return;
    if(guiThread&&guiThread!=GetCurrentThreadId())return;
    module=(unsigned char*)GetModuleHandleW(L"Weixin.dll");if(!module)return;
    if(wcslen(folder)>MAX_PATH-30)return;
    guiThread=GetCurrentThreadId();swprintf(configPath,MAX_PATH,L"%ls\\theme.ini",folder);swprintf(reportPath,MAX_PATH,L"%ls\\semantic.log",folder);swprintf(receiptPath,MAX_PATH,L"%ls\\theme.receipt",folder);
    GetPrivateProfileStringW(L"theme",L"revision",L"",revision,80,configPath);
    if(const wchar_t*failure=preflight()){receipt(L"incompatible",failure);log("preflight-incompatible");return;}
    auto rowSlot=(void**)(module+adapter::RowSlot);originalRow=(Paint)(module+adapter::RowPaint);
    // Fully construct the immutable lookup before another thread can enter a hook.
    if(patched.empty()){patched.reserve(sizeof(paintTargets)/sizeof(paintTargets[0]));for(const auto&t:paintTargets)patched.push_back({(void**)(module+t.table+8),module+t.callback,&t});}
    for(auto&r:patched){
        if(!setSlot(r.slot,r.original,(void*)dispatch)){log(restoreSlots()?"install-rolled-back":"rollback-incomplete");receipt(L"incompatible",L"hook-install-failed");return;}}
    if(!setSlot(rowSlot,(void*)originalRow,(void*)rowDispatch)){log(restoreSlots()?"row-install-rolled-back":"rollback-incomplete");receipt(L"incompatible",L"row-install-failed");return;}
    configTimer=SetTimer(nullptr,0,250,tick);if(!configTimer){log(restoreSlots()?"timer-install-rolled-back":"rollback-incomplete");receipt(L"incompatible",L"timer-install-failed");return;}
    forceReload=true;
    installed=true;
    receipt(L"ready");
    log("native-paint-ready",(int)patched.size());
    log("config-timer",1);
}
// Called only on the GUI thread. The module remains loaded until process exit.
extern "C" __declspec(dllexport) int semantic_stop(){
    if(GetCurrentThreadId()!=guiThread||inside)return 0;
    enabled=false;if(configTimer){KillTimer(nullptr,configTimer);configTimer=0;}
    bool restored=restoreSlots();
    if(rootObject&&get<void*>((unsigned char*)rootObject+8)==rootIdentity&&!strcmp(typeOf(rootObject),"mmui::MainView"))((void(*)(void*))(module+adapter::WidgetUpdate))(rootObject);
    releaseImage(imageAsset);imageAsset=nullptr;imagePath.clear();forceReload=true;installed=!restored;
    log(restored?"native-paint-stopped":"native-paint-disabled-restore-incomplete");return restored?1:0;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(h);return TRUE;}
