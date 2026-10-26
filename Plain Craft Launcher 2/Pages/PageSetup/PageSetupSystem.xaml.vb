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
        InitAccelerationUi()
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
                    "一般选择 仅在有重大漏洞更新时提示 就可以让你尽量不受打扰了。" & vbCrLf &
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

#Region "网络加速"

    Private _accelerationUiBound As Boolean = False

    ''' <summary>
    ''' 初始化「网络加速」区域的 UI 状态。
    ''' 只在首次加载页面时执行一次，避免重复绑定日志事件。
    ''' </summary>
    Private Sub InitAccelerationUi()
        If _accelerationUiBound Then Return
        _accelerationUiBound = True

        '回读状态
        CheckAccelerationEnabled.Checked = Settings.Get(Of Boolean)("AccelerationEnabled")
        TextAccelerationSubscription.Text = Settings.Get(Of String)("AccelerationSubscriptionUrl")
        TextAccelerationAutoUpdate.Text = Settings.Get(Of Integer)("AccelerationAutoUpdateHours").ToString()

        '实时日志
        AddHandler ModAcceleration.Xray.LogReceived,
            Sub(msg)
                RunInUi(
                Sub()
                    If TextAccelerationLog Is Nothing Then Return
                    TextAccelerationLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{vbCrLf}")
                    TextAccelerationLog.ScrollToEnd()
                End Sub)
            End Sub

        '代理已在运行（例如从其他页面启动后返回本页）时同步 UI
        If ModAcceleration.Xray.IsRunning Then
            BtnAccelerationToggle.Text = "停止加速"
            TextAccelerationStatus.Text = $"代理运行中 :{Settings.Get(Of Integer)("AccelerationPort")}"
            TextAccelerationStatus.Opacity = 1
        Else
            BtnAccelerationToggle.Text = "启动加速"
            TextAccelerationStatus.Text = "未启动"
            TextAccelerationStatus.Opacity = 0.6
        End If
    End Sub

    ''' <summary>
    ''' 总开关勾选状态变化。
    ''' 勾选本身不代表代理已启动，只是记录用户意图；实际启停由下方按钮负责。
    ''' </summary>
    Private Sub CheckAccelerationEnabled_Change() Handles CheckAccelerationEnabled.Change
        If AniControlEnabled <> 0 Then Return
        Settings.Set("AccelerationEnabled", CheckAccelerationEnabled.Checked)
        If CheckAccelerationEnabled.Checked AndAlso Not ModAcceleration.Xray.IsRunning Then
            Hint("已记录加速开关。请点击下方「启动加速」按钮以实际启动代理。", HintType.Blue)
        End If
    End Sub

    ''' <summary>
    ''' 导入外部订阅到 xray-knife 的订阅数据库。
    ''' </summary>
    Private Async Sub BtnAccelerationImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAccelerationImport.Click
        Dim url = If(TextAccelerationSubscription.Text, "").Trim()
        If String.IsNullOrEmpty(url) Then
            Hint("请先在上方输入外部订阅链接", HintType.Blue)
            Return
        End If
        BtnAccelerationImport.IsEnabled = False
        Try
            ModAcceleration.EnsureAccelerationReady()
            Dim added = Await ModAcceleration.Xray.ImportExternalSubscriptionAsync(url)
            If added Then
                Settings.Set("AccelerationSubscriptionUrl", url)
                Hint("外部订阅已导入，请点击「拉取节点并测速择优」使其生效。", HintType.Green)
            Else
                Hint("该订阅已存在于数据库中，未重复导入。", HintType.Blue)
            End If
        Catch ex As Exception
            Hint("导入外部订阅失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "导入外部订阅失败")
        Finally
            BtnAccelerationImport.IsEnabled = True
        End Try
    End Sub

    ''' <summary>
    ''' 从所有已导入的订阅（含内置节点池）拉取节点并测速择优。
    ''' </summary>
    Private Async Sub BtnAccelerationRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAccelerationRefresh.Click
        BtnAccelerationRefresh.IsEnabled = False
        Dim oldStatus = TextAccelerationStatus.Text
        Dim oldOpacity = TextAccelerationStatus.Opacity
        TextAccelerationStatus.Text = "正在拉取节点并测速，请稍候..."
        TextAccelerationStatus.Opacity = 1
        Try
            ModAcceleration.EnsureAccelerationReady()
            Dim count = Await ModAcceleration.Xray.RefreshAndPrepareAsync(Settings.Get(Of Integer)("AccelerationTopN"))
            Hint($"已拉取节点并测速，择优保留 {count} 个节点。", HintType.Green)
            TextAccelerationStatus.Text = If(ModAcceleration.Xray.IsRunning,
                $"代理运行中 :{Settings.Get(Of Integer)("AccelerationPort")}",
                "节点已就绪，可点击「启动加速」")
            TextAccelerationStatus.Opacity = 1
        Catch ex As Exception
            Hint("测速择优失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "测速择优失败")
            TextAccelerationStatus.Text = "测速失败"
            TextAccelerationStatus.Opacity = 0.6
        Finally
            BtnAccelerationRefresh.IsEnabled = True
        End Try
    End Sub

    ''' <summary>
    ''' 启动或停止 Xray 代理。
    ''' 启动后需要通知 DownloadHttpClientFactory 重建下载专用 HttpClient。
    ''' </summary>
    Private Sub BtnAccelerationToggle_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAccelerationToggle.Click
        Try
            ModAcceleration.EnsureAccelerationReady()
            If ModAcceleration.Xray.IsRunning Then
                ModAcceleration.Xray.StopProxy()
                DownloadHttpClientFactory.NotifyAccelerationChanged(False, 0)
                BtnAccelerationToggle.Text = "启动加速"
                TextAccelerationStatus.Text = "未启动"
                TextAccelerationStatus.Opacity = 0.6
                CheckAccelerationEnabled.Checked = False
                Settings.Set("AccelerationEnabled", False)
            Else
                Dim port = Settings.Get(Of Integer)("AccelerationPort")
                ModAcceleration.Xray.StartProxy(port)
                DownloadHttpClientFactory.NotifyAccelerationChanged(True, port)
                BtnAccelerationToggle.Text = "停止加速"
                TextAccelerationStatus.Text = $"代理运行中 :{port}"
                TextAccelerationStatus.Opacity = 1
                CheckAccelerationEnabled.Checked = True
                Settings.Set("AccelerationEnabled", True)
            End If
        Catch ex As Exception
            Hint("启动加速失败：" & ex.Message & vbCrLf &
                 "请先点击「拉取节点并测速择优」生成可用节点，并确认端口 " &
                 Settings.Get(Of Integer)("AccelerationPort") & " 未被占用。", HintType.Red)
            Logger.Warn(ex, "启动加速失败")
        End Try
    End Sub

    ''' <summary>
    ''' 自动更新间隔变化。
    ''' </summary>
    Private Sub TextAccelerationAutoUpdate_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextAccelerationAutoUpdate.TextChanged
        If AniControlEnabled <> 0 Then Return
        Dim hours As Integer
        If Not Integer.TryParse(TextAccelerationAutoUpdate.Text, hours) Then Return
        If hours < 0 Then Return
        Settings.Set("AccelerationAutoUpdateHours", hours)
        If hours > 0 Then
            ModAcceleration.Xray.EnableAutoUpdate(hours, Settings.Get(Of Integer)("AccelerationTopN"))
        Else
            ModAcceleration.Xray.DisableAutoUpdate()
        End If
    End Sub

#End Region

End Class
