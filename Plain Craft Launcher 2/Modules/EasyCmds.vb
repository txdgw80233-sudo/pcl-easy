Imports System.Runtime.InteropServices
Imports System.Windows.Input

''' <summary>
''' 指令工坊：按当前运行的游戏版本生成指令，并提供全局热键唤起的小窗口。
''' </summary>
Public Module EasyCmds

    Public Const StudioHotkeyText As String = "Ctrl+Alt+M"
    Public StudioHotkeyActual As String = ""

    ''' <summary>常见物品（key 为注册名，不含 minecraft:）</summary>
    Public ReadOnly Items As String() = {
        "diamond_sword", "diamond_pickaxe", "diamond_axe", "diamond_shovel", "diamond_helmet", "diamond_chestplate",
        "diamond_leggings", "diamond_boots", "netherite_sword", "netherite_pickaxe", "netherite_chestplate",
        "iron_sword", "iron_pickaxe", "iron_helmet", "iron_chestplate", "bow", "crossbow", "arrow", "shield",
        "golden_apple", "enchanted_golden_apple", "bread", "cooked_beef", "ender_pearl", "ender_eye",
        "torch", "oak_planks", "cobblestone", "dirt", "stone", "glass", "oak_log", "coal", "iron_ingot",
        "gold_ingot", "diamond", "netherite_ingot", "emerald", "redstone", "lapis_lazuli", "stick",
        "crafting_table", "furnace", "chest", "hopper", "anvil", "enchanting_table", "beacon", "tnt", "water_bucket"}

    Public ReadOnly Effects As String() = {
        "speed", "slowness", "haste", "mining_fatigue", "strength", "instant_health", "instant_damage",
        "jump_boost", "nausea", "regeneration", "resistance", "fire_resistance", "water_breathing",
        "invisibility", "blindness", "night_vision", "hunger", "weakness", "poison", "wither",
        "health_boost", "absorption", "saturation", "glowing", "levitation", "slow_falling", "luck"}

    Public ReadOnly Entities As String() = {
        "zombie", "skeleton", "creeper", "spider", "enderman", "witch", "slime", "blaze", "ghast",
        "pig", "cow", "sheep", "chicken", "horse", "wolf", "cat", "villager", "iron_golem",
        "item", "armor_stand", "boat", "minecart", "tnt", "lightning_bolt", "fireball"}

    Public ReadOnly Structures As String() = {
        "village", "desert_pyramid", "jungle_temple", "stronghold", "monument", "mansion", "mineshaft",
        "fortress", "bastion_remnant", "end_city", "shipwreck", "ruined_portal", "ocean_ruin", "buried_treasure"}

    Public ReadOnly GameRules As String() = {
        "keepInventory", "doDaylightCycle", "doWeatherCycle", "doMobSpawning", "mobGriefing",
        "doFireTick", "showCoordinates", "naturalRegeneration", "doImmediateRespawn",
        "spawnRadius", "randomTickSpeed", "commandBlockOutput", "sendCommandFeedback", "fallDamage"}


    Private Const MOD_ALT As UInteger = 1UI
    Private Const MOD_CONTROL As UInteger = 2UI
    Private Const MOD_SHIFT As UInteger = 4UI
    Private Const WM_HOTKEY As Integer = &H312
    Private HotkeyId As Integer = &H4D43
    Private StudioHook As Interop.HwndSource

    <DllImport("user32.dll", SetLastError:=True)> Private Function RegisterHotKey(hWnd As IntPtr, id As Integer, fsModifiers As UInteger, vk As UInteger) As Boolean
    End Function
    <DllImport("user32.dll")> Private Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As Boolean
    End Function

    Public Sub RegisterStudioHotkey(Owner As System.Windows.Window)
        Try
            Dim Helper As New Interop.WindowInteropHelper(Owner)
            Dim Handle = Helper.Handle
            StudioHook = Interop.HwndSource.FromHwnd(Handle)
            If StudioHook Is Nothing Then
                Logger.Warn("[指令] 主窗口句柄不可用，热键未注册")
                Return
            End If
            StudioHook.AddHook(AddressOf StudioWndProc)
            Dim Candidates As String()() = {
                New String() {"Ctrl+Alt+M", (MOD_CONTROL Or MOD_ALT).ToString(), &H4D.ToString()},
                New String() {"Ctrl+Alt+K", (MOD_CONTROL Or MOD_ALT).ToString(), &H4B.ToString()},
                New String() {"Ctrl+Shift+M", (MOD_CONTROL Or MOD_SHIFT).ToString(), &H4D.ToString()},
                New String() {"Alt+M", MOD_ALT.ToString(), &H4D.ToString()}}
            For Each C As String() In Candidates
                If RegisterHotKey(Handle, HotkeyId, CUInt(C(1)), CUInt(C(2))) Then
                    StudioHotkeyActual = C(0)
                    Logger.Info("[指令] 已注册全局热键 " & C(0))
                    Return
                End If
            Next
            Logger.Warn("[指令] 所有热键候选都被占用，热键不可用（仍可从百宝箱打开）")
        Catch ex As Exception
            Logger.Warn("[指令] 热键注册异常：" & ex.Message)
        End Try
    End Sub

    Private Function StudioWndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        If msg = WM_HOTKEY AndAlso wParam.ToInt32() = HotkeyId Then
            Try
                Windows.CommandStudio.ToggleStudio()
            Catch ex As Exception
                Logger.Warn("[指令] 呼出失败：" & ex.Message)
            End Try
            handled = True
        End If
        Return IntPtr.Zero
    End Function

    ''' <summary>当前选中（或最后启动）的版本号，例如 1.21.11；识别不到就用 1.21.11。</summary>
    Public Function CurrentVersion() As String
        Try
            If McInstanceSelected IsNot Nothing AndAlso McInstanceSelected.Name <> "" Then
                Return StripLoader(McInstanceSelected.Name)
            End If
        Catch
        End Try
        Return "1.21.11"
    End Function

    ''' <summary>把 1.21.11-Fabric 这类名字还原成 1.21.11。</summary>
    Public Function StripLoader(Name As String) As String
        Dim Suffix As String() = {"-Fabric", "-Forge", "-NeoForge", "-OptiFine", "-LiteLoader", "-Quilt", "-FabricAPI"}
        For Each S As String In Suffix
            If Name.EndsWith(S, StringComparison.OrdinalIgnoreCase) Then Return Name.Substring(0, Name.Length - S.Length)
        Next
        Return Name
    End Function

    ''' <summary>比较版本号（只比较数字段），v >= Target。</summary>
    Public Function VersionAtLeast(v As String, Target As String) As Boolean
        Dim A = v.Split("."c), B = Target.Split("."c)
        For i = 0 To Math.Max(A.Length, B.Length) - 1
            Dim x = 0, y = 0
            If i < A.Length Then Integer.TryParse(A(i), x)
            If i < B.Length Then Integer.TryParse(B(i), y)
            If x > y Then Return True
            If x < y Then Return False
        Next
        Return True
    End Function

    ''' <summary>生成一条指令。Params 里放界面上的各字段值。</summary>
    Public Function BuildCommand(Id As String, Params As Dictionary(Of String, String)) As String
        Dim Target = GetP(Params, "target", "@p")
        Dim Ver = CurrentVersion()
        Select Case Id
            Case "give"
                Dim Item = GetP(Params, "item", "diamond_sword")
                Dim Count = GetP(Params, "count", "1")
                Return "give " & Target & " minecraft:" & Item & " " & Count
            Case "effect"
                Dim Eff = GetP(Params, "effect", "speed")
                Dim Sec = GetP(Params, "seconds", "60")
                Dim Amp = GetP(Params, "amplifier", "0")
                If VersionAtLeast(Ver, "1.13") Then
                    Return "effect give " & Target & " minecraft:" & Eff & " " & Sec & " " & Amp
                Else
                    Return "effect " & Target & " " & Eff & " " & Sec & " " & Amp
                End If
            Case "gamemode"
                Return "gamemode " & GetP(Params, "mode", "creative") & " " & Target
            Case "difficulty"
                Return "difficulty " & GetP(Params, "difficulty", "peaceful")
            Case "tp"
                Return "tp " & Target & " " & GetP(Params, "x", "~") & " " & GetP(Params, "y", "~") & " " & GetP(Params, "z", "~")
            Case "time"
                Return "time set " & GetP(Params, "time", "day")
            Case "weather"
                Dim W = GetP(Params, "weather", "clear")
                Dim D = GetP(Params, "duration", "")
                If D = "" Then Return "weather " & W
                Return "weather " & W & " " & D
            Case "gamerule"
                Return "gamerule " & GetP(Params, "rule", "keepInventory") & " " & GetP(Params, "value", "true")
            Case "summon"
                Dim E = GetP(Params, "entity", "zombie")
                Return "summon minecraft:" & E & " " & GetP(Params, "x", "~") & " " & GetP(Params, "y", "~") & " " & GetP(Params, "z", "~")
            Case "locate"
                Dim Kind = GetP(Params, "kind", "structure")
                If Kind = "biome" AndAlso Not VersionAtLeast(Ver, "1.19") Then Kind = "structure"
                Return "locate " & Kind & " " & GetP(Params, "id", "village")
            Case Else
                Return ""
        End Select
    End Function

    Private Function GetP(Params As Dictionary(Of String, String), Key As String, DefaultValue As String) As String
        If Params Is Nothing Then Return DefaultValue
        If Params.ContainsKey(Key) AndAlso Params(Key) <> "" Then Return Params(Key)
        Return DefaultValue
    End Function

    ''' <summary>复制到剪贴板。</summary>
    Public Sub CopyCommand(Text As String)
        Try
            RunInUi(Sub() Clipboard.SetText(Text))
        Catch ex As Exception
            Logger.Warn("[指令] 复制失败：" & ex.Message)
        End Try
    End Sub

    <DllImport("user32.dll")> Private Function GetForegroundWindow() As IntPtr
    End Function
    <DllImport("user32.dll")> Private Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef pid As Integer) As Integer
    End Function
    <DllImport("user32.dll")> Private Sub keybd_event(bVk As Byte, bScan As Byte, dwFlags As Integer, dwExtraInfo As UIntPtr)
    End Sub
    Private Const KEYEVENTF_KEYUP As Integer = 2

    Private Sub PressKey(Vk As Integer)
        keybd_event(CByte(Vk), 0, 0, UIntPtr.Zero)
        Threading.Thread.Sleep(30)
        keybd_event(CByte(Vk), 0, KEYEVENTF_KEYUP, UIntPtr.Zero)
        Threading.Thread.Sleep(60)
    End Sub

    ''' <summary>
    ''' 尝试把指令直接打进游戏聊天框：要求当前前台窗口属于 java/javaw 进程。
    ''' 返回是否成功（失败时调用方提示用户手动粘贴）。
    ''' </summary>
    Public Function TryPasteToGame(Text As String) As Boolean
        Try
            Dim Hwnd = GetForegroundWindow()
            If Hwnd = IntPtr.Zero Then Return False
            Dim Pid As Integer
            GetWindowThreadProcessId(Hwnd, Pid)
            Dim Proc = Diagnostics.Process.GetProcessById(Pid)
            Dim N = Proc.ProcessName.ToLowerInvariant()
            If N <> "javaw" AndAlso N <> "java" Then Return False
            RunInUi(Sub() Clipboard.SetText(Text))
            Threading.Thread.Sleep(150)
            PressKey(&H54) ' T
            PressKey(&H11) ' Ctrl
            ' Ctrl+V
            keybd_event(&H11, 0, 0, UIntPtr.Zero)
            PressKey(&H56)
            keybd_event(&H11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero)
            Threading.Thread.Sleep(120)
            PressKey(&HD) ' Enter
            Return True
        Catch ex As Exception
            Logger.Warn("[指令] 自动输入失败：" & ex.Message)
            Return False
        End Try
    End Function

End Module
