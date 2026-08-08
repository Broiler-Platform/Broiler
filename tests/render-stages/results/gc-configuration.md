# GC configuration probe (multithreading item #19)

- Mode: **Workstation GC**, latency mode Interactive
- Cores: 4 logical
- Rounds: 5 over 5 pages = 25 renders at 1280x1024

| Metric | Value |
|---|---:|
| wall time | 43546.5 ms |
| per render | 1741.86 ms |
| allocated | 49258.2 MiB |
| allocated per render | 1970.33 MiB |
| gen 0 collections | 3036 |
| gen 1 collections | 245 |
| gen 2 collections | 50 |

Compare two modes by running this command twice with `DOTNET_gcServer=0` and
`DOTNET_gcServer=1`; the mode is fixed at runtime start and cannot be switched here.

---

# GC configuration probe (multithreading item #19)

- Mode: **Server GC**, latency mode Interactive
- Cores: 4 logical
- Rounds: 5 over 5 pages = 25 renders at 1280x1024

| Metric | Value |
|---|---:|
| wall time | 70741.4 ms |
| per render | 2829.65 ms |
| allocated | 49257.4 MiB |
| allocated per render | 1970.29 MiB |
| gen 0 collections | 4531 |
| gen 1 collections | 282 |
| gen 2 collections | 54 |

Compare two modes by running this command twice with `DOTNET_gcServer=0` and
`DOTNET_gcServer=1`; the mode is fixed at runtime start and cannot be switched here.
