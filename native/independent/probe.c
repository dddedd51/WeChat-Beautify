/* Independently implemented Windows GDI route probe.
 * It observes API call metadata only. No pixels, titles or message text are saved.
 * No rendering is changed. Hooking occurs at qt_testability_init, never DllMain.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
#include <stdint.h>
#include <wchar.h>
#include <string.h>

typedef BOOL (WINAPI *BltFn)(HDC,int,int,int,int,HDC,int,int,DWORD);
typedef HBITMAP (WINAPI *DibFn)(HDC,const BITMAPINFO*,UINT,VOID**,HANDLE,DWORD);
typedef BOOL (WINAPI *LayerFn)(HWND,HDC,POINT*,SIZE*,HDC,POINT*,COLORREF,BLENDFUNCTION*,DWORD);
typedef BOOL (WINAPI *LayerIndirectFn)(HWND,const UPDATELAYEREDWINDOWINFO*);
static BltFn systemBlt;
static DibFn systemDib;
static LayerFn systemLayer;
static LayerIndirectFn systemLayerIndirect;
static wchar_t report[MAX_PATH];
static SRWLOCK reportLock=SRWLOCK_INIT;
static LONG bltCount,dibCount,layerCount;
static unsigned char *module;
static DWORD moduleBytes;
static HMODULE self;
typedef int (__stdcall *SwapFn)(void*,void*);
typedef int (__stdcall *QuerySurfaceFn)(void*,void*,int,int*);
typedef void* (__stdcall *EglProcFn)(const char*);
typedef FARPROC (WINAPI *GetProcFn)(HMODULE,LPCSTR);
static SwapFn systemSwap;
static QuerySurfaceFn querySurface;
static EglProcFn systemEglProc;
static GetProcFn systemGetProc;
static LONG swapCount;
static void line(const char *kind,HWND window,int w,int h,int bpp,int dib){
    FILE *f;wchar_t cls[100]=L"";
    if(window)GetClassNameW(window,cls,100);
    AcquireSRWLockExclusive(&reportLock);
    f=_wfopen(report,L"a");
    if(f){fprintf(f,"%s tid=%lu hwnd=%llu class=%ls size=%dx%d bpp=%d dib=%d\n",kind,GetCurrentThreadId(),(unsigned long long)(uintptr_t)window,cls,w,h,bpp,dib);fclose(f);}
    ReleaseSRWLockExclusive(&reportLock);
}
static int __stdcall observeSwap(void *display,void *surface){
    LONG count=InterlockedIncrement(&swapCount);int w=0,h=0;
    if(count<=5||count%120==0){if(querySurface){querySurface(display,surface,0x3057,&w);querySurface(display,surface,0x3056,&h);}line("eglSwapBuffers",NULL,w,h,0,(int)count);}
    return systemSwap(display,surface);
}
static void* __stdcall observeEglProc(const char *name){
    if(!systemEglProc){HMODULE egl=GetModuleHandleW(L"libEGL.dll");if(egl)systemEglProc=(EglProcFn)systemGetProc(egl,"eglGetProcAddress");}
    if(!systemEglProc)return NULL;
    void *result=systemEglProc(name);
    if(!strcmp(name,"eglSwapBuffers")){systemSwap=(SwapFn)result;line("resolved-eglSwapBuffers",NULL,0,0,0,0);return (void*)observeSwap;}
    return result;
}
static FARPROC WINAPI observeGetProc(HMODULE lib,LPCSTR name){
    FARPROC result=systemGetProc(lib,name);
    if((uintptr_t)name>65535&&!strcmp(name,"eglSwapBuffers")){
        systemSwap=(SwapFn)result;querySurface=(QuerySurfaceFn)systemGetProc(lib,"eglQuerySurface");line("resolved-swap",NULL,0,0,0,0);return (FARPROC)observeSwap;
    }
    return result;
}
static DWORD WINAPI discoverSwap(LPVOID ignored){
    (void)ignored;
    wchar_t path[MAX_PATH];GetModuleFileNameW(self,path,MAX_PATH);wchar_t*slash=wcsrchr(path,L'\\');
    if(slash){wcscpy(slash+1,L"dxgi-probe.dll");HMODULE addon=LoadLibraryW(path);if(addon){void(*start)(const wchar_t*)=(void(*)(const wchar_t*))GetProcAddress(addon,"probe_start");*slash=0;if(start)start(path);}}
    for(int attempt=0;attempt<16;attempt++){
        Sleep(1500);HMODULE egl=GetModuleHandleW(L"libEGL.dll");
        if(!egl)continue;
        if(!systemSwap)systemSwap=(SwapFn)systemGetProc(egl,"eglSwapBuffers");
        if(!querySurface)querySurface=(QuerySurfaceFn)systemGetProc(egl,"eglQuerySurface");
        int count=0;
        for(SIZE_T offset=0;offset<moduleBytes;){
            MEMORY_BASIC_INFORMATION mbi;
            if(!VirtualQuery(module+offset,&mbi,sizeof(mbi)))break;
            SIZE_T end=(unsigned char*)mbi.BaseAddress+mbi.RegionSize-module;
            if(end>moduleBytes)end=moduleBytes;
            if(mbi.State==MEM_COMMIT&&!(mbi.Protect&(PAGE_GUARD|PAGE_NOACCESS))&&(mbi.Protect&(PAGE_READWRITE|PAGE_WRITECOPY|PAGE_READONLY))){
                for(SIZE_T at=(offset+7)&~(SIZE_T)7;at+8<=end;at+=8){
                    void *volatile *slot=(void*volatile*)(module+at);
                    if(*slot==(void*)systemSwap&&systemSwap){DWORD old;if(VirtualProtect((void*)slot,8,PAGE_READWRITE,&old)){InterlockedCompareExchangePointer(slot,(void*)observeSwap,(void*)systemSwap);VirtualProtect((void*)slot,8,old,&old);count++;}}
                }
            }
            if(end<=offset)break;offset=end;
        }
        if(count)line("swap-pointers-attached",NULL,count,0,0,0);
    }
    return 0;
}
static BOOL WINAPI observeBlt(HDC dst,int x,int y,int w,int h,HDC src,int sx,int sy,DWORD rop){
    LONG count=InterlockedIncrement(&bltCount);
    if(count<=8||(w>=700&&h>=400&&count%60==0)){
        DIBSECTION info={0};HGDIOBJ bitmap=GetCurrentObject(src,OBJ_BITMAP);
        int size=bitmap?GetObjectW(bitmap,sizeof(info),&info):0;
        line("BitBlt",WindowFromDC(dst),w,h,info.dsBm.bmBitsPixel,size==sizeof(info));
    }
    return systemBlt(dst,x,y,w,h,src,sx,sy,rop);
}
static HBITMAP WINAPI observeDib(HDC dc,const BITMAPINFO *info,UINT usage,VOID **bits,HANDLE section,DWORD offset){
    HBITMAP bitmap=systemDib(dc,info,usage,bits,section,offset);
    if(InterlockedIncrement(&dibCount)<=30&&info)line("CreateDIBSection",NULL,info->bmiHeader.biWidth,info->bmiHeader.biHeight,info->bmiHeader.biBitCount,bitmap!=NULL);
    return bitmap;
}
static BOOL WINAPI observeLayer(HWND window,HDC dst,POINT *at,SIZE *size,HDC src,POINT *from,COLORREF key,BLENDFUNCTION *blend,DWORD flags){
    if(InterlockedIncrement(&layerCount)<=30)line("UpdateLayeredWindow",window,size?size->cx:0,size?size->cy:0,0,0);
    return systemLayer(window,dst,at,size,src,from,key,blend,flags);
}
static BOOL WINAPI observeLayerIndirect(HWND window,const UPDATELAYEREDWINDOWINFO *info){
    if(InterlockedIncrement(&layerCount)<=30)line("UpdateLayeredWindowIndirect",window,info&&info->psize?info->psize->cx:0,info&&info->psize?info->psize->cy:0,0,0);
    return systemLayerIndirect(window,info);
}
static int verified(void){
    static const unsigned char expected[32]={0xe0,0x42,0x8a,0x7b,0x69,0x18,0x53,0xde,0x49,0x65,0x32,0x52,0x3c,0x2c,0x09,0xaa,0x09,0x5f,0x90,0x94,0x1c,0x37,0xc9,0xf4,0xcf,0xb3,0xf1,0x37,0xf3,0xc6,0x65,0x56};
    wchar_t path[32768];unsigned char bytes[65536],digest[32];DWORD read;int equal=0;
    BCRYPT_ALG_HANDLE algorithm=NULL;BCRYPT_HASH_HANDLE hash=NULL;HANDLE file=INVALID_HANDLE_VALUE;
    if(!GetModuleFileNameW((HMODULE)module,path,32768))goto cleanup;
    file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,FILE_FLAG_SEQUENTIAL_SCAN,NULL);
    if(file==INVALID_HANDLE_VALUE)goto cleanup;
    if(BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,NULL,0)<0)goto cleanup;
    if(BCryptCreateHash(algorithm,&hash,NULL,0,NULL,0,0)<0)goto cleanup;
    do{if(!ReadFile(file,bytes,sizeof(bytes),&read,NULL))goto cleanup;if(read&&BCryptHashData(hash,bytes,read,0)<0)goto cleanup;}while(read);
    if(BCryptFinishHash(hash,digest,32,0)<0)goto cleanup;
    equal=memcmp(digest,expected,32)==0;
cleanup:
    if(file!=INVALID_HANDLE_VALUE)CloseHandle(file);
    if(hash)BCryptDestroyHash(hash);
    if(algorithm)BCryptCloseAlgorithmProvider(algorithm,0);
    return equal;
}
static void *inside(DWORD rva,SIZE_T bytes){return rva<moduleBytes&&bytes<=moduleBytes-rva?module+rva:NULL;}
static int hookThunk(DWORD namesRva,DWORD slotsRva){
    int count=0;DWORD offset=0;
    for(;;offset+=8){
        ULONGLONG *name=inside(namesRva+offset,8),*slot=inside(slotsRva+offset,8);void *target=NULL;DWORD old;
        if(!name||!slot||!*name)break;
        if(*name&IMAGE_ORDINAL_FLAG64)continue;
        IMAGE_IMPORT_BY_NAME *symbol=inside((DWORD)*name,128);
        if(!symbol)continue;
        if(!strcmp(symbol->Name,"BitBlt"))target=observeBlt;
        if(!strcmp(symbol->Name,"CreateDIBSection"))target=observeDib;
        if(!strcmp(symbol->Name,"UpdateLayeredWindow"))target=observeLayer;
        if(!strcmp(symbol->Name,"UpdateLayeredWindowIndirect"))target=observeLayerIndirect;
        if(!strcmp(symbol->Name,"eglGetProcAddress"))target=observeEglProc;
        if(!strcmp(symbol->Name,"GetProcAddress"))target=observeGetProc;
        if(target&&VirtualProtect(slot,8,PAGE_READWRITE,&old)){
            InterlockedExchangePointer((void *volatile*)slot,target);VirtualProtect(slot,8,old,&old);count++;
        }
    }
    return count;
}
typedef struct{DWORD attributes,name,handle,iat,intable,bound,unload,time;} DelayDescriptor;
__declspec(dllexport) void qt_testability_init(void){
    int hooked=0;
    module=(unsigned char*)GetModuleHandleW(L"Weixin.dll");
    if(!module)return;
    GetModuleFileNameW(self,report,MAX_PATH);
    wchar_t *slash=wcsrchr(report,L'\\');if(!slash)return;
    wcscpy(slash+1,L"independent-probe.log");
    line("entry",NULL,0,0,0,0);
    if(!verified()){line("rejected-hash",NULL,0,0,0,0);return;}
    IMAGE_DOS_HEADER *dos=(IMAGE_DOS_HEADER*)module;
    IMAGE_NT_HEADERS64 *nt=(IMAGE_NT_HEADERS64*)(module+dos->e_lfanew);
    moduleBytes=nt->OptionalHeader.SizeOfImage;
    HMODULE gdi=LoadLibraryW(L"gdi32.dll"),user=LoadLibraryW(L"user32.dll");
    systemGetProc=GetProcAddress;
    HMODULE egl=GetModuleHandleW(L"libEGL.dll");
    if(egl){systemEglProc=(EglProcFn)GetProcAddress(egl,"eglGetProcAddress");systemSwap=(SwapFn)GetProcAddress(egl,"eglSwapBuffers");querySurface=(QuerySurfaceFn)GetProcAddress(egl,"eglQuerySurface");}
    systemBlt=(BltFn)GetProcAddress(gdi,"BitBlt");systemDib=(DibFn)GetProcAddress(gdi,"CreateDIBSection");
    systemLayer=(LayerFn)GetProcAddress(user,"UpdateLayeredWindow");systemLayerIndirect=(LayerIndirectFn)GetProcAddress(user,"UpdateLayeredWindowIndirect");
    if(!systemBlt||!systemDib||!systemLayer||!systemLayerIndirect)return;
    IMAGE_DATA_DIRECTORY dir=nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT];
    for(DWORD offset=0;offset+sizeof(DelayDescriptor)<=dir.Size;offset+=sizeof(DelayDescriptor)){
        DelayDescriptor *d=inside(dir.VirtualAddress+offset,sizeof(*d));
        if(!d||!d->name)break;if(d->attributes&1)hooked+=hookThunk(d->intable,d->iat);
    }
    dir=nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    for(DWORD offset=0;offset+sizeof(IMAGE_IMPORT_DESCRIPTOR)<=dir.Size;offset+=sizeof(IMAGE_IMPORT_DESCRIPTOR)){
        IMAGE_IMPORT_DESCRIPTOR *d=inside(dir.VirtualAddress+offset,sizeof(*d));
        if(!d||!d->Name)break;if(d->OriginalFirstThunk)hooked+=hookThunk(d->OriginalFirstThunk,d->FirstThunk);
    }
    line("installed-metadata-only",NULL,hooked,0,0,0);
    HANDLE thread=CreateThread(NULL,0,discoverSwap,NULL,0,NULL);if(thread)CloseHandle(thread);
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH){self=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
