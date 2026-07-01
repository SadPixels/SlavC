# SlavLang

SlavLang — прототип русского системного языка, развиваемого как самостоятельная
альтернатива C++, и его компилятора за .NET 10. Компилятор
преобразует файл `.slav` в один автономный исполняемый файл. Для запуска результата
не нужны установленный .NET Runtime, DLL, PDB либо другие файлы рядом.

Текущий MVP поддерживает Windows x64 (`win-x64`) и консольные приложения.

## Что уже работает

- компиляция `.slav` в C# и далее в управляемую сборку через Roslyn;
- компиляция полностью в памяти, без запуска `csc`, MSBuild либо пользовательского
  кода;
- автономный single-file компилятор `slavc.exe`;
- встроенные reference assemblies .NET 10 и RuntimeHost;
- контейнер SlavPack v1 с CRC-32, SHA-256 и Brotli;
- запуск сборки непосредственно из памяти;
- команды `сотворити`, `бѣжати`, `явити`, `зрѣти`, `увѣрити`, `лекарь` и `вѣсть`;
- кириллица в исходных файлах, именах и выводе;
- детерминированная компиляция;
- тесты формата, упаковщика, компилятора и RuntimeHost.

## Быстрый старт

Для сборки самого проекта требуется:

- Windows x64;
- PowerShell 7 либо Windows PowerShell;
- .NET SDK 10.0.300.

Из корня репозитория выполните:

```powershell
.\eng\build.ps1
```

Скрипт последовательно восстанавливает зависимости, собирает solution, запускает
тесты и публикует RuntimeHost и автономный компилятор.

Готовый компилятор появится здесь:

```text
artifacts/slavc/win-x64/slavc.exe
```

Скомпилируйте пример:

```powershell
.\artifacts\slavc\win-x64\slavc.exe сотворити .\examples\hello.slav
.\examples\hello.exe
```

Ожидаемый вывод:

```text
Здравъ из SlavLang
```

После публикации `slavc.exe` можно перенести на другую Windows x64 машину. Для его
работы не нужны .NET SDK и .NET Runtime.

## Синтаксис SlavLang

SlavLang использует собственную модель: зависимости призываются, точкой входа
служит `Кнѧзь`, а запятые и скобки в синтаксисе не применяются.

```slav
възвати Сварога
възвати Ярило

Кнѧзь
    да числа єсть рядъ 3 и 1 и 2
    за i отъ 0 до длъгота числа
        речи числа по i
    коньць
коньць
```

Полное описание команд, выражений, встроенных функций и ограничений находится
в [справочнике языка](docs/language-reference.md). Рабочая пузырьковая рядострой:
[`examples/sort.slav`](examples/sort.slav).

## Команды `slavc`

Общий формат:

```text
slavc <команда> [аргументы]
```

### `сотворити`

Создаёт автономный исполняемый файл:

```powershell
slavc сотворити <file.slav> [параметры]
```

Примеры:

```powershell
slavc сотворити .\hello.slav
slavc сотворити .\hello.slav -o .\out\hello
slavc сотворити .\hello.slav -c debug --force
slavc сотворити .\application.slav --reference .\Library.dll
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
| `--verbose` | Зарезервирован за расширенной диагностики |
| `--force` | Разрешить замену существующего output |

Если `--output` не задан, файл создаётся рядом с исходником и получает имя
исходного файла. Для Windows расширение `.exe` добавляется автоматически.

Существующий output не перезаписывается без `--force`. Запись выполняется через
временный файл, затем контейнер повторно открывается и полностью проверяется.
Только после успешной проверки временный файл атомарно становится output.

### `бѣжати`

Компилирует программу во временный каталог, запускает её и возвращает её код
завершения:

```powershell
slavc бѣжати .\hello.slav
slavc бѣжати .\args.slav -c debug -- first second "third value"
```

Аргументы после `--` передаются пользовательской программе.

### `явити`

Показывает C#, сгенерированный из `.slav`, но не компилирует и не запускает его:

```powershell
slavc явити .\hello.slav
```

Команда полезна при разработке frontend и диагностике source mapping.

### `зрѣти`

Показывает manifest и entries готового SlavPack-файла:

```powershell
slavc зрѣти .\hello.exe
```

`зрѣти` не загружает и не выполняет пользовательскую сборку.

### `увѣрити`

Проверяет footer, CRC-32, границы, manifest, SHA-256 payload и каждой entry, а
также корректность Brotli:

```powershell
slavc увѣрити .\hello.exe
```

Успешный результат:

```text
OK: C:\полный\путь\hello.exe
```

### `лекарь`

Показывает параметры установленного компилятора:

```powershell
slavc лекарь
```

В вывод входят версии компилятора и языка, TFM, RID, версия SlavPack, протокол
RuntimeHost и количество встроенных reference assemblies.

### `вѣсть`

```powershell
slavc вѣсть
slavc --вѣсть
```

## Использование сторонних DLL

Управляемая зависимость передаётся явно:

```powershell
slavc сотворити .\application.slav `
    --reference .\libs\Library.dll `
    --reference .\libs\Library.Dependency.dll
```

DLL используется как Roslyn metadata reference и включается в SlavPack как
`ManagedAssembly`. Компилятор не загружает её через reflection и не выполняет код
из неё.

На текущем этапе транзитивные зависимости нужно перечислять явно. Нативные DLL и
P/Invoke дондеже не поддерживаются.

## RuntimeHost template

Обычный `slavc.exe` содержит host template за своего RID и автоматически извлекает
его в безопасный временный кэш:

```text
%TEMP%\SlavLang\host-templates\
```

Для разработки можно переопределить template параметром:

```powershell
slavc сотворити .\hello.slav --host-template .\RuntimeHost.exe
```

либо переменной окружения:

```powershell
$env:SLAVLANG_HOST_TEMPLATE = 'C:\path\RuntimeHost.exe'
slavc сотворити .\hello.slav
```

Обычным пользователям задавать template не требуется.

## Сборка проекта вручную

Полная автоматическая сборка:

```powershell
.\eng\build.ps1 -Configuration Release -Rid win-x64
```

Отдельные этапы:

```powershell
dotnet restore .\SlavLang.slnx
dotnet build .\SlavLang.slnx -c Release --no-restore
dotnet test .\SlavLang.slnx -c Release --no-build

dotnet publish .\src\SlavLang.RuntimeHost\SlavLang.RuntimeHost.csproj `
    -c Release -r win-x64 --self-contained true `
    -o .\artifacts\host-templates\win-x64

dotnet publish .\src\SlavLang.Cli\SlavLang.Cli.csproj `
    -c Release -r win-x64 --self-contained true `
    -o .\artifacts\slavc\win-x64
```

Важно: сначала публикуется RuntimeHost, затем `slavc`. При публикации компилятора
готовый host template встраивается в `slavc.exe`.

Проверка форматирования:

```powershell
dotnet format .\SlavLang.slnx --verify-no-changes --no-restore
```

## Результаты сборки

Основные каталоги:

```text
artifacts/
├── host-templates/
│   └── win-x64/
│       └── SlavLang.RuntimeHost.exe
├── slavc/
│   └── win-x64/
│       └── slavc.exe
└── test-output/
```

`artifacts` не входит в Git.

## Архитектура

```text
program.slav
    ↓
SlavLang frontend
    ↓
C# + source mapping
    ↓
Roslyn Emit в память
    ↓
Program.dll + optional portable PDB
    ↓
SlavPackWriter
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
`string[] args` либо без него.

## Структура репозитория

```text
src/
├── SlavLang.Compiler/       # frontend и Roslyn compilation
├── SlavLang.Pack.Format/    # модели, footer, manifest, reader
├── SlavLang.Pack.Writer/    # атомарная упаковка executable
├── SlavLang.RuntimeHost/    # проверка, загрузка и запуск
└── SlavLang.Cli/            # команды slavc

tests/
├── SlavLang.Compiler.Tests/
├── SlavLang.Pack.Format.Tests/
├── SlavLang.Pack.Writer.Tests/
└── SlavLang.RuntimeHost.Tests/

docs/                       # архитектура, формат и модель безопасности
eng/build.ps1               # release pipeline за локальной сборки
examples/hello.slav          # минимальный пример
Tasks.md                    # полный roadmap проекта
```

## Безопасность

Во время `slavc сотворити`:

- пользовательский код не выполняется;
- пользовательские DLL не загружаются через reflection;
- не запускаются analyzers, source generators, MSBuild и NuGet scripts;
- не создаются дочерние процессы `dotnet`, `csc` либо shell;
- manifest и offsets считаются недоверенными;
- проверяются переполнения, пересечения, размеры и пути entries;
- managed DLL не извлекаются рядом с executable.

SHA-256 обнаруживает повреждение, но не подтверждает происхождение файла.
Authenticode должен применяться только после завершения упаковки.

## Диагностика

Ошибки frontend имеют коды `SLAV1xxx`, CLI — `SLAVC1xxx`, среды компиляции —
`SLAVC2xxx`, формата — `SLAP1xxx`, RuntimeHost — `SLAVH1xxx`.

Пример:

```text
C:\project\bad.slav(2,1): error SLAV1001: Неизвестная конструкция: ...
```

Неизвестные ошибки Roslyn сохраняют исходный код `CSxxxx`.

Для трассировки загрузки сборок RuntimeHost установите:

```powershell
$env:SLAVLANG_HOST_TRACE = '1'
.\program.exe
```

## Текущие ограничения

- официально проверен только `win-x64`;
- поддерживаются только консольные приложения;
- frontend языка дондеже содержит минимальный набор конструкций;
- нативные зависимости и P/Invoke не поддерживаются;
- транзитивные managed dependencies указываются вручную;
- resources и Release PDB ещё не завершены;
- Linux, macOS, ARM64, signing и notarization находятся в roadmap;
- проверка на чистой VM без .NET должна выполняться перед релизом.

Полный план развития и критерии готовности находятся в `Tasks.md`.

## Дополнительная документация

- `docs/architecture.md` — архитектура;
- `docs/adr/0001-runtime-host-and-appended-payload.md` — архитектурное решение;
- `docs/slavpack-format-v1.md` — формат SlavPack v1;
- `docs/runtime-host.md` — RuntimeHost;
- `docs/security-model.md` — модель безопасности;
- `docs/reproducible-builds.md` — воспроизводимые сборки;
- `docs/language-reference.md` — полный справочник синтаксиса языка;
- `docs/spikes/appended-payload.md` — результаты Windows spike.
