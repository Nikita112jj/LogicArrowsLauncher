using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// CefApp: общий для главного и субпроцессов CEF.
/// Дополнительные флаги Chromium — для стабильного OSR-рендера на Linux.
/// </summary>
public sealed class LaApp : CefApp
{
    protected override void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
    {
        // OSR без окон: программный GL может конфликтовать с драйверами — рендерим софтом.
        commandLine.AppendSwitch("disable-gpu-compositing");
        commandLine.AppendSwitch("disable-gpu-shader-disk-cache");
    }
}
