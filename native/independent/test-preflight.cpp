// Synthetic PE metadata only: no WeChat process, code execution or hook writes.
#define BEAUTIFY_TESTING
#include "semantic-engine.cpp"
#include <cassert>
#include <set>
#include <array>
struct FakeWidget {
    alignas(8) unsigned char object[0x400]{},data[0x40]{},widget[0x40]{};
    void init(unsigned type,FakeWidget*parent,Geometry box){
        *(void**)object=module+type;*(void**)(object+adapter::ObjectDataOffset)=data;*(void**)(object+adapter::WidgetDataOffset)=widget;
        *(void**)(data+adapter::ParentOffset)=parent?parent->object:nullptr;
        *(int*)(widget+adapter::GeometryX)=box.x;*(int*)(widget+adapter::GeometryY)=box.y;
        *(int*)(widget+adapter::GeometryRight)=box.x+box.width-1;*(int*)(widget+adapter::GeometryBottom)=box.y+box.height-1;
    }
};
static void testTraversal(){
    for(const auto&t:objectTypes)assert(typeFor((uintptr_t)(module+t.primary))==&t);
    assert(!typeFor((uintptr_t)module-1)&&!typeFor((uintptr_t)module+UINT64_C(0x100000000)+objectTypes[0].primary));
    FakeWidget window,outer,host,inner,list,table,viewport,content,row,label;
    window.init(adapter::MainWindow,nullptr,{859,201,826,730});outer.init(adapter::MainView,&window,{0,0,826,730});host.init(adapter::Widget,&outer,{60,0,766,730});inner.init(adapter::MainView,&host,{0,32,762,694});
    list.init(adapter::SessionList,&inner,{0,48,189,646});table.init(adapter::TableView,&list,{0,0,189,646});viewport.init(adapter::Widget,&table,{0,0,189,646});content.init(adapter::Widget,&viewport,{0,-130,189,17160});row.init(adapter::SessionCell,&content,{0,325,189,65});
    memoryChecks=0;auto l=locate(row.object);assert(l.valid&&l.rowPath&&l.rootObject==outer.object&&l.x==60&&l.y==275);
    assert(memoryChecks==26); // 3 validated spans per ancestor; no per-field re-query.
    printf("PASS: nine-node scrolling row requires %u memory checks (previous traversal required 88)\n",memoryChecks);
    // Geometry is read again on every call, including a recycled scrolling row.
    content.init(adapter::Widget,&viewport,{0,-195,189,17160});assert(locate(row.object).y==210);
    unsigned contact=0;for(const auto&t:objectTypes)if(!strcmp(t.name,"mmui::ContactsTableBaseView")){contact=t.primary;break;}assert(contact);
    list.init(contact,&inner,{0,48,189,646});label.init(paintTargets[0].primary,&row,{30,0,130,20});
    assert(!locate(label.object).valid&&!locate(list.object).valid);
    list.init(adapter::SessionList,&inner,{0,48,189,646});assert(locate(row.object).valid);
    *(void**)(content.object+adapter::WidgetDataOffset)=nullptr;assert(!locate(row.object).valid);
    content.init(adapter::Widget,&content,{0,-130,189,17160});assert(!locate(row.object).valid);
    puts("PASS: actual locator uses fresh geometry, protects contacts descendants, and rejects invalid pointers/cycles");
}
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
    assert(preflight()==nullptr);testTraversal();VirtualFree(memory,0,MEM_RELEASE);module=nullptr;
    puts("PASS: structural preflight accepts coherent fixtures; rejects machine, function, vtable, interface, paint and row mismatches without writes");
}
