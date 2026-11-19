# Libraries inside Sentinel.exe

`build.bat` packs these files into the exe as resources. They come unchanged from the NuGet package [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) 1.0.4314.35, BSD license ([WebView2-LICENSE.txt](WebView2-LICENSE.txt)).

| File | From the package |
|------|------------------|
| Microsoft.Web.WebView2.Core.dll, Microsoft.Web.WebView2.WinForms.dll | `lib/net462` |
| WebView2Loader.x64.dll, WebView2Loader.arm64.dll, WebView2Loader.x86.dll | `runtimes/win-x64`, `win-arm64`, `win-x86` `/native/WebView2Loader.dll` |

The loader is native code, so Windows can't load it from memory: the first time the settings window opens, Sentinel writes the one for your processor to `%LOCALAPPDATA%\Sentinel\web\WebView2Loader.dll` (in TEMP for the portable version).

The window's fonts sit in `ui/fonts/`: Geist and Geist Mono (SIL OFL 1.1, [Geist-LICENSE.txt](../ui/fonts/Geist-LICENSE.txt)) and Instrument Serif italic (SIL OFL 1.1, [InstrumentSerif-LICENSE.txt](../ui/fonts/InstrumentSerif-LICENSE.txt)), the latin subsets from Fontsource 5.3.0, the same as davide-mariotti.github.io. The icons in `ui/icons.js` are Lucide 1.74.0 (ISC).
