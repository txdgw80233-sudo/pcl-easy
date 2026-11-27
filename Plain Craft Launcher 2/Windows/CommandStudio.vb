Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Namespace Windows

    ''' <summary>
    ''' 指令工坊：置顶小窗，按热键随时唤起，按当前游戏版本生成指令。
    ''' </summary>
    Public Class CommandStudio
        Inherits Window

        Private Shared Instance As CommandStudio

        Private ReadOnly LabVersion As New TextBlock
        Private ReadOnly LabTitle As New TextBlock
        Private ReadOnly LabDesc As New TextBlock
        Private ReadOnly PanForm As New StackPanel
        Private ReadOnly TxtPreview As New TextBox
        Private ReadOnly LabStatus As New TextBlock
        Private ReadOnly LstCommands As New ListBox
        Private ReadOnly Fields As New Dictionary(Of String, Control)
        Private CurrentId As String = "give"

        Private Class CmdItem
            Public Id As String
            Public Title As String
            Public Overrides Function ToString() As String
                Return Title
            End Function
        End Class

        Public Sub New()
            Title = "指令工坊"
            Width = 720
            Height = 560
            Topmost = True
            ShowInTaskbar = False
            WindowStartupLocation = WindowStartupLocation.CenterScreen
            Background = New SolidColorBrush(Color.FromRgb(&HF7, &HF9, &HFC))
            FontFamily = New FontFamily("Microsoft YaHei UI, Segoe UI")
            FontSize = 13
            BuildUi()
            AddHandler Loaded, Sub(s, e) RefreshVersion()
        End Sub

        Public Shared Sub ToggleStudio()
            Try
                If Instance Is Nothing Then
                    Instance = New CommandStudio()
                    AddHandler Instance.Closed, Sub(s, e) Instance = Nothing
                    Instance.Show()
                    Instance.Activate()
                ElseIf Instance.IsVisible Then
                    Instance.Hide()
                Else
                    Instance.RefreshVersion()
                    Instance.Show()
                    Instance.Activate()
                End If
            Catch ex As Exception
                Logger.Warn("[指令] 打开窗口失败：" & ex.Message)
            End Try
        End Sub

        Private Sub RefreshVersion()
            Dim Ver = EasyCmds.CurrentVersion()
            LabVersion.Text = "游戏版本 " & Ver & "（自动识别）"
        End Sub

        Private Sub BuildUi()
            Dim Root As New Grid With {.Margin = New Thickness(14)}
            Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
            Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})

            Dim Head As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 10)}
            Dim LabName As New TextBlock With {.Text = "指令工坊", .FontSize = 20, .FontWeight = FontWeights.Bold}
            LabVersion.Foreground = New SolidColorBrush(Color.FromRgb(&H2B, &H6C, &HD8))
            LabVersion.Margin = New Thickness(14, 6, 0, 0)
            Head.Children.Add(LabName)
            Head.Children.Add(LabVersion)
            Grid.SetRow(Head, 0)
            Root.Children.Add(Head)

            Dim Body As New Grid
            Body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(210)})
            Body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(14)})
            Body.ColumnDefinitions.Add(New ColumnDefinition)
            Grid.SetRow(Body, 1)
            Root.Children.Add(Body)

            Dim Specs As String()() = {
                New String() {"give", "给予物品"}, New String() {"effect", "药水效果"},
                New String() {"gamemode", "游戏模式"}, New String() {"difficulty", "世界难度"},
                New String() {"tp", "传送"}, New String() {"time", "时间"},
                New String() {"weather", "天气"}, New String() {"gamerule", "游戏规则"},
                New String() {"summon", "生成实体"}, New String() {"locate", "查找结构 / 生物群系"}}
            For Each Item As String() In Specs
                LstCommands.Items.Add(New CmdItem With {.Id = Item(0), .Title = Item(1)})
            Next
            LstCommands.BorderThickness = New Thickness(0)
            LstCommands.Background = Brushes.White
            AddHandler LstCommands.SelectionChanged, Sub(s, e)
                                                         Dim It = TryCast(LstCommands.SelectedItem, CmdItem)
                                                         If It IsNot Nothing Then SelectCommand(It.Id)
                                                     End Sub
            Grid.SetColumn(LstCommands, 0)
            Body.Children.Add(LstCommands)

            Dim Right As New StackPanel
            Grid.SetColumn(Right, 2)
            Body.Children.Add(Right)

            LabTitle.FontSize = 17
            LabTitle.FontWeight = FontWeights.Bold
            Right.Children.Add(LabTitle)
            LabDesc.Foreground = New SolidColorBrush(Color.FromRgb(&H66, &H66, &H66))
            LabDesc.Margin = New Thickness(0, 4, 0, 10)
            LabDesc.TextWrapping = TextWrapping.Wrap
            Right.Children.Add(LabDesc)

            PanForm.Margin = New Thickness(0, 0, 0, 10)
            Right.Children.Add(PanForm)

            Dim LabPrev As New TextBlock With {.Text = "生成的指令"}
            Right.Children.Add(LabPrev)
            TxtPreview.IsReadOnly = True
            TxtPreview.FontFamily = New FontFamily("Consolas, Microsoft YaHei UI")
            TxtPreview.Height = 64
            TxtPreview.Margin = New Thickness(0, 4, 0, 8)
            TxtPreview.TextWrapping = TextWrapping.Wrap
            Right.Children.Add(TxtPreview)

            Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal}
            Dim BtnCopy As New Button With {.Content = "复制指令", .Width = 110, .Padding = New Thickness(8, 6, 8, 6)}
            AddHandler BtnCopy.Click, Sub(s, e)
                                          EasyCmds.CopyCommand(TxtPreview.Text)
                                          LabStatus.Text = "已复制，进游戏按 T 粘贴（Ctrl+V）再回车。"
                                      End Sub
            Dim BtnPaste As New Button With {.Content = "直接输入到游戏", .Width = 150, .Margin = New Thickness(10, 0, 0, 0), .Padding = New Thickness(8, 6, 8, 6)}
            AddHandler BtnPaste.Click, Sub(s, e)
                                           If EasyCmds.TryPasteToGame(TxtPreview.Text) Then
                                               LabStatus.Text = "已发送到游戏。"
                                           Else
                                               EasyCmds.CopyCommand(TxtPreview.Text)
                                               LabStatus.Text = "游戏不在前台，已复制到剪贴板，切回游戏按 T 粘贴。"
                                           End If
                                       End Sub
            Dim BtnClose As New Button With {.Content = "关闭", .Width = 80, .Margin = New Thickness(10, 0, 0, 0), .Padding = New Thickness(8, 6, 8, 6)}
            AddHandler BtnClose.Click, Sub(s, e) Hide()
            Buttons.Children.Add(BtnCopy)
            Buttons.Children.Add(BtnPaste)
            Buttons.Children.Add(BtnClose)
            Right.Children.Add(Buttons)

            LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(&H2E, &H7D, &H32))
            LabStatus.Margin = New Thickness(0, 8, 0, 0)
            LabStatus.TextWrapping = TextWrapping.Wrap
            Right.Children.Add(LabStatus)

            Dim Foot As New TextBlock With {.Text = "热键 " & If(EasyCmds.StudioHotkeyActual = "", EasyCmds.StudioHotkeyText, EasyCmds.StudioHotkeyActual) & " 可随时呼出/收起。独占全屏（F11）下窗口会被游戏盖住，建议用窗口化或无边框。", .Foreground = New SolidColorBrush(Color.FromRgb(&H88, &H88, &H88)), .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 10, 0, 0)}
            Grid.SetRow(Foot, 2)
            Root.Children.Add(Foot)

            Content = Root
            LstCommands.SelectedIndex = 0
        End Sub

        Private Sub SelectCommand(Id As String)
            CurrentId = Id
            PanForm.Children.Clear()
            Fields.Clear()
            Select Case Id
                Case "give"
                    LabTitle.Text = "给予物品"
                    LabDesc.Text = "给自己或别人发物品。"
                    AddRow("目标", "target", "combo", {"@p", "@a", "@r", "@s"})
                    AddRow("物品", "item", "combo", EasyCmds.Items)
                    AddRow("数量", "count", "text", {"1"})
                Case "effect"
                    LabTitle.Text = "药水效果"
                    LabDesc.Text = "给自己或别人加 buff / debuff。"
                    AddRow("目标", "target", "combo", {"@p", "@a", "@r", "@s"})
                    AddRow("效果", "effect", "combo", EasyCmds.Effects)
                    AddRow("持续秒数", "seconds", "text", {"60"})
                    AddRow("等级（0 = I 级）", "amplifier", "text", {"0"})
                Case "gamemode"
                    LabTitle.Text = "游戏模式"
                    LabDesc.Text = "切换生存 / 创造 / 冒险 / 旁观。"
                    AddRow("目标", "target", "combo", {"@p", "@a", "@r", "@s"})
                    AddRow("模式", "mode", "combo", {"survival", "creative", "adventure", "spectator"})
                Case "difficulty"
                    LabTitle.Text = "世界难度"
                    LabDesc.Text = "设置当前世界的难度。"
                    AddRow("难度", "difficulty", "combo", {"peaceful", "easy", "normal", "hard"})
                Case "tp"
                    LabTitle.Text = "传送"
                    LabDesc.Text = "把玩家传送到坐标；~ 表示相对当前坐标。"
                    AddRow("目标", "target", "combo", {"@p", "@a", "@r", "@s"})
                    AddRow("X", "x", "text", {"~"})
                    AddRow("Y", "y", "text", {"~"})
                    AddRow("Z", "z", "text", {"~"})
                Case "time"
                    LabTitle.Text = "时间"
                    LabDesc.Text = "设置世界时间；也可以填数字（0~24000）。"
                    AddRow("时间", "time", "combo", {"day", "noon", "night", "midnight"})
                Case "weather"
                    LabTitle.Text = "天气"
                    LabDesc.Text = "切换晴天 / 下雨 / 雷暴。"
                    AddRow("天气", "weather", "combo", {"clear", "rain", "thunder"})
                    AddRow("持续秒数（可留空）", "duration", "text", {""})
                Case "gamerule"
                    LabTitle.Text = "游戏规则"
                    LabDesc.Text = "常用规则，比如死亡不掉落。"
                    AddRow("规则", "rule", "combo", EasyCmds.GameRules)
                    AddRow("值", "value", "combo", {"true", "false"})
                Case "summon"
                    LabTitle.Text = "生成实体"
                    LabDesc.Text = "在坐标处生成一个实体。"
                    AddRow("实体", "entity", "combo", EasyCmds.Entities)
                    AddRow("X", "x", "text", {"~"})
                    AddRow("Y", "y", "text", {"~"})
                    AddRow("Z", "z", "text", {"~"})
                Case "locate"
                    LabTitle.Text = "查找结构 / 生物群系"
                    LabDesc.Text = "定位最近的结构或生物群系（生物群系需要 1.19+）。"
                    AddRow("类型", "kind", "combo", {"structure", "biome"})
                    AddRow("名称", "id", "combo", EasyCmds.Structures)
            End Select
            UpdatePreview()
        End Sub

        Private Sub AddRow(Label As String, Key As String, Kind As String, Options As String())
            Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(120)})
            Row.ColumnDefinitions.Add(New ColumnDefinition)
            Dim Lab As New TextBlock With {.Text = Label, .VerticalAlignment = VerticalAlignment.Center, .Foreground = New SolidColorBrush(Color.FromRgb(&H44, &H44, &H44))}
            Grid.SetColumn(Lab, 0)
            Row.Children.Add(Lab)
            Dim Ctrl As Control
            If Kind = "combo" Then
                Dim Cb As New ComboBox With {.IsEditable = True, .Padding = New Thickness(6, 3, 6, 3)}
                For Each O As String In Options
                    Cb.Items.Add(O)
                Next
                If Cb.Items.Count > 0 Then Cb.Text = Options(0)
                AddHandler Cb.SelectionChanged, Sub(s, e) UpdatePreview()
                AddHandler Cb.LostFocus, Sub(s, e) UpdatePreview()
                Ctrl = Cb
            Else
                Dim Tb As New TextBox With {.Text = If(Options.Length > 0, Options(0), ""), .Padding = New Thickness(6, 3, 6, 3)}
                AddHandler Tb.TextChanged, Sub(s, e) UpdatePreview()
                Ctrl = Tb
            End If
            Grid.SetColumn(Ctrl, 1)
            Row.Children.Add(Ctrl)
            PanForm.Children.Add(Row)
            Fields(Key) = Ctrl
        End Sub

        Private Function ReadFields() As Dictionary(Of String, String)
            Dim Result As New Dictionary(Of String, String)
            For Each Pair In Fields
                Dim Cb = TryCast(Pair.Value, ComboBox)
                If Cb IsNot Nothing Then
                    Result(Pair.Key) = Cb.Text
                Else
                    Result(Pair.Key) = CType(Pair.Value, TextBox).Text
                End If
            Next
            Return Result
        End Function

        Private Sub UpdatePreview()
            Try
                TxtPreview.Text = "/" & EasyCmds.BuildCommand(CurrentId, ReadFields())
                LabStatus.Text = ""
            Catch ex As Exception
                TxtPreview.Text = ""
            End Try
        End Sub

    End Class

End Namespace
