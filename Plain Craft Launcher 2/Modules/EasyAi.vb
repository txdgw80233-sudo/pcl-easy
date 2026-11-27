Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq

''' <summary>
''' 本地小模型答疑：自带 llama.cpp 运行库 + Qwen2.5 小模型，全部离线运行。
''' </summary>
Public Module EasyAi

    Public Const AiPort As Integer = 18123
    Public Const RuntimeUrl As String = "https://gh-proxy.com/https://github.com/ggml-org/llama.cpp/releases/download/b12809/llama-b12809-bin-win-cpu-x64.zip"
    Public Const ModelUrlLow As String = "https://modelscope.cn/models/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/master/qwen2.5-0.5b-instruct-q4_k_m.gguf"
    Public Const ModelUrlStd As String = "https://modelscope.cn/models/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/master/qwen2.5-1.5b-instruct-q4_k_m.gguf"

    Private ServerProcess As Process = Nothing
    Private GuardTimer As Timer = Nothing
    Private GuardStarted As Boolean = False

    Public ReadOnly Property AiFolder As String
        Get
            Return Paths.Base & "AI\"
        End Get
    End Property

    Public ReadOnly Property RuntimeExe As String
        Get
            Return AiFolder & "llama-server.exe"
        End Get
    End Property

    Public Function SelectedModel() As Integer
        If Environment.GetEnvironmentVariable("PCL_AI_SMALL") = "1" Then Return 0
        Try
            Dim Chosen As Integer = Settings.Get(Of Integer)("EasyAiModel")
            If Chosen = 0 OrElse Chosen = 1 Then Return Chosen
        Catch ex As Exception
        End Try
        Return RecommendedModel()
    End Function

    Public Function RecommendedModel() As Integer
        Try
            Return If(My.Computer.Info.TotalPhysicalMemory < 8L * 1024 * 1024 * 1024, 0, 1)
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Public Function UseSmallModel() As Boolean
        Return SelectedModel() = 0
    End Function

    Public ReadOnly Property ModelFileName As String
        Get
            Return If(UseSmallModel(), "qwen2.5-0.5b-instruct-q4_k_m.gguf", "qwen2.5-1.5b-instruct-q4_k_m.gguf")
        End Get
    End Property

    Public ReadOnly Property ModelPath As String
        Get
            Return AiFolder & ModelFileName
        End Get
    End Property

    Public ReadOnly Property ModelUrl As String
        Get
            Return If(UseSmallModel(), ModelUrlLow, ModelUrlStd)
        End Get
    End Property

    Public Function RuntimeReady() As Boolean
        Return File.Exists(RuntimeExe)
    End Function

    Public Function ModelReady() As Boolean
        Try
            Return File.Exists(ModelPath) AndAlso New FileInfo(ModelPath).Length > 100L * 1024 * 1024
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 下载文件，回调 (已下载字节, 总字节)。
    ''' </summary>
    Public Sub DownloadFile(Url As String, Target As String, OnProgress As Action(Of Long, Long))
        Directory.CreateDirectory(IO.Path.GetDirectoryName(Target))
        Dim Temp = Target & ".part"
        If File.Exists(Temp) Then File.Delete(Temp)
        Net.ServicePointManager.SecurityProtocol = Net.SecurityProtocolType.Tls12 Or Net.SecurityProtocolType.Tls11
        Dim Request = CType(WebRequest.Create(Url), HttpWebRequest)
        Request.Proxy = Nothing
        Request.UserAgent = "Mozilla/5.0 PCL2/" & VersionBaseName
        Request.Timeout = 30000
        Request.ReadWriteTimeout = 60000
        Request.AllowAutoRedirect = True
        Using Response = Request.GetResponse()
            Dim Total As Long = Response.ContentLength
            Using Stream = Response.GetResponseStream()
                Using Output = New FileStream(Temp, FileMode.Create, FileAccess.Write, FileShare.None)
                    Dim Buffer(1024 * 1024 - 1) As Byte
                    Dim Read As Integer
                    Dim Done As Long = 0
                    Do
                        Read = Stream.Read(Buffer, 0, Buffer.Length)
                        If Read <= 0 Then Exit Do
                        Output.Write(Buffer, 0, Read)
                        Done += Read
                        If OnProgress IsNot Nothing Then OnProgress(Done, Total)
                        If Done Mod (5 * 1024 * 1024) < 1024 * 1024 Then Logger.Info($"[AI] 下载 {IO.Path.GetFileName(Target)}: {Done \ 1024 \ 1024} MB")
                    Loop
                End Using
            End Using
        End Using
        If File.Exists(Target) Then File.Delete(Target)
        File.Move(Temp, Target)
    End Sub

    Public Sub InstallRuntime(OnProgress As Action(Of Long, Long))
        Dim ZipFilePath As String = AiFolder & "runtime.zip"
        DownloadFile(RuntimeUrl, ZipFilePath, OnProgress)
        IO.Compression.ZipFile.ExtractToDirectory(ZipFilePath, AiFolder)
        File.Delete(ZipFilePath)
        '有的版本把 exe 放在 bin 子目录里
        If Not File.Exists(RuntimeExe) Then
            Dim Found = IO.Directory.GetFiles(AiFolder, "llama-server.exe", SearchOption.AllDirectories).FirstOrDefault()
            If Found IsNot Nothing Then File.Copy(Found, RuntimeExe, True)
        End If
        If Not File.Exists(RuntimeExe) Then Throw New Exception("运行库解压后没有找到 llama-server.exe")
    End Sub

    Public Sub InstallModel(OnProgress As Action(Of Long, Long))
        DownloadFile(ModelUrl, ModelPath, OnProgress)
    End Sub

    Public Function IsGameRunning() As Boolean
        Try
            For Each P As Process In Process.GetProcesses()
                Dim Name As String = P.ProcessName.ToLower
                If Name = "java" OrElse Name = "javaw" Then
                    If P.WorkingSet64 > 300L * 1024 * 1024 Then Return True
                End If
            Next
        Catch ex As Exception
        End Try
        Return False
    End Function

    Public Function ServerRunning() As Boolean
        Try
            Return ServerProcess IsNot Nothing AndAlso Not ServerProcess.HasExited
        Catch ex As Exception
            Return False
        End Try
    End Function

    Public Sub StartServer()
        If ServerRunning() Then Return
        If Not RuntimeReady() Then Throw New Exception("还没有下载 AI 运行库")
        If Not ModelReady() Then Throw New Exception("还没有下载 AI 模型")
        StopServer()
        Dim Info As New ProcessStartInfo(RuntimeExe) With {
            .WorkingDirectory = AiFolder,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }
        Info.Arguments = "-m " & Chr(34) & ModelPath & Chr(34) & " -c 4096 -t " &
                        Math.Max(2, Environment.ProcessorCount \ 2) & " --host 127.0.0.1 --port " & AiPort & " --no-warmup"
        ServerProcess = Process.Start(Info)
        AddHandler ServerProcess.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then Logger.Info("[AI] " & e.Data)
        AddHandler ServerProcess.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then Logger.Info("[AI] " & e.Data)
        ServerProcess.BeginOutputReadLine()
        ServerProcess.BeginErrorReadLine()
        '等待端口就绪
        Dim WaitUntil = Now.AddSeconds(120)
        Do
            If ServerProcess.HasExited Then Throw New Exception("AI 服务启动失败（退出码 " & ServerProcess.ExitCode & "）")
            Try
                Using Client As New Net.Sockets.TcpClient()
                    Client.Connect("127.0.0.1", AiPort)
                    Return
                End Using
            Catch ex As Exception
            End Try
            Thread.Sleep(500)
        Loop Until Now > WaitUntil
        StopServer()
        Throw New Exception("AI 服务启动超时")
    End Sub

    Public Sub StopServer()
        Try
            If ServerProcess IsNot Nothing AndAlso Not ServerProcess.HasExited Then
                ServerProcess.Kill()
                ServerProcess.WaitForExit(3000)
            End If
        Catch ex As Exception
        End Try
        ServerProcess = Nothing
    End Sub

    ''' <summary>
    ''' 与模型对话，逐段回调增量文本。messages 为 {"role","content"} 数组。
    ''' </summary>
    Public Function AskAsync(Messages As JArray, OnDelta As Action(Of String), Optional Cancel As CancellationToken = Nothing) As Task(Of String)
        Return Task.Run(
            Function()
                Dim Request = CType(WebRequest.Create($"http://127.0.0.1:{AiPort}/v1/chat/completions"), HttpWebRequest)
                Request.Method = "POST"
                Request.ContentType = "application/json"
                Request.Timeout = 120000
                Request.ReadWriteTimeout = 120000
                Dim BodyJson As New JObject()
                BodyJson.Add("messages", Messages)
                BodyJson.Add("stream", True)
                BodyJson.Add("temperature", 0.3)
                BodyJson.Add("top_p", 0.9)
                BodyJson.Add("repeat_penalty", 1.1)
                BodyJson.Add("max_tokens", 800)
                Dim Body As String = BodyJson.ToString()
                Dim Bytes As Byte() = Encoding.UTF8.GetBytes(Body)
                Request.ContentLength = Bytes.Length
                Using Input = Request.GetRequestStream()
                    Input.Write(Bytes, 0, Bytes.Length)
                End Using
                Dim Builder As New StringBuilder()
                Using Response = Request.GetResponse()
                    Using Stream = Response.GetResponseStream()
                        Using Reader As New StreamReader(Stream, Encoding.UTF8)
                            Do
                                If Cancel.IsCancellationRequested Then Exit Do
                                Dim Line As String = Reader.ReadLine()
                                If Line Is Nothing Then Exit Do
                                If Not Line.StartsWith("data:") Then Continue Do
                                Dim Data As String = Line.Substring(5).Trim()
                                If Data = "[DONE]" Then Exit Do
                                Try
                                    Dim Json = JObject.Parse(Data)
                                    Dim Delta = Json("choices")?(0)?("delta")?("content")?.ToString()
                                    If Not String.IsNullOrEmpty(Delta) Then
                                        Builder.Append(Delta)
                                        If OnDelta IsNot Nothing Then OnDelta(Delta)
                                    End If
                                Catch ex As Exception
                                End Try
                            Loop
                        End Using
                    End Using
                End Using
                Return Builder.ToString()
            End Function, Cancel)
    End Function

    ''' <summary>
    ''' 游戏在跑的时候把 AI 服务关掉，免得抢内存。
    ''' </summary>
    Public Sub StartGuard()
        If GuardStarted Then Return
        GuardStarted = True
        GuardTimer = New Timer(
            Sub(state)
                Try
                    If IsGameRunning() AndAlso ServerRunning() Then
                        Dim StopOnGame As Boolean = True
                        Try
                            StopOnGame = Settings.Get(Of Boolean)("EasyAiStopOnGame")
                        Catch ex As Exception
                        End Try
                        If StopOnGame Then
                            Logger.Info("检测到游戏正在运行，停止本地 AI 服务")
                            StopServer()
                        End If
                    End If
                Catch ex As Exception
                End Try
            End Sub, Nothing, 3000, 3000)
    End Sub

    Public Function BuildSystemPrompt(InstanceInfo As String) As String
        Return "你是「PCL 小白修改版」启动器里内置的答疑助手，服务对象是电脑新手。" & vbCrLf &
            "回答规则：" & vbCrLf &
            "1. 用最简单的中文，不堆术语；必须用术语时先用一句话解释。" & vbCrLf &
            "2. 分步骤给操作路径，例如「设置 → 其他 → 百宝箱 → 清理内存」。" & vbCrLf &
            "3. 涉及删除文件、改配置、下载 Mod 时，先提醒备份存档。" & vbCrLf &
            "4. 不确定就直说不知道，不要编造菜单和按钮；你可以建议用户点右上角「导出诊断包」。" & vbCrLf &
            "5. 回答控制在 300 字内，先给结论再给步骤。" & vbCrLf & vbCrLf &
            "当前环境：" & InstanceInfo & vbCrLf & vbCrLf &
            "启动器已知信息：" & vbCrLf &
            "- 页面：启动（正版/离线登录、启动游戏、版本选择、版本设置）、下载（一键安装：原版/加模组→Fabric/Forge、优化 Mod、光影）、设置（启动/个性化/其他）、更多（帮助/关于/百宝箱/反馈）。" & vbCrLf &
            "- 一键安装会自动选最新正式版；装完可以一键装 Sodium/Lithium/Fabric API 优化 Mod、一键开光影（Iris+光影包）。" & vbCrLf &
            "- 光影对配置要求高，普通笔记本/无独显不建议装（设置页有提示）。" & vbCrLf &
            "- 版本设置 → 设置 → 快捷设置：可改亮度/渲染距离/帧率/粒子/界面缩放/灵敏度/视野/视角晃动/自动跳跃/画质，写进该版本的 options.txt，下次启动生效。" & vbCrLf &
            "- 设置 → 其他 → 百宝箱：打开游戏文件夹、备份存档到桌面、清理日志、导出诊断包、清理内存。" & vbCrLf &
            "- 记住：启动器不提供游戏文件下载以外的服务，也不支持盗版联机；报错时先看「导出诊断包」里的日志。"
    End Function

End Module
