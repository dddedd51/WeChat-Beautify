using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

// Observe the current token only. Never change privileges or escape a restricted host.
internal static class LaunchContext {
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int type,IntPtr buffer,uint size,out uint needed);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
    internal static uint IntegrityRid(){
        IntPtr token;if(!OpenProcessToken(GetCurrentProcess(),8,out token))throw new Win32Exception(Marshal.GetLastWin32Error());
        try{
            uint size;GetTokenInformation(token,25,IntPtr.Zero,0,out size);
            if(size==0||size>65536)throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr buffer=Marshal.AllocHGlobal((int)size);
            try{
                if(!GetTokenInformation(token,25,buffer,size,out size))throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr sid=Marshal.ReadIntPtr(buffer);byte count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                if(count==0)throw new InvalidOperationException("无法读取运行完整性级别。");
                return unchecked((uint)Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)count-1)));
            }finally{Marshal.FreeHGlobal(buffer);}
        }finally{CloseHandle(token);}
    }
    internal static string Problem(){return IntegrityRid()<0x2000?"当前程序或所在目录带有 Windows 低完整性标记，微信无法正常继承此启动环境。请将发布 ZIP 解压到普通下载文件夹中的新目录，再双击「微信美化.exe」。仅在此目录重新打开不会解决问题；无需管理员权限。":null;}
    internal static string Diagnostic(){try{return "IntegrityRID="+IntegrityRid()+"\n";}catch(Exception e){return "IntegrityReadError="+e.Message+"\n";}}
}
