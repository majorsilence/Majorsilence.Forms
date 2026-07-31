## Majorsilence.Forms Samples

### Explore

`Explore` is a clone of Windows' Explorer application.  It is available in the `Majorsilence.Forms` repository.

#### Windows

* Clone this repository
* Install .NET 6
  * https://dotnet.microsoft.com/download/dotnet-core
* Open `Majorsilence.Forms.sln` in Visual Studio 2022
* Ensure `Explore` is set as the Startup project
* Launch with F5

![Windows Explore Screenshot](explorer-windows.png "Windows Explore Screenshot")

#### Ubuntu 19.04 AMD64

* Clone this repository
* Install .NET 6
  * https://dotnet.microsoft.com/download/dotnet-core
* Navigate to `samples/Explorer`
* Run `dotnet run`

![Ubuntu Explore Screenshot](explorer-ubuntu.png "Ubuntu Explore Screenshot")

#### Mac OSX

* Clone this repository
* Install .NET 6
  * https://dotnet.microsoft.com/download/dotnet-core
* Navigate to `samples/Explorer`
* Run `dotnet run`

![Mac Explore Screenshot](explorer-osx.png "Mac Explore Screenshot")

### Outlaw

`Outlaw` is a clone of Microsoft's Outlook, showing off how `Majorsilence.Forms` can be used to create a complex modern application.

Follow the steps above for your system, replacing with `Outlaw` for the startup project or directory.

![Windows Outlaw Screenshot](outlaw-windows.png "Windows Outlaw Screenshot")


### WinFormsInterop (Windows-only)

Demonstrates bi-directional interop between `System.Windows.Forms` and Majorsilence.Forms in a
single process. The sample starts as a real WinForms host (Direction B: WF → MF) and each
opened Majorsilence window can in turn open legacy WinForms forms (Direction A: MF → WF).

See [WinForms Interop](winforms-interop.md) for full API documentation.

```bash
dotnet run --project samples/WinFormsInterop
```

### ControlGallery

`ControlGallery` shows off the various controls and features currently available in `Majorsilence.Forms`.

Follow the steps above for your system, replacing with `ControlGallery` for the startup project or directory.

![Windows ControlGallery Screenshot](controlgallery-windows.png "Windows ControlGallery Screenshot")

### Gallery.Android (Android-only, work in progress)

> ⚠️ Android/mobile support is early: it builds and boots the gallery, but hasn't seen the same real-device
> testing or control coverage as the desktop/browser backends. Expect rough edges.

Runs the same `ControlGallery` `MainForm` on the Avalonia backend's Android target, hosted by a single
Activity. Requires the `android` workload:

```bash
dotnet workload install android
dotnet build samples/Gallery.Android -p:EnableAndroidTarget=true -t:Run
```

Not part of the default solution build (`Majorsilence.Forms.slnx`), so a plain `dotnet build`/`dotnet
test` at the repo root never needs that workload — see the comment at the top of
`samples/Gallery.Android/Gallery.Android.csproj` for why, and `samples/WinFormsInterop` for the same
pattern applied to a different platform-specific sample.
