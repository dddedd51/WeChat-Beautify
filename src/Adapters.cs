using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Generated identities come from the reviewed adapter manifests and the DLLs
// built with them. No downloaded manifest can authorize a new native adapter.
internal sealed class AdapterProfile {
    internal readonly string Id, Version, TargetHash, Status, Reason, EntryHash, EngineHash;
    internal AdapterProfile(string id,string version,string targetHash,string status,string reason,string entryHash,string engineHash){
        if(!Regex.IsMatch(id??"","^weixin-[0-9]+(?:\\.[0-9]+){3}-x64$")||
           !Regex.IsMatch(version??"","^[0-9]+(?:\\.[0-9]+){3}$")||id!="weixin-"+version+"-x64"||
           !IsHash(targetHash)||(status!="verified"&&status!="experimental"&&status!="pending")||
           (status!="pending"&&(!IsHash(entryHash)||!IsHash(engineHash)))||
           (status=="pending"&&(!String.IsNullOrEmpty(entryHash)||!String.IsNullOrEmpty(engineHash))))
            throw new InvalidDataException("适配器清单无效。");
        Id=id;Version=version;TargetHash=targetHash.ToLowerInvariant();Status=status;Reason=reason??"";
        EntryHash=entryHash;EngineHash=engineHash;
    }
    static bool IsHash(string text){return Regex.IsMatch(text??"","^[0-9a-fA-F]{64}$");}
    internal bool CanLoad {get{return Status!="pending";}}
    internal string Family {get{return Version.Substring(0,Version.LastIndexOf('.')+1)+"X";}}
    internal bool MatchesFamily(string version){
        return Regex.IsMatch(version??"","^[0-9]+(?:\\.[0-9]+){3}$")&&
            version.Substring(0,version.LastIndexOf('.')+1)==Version.Substring(0,Version.LastIndexOf('.')+1);
    }
    internal bool IsTestedBuild(string version,string hash){return Status=="verified"&&Version==version&&String.Equals(TargetHash,hash,StringComparison.OrdinalIgnoreCase);}
    internal string StatusLabel {get{return Status=="verified"?"已实测":Status=="experimental"?"实验性 · 待实机验证":"待适配";}}
    internal string Bundle(string root){return Path.Combine(root,"native-runtime",Id);}
    // Include both native binaries, so rebuilding only the entry never aliases
    // the path of a DLL pair that is already loaded in another process.
    internal string Runtime {get{return Path.Combine(Files.Data,"engine",Id,EntryHash.Substring(0,16)+"-"+EngineHash.Substring(0,16));}}
}
internal static class Adapters {
    internal static IReadOnlyList<AdapterProfile> All {get{return RuntimeIdentity.Profiles;}}
    internal static AdapterProfile Resolve(string version,ushort machine,string hash,out string error){return Resolve(All,version,machine,hash,out error);}
    internal static AdapterProfile Resolve(IEnumerable<AdapterProfile> profiles,string version,ushort machine,string hash,out string error){
        error=null;
        if(machine!=0x8664){error="当前仅提供 Windows x64 适配，检测到的微信架构不受支持。";return null;}
        var candidates=profiles.Where(p=>p.MatchesFamily(version)).ToArray();
        if(candidates.Length==0){error="微信 "+version+" 所属版本系列暂无适配器，请等待对应系列适配。";return null;}
        var match=candidates.FirstOrDefault(p=>p.Version==version&&String.Equals(p.TargetHash,hash,StringComparison.OrdinalIgnoreCase))??candidates.FirstOrDefault(p=>p.CanLoad)??candidates[0];
        if(!match.CanLoad){error="已识别微信 "+version+"，当前待适配。"+match.Reason;return null;}
        return match;
    }
    internal static string Summary {get{return String.Join("\n",All.Select(p=>p.Family+" x64 · "+(p.Status=="verified"?"实测 "+p.Version:p.StatusLabel)));}}
}
