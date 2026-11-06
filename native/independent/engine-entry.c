/* Independently authored entry point. No Qt private memory patches.
 * qt_testability_init runs after Qt startup, outside DllMain. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
#include <wchar.h>
#include <string.h>
static HMODULE self,module;
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
__declspec(dllexport) void qt_testability_init(void){
    static LONG started;if(InterlockedExchange(&started,1))return;
    module=GetModuleHandleW(L"Weixin.dll");if(!module||!verified())return;
    wchar_t folder[MAX_PATH],path[MAX_PATH];if(!GetModuleFileNameW(self,folder,MAX_PATH))return;
    wchar_t*slash=wcsrchr(folder,L'\\');if(!slash)return;*slash=0;
#ifdef SEMANTIC_ENGINE
    swprintf(path,MAX_PATH,L"%ls\\beautify-engine.dll",folder);
#else
    swprintf(path,MAX_PATH,L"%ls\\dxgi-probe.dll",folder);
#endif
    HMODULE renderer=LoadLibraryW(path);if(!renderer)return;
#ifdef SEMANTIC_ENGINE
    void(*start)(const wchar_t*)=(void(*)(const wchar_t*))GetProcAddress(renderer,"semantic_start");
#else
    void(*start)(const wchar_t*)=(void(*)(const wchar_t*))GetProcAddress(renderer,"probe_start");
#endif
    if(start)start(folder);
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH){self=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
