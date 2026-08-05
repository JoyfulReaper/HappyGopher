# HappyGopher

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/JoyfulReaper/HappyGopher)](LICENSE)
[![GitHub Repo](https://img.shields.io/badge/GitHub-JoyfulReaper%2FHappyGopher-181717?logo=github)](https://github.com/JoyfulReaper/HappyGopher)

A small cross-platform Gopher server built with C# and .NET 10. It serves
static content and supports compiled dynamic Gopher pages.

HappyGopher serves files, directory menus, and dynamic pages over the classic
[Gopher protocol](https://en.wikipedia.org/wiki/Gopher_%28protocol%29). It can
run directly from the console, in a Linux container, or as a Windows Service.

No web framework. No database. No JavaScript. Just a TCP listener, content,
optional compiled pages, and a protocol from a simpler time.

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

The live site is intentionally small and plain text, and serves as a
real-world example of HappyGopher hosting Gopher content.


## Features

* Serves static text, images, and binary files over Gopher
* Supports custom `gophermap` menu files
* Automatically generates directory menus when no `gophermap` exists
* Parses type-7 input/search requests as `GopherRequest` selector and input
* Supports exact-selector dynamic pages through `IGopherPage`
* Resolves dynamic pages before static content at the same selector
* Provides `GopherResponseWriter` for reusable protocol-safe response
  formatting
* Includes a `/server-time` dynamic page that returns the current UTC time
* Optionally provides `/qotd` and `/random-quote` through HappyQOTD
* Optionally provides a file-backed guestbook with named or anonymous entries
  and replay suppression for clients that resend type-7 requests
* Optionally publishes selector-served telemetry through Mission Control
* Runs as a console application or Linux container, with Windows Service support
* Includes a Dockerfile for container deployment
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

HappyGopher runs anywhere the .NET 10 runtime is supported. The repository
includes a Dockerfile for Linux deployment and Windows Service integration for
Windows installations.

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

With this default, HappyGopher accepts IPv4 local connections only. To serve
other machines or enable IPv6, configure `ListenAddress` and `DualMode` for the
desired address families and review your firewall rules.

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
    "DualMode": false,
    "Port": 70,
    "PublicHost": "127.0.0.1",
    "ContentRoot": "content",
    "MaxConcurrentConnections": 64,
    "MaxSelectorBytes": 4096,
    "MaxInputBytes": 1024,
    "RequestTimeoutSeconds": 15
  },
  "MissionControl": {
    "Enabled": false,
    "BaseUrl": "http://localhost:5190",
    "ApiKey": "",
    "TimeoutMilliseconds": 1000
  },
  "HappyQotd": {
    "BaseUrl": "https://qotd-api.kgivler.com",
    "TimeoutMilliseconds": 2000,
    "Enabled": false
  },
  "Guestbook": {
    "Enabled": false,
    "DataPath": "data/guestbook.jsonl",
    "MaxEntriesDisplayed": 50
  }
}
```

### Configuration options

#### Gopher

| Setting                         |     Default | Description                                                      |
| ------------------------------- | ----------: | ---------------------------------------------------------------- |
| `ListenAddress`                 | `127.0.0.1` | Local IP address on which the TCP server listens.                |
| `DualMode`                      |      `false` | Allows one IPv6 wildcard listener to accept IPv6 and IPv4.       |
| `Port`                          |        `70` | TCP port used by the server.                                     |
| `PublicHost`                    | `127.0.0.1` | Hostname or IP address advertised inside generated Gopher menus. |
| `ContentRoot`                   |   `content` | Directory containing files and `gophermap` menus.                |
| `MaxConcurrentConnections`      |        `64` | Maximum number of requests handled concurrently.                 |
| `MaxSelectorBytes`              |      `4096` | Maximum permitted selector length in UTF-8 bytes.                |
| `MaxInputBytes`                 |      `1024` | Maximum permitted type-7 input length in UTF-8 bytes.            |
| `RequestTimeoutSeconds`         |        `15` | Time allowed for a client to send its request.                   |
| `TelemetryIgnoredRemoteAddress` |   not set   | Remote IP address excluded from selector telemetry.              |

#### Mission Control

| Setting               |                  Default | Description                                      |
| --------------------- | -----------------------: | ------------------------------------------------ |
| `Enabled`             |                  `false` | Enables selector-served telemetry.               |
| `BaseUrl`             | `http://localhost:5190` | Mission Control service base URL.                |
| `ApiKey`              |                    empty | API key sent to Mission Control.                  |
| `TimeoutMilliseconds` |                   `1000` | Mission Control client timeout in milliseconds.  |

#### HappyQOTD

| Setting               |                             Default | Description                                  |
| --------------------- | ----------------------------------: | -------------------------------------------- |
| `Enabled`             |                             `false` | Registers the HappyQOTD integration pages.   |
| `BaseUrl`             | `https://qotd-api.kgivler.com` | HappyQOTD service base URL.                  |
| `TimeoutMilliseconds` |                              `2000` | HappyQOTD request timeout in milliseconds.  |

#### Guestbook

| Setting               |                    Default | Description                                      |
| --------------------- | -------------------------: | ------------------------------------------------ |
| `Enabled`             |                    `false` | Registers the guestbook store and pages.         |
| `DataPath`            | `data/guestbook.jsonl` | Newline-delimited JSON storage path.             |
| `MaxEntriesDisplayed` |                       `50` | Maximum number of recent entries shown.          |

`ListenAddress` and `PublicHost` serve different purposes:

* `ListenAddress` controls where HappyGopher accepts connections. The checked-in default is IPv4 loopback-only, not all interfaces.
* `DualMode` controls whether an IPv6 wildcard listener also accepts IPv4 clients. HappyGopher still creates only one listener.
* `PublicHost` is the address placed into menu entries returned to clients.

Use these combinations for common listener configurations:

| Purpose             | `ListenAddress` | `DualMode` |
| ------------------- | --------------- | ---------- |
| IPv4 loopback       | `127.0.0.1`     | `false`    |
| IPv4 all interfaces | `0.0.0.0`       | `false`    |
| IPv6 loopback       | `::1`           | `false`    |
| IPv6 all interfaces | `::`            | `false`    |
| IPv4 and IPv6       | `::`            | `true`     |

Enabling `DualMode` with an IPv4 address or a non-wildcard IPv6 address is
invalid and causes startup to fail.

For a public server, set `PublicHost` to the hostname clients actually use:

```json
"PublicHost": "gopher.example.com"
```

For dual-stack public service, use a DNS hostname with both `A` and `AAAA`
records for `PublicHost`. Avoid a raw IPv6 literal because generated Gopher menu
fields do not provide the same unambiguous bracketed host-and-port formatting as
a network endpoint.

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
|  `7` | Input/search prompt  |
|  `9` | Binary file          |
|  `g` | GIF image            |
|  `I` | Other image          |
|  `h` | HTML or external URL |

Comments may be added by beginning a line with `#`:

```text
# This line is ignored
iThis line is displayed
```

## Dynamic Pages

Compiled dynamic pages implement `IGopherPage`. The `Selector` property
identifies the one exact selector handled by the page, and `WriteAsync` writes
the response and returns its `GopherResponseKind`. That response kind is used
by the normal selector telemetry.

HappyGopher resolves dynamic pages before checking filesystem content. A
dynamic page therefore overrides a static resource at the same selector.
Selectors without a matching page fall back to `GopherContentStore` and the
normal static-content behavior.

Type-7 requests provide input after the selector, separated by the first tab:

```text
selector<TAB>input<CRLF>
```

Input is separated from the selector by the first tab. The selector and input
are exposed to dynamic pages through `GopherRequest`. `MaxSelectorBytes` and
`MaxInputBytes` limit their UTF-8 encoded lengths independently. Requests
without a tab have a null `Input`; a trailing tab produces an empty input
string. Type `7` menu entries tell compatible clients to prompt for this input.

`GopherResponseWriter` provides reusable formatting for text responses, menu
items, informational lines, and errors. It handles CRLF line endings, menu
field sanitization, dot-stuffing, and response termination.

```csharp
public sealed class ExamplePage : IGopherPage
{
    public string Selector => "/example";

    public async Task<GopherResponseKind> WriteAsync(
        GopherRequest request,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using GopherResponseWriter writer = new(output);

        await writer.WriteTextLineAsync(
            "Hello from a dynamic Gopher page.",
            cancellationToken);

        await writer.CompleteAsync(cancellationToken);

        return GopherResponseKind.Text;
    }
}
```

The output stream is owned by the server and page implementations must not
close it. `GopherResponseWriter` leaves the stream open.

Pages currently require explicit dependency-injection registration in
`Program.cs`:

```csharp
builder.Services.AddScoped<IGopherPage, ExamplePage>();
```

The core example uses selector `/server-time`, returns a text response
containing the current UTC server time, and is implemented by `ServerTimePage`
in `HappyGopher/Pages/ServerTimePage.cs`. The HappyQOTD and guestbook pages are
additional examples of compiled dynamic pages. The sample root `gophermap`
links to the server-time page but does not advertise optional pages that are
disabled in the default configuration.

Runtime DLL scanning and plugin-folder loading are planned but are not
implemented. HappyGopher does not currently discover page assemblies
automatically.

## Guestbook

The optional file-backed guestbook is controlled by `Guestbook:Enabled`. When
enabled, `/guestbook` displays recent entries and `/guestbook/sign` accepts
type-7 input in either form:

```text
Message
Name | Message
```

Enabling the guestbook through an environment variable:

```text
Guestbook__Enabled=true
```

or JSON configuration:

```json
"Guestbook": {
  "Enabled": true
}
```

does not automatically modify static `gophermap` files. Add these entries to
the deployed root `gophermap` when enabling the feature:

```text
1Guestbook	/guestbook
7Sign Guestbook	/guestbook/sign
```

`/guestbook` displays recent entries. `/guestbook/sign` is a type-7 input item
used to submit an entry. Optional dynamic pages are registered from
configuration, while static menus remain administrator-managed. A fresh clone
requires no menu changes when the guestbook remains disabled because the
default root menu does not advertise it.

An omitted or blank name is displayed as `Anonymous`. Names are limited to 40
characters and messages to 500 characters. The store appends entries as
newline-delimited JSON and `MaxEntriesDisplayed` controls how many recent
entries appear on the view page.

Some Gopher clients replay type-7 requests. To avoid an immediate duplicate,
the store suppresses an identical normalized name and message received within
five seconds. This is replay protection, not general abuse prevention.

Relative `DataPath` values are resolved from the application directory
(`AppContext.BaseDirectory`); absolute paths are used directly. The process
must have write access to the selected directory.

The guestbook currently has no authentication, moderation interface, CAPTCHA,
or per-client rate limiting. Do not treat it as secure against automated or
deliberate abuse.

## HappyQOTD Integration

The optional HappyQOTD integration uses `HappyQotd:Enabled`,
`HappyQotd:BaseUrl`, and `HappyQotd:TimeoutMilliseconds`. Its pages are
registered only when the integration is enabled:

* `/qotd` returns the quote selected for the current day.
* `/random-quote` returns a random quote.

Upstream HTTP failures and timeouts produce a completed plain-text unavailable
response rather than crashing the Gopher server.

## Mission Control Telemetry

Mission Control configuration uses `MissionControl:Enabled`,
`MissionControl:BaseUrl`, `MissionControl:ApiKey`, and
`MissionControl:TimeoutMilliseconds`. Selector-served telemetry is skipped
when Mission Control is disabled.

Each selector-served event reports the selector, response type, remote
endpoint, duration, and success state. Its event metadata also carries the
request timestamp and correlation ID. `Gopher:TelemetryIgnoredRemoteAddress`
may be set to one remote IP address whose requests should not produce selector
telemetry, which is useful for a local health check or monitor.

## Testing the Server

Run the automated test suite:

```powershell
dotnet test
```

Tests cover static serving, dynamic routing, dynamic-page precedence and
static fallback, type-7 parsing, HappyQOTD pages, guestbook storage and pages,
response formatting, telemetry, concurrency, request limits, and graceful
shutdown.

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

Publish a portable framework-dependent build:

```powershell
dotnet publish .\HappyGopher\HappyGopher.csproj `
    --configuration Release `
    --self-contained false `
    --output .\publish `
    /p:UseAppHost=false
```

HappyGopher intentionally uses a framework-dependent .NET deployment rather
than Native AOT to preserve support for future runtime-loaded plugins and
content providers. Runtime plugin discovery and loading are not implemented
yet; pages currently must be compiled and registered explicitly.

The published `content` directory and `appsettings.json` should remain beside the executable. Content files under `HappyGopher/content` are copied recursively during publish.

Run the published server on any supported platform with the .NET 10 runtime:

```powershell
dotnet .\publish\HappyGopher.dll
```

## Docker Deployment

Build the image from the repository root:

```powershell
docker build -t happygopher .
```

The image exposes TCP port `70` and runs the application as the non-root
`$APP_UID` user supplied by the .NET runtime image. The configured
`Gopher:ListenAddress` must accept container traffic when the server should be
reachable outside the container.

If the guestbook is enabled, mount `Guestbook:DataPath` on writable persistent
storage. A minimal Compose configuration is:

```yaml
services:
  happygopher:
    build: .
    ports:
      - "70:70"
    volumes:
      - ./data:/app/data
    environment:
      Gopher__ListenAddress: "::"
      Gopher__DualMode: "true"
      Gopher__PublicHost: "gopher.example.com"
      Guestbook__Enabled: "true"
```

Double underscores are the standard .NET environment-variable separator for
nested configuration keys. The mounted host directory must be writable by the
container user.

The `70:70` mapping normally publishes container port 70 on the host's IPv4 and
IPv6 addresses. HappyGopher must bind to `::` with dual mode enabled so Docker
can reach the application through either address family. A native IPv6 address
inside the container is not required for ordinary published-port forwarding.

Compose network `enable_ipv6: true` is only required when the container itself
needs native IPv6 addressing, direct IPv6 routing, or outbound IPv6
connectivity. Explicitly disabling IPv6 in the container is incompatible with
binding HappyGopher to `::`. Docker port publishing also does not replace host
security policy: Windows and Linux host firewalls must separately allow inbound
TCP port 70.

## Installing as a Windows Service

For Windows Service installation, publish a Windows app host to a permanent
location, such as:

```powershell
dotnet publish .\HappyGopher\HappyGopher.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output C:\Services\HappyGopher
```

The example output directory is:

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
* Run the process under a restricted operating-system account where practical.
* Treat an enabled guestbook as untrusted public input and monitor its storage.

Gopher does not provide encryption. Traffic, selectors, and downloaded content are sent in plaintext.

## Current Limitations

* Compiled `IGopherPage` implementations are supported, but runtime plugin DLL
  discovery and loading are not
* Dynamic routing supports exact selectors only; prefix, wildcard, and
  parameterized routes are not supported
* No CGI or arbitrary scripting
* No authentication or access control
* No TLS support
* Gopher traffic is plaintext
* No administrative interface
* No guestbook moderation system
* No official packaged releases yet

## Project Structure

```text
HappyGopher.slnx
├── Dockerfile
├── HappyGopher/
│   ├── Events/
│   │   ├── GopherServiceStartedEvent.cs
│   │   ├── SelectorServedEvent.cs
│   │   └── HappyGopherJsonContext.cs
│   ├── Gopher/
│   │   ├── GopherConnectionHandler.cs
│   │   ├── GopherContentStore.cs
│   │   ├── GopherRequest.cs
│   │   ├── GopherResponseWriter.cs
│   │   ├── GopherResponseKind.cs
│   │   └── GopherSessionResult.cs
│   ├── Integrations/
│   │   └── HappyQotd/
│   ├── Pages/
│   │   ├── IGopherPage.cs
│   │   ├── GopherPageResolver.cs
│   │   ├── ServerTimePage.cs
│   │   ├── QuoteOfTheDayPage.cs
│   │   ├── RandomQuotePage.cs
│   │   └── Guestbook/
│   │       ├── FileGuestbookStore.cs
│   │       ├── SignGuestbookPage.cs
│   │       └── ViewGuestbookPage.cs
│   ├── Telemetry/
│   │   └── TelemetryService.cs
│   ├── Program.cs
│   ├── HappyGopher.csproj
│   ├── appsettings.json
│   └── content/
├── HappyGopher.Tests/
│   ├── GopherPageResolverTests.cs
│   ├── GopherResponseWriterTests.cs
│   └── ServerTimePageTests.cs
└── LICENSE
```

## Contributing

Bug reports, testing, documentation fixes, and pull requests are welcome.

The project is intentionally small, so changes should favor straightforward code and minimal dependencies over elaborate infrastructure.

## License

HappyGopher is available under the [MIT License](LICENSE).

Copyright © 2026 Kyle Givler.
