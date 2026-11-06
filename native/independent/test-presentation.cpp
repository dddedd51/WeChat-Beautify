#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <dcomp.h>
#include <wrl/client.h>
#include <vector>
#include <cassert>
#include <cstdio>
#include "compositor.h"
using Microsoft::WRL::ComPtr;
static void check(HRESULT hr){if(FAILED(hr)){printf("HRESULT=%08lx\n",hr);ExitProcess(2);}}
static LRESULT CALLBACK wnd(HWND w,UINT m,WPARAM a,LPARAM b){return DefWindowProcW(w,m,a,b);}
static const std::vector<beautify::Color>*expected=nullptr;
static void verifySource(IDXGISwapChain*chain){
    ComPtr<ID3D11Texture2D>back,read;check(chain->GetBuffer(0,IID_PPV_ARGS(&back)));D3D11_TEXTURE2D_DESC d;back->GetDesc(&d);
    ComPtr<ID3D11Device>device;ComPtr<ID3D11DeviceContext>context;back->GetDevice(&device);device->GetImmediateContext(&context);
    d.Usage=D3D11_USAGE_STAGING;d.BindFlags=0;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;d.MiscFlags=0;check(device->CreateTexture2D(&d,nullptr,&read));
    context->CopyResource(read.Get(),back.Get());D3D11_MAPPED_SUBRESOURCE map;check(context->Map(read.Get(),0,D3D11_MAP_READ,0,&map));
    for(UINT y=0;y<d.Height;y++)assert(memcmp((char*)map.pData+(size_t)y*map.RowPitch,expected->data()+(size_t)y*d.Width,d.Width*4)==0);context->Unmap(read.Get(),0);
}
int wmain(int argc,wchar_t**argv){
    assert(argc>=2);bool visible=argc>2;
    CoInitializeEx(nullptr,COINIT_MULTITHREADED);
    wchar_t dll[MAX_PATH];swprintf(dll,MAX_PATH,L"%ls\\dxgi-probe.dll",argv[1]);HMODULE lib=LoadLibraryW(dll);assert(lib);
    auto start=(void(*)(const wchar_t*))GetProcAddress(lib,"probe_start");assert(start);start(argv[1]);
    auto state=(int(*)(IDXGISwapChain1*,UINT*,UINT*))GetProcAddress(lib,"probe_test_state");assert(state);
    auto observer=(void(*)(void(*)(IDXGISwapChain*)))GetProcAddress(lib,"probe_test_observer");assert(observer);observer(verifySource);
    wchar_t settings[MAX_PATH];swprintf(settings,MAX_PATH,L"%ls\\theme.ini",argv[1]);
    WNDCLASSW wc={};wc.lpfnWndProc=wnd;wc.hInstance=GetModuleHandleW(nullptr);wc.lpszClassName=L"BeautifySyntheticTest";assert(RegisterClassW(&wc));
    HWND window=CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP,wc.lpszClassName,L"Independent engine - synthetic layout",WS_OVERLAPPEDWINDOW,100,100,930,730,nullptr,nullptr,wc.hInstance,nullptr);assert(window);
    if(visible)ShowWindow(window,SW_SHOW);
    ComPtr<ID3D11Device>device;ComPtr<ID3D11DeviceContext>context;D3D_FEATURE_LEVEL level;
    check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context));
    ComPtr<IDXGIDevice>dx;check(device.As(&dx));ComPtr<IDXGIAdapter>adapter;check(dx->GetAdapter(&adapter));ComPtr<IDXGIFactory2>factory;check(adapter->GetParent(IID_PPV_ARGS(&factory)));
    // Exercise both source byte orders using the observed DComp generation.
    for(int mode=0;mode<2;mode++){
        ComPtr<IDCompositionDevice>comp;ComPtr<IDCompositionDevice2>comp2;ComPtr<IDCompositionDesktopDevice>desktop;ComPtr<IDCompositionTarget>target;ComPtr<IDCompositionVisual>visual;
        check(DCompositionCreateDevice(dx.Get(),IID_PPV_ARGS(&comp)));check(comp->CreateTargetForHwnd(window,TRUE,&target));check(comp->CreateVisual(&visual));
        auto commit=[&](){check(comp?comp->Commit():comp2->Commit());};
        DXGI_SWAP_CHAIN_DESC1 desc={};desc.Width=900;desc.Height=680;desc.Format=mode?DXGI_FORMAT_R8G8B8A8_UNORM:DXGI_FORMAT_B8G8R8A8_UNORM;desc.SampleDesc.Count=1;desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;desc.BufferCount=2;desc.SwapEffect=DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;desc.Scaling=DXGI_SCALING_STRETCH;desc.AlphaMode=DXGI_ALPHA_MODE_PREMULTIPLIED;
        ComPtr<IDXGISwapChain1>chain;check(factory->CreateSwapChainForComposition(device.Get(),&desc,nullptr,&chain));check(visual->SetContent(chain.Get()));check(target->SetRoot(visual.Get()));commit();
        auto draw=[&](UINT w,UINT h){
            std::vector<beautify::Color>pixels((size_t)w*h);
            for(UINT y=0;y<h;y++)for(UINT x=0;x<w;x++)pixels[(size_t)y*w+x]=x<60?beautify::Color{225,225,230,255}:x<300?beautify::Color{238,238,240,255}:beautify::Color{250,250,250,255};
            for(UINT y=150;y<220;y++)for(UINT x=390;x<650;x++)pixels[(size_t)y*w+x]={20,25,30,255};
            if(mode)for(auto&p:pixels)std::swap(p.r,p.b);
            ComPtr<ID3D11Texture2D>back;check(chain->GetBuffer(0,IID_PPV_ARGS(&back)));context->UpdateSubresource(back.Get(),0,nullptr,pixels.data(),w*4,0);
            // Verify after theme composition, before the source's flip rotates DXGI resources.
            expected=&pixels;DXGI_PRESENT_PARAMETERS p={};check(chain->Present1(0,0,&p));expected=nullptr;
            MSG msg;while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){TranslateMessage(&msg);DispatchMessageW(&msg);}
        };
        WritePrivateProfileStringW(L"theme",L"enabled",L"1",settings);Sleep(750);draw(900,680);UINT w,h;assert(state(chain.Get(),&w,&h)==1&&w==900&&h==680);
        for(int i=0;i<4;i++)draw(900,680);
        check(chain->ResizeBuffers(2,1000,740,desc.Format,0));draw(1000,740);assert(state(chain.Get(),&w,&h)==1&&w==1000&&h==740);
        WritePrivateProfileStringW(L"theme",L"enabled",L"0",settings);Sleep(750);draw(1000,740);assert(state(chain.Get(),nullptr,nullptr)==0);
        WritePrivateProfileStringW(L"theme",L"enabled",L"1",settings);Sleep(750);draw(1000,740);assert(state(chain.Get(),nullptr,nullptr)==1);
        printf("PASS: %s, separate output, source unchanged, resize, restore, re-enable\n",mode?"RGBA":"BGRA");fflush(stdout);
        if(visible&&mode==1){for(int i=0;i<900;i++){draw(1000,740);Sleep(50);}}
        check(visual->SetContent(nullptr));check(target->SetRoot(nullptr));commit();
    }
    DestroyWindow(window);puts("ALL PRESENTATION TESTS PASSED");return 0;
}
