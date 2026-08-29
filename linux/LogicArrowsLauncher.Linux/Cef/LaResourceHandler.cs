using System.Runtime.InteropServices;
using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// Ответ CEF из памяти: статус, mime и заголовки задаются один раз,
/// тело отдаётся порциями в Read. Аналог CreateWebResourceResponse в WebView2.
/// </summary>
public sealed class LaResourceHandler : CefResourceHandler
{
    private byte[] body;
    private int readPosition;

    public LaResourceHandler(byte[] body, int status, string mimeType, string? extraHeaders = null)
    {
        this.body = body;
        Status = status;
        MimeType = mimeType;
        ExtraHeaders = extraHeaders;
    }

    public int Status { get; }
    public string MimeType { get; }
    public string? ExtraHeaders { get; }

    protected override bool Open(CefRequest request, ref int handleRequest, CefCallback callback)
    {
        // 1 = перехватили запрос полностью, отдаём ответ сами.
        handleRequest = 1;
        return true;
    }

    protected override void GetResponseHeaders(CefResponse response, ref long responseLength, ref string? redirectUrl)
    {
        response.Status = Status;
        response.StatusText = Status == 200 ? "OK" : "Not Found";
        response.MimeType = MimeType;
        if (ExtraHeaders is not null)
        {
            foreach (var header in ExtraHeaders.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var sep = header.IndexOf(':');
                if (sep <= 0) continue;
                var name = header[..sep].Trim();
                var value = header[(sep + 1)..].Trim();
                response.SetHeaderByName(name, value, false);
            }
        }
        responseLength = body.Length;
    }

    protected override bool Read(IntPtr dataOut, int bytesToRead, ref int bytesRead, CefResourceReadCallback callback)
    {
        if (readPosition >= body.Length)
        {
            bytesRead = 0;
            return false;
        }
        var remaining = body.Length - readPosition;
        var chunk = Math.Min(remaining, bytesToRead);
        Marshal.Copy(body, readPosition, dataOut, chunk);
        readPosition += chunk;
        bytesRead = chunk;
        return true;
    }

    protected override void Cancel()
    {
    }

    public static LaResourceHandler Ok(byte[] body, string mimeType, string? extraHeaders = null)
        => new(body, 200, mimeType, extraHeaders);

    public static LaResourceHandler NotFound()
        => new("not found"u8.ToArray(), 404, "text/plain");
}
