// Synthetic PE metadata only: no WeChat process, code execution or hook writes.
#include "semantic-engine.cpp"
#include <cassert>
#include <set>
int main(){
    constexpr unsigned imageSize=0x0b000000,colStart=0x0a800000;
    auto*memory=(unsigned char*)VirtualAlloc(nullptr,imageSize,MEM_RESERVE,PAGE_READWRITE);assert(memory);
    module=memory;std::set<unsigned>pages;
    auto commit=[&](unsigned rva,size_t bytes){for(unsigned at=rva&~4095u;at<rva+bytes;at+=4096)if(pages.insert(at).second)assert(VirtualAlloc(memory+at,4096,MEM_COMMIT,PAGE_READWRITE));};
    commit(0,4096);auto*dos=(IMAGE_DOS_HEADER*)memory;dos->e_magic=IMAGE_DOS_SIGNATURE;dos->e_lfanew=0x100;
    auto*nt=(IMAGE_NT_HEADERS64*)(memory+0x100);nt->Signature=IMAGE_NT_SIGNATURE;nt->FileHeader.Machine=IMAGE_FILE_MACHINE_AMD64;nt->FileHeader.NumberOfSections=1;nt->FileHeader.SizeOfOptionalHeader=sizeof(IMAGE_OPTIONAL_HEADER64);nt->OptionalHeader.Magic=IMAGE_NT_OPTIONAL_HDR64_MAGIC;nt->OptionalHeader.SizeOfImage=imageSize;
    auto*section=IMAGE_FIRST_SECTION(nt);section->VirtualAddress=0x1000;section->Misc.VirtualSize=0x08000000;section->Characteristics=IMAGE_SCN_MEM_EXECUTE|IMAGE_SCN_MEM_READ;
    unsigned nextCol=colStart;
    auto table=[&](unsigned rva,unsigned displacement,unsigned type,unsigned fn){
        commit(rva-8,24);commit(nextCol,24);auto*col=(unsigned*)(memory+nextCol);
        col[0]=1;col[1]=displacement;col[3]=type;col[5]=nextCol;
        *(void**)(memory+rva-8)=col;*(void**)(memory+rva)=memory+fn;nextCol+=32;commit(fn,1);
    };
    for(unsigned rva:adapter::FunctionRvas)commit(rva,1);
    for(const auto&t:objectTypes)table(t.primary,0,t.primary,adapter::WidgetUpdate);
    unsigned nextStrings=0x0a900000;
    for(const auto&g:adapter::MetaGuards){
        commit(g.getter,1);*(void**)(memory+g.table)=memory+g.getter;commit(g.meta,16);commit(nextStrings,256);
        *(void**)(memory+g.meta+8)=memory+nextStrings;*(int*)(memory+nextStrings+4)=(int)strlen(g.name);*(intptr_t*)(memory+nextStrings+16)=24;
        memcpy(memory+nextStrings+24,g.name,strlen(g.name)+1);nextStrings+=256;
    }
    for(const auto&t:paintTargets){table(t.table,adapter::PaintInterfaceOffset,t.primary,t.callback);*(void**)(memory+t.table+8)=memory+t.callback;}
    table(adapter::RowSlot-8,adapter::RowInterfaceOffset,adapter::SessionCell,adapter::RowPaint);*(void**)(memory+adapter::RowSlot)=memory+adapter::RowPaint;
    assert(preflight()==nullptr);
    auto*strings=*(unsigned char**)(memory+adapter::MetaGuards[0].meta+8);char first=*(char*)(strings+24);*(char*)(strings+24)='?';
    assert(!wcscmp(preflight(),L"key-object-meta-name"));*(char*)(strings+24)=first;
    auto*slot=(void**)(memory+paintTargets[0].table+8);void*saved=*slot;*slot=memory+adapter::RowPaint;
    assert(!wcscmp(preflight(),L"paint-slot-mismatch"));assert(*slot==memory+adapter::RowPaint);*slot=saved;
    auto*col=*(unsigned**)(memory+paintTargets[0].table-8);unsigned type=col[3];col[3]=1;
    assert(!wcscmp(preflight(),L"paint-interface-layout"));col[3]=type;
    auto*row=(void**)(memory+adapter::RowSlot);saved=*row;*row=nullptr;
    assert(!wcscmp(preflight(),L"row-slot-mismatch"));*row=saved;
    auto*primary=(void**)(memory+objectTypes[0].primary);saved=*primary;*primary=memory+colStart;
    assert(!wcscmp(preflight(),L"object-vtable-layout"));*primary=saved;
    section->Characteristics=IMAGE_SCN_MEM_READ;
    assert(!wcscmp(preflight(),L"function-outside-executable-section"));section->Characteristics|=IMAGE_SCN_MEM_EXECUTE;
    nt->FileHeader.Machine=IMAGE_FILE_MACHINE_I386;assert(!wcscmp(preflight(),L"module-machine"));nt->FileHeader.Machine=IMAGE_FILE_MACHINE_AMD64;
    assert(preflight()==nullptr);VirtualFree(memory,0,MEM_RELEASE);module=nullptr;
    puts("PASS: structural preflight accepts coherent fixtures; rejects machine, function, vtable, interface, paint and row mismatches without writes");
}
