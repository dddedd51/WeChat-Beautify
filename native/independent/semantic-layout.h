#pragma once
#include "selected-adapter.h"
#include <cstdint>
#include <cstddef>
namespace semantic {
struct Geometry { int x=0,y=0,width=0,height=0; };
// Qt scroll-content widgets may exceed the size of a render target.
inline bool validGeometry(const Geometry&g){
    constexpr int limit=16777215;
    return g.width>0&&g.height>0&&g.width<=limit&&g.height<=limit&&
        g.x>=-limit&&g.x<=limit&&g.y>=-limit&&g.y<=limit;
}
struct LayoutNode { std::uint32_t type; Geometry box; };
struct RootLayout { Geometry local{},root{}; int x=0,y=0; std::size_t rootIndex=0; bool valid=false; };
// Select the outer MainView under this version's MainWindow, rather than
// stopping at the nested chat MainView. Never use desktop window coordinates.
inline RootLayout locateRoot(const LayoutNode*nodes,std::size_t count){
    RootLayout out;if(!count)return out;out.local=nodes[0].box;
    std::int64_t x=0,y=0;bool found=false;
    for(std::size_t i=0;i<count;i++){
        const auto&n=nodes[i];if(!validGeometry(n.box))return {};
        if(adapter::IsMainWindow(n.type)){out.valid=found;return out;}
        if(n.type==adapter::MainView){
            if(n.box.width>32768||n.box.height>32768||x<-16777215||x>16777215||y<-16777215||y>16777215)return {};
            out.root=n.box;out.x=(int)x;out.y=(int)y;out.rootIndex=i;found=true;
        }
        x+=n.box.x;y+=n.box.y;
    }
    return {};
}
inline bool sessionRowPrefix(const LayoutNode*n,std::size_t count){
    return count>=5&&n[0].type==adapter::SessionCell&&n[1].type==adapter::Widget&&
        n[2].type==adapter::Widget&&n[3].type==adapter::TableView&&n[4].type==adapter::SessionList;
}
}
