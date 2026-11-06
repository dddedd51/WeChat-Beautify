/* New glass extension: no wallpaper, capture, global opacity or color key.
 * Target: exact Weixin.dll SHA-256, QWidget::setAttribute RVA 0x5b7a00.
 * The native render integration remains experimental until checked in Weixin. */
typedef struct { int state,flags; DWORD color; int animation; } GlassAccent;
typedef struct { int attribute; void *data; SIZE_T size; } GlassComposition;
typedef BOOL (WINAPI *GlassSetComposition)(HWND,GlassComposition*);
typedef BOOL (WINAPI *GlassGetComposition)(HWND,GlassComposition*);
typedef void (*QtAttribute)(void*,int,int);
static void *glassTop;
static int oldTranslucent,oldNoBackground,oldOpaque,oldSystemBackdrop,oldDarkMode;
static int haveBackdrop,haveDark,haveAccent,haveFill,oldFill;
static GlassAccent oldAccent;
static HWND glassWindow;
static int settingAttribute;
static int attrValue(void *widget,int attribute){
    byte *d=ptr((byte*)widget+(attribute<32?0x28:8));
    unsigned *bits=(unsigned*)(d+(attribute<32?8:0x1a0+((attribute-32)/32)*4));
    return readable(bits,4)?((*bits>>(attribute%32))&1):0;
}
static void setAttr(void *o,int attr,int on){
    ((QtAttribute)(image+0x5b7a00))(o,attr,on);
}
static void glassRead(void){
    int next=GetPrivateProfileIntW(L"glass",L"enabled",0,configPath)==1;
    if(!next&&glassReady)glassRestore();
    glassEnabled=next;
    glassDensity=GetPrivateProfileIntW(L"glass",L"density",65,configPath);
    if(glassDensity<20)glassDensity=20;if(glassDensity>95)glassDensity=95;
    glassDark=GetPrivateProfileIntW(L"glass",L"dark",0,configPath)==1;
}
static void glassReceipt(const char *state,HRESULT hr,int accent){
    wchar_t p[1100];size_t n=wcslen(configPath);FILE *f;
    wcscpy(p,configPath);while(n&&p[n-1]!=L'\\')n--;p[n]=0;wcscat(p,L"glass.receipt");
    f=_wfopen(p,L"wb");if(f){fprintf(f,"state=%s\npid=%lu\nwindow=%llu\ngeneration=%u\nqtAlpha=%d\ndwmResult=%ld\naccent=%d\nvisualVerified=0\n",state,GetCurrentProcessId(),(unsigned long long)(up)glassWindow,status.generation,glassTop?attrValue(glassTop,120):0,(long)hr,accent);fclose(f);}
}
static void glassApply(void){
    void *top,*p;int depth,dk,backdrop=3,accentOK=0;
    HRESULT hr;GlassSetComposition set;GlassGetComposition get;
    GlassAccent a;GlassComposition data;
    if(!glassEnabled){glassRestore();return;}
    if(!controlWindow||!mainRoot||settingAttribute)return;
    if(glassReady&&glassLastGeneration==status.generation)return;
    if(!glassReady){
        top=mainRoot;
        for(depth=0;depth<32;depth++){p=parent(top);if(!p)break;if(!readable(p,0x168))return;top=p;}
        if(depth==32)return;
        glassTop=top;glassWindow=controlWindow;
        oldTranslucent=attrValue(top,120);oldNoBackground=attrValue(top,9);oldOpaque=attrValue(top,4);
        {byte *extra=ptr((byte*)ptr((byte*)top+8)+0x58);haveFill=readable(extra,0x70);oldFill=haveFill&&(*(unsigned*)(extra+0x6c)&16)!=0;}
        haveBackdrop=SUCCEEDED(DwmGetWindowAttribute(glassWindow,38,&oldSystemBackdrop,sizeof oldSystemBackdrop));
        haveDark=SUCCEEDED(DwmGetWindowAttribute(glassWindow,20,&oldDarkMode,sizeof oldDarkMode));
        get=(GlassGetComposition)GetProcAddress(GetModuleHandleW(L"user32.dll"),"GetWindowCompositionAttribute");
        data.attribute=19;data.data=&oldAccent;data.size=sizeof oldAccent;
        haveAccent=get&&get(glassWindow,&data);
        /* Refuse a window whose compositor state cannot be saved for recovery. */
        if(!haveAccent){glassReceipt("unsupported",E_NOTIMPL,0);glassTop=NULL;glassWindow=NULL;return;}
        settingAttribute=1;
        setAttr(top,120,1);setAttr(top,9,1);setAttr(top,4,0);
        if(oldFill)((void(*)(void*,int))(image+0x5b6e60))(top,0);
        settingAttribute=0;glassReady=1;
    }
    dk=glassDark;DwmSetWindowAttribute(glassWindow,20,&dk,sizeof dk);
    hr=DwmSetWindowAttribute(glassWindow,38,&backdrop,sizeof backdrop);
    set=(GlassSetComposition)GetProcAddress(GetModuleHandleW(L"user32.dll"),"SetWindowCompositionAttribute");
    a.state=4;a.flags=2;a.animation=0;
    a.color=((DWORD)(glassDensity*255/100)<<24)|(glassDark?0x0028211c:0x00f4eee8);
    data.attribute=19;data.data=&a;data.size=sizeof a;
    if(set)accentOK=set(glassWindow,&data);
    glassLastGeneration=status.generation;
    glassReceipt((accentOK&&attrValue(glassTop,120))?"configured":"failed",hr,accentOK);
    logStatus("glass-configured");
}
static void glassRestore(void){
    GlassSetComposition set;GlassComposition data;
    if(settingAttribute)return;
    if(!glassReady){glassReceipt("disabled",S_OK,0);return;}
    settingAttribute=1;
    if(glassWindow&&IsWindow(glassWindow)){
        set=(GlassSetComposition)GetProcAddress(GetModuleHandleW(L"user32.dll"),"SetWindowCompositionAttribute");
        data.attribute=19;data.data=&oldAccent;data.size=sizeof oldAccent;
        if(set&&haveAccent)set(glassWindow,&data);
        if(haveBackdrop)DwmSetWindowAttribute(glassWindow,38,&oldSystemBackdrop,sizeof oldSystemBackdrop);
        if(haveDark)DwmSetWindowAttribute(glassWindow,20,&oldDarkMode,sizeof oldDarkMode);
    }
    if(glassTop&&readable(glassTop,0x168)){
        setAttr(glassTop,120,oldTranslucent);setAttr(glassTop,9,oldNoBackground);setAttr(glassTop,4,oldOpaque);
        if(haveFill)((void(*)(void*,int))(image+0x5b6e60))(glassTop,oldFill);
        ((void(*)(void*))(image+0x5b7260))(glassTop);
    }
    glassReceipt("disabled",S_OK,0);glassReady=0;glassTop=NULL;glassWindow=NULL;settingAttribute=0;
}
static int verifyTargetFile(void){
    static const byte expected[32]={0xe0,0x42,0x8a,0x7b,0x69,0x18,0x53,0xde,0x49,0x65,0x32,0x52,0x3c,0x2c,0x09,0xaa,0x09,0x5f,0x90,0x94,0x1c,0x37,0xc9,0xf4,0xcf,0xb3,0xf1,0x37,0xf3,0xc6,0x65,0x56};
    wchar_t path[32768];HANDLE file=INVALID_HANDLE_VALUE;BCRYPT_ALG_HANDLE alg=NULL;BCRYPT_HASH_HANDLE hash=NULL;
    byte digest[32],buffer[65536];DWORD got;int ok=0;
    if(!GetModuleFileNameW((HMODULE)image,path,32768))goto done;
    file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,FILE_FLAG_SEQUENTIAL_SCAN,NULL);
    if(file==INVALID_HANDLE_VALUE)goto done;
    if(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,NULL,0)<0)goto done;
    if(BCryptCreateHash(alg,&hash,NULL,0,NULL,0,0)<0)goto done;
    for(;;){if(!ReadFile(file,buffer,sizeof buffer,&got,NULL))goto done;if(!got)break;if(BCryptHashData(hash,buffer,got,0)<0)goto done;}
    if(BCryptFinishHash(hash,digest,32,0)<0)goto done;
    ok=!memcmp(digest,expected,32);
done:
    if(hash)BCryptDestroyHash(hash);if(alg)BCryptCloseAlgorithmProvider(alg,0);if(file!=INVALID_HANDLE_VALUE)CloseHandle(file);return ok;
}
