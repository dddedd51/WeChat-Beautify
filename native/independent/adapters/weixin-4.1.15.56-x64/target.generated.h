// Independently observed adapter facts. See ../../RESEARCH.md.
#pragma once
#define BEAUTIFY_ADAPTER_ID "weixin-4.1.15.56-x64"
#define BEAUTIFY_VERSION_MAJOR 4
#define BEAUTIFY_VERSION_MINOR 1
#define BEAUTIFY_VERSION_PATCH 15
#define BEAUTIFY_TARGET_HASH_BYTES {0xe0,0x42,0x8a,0x7b,0x69,0x18,0x53,0xde,0x49,0x65,0x32,0x52,0x3c,0x2c,0x09,0xaa,0x09,0x5f,0x90,0x94,0x1c,0x37,0xc9,0xf4,0xcf,0xb3,0xf1,0x37,0xf3,0xc6,0x65,0x56}
#ifdef __cplusplus
namespace adapter {
constexpr unsigned MainWindow=0x983c628,MainView=0x97beb48,SessionCell=0x9b64a88;
constexpr unsigned Widget=0x8ffbb98,TableView=0x938b3b8,SessionList=0x9b62c98;
constexpr unsigned PrimaryText=0x9883ab8,TextView=0x93ab1e8,LabelContainer=0x9d261e8;
constexpr unsigned StackedWidget=0x91e41e8;
constexpr unsigned ImageConstruct=0x4fd250,ImageWrap=0x70b60,ImageDestroy=0x4fdbc0;
constexpr unsigned WidgetUpdate=0x5b7260,CanvasImage=0x53fa30;
constexpr unsigned PaintConstruct=0x532fd0,PaintSetColor=0x533980,CanvasRect=0x53cd00,PaintDestroy=0x5332b0;
constexpr unsigned TextCacheInvalidate=0x1e9f0c0,ParagraphInvalidate=0x6249740,ParagraphVirtual=0x44e7bd0;
constexpr unsigned RowSlot=0x9b64d50,RowPaint=0x6013f30,TextPaint=0x1ea62d0;
constexpr unsigned ObjectDataOffset=8,ParentOffset=16,WidgetDataOffset=0x28;
constexpr unsigned GeometryX=0x14,GeometryY=0x18,GeometryRight=0x1c,GeometryBottom=0x20;
constexpr unsigned TextColorOffset=0x234,TextCacheOffset=0x188,ParagraphHolderOffset=0x3e0;
constexpr unsigned ParagraphVirtualOffset=0x78,ParagraphMinimumBytes=0x240;
constexpr unsigned PaintInterfaceOffset=0xc0,RowInterfaceOffset=0x120,RowSelectedOffset=0x210,CustomPaintOffset=0x160;
constexpr unsigned ImageReferenceOffset=8,ImageWidthOffset=32,ImageHeightOffset=36;
constexpr bool IsMainWindow(unsigned rva){return rva==MainWindow;}
constexpr unsigned FunctionRvas[]={ImageConstruct,ImageWrap,ImageDestroy,WidgetUpdate,CanvasImage,PaintConstruct,PaintSetColor,CanvasRect,PaintDestroy,TextCacheInvalidate,ParagraphInvalidate,ParagraphVirtual,RowPaint,TextPaint};
struct MetaGuard {unsigned table,getter,meta;const char*name;};
constexpr MetaGuard MetaGuards[]={
    {Widget,0x5d3420,0x8ffb238,"QWidget"},
    {TableView,0x917aa0,0x90734d8,"mmui::XTableView"},
    {TextView,0x91a530,0x9070e98,"mmui::XTextView"},
    {MainView,0x8cbb10,0x905a890,"mmui::MainView"},
    {MainWindow,0x8cbc00,0x905a948,"mmui::MainWindow"},
    {PrimaryText,0x91a530,0x9070e98,"mmui::XTextView"},
    {SessionList,0x8acfe0,0x9051f18,"mmui::ChatSessionList"},
    {SessionCell,0x8aca40,0x9051dc8,"mmui::ChatSessionCell"},
    {LabelContainer,0x91a960,0x90742c0,"mmui::DragActionView"}
};
}
#endif
