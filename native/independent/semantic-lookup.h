#pragma once
#include <cstddef>
#include <cstdint>
namespace semantic {
// Immutable vtable metadata, never a cache of QWidget pointers or geometry.
// The caller validates strictly increasing keys before installing any hook.
template<class T,class Key>
inline const T*findSorted(const T*rows,std::size_t count,std::uintptr_t wanted,Key key){
    std::size_t first=0,last=count;
    while(first<last){auto middle=first+(last-first)/2;if(key(rows[middle])<wanted)first=middle+1;else last=middle;}
    return first<count&&key(rows[first])==wanted?rows+first:nullptr;
}
template<class T,class Key>
inline bool strictlySorted(const T*rows,std::size_t count,Key key){
    for(std::size_t i=1;i<count;i++)if(key(rows[i-1])>=key(rows[i]))return false;
    return true;
}
}
