Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports Newtonsoft.Json.Linq

Namespace Windows

    Public Class AiChat
        Inherits Window

        Private ReadOnly Cts As New CancellationTokenSource
        Private ReadOnly LabTitle As New TextBlock
        Private ReadOnly LabStatus As New TextBlock
        Private ReadOnly PanChat As New StackPanel
        Private ReadOnly PanScroll As New ScrollViewer
        Private ReadOnly TxtInput As New TextBox
        Private ReadOnly BtnSend As New Button
        Private ReadOnly BtnStop As New Button
        Private ReadOnly BtnSetup As New Button
        Private ReadOnly RadioLight As New RadioButton
        Private ReadOnly RadioStd As New RadioButton
        Private ReadOnly CheckStopOnGame As New CheckBox
        Private ReadOnly LabModelTip As New TextBlock
        Private ReadOnly LabRisk As New TextBlock
        Private Busy As Boolean = False
        Private SetupBusy As Boolean = False
        Private ServerStarted As Boolean = False
        Private ReadOnly Messages As New JArray
        Private LastProgressTick As Long = 0
        Private UiReady As Boolean = False

        Public Sub New()
            Title = "AI 答疑（本地小模型）"
            Width = 720
            Height = 640
            Background = New SolidColorBrush(Color.FromRgb(&HF9, &HFA, &HFC))
            FontFamily = New FontFamily("Microsoft YaHei UI, Segoe UI")
            FontSize = 13
            WindowStartupLocation = WindowStartupLocation.CenterOwner
            BuildUi()
            AddHandler Loaded, AddressOf AiChat_Loaded
            AddHandler Closing, AddressOf AiChat_Closing
            EasyAi.StartGuard()
            Messages.Add(NewMessage("system", EasyAi.BuildSystemPrompt(GetInstanceInfo())))
        End Sub

        Private Sub BuildUi()
            Dim Root As New Grid With {.Margin = New Thickness(14)}
            Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
            Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})

            '顶部状态卡
            Dim Header As New Border With {
                .Background = Brushes.White,
                .CornerRadius = New CornerRadius(6),
                .Padding = New Thickness(12, 10, 12, 10),
                .Margin = New Thickness(0, 0, 0, 10)
            }
            Dim HeaderGrid As New Grid()
            HeaderGrid.ColumnDefinitions.Add(New ColumnDefinition())
            HeaderGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            LabTitle.Text = "AI 答疑"
            LabTitle.FontSize = 15
            LabTitle.FontWeight = FontWeights.Bold
            LabTitle.Foreground = New SolidColorBrush(Color.FromRgb(&H1B, &H3A, &H63))
            LabStatus.Text = "正在准备……"
            LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66))
            LabStatus.TextWrapping = TextWrapping.Wrap
            LabStatus.Margin = New Thickness(0, 4, 0, 0)
            Dim HeaderStack As New StackPanel()
            HeaderStack.Children.Add(LabTitle)
            HeaderStack.Children.Add(LabStatus)
            HeaderGrid.Children.Add(HeaderStack)
            StyleButton(BtnSetup, "下载并启动", True)
            BtnSetup.Visibility = Visibility.Collapsed
            BtnSetup.VerticalAlignment = VerticalAlignment.Center
            BtnSetup.Margin = New Thickness(10, 0, 0, 0)
            AddHandler BtnSetup.Click, AddressOf BtnSetup_Click
            Grid.SetColumn(BtnSetup, 1)
            HeaderGrid.Children.Add(BtnSetup)
            Header.Child = HeaderGrid
            Root.Children.Add(Header)

            '模型与设置
            Dim Options As New Border With {
                .Background = Brushes.White,
                .CornerRadius = New CornerRadius(6),
                .Padding = New Thickness(12, 8, 12, 10),
                .Margin = New Thickness(0, 0, 0, 10)
            }
            Dim OptionsStack As New StackPanel()
            Dim OptionsTitle As New TextBlock With {.Text = "模型选择（按自己电脑的配置选）", .FontWeight = FontWeights.Bold, .Margin = New Thickness(0, 0, 0, 4)}
            OptionsStack.Children.Add(OptionsTitle)
            RadioLight.GroupName = "AiModel"
            RadioLight.Content = "轻量版 0.5B：占用约 400 MB 磁盘、8 GB 内存以内也能跑；回答简单问题够用，偶尔会绕。"
            RadioLight.Margin = New Thickness(0, 2, 0, 2)
            AddHandler RadioLight.Checked, AddressOf ModelRadio_Checked
            OptionsStack.Children.Add(RadioLight)
            RadioStd.GroupName = "AiModel"
            RadioStd.Content = "标准版 1.5B：占用约 1.1 GB 磁盘、建议 16 GB 内存；回答更准，低配电脑会明显变慢。"
            RadioStd.Margin = New Thickness(0, 2, 0, 4)
            AddHandler RadioStd.Checked, AddressOf ModelRadio_Checked
            OptionsStack.Children.Add(RadioStd)
            LabModelTip.Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66))
            LabModelTip.Margin = New Thickness(0, 0, 0, 6)
            OptionsStack.Children.Add(LabModelTip)
            CheckStopOnGame.Content = "游戏启动时自动关闭 AI（推荐）"
            AddHandler CheckStopOnGame.Checked, AddressOf CheckStopOnGame_Changed
            AddHandler CheckStopOnGame.Unchecked, AddressOf CheckStopOnGame_Changed
            OptionsStack.Children.Add(CheckStopOnGame)
            LabRisk.Foreground = New SolidColorBrush(Color.FromRgb(&HC0, &H50, &H20))
            LabRisk.TextWrapping = TextWrapping.Wrap
            LabRisk.Margin = New Thickness(0, 4, 0, 0)
            LabRisk.Text = "风险提示：AI 和游戏同时跑会抢内存与 CPU。8 GB 内存的电脑同时开很容易卡顿、掉帧，甚至游戏崩溃；只有 16 GB 以上内存、并且能接受帧数下降，才建议关掉上面这个选项。"
            OptionsStack.Children.Add(LabRisk)
            Options.Child = OptionsStack
            Grid.SetRow(Options, 1)
            Root.Children.Add(Options)

            '聊天区
            Dim ChatBox As New Border With {
                .Background = Brushes.White,
                .CornerRadius = New CornerRadius(6),
                .Padding = New Thickness(4)
            }
            PanChat.Margin = New Thickness(8)
            PanScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            PanScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            PanScroll.Content = PanChat
            ChatBox.Child = PanScroll
            Grid.SetRow(ChatBox, 2)
            Root.Children.Add(ChatBox)

            '输入区
            Dim InputGrid As New Grid With {.Margin = New Thickness(0, 10, 0, 0)}
            InputGrid.ColumnDefinitions.Add(New ColumnDefinition())
            InputGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            InputGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            TxtInput.Height = 62
            TxtInput.AcceptsReturn = True
            TxtInput.TextWrapping = TextWrapping.Wrap
            TxtInput.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            TxtInput.Padding = New Thickness(8, 6, 8, 6)
            TxtInput.VerticalContentAlignment = VerticalAlignment.Top
            AddHandler TxtInput.TextChanged, AddressOf TxtInput_TextChanged
            InputGrid.Children.Add(TxtInput)
            StyleButton(BtnSend, "发送", True)
            BtnSend.Margin = New Thickness(10, 0, 0, 0)
            BtnSend.Height = 62
            BtnSend.MinWidth = 90
            BtnSend.IsEnabled = False
            AddHandler BtnSend.Click, AddressOf BtnSend_Click
            Grid.SetColumn(BtnSend, 1)
            InputGrid.Children.Add(BtnSend)
            StyleButton(BtnStop, "停止", False)
            BtnStop.Margin = New Thickness(8, 0, 0, 0)
            BtnStop.Height = 62
            BtnStop.MinWidth = 80
            BtnStop.Visibility = Visibility.Collapsed
            AddHandler BtnStop.Click, AddressOf BtnStop_Click
            Grid.SetColumn(BtnStop, 2)
            InputGrid.Children.Add(BtnStop)
            Grid.SetRow(InputGrid, 3)
            Root.Children.Add(InputGrid)

            Content = Root
        End Sub

        Private Shared Sub StyleButton(Button As Button, Text As String, Highlight As Boolean)
            Button.Content = Text
            Button.Padding = New Thickness(13, 5, 13, 5)
            Button.BorderThickness = New Thickness(0)
            Button.Foreground = If(Highlight, Brushes.White, New SolidColorBrush(Color.FromRgb(&H33, &H33, &H33)))
            Button.Background = If(Highlight, New SolidColorBrush(Color.FromRgb(&H2B, &H6C, &HD8)), New SolidColorBrush(Color.FromRgb(&HEB, &HEE, &HF3)))
            Button.Cursor = System.Windows.Input.Cursors.Hand
        End Sub

        Private Function NewMessage(Role As String, Content As String) As JObject
            Dim Message As New JObject()
            Message.Add("role", Role)
            Message.Add("content", Content)
            Return Message
        End Function

        Private Function GetInstanceInfo() As String
            Try
                If PageInstanceLeft.Instance IsNot Nothing Then
                    Return $"当前实例：{PageInstanceLeft.Instance.Name}"
                End If
            Catch ex As Exception
            End Try
            Return "当前没有选中游戏实例"
        End Function

        Private Sub AiChat_Closing(sender As Object, e As System.ComponentModel.CancelEventArgs)
            Try
                Cts.Cancel()
                EasyAi.StopServer()
            Catch ex As Exception
            End Try
        End Sub

        Private Async Sub AiChat_Loaded(sender As Object, e As RoutedEventArgs)
            InitModelUi()
            AddBubble("你好，我是启动器自带的答疑助手，完全在你电脑上跑，不联网。" & vbCrLf &
                      "可以问我：怎么装游戏、报错是什么意思、这个按钮干什么的、推荐装什么模组或光影。", False)
            Await RefreshStateAsync()
        End Sub

        Private Async Function RefreshStateAsync() As Task
            If Not EasyAi.RuntimeReady() OrElse Not EasyAi.ModelReady() Then
                Dim ModelText = If(EasyAi.UseSmallModel(), "约 400 MB（轻量版，适合 8G 内存）", "约 1.1 GB（标准版，答得更准）")
                LabStatus.Text = $"第一次使用需要下载 AI 运行库（约 24 MB）和模型 {ModelText}，下载完就能离线用。"
                BtnSetup.Visibility = Visibility.Visible
                Await Task.CompletedTask
                Exit Function
            End If
            BtnSetup.Visibility = Visibility.Collapsed
            If EasyAi.ServerRunning() Then
                LabStatus.Text = "AI 已就绪，直接问吧。（游戏运行时它会自动退出，不占内存）"
                Exit Function
            End If
            LabStatus.Text = "正在启动本地 AI 服务……"
            Try
                Await Task.Run(Sub() EasyAi.StartServer())
                ServerStarted = True
                LabStatus.Text = "AI 已就绪，直接问吧。（游戏运行时它会自动退出，不占内存）"
            Catch ex As Exception
                LabStatus.Text = "AI 服务启动失败：" & ex.Message
                Logger.Error(ex, "启动本地 AI 服务失败")
            End Try
        End Function

        Private Async Sub BtnSetup_Click(sender As Object, e As RoutedEventArgs)
            If SetupBusy Then Exit Sub
            SetupBusy = True
            BtnSetup.IsEnabled = False
            Try
                If Not EasyAi.RuntimeReady() Then
                    LabStatus.Text = "正在下载 AI 运行库……"
                    Await Task.Run(Sub() EasyAi.InstallRuntime(Sub(done, total) ReportDownload("AI 运行库", done, total)))
                End If
                If Not EasyAi.ModelReady() Then
                    LabStatus.Text = "正在下载 AI 模型（第一次会比较久）……"
                    Await Task.Run(Sub() EasyAi.InstallModel(Sub(done, total) ReportDownload("AI 模型", done, total)))
                End If
                BtnSetup.Visibility = Visibility.Collapsed
                Await RefreshStateAsync()
            Catch ex As Exception
                LabStatus.Text = "下载失败：" & ex.Message & "（可以稍后再点一次）"
                Logger.Error(ex, "下载 AI 组件失败")
                BtnSetup.IsEnabled = True
            Finally
                SetupBusy = False
                If BtnSetup.Visibility = Visibility.Visible Then BtnSetup.IsEnabled = True
            End Try
        End Sub

        Private Sub ReportDownload(Name As String, Done As Long, Total As Long)
            Dim NowTick = Environment.TickCount
            If NowTick - LastProgressTick < 300 AndAlso Done <> Total Then Exit Sub
            LastProgressTick = NowTick
            Dim Text As String
            If Total > 0 Then
                Text = $"正在下载{Name}：{Done \ 1024 \ 1024} / {Total \ 1024 \ 1024} MB（{Done * 100.0 / Total:0}%）"
            Else
                Text = $"正在下载{Name}：{Done \ 1024 \ 1024} MB"
            End If
            RunInUi(Sub() LabStatus.Text = Text)
        End Sub

        Private Sub TxtInput_TextChanged(sender As Object, e As TextChangedEventArgs)
            BtnSend.IsEnabled = Not Busy AndAlso TxtInput.Text.Trim() <> "" AndAlso EasyAi.ServerRunning()
        End Sub

        Private Async Sub BtnSend_Click(sender As Object, e As RoutedEventArgs)
            If Busy Then Exit Sub
            Dim Question = TxtInput.Text.Trim()
            If Question = "" Then Exit Sub
            If Not EasyAi.ServerRunning() Then
                AddBubble("AI 服务还没启动，先点上面的「下载并启动」。", False)
                Exit Sub
            End If
            TxtInput.Text = ""
            Busy = True
            BtnSend.IsEnabled = False
            BtnStop.Visibility = Visibility.Visible
            AddBubble(Question, True)
            Messages.Add(NewMessage("user", Question))
            Dim Bubble = AddBubble("", False)
            Try
                Dim Answer = Await EasyAi.AskAsync(Messages, Sub(delta) RunInUi(Sub() Bubble.Text &= delta), Cts.Token)
                If Answer.Trim() = "" Then Bubble.Text = "（模型没有回答，换个问法试试）"
                Messages.Add(NewMessage("assistant", Answer))
            Catch ex As Exception
                Bubble.Text = "出错了：" & ex.Message
                Logger.Error(ex, "AI 对话失败")
            Finally
                Busy = False
                BtnStop.Visibility = Visibility.Collapsed
                BtnSend.IsEnabled = TxtInput.Text.Trim() <> "" AndAlso EasyAi.ServerRunning()
                PanScroll.ScrollToEnd()
            End Try
        End Sub

        Private Sub BtnStop_Click(sender As Object, e As RoutedEventArgs)
            Cts.Cancel()
            Busy = False
            BtnStop.Visibility = Visibility.Collapsed
        End Sub

        Private Sub InitModelUi()
            Try
                Dim Chosen As Integer = EasyAi.SelectedModel()
                RadioStd.IsChecked = (Chosen = 1)
                RadioLight.IsChecked = (Chosen = 0)
                CheckStopOnGame.IsChecked = Settings.Get(Of Boolean)("EasyAiStopOnGame")
            Catch ex As Exception
            End Try
            RefreshModelTip()
            UiReady = True
        End Sub

        Private Sub RefreshModelTip()
            Dim Ram = 0.0
            Try
                Ram = Math.Round(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024, 0)
            Catch ex As Exception
            End Try
            Dim Rec = If(EasyAi.RecommendedModel() = 0, "轻量版", "标准版")
            LabModelTip.Text = $"你的电脑内存约 {Ram} GB，系统推荐：{Rec}。下载过的模型会存在启动器目录，切换只影响下次启动服务。"
        End Sub

        Private Sub ModelRadio_Checked(sender As Object, e As RoutedEventArgs)
            If Not UiReady Then Exit Sub
            Dim Chosen As Integer = If(sender Is RadioStd, 1, 0)
            Settings.Set("EasyAiModel", Chosen)
            RefreshModelTip()
            Logger.Info("切换 AI 模型为 " & If(Chosen = 1, "标准版 1.5B", "轻量版 0.5B"))
            RunInNewThread(
                Sub()
                    Try
                        EasyAi.StopServer()
                        RunInUi(Async Sub()
                                    Await RefreshStateAsync()
                                End Sub)
                    Catch ex As Exception
                        Logger.Error(ex, "切换 AI 模型失败")
                    End Try
                End Sub)
        End Sub

        Private Sub CheckStopOnGame_Changed(sender As Object, e As RoutedEventArgs)
            If Not UiReady Then Exit Sub
            Dim Checked As Boolean = CheckStopOnGame.IsChecked
            Settings.Set("EasyAiStopOnGame", Checked)
            If Not Checked Then
                Hint("已关闭「游戏启动时自动关闭 AI」。注意：同时运行会抢内存，配置不够时游戏会很卡。", HintType.Red)
            End If
        End Sub

        Private Function AddBubble(Text As String, IsUser As Boolean) As TextBlock
            Dim Content As New TextBlock With {.Text = Text, .TextWrapping = TextWrapping.Wrap, .MaxWidth = 520}
            If IsUser Then Content.Foreground = Brushes.White
            Dim Box As New Border With {
                .Child = Content,
                .CornerRadius = New CornerRadius(6),
                .Padding = New Thickness(12, 9, 12, 9),
                .Margin = New Thickness(0, 4, 0, 4),
                .MaxWidth = 560,
                .Background = If(IsUser, New SolidColorBrush(Color.FromRgb(&H2B, &H6C, &HD8)), New SolidColorBrush(Color.FromRgb(&HF0, &HF3, &HF8))),
                .HorizontalAlignment = If(IsUser, HorizontalAlignment.Right, HorizontalAlignment.Left)
            }
            PanChat.Children.Add(Box)
            PanScroll.ScrollToEnd()
            Return Content
        End Function

    End Class

End Namespace
