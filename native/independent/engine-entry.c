/* Independently authored entry point. No Qt private memory patches.
 * qt_testability_init runs after Qt startup, outside DllMain. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
#include <wchar.h>
#include <string.h>
#include "selected-adapter.h"
#pragma comment(lib,"version.lib")
static HMODULE self,module;
// Return 2 for the tested file, 1 for an untested build in the same family.
// The semantic engine must pass its structural preflight before patching either.
static int admitted(void){
    static const unsigned char expected[32]=BEAUTIFY_TARGET_HASH_BYTES;
    wchar_t path[32768];unsigned char bytes[65536],digest[32];DWORD read;int equal=0;
    BCRYPT_ALG_HANDLE algorithm=NULL;BCRYPT_HASH_HANDLE hash=NULL;HANDLE file=INVALID_HANDLE_VALUE;
    if(!GetModuleFileNameW((HMODULE)module,path,32768))goto cleanup;
    DWORD ignored=0,versionSize=GetFileVersionInfoSizeW(path,&ignored);
    if(!versionSize||versionSize>1024*1024)goto cleanup;
    void*version=HeapAlloc(GetProcessHeap(),0,versionSize);if(!version)goto cleanup;
    VS_FIXEDFILEINFO*info=NULL;UINT infoSize=0;int family=0;
    if(GetFileVersionInfoW(path,0,versionSize,version)&&VerQueryValueW(version,L"\\",(void**)&info,&infoSize)&&infoSize>=sizeof(*info)&&info->dwSignature==0xfeef04bd)
        family=HIWORD(info->dwFileVersionMS)==BEAUTIFY_VERSION_MAJOR&&LOWORD(info->dwFileVersionMS)==BEAUTIFY_VERSION_MINOR&&HIWORD(info->dwFileVersionLS)==BEAUTIFY_VERSION_PATCH;
    HeapFree(GetProcessHeap(),0,version);if(!family)goto cleanup;
    file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,FILE_FLAG_SEQUENTIAL_SCAN,NULL);
    if(file==INVALID_HANDLE_VALUE)goto cleanup;
    // Check the actual PE machine as well as the version resource.
    IMAGE_DOS_HEADER dos;IMAGE_NT_HEADERS64 nt;DWORD count;
    if(!ReadFile(file,&dos,sizeof dos,&count,NULL)||count!=sizeof dos||dos.e_magic!=IMAGE_DOS_SIGNATURE||dos.e_lfanew<0||dos.e_lfanew>1024*1024)goto cleanup;
    if(SetFilePointer(file,dos.e_lfanew,NULL,FILE_BEGIN)==INVALID_SET_FILE_POINTER)goto cleanup;
    if(!ReadFile(file,&nt,sizeof nt,&count,NULL)||count!=sizeof nt||nt.Signature!=IMAGE_NT_SIGNATURE||nt.FileHeader.Machine!=IMAGE_FILE_MACHINE_AMD64||nt.OptionalHeader.Magic!=IMAGE_NT_OPTIONAL_HDR64_MAGIC)goto cleanup;
    if(SetFilePointer(file,0,NULL,FILE_BEGIN)==INVALID_SET_FILE_POINTER)goto cleanup;
    if(BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,NULL,0)<0)goto cleanup;
    if(BCryptCreateHash(algorithm,&hash,NULL,0,NULL,0,0)<0)goto cleanup;
    do{if(!ReadFile(file,bytes,sizeof(bytes),&read,NULL))goto cleanup;if(read&&BCryptHashData(hash,bytes,read,0)<0)goto cleanup;}while(read);
    if(BCryptFinishHash(hash,digest,32,0)<0)goto cleanup;
#ifdef SEMANTIC_ENGINE
    equal=memcmp(digest,expected,32)==0?2:1;
#else
    equal=memcmp(digest,expected,32)==0?2:0;
#endif
cleanup:
    if(file!=INVALID_HANDLE_VALUE)CloseHandle(file);
    if(hash)BCryptDestroyHash(hash);
    if(algorithm)BCryptCloseAlgorithmProvider(algorithm,0);
    return equal;
}
static void incompatible(const wchar_t*folder,const wchar_t*reason){
    wchar_t config[MAX_PATH],path[MAX_PATH],temp[MAX_PATH],revision[80];
    if(swprintf(config,MAX_PATH,L"%ls\\theme.ini",folder)<0||swprintf(path,MAX_PATH,L"%ls\\theme.receipt",folder)<0||swprintf(temp,MAX_PATH,L"%ls.tmp",path)<0)return;
    GetPrivateProfileStringW(L"theme",L"revision",L"",revision,80,config);
    FILE*f=_wfopen(temp,L"w, ccs=UTF-8");if(!f)return;
    fwprintf(f,L"protocol=1\npid=%lu\nrevision=%ls\nstate=incompatible\nreason=%ls\n",GetCurrentProcessId(),revision,reason);fclose(f);
    MoveFileExW(temp,path,MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH);
}
__declspec(dllexport) void qt_testability_init(void){
    static LONG started;if(InterlockedExchange(&started,1))return;
    wchar_t folder[MAX_PATH],path[MAX_PATH];if(!GetModuleFileNameW(self,folder,MAX_PATH))return;
    wchar_t*slash=wcsrchr(folder,L'\\');if(!slash)return;*slash=0;
    module=GetModuleHandleW(L"Weixin.dll");if(!module){incompatible(folder,L"target-module-missing");return;}
    if(!admitted()){incompatible(folder,L"target-family-or-machine-mismatch");return;}
#ifdef SEMANTIC_ENGINE
    swprintf(path,MAX_PATH,L"%ls\\beautify-engine.dll",folder);
#else
    swprintf(path,MAX_PATH,L"%ls\\dxgi-probe.dll",folder);
#endif
    HMODULE renderer=LoadLibraryW(path);if(!renderer){incompatible(folder,L"renderer-load-failed");return;}
#ifdef SEMANTIC_ENGINE
    void(*start)(const wchar_t*)=(void(*)(const wchar_t*))GetProcAddress(renderer,"semantic_start");
#else
    void(*start)(const wchar_t*)=(void(*)(const wchar_t*))GetProcAddress(renderer,"probe_start");
#endif
    if(start)start(folder);else incompatible(folder,L"renderer-entry-missing");
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH){self=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
