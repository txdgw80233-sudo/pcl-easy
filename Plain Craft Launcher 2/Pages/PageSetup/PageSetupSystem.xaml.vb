Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        If BuildType = BuildTypes.Release Then
            PanDonate.Visibility = Visibility.Collapsed
        Else
            PanDonate.Visibility = Visibility.Visible
            ItemSystemUpdateDownload.Content = "在有新版本时自动下载（更新快照版前需要先输入土豆码）"
        End If

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '识别码/土豆码替代入口
    Private Sub BtnSystemIdentify_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemIdentify.Click
        PageOtherAbout.CopyIdentify()
    End Sub
    Private Sub BtnSystemUnlock_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemUnlock.Click
        InputPotatoCode(False)
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

    '自动更新
    Private Sub ComboSystemActivity_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemActivity.SelectedIndex <> 2 Then Return
        If MyMsgBox("若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" & vbCrLf &
                    "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemActivity.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub ComboSystemUpdate_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdate.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemUpdate.SelectedIndex <> 3 Then Return
        If MyMsgBox("若选择此项，即使在启动器将来出现严重问题时，你也无法获取更新并获得修复。" & vbCrLf &
                    "例如，如果官方修改了登录方式，从而导致现有启动器无法登录，你可能就会因为无法更新而无法开始游戏。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重大漏洞更新时显示提示 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemUpdate.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub BtnSystemUpdate_Click(sender As Object, e As EventArgs) Handles BtnSystemUpdate.Click
        UpdateCheckByButton()
    End Sub
    ''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        Try
            '确认服务器公告是否正常
            Dim ServerContent As String = If(FileUtils.TryReadAsString(PathTemp & "Cache\Notice.cfg"), "")
            If ServerContent.Split("|").Count < 3 Then Return Nothing
            '确认是否为最新
            Return ServerContent.Split("|")(If(BuildType = BuildTypes.Release, 2, 1)) <= VersionCode
        Catch ex As Exception
            Logger.Error(ex, "确认启动器更新失败")
            Return Nothing
        End Try
    End Function

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint("该功能尚在开发中！")
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint("该功能尚在开发中！")
    End Sub

#End Region

#Region "百宝箱"

    '选一个能写进去的导出位置：桌面优先，不行就放到游戏文件夹里
    Private Function GetExportFolder() As String
        Try
            Dim Desktop As String = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            If Not String.IsNullOrWhiteSpace(Desktop) AndAlso IO.Directory.Exists(Desktop) Then
                Dim TestFile As String = IO.Path.Combine(Desktop, ".pcl_write_test")
                IO.File.WriteAllText(TestFile, "test")
                IO.File.Delete(TestFile)
                Return Desktop
            End If
        Catch
        End Try
        Dim Fallback As String = McFolderSelected & "PCL导出\"
        IO.Directory.CreateDirectory(Fallback)
        Return Fallback
    End Function

    Private Sub ToolboxStatus(Text As String)
        Try
            LabToolboxStatus.Text = Text
        Catch
        End Try
    End Sub

    '打开游戏文件夹
    Private Sub BtnToolboxOpenFolder_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Diagnostics.Process.Start("explorer.exe", """" & McFolderSelected & """")
            ToolboxStatus("已打开：" & McFolderSelected)
        Catch ex As Exception
            Hint("打开游戏文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    '备份存档
    Private Sub BtnToolboxBackup_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim SavesPath As String = McFolderSelected & "saves\"
            If Not IO.Directory.Exists(SavesPath) Then
                Hint("这个文件夹里还没有存档，进游戏建一个世界后再来备份吧。", HintType.Blue)
                Return
            End If
            Dim Files As New Dictionary(Of String, String)
            For Each FilePath As String In IO.Directory.GetFiles(SavesPath, "*", IO.SearchOption.AllDirectories)
                Files("saves\" & FilePath.Substring(SavesPath.Length)) = FilePath
            Next
            If Files.Count = 0 Then
                Hint("存档文件夹是空的，没有需要备份的内容。", HintType.Blue)
                Return
            End If
            Dim Target As String = IO.Path.Combine(GetExportFolder(), $"MC存档备份_{Now:yyyyMMdd_HHmmss}.zip")
            ToolboxStatus("正在备份 " & Files.Count & " 个文件……")
            FileUtils.CreateZipFromFiles(Target, Files)
            Hint("存档已备份：" & Target, HintType.Green)
            ToolboxStatus("备份完成：" & Target)
        Catch ex As Exception
            Hint("备份存档失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    '清理日志
    Private Sub BtnToolboxCleanLogs_Click(sender As Object, e As MouseButtonEventArgs)
        If MyMsgBox("会删除游戏文件夹里的日志和崩溃报告，不会动存档和 Mod。确定清理吗？", "清理日志", "取消", "清理") <> 2 Then Return
        Try
            Dim Count As Integer = 0
            For Each Folder As String In {"logs\", "crash-reports\"}
                Dim FullPath As String = McFolderSelected & Folder
                If Not IO.Directory.Exists(FullPath) Then Continue For
                For Each FilePath As String In IO.Directory.GetFiles(FullPath)
                    Try
                        IO.File.Delete(FilePath)
                        Count += 1
                    Catch
                    End Try
                Next
            Next
            ToolboxStatus($"已清理 {Count} 个日志文件。")
            Hint($"清理完成，共删除了 {Count} 个文件。", HintType.Green)
        Catch ex As Exception
            Hint("清理日志失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    '导出诊断包
    Private Sub BtnToolboxDiag_Click(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim Files As New Dictionary(Of String, String)
            CollectFiles(McFolderSelected & "logs\", "logs", Files)
            CollectFiles(McFolderSelected & "crash-reports\", "crash-reports", Files)
            If IO.File.Exists(McFolderSelected & "PCL.ini") Then Files("PCL.ini") = McFolderSelected & "PCL.ini"
            Dim SetupPath As String = Paths.Base & "PCL\Setup.ini"
            If IO.File.Exists(SetupPath) Then Files("Setup.ini") = SetupPath
            If Files.Count = 0 Then
                Hint("没有找到日志文件，先启动一次游戏再来导出。", HintType.Blue)
                Return
            End If
            Dim Target As String = IO.Path.Combine(GetExportFolder(), $"PCL诊断包_{Now:yyyyMMdd_HHmmss}.zip")
            ToolboxStatus("正在打包 " & Files.Count & " 个文件……")
            FileUtils.CreateZipFromFiles(Target, Files)
            Hint("诊断包已导出：" & Target & "，遇到问题可以把它发给我。", HintType.Green)
            ToolboxStatus("导出完成：" & Target)
        Catch ex As Exception
            Hint("导出诊断包失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub CollectFiles(Folder As String, Prefix As String, Files As Dictionary(Of String, String))
        If Not IO.Directory.Exists(Folder) Then Return
        For Each FilePath As String In IO.Directory.GetFiles(Folder, "*", IO.SearchOption.AllDirectories)
            Files(Prefix & "\" & FilePath.Substring(Folder.Length)) = FilePath
        Next
    End Sub

#End Region

End Class
