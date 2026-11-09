# Xiaomi BE3600 Toolbox

Кроссплатформенная утилита (.NET 10) для управления, разблокировки SSH, локализации и безопасной прошивки роутера **Xiaomi Router BE3600** (аппаратная платформа `RD15`, стоковая прошивка MiWiFi).

Предоставляет два варианта интерфейса, использующих единое ядро (`Engine`):
- **CLI** — интерактивное консольное приложение.
- **Desktop** — графический интерфейс на базе [Avalonia UI](https://avaloniaui.net/).

---

## 🚀 Основные возможности

1. **Автоопределение и подключение:**
   - Автоматический поиск роутера в сети (по умолчанию `192.168.31.1` или сохранённый IP).
   - Сохранение настроек в профиле пользователя (`settings.json`).
   - Получение токена сессии (`stok`) через веб-интерфейс MiWiFi.

2. **Разблокировка SSH (SSH Unlock):**
   - Эксплойт через веб-интерфейс (`stok` injection) для запуска SSH-сервера.
   - Расчёт стандартного пароля SSH по серийному номеру / MAC-адресу роутера по алгоритму Xiaomi.
   - Проверка доступности 22 порта и автоматическое SSH-подключение.

3. **Локализация веб-интерфейса (EN / RU):**
   - Установка английского и русского языков в веб-интерфейс стоковой прошивки MiWiFi.
   - **Встроенный компилятор LMO:** файлы локализации хранятся в репозитории в исходном текстовом формате `.po` (Gettext) и компилируются в бинарный формат LuCI `.lmo` прямо в памяти "на лету" без внешних зависимостей.
   - Возможность чистого удаления добавленных языков (возврат к оригинальному состоянию).

4. **Перманентный SSH (Permanent SSH):**
   - Закрепление доступа по SSH, выдерживающего перезагрузки роутера (через патч `/etc/init.d/dropbear` и правила firewall/cron).
   - Возможность чистого удаления перманентного SSH.

5. **Безопасная прошивка OpenWrt (Safe Mode):**
   - Pre-flight проверки: сверка модели роутера (`RD15` / BE3600) и версии прошивки перед выполнением операций.
   - Обязательный бэкап критических разделов (`bdata`, `Factory`, `bootloader`, `crash`).
   - Проверка контрольных сумм SHA-256 загружаемых файлов.
   - Поддержка режима симуляции (`Dry Run`).

---

## 📁 Структура решения

```text
BE3600Toolbox.slnx
├── src/BE3600Toolbox.Core/       # Контракты сервисов, модели данных (без UI и платформенных зависимостей)
├── src/BE3600Toolbox.Engine/     # HTTP/stok API, SSH.NET, компилятор LMO, работа с MTD/партициями
├── src/BE3600Toolbox.Desktop/    # Кроссплатформенный GUI на Avalonia UI (MVVM)
└── src/BE3600Toolbox.Cli/        # Интерактивная консольная утилита
```

---

## 🛠 Сборка и запуск

### Требования
- [.NET 10 SDK](https://dotnet.microsoft.com/download) или новее.

### Клонирование и сборка
```bash
git clone https://github.com/your-username/be3600-toolbox.git
cd be3600-toolbox
dotnet build
```

### Запуск CLI
```bash
dotnet run --project src/BE3600Toolbox.Cli
```
*Дополнительные аргументы CLI:*
- `--verbose` / `--debug` / `-vv` — подробный вывод логов.
- `--test-lmo` — встроенный тест побайтовой верификации компилятора LMO с эталонными файлами.

### Запуск Desktop GUI
```bash
dotnet run --project src/BE3600Toolbox.Desktop
```

### Публикация Single-File сборки (без внешних зависимостей)

В репозитории настроен автоматический CI/CD (GitHub Actions), который при каждом релизе собирает и публикует готовые автономные архивы:
- **Windows:** `BE3600Toolbox-Desktop-win-x64.zip`, `BE3600Toolbox-Cli-win-x64.zip`
- **Linux:** `BE3600Toolbox-Cli-linux-x64.tar.gz`, `BE3600Toolbox-Cli-linux-arm64.tar.gz`
- **macOS:** `BE3600Toolbox-Cli-osx-arm64.tar.gz` (Apple Silicon M1-M4), `BE3600Toolbox-Cli-osx-x64.tar.gz` (Intel)

Для ручной сборки CLI под любую из платформ:
```bash
# Windows (x64)
dotnet publish src/BE3600Toolbox.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-win-x64

# Linux (x64 / PC, серверы)
dotnet publish src/BE3600Toolbox.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-linux-x64

# Linux (ARM64 / Raspberry Pi и др.)
dotnet publish src/BE3600Toolbox.Cli -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-linux-arm64

# macOS (Apple Silicon / M1, M2, M3, M4)
dotnet publish src/BE3600Toolbox.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-osx-arm64

# macOS (Intel)
dotnet publish src/BE3600Toolbox.Cli -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-osx-x64
```

---

## 🙏 Благодарности и используемые материалы (Credits)

В проекте используются скрипты и файлы переводов, взятые из проекта:
- **[xmir-patcher](https://github.com/openwrt-xiaomi/xmir-patcher)** (автор и сообщество [openwrt-xiaomi](https://github.com/openwrt-xiaomi)).

Оригинальные файлы и скрипты были модифицированы нами и используются в составе toolbox. Огромное спасибо сообществу OpenWrt и авторам xmir-patcher за исследование архитектуры роутеров Xiaomi!

---

## ⚠️ Отказ от ответственности (Disclaimer)
Любые модификации прошивки, разблокировка SSH и перепрошивка роутера выполняются на ваш собственный страх и риск. Разработчики не несут ответственности за возможный выход устройства из строя («окирпичивание») или потерю гарантии. Всегда создавайте резервные копии разделов перед внесением изменений!
