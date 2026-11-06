#pragma once
#include <cstring>
namespace semantic {
enum class Region { Preserve, Base, Navigation, List, Canvas, Input };
inline Region classify(const char*name){
    if(!strcmp(name,"mmui::MainView"))return Region::Base;
    if(!strcmp(name,"mmui::MainTabBar"))return Region::Navigation;
    if(!strcmp(name,"mmui::ChatSessionList"))return Region::List;
    if(!strcmp(name,"mmui::ChatInputView"))return Region::Input;
    if(!strcmp(name,"mmui::ChatMessagePage")||!strcmp(name,"mmui::ChatDetailView")||!strcmp(name,"mmui::ChatMasterView")||!strcmp(name,"mmui::ChatPage"))return Region::Canvas;
    if(!strcmp(name,"mmui::XView")||!strcmp(name,"mmui::XHBoxView")||!strcmp(name,"mmui::XVBoxView")||!strncmp(name,"mmui::ChatTitleBar",18))return Region::Base;
    return Region::Preserve;
}
inline bool protectedBranch(const char*name){
    // Contacts rows have their own native background/text hierarchy. Applying
    // generic XView backgrounds below them creates rectangles behind each name.
    // Preserve the complete subtree until a contacts-specific adapter is tested.
    return !strncmp(name,"mmui::Contacts",14)||strstr(name,"Avatar")||strstr(name,"MessageView")||strstr(name,"Emoticon")||strstr(name,"Emoji")||strstr(name,"Card")||strstr(name,"Bubble")||strstr(name,"Discover")||strstr(name,"Favorite")||strstr(name,"Web");
}
}
