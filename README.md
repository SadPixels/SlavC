# RusLang

RusLang — прототип русского системного языка, развиваемого как самостоятельная
альтернатива C++, и его компилятора для .NET 10. Компилятор
преобразует файл `.rus` в один автономный исполняемый файл. Для запуска результата
не нужны установленный .NET Runtime, DLL, PDB или другие файлы рядом.

Текущий MVP поддерживает Windows x64 (`win-x64`) и консольные приложения.

## Что уже работает

- компиляция `.rus` в C# и далее в управляемую сборку через Roslyn;
- компиляция полностью в памяти, без запуска `csc`, MSBuild или пользовательского
  кода;
- автономный single-file компилятор `rusc.exe`;
- встроенные reference assemblies .NET 10 и RuntimeHost;
- контейнер RusPack v1 с CRC-32, SHA-256 и Brotli;
- запуск сборки непосредственно из памяти;
- команды `build`, `run`, `reveal`, `inspect`, `verify`, `doctor` и `version`;
- кириллица в исходных файлах, именах и выводе;
- детерминированная компиляция;
- тесты формата, упаковщика, компилятора и RuntimeHost.

## Быстрый старт

Для сборки самого проекта требуется:

- Windows x64;
- PowerShell 7 или Windows PowerShell;
- .NET SDK 10.0.300.

Из корня репозитория выполните:

```powershell
.\eng\build.ps1
```

Скрипт последовательно восстанавливает зависимости, собирает solution, запускает
тесты и публикует RuntimeHost и автономный компилятор.

Готовый компилятор появится здесь:

```text
artifacts/rusc/win-x64/rusc.exe
```

Скомпилируйте пример:

```powershell
.\artifacts\rusc\win-x64\rusc.exe build .\examples\hello.rus
.\examples\hello.exe
```

Ожидаемый вывод:

```text
Привет из RusLang
```

После публикации `rusc.exe` можно перенести на другую Windows x64 машину. Для его
работы не нужны .NET SDK и .NET Runtime.

## Синтаксис RusLang

RusLang использует собственную модель: зависимости призываются, точкой входа
служит `Царь`, а запятые и скобки в синтаксисе не применяются.

```rus
призвать Сварога
призвать Ярило

Царь
    пусть числа есть ряд 3 и 1 и 2
    для i от 0 до длина числа
        печать числа по i
    конец
конец
```

Полное описание команд, выражений, встроенных функций и ограничений находится
в [справочнике языка](docs/language-reference.md). Рабочая пузырьковая сортировка:
[`examples/sort.rus`](examples/sort.rus).

## Команды `rusc`

Общий формат:

```text
rusc <команда> [аргументы]
```

### `build`

Создаёт автономный исполняемый файл:

```powershell
rusc build <file.rus> [параметры]
```

Примеры:

```powershell
rusc build .\hello.rus
rusc build .\hello.rus -o .\out\hello
rusc build .\hello.rus -c debug --force
rusc build .\application.rus --reference .\Library.dll
```

Поддерживаемые параметры:

| Параметр | Назначение |
| --- | --- |
| `-o, --output <path>` | Путь выходного файла |
| `-r, --rid <rid>` | Целевой RID; в MVP доступен `win-x64` |
| `-c, --configuration <debug\|release>` | Конфигурация компиляции |
| `--reference <path>` | Добавить управляемую DLL; параметр можно повторять |
| `--host-template <path>` | Использовать указанный RuntimeHost template |
| `--no-compress` | Отключить Brotli-сжатие entries |
| `--keep-pdb` | Сохранить portable PDB внутри контейнера при Debug-сборке |
| `--emit-intermediate` | Записать сгенерированный `.generated.cs` |
| `--deterministic` | Явно запросить детерминированную сборку; сейчас она включена всегда |
| `--verbose` | Зарезервирован для расширенной диагностики |
| `--force` | Разрешить замену существующего output |

Если `--output` не задан, файл создаётся рядом с исходником и получает имя
исходного файла. Для Windows расширение `.exe` добавляется автоматически.

Существующий output не перезаписывается без `--force`. Запись выполняется через
временный файл, затем контейнер повторно открывается и полностью проверяется.
Только после успешной проверки временный файл атомарно становится output.

### `run`

Компилирует программу во временный каталог, запускает её и возвращает её код
завершения:

```powershell
rusc run .\hello.rus
rusc run .\args.rus -c debug -- first second "third value"
```

Аргументы после `--` передаются пользовательской программе.

### `reveal`

Показывает C#, сгенерированный из `.rus`, но не компилирует и не запускает его:

```powershell
rusc reveal .\hello.rus
```

Команда полезна при разработке frontend и диагностике source mapping.

### `inspect`

Показывает manifest и entries готового RusPack-файла:

```powershell
rusc inspect .\hello.exe
```

`inspect` не загружает и не выполняет пользовательскую сборку.

### `verify`

Проверяет footer, CRC-32, границы, manifest, SHA-256 payload и каждой entry, а
также корректность Brotli:

```powershell
rusc verify .\hello.exe
```

Успешный результат:

```text
OK: C:\полный\путь\hello.exe
```

### `doctor`

Показывает параметры установленного компилятора:

```powershell
rusc doctor
```

В вывод входят версии компилятора и языка, TFM, RID, версия RusPack, протокол
RuntimeHost и количество встроенных reference assemblies.

### `version`

```powershell
rusc version
rusc --version
```

## Использование сторонних DLL

Управляемая зависимость передаётся явно:

```powershell
rusc build .\application.rus `
    --reference .\libs\Library.dll `
    --reference .\libs\Library.Dependency.dll
```

DLL используется как Roslyn metadata reference и включается в RusPack как
`ManagedAssembly`. Компилятор не загружает её через reflection и не выполняет код
из неё.

На текущем этапе транзитивные зависимости нужно перечислять явно. Нативные DLL и
P/Invoke пока не поддерживаются.

## RuntimeHost template

Обычный `rusc.exe` содержит host template для своего RID и автоматически извлекает
его в безопасный временный кэш:

```text
%TEMP%\RusLang\host-templates\
```

Для разработки можно переопределить template параметром:

```powershell
rusc build .\hello.rus --host-template .\RuntimeHost.exe
```

или переменной окружения:

```powershell
$env:RUSLANG_HOST_TEMPLATE = 'C:\path\RuntimeHost.exe'
rusc build .\hello.rus
```

Обычным пользователям задавать template не требуется.

## Сборка проекта вручную

Полная автоматическая сборка:

```powershell
.\eng\build.ps1 -Configuration Release -Rid win-x64
```

Отдельные этапы:

```powershell
dotnet restore .\RusLang.slnx
dotnet build .\RusLang.slnx -c Release --no-restore
dotnet test .\RusLang.slnx -c Release --no-build

dotnet publish .\src\RusLang.RuntimeHost\RusLang.RuntimeHost.csproj `
    -c Release -r win-x64 --self-contained true `
    -o .\artifacts\host-templates\win-x64

dotnet publish .\src\RusLang.Cli\RusLang.Cli.csproj `
    -c Release -r win-x64 --self-contained true `
    -o .\artifacts\rusc\win-x64
```

Важно: сначала публикуется RuntimeHost, затем `rusc`. При публикации компилятора
готовый host template встраивается в `rusc.exe`.

Проверка форматирования:

```powershell
dotnet format .\RusLang.slnx --verify-no-changes --no-restore
```

## Результаты сборки

Основные каталоги:

```text
artifacts/
├── host-templates/
│   └── win-x64/
│       └── RusLang.RuntimeHost.exe
├── rusc/
│   └── win-x64/
│       └── rusc.exe
└── test-output/
```

`artifacts` не входит в Git.

## Архитектура

```text
program.rus
    ↓
RusLang frontend
    ↓
C# + source mapping
    ↓
Roslyn Emit в память
    ↓
Program.dll + optional portable PDB
    ↓
RusPackWriter
    ↓
[self-contained RuntimeHost][entries][manifest][footer]
    ↓
один executable
```

При запуске RuntimeHost:

1. получает собственный путь через `Environment.ProcessPath`;
2. читает фиксированный footer с конца executable;
3. проверяет CRC, версии, offsets, limits и SHA-256;
4. читает и валидирует manifest;
5. распаковывает и проверяет управляемые entries;
6. загружает entry assembly через отдельный `AssemblyLoadContext`;
7. вызывает `Assembly.EntryPoint`;
8. передаёт аргументы и возвращает код завершения программы.

Поддерживаются точки входа `void`, `int`, `Task` и `Task<int>` с параметром
`string[] args` или без него.

## Структура репозитория

```text
src/
├── RusLang.Compiler/       # frontend и Roslyn compilation
├── RusLang.Pack.Format/    # модели, footer, manifest, reader
├── RusLang.Pack.Writer/    # атомарная упаковка executable
├── RusLang.RuntimeHost/    # проверка, загрузка и запуск
└── RusLang.Cli/            # команды rusc

tests/
├── RusLang.Compiler.Tests/
├── RusLang.Pack.Format.Tests/
├── RusLang.Pack.Writer.Tests/
└── RusLang.RuntimeHost.Tests/

docs/                       # архитектура, формат и модель безопасности
eng/build.ps1               # release pipeline для локальной сборки
examples/hello.rus          # минимальный пример
Tasks.md                    # полный roadmap проекта
```

## Безопасность

Во время `rusc build`:

- пользовательский код не выполняется;
- пользовательские DLL не загружаются через reflection;
- не запускаются analyzers, source generators, MSBuild и NuGet scripts;
- не создаются дочерние процессы `dotnet`, `csc` или shell;
- manifest и offsets считаются недоверенными;
- проверяются переполнения, пересечения, размеры и пути entries;
- managed DLL не извлекаются рядом с executable.

SHA-256 обнаруживает повреждение, но не подтверждает происхождение файла.
Authenticode должен применяться только после завершения упаковки.

## Диагностика

Ошибки frontend имеют коды `RUS1xxx`, CLI — `RUSC1xxx`, среды компиляции —
`RUSC2xxx`, формата — `RUSP1xxx`, RuntimeHost — `RUSH1xxx`.

Пример:

```text
C:\project\bad.rus(2,1): error RUS1001: Неизвестная конструкция: ...
```

Неизвестные ошибки Roslyn сохраняют исходный код `CSxxxx`.

Для трассировки загрузки сборок RuntimeHost установите:

```powershell
$env:RUSLANG_HOST_TRACE = '1'
.\program.exe
```

## Текущие ограничения

- официально проверен только `win-x64`;
- поддерживаются только консольные приложения;
- frontend языка пока содержит минимальный набор конструкций;
- нативные зависимости и P/Invoke не поддерживаются;
- транзитивные managed dependencies указываются вручную;
- resources и Release PDB ещё не завершены;
- Linux, macOS, ARM64, signing и notarization находятся в roadmap;
- проверка на чистой VM без .NET должна выполняться перед релизом.

Полный план развития и критерии готовности находятся в `Tasks.md`.

## Дополнительная документация

- `docs/architecture.md` — архитектура;
- `docs/adr/0001-runtime-host-and-appended-payload.md` — архитектурное решение;
- `docs/ruspack-format-v1.md` — формат RusPack v1;
- `docs/runtime-host.md` — RuntimeHost;
- `docs/security-model.md` — модель безопасности;
- `docs/reproducible-builds.md` — воспроизводимые сборки;
- `docs/language-reference.md` — полный справочник синтаксиса языка;
- `docs/spikes/appended-payload.md` — результаты Windows spike.
