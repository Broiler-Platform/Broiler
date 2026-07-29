# Running BOSS as a Windows service

`Install-BossService.ps1` registers **BOSS — the Broiler Office Standalone Server** with the Windows
Service Control Manager so the Broiler Writer is served across reboots.

The server links `Microsoft.Extensions.Hosting.WindowsServices`, so it speaks the service control
protocol itself — no wrapper (NSSM, `srvany`) is involved.

```
service/
├── Install-BossService.ps1     install / upgrade the service
└── Uninstall-BossService.ps1   remove it
```

---

## Install

From an **elevated** PowerShell prompt, in the extracted package:

```powershell
powershell -ExecutionPolicy Bypass -File .\service\Install-BossService.ps1
powershell -ExecutionPolicy Bypass -File .\service\Install-BossService.ps1 -Urls http://0.0.0.0:5555 -OpenFirewall
powershell -ExecutionPolicy Bypass -File .\service\Install-BossService.ps1 -InstallPath D:\Apps\BOSS -ServiceAccount LocalSystem
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `-Urls` | `http://0.0.0.0:5555` | Listening addresses; semicolon-separated for several. |
| `-InstallPath` | `%ProgramFiles%\Broiler\Office Server` | Where the server and its `wwwroot\` are installed. |
| `-ServiceName` | `BroilerOfficeServer` | Service name used by `Start-Service` and friends. |
| `-ServiceAccount` | `NT AUTHORITY\NetworkService` | Account the service runs as. `LocalSystem` for more privilege, or a domain account with `-ServiceAccountPassword`. |
| `-Environment` | `Production` | Sets `ASPNETCORE_ENVIRONMENT` for the service. |
| `-OpenFirewall` | — | Add an inbound firewall rule for the TCP port in `-Urls`. |
| `-NoStart` | — | Register but do not start. |

The script copies the payload to `-InstallPath`, registers the service (delayed automatic start,
restart-on-crash after 5 s / 10 s / 60 s), grants the service account read+execute on the install
tree, and starts it. **Re-running upgrades in place** — the service is stopped, `wwwroot\` is
replaced wholesale, and the service starts again.

Check it:

```powershell
Invoke-RestMethod http://localhost:5555/healthz
```

---

## Manage

```powershell
Get-Service BroilerOfficeServer
Start-Service BroilerOfficeServer
Stop-Service  BroilerOfficeServer
Restart-Service BroilerOfficeServer

# The service logs to the Application event log.
Get-EventLog -LogName Application -Source BroilerOfficeServer -Newest 20
```

### Changing the listening address

The addresses are part of the registered command line, so re-run the installer with the new value:

```powershell
powershell -ExecutionPolicy Bypass -File .\service\Install-BossService.ps1 -Urls http://0.0.0.0:8080
```

Everything else is a per-service environment variable under
`HKLM:\SYSTEM\CurrentControlSet\Services\BroilerOfficeServer\Environment` — the installer writes
`ASPNETCORE_ENVIRONMENT` there, and other settings follow the same `Section__Key=value` form:

```powershell
$key = 'HKLM:\SYSTEM\CurrentControlSet\Services\BroilerOfficeServer'
Set-ItemProperty $key -Name Environment -Type MultiString -Value @(
    'ASPNETCORE_ENVIRONMENT=Production'
    'Logging__LogLevel__Microsoft.AspNetCore=Information'
)
Restart-Service BroilerOfficeServer
```

---

## Firewall

`-OpenFirewall` adds the inbound rule for you. By hand:

```powershell
New-NetFirewallRule -DisplayName 'BOSS (TCP 5555)' -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort 5555 -Profile Any
```

Without a rule the server is reachable from the machine itself only, whatever `--urls` says.

---

## HTTPS

Either terminate TLS in front (IIS with ARR, or any reverse proxy) and let BOSS listen on
`http://127.0.0.1:5555`, or point Kestrel at a certificate from the machine store:

```powershell
$key = 'HKLM:\SYSTEM\CurrentControlSet\Services\BroilerOfficeServer'
Set-ItemProperty $key -Name Environment -Type MultiString -Value @(
    'ASPNETCORE_ENVIRONMENT=Production'
    'Kestrel__Certificates__Default__Path=C:\ProgramData\Broiler\office-server.pfx'
    'Kestrel__Certificates__Default__Password=…'
)
```

Then reinstall with `-Urls "http://127.0.0.1:5555;https://0.0.0.0:5556"`. The service account needs
read access to the `.pfx`.

---

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\service\Uninstall-BossService.ps1
powershell -ExecutionPolicy Bypass -File .\service\Uninstall-BossService.ps1 -KeepFiles
```

Stops and deletes the service, removes the firewall rule it added, and deletes the install directory
unless `-KeepFiles` is given.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `…must run from an elevated (Administrator) PowerShell prompt` | Right-click PowerShell → *Run as administrator*. |
| `…cannot be loaded because running scripts is disabled` | Invoke it as shown, with `-ExecutionPolicy Bypass -File`. |
| Service starts, then stops immediately | Check the Application event log. Usually the port is taken (`netstat -ano \| findstr :5555`) or the account cannot read the install tree. |
| Service runs, pages 404 | `wwwroot\` did not make it into the install path. Re-run the installer from the *complete* extracted package. |
| Reachable locally, not from another machine | The firewall rule is missing (`-OpenFirewall`), or `-Urls` is loopback-only. |
| `You must install .NET to run this application` | A framework-dependent package without the ASP.NET Core 10 runtime. Install it, or use the self-contained package. |

For anything above the service layer — arguments, endpoints, configuration precedence, caching —
see [`../README.md`](../README.md).
