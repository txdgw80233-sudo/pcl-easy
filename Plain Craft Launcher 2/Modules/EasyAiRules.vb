Imports System.Collections.Generic
Imports System.Text
Imports Newtonsoft.Json.Linq
Imports System.IO

''' <summary>
''' 内置的“报错 → 人话解释 + 修复步骤”规则库。命中就直接回答，不走本地模型。
''' </summary>
Public Module EasyAiRules

    Public Class AiRule
        Public Title As String
        Public Keys As String()
        Public Answer As String
    End Class

    ''' <summary>
    ''' 规则文件位置：启动器目录\rules\ai_rules.json，可以用记事本改，改完重开 AI 窗口生效。
    ''' </summary>
    Public ReadOnly Property RulesFilePath As String
        Get
            Return IO.Path.Combine(Paths.Base, "rules\ai_rules.json")
        End Get
    End Property

    Private Loaded As List(Of AiRule) = BuildRules()
    Private LoadedSource As String = "内置"

    ''' <summary>
    ''' 当前规则的来源：外置 JSON 或内置。
    ''' </summary>
    Public ReadOnly Property RulesSource As String
        Get
            Return LoadedSource
        End Get
    End Property

    ''' <summary>
    ''' 如果还没有规则文件，就把内置的 50 条导出成 JSON，方便用户自己改。
    ''' </summary>
    Public Sub EnsureRulesFile()
        Try
            Dim Folder As String = IO.Path.GetDirectoryName(RulesFilePath)
            If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
            If IO.File.Exists(RulesFilePath) Then Exit Sub
            IO.File.WriteAllText(RulesFilePath, BuildJson(BuildRules()), New UTF8Encoding(True))
            Logger.Info("[AI] 已生成外置规则文件：" & RulesFilePath)
        Catch ex As Exception
            Logger.Warn("[AI] 生成规则文件失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 重新读取外置规则；文件不存在或格式错就退回内置规则。
    ''' </summary>
    Public Sub ReloadRules()
        Try
            If Not IO.File.Exists(RulesFilePath) Then
                Loaded = BuildRules()
                LoadedSource = "内置"
                Logger.Info("[AI] 未找到外置规则，使用内置 " & Loaded.Count & " 条")
                Exit Sub
            End If
            Dim Text As String = IO.File.ReadAllText(RulesFilePath, Encoding.UTF8)
            Dim Root = JObject.Parse(Text)
            Dim Arr = Root("rules")
            If Arr Is Nothing OrElse Arr.Type <> JTokenType.Array Then Throw New Exception("缺少 rules 数组")
            Dim List As New List(Of AiRule)
            For Each Item As JObject In Arr
                Dim Rule As New AiRule With {
                    .Title = If(Item("title") Is Nothing, "", Item("title").ToString()),
                    .Answer = NormalizeAnswer(If(Item("answer") Is Nothing, "", Item("answer").ToString()))
                }
                Dim Keys As New List(Of String)
                If Item("keys") IsNot Nothing Then
                    For Each K As JValue In Item("keys")
                        If K.Value IsNot Nothing Then Keys.Add(K.Value.ToString())
                    Next
                End If
                Rule.Keys = Keys.ToArray()
                If Rule.Keys.Length > 0 AndAlso Rule.Answer <> "" Then List.Add(Rule)
            Next
            If List.Count = 0 Then Throw New Exception("规则数组是空的")
            Loaded = List
            LoadedSource = "外置"
            Logger.Info("[AI] 已加载外置规则 " & List.Count & " 条：" & RulesFilePath)
        Catch ex As Exception
            Loaded = BuildRules()
            LoadedSource = "内置"
            Logger.Warn("[AI] 外置规则读取失败，改用内置：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 按关键词匹配，返回第一条命中的规则；没有命中返回 Nothing。
    ''' </summary>
    Public Function MatchRule(Text As String) As AiRule
        If String.IsNullOrWhiteSpace(Text) Then Return Nothing
        For Each R As AiRule In Loaded
            For Each K As String In R.Keys
                If Text.IndexOf(K, StringComparison.OrdinalIgnoreCase) >= 0 Then Return R
            Next
        Next
        Return Nothing
    End Function

    Public Function RuleCount() As Integer
        Return Loaded.Count
    End Function

    Private Function NormalizeAnswer(Text As String) As String
        Return Text.Replace(vbCrLf, vbLf).Replace("\n", vbLf).Replace(vbLf, vbCrLf)
    End Function

    Private Function BuildJson(List As List(Of AiRule)) As String
        Dim Root As New JObject()
        Root("version") = 1
        Dim Arr As New JArray()
        For Each R As AiRule In List
            Dim Item As New JObject()
            Item("title") = R.Title
            Item("keys") = New JArray(R.Keys)
            Item("answer") = R.Answer
            Arr.Add(Item)
        Next
        Root("rules") = Arr
        Return Root.ToString()
    End Function

    Private Function BuildRules() As List(Of AiRule)
        Dim L As New List(Of AiRule)

        L.Add(New AiRule With {
            .Title = "微软登录参数缺失（invalid_client）",
            .Keys = {"invalid_client", "client_id", "client id", "AADSTS70002"},
            .Answer = "结论：启动器没有配置好微软登录用的「应用 ID（Client ID）」，不是你的网络或账号问题，换 VPN 也没用。" & vbCrLf &
                      "1. 确认启动器目录里已经配好 Client ID（这是打包时就该带的，普通用户不用自己填）。" & vbCrLf &
                      "2. 如果自己申请过：到 Microsoft Entra 管理中心 → 应用注册 → 你的应用 → 身份验证，把「允许公共客户端流」设为「是」，并确认「支持的账户类型」包含个人 Microsoft 账户。" & vbCrLf &
                      "3. 改完保存后，关掉启动器重开，再点「正版」登录。" & vbCrLf &
                      "4. 还不行点「导出诊断包」发日志（日志里能看到第 4 步 403 的具体原因）。"
        })

        L.Add(New AiRule With {
            .Title = "微软应用未通过 Mojang 审核（Invalid app registration）",
            .Keys = {"Invalid app registration", "login_with_xbox", "AppRegInfo"},
            .Answer = "结论：启动器用的微软应用还没在 Mojang 那边通过审核，正版登录暂时用不了，和你的电脑、网络无关。" & vbCrLf &
                      "1. 先改用「离线登录」进游戏（离线也能正常玩单人、装 Mod、光影）。" & vbCrLf &
                      "2. 等启动器作者把应用 ID 提交审核并收到通过通知后，再点「正版」登录。" & vbCrLf &
                      "3. 需要催进度的话，把这个问题反馈给启动器作者，附上「导出诊断包」。"
        })

        L.Add(New AiRule With {
            .Title = "注册表写入失败（PCLDebug）",
            .Keys = {"写入注册表失败", "PCLDebug", "HKEY_CURRENT_USER\Software"},
            .Answer = "结论：只是一条提示，不影响玩游戏。启动器想把自己的偏好写进注册表，但被系统/安全软件挡了。" & vbCrLf &
                      "1. 可以忽略，直接启动游戏试试。" & vbCrLf &
                      "2. 如果设置每次都被重置：把启动器挪到 D 盘，或允许它在「用户目录」写入。" & vbCrLf &
                      "3. 不要为了这条提示去改注册表权限，风险大且没必要。"
        })

        L.Add(New AiRule With {
            .Title = "没有权限写游戏文件夹（访问被拒绝 / 程序权限不足）",
            .Keys = {"程序权限不足", "Access is denied", "对路径", "拒绝访问"},
            .Answer = "结论：启动器没有写入游戏文件夹的权限，最常见是被杀毒软件拦了，或者游戏目录在 C 盘根目录这类受保护位置。" & vbCrLf &
                      "1. 先「备份存档到桌面」（设置 → 其他 → 百宝箱）。" & vbCrLf &
                      "2. 临时关掉 360、火绒、电脑管家的「防护」，再重新安装一次。" & vbCrLf &
                      "3. 还不行就把整个启动器文件夹挪到 D 盘（例如 D:\PCL），重开再装；游戏目录也别放 C 盘根目录。" & vbCrLf &
                      "4. 仍然失败，点「导出诊断包」，把日志发出来。"
        })


        L.Add(New AiRule With {
            .Title = "Mod 冲突导致启动失败（NEG_HARD_DEP / breaks）",
            .Keys = {"NEG_HARD_DEP", "breaks", "MOD Resolution", "Incompatible mod set"},
            .Answer = "结论：有两个 Mod 版本互相冲突（比如 Sodium 声明不兼容你装的 Iris 版本），游戏为了安全直接不启动。" & vbCrLf &
                      "1. 先「备份存档到桌面」。" & vbCrLf &
                      "2. 百宝箱 → 打开游戏文件夹 → 进入 mods 文件夹。" & vbCrLf &
                      "3. 按日志里 breaks 后面那个名字删：日志写「sodium … breaks iris」就删 iris 开头的 .jar（保留 sodium）。" & vbCrLf &
                      "4. 回启动器再启动；想用光影就换一个和当前游戏版本匹配的光影/加载器组合。" & vbCrLf &
                      "5. 不确定删哪个，点「导出诊断包」发日志。"
        })

        L.Add(New AiRule With {
            .Title = "Java 版本不对 / 找不到 Java",
            .Keys = {"UnsupportedClassVersionError", "找不到 Java", "No Java", "Java not found", "需要 Java", "java.lang.UnsupportedClassVersion"},
            .Answer = "结论：这个游戏版本需要的 Java 和电脑上现有的不一致（新版本要 Java 21/25，旧版本要 Java 8）。" & vbCrLf &
                      "1. 打开 设置 → 启动 → Java，选「自动选择」，让启动器自己挑。" & vbCrLf &
                      "2. 版本设置 → 设置 → 启动选项 → Java，确认没被手动固定成旧版本。" & vbCrLf &
                      "3. 还不行就点「补全文件」或重新安装该版本。"
        })

        L.Add(New AiRule With {
            .Title = "内存不足（OutOfMemoryError）",
            .Keys = {"OutOfMemoryError", "Java heap space", "内存不足", "GC overhead"},
            .Answer = "结论：分配给游戏的运行内存不够，尤其是装了很多 Mod 或高清材质时。" & vbCrLf &
                      "1. 版本设置 → 设置 → 游戏内存，改成「自定义」，调到物理内存的一半左右（8G 电脑给 4G，16G 给 6~8G）。" & vbCrLf &
                      "2. 关掉浏览器等吃内存的程序。" & vbCrLf &
                      "3. 还不行就点「清理内存」（设置 → 其他 → 百宝箱）再启动。"
        })

        L.Add(New AiRule With {
            .Title = "下载失败 / 连接超时",
            .Keys = {"下载失败", "连接超时", "timed out", "Timeout", "网络环境不佳", "无法连接"},
            .Answer = "结论：下载时网络断了或太慢，常见于国内直连官方源。" & vbCrLf &
                      "1. 设置 → 启动 → 文件下载源，改成「优先使用镜像源」。" & vbCrLf &
                      "2. 关掉 VPN/加速器（它们有时反而让官方源连不上），再重试。" & vbCrLf &
                      "3. 已经下了一半的会自动续传，不用删掉重来。" & vbCrLf &
                      "4. 多次失败就换一个时间段再试，或点「导出诊断包」看具体是哪个地址超时。"
        })

        L.Add(New AiRule With {
            .Title = "文件损坏 / 缺文件",
            .Keys = {"文件损坏", "corrupt", "缺少文件", "补全文件", "Hash 校验失败", "下载的文件不完整"},
            .Answer = "结论：游戏文件有一块坏了或没下全，启动器校验能查出来。" & vbCrLf &
                      "1. 版本设置 → 概览 → 高级管理 → 「补全文件」。" & vbCrLf &
                      "2. 等它把缺的重新下完，再启动。" & vbCrLf &
                      "3. 反复损坏就检查硬盘空间和杀毒软件（有些杀软会删游戏文件）。"
        })

        L.Add(New AiRule With {
            .Title = "游戏闪退（没有明显报错）",
            .Keys = {"闪退", "启动就退出", "自动退出", "exit code", "崩溃"},
            .Answer = "结论：游戏启动过程中断，原因要看日志，最常见是 Mod 冲突、Java 版本不对、内存不足、显卡驱动太旧。" & vbCrLf &
                      "1. 先看启动器弹出的「崩溃分析」，它会直接给原因。" & vbCrLf &
                      "2. 没弹就把 mods 文件夹先整个移走（备份），只跑原版看能不能进。" & vbCrLf &
                      "3. 能进说明是 Mod 问题，再一半一半加回去排查。" & vbCrLf &
                      "4. 还闪退就点「导出诊断包」发日志。"
        })

        L.Add(New AiRule With {
            .Title = "OptiFine 和 Fabric 混装冲突",
            .Keys = {"OptiFine 和 Fabric", "optifine 冲突", "OptiFine 混装", "混合安装", "OptiFine 一起装"},
            .Answer = "结论：OptiFine 和 Fabric 一起装容易冲突，尤其是新版本。" & vbCrLf &
                      "1. 只用一种：要光影就用 Iris（Fabric 方案），别再加 OptiFine。" & vbCrLf &
                      "2. 已经混装了：把 OptiFine 的 jar 从 mods 里移走。" & vbCrLf &
                      "3. 想省事就用「一键开光影」，它用的是 Iris。" 
        })

        L.Add(New AiRule With {
            .Title = "光影装不上 / 开了光影就崩",
            .Keys = {"光影", "Iris", "OptiFine", "shader", "shaderpack"},
            .Answer = "结论：光影要先装支持光影的加载器（Iris / OptiFine），而且对显卡要求高。" & vbCrLf &
                      "1. 光影必须配 Iris 或 OptiFine，并且和游戏版本对得上。" & vbCrLf &
                      "2. 集显/老笔记本建议直接不装，会卡到没法玩。" & vbCrLf &
                      "3. 已经装了但进游戏崩：把光影包从 shaderpacks 里移走，先确认原版能进。" & vbCrLf &
                      "4. 想省事就用「下载 → 一键安装 → 一键开光影」，它会自动处理版本兼容。"
        })

        L.Add(New AiRule With {
            .Title = "版本隔离 / 存档和 Mod 混在一起",
            .Keys = {"版本隔离", "存档不见了", "Mod 跑到别的版本", "存档串了"},
            .Answer = "结论：多个版本共用一个 .minecraft 时，Mod 和存档会互相影响，建议开版本隔离。" & vbCrLf &
                      "1. 设置 → 启动 → 默认版本隔离，选「开启」。" & vbCrLf &
                      "2. 之后每个版本有自己的 mods、saves、resourcepacks。" & vbCrLf &
                      "3. 想找回旧存档：百宝箱 → 打开游戏文件夹，到 .minecraft/saves 里翻。"
        })

        L.Add(New AiRule With {
            .Title = "登录状态异常（state_mismatch / 401）",
            .Keys = {"state_mismatch", "401", "Unauthorized", "令牌", "token", "需要重新登录"},
            .Answer = "结论：登录令牌过期或和当前应用对不上，需要重新登录一次。" & vbCrLf &
                      "1. 启动页点「正版」，重新走一遍登录流程。" & vbCrLf &
                      "2. 如果一直失败，先关掉 VPN/加速器再试。" & vbCrLf &
                      "3. 还不行就用「离线登录」先进游戏，等正版登录修好再用。"
        })

        L.Add(New AiRule With {
            .Title = "整合包装完打不开 / 缺前置",
            .Keys = {"缺少前置", "requires", "missing dependency", "整合包", "依赖"},
            .Answer = "结论：整合包里的某个 Mod 需要另一个前置 Mod，但没装全。" & vbCrLf &
                      "1. 用启动器重新导入整合包，让它按清单把依赖装全。" & vbCrLf &
                      "2. 手动加的 Mod 要看它的页面说明，把 required 的前置一起下。" & vbCrLf &
                      "3. 装之前先「备份存档到桌面」。"
        })

        L.Add(New AiRule With {
            .Title = "启动器被杀毒软件误报 / 文件被删",
            .Keys = {"病毒", "误报", "被删除", "隔离", "Trojan", "安全软件"},
            .Answer = "结论：启动器没签名，容易被 360/Windows Defender 误判。" & vbCrLf &
                      "1. 在杀软里把启动器整个文件夹加入「信任/白名单」。" & vbCrLf &
                      "2. 从隔离区恢复被删的文件，或重新解压一份启动器。" & vbCrLf &
                      "3. 只从作者给的地址下载，别用来路不明的「增强版」。"
        })

        L.Add(New AiRule With {
            .Title = "显卡/OpenGL 太旧",
            .Keys = {"OpenGL", "显卡驱动", "GLFW", "Pixel format", "显卡"},
            .Answer = "结论：显卡驱动太旧或显卡不支持游戏要求的 OpenGL 版本。" & vbCrLf &
                      "1. 去显卡官网（NVIDIA/AMD/Intel）更新驱动，别用系统自带的。" & vbCrLf &
                      "2. 笔记本要确认游戏用的是独显而不是核显。" & vbCrLf &
                      "3. 驱动更新后重启电脑再试。"
        })

        L.Add(New AiRule With {
            .Title = "磁盘空间不足",
            .Keys = {"空间不足", "磁盘已满", "No space", "not enough space"},
            .Answer = "结论：装游戏/整合包需要好几个 GB，磁盘满了。" & vbCrLf &
                      "1. 清理 C 盘，或把游戏目录改到空间大的盘。" & vbCrLf &
                      "2. 百宝箱 → 清理日志，能腾一点是一点。" & vbCrLf &
                      "3. 整合包一般要留 10 GB 以上余量。"
        })

        L.Add(New AiRule With {
            .Title = "启动器打不开 / 无响应",
            .Keys = {"打不开", "无响应", "闪一下就没了", "启动器崩"},
            .Answer = "结论：启动器本身没能正常启动，常见是缺 .NET Framework、被杀软拦、或旧进程没退干净。" & vbCrLf &
                      "1. 任务管理器里结束所有「Plain Craft Launcher」进程，再打开。" & vbCrLf &
                      "2. 确认系统装了 .NET Framework 4.8（Win10/11 一般自带）。" & vbCrLf &
                      "3. 把启动器挪到 D 盘、关掉杀软防护再试。"
        })

        L.Add(New AiRule With {
            .Title = "Win7 / 32 位系统不支持",
            .Keys = {"Win7", "Windows 7", "32 位", "x86"},
            .Answer = "结论：这个启动器和较新的游戏版本都不再支持 Win7 和 32 位系统。" & vbCrLf &
                      "1. 建议升级到 64 位 Win10/11。" & vbCrLf &
                      "2. 实在要用 Win7，只能玩很老的版本，且不保证能用。"
        })

        L.Add(New AiRule With {
            .Title = "存档损坏 / 进世界报错",
            .Keys = {"存档损坏", "level.dat", "世界打不开", "Chunk", "存档"},
            .Answer = "结论：存档文件损坏，常见于游戏崩溃或强制关机时。" & vbCrLf &
                      "1. 先从「备份存档到桌面」里的备份恢复（百宝箱里有）。" & vbCrLf &
                      "2. 没有备份就试游戏自带的「修复/优化世界」。" & vbCrLf &
                      "3. 以后养成定期备份的习惯，百宝箱一键就能备份。"
        })

        L.Add(New AiRule With {
            .Title = "皮肤不显示",
            .Keys = {"皮肤", "skin", "皮肤不显示", "皮肤没变"},
            .Answer = "结论：皮肤分「正版皮肤」和「离线皮肤」，离线登录只能用本地皮肤。" & vbCrLf &
                      "1. 正版登录：皮肤跟着账号走，重启游戏刷新。" & vbCrLf &
                      "2. 离线登录：启动页 → 离线 → 选皮肤文件（PNG，64x64 或 64x32）。" & vbCrLf &
                      "3. 多人服务器里别人看不到你的离线皮肤是正常的。"
        })

        L.Add(New AiRule With {
            .Title = "端口被占用（联机/本地服务）",
            .Keys = {"端口被占用", "port", "Address already in use", "18123", "25565"},
            .Answer = "结论：要用的端口被别的程序占了。" & vbCrLf &
                      "1. 关掉可能占用端口的程序（另一个启动器、旧的服务进程）。" & vbCrLf &
                      "2. 任务管理器结束残留的 java/llama-server 进程。" & vbCrLf &
                      "3. 重开启动器再试。"
        })

        L.Add(New AiRule With {
            .Title = "路径含中文/空格导致异常",
            .Keys = {"中文路径", "路径含", "空格", "乱码"},
            .Answer = "结论：有些老版本游戏/Mod 对中文或空格路径不友好。" & vbCrLf &
                      "1. 把游戏目录换到纯英文路径，例如 D:\MC。" & vbCrLf &
                      "2. 启动器本体也放纯英文路径下。" & vbCrLf &
                      "3. 改完重开启动器。"
        })

        L.Add(New AiRule With {
            .Title = "Mod 装完没效果 / 游戏里看不到",
            .Keys = {"Mod 没效果", "装了没反应", "看不到 Mod", "mod 不生效"},
            .Answer = "结论：Mod 必须装在「对应版本」的 mods 文件夹，并且需要 Fabric/Forge 加载器。" & vbCrLf &
                      "1. 确认启动的是装了加载器的那个版本（名字后面带 Fabric/Forge）。" & vbCrLf &
                      "2. 百宝箱 → 打开游戏文件夹 → mods，确认 .jar 在里面。" & vbCrLf &
                      "3. Mod 的版本要和游戏版本一致，否则会被静默跳过。"
        })

        L.Add(New AiRule With {
            .Title = "光影/材质包放进去了但列表里没有",
            .Keys = {"材质包", "resourcepack", "资源包不显示"},
            .Answer = "结论：文件格式或版本不对，游戏不会列出来。" & vbCrLf &
                      "1. 材质包要是 .zip，不能解压。" & vbCrLf &
                      "2. 放进 resourcepacks 文件夹（百宝箱 → 打开游戏文件夹）。" & vbCrLf &
                      "3. 版本不匹配的材质包会显示为「不兼容」，大部分仍能用。"
        })

        L.Add(New AiRule With {
            .Title = "启动很慢 / 卡在加载界面",
            .Keys = {"启动很慢", "卡在加载", "卡住不动", "Loading"},
            .Answer = "结论：加载慢多半是 Mod 多、硬盘慢（机械盘）、或内存不够。" & vbCrLf &
                      "1. 首次启动本来就慢，第二次会快很多。" & vbCrLf &
                      "2. 把游戏目录放到固态硬盘。" & vbCrLf &
                      "3. 装 Sodium/Lithium 这类优化 Mod（一键安装里有）。"
        })

        L.Add(New AiRule With {
            .Title = "启动器提示需要更新 / 版本过旧",
            .Keys = {"需要更新", "版本过旧", "请更新启动器"},
            .Answer = "结论：启动器版本比服务器要求的老。" & vbCrLf &
                      "1. 到这个启动器的发布页下载新版本压缩包，解压覆盖。" & vbCrLf &
                      "2. 覆盖前先备份 PCL 文件夹里的配置（可先「导出设置」）。" & vbCrLf &
                      "3. 这个修改版不自动更新，需要手动替换。"
        })

        L.Add(New AiRule With {
            .Title = "联机连不上 / 房间进不去",
            .Keys = {"联机", "进不去房间", "连接服务器失败", "Connection refused"},
            .Answer = "结论：联机要双方网络能互通，常见是房主没开好隧道或防火墙拦了。" & vbCrLf &
                      "1. 确认房主的房间还在、没到期。" & vbCrLf &
                      "2. 双方都关掉 VPN/加速器再试。" & vbCrLf &
                      "3. 换用其它联机方式（如第三方联机平台）先保证能进游戏。"
        })

        L.Add(New AiRule With {
            .Title = "内存优化 / 清理内存",
            .Keys = {"清理内存", "内存优化", "内存占用高"},
            .Answer = "结论：可以用启动器自带的清理，把不用的内存还给系统。" & vbCrLf &
                      "1. 设置 → 其他 → 百宝箱 → 「清理内存」。" & vbCrLf &
                      "2. 启动游戏前也会自动做一次（设置 → 启动 里可开关）。" & vbCrLf &
                      "3. 游戏里卡先调低渲染距离和画质。"
        })

        L.Add(New AiRule With {
            .Title = "AI 答疑用不了 / 模型下载慢",
            .Keys = {"AI 用不了", "模型下载", "AI 没反应", "下载模型", "18123"},
            .Answer = "结论：AI 答疑需要先下运行库（约 24 MB）和一个模型（0.5B 约 400 MB / 1.5B 约 1.1 GB），只下一次。" & vbCrLf &
                      "1. 打开 AI 答疑窗口，点「下载并启动」，等进度走完。" & vbCrLf &
                      "2. 下载会自动挑最快的源，失败会换源。" & vbCrLf &
                      "3. 游戏运行时 AI 会自动关闭（默认），这是正常的，能省内存。"
        })

        L.Add(New AiRule With {
            .Title = "提示「现有实例正在以提升的权限运行」",
            .Keys = {"提升的权限运行", "以提升的权限", "已有实例", "另一个实例"},
            .Answer = "结论：已经有一个「以管理员身份运行」的启动器在后台，普通双击打开时就会提示这个。" & vbCrLf &
                      "1. 任务管理器 → 详细信息，结束所有「Plain Craft Launcher 2.exe」进程。" & vbCrLf &
                      "2. 以后正常双击打开即可，不要右键「以管理员身份运行」。" & vbCrLf &
                      "3. 如果结束不掉，重启电脑最省事。"
        })

        L.Add(New AiRule With {
            .Title = "该微软账号没有购买 Minecraft",
            .Keys = {"没有购买", "未拥有", "does not own", "not entitled", "Xbox 帐户"},
            .Answer = "结论：这个微软账号没有买过 Minecraft（或者买在了另一个账号上），正版登录会被拒绝。" & vbCrLf &
                      "1. 确认买游戏的账号和你现在登录的是同一个。" & vbCrLf &
                      "2. 官网 xbox.com 里的「我的游戏」能看到 Minecraft 才算拥有。" & vbCrLf &
                      "3. 暂时可以先用「离线登录」玩单机。"
        })

        L.Add(New AiRule With {
            .Title = "登录服务器连不上（login.live.com / 登录超时）",
            .Keys = {"login.live.com", "登录超时", "无法连接到登录", "登录服务器"},
            .Answer = "结论：启动器连不上微软的登录服务器，通常是网络/代理问题。" & vbCrLf &
                      "1. 关掉 VPN、加速器、代理软件，再点「正版」重试。" & vbCrLf &
                      "2. 换手机热点试一次，能登说明是原来的网络问题。" & vbCrLf &
                      "3. 都不行就先用「离线登录」。"
        })

        L.Add(New AiRule With {
            .Title = "证书 / TLS 错误",
            .Keys = {"证书", "TLS", "SSL", "SecureChannel", "schannel", "无法建立安全连接"},
            .Answer = "结论：系统时间不对，或者代理软件篡改了 HTTPS 证书。" & vbCrLf &
                      "1. 把系统时间自动同步打开（设置 → 时间和语言）。" & vbCrLf &
                      "2. 关掉抓包/代理类软件（Clash、Fiddler 等）再试。" & vbCrLf &
                      "3. 还不行就换网络（手机热点）确认是不是网络劫持。"
        })

        L.Add(New AiRule With {
            .Title = "JVM 参数错误 / 内存分配过大",
            .Keys = {"JVM", "Xmx", "Invalid maximum heap", "无法创建 Java 虚拟机", "参数错误"},
            .Answer = "结论：手动填的 Java 启动参数不对，或者给的内存超过了电脑实际内存。" & vbCrLf &
                      "1. 版本设置 → 设置 → 启动选项 → 自定义 JVM 参数，点「恢复默认」。" & vbCrLf &
                      "2. 版本设置 → 设置 → 游戏内存，改回「自动配置」。" & vbCrLf &
                      "3. 保存后重新启动游戏。"
        })

        L.Add(New AiRule With {
            .Title = "无法创建游戏进程（CreateProcess 失败）",
            .Keys = {"CreateProcess", "无法启动进程", "未能启动 java", "The system cannot find the file"},
            .Answer = "结论：启动器找不到 Java，或者 Java 路径被改动/删除了。" & vbCrLf &
                      "1. 设置 → 启动 → Java，选「自动选择」。" & vbCrLf &
                      "2. 还不行就点「下载 Java」，让启动器装一个。" & vbCrLf &
                      "3. 杀软如果删过 Java 文件，需要重新装 Java。"
        })

        L.Add(New AiRule With {
            .Title = "游戏版本不存在 / 版本文件缺失",
            .Keys = {"版本不存在", "找不到版本", "version json", "版本文件缺失", "无法读取版本"},
            .Answer = "结论：这个版本的文件不完整，或者你删过 versions 里的东西。" & vbCrLf &
                      "1. 版本设置 → 概览 → 高级管理 → 「补全文件」。" & vbCrLf &
                      "2. 补全不了就重新「一键安装」装一次同名版本。" & vbCrLf &
                      "3. 装之前先备份存档。"
        })

        L.Add(New AiRule With {
            .Title = "下载源/镜像连不上",
            .Keys = {"BMCLAPI", "镜像源", "下载源", "源不可用", "镜像连不上"},
            .Answer = "结论：当前选的下载源在你的网络下不通。" & vbCrLf &
                      "1. 设置 → 启动 → 文件下载源，切到「优先使用官方源」或「优先使用镜像源」，两个都试一次。" & vbCrLf &
                      "2. 关掉 VPN/加速器再试。" & vbCrLf &
                      "3. 只是某个版本下不动，可以过一会儿再试。"
        })

        L.Add(New AiRule With {
            .Title = "缺 Fabric API（Mod 要求 fabric-api）",
            .Keys = {"fabric-api", "Fabric API", "requires fabric", "缺少前置"},
            .Answer = "结论：Fabric 的 Mod 大多需要 Fabric API 这个底座，没装就会被跳过或报错。" & vbCrLf &
                      "1. 下载 → 一键安装 → 装好 Fabric 后点「一键装好」，会把 Fabric API 一起装上。" & vbCrLf &
                      "2. 已经装过游戏：把 fabric-api 的 .jar 放进该版本的 mods 文件夹。" & vbCrLf &
                      "3. 注意 Fabric API 的版本要和游戏版本一致。"
        })

        L.Add(New AiRule With {
            .Title = "加载器装错（Forge Mod 装到 Fabric 上）",
            .Keys = {"Forge Mod", "装错加载器", "加载器不匹配", "mod loader mismatch", "fabric 版"},
            .Answer = "结论：Mod 和加载器类型必须一致——Forge 的 Mod 只能配 Forge，Fabric 的只能配 Fabric。" & vbCrLf &
                      "1. 看 Mod 下载页写的「Fabric / Forge / NeoForge」。" & vbCrLf &
                      "2. 装一个对应类型的版本（一键安装里可以选 Fabric 或 Forge）。" & vbCrLf &
                      "3. 把 Mod 放进那个版本的 mods 文件夹。"
        })


        L.Add(New AiRule With {
            .Title = "杀毒软件把游戏文件删了",
            .Keys = {"文件消失", "被删了", "少了一个文件", "杀毒删除", "隔离区"},
            .Answer = "结论：杀软把 .jar 或 exe 当病毒删掉了，这在未签名的启动器上很常见。" & vbCrLf &
                      "1. 到杀软的「隔离区」恢复被删的文件。" & vbCrLf &
                      "2. 把启动器文件夹和游戏目录加入白名单。" & vbCrLf &
                      "3. 用「补全文件」把缺的游戏文件补回来。"
        })

        L.Add(New AiRule With {
            .Title = "资源包 / 光影包 zip 损坏",
            .Keys = {"zip 损坏", "压缩包损坏", "材质包加载失败", "shader 加载失败", "无效的 zip"},
            .Answer = "结论：下载的包不完整或不是标准 zip。" & vbCrLf &
                      "1. 重新下载一次，别用下载工具改文件名。" & vbCrLf &
                      "2. 光影包直接放 shaderpacks，不要解压。" & vbCrLf &
                      "3. 材质包放 resourcepacks，也别解压。"
        })

        L.Add(New AiRule With {
            .Title = "用户名含中文导致异常",
            .Keys = {"中文用户名", "用户名是中文", "C:\Users\中文"},
            .Answer = "结论：有些老游戏/Mod 处理不了中文路径（比如 C:\Users\小明）。" & vbCrLf &
                      "1. 把启动器和游戏目录都放到 D:\MC 这类纯英文路径。" & vbCrLf &
                      "2. 设置 → 启动 里把游戏目录改到新位置。" & vbCrLf &
                      "3. 不要直接改 Windows 用户名，风险大。"
        })

        L.Add(New AiRule With {
            .Title = "无法保存截图 / 日志（目录只读）",
            .Keys = {"无法保存截图", "日志写入失败", "只读", "无法写入文件"},
            .Answer = "结论：启动器所在的文件夹没有写权限（比如放在 C:\Program Files）。" & vbCrLf &
                      "1. 把启动器整个文件夹挪到 D 盘普通目录。" & vbCrLf &
                      "2. 不要放在 Program Files、系统盘根目录。" & vbCrLf &
                      "3. 挪完重开启动器。"
        })

        L.Add(New AiRule With {
            .Title = "显卡驱动崩溃（驱动重置 / GL_OUT_OF_MEMORY）",
            .Keys = {"GL_OUT_OF_MEMORY", "驱动重置", "display driver", "TDR", "显卡崩溃"},
            .Answer = "结论：显卡驱动崩了，常见于驱动太旧或光影太吃显存。" & vbCrLf &
                      "1. 先把光影关掉（选项 → 视频设置 → 光影）。" & vbCrLf &
                      "2. 更新显卡驱动，笔记本确认在用独显。" & vbCrLf &
                      "3. 降低渲染距离/画质再试。"
        })

        L.Add(New AiRule With {
            .Title = "整合包解压失败",
            .Keys = {"解压失败", "解压缩", "压缩包损坏", "无法解压"},
            .Answer = "结论：整合包文件不完整，或者磁盘空间不够。" & vbCrLf &
                      "1. 确认磁盘至少还有整合包 3 倍大小的空间。" & vbCrLf &
                      "2. 重新下载整合包（别用浏览器断点续传的残包）。" & vbCrLf &
                      "3. 还是失败就换个整合包，或联系整合包作者。"
        })

        L.Add(New AiRule With {
            .Title = "版本列表 / 资源列表加载失败",
            .Keys = {"版本列表加载失败", "获取版本列表失败", "资源列表加载", "列表是空的"},
            .Answer = "结论：启动器没连上资源服务器，列表拉不下来。" & vbCrLf &
                      "1. 点右上角的「刷新」按钮重试。" & vbCrLf &
                      "2. 关掉 VPN/代理，或换个下载源。" & vbCrLf &
                      "3. 网络正常还空，点「导出诊断包」看具体报错。"
        })

        L.Add(New AiRule With {
            .Title = "启动器更新失败 / 覆盖后打不开",
            .Keys = {"更新失败", "覆盖后打不开", "新版本打不开", "升级后"},
            .Answer = "结论：覆盖更新时旧文件没清干净，或者被杀软拦了。" & vbCrLf &
                      "1. 关掉启动器，把整个文件夹删掉（先备份存档和 PCL 文件夹）。" & vbCrLf &
                      "2. 重新解压一份新版本，游戏目录不用动。" & vbCrLf &
                      "3. 注意不要只覆盖 exe 一个文件。"
        })

        L.Add(New AiRule With {
            .Title = "游戏能开但没声音",
            .Keys = {"没有声音", "没声音", "声音卡", "音频设备"},
            .Answer = "结论：通常是系统默认音频设备变了，或者游戏里的声音被关。" & vbCrLf &
                      "1. 游戏里 选项 → 音乐和声音，确认不是 0。" & vbCrLf &
                      "2. Windows 声音设置里切换一下默认输出设备。" & vbCrLf &
                      "3. 蓝牙耳机切换设备后，要重进游戏才生效。"
        })

        Return L
    End Function

End Module
