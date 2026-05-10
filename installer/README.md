# Установка PassFlow Tracker (Windows)

## Требования

- Windows 10/11 (64-bit)
- [Docker Desktop для Windows](https://www.docker.com/products/docker-desktop/) — для базы данных PostgreSQL
- .NET 8 Runtime (включён в дистрибутив, не требует отдельной установки)

## Установка

### Шаг 1 — Установите Docker Desktop

Скачайте и установите [Docker Desktop](https://www.docker.com/products/docker-desktop/).  
После установки запустите Docker Desktop и дождитесь появления иконки в трее.

### Шаг 2 — Запустите установщик

1. Распакуйте архив дистрибутива
2. Откройте PowerShell **от имени администратора**
3. Выполните:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\install.ps1
```

Установщик автоматически:
- Проверит наличие Docker Desktop
- Скопирует файлы в `C:\Program Files\PassFlow Tracker\`
- Создаст ярлык на рабочем столе и в меню Пуск
- Зарегистрирует приложение в «Установка и удаление программ»

### Шаг 3 — Запуск

Используйте ярлык **PassFlow Tracker** на рабочем столе.  
При первом запуске Docker автоматически скачает образ PostgreSQL (~300 МБ).

## Удаление

**Через меню Пуск:** Пуск → PassFlow Tracker → Удалить PassFlow Tracker

**Или вручную:**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
& "C:\Program Files\PassFlow Tracker\uninstall.ps1"
```

## Сборка дистрибутива из исходников

```powershell
# В корне репозитория
.\build-windows.ps1
```

Готовый дистрибутив появится в папке `dist\`.

## Структура дистрибутива

```
dist\
├── install.ps1          ← установщик
├── uninstall.ps1        ← деинсталлятор
└── app\
    ├── PassFlowTracker.exe
    ├── appsettings.json
    └── launch.bat
```

## Устранение неполадок

**«Docker daemon не запущен»** — запустите Docker Desktop из меню Пуск и дождитесь иконки в трее.

**«Запустите от имени администратора»** — ПКМ на PowerShell → «Запуск от имени администратора».

**Приложение не подключается к БД** — проверьте что Docker Desktop запущен, контейнер `my_postgres` работает:
```
docker ps
```
