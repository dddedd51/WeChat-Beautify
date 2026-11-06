using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Avoid .NET Framework's performance-counter process lookup, including GetProcessById.
internal static class ProcessDiscovery {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Entry {
        public uint Size,Usage,Id;public UIntPtr Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Exe;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Module {
        public uint Size,Id,ProcessId,Usage,ProcessUsage;public IntPtr Base;public uint Bytes;public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Path;
    }
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32FirstW(IntPtr h,ref Entry e);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32NextW(IntPtr h,ref Entry e);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Module32FirstW(IntPtr h,ref Module e);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Module32NextW(IntPtr h,ref Module e);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)]static extern bool QueryFullProcessImageNameW(IntPtr h,uint flags,StringBuilder name,ref uint size);
    static List<KeyValuePair<int,string>> Snapshot(){
        var result=new List<KeyValuePair<int,string>>();var h=CreateToolhelp32Snapshot(2,0);
        if(h==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法读取进程列表");
        try{
            var e=new Entry{Size=(uint)Marshal.SizeOf(typeof(Entry))};
            if(!Process32FirstW(h,ref e))throw new Win32Exception(Marshal.GetLastWin32Error());
            do{result.Add(new KeyValuePair<int,string>((int)e.Id,e.Exe));}while(Process32NextW(h,ref e));
            int error=Marshal.GetLastWin32Error();if(error!=18)throw new Win32Exception(error,"进程列表读取不完整");
        }finally{CloseHandle(h);}return result;
    }
    internal static int[] Named(string executable){var result=new List<int>();foreach(var entry in Snapshot())if(String.Equals(entry.Value,executable,StringComparison.OrdinalIgnoreCase))result.Add(entry.Key);return result.ToArray();}
    internal static string Diagnostic(){var b=new StringBuilder();var snapshot=Snapshot();b.AppendLine("SnapshotCount="+snapshot.Count);foreach(var entry in snapshot)if(entry.Value.IndexOf("wei",StringComparison.OrdinalIgnoreCase)>=0)b.AppendLine("Snapshot="+entry.Key+" Name="+entry.Value);using(var current=System.Diagnostics.Process.GetCurrentProcess())b.AppendLine("ControllerPID="+current.Id+" Session="+current.SessionId+" OS="+Environment.OSVersion+" CLR="+Environment.Version);return b.ToString();}
    internal static int[] Weixin(){return Named("Weixin.exe");}
    internal static string ImagePath(int pid){
        var h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        try{uint size=32768;var path=new StringBuilder((int)size);if(!QueryFullProcessImageNameW(h,0,path,ref size))throw new Win32Exception(Marshal.GetLastWin32Error());return path.ToString();}finally{CloseHandle(h);}
    }
    internal static bool HasModule(int pid,string path){
        string error;bool found=TryModule(pid,path,out error);if(error!=null)throw new System.IO.IOException(error);return found;
    }
    internal static bool TryModule(int pid,string path,out string error){
        error=null;IntPtr h=new IntPtr(-1);
        for(int attempt=0;attempt<4;attempt++){
            h=CreateToolhelp32Snapshot(0x18,(uint)pid);if(h!=new IntPtr(-1))break;
            int code=Marshal.GetLastWin32Error();if(code==87)return false;
            if(code!=24||attempt==3){error="无法检查微信进程 "+pid+" 的主题模块（Windows 错误 "+code+"）。";return false;}
            Thread.Sleep(20);
        }
        try{
            var e=new Module{Size=(uint)Marshal.SizeOf(typeof(Module))};
            if(Module32FirstW(h,ref e))do{if(String.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase))return true;}while(Module32NextW(h,ref e));
            int code=Marshal.GetLastWin32Error();if(code!=18&&code!=87)error="无法完整检查微信进程 "+pid+" 的主题模块（Windows 错误 "+code+"）。";
            return false;
        }finally{CloseHandle(h);}
    }
}
