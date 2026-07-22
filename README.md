# HappyGopher

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/JoyfulReaper/HappyGopher)](LICENSE)
[![GitHub Repo](https://img.shields.io/badge/GitHub-JoyfulReaper%2FHappyGopher-181717?logo=github)](https://github.com/JoyfulReaper/HappyGopher)

A small, static Gopher server for Windows, built with C# and .NET 10.

HappyGopher serves files and directory menus over the classic [Gopher protocol](https://en.wikipedia.org/wiki/Gopher_%28protocol%29). It can run directly from the console during development or as a Windows Service for long-running installations.

No web framework. No database. No JavaScript. Just a TCP listener, a folder full of content, and a protocol from a simpler time.

## Live Demo

A live Gopher site powered by HappyGopher is available at:

```text
gopher://gopher.kgivler.com/
```

You can connect with any Gopher client:

```text
gopher gopher.kgivler.com
```

Or, if your build of `curl` supports Gopher:

```powershell
curl.exe gopher://gopher.kgivler.com/
```

The live site is intentionally small and plain text, and serves as a real-world example of HappyGopher hosting static Gopher content.


## Features

* Serves static text, images, and binary files over Gopher
* Supports custom `gophermap` menu files
* Automatically generates directory menus when no `gophermap` exists
* Runs as a console application or Windows Service
* Configurable listening address and port
* Configurable public hostname used in generated menus
* Limits concurrent connections
* Enforces selector length and request timeout limits
* Blocks directory traversal attempts
* Blocks access through symbolic links and other reparse points
* Gracefully waits for active connections during shutdown
* Uses standard .NET configuration and logging

## Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* Windows is the primary supported platform

The server uses TCP port `70` by default, the standard Gopher port.

## Getting Started

Clone the repository:

```powershell
git clone https://github.com/JoyfulReaper/HappyGopher.git
cd HappyGopher
```

Run the server:

```powershell
dotnet run --project .\HappyGopher\HappyGopher.csproj
```

The default checked-in configuration listens on loopback at port `70`.

```text
127.0.0.1:70
```

With this default, HappyGopher accepts local connections only. To serve other machines, change `ListenAddress` to an address such as `0.0.0.0` and review your firewall rules.

## Configuration

Configuration lives in:

```text
HappyGopher/appsettings.json
```

The default configuration resembles:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Gopher": {
    "ListenAddress": "127.0.0.1",
    "Port": 70,
    "PublicHost": "127.0.0.1",
    "ContentRoot": "content",
    "MaxConcurrentConnections": 64,
    "MaxSelectorBytes": 4096,
    "RequestTimeoutSeconds": 15
  }
}
```

### Configuration options

| Setting                    |     Default | Description                                                      |
| -------------------------- | ----------: | ---------------------------------------------------------------- |
| `ListenAddress`            | `127.0.0.1` | Local IP address on which the TCP server listens.                |
| `Port`                     |        `70` | TCP port used by the server.                                     |
| `PublicHost`               | `127.0.0.1` | Hostname or IP address advertised inside generated Gopher menus. |
| `ContentRoot`              |   `content` | Directory containing files and `gophermap` menus.                |
| `MaxConcurrentConnections` |        `64` | Maximum number of requests handled concurrently.                 |
| `MaxSelectorBytes`         |      `4096` | Maximum permitted selector length.                               |
| `RequestTimeoutSeconds`    |        `15` | Time allowed for a client to send its request.                   |

`ListenAddress` and `PublicHost` serve different purposes:

* `ListenAddress` controls where HappyGopher accepts connections. The checked-in default is loopback-only, not all interfaces.
* `PublicHost` is the address placed into menu entries returned to clients.

For a public server, set `PublicHost` to the hostname clients actually use:

```json
"PublicHost": "gopher.example.com"
```

For a public server, bind to the interface that should accept client connections:

```json
"ListenAddress": "0.0.0.0"
```

## Adding Content

Place files inside:

```text
HappyGopher/content
```

The included example content is structured like this:

```text
content/
├── gophermap
├── about.txt
└── downloads/
    ├── gophermap
    └── readme.txt
```

Text files are sent using Gopher text-file formatting. Other files are streamed directly to the client.

When a directory does not contain a `gophermap`, HappyGopher generates a menu by scanning its files and subdirectories. Directories are listed before files, entries are sorted by name, and file item types are inferred from file extensions.

The project file recursively copies `HappyGopher/content/**` into both build output and publish output, so the sample content stays beside the executable during normal `dotnet build` and `dotnet publish` workflows.

## Gophermaps

A `gophermap` controls how a directory appears to Gopher clients.

Example:

```text
iWelcome to my Gopher server
i
0About this server<TAB>about.txt
1Downloads<TAB>downloads
hProject on GitHub<TAB>URL:https://github.com/JoyfulReaper/HappyGopher
```

Replace each `<TAB>` marker with an actual tab character.

HappyGopher automatically fills in the configured public hostname and port for local entries.

Relative selectors are resolved relative to the directory containing the `gophermap`. Absolute selectors, external hosts, and explicit valid ports are preserved. Invalid explicit ports fall back to the configured server port.

Tabs, carriage returns, and newlines in generated menu fields are replaced with spaces before being written to the wire.

### Common item types

| Type | Meaning              |
| :--: | -------------------- |
|  `i` | Informational text   |
|  `0` | Text file            |
|  `1` | Directory or menu    |
|  `3` | Error                |
|  `9` | Binary file          |
|  `g` | GIF image            |
|  `I` | Other image          |
|  `h` | HTML or external URL |

Comments may be added by beginning a line with `#`:

```text
# This line is ignored
iThis line is displayed
```

## Testing the Server

Run the automated test suite:

```powershell
dotnet test
```

Use a Gopher client and connect to:

```text
gopher://127.0.0.1/
```

You can also test the raw TCP response from PowerShell:

```powershell
$client = [System.Net.Sockets.TcpClient]::new("127.0.0.1", 70)
$stream = $client.GetStream()

$writer = [System.IO.StreamWriter]::new(
    $stream,
    [System.Text.Encoding]::ASCII,
    1024,
    $true)

$writer.NewLine = "`r`n"
$writer.WriteLine("")
$writer.Flush()

$reader = [System.IO.StreamReader]::new($stream)
$reader.ReadToEnd()

$client.Dispose()
```

An empty selector requests the root menu.

Some builds of `curl` also support Gopher:

```powershell
curl.exe gopher://127.0.0.1/
```

Protocol support depends on how that particular `curl` build was compiled.

## Publishing

Publish a framework-dependent Windows build:

```powershell
dotnet publish .\HappyGopher\HappyGopher.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output .\publish
```

HappyGopher intentionally uses a framework-dependent .NET deployment rather than Native AOT to preserve support for future runtime-loaded plugins and content providers.

The published `content` directory and `appsettings.json` should remain beside the executable. Content files under `HappyGopher/content` are copied recursively during publish.

Run the published server:

```powershell
.\publish\HappyGopher.exe
```

## Installing as a Windows Service

First publish the application to a permanent location, such as:

```text
C:\Services\HappyGopher
```

Create the service from an elevated PowerShell or Command Prompt:

```powershell
sc.exe create HappyGopher `
    binPath= "C:\Services\HappyGopher\HappyGopher.exe" `
    start= auto `
    DisplayName= "Happy Gopher Server"
```

Start it:

```powershell
sc.exe start HappyGopher
```

Stop it:

```powershell
sc.exe stop HappyGopher
```

Remove it:

```powershell
sc.exe delete HappyGopher
```

Update `appsettings.json` in the published directory before starting the service.

## Security

HappyGopher treats the configured content directory as a security boundary.

Requested selectors are normalized and checked to ensure that they remain inside the content root. Requests involving path traversal, symbolic links, junctions, or other reparse points are rejected.

Selectors are read as UTF-8 and are limited by byte count, not .NET character count. By default, selectors may be up to `4096` bytes and clients have `15` seconds to complete a request line.

That said, this is an early-stage project. Before exposing it publicly:

* Keep the .NET runtime and operating system updated.
* Serve only files intended for public access.
* Review your firewall and router configuration.
* Avoid placing secrets anywhere inside the content directory.
* Run the service under a restricted Windows account where practical.

Gopher does not provide encryption. Traffic, selectors, and downloaded content are sent in plaintext.

## Current Limitations

* Static files only
* No authentication or access control
* No TLS support
* Gopher traffic is plaintext
* No dynamic scripts or CGI handlers
* No Gopher search handlers
* No administrative interface
* No official packaged releases yet
* Primarily developed and tested for Windows

## Project Structure

```text
HappyGopher.slnx
├── HappyGopher/
│   ├── Program.cs
│   ├── HappyGopherWorker.cs
│   ├── GopherSelectorReader.cs
│   ├── GopherPathSecurity.cs
│   ├── GopherMenuFields.cs
│   ├── HappyGopherOptions.cs
│   ├── GopherContentStore.cs
│   ├── HappyGopher.csproj
│   ├── appsettings.json
│   └── content/
├── HappyGopher.Tests/
│   ├── GopherSelectorReaderTests.cs
│   ├── GopherContentStoreTests.cs
│   ├── GopherPathSecurityTests.cs
│   ├── GopherMenuFieldsTests.cs
│   ├── HappyGopherIntegrationTests.cs
│   └── HappyGopher.Tests.csproj
└── LICENSE
```

## Contributing

Bug reports, testing, documentation fixes, and pull requests are welcome.

The project is intentionally small, so changes should favor straightforward code and minimal dependencies over elaborate infrastructure.

## License

HappyGopher is available under the [MIT License](LICENSE).

Copyright © 2026 Kyle Givler.
