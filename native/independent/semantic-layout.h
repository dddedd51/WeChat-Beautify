#pragma once
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
        if(n.type==0x983c628){out.valid=found;return out;}
        if(n.type==0x97beb48){
            if(n.box.width>32768||n.box.height>32768||x<-16777215||x>16777215||y<-16777215||y>16777215)return {};
            out.root=n.box;out.x=(int)x;out.y=(int)y;out.rootIndex=i;found=true;
        }
        x+=n.box.x;y+=n.box.y;
    }
    return {};
}
inline bool sessionRowPrefix(const LayoutNode*n,std::size_t count){
    return count>=5&&n[0].type==0x9b64a88&&n[1].type==0x8ffbb98&&
        n[2].type==0x8ffbb98&&n[3].type==0x938b3b8&&n[4].type==0x9b62c98;
}
}
