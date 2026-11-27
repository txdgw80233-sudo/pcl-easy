Imports System.Windows.Markup

<ContentProperty("SearchTags")>
Public Class PageResource

#Region "属性"

    ''' <summary>
    ''' 用于 XAML 快速设置的 Tag 下拉框列表。
    ''' </summary>
    Public ReadOnly Property SearchTags As ItemCollection
        Get
            Return ComboSearchTag.Items
        End Get
    End Property

    ''' <summary>
    ''' 英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    ''' </summary>
    Public Property TypeName As String
        Get
            Return _TypeName
        End Get
        Set(Value As String)
            If _TypeName = Value Then Return
            _TypeName = Value
            Loader.Name = $"社区资源获取：{Value}"
        End Set
    End Property
    Private _TypeName As String = ""

    ''' <summary>
    ''' 英文前后含一个空格的可读资源类型名，例如 " Mod "、"整合包"。
    ''' </summary>
    Public Property TypeNameSpaced As String
        Get
            Return _TypeNameSpaced
        End Get
        Set(Value As String)
            If _TypeNameSpaced = Value Then Return
            _TypeNameSpaced = Value
            PanAlways.Title = $"搜索{Value}"
            Load.Text = $"正在获取{Value}列表"
        End Set
    End Property
    Private _TypeNameSpaced As String = ""

    ''' <summary>
    ''' 该页面对应的资源类型。
    ''' </summary>
    Public Property PageType As ResourceTypes
        Get
            Return _Type
        End Get
        Set(Value As ResourceTypes)
            If _Type = Value Then Return
            _Type = Value
            BtnSearchInstallModPack.Visibility = If(Value = ResourceTypes.ModPack AndAlso Not RestrictMcDownload, Visibility.Visible, Visibility.Collapsed)
            If TextIntro IsNot Nothing Then ApplyIntro()
        End Set
    End Property
    Private _Type As ResourceTypes = -1

#End Region

#Region "加载"

    ''' <summary>
    ''' 在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    ''' </summary>
    Public Shared TargetInstance As McInstance = Nothing
    ''' <summary>
    ''' 在切换到该页面时自动设置的搜索框内的内容。
    ''' </summary>
    Public Shared TargetName As String = Nothing

    '在点击 MyResourceItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    Public Loader As LoaderTask(Of ResourceSearcher.SearchRequest, Integer) = ResourceSearcher.GetLoader(AddressOf LoaderInput)

    Private Sub PageResource_Loaded(sender As Object, e As EventArgs) Handles Me.Loaded
        ApplyIntro()
        Static IsFirstLoaded As Boolean = False
        '不知道从 Initialized 改成 Loaded 会不会有问题，但用 Initialized 会导致初始的筛选器修改被覆盖回默认值
        If TargetInstance IsNot Nothing Then
            '设置目标
            ResetFilter() '重置筛选器
            TextSearchVersion.Text = TargetInstance.Version.VanillaName
            Dim GetTargetItemByName =
            Function(Name As String) As MyComboBoxItem
                For Each Item As MyComboBoxItem In ComboSearchLoader.Items
                    If Item.Content = Name Then Return Item
                Next
                Return ComboSearchLoader.Items(0)
            End Function
            If TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge")
            ElseIf TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric")
            ElseIf TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge")
            End If
            TargetInstance = Nothing
            If TargetName IsNot Nothing Then
                TextSearchName.Text = TargetName
                TargetName = Nothing
            End If
            '如果已经完成请求，则重新开始
            If IsFirstLoaded Then StartNewSearch()
            ScrollToHome()
        End If
        '加载器初始化
        If IsFirstLoaded Then Return
        IsFirstLoaded = True
        CType(Parent, MyPageRight).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, Loader, AddressOf Load_OnFinish, AddressOf LoaderInput)
        '将最高 Drop 加入筛选
        If AllDrops IsNot Nothing AndAlso AllDrops.First > 250 Then
            Dim HighestVersion As String = McVersion.DropToVersion(AllDrops.First)
            If CType(TextSearchVersion.Items(1), MyComboBoxItem).Content.ToString <> HighestVersion Then '0 是全部
                TextSearchVersion.Items.Insert(1, New MyComboBoxItem With {.Content = HighestVersion})
            End If
        End If
    End Sub
    Private Function LoaderInput() As ResourceSearcher.SearchRequest
        Dim Request As New ResourceSearcher.SearchRequest(PageType, Storage, (Page + 1) * PAGE_SIZE)
        Dim GameVersion As String = If(TextSearchVersion.Text = "全部 (也可自行输入)", Nothing,
            If(TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w"), TextSearchVersion.Text, Nothing))
        With Request
            .SearchText = TextSearchName.Text
            .GameVersion = GameVersion
            .Tag = ComboSearchTag.SelectedItem.Tag
            .ModLoaders = If(PageType = ResourceTypes.Mod, Val(ComboSearchLoader.SelectedItem.Tag), ModLoaders.None)
            .Sources = CType(Val(ComboSearchSource.SelectedItem.Tag), ResourcePlatforms)
        End With
        Return Request
    End Function

#End Region

    Public Storage As New ResourceSearcher.SearchResult
    ''' <summary>
    ''' 每页展示的结果数量。
    ''' </summary>
    Public Const PAGE_SIZE = 40
    Public Page As Integer = 0

    '结果 UI 化
    Private Sub Load_OnFinish()
        Try
            Logger.Info($"开始可视化{TypeNameSpaced}列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页")
            '列表项
            PanProjects.Children.Clear()
            Dim Index As Integer = Math.Min(Page * PAGE_SIZE, Storage.Results.Count - 1)
            For Each Result In Storage.Results.GetRange(Index, Math.Min(Storage.Results.Count - Index, PAGE_SIZE))
                PanProjects.Children.Add(Result.ToResourceItem(
                    ShowMcVersionDesc:=Loader.Input.GameVersion Is Nothing,
                    ShowLoaderDesc:=Loader.Input.ModLoaders = ModLoaders.None AndAlso (PageType = ResourceTypes.Mod OrElse PageType = ResourceTypes.ModPack)))
            Next
            '页码
            CardPages.Visibility = If(Storage.Results.Count > 40 OrElse
                                      Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal,
                                      Visibility.Visible, Visibility.Collapsed)
            LabPage.Text = Page + 1
            BtnPageFirst.IsEnabled = Page > 1
            BtnPageFirst.Opacity = If(Page > 1, 1, 0.2)
            BtnPageLeft.IsEnabled = Page > 0
            BtnPageLeft.Opacity = If(Page > 0, 1, 0.2)
            Dim IsRightEnabled As Boolean = '由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
                Storage.Results.Count > PAGE_SIZE * (Page + 1) OrElse
                Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            BtnPageRight.IsEnabled = IsRightEnabled
            BtnPageRight.Opacity = If(IsRightEnabled, 1, 0.2)
            '错误信息
            If Storage.ErrorMessage Is Nothing Then
                HintError.Visibility = Visibility.Collapsed
            Else
                HintError.Visibility = Visibility.Visible
                HintError.Text = Storage.ErrorMessage
            End If
            '强制返回顶部
            ScrollToTop()
        Catch ex As Exception
            Logger.Error(ex, $"可视化{TypeNameSpaced}列表出错")
        End Try
    End Sub

    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case Loader.State
            Case LoadState.Failed
                Dim ErrorMessage As String = ""
                If Loader.Error IsNot Nothing Then ErrorMessage = Loader.Error.Message
                If ErrorMessage.Contains("不是有效的 json 文件") Then
                    Logger.Warn($"下载的{TypeNameSpaced}列表 json 文件损坏，已自动重试")
                    CType(Parent, MyPageRight).PageLoaderRestart()
                End If
        End Select
    End Sub

    '切换页码
    Private Sub BtnPageFirst_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageFirst.Click
        ChangePage(0)
    End Sub
    Private Sub BtnPageLeft_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageLeft.Click
        ChangePage(Page - 1)
    End Sub
    Private Sub BtnPageRight_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageRight.Click
        ChangePage(Page + 1)
    End Sub
    Private Sub ChangePage(NewPage As Integer)
        CardPages.IsEnabled = False
        Page = NewPage
        FrmMain.BackToTop()
        Logger.Info($"{TypeName}：切换到第 {Page + 1} 页")
        RunInThread(
        Sub()
            Thread.Sleep(100) '等待向上滚的动画结束
            RunInUi(Sub() CardPages.IsEnabled = True)
            Loader.Start()
        End Sub)
    End Sub

#Region "搜索"

    '搜索按钮
    Private Sub StartNewSearch() Handles BtnSearchRun.Click
        Page = 0
        If Loader.ShouldStart(LoaderInput()) Then Storage = New ResourceSearcher.SearchResult '避免连续搜索两次导致 ProjectStorage 引用丢失（#1311）
        Loader.Start()
    End Sub
    Private Sub EnterTrigger(sender As Object, e As KeyEventArgs) Handles TextSearchName.KeyDown, TextSearchVersion.KeyDown
        If e.Key = Key.Enter Then StartNewSearch()
    End Sub

    '重置按钮
    Private Sub ResetFilter() Handles BtnSearchReset.Click
        TextSearchName.Text = ""
        TextSearchVersion.Text = "全部 (也可自行输入)"
        TextSearchVersion.SelectedIndex = 0
        ComboSearchSource.SelectedIndex = 0
        ComboSearchTag.SelectedIndex = 0
        ComboSearchLoader.SelectedIndex = 0
        Loader.LastFinishedTime = 0 '要求强制重新开始
    End Sub

    '版本选择
    '#3067：当下拉菜单展开时，程序会被 WPF 挂起，因而无法更新 Grid 布局，所以必须延迟到下拉菜单收起后才能更新
    Private Sub TextSearchVersion_TextChanged() Handles TextSearchVersion.TextChanged
        If Not TextSearchVersion.IsDropDownOpen Then UpdateSearchLoaderVisibility()
    End Sub
    Private Sub UpdateSearchLoaderVisibility() Handles TextSearchVersion.DropDownClosed
        If PageType = ResourceTypes.Mod AndAlso (TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w")) Then
            ComboSearchLoader.Visibility = Visibility.Visible
            Grid.SetColumnSpan(TextSearchVersion, 1)
        Else
            ComboSearchLoader.Visibility = Visibility.Collapsed
            Grid.SetColumnSpan(TextSearchVersion, 2)
            ComboSearchLoader.SelectedIndex = 0
        End If
    End Sub

#End Region

    '安装已有整合包按钮
    Private Sub BtnSearchInstallModPack_Click(sender As Object, e As EventArgs) Handles BtnSearchInstallModPack.Click
        ModpackInstall()
    End Sub

    '小白说明
    Private Sub ApplyIntro()
        If TextIntro Is Nothing Then Return
        Select Case PageType
            Case ResourceTypes.Mod
                TextIntro.Text = "Mod（模组）是给游戏加内容的小插件：可以加新物品、新生物、新地图，也可以让游戏更流畅。" & vbCrLf &
                    "为什么要装：原版玩腻了想加新内容，或者电脑玩起来有点卡（装优化类 Mod）。" & vbCrLf &
                    "怎么用：装好之后，在「启动」页选对应版本进游戏就会生效。" & vbCrLf &
                    "注意：Mod 必须和游戏版本、加载器（Fabric/Forge）对得上，下错版本游戏会打不开；装之前建议先备份存档。"
            Case ResourceTypes.Shader
                TextIntro.Text = "光影（光影包）是给画面加一层「滤镜」：让光线、水面、云和阴影更好看。它只改画面，不改玩法。" & vbCrLf &
                    "为什么要装：想让游戏更好看、截图更漂亮。" & vbCrLf &
                    "怎么用：需要先装 Iris 或 OptiFine，装好后在游戏里 选项 → 视频设置 → 光影 里选。" & vbCrLf &
                    "装光影对显卡要求高，装之前先看下面的提示。"
            Case ResourceTypes.ModPack
                TextIntro.Text = "整合包是把几十个 Mod、配置和剧情预先搭配好的一整套游戏，下载解压后就能直接玩，适合不想自己一个个挑 Mod 的人。" & vbCrLf &
                    "注意：整合包通常要占几个 GB 磁盘空间，装之前先确认硬盘够用。"
            Case ResourceTypes.ResourcePack
                TextIntro.Text = "资源包只改游戏的外观和声音（材质、音乐、字体），不改玩法和规则。" & vbCrLf &
                    "装好之后在游戏里 选项 → 资源包 里启用。"
            Case ResourceTypes.DataPack
                TextIntro.Text = "数据包改的是游戏规则、配方、战利品这些内容，一般装进某个存档里，只影响那个世界。"
            Case Else
                TextIntro.Text = "这里可以下载社区制作的 Mod、整合包、资源包、数据包和光影。"
        End Select
        HintShaderWarn.Visibility = If(PageType = ResourceTypes.Shader, Visibility.Visible, Visibility.Collapsed)
    End Sub

End Class
