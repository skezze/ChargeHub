# Third-party components

ChargeHub has no third-party NuGet dependencies at runtime.

- The Windows Runtime API projection (`Microsoft.Windows.SDK.NET.Ref`, `WinRT.Runtime`) is supplied by the .NET SDK and is included in self-contained builds. It is provided by Microsoft under the licenses accompanying the Windows SDK .NET projection packages. See https://github.com/microsoft/CsWinRT.
- The test project uses xUnit (Apache-2.0) and Microsoft.NET.Test.Sdk (MIT). They are not part of the distributed application.

The ASUS battery query was implemented independently; product identifiers and protocol behaviour were checked against [G-Helper](https://github.com/seerge/g-helper) and libratbag. No G-Helper source code is incorporated.
