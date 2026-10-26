' DownloadHttpClientFactory.vb
Friend Module DownloadHttpClientFactory

    Private ReadOnly _lock As New Object()
    Private _cachedClient As HttpClient
    Private _accelerationActive As Boolean
    Private _listenPort As Integer

    ''' <summary>Xray 加速启停时调用，强制重建下载客户端。</summary>
    Friend Sub NotifyAccelerationChanged(active As Boolean, port As Integer)
        SyncLock _lock
            _accelerationActive = active
            _listenPort = port
            _cachedClient?.Dispose()
            _cachedClient = Nothing
        End SyncLock
    End Sub

    ''' <summary>获取下载专用 HttpClient。加速开启时走 Xray SOCKS5，否则走原有代理逻辑。</summary>
    Friend Function GetDownloadClient() As HttpClient
        SyncLock _lock
            If _cachedClient IsNot Nothing Then Return _cachedClient

            Dim handler As New HttpClientHandler With {.UseProxy = False}

            If _accelerationActive AndAlso ModAcceleration.Xray.IsRunning Then
                handler.UseProxy = True
                handler.Proxy = New WebProxy($"socks5://127.0.0.1:{_listenPort}")
            Else
                ' 与全局网络层保持一致的“非加速”行为
                ApplyLegacyProxySettings(handler)
            End If

            _cachedClient = New HttpClient(handler)
            Return _cachedClient
        End SyncLock
    End Function

End Module
