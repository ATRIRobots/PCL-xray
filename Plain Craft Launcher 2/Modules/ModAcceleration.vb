Imports System.IO
Imports System.Reflection
Imports PCL.Acceleration

Public Module ModAcceleration

    Friend Xray As New XrayService()

    ' 内置节点池地址（硬编码，确保开箱即用）
    Friend Const BuiltinSubscriptionUrl As String =
        "https://raw.githubusercontent.com/chengaopan/AutoMergePublicNodes/master/list.yml"

    Private _initialized As Boolean

    ''' <summary>
    ''' 确保加速组件已初始化，检查本地文件是否存在。
    ''' </summary>
    Friend Sub EnsureAccelerationReady()
        If _initialized Then Return

        ' 获取运行目录下的 Resources\Xray 文件夹
        Dim dir = Path.Combine(PathPure.Value, "Resources", "Xray")
        Directory.CreateDirectory(dir)

        Dim knifePath = Path.Combine(dir, "xray-knife.exe")
        Dim corePath = Path.Combine(dir, "xray-core.exe")

        ' 检查文件是否齐全
        If Not File.Exists(knifePath) OrElse Not File.Exists(corePath) Then
            Throw New FileNotFoundException(
                "未找到 Xray 核心组件。请确保 PCL 根目录下的 Resources\Xray\ 文件夹中包含了 xray-knife.exe 和 xray-core.exe。",
                dir)
        End If

        Xray.Initialize(dir, knifePath, corePath)
        _initialized = True
    End Sub

    ''' <summary>
    ''' 启动时调用：注册内置订阅 + 按设置决定是否自动更新/自动启动代理。
    ''' </summary>
    Friend Async Function BootstrapAccelerationAsync() As Task
        Try
            EnsureAccelerationReady()
            Await Xray.EnsureBuiltinSubscriptionAsync(BuiltinSubscriptionUrl)

            ' 如果设置了自动启动，则进行初始化拉取和代理启动
            If Settings.Get(Of Boolean)("AccelerationEnabled") Then
                Try
                    Await Xray.RefreshAndPrepareAsync(Settings.Get(Of Integer)("AccelerationTopN"))
                    Xray.StartProxy(Settings.Get(Of Integer)("AccelerationPort"))
                    ' 通知下载工厂重建专用 HttpClient
                    DownloadHttpClientFactory.NotifyAccelerationChanged(True, Settings.Get(Of Integer)("AccelerationPort"))
                Catch ex As Exception
                    Debug.WriteLine($"[加速] 自动启动失败: {ex.Message}")
                End Try
            End If

            ' 开启自动更新定时器
            If Settings.Get(Of Integer)("AccelerationAutoUpdateHours") > 0 Then
                Xray.EnableAutoUpdate(Settings.Get(Of Integer)("AccelerationAutoUpdateHours"), Settings.Get(Of Integer)("AccelerationTopN"))
            End If
        Catch ex As Exception
            ' 捕获组件缺失等异常，防止阻塞启动
            Debug.WriteLine($"[加速] 初始化失败: {ex.Message}")
        End Try
    End Function

End Module
