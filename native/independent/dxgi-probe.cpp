// Independent COM/vtable observation. Saves dimensions and counters, never pixels.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <stdio.h>
#include <stdint.h>
#include <wincodec.h>
#include <dcomp.h>
#include <wrl/client.h>
#include <mutex>
#include <memory>
#include "compositor.h"
using Microsoft::WRL::ComPtr;
static wchar_t logPath[MAX_PATH];
static SRWLOCK lock=SRWLOCK_INIT;
static wchar_t settingsPath[MAX_PATH];
static beautify::Theme theme;
static DWORD lastConfig;
static FILETIME lastConfigWrite={};
static void log(const char *event,int a=0,int b=0){AcquireSRWLockExclusive(&lock);FILE*f=_wfopen(logPath,L"a");if(f){fprintf(f,"%s %d %d tid=%lu\n",event,a,b,GetCurrentThreadId());fclose(f);}ReleaseSRWLockExclusive(&lock);}
static beautify::Color parseColor(const wchar_t*key,beautify::Color fallback){wchar_t s[24];GetPrivateProfileStringW(L"theme",key,L"",s,24,settingsPath);unsigned rgb;if(swscanf(s,L"#%x",&rgb)!=1)return fallback;return {(unsigned char)rgb,(unsigned char)(rgb>>8),(unsigned char)(rgb>>16),255};}
static void readTheme(){
    DWORD now=GetTickCount();if(now-lastConfig<700)return;lastConfig=now;
    WIN32_FILE_ATTRIBUTE_DATA attr;if(!GetFileAttributesExW(settingsPath,GetFileExInfoStandard,&attr))return;
    if(CompareFileTime(&attr.ftLastWriteTime,&lastConfigWrite)==0)return;lastConfigWrite=attr.ftLastWriteTime;
    theme.enabled=GetPrivateProfileIntW(L"theme",L"enabled",0,settingsPath)==1;
    const wchar_t*names[]={L"navigation",L"list",L"canvas",L"input"};
    for(int i=0;i<4;i++){theme.regions[i]=parseColor(names[i],theme.regions[i]);wchar_t key[50];swprintf(key,50,L"%lsOpacity",names[i]);theme.opacity[i]=std::clamp((int)GetPrivateProfileIntW(L"theme",key,0,settingsPath),0,100);}
    theme.text=parseColor(L"text",theme.text);
    wchar_t image[MAX_PATH];GetPrivateProfileStringW(L"theme",L"wallpaper",L"",image,MAX_PATH,settingsPath);
    theme.wallpaper.clear();theme.wallW=theme.wallH=0;
    IWICImagingFactory*factory=NULL;IWICBitmapDecoder*decoder=NULL;IWICBitmapFrameDecode*frame=NULL;IWICFormatConverter*converter=NULL;
    HRESULT init=CoInitializeEx(NULL,COINIT_MULTITHREADED);
    HRESULT hr=CoCreateInstance(CLSID_WICImagingFactory,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&factory));
    UINT w=0,h=0;
    if(SUCCEEDED(hr))hr=factory->CreateDecoderFromFilename(image,NULL,GENERIC_READ,WICDecodeMetadataCacheOnLoad,&decoder);
    if(SUCCEEDED(hr))hr=decoder->GetFrame(0,&frame);
    if(SUCCEEDED(hr))hr=frame->GetSize(&w,&h);
    if(SUCCEEDED(hr)&&(w==0||h==0||w>8192||h>8192))hr=E_INVALIDARG;
    if(SUCCEEDED(hr))hr=factory->CreateFormatConverter(&converter);
    if(SUCCEEDED(hr))hr=converter->Initialize(frame,GUID_WICPixelFormat32bppBGRA,WICBitmapDitherTypeNone,NULL,0,WICBitmapPaletteTypeCustom);
    if(SUCCEEDED(hr)){theme.wallpaper.resize((size_t)w*h);hr=converter->CopyPixels(NULL,w*4,w*h*4,(BYTE*)theme.wallpaper.data());if(SUCCEEDED(hr)){theme.wallW=w;theme.wallH=h;}else theme.wallpaper.clear();}
    if(converter)converter->Release();if(frame)frame->Release();if(decoder)decoder->Release();if(factory)factory->Release();if(SUCCEEDED(init))CoUninitialize();
    log("theme-enabled-image-result",theme.enabled,hr);log("wallpaper-size",theme.wallW,theme.wallH);
}
// COM slots below are the documented interfaces in the Windows SDK dcomp.h/dxgi.h.
struct Slot{void**place;void*before;};static Slot slots[96];static int slotCount;
static SRWLOCK slotsLock=SRWLOCK_INIT;
static void *original(void *object,int index){void**p=*(void***)object;void*answer=nullptr;AcquireSRWLockShared(&slotsLock);for(int i=0;i<slotCount;i++)if(slots[i].place==p+index){answer=slots[i].before;break;}ReleaseSRWLockShared(&slotsLock);return answer;}
static void hook(void *object,int index,void *callback){
    void**p=*(void***)object+index;AcquireSRWLockExclusive(&slotsLock);
    for(int i=0;i<slotCount;i++)if(slots[i].place==p){ReleaseSRWLockExclusive(&slotsLock);return;}
    DWORD old;if(slotCount<96&&VirtualProtect(p,sizeof(void*),PAGE_READWRITE,&old)){
        slots[slotCount++]={p,*p};InterlockedExchangePointer(p,callback);VirtualProtect(p,sizeof(void*),old,&old);
    }
    ReleaseSRWLockExclusive(&slotsLock);
}
using PresentFn=HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*,UINT,UINT);
using Present1Fn=HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain1*,UINT,UINT,const DXGI_PRESENT_PARAMETERS*);
using CompositionFn=HRESULT(STDMETHODCALLTYPE*)(IDXGIFactory2*,IUnknown*,const DXGI_SWAP_CHAIN_DESC1*,IDXGIOutput*,IDXGISwapChain1**);
using ContentFn=HRESULT(STDMETHODCALLTYPE*)(IDCompositionVisual*,IUnknown*);
using CommitFn=HRESULT(STDMETHODCALLTYPE*)(IUnknown*);
using ReleaseFn=ULONG(STDMETHODCALLTYPE*)(IUnknown*);
using VisualFn=HRESULT(STDMETHODCALLTYPE*)(IUnknown*,IDCompositionVisual**);
static std::recursive_mutex renderMutex;
static thread_local bool internalCall=false;
static void(*testObserver)(IDXGISwapChain*)=nullptr;
struct Inside {bool previous;Inside():previous(internalCall){internalCall=true;}~Inside(){internalCall=previous;}};
struct Visual {
    IDCompositionVisual*visual=nullptr; // Weak; the Release hook removes this record at zero.
    ComPtr<IUnknown>owner;
    CommitFn commit=nullptr;
    ComPtr<IDXGISwapChain1>source,output;
    ComPtr<ID3D11Device>device;
    ComPtr<ID3D11DeviceContext>context;
    ComPtr<ID3D11Texture2D>stage;
    UINT width=0,height=0;DXGI_FORMAT format=DXGI_FORMAT_UNKNOWN;
    std::vector<beautify::Color>normalized,pixels;
    bool redirected=false;unsigned frames=0;
    void clearResources(){output.Reset();stage.Reset();context.Reset();device.Reset();width=height=0;format=DXGI_FORMAT_UNKNOWN;normalized.clear();pixels.clear();}
};
static std::vector<std::unique_ptr<Visual>> visuals;
static Visual*findVisual(IDCompositionVisual*v){for(auto&x:visuals)if(x->visual==v)return x.get();return nullptr;}
static HRESULT bind(Visual&v,bool custom){
    auto fn=(ContentFn)original(v.visual,15);if(!fn)return E_FAIL;
    HRESULT hr=fn(v.visual,custom?(IUnknown*)v.output.Get():(IUnknown*)v.source.Get());
    if(SUCCEEDED(hr)){v.redirected=custom;hr=v.commit(v.owner.Get());}
    log(custom?"bind-output":"restore-source",hr);return hr;
}
static HRESULT STDMETHODCALLTYPE setContent(IDCompositionVisual*v,IUnknown*c){
    auto fn=(ContentFn)original(v,15);if(!fn)return E_FAIL;
    if(internalCall)return fn(v,c);
    std::lock_guard<std::recursive_mutex> guard(renderMutex);Inside inside;
    HRESULT hr=fn(v,c);if(FAILED(hr))return hr;
    if(auto*r=findVisual(v)){
        r->redirected=false;r->clearResources();r->source.Reset();
        if(c)c->QueryInterface(IID_PPV_ARGS(&r->source));
        log("visual-content-swapchain",r->source?1:0);
    }else log("visual-not-tracked");
    return hr;
}
static ULONG STDMETHODCALLTYPE releaseVisual(IUnknown*v){
    auto fn=(ReleaseFn)original(v,2);if(!fn)return 1;
    std::lock_guard<std::recursive_mutex> guard(renderMutex);
    ULONG refs=fn(v);
    if(!refs){for(auto it=visuals.begin();it!=visuals.end();++it)if((*it)->visual==(IDCompositionVisual*)v){visuals.erase(it);break;}}
    return refs;
}
static void observeVisual(IUnknown*device,IDCompositionVisual*v){
    std::lock_guard<std::recursive_mutex> guard(renderMutex);
    hook(v,15,(void*)setContent);hook(v,2,(void*)releaseVisual);
    if(!findVisual(v)&&visuals.size()<128){auto r=std::make_unique<Visual>();r->visual=v;r->owner=device;r->commit=(CommitFn)(*(void***)device)[3];visuals.push_back(std::move(r));}
    log("visual-observed",(int)visuals.size());
}
static HRESULT STDMETHODCALLTYPE createVisual1(IUnknown*d,IDCompositionVisual**v){auto f=(VisualFn)original(d,7);HRESULT hr=f?f(d,v):E_FAIL;if(SUCCEEDED(hr)&&!internalCall)observeVisual(d,*v);return hr;}
static HRESULT STDMETHODCALLTYPE createVisual2(IUnknown*d,IDCompositionVisual**v){auto f=(VisualFn)original(d,6);HRESULT hr=f?f(d,v):E_FAIL;if(SUCCEEDED(hr)&&!internalCall)observeVisual(d,*v);return hr;}
static bool rgba(DXGI_FORMAT f){return f==DXGI_FORMAT_R8G8B8A8_UNORM||f==DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;}
static bool supported(DXGI_FORMAT f){return rgba(f)||f==DXGI_FORMAT_B8G8R8A8_UNORM||f==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;}
static HRESULT prepare(Visual&v,ID3D11Texture2D*back,const D3D11_TEXTURE2D_DESC&d){
    if(v.width==d.Width&&v.height==d.Height&&v.format==d.Format&&v.output)return S_OK;
    if(v.redirected)bind(v,false);v.clearResources();
    back->GetDevice(&v.device);v.device->GetImmediateContext(&v.context);
    D3D11_TEXTURE2D_DESC staging=d;staging.Usage=D3D11_USAGE_STAGING;staging.BindFlags=0;staging.CPUAccessFlags=D3D11_CPU_ACCESS_READ;staging.MiscFlags=0;
    HRESULT hr=v.device->CreateTexture2D(&staging,nullptr,&v.stage);if(FAILED(hr))return hr;
    ComPtr<IDXGIDevice>dx;ComPtr<IDXGIAdapter>adapter;ComPtr<IDXGIFactory2>factory;
    hr=v.device.As(&dx);if(SUCCEEDED(hr))hr=dx->GetAdapter(&adapter);if(SUCCEEDED(hr))hr=adapter->GetParent(IID_PPV_ARGS(&factory));
    DXGI_SWAP_CHAIN_DESC1 out={};out.Width=d.Width;out.Height=d.Height;out.Format=DXGI_FORMAT_B8G8R8A8_UNORM;out.SampleDesc.Count=1;out.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;out.BufferCount=2;out.Scaling=DXGI_SCALING_STRETCH;out.SwapEffect=DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;out.AlphaMode=DXGI_ALPHA_MODE_PREMULTIPLIED;
    if(SUCCEEDED(hr))hr=factory->CreateSwapChainForComposition(v.device.Get(),&out,nullptr,&v.output);
    if(FAILED(hr)){v.clearResources();return hr;}
    v.width=d.Width;v.height=d.Height;v.format=d.Format;v.normalized.resize((size_t)d.Width*d.Height);v.pixels.resize(v.normalized.size());
    log("output-created",d.Width,d.Height);log("source-format",d.Format,d.SampleDesc.Count);return S_OK;
}
static void paint(IDXGISwapChain*chain){
    readTheme();ComPtr<IDXGISwapChain1>one;if(FAILED(chain->QueryInterface(IID_PPV_ARGS(&one))))return;
    Visual*v=nullptr;for(auto&r:visuals)if(r->source.Get()==one.Get()){v=r.get();break;}
    if(!v){static int missed=0;if(++missed<8)log("present-without-visual");return;}
    if(!theme.enabled||theme.wallpaper.empty()){if(v->redirected)bind(*v,false);return;}
    ComPtr<ID3D11Texture2D>back;HRESULT hr=chain->GetBuffer(0,IID_PPV_ARGS(&back));if(FAILED(hr)){log("buffer-failed",hr);return;}
    D3D11_TEXTURE2D_DESC d;back->GetDesc(&d);bool report=++v->frames<5||v->frames%120==0;
    if(report){log("source-size",d.Width,d.Height);log("source-format",d.Format,d.SampleDesc.Count);}
    if(d.Width<700||d.Height<450||d.Width>4096||d.Height>4096||!supported(d.Format)||d.SampleDesc.Count!=1){if(v->redirected)bind(*v,false);return;}
    hr=prepare(*v,back.Get(),d);if(FAILED(hr)){if(report)log("prepare-failed",hr);return;}
    v->context->CopyResource(v->stage.Get(),back.Get());D3D11_MAPPED_SUBRESOURCE mapped={};hr=v->context->Map(v->stage.Get(),0,D3D11_MAP_READ,0,&mapped);
    if(FAILED(hr)){if(report)log("map-failed",hr);if(v->redirected)bind(*v,false);return;}
    beautify::convertPixels((unsigned char*)mapped.pData,(int)mapped.RowPitch,v->normalized.data(),d.Width,d.Height,rgba(d.Format));v->context->Unmap(v->stage.Get(),0);
    auto m=beautify::compose((unsigned char*)v->normalized.data(),d.Width*4,v->pixels.data(),d.Width,d.Height,theme);
    if(report){log("regions",m.nav,m.list);log("classified",m.accepted,(int)m.changed);}
    if(!m.accepted||!m.changed){if(v->redirected)bind(*v,false);return;}
    ComPtr<ID3D11Texture2D>out;hr=v->output->GetBuffer(0,IID_PPV_ARGS(&out));
    if(SUCCEEDED(hr)){v->context->UpdateSubresource(out.Get(),0,nullptr,v->pixels.data(),d.Width*4,0);DXGI_PRESENT_PARAMETERS full={};auto f=(Present1Fn)original(v->output.Get(),22);hr=f?f(v->output.Get(),0,0,&full):v->output->Present1(0,0,&full);}
    if(FAILED(hr)){if(report)log("output-present-failed",hr);if(v->redirected)bind(*v,false);return;}
    if(!v->redirected)bind(*v,true);
    if(report)log("output-presented",(int)m.changed);
}
static void safePaint(IDXGISwapChain*o){try{paint(o);}catch(...){theme.enabled=false;log("render-exception-disabled");for(auto&r:visuals)if(r->redirected)bind(*r,false);}}
static HRESULT STDMETHODCALLTYPE present(IDXGISwapChain*o,UINT interval,UINT flags){
    auto f=(PresentFn)original(o,8);if(!f)return E_FAIL;if(internalCall||flags&DXGI_PRESENT_TEST)return f(o,interval,flags);
    std::lock_guard<std::recursive_mutex> guard(renderMutex);Inside inside;safePaint(o);if(testObserver)testObserver(o);return f(o,interval,flags);
}
static HRESULT STDMETHODCALLTYPE present1(IDXGISwapChain1*o,UINT interval,UINT flags,const DXGI_PRESENT_PARAMETERS*p){
    auto f=(Present1Fn)original(o,22);if(!f)return E_FAIL;if(internalCall||flags&DXGI_PRESENT_TEST)return f(o,interval,flags,p);
    std::lock_guard<std::recursive_mutex> guard(renderMutex);Inside inside;safePaint(o);if(testObserver)testObserver(o);return f(o,interval,flags,p);
}
static HRESULT STDMETHODCALLTYPE createComposition(IDXGIFactory2*f,IUnknown*d,const DXGI_SWAP_CHAIN_DESC1*desc,IDXGIOutput*out,IDXGISwapChain1**chain){
    auto fn=(CompositionFn)original(f,24);HRESULT h=fn?fn(f,d,desc,out,chain):E_FAIL;
    if(SUCCEEDED(h)&&!internalCall){hook(*chain,8,(void*)present);hook(*chain,22,(void*)present1);log("source-created",desc->Width,desc->Height);}return h;
}
extern "C" __declspec(dllexport) void probe_start(const wchar_t*folder){
    swprintf(logPath,MAX_PATH,L"%ls\\dxgi-probe.log",folder);swprintf(settingsPath,MAX_PATH,L"%ls\\theme.ini",folder);
    Inside inside;ComPtr<IDXGIFactory2>f;HRESULT hr=CreateDXGIFactory1(IID_PPV_ARGS(&f));if(FAILED(hr)){log("factory-failed",hr);return;}hook(f.Get(),24,(void*)createComposition);
    ComPtr<IDCompositionDevice>d1;hr=DCompositionCreateDevice(nullptr,IID_PPV_ARGS(&d1));if(SUCCEEDED(hr))hook(d1.Get(),7,(void*)createVisual1);log("dcomp1-hook",hr);
    ComPtr<IDCompositionDevice2>d2;hr=DCompositionCreateDevice2(nullptr,IID_PPV_ARGS(&d2));if(SUCCEEDED(hr))hook(d2.Get(),6,(void*)createVisual2);log("dcomp2-hook",hr);
    ComPtr<IDCompositionDevice3>d3;hr=DCompositionCreateDevice3(nullptr,IID_PPV_ARGS(&d3));if(SUCCEEDED(hr))hook(d3.Get(),6,(void*)createVisual2);log("dcomp3-hook",hr);log("hooks-ready",slotCount);
}
// Used only by the standalone synthetic harness; no host UI pixels are exported.
extern "C" __declspec(dllexport) int probe_test_state(IDXGISwapChain1*s,UINT*width,UINT*height){std::lock_guard<std::recursive_mutex>guard(renderMutex);for(auto&r:visuals)if(r->source.Get()==s){if(width)*width=r->width;if(height)*height=r->height;return r->redirected?1:0;}return -1;}
extern "C" __declspec(dllexport) void probe_test_observer(void(*observer)(IDXGISwapChain*)){testObserver=observer;}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(h);return TRUE;}
