#include "semantic-policy.h"
#include "semantic-layout.h"
#include <cassert>
#include <cstdio>
int main(){using namespace semantic;
    assert(classify("mmui::ChatSessionList")==Region::List);
    assert(classify("mmui::ChatInputView")==Region::Input);
    assert(classify("mmui::XTextView")==Region::Preserve);
    assert(classify("unknown")==Region::Preserve);
    assert(protectedBranch("mmui::ImageMessageView"));
    assert(protectedBranch("mmui::AvatarView"));
    assert(protectedBranch("mmui::DiscoverPage"));
    LayoutNode chain[]={
        {adapter::SessionCell,{0,325,189,65}},{adapter::Widget,{0,-130,189,17160}},
        {adapter::Widget,{0,0,189,646}},{adapter::TableView,{0,0,189,646}},
        {adapter::SessionList,{0,48,189,646}},{adapter::MainView,{0,32,762,694}},
        {adapter::Widget,{60,0,766,730}},{adapter::MainView,{0,0,826,730}},
        {adapter::StackedWidget,{0,0,826,730}},{adapter::MainWindow,{859,201,826,730}}
    };
    auto l=locateRoot(chain,10);assert(l.valid&&l.rootIndex==7);
    assert(l.x==60&&l.y==275&&l.root.width==826&&l.local.height==65);
    assert(sessionRowPrefix(chain,10));
    assert(!locateRoot(chain,8).valid); // A similarly named detached window is insufficient.
    chain[1].box.height=16777216;assert(!locateRoot(chain,10).valid);
    chain[1].box.height=17160;chain[3].type=0x123456;assert(!sessionRowPrefix(chain,10));
    puts("PASS: policy names, long scrolling geometry, outer-root alignment and row ancestry fixtures");
}
