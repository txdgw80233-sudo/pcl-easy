Public Class PageEasyInstall
    Inherits MyPageRight

    Private Enum ModeType
        Vanilla = 0
        Modded = 1
    End Enum

    Private Enum LoaderType
        Fabric = 0
        Forge = 1
    End Enum

    '用户的选择
    Private SelectedMode As ModeType? = Nothing
    Private SelectedLoader As LoaderType? = Nothing

    '版本信息
    Private VanillaName As String = Nothing
    Private VanillaData As JObject = Nothing
    Private FabricVersion As String = Nothing
    Private ForgeEntry As DlForgeVersionEntry = Nothing
    Private ForgeLoader As LoaderTask(Of String, List(Of DlForgeVersionEntry)) = Nothing

    '版本下拉框
    Private Releases As New List(Of JObject)
    Private ReleaseData As New Dictionary(Of String, JObject)

    Private IsHooked As Boolean = False
    Private IsBusy As Boolean = False
    Private InstallDone As Boolean = False

    '安装完成后的优化 Mod 引导
    Private InstalledFolder As String = Nothing
    Private OptimizeBusy As Boolean = False
    Private ReadOnly OptimizeSlugs As String() = {"sodium", "lithium", "fabric-api"}
    Private ReadOnly OptimizeNames As String() = {"Sodium（钠）", "Lithium（锂）", "Fabric API"}

    Public Overridable Sub Init() Handles Me.Loaded
        If Not IsHooked Then
            IsHooked = True
            AddHandler DlClientListLoader.OnStateChangedUi, AddressOf ClientList_StateChanged
            AddHandler DlFabricListLoader.OnStateChangedUi, AddressOf FabricList_StateChanged
        End If
        DlClientListLoader.Start()
        ClientList_Changed(DlClientListLoader)
        DlFabricListLoader.Start()
        FabricList_Changed(DlFabricListLoader)
        UpdateSummary()
    End Sub

    '版本列表
    Private Sub ClientList_StateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        ClientList_Changed(Loader)
    End Sub

    Private Sub ClientList_Changed(Loader As LoaderBase)
        Try
            Select Case Loader.State
                Case LoadState.Loading
                    LabStatus.Text = "正在获取游戏版本列表……"
                    Return
                Case LoadState.Failed
                    LabStatus.Text = "获取版本列表失败，请检查网络后点「刷新」重试。"
                    Return
                Case LoadState.Finished
                Case Else
                    Return
            End Select
            '收集正式版
            Dim Versions = DlClientListLoader.Output.Value("versions")
            If Versions Is Nothing Then Return
            Releases.Clear()
            ReleaseData.Clear()
            For Each Token As JObject In Versions
                If Token("type")?.ToString = "release" AndAlso Token("url") IsNot Nothing Then
                    Dim Id As String = Token("id").ToString
                    Releases.Add(Token)
                    ReleaseData(Id) = Token
                End If
            Next
            If Not Releases.Any() Then
                LabStatus.Text = "没有找到可安装的正式版，请检查网络。"
                Return
            End If
            '填充下拉框（保持已有选择）
            Dim Keep As String = VanillaName
            ComboVersion.Items.Clear()
            For Each Token In Releases
                ComboVersion.Items.Add(New MyComboBoxItem With {.Content = Token("id").ToString})
            Next
            Dim Index As Integer = 0
            If Keep IsNot Nothing Then
                For i = 0 To Releases.Count - 1
                    If Releases(i)("id").ToString = Keep Then Index = i
                Next
            End If
            ComboVersion.SelectedIndex = Index
            LabStatus.Text = "已找到 " & Releases.Count & " 个正式版，默认选最新的 " & VanillaName & "。"
        Catch ex As Exception
            Logger.Error(ex, "新手安装：读取版本列表失败")
        End Try
    End Sub

    'Fabric 版本列表
    Private Sub FabricList_StateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        FabricList_Changed(Loader)
    End Sub

    Private Sub FabricList_Changed(Loader As LoaderBase)
        Try
            If DlFabricListLoader.State <> LoadState.Finished Then Return
            Dim Loaders = TryCast(DlFabricListLoader.Output.Value("loader"), JArray)
            If Loaders Is Nothing OrElse Not Loaders.Any() Then Return
            For Each Token As JObject In Loaders
                If Token("stable")?.ToObject(Of Boolean) = True Then
                    FabricVersion = Token("version").ToString
                    Exit For
                End If
            Next
            If FabricVersion Is Nothing Then FabricVersion = Loaders(0)("version").ToString
            UpdateSummary()
        Catch ex As Exception
            Logger.Error(ex, "新手安装：读取 Fabric 版本失败")
        End Try
    End Sub

    'Forge 版本列表
    Private Sub EnsureForgeLoader()
        If ForgeLoader IsNot Nothing OrElse VanillaName Is Nothing Then Return
        ForgeLoader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion " & VanillaName, AddressOf DlForgeVersionMain)
        AddHandler ForgeLoader.OnStateChangedUi, AddressOf ForgeList_StateChanged
        ForgeLoader.Start(VanillaName)
    End Sub

    Private Sub ForgeList_StateChanged(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
        ForgeList_Changed(Loader)
    End Sub

    Private Sub ForgeList_Changed(Loader As LoaderBase)
        Try
            If ForgeLoader Is Nothing OrElse ForgeLoader.State <> LoadState.Finished Then Return
            Dim Best As DlForgeVersionEntry = Nothing
            For Each Version In ForgeLoader.Output
                If Version.Category = "universal" OrElse Version.Category = "client" Then Continue For
                If Best Is Nothing OrElse Version.CompareTo(Best) > 0 Then Best = Version
            Next
            ForgeEntry = Best
            If Best Is Nothing AndAlso SelectedLoader = LoaderType.Forge Then
                LabStatus.Text = "这个版本暂时没有可用的 Forge，建议改用 Fabric 或换个版本。"
            End If
            UpdateSummary()
        Catch ex As Exception
            Logger.Error(ex, "新手安装：读取 Forge 版本失败")
        End Try
    End Sub

    '交互
    Private Sub Mode_Click(sender As Object, e As MouseButtonEventArgs)
        If sender Is ItemVanilla Then
            SelectedMode = ModeType.Vanilla
        Else
            SelectedMode = ModeType.Modded
        End If
        CardLoader.Visibility = If(SelectedMode = ModeType.Modded, Visibility.Visible, Visibility.Collapsed)
        If SelectedMode = ModeType.Modded AndAlso SelectedLoader Is Nothing Then
            SelectedLoader = LoaderType.Fabric
            ItemFabric.SetChecked(True, True, False)
        End If
        If SelectedLoader = LoaderType.Forge Then EnsureForgeLoader()
        UpdateSummary()
    End Sub

    Private Sub Loader_Click(sender As Object, e As MouseButtonEventArgs)
        If sender Is ItemFabric Then
            SelectedLoader = LoaderType.Fabric
        Else
            SelectedLoader = LoaderType.Forge
            EnsureForgeLoader()
        End If
        UpdateSummary()
    End Sub

    Private Sub UpdateSummary()
        If VanillaName Is Nothing Then
            LabSummary.Text = "正在获取版本列表……"
            BtnInstall.IsEnabled = False
            Return
        End If
        Dim VanillaText As String = "Minecraft " & VanillaName
        Select Case SelectedMode
            Case Nothing
                LabSummary.Text = VanillaText & "｜请先选择上面的玩法"
                BtnInstall.IsEnabled = False
            Case ModeType.Vanilla
                LabSummary.Text = VanillaText & "（原版）"
                BtnInstall.IsEnabled = True
            Case ModeType.Modded
                If SelectedLoader = LoaderType.Fabric Then
                    If FabricVersion IsNot Nothing AndAlso Not IsFabricSupported(VanillaName) Then
                        LabSummary.Text = VanillaText & "｜Fabric 暂不支持这个版本，换个版本或选 Forge 试试"
                        BtnInstall.IsEnabled = False
                        Return
                    End If
                    LabSummary.Text = VanillaText & " + Fabric " & If(FabricVersion, "（获取中……）")
                    BtnInstall.IsEnabled = FabricVersion IsNot Nothing
                Else
                    Dim ForgeText As String = If(ForgeEntry Is Nothing, "（获取中……）", ForgeEntry.VersionName)
                    LabSummary.Text = VanillaText & " + Forge " & ForgeText
                    BtnInstall.IsEnabled = ForgeEntry IsNot Nothing
                End If
        End Select
    End Sub

    '换了游戏版本
    Private Sub ComboVersion_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim Item As MyComboBoxItem = TryCast(ComboVersion.SelectedItem, MyComboBoxItem)
        If Item Is Nothing Then Return
        Dim Name As String = Item.Content.ToString
        If Name = VanillaName Then Return
        VanillaName = Name
        VanillaData = ReleaseData(Name)
        '加载器缓存作废
        ForgeEntry = Nothing
        ForgeLoader = Nothing
        If SelectedLoader = LoaderType.Forge Then EnsureForgeLoader()
        UpdateSummary()
    End Sub

    'Fabric 是否支持这个版本
    Private Function IsFabricSupported(VersionName As String) As Boolean
        Try
            Dim Games = TryCast(DlFabricListLoader.Output.Value("game"), JArray)
            If Games Is Nothing Then Return False
            For Each Token As JObject In Games
                If Token("version")?.ToString = VersionName Then Return True
            Next
        Catch ex As Exception
            Logger.Error(ex, "新手安装：判断 Fabric 兼容性失败")
        End Try
        Return False
    End Function

    '开始安装
    Private Sub BtnInstall_Click(sender As Object, e As MouseButtonEventArgs)
        If IsBusy Then Return
        If InstallDone Then
            '已经装好了，按钮变成启动游戏
            McLaunchStart()
            Return
        End If
        If VanillaName Is Nothing Then
            Hint("版本列表还没加载好，请稍等一下再试。", HintType.Red)
            Return
        End If
        '组织安装请求
        Dim InstanceName As String
        Dim Components As McComponents
        Dim ForgeEntity As DlForgeVersionEntry = Nothing
        Select Case SelectedMode
            Case ModeType.Vanilla
                InstanceName = VanillaName
                Components = New McComponents(minecraftVersion:=VanillaName, optiFineEntry:=Nothing, forgeVersion:=Nothing, neoForgeVersion:=Nothing, fabricVersion:=Nothing, fabricApiVersion:=Nothing, optiFabricVersion:=Nothing, liteLoaderVersion:=Nothing)
            Case ModeType.Modded
                If SelectedLoader = LoaderType.Fabric Then
                    If FabricVersion Is Nothing Then
                        Hint("Fabric 版本还没获取到，请稍等一下再试。", HintType.Red)
                        Return
                    End If
                    InstanceName = VanillaName & "-Fabric"
                    Components = New McComponents(minecraftVersion:=VanillaName, optiFineEntry:=Nothing, forgeVersion:=Nothing, neoForgeVersion:=Nothing, fabricVersion:=FabricVersion, fabricApiVersion:=Nothing, optiFabricVersion:=Nothing, liteLoaderVersion:=Nothing)
                Else
                    If ForgeEntry Is Nothing Then
                        Hint("Forge 版本还没获取到，请稍等一下再试。", HintType.Red)
                        Return
                    End If
                    InstanceName = VanillaName & "-Forge"
                    Components = New McComponents(minecraftVersion:=VanillaName, optiFineEntry:=Nothing, forgeVersion:=ForgeEntry.VersionName, neoForgeVersion:=Nothing, fabricVersion:=Nothing, fabricApiVersion:=Nothing, optiFabricVersion:=Nothing, liteLoaderVersion:=Nothing)
                    ForgeEntity = ForgeEntry
                End If
            Case Else
                Hint("请先选择是玩原版还是加模组。", HintType.Red)
                Return
        End Select
        '重复检查
        Dim Folder As String = McFolderSelected & "versions\" & InstanceName & "\"
        If FileUtils.Exists(Folder & InstanceName & ".json") Then
            MarkInstalled(Folder, "这个版本之前已经装过了，直接点上面的「启动游戏」就能玩。")
            Return
        End If
        '版本隔离：Mod 版本不隔离容易互相污染，这里直接替小白打开
        Dim Indie As Integer = Settings.Get(Of Integer)("LaunchArgumentIndieV2")
        If Indie = 0 OrElse Indie = 2 Then
            Settings.Set("LaunchArgumentIndieV2", 1)
            Logger.Info("新手安装：已自动开启版本隔离")
        End If
        '提交安装
        Dim Request As New McInstallRequest With {
            .NewInstanceName = InstanceName,
            .InstanceFolder = Folder,
            .Components = Components,
            .MinecraftJson = VanillaData("url").ToString,
            .ForgeEntry = ForgeEntity
        }
        IsBusy = True
        Try
            If McInstallStart(Request) Then
                MarkInstalled(Folder, "安装完成！点上面的按钮就能进游戏了。")
            Else
                LabStatus.Text = "安装没能开始，请稍后再试。"
            End If
        Catch ex As Exception
            Logger.Error(ex, "新手安装：启动安装失败")
            Hint("安装启动失败：" & ex.Message, HintType.Red)
        Finally
            IsBusy = False
        End Try
    End Sub

    '版本已经可用：按钮切换成「启动游戏」，并给 Fabric 用户提示优化 Mod
    Private Sub MarkInstalled(Folder As String, Message As String)
        InstallDone = True
        InstalledFolder = Folder
        BtnInstall.Text = " 启动游戏"
        LabStatus.Text = Message
        If SelectedMode = ModeType.Modded AndAlso SelectedLoader = LoaderType.Fabric Then
            CardOptimize.Visibility = Visibility.Visible
            LabOptimizeStatus.Text = "装好这三个，游戏会明显更顺畅。点「一键装好」就行，不用你找文件。"
        End If
    End Sub

    '一键装优化 Mod
    Private Sub BtnOptimize_Click(sender As Object, e As MouseButtonEventArgs)
        If OptimizeBusy Then Return
        If InstalledFolder Is Nothing OrElse VanillaName Is Nothing Then Return
        OptimizeBusy = True
        BtnOptimize.IsEnabled = False
        BtnSkipOptimize.IsEnabled = False
        RunInNewThread(
            Sub()
                Dim SuccessCount As Integer = 0
                Dim FailNames As New List(Of String)
                For i = 0 To OptimizeSlugs.Length - 1
                    Dim Index As Integer = i
                    Dim ModName As String = OptimizeNames(Index)
                    Try
                        RunInUi(Sub() LabOptimizeStatus.Text = "正在下载 " & ModName & "……（" & (Index + 1) & "/" & OptimizeSlugs.Length & "）")
                        Dim Url As String = "https://api.modrinth.com/v2/project/" & OptimizeSlugs(Index) &
                            "/version?game_versions=%5B%22" & VanillaName & "%22%5D&loaders=%5B%22fabric%22%5D"
                        Dim Raw As String = NetRequestByClientRetry(Url, RequireJson:=True)
                        Dim VersionList As JArray = JArray.Parse(Raw)
                        If VersionList.Count = 0 Then Throw New Exception("没有找到适配 " & VanillaName & " 的版本")
                        Dim Files = VersionList(0)("files")
                        If Files Is Nothing OrElse Files.Count = 0 Then Throw New Exception("没有可下载的文件")
                        Dim FileUrl As String = Files(0)("url").ToString
                        Dim FileName As String = Files(0)("filename").ToString
                        Dim TargetDir As String = InstalledFolder & "mods\"
                        If Not FileUtils.Exists(TargetDir & FileName) Then
                            NetDownloadByClient(FileUrl, TargetDir & FileName)
                        End If
                        SuccessCount += 1
                    Catch ex As Exception
                        Logger.Error(ex, "新手安装：下载优化 Mod 失败（" & ModName & "）")
                        FailNames.Add(ModName)
                    End Try
                Next
                RunInUi(
                    Sub()
                        OptimizeBusy = False
                        BtnSkipOptimize.IsEnabled = True
                        If FailNames.Count = 0 Then
                            LabOptimizeStatus.Text = "装好了！点上面的「启动游戏」就能玩，进游戏后应该会明显更顺。"
                            BtnOptimize.Text = " 已装好"
                            BtnSkipOptimize.Visibility = Visibility.Collapsed
                        Else
                            BtnOptimize.IsEnabled = True
                            BtnOptimize.Text = " 再试一次"
                            LabOptimizeStatus.Text = "装好了 " & SuccessCount & " 个；" & String.Join("、", FailNames) &
                                " 没装成，多半是网络问题，可以再点一次重试，也可以先直接进游戏。"
                        End If
                    End Sub)
            End Sub)
    End Sub

    '先不装
    Private Sub BtnSkipOptimize_Click(sender As Object, e As MouseButtonEventArgs)
        CardOptimize.Visibility = Visibility.Collapsed
        LabStatus.Text = "好的，想装的时候随时回来点「一键安装」页就行。"
    End Sub

End Class
