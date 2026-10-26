Imports System.IO
Imports System.Reflection
Imports PCL.Acceleration

Public Module ModAcceleration

    Friend Xray As New XrayService()

    ' 内置节点池地址（硬编码，确保开箱即用）
    Friend Const BuiltinSubscriptionUrl As String =
        "https://raw.githubusercontent.com/chengaopan/AutoMergePublicNodes/master/list.yml"

    Private _initialized As Boolean

    Friend Sub EnsureAccelerationReady()
        If _initialized Then Return
        Dim dir = Path.Combine(PathTemp, "Xray")
        Directory.CreateDirectory(dir)
        Dim knifePath = Path.Combine(dir, "xray-knife.exe")
        Dim corePath = Path.Combine(dir, "xray-core.exe")
        ExtractIfMissing("PCL.Resources.Xray.xray-knife.exe", knifePath)
        ExtractIfMissing("PCL.Resources.Xray.xray-core.exe", corePath)
        Xray.Initialize(dir, knifePath, corePath)
        _initialized = True
    End Sub

    Private Sub ExtractIfMissing(resourceName As String, destPath As String)
        If File.Exists(destPath) Then Return
        Using stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            If stream Is Nothing Then Throw New FileNotFoundException($"嵌入资源缺失: {resourceName}")
            Using fs = File.Create(destPath)
                stream.CopyTo(fs)
            End Using
        End Using
    End Sub

    ''' <summary>启动时调用：注册内置订阅 + 按设置决定是否自动更新/自动启动代理。</summary>
    Friend Async Function BootstrapAccelerationAsync() As Task
        EnsureAccelerationReady()
        Await Xray.EnsureBuiltinSubscriptionAsync(BuiltinSubscriptionUrl)

        If Settings.AccelerationEnabled Then
            Try
                Await Xray.RefreshAndPrepareAsync(Settings.AccelerationTopN)
                Xray.StartProxy(Settings.AccelerationPort)
                HttpWebProxyFactory.NotifyAccelerationChanged(True)
            Catch ex As Exception
                ' 失败时静默降级，不阻塞启动
                Debug.WriteLine($"[加速] 自动启动失败: {ex.Message}")
            End Try
        End If

        If Settings.AccelerationAutoUpdateHours > 0 Then
            Xray.EnableAutoUpdate(Settings.AccelerationAutoUpdateHours, Settings.AccelerationTopN)
        End If
    End Function

End Module
