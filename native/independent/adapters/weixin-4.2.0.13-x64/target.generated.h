// Independently observed from Weixin.dll 4.2.0.13; see ../README.md.
// Native visual behavior has not yet been tested. No 4.1 function RVA fallback.
#pragma once
#define BEAUTIFY_ADAPTER_ID "weixin-4.2.0.13-x64"
#define BEAUTIFY_VERSION_MAJOR 4
#define BEAUTIFY_VERSION_MINOR 2
#define BEAUTIFY_VERSION_PATCH 0
#define BEAUTIFY_TARGET_HASH_BYTES {0xaf,0xe3,0xd0,0x2c,0x58,0xba,0xda,0x3c,0x35,0x00,0x9c,0x0c,0xd5,0x8e,0xe1,0xf5,0x7b,0x2a,0x00,0xfe,0x53,0x23,0x7d,0x0d,0xce,0x64,0x91,0x07,0xfd,0xdb,0x9a,0x20}
#ifdef __cplusplus
namespace adapter {
constexpr unsigned MainWindow=0x919b988,MainView=0x98debb8,SessionCell=0x9c8c5c8;
constexpr unsigned Widget=0x9103628,TableView=0x9499a38,SessionList=0x9c8a7d8;
constexpr unsigned PrimaryText=0x99a8d58,TextView=0x94b99a8,LabelContainer=0x9e57638;
constexpr unsigned StackedWidget=0x92f2f88;
constexpr unsigned ImageConstruct=0x506b60,ImageWrap=0x70b60,ImageDestroy=0x5074d0;
constexpr unsigned WidgetUpdate=0x5c09c0,CanvasImage=0x549340;
constexpr unsigned PaintConstruct=0x53c8e0,PaintSetColor=0x53d290,CanvasRect=0x546610,PaintDestroy=0x53cbc0;
constexpr unsigned TextCacheInvalidate=0x1ed8580,ParagraphInvalidate=0x6377580,ParagraphVirtual=0x45bbcb0;
constexpr unsigned RowSlot=0x9c8c890,RowPaint=0x61603f0,TextPaint=0x1edf780;
constexpr unsigned ObjectDataOffset=8,ParentOffset=16,WidgetDataOffset=0x28;
constexpr unsigned GeometryX=0x14,GeometryY=0x18,GeometryRight=0x1c,GeometryBottom=0x20;
constexpr unsigned TextColorOffset=0x234,TextCacheOffset=0x188,ParagraphHolderOffset=0x3e0;
constexpr unsigned ParagraphVirtualOffset=0x78,ParagraphMinimumBytes=0x240;
constexpr unsigned PaintInterfaceOffset=0xc0,RowInterfaceOffset=0x120,RowSelectedOffset=0x210,CustomPaintOffset=0x160;
constexpr unsigned ImageReferenceOffset=8,ImageWidthOffset=32,ImageHeightOffset=36;
constexpr bool IsMainWindow(unsigned rva){return rva==0x919b988||rva==0x9478cf8||rva==0x98ccea8||rva==0x9960ed8;}
constexpr unsigned FunctionRvas[]={ImageConstruct,ImageWrap,ImageDestroy,WidgetUpdate,CanvasImage,PaintConstruct,PaintSetColor,CanvasRect,PaintDestroy,TextCacheInvalidate,ParagraphInvalidate,ParagraphVirtual,RowPaint,TextPaint};
struct MetaGuard {unsigned table,getter,meta;const char*name;};
constexpr MetaGuard MetaGuards[]={
    {Widget,0x5dcb80,0x9102cc8,"QWidget"},
    {TableView,0x92e6d0,0x917bdf8,"mmui::XTableView"},
    {TextView,0x931160,0x91797b8,"mmui::XTextView"},
    {MainView,0x8e26d0,0x9163590,"mmui::MainView"},
    {0x919b988,0x8e27c0,0x9163648,"mmui::MainWindow"},
    {0x9478cf8,0x8e27c0,0x9163648,"mmui::MainWindow"},
    {0x98ccea8,0x410e160,0x9163648,"mmui::MainWindow"},
    {0x9960ed8,0x4636e10,0x9163648,"mmui::MainWindow"},
    {PrimaryText,0x931160,0x91797b8,"mmui::XTextView"},
    {SessionList,0x8c2f20,0x915a7f8,"mmui::ChatSessionList"},
    {SessionCell,0x8c2980,0x915a6a8,"mmui::ChatSessionCell"},
    {LabelContainer,0x9314a0,0x917cb60,"mmui::DragActionView"}
};
}
#endif
