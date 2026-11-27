Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Namespace Windows

    Public Class AiSettings
        Inherits Window

        Private ReadOnly RadioLight As New RadioButton
        Private ReadOnly RadioStd As New RadioButton
        Private ReadOnly CheckStopOnGame As New CheckBox
        Private ReadOnly LabModelTip As New TextBlock
        Private ReadOnly LabDownload As New TextBlock
        Private UiReady As Boolean = False

        Public Sub New()
            Title = "AI 答疑设置"
            Width = 560
            Height = 540
            ResizeMode = ResizeMode.NoResize
            WindowStartupLocation = WindowStartupLocation.CenterOwner
            Background = New SolidColorBrush(Color.FromRgb(&HF9, &HFA, &HFC))
            FontFamily = New FontFamily("Microsoft YaHei UI, Segoe UI")
            FontSize = 13
            BuildUi()
            AddHandler Loaded, Sub(s, e) InitState()
        End Sub

        Private Sub BuildUi()
            Dim Root As New StackPanel With {.Margin = New Thickness(16)}

            Root.Children.Add(New TextBlock With {
                .Text = "模型选择（按自己电脑的配置选）",
                .FontSize = 15,
                .FontWeight = FontWeights.Bold,
                .Foreground = New SolidColorBrush(Color.FromRgb(&H1B, &H3A, &H63)),
                .Margin = New Thickness(0, 0, 0, 4)
            })
            Dim Sub1 As New TextBlock With {
                .Text = "模型都在你自己电脑上跑，不联网。切换只影响下一次启动 AI 服务。",
                .Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66)),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 0, 0, 10)
            }
            Root.Children.Add(Sub1)

            RadioLight.GroupName = "AiModel"
            RadioLight.Content = "轻量版 0.5B（小、快，适合内存小的电脑）"
            RadioLight.Margin = New Thickness(0, 2, 0, 2)
            AddHandler RadioLight.Checked, AddressOf Model_Checked
            Root.Children.Add(RadioLight)
            Dim LightTip As New TextBlock With {
                .Text = "    配置要求：约 400 MB 磁盘；8 GB 内存以内也能跑（4 GB 会很慢）。适合简单问答，偶尔会绕。",
                .Foreground = New SolidColorBrush(Color.FromRgb(&H77, &H77, &H77)),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 0, 0, 8)
            }
            Root.Children.Add(LightTip)

            RadioStd.GroupName = "AiModel"
            RadioStd.Content = "标准版 1.5B（回答更准，建议 16 GB 内存）"
            RadioStd.Margin = New Thickness(0, 2, 0, 2)
            AddHandler RadioStd.Checked, AddressOf Model_Checked
            Root.Children.Add(RadioStd)
            Dim StdTip As New TextBlock With {
                .Text = "    配置要求：约 1.1 GB 磁盘，建议 16 GB 内存；回答更准。8 GB 内存的电脑也能跑，但会明显变慢。",
                .Foreground = New SolidColorBrush(Color.FromRgb(&H77, &H77, &H77)),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 0, 0, 8)
            }
            Root.Children.Add(StdTip)

            LabModelTip.Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66))
            LabModelTip.TextWrapping = TextWrapping.Wrap
            LabModelTip.Margin = New Thickness(0, 0, 0, 4)
            Root.Children.Add(LabModelTip)
            LabDownload.Foreground = New SolidColorBrush(Color.FromRgb(&H33, &H77, &HCC))
            LabDownload.TextWrapping = TextWrapping.Wrap
            LabDownload.Margin = New Thickness(0, 0, 0, 12)
            Root.Children.Add(LabDownload)

            Dim Line As New Border With {.Height = 1, .Background = New SolidColorBrush(Color.FromRgb(&HE0, &HE4, &HEA)), .Margin = New Thickness(0, 0, 0, 12)}
            Root.Children.Add(Line)

            CheckStopOnGame.Content = "游戏启动时自动关闭 AI（推荐）"
            AddHandler CheckStopOnGame.Checked, AddressOf StopOnGame_Changed
            AddHandler CheckStopOnGame.Unchecked, AddressOf StopOnGame_Changed
            Root.Children.Add(CheckStopOnGame)
            Dim Risk As New TextBlock With {
                .Text = "风险提示：AI 和游戏同时跑会抢内存与 CPU。8 GB 内存的电脑同时开很容易卡顿、掉帧，甚至游戏崩溃；只有 16 GB 以上内存、并且能接受帧数下降，才建议关掉这个选项。",
                .Foreground = New SolidColorBrush(Color.FromRgb(&HC0, &H50, &H20)),
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(0, 6, 0, 12)
            }
            Root.Children.Add(Risk)

            '规则文件信息
            Dim RuleInfo As New TextBlock With {
                .Text = "内置规则：" & EasyAiRules.RuleCount() & " 条（当前来源：" & EasyAiRules.RulesSource & "）" & vbCrLf &
                        "规则文件：" & EasyAiRules.RulesFilePath,
                .TextWrapping = TextWrapping.Wrap,
                .Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66)),
                .Margin = New Thickness(0, 0, 0, 6)
            }
            Root.Children.Add(RuleInfo)
            Dim BtnRules As New Button With {
                .Content = "打开规则文件（可自己加条目）",
                .HorizontalAlignment = HorizontalAlignment.Left,
                .Padding = New Thickness(12, 4, 12, 4),
                .Margin = New Thickness(0, 0, 0, 14),
                .Cursor = System.Windows.Input.Cursors.Hand
            }
            AddHandler BtnRules.Click, Sub(s, e)
                                           Try
                                               EasyAiRules.EnsureRulesFile()
                                               System.Diagnostics.Process.Start("explorer.exe", "/select,""" & EasyAiRules.RulesFilePath & """")
                                           Catch ex As Exception
                                               MsgBox("打开规则文件失败：" & ex.Message, MsgBoxStyle.OkOnly, "AI 答疑")
                                           End Try
                                       End Sub
            Root.Children.Add(BtnRules)

            Dim Ok As New Button With {
                .Content = "确定",
                .HorizontalAlignment = HorizontalAlignment.Right,
                .Padding = New Thickness(18, 6, 18, 6),
                .BorderThickness = New Thickness(0),
                .Foreground = Brushes.White,
                .Background = New SolidColorBrush(Color.FromRgb(&H2B, &H6C, &HD8)),
                .Cursor = System.Windows.Input.Cursors.Hand
            }
            AddHandler Ok.Click, Sub(s, e) Close()
            Root.Children.Add(Ok)

            Content = Root
        End Sub

        Private Sub InitState()
            Try
                Dim Chosen As Integer = EasyAi.SelectedModel()
                RadioStd.IsChecked = (Chosen = 1)
                RadioLight.IsChecked = (Chosen = 0)
                CheckStopOnGame.IsChecked = Settings.Get(Of Boolean)("EasyAiStopOnGame")
            Catch ex As Exception
            End Try
            RefreshTip()
            UiReady = True
        End Sub

        Private Sub RefreshTip()
            Dim Ram = 0.0
            Try
                Ram = Math.Round(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024, 0)
            Catch ex As Exception
            End Try
            LabModelTip.Text = $"你的电脑内存约 {Ram} GB，系统推荐：{If(EasyAi.RecommendedModel() = 0, "轻量版 0.5B", "标准版 1.5B")}。"
            Dim SmallReady As Boolean = IO.File.Exists(EasyAi.AiFolder & "qwen2.5-0.5b-instruct-q4_k_m.gguf")
            Dim StdReady As Boolean = IO.File.Exists(EasyAi.AiFolder & "qwen2.5-1.5b-instruct-q4_k_m.gguf")
            LabDownload.Text = $"下载状态：轻量版{(If(SmallReady, "已下载", "未下载（约 400 MB）"))}；标准版{(If(StdReady, "已下载", "未下载（约 1.1 GB）"))}。"
        End Sub

        Private Sub Model_Checked(sender As Object, e As RoutedEventArgs)
            If Not UiReady Then Exit Sub
            Dim Chosen As Integer = If(sender Is RadioStd, 1, 0)
            Settings.Set("EasyAiModel", Chosen)
            Logger.Info("切换 AI 模型为 " & If(Chosen = 1, "标准版 1.5B", "轻量版 0.5B"))
            Try
                EasyAi.StopServer()
            Catch ex As Exception
            End Try
            RefreshTip()
        End Sub

        Private Sub StopOnGame_Changed(sender As Object, e As RoutedEventArgs)
            If Not UiReady Then Exit Sub
            Settings.Set("EasyAiStopOnGame", CheckStopOnGame.IsChecked)
        End Sub

    End Class

End Namespace
