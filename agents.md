# AI Developer Guide: Xiaomi BE3600 Toolbox

## 1. Project Overview
Cross-platform toolbox (.NET 10) for managing the **Xiaomi Router BE3600** (stock MiWiFi firmware):
- SSH unlock via web exploit (`stok` injection)
- Web interface localization (Russian / English)
- Safe OpenWrt firmware flashing (with MTD/UBI partition safeguards)
- Dual frontends: **Desktop GUI** (Avalonia UI) and **CLI** sharing the same engine.

---

## 2. Solution Structure & Layer Boundaries
```
BE3600Toolbox.slnx
├── src/BE3600Toolbox.Core/       # Domain models, service contracts (ZERO UI/Engine dependencies)
├── src/BE3600Toolbox.Engine/     # HTTP/stok, SSH.NET, MTD/flash implementation
├── src/BE3600Toolbox.Desktop/    # Avalonia UI (MVVM via CommunityToolkit.Mvvm)
└── src/BE3600Toolbox.Cli/        # Console application
```

### Strict Rules:
1. **Zero UI in Core/Engine:** Never reference `Avalonia`, `System.Drawing`, or terminal formatting in `Core` or `Engine`.
2. **No Direct Console I/O in Engine:** Never use `Console.WriteLine` or `Console.ReadLine` inside `Engine`. Use `ILogger<T>` for logs.
3. **User Interaction Abstraction:** All user prompts (confirmations, passwords, choices) must route through `IInteractionService`.
4. **Configuration & State:** Persist non-sensitive settings (`RouterIp`, `Language`, `LastBackupPath`) via `ISettingsService` (`settings.json`). NEVER store plain passwords on disk.
5. **Progress Reporting:** Long-running tasks must report status and percentages via `IProgress<StepProgress>`.
6. **Shared DI:** `Engine` must register its services via `services.AddBe3600Engine()`.

---

## 3. Router Safety Guardrails (MANDATORY)
1. **Pre-flight Checks:** Always verify router model (`RD15` / BE3600) and firmware version via `xqsystem/init_info` before triggering exploits.
2. **Mandatory Backup:** Never write to `mtd` / `ubi` without first creating and verifying a backup of critical partitions (`bdata`, `Factory`, `bootloader`, `crash`).
3. **Checksum Verification:** All firmware binaries, payloads, and archives must be validated with SHA256 before upload.
4. **Dry-Run Support:** Every destructive action (writing partitions, flashing) must respect a `dryRun: true` parameter.
5. **Explicit SSH Commands:** Do not run blind chained commands (`&& reboot`). Verify exit codes for each step.

---

## 4. Coding Conventions
- **Language:** C# 13+ / .NET 10, Nullable reference types enabled.
- **Async Pattern:** Every I/O or network method must be `async Task` and accept a `CancellationToken ct = default`.
- **Paths:** Use `Path.Combine()` for local OS paths; use forward slashes (`/`) for remote Linux router paths.
- **Testing:** New business logic in `Engine` must be decoupled from live sockets to allow unit testing with mocks.

