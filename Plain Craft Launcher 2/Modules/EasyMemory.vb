Imports System.Diagnostics
Imports System.Runtime.InteropServices

''' <summary>
''' 内存清理：把 Java / 启动器进程的工作集交还给系统（PCL 自带的「内存优化」在开源版里是空实现，这里补上）。
''' </summary>
Public Module EasyMemory

    <DllImport("psapi.dll", SetLastError:=True)>
    Private Function EmptyWorkingSet(hProcess As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function OpenProcess(dwDesiredAccess As Integer, bInheritHandle As Boolean, dwProcessId As Integer) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function CloseHandle(hObject As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Function GetCurrentProcess() As IntPtr
    End Function

    Private Const PROCESS_SET_QUOTA As Integer = &H100
    Private Const PROCESS_QUERY_INFORMATION As Integer = &H400

    ''' <summary>
    ''' 清理内存。返回给用户看的说明文字。
    ''' </summary>
    Public Function Optimize() As String
        Dim Before As Long = My.Computer.Info.AvailablePhysicalMemory
        Dim BeforeSelf As Long = Process.GetCurrentProcess().WorkingSet64
        Dim TrimmedGame As Integer = 0
        Dim TrimmedSelf As Boolean = False
        Try
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
        Catch ex As Exception
            Logger.Error(ex, "内存清理时回收自身内存失败")
        End Try
        '游戏进程（Java）
        Try
            For Each P As Process In Process.GetProcesses()
                Try
                    Dim Name As String = P.ProcessName.ToLower
                    If Name <> "java" AndAlso Name <> "javaw" Then Continue For
                    If P.WorkingSet64 < 150L * 1024 * 1024 Then Continue For
                    Dim Handle As IntPtr = OpenProcess(PROCESS_SET_QUOTA Or PROCESS_QUERY_INFORMATION, False, P.Id)
                    If Handle = IntPtr.Zero Then Continue For
                    Try
                        If EmptyWorkingSet(Handle) Then TrimmedGame += 1
                    Finally
                        CloseHandle(Handle)
                    End Try
                Catch ex As Exception
                    Logger.Error(ex, "内存清理：处理游戏进程失败")
                End Try
            Next
        Catch ex As Exception
            Logger.Error(ex, "内存清理：枚举进程失败")
        End Try
        '启动器自己
        Try
            TrimmedSelf = EmptyWorkingSet(GetCurrentProcess())
        Catch ex As Exception
            Logger.Error(ex, "内存清理：处理启动器进程失败")
        End Try
        Thread.Sleep(400)
        Dim After As Long = My.Computer.Info.AvailablePhysicalMemory
        Dim Freed As Long = Math.Max(0, After - Before)
        Dim SelfFreed As Long = Math.Max(0, BeforeSelf - Process.GetCurrentProcess().WorkingSet64)
        Dim Text As String = If(Freed >= 1024 * 1024,
            $"已清理 {Math.Round(Freed / 1024 / 1024, 0)} MB 内存",
            $"已整理 {TrimmedGame} 个游戏进程的内存")
        If TrimmedSelf AndAlso SelfFreed > 0 Then Text &= $"，启动器自身释放 {Math.Round(SelfFreed / 1024 / 1024, 0)} MB"
        Logger.Info($"内存清理完成：游戏进程 {TrimmedGame} 个，可用内存 +{Math.Round(Freed / 1024 / 1024, 1)} MB")
        Return Text
    End Function

End Module
