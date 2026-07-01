# SlavLang

**SlavLang — древнерусский языкъ программированія словѣнъ и руси.**

SlavLang не есть переводъ английскаго синтаксиса и не оболочка надъ чужимъ
языкомъ. Онъ речетъ по-славѧньски: книги `възвати`, въ програмѣ княжитъ
`Кнѧзь`, слово `да` полагаетъ имѧ, `єсть` и `се` связываютъ значеніе, а
`речи` выводитъ гласъ на свѣтъ.

Съставникъ `slavc.exe` беретъ писаніе `.slav` и творитъ едино самостоящее
исполняемое тѣло `.exe`. Рядомъ не требуются .NET Runtime, DLL, PDB или иные
приложенія: всё потребное входитъ въ сътворенный образъ.

## Чьто уже въ дѣлѣ

- съставленіе `.slav` въ C# и управимую съборку чрезъ Roslyn;
- съставленіе въ памяти, безъ призыва `csc`, MSBuild или пользовательскаго кода;
- самостоящій single-file съставникъ `slavc.exe`;
- въстроенные reference assemblies .NET 10 и RuntimeHost;
- сосудъ SlavPack v1 съ CRC-32, SHA-256 и Brotli;
- пускъ съборки прямо изъ памяти;
- древнерусскія повелѣнія `сътворити`, `бѣжати`, `явити`, `зрѣти`, `увѣрити`,
  `здравіе` и `вѣсть`;
- древнерусскія знамена CLI: `--изходъ`, `--цѣль`, `--образъ`, `--съсылка`,
  `--основа`, `--безъ-сжатия`, `--переписати`;
- кириллица въ писаніяхъ, именахъ и выводѣ;
- повторяемое съставленіе;
- испытанія формата, упаковщика, съставника и RuntimeHost;
- GitHub Actions для CI и выпуска по тэгамъ `v*`.

## Скорый починъ

Для съставленія самаго проекта потребно:

- Windows x64;
- PowerShell 7 или Windows PowerShell;
- .NET SDK 10.0.300.

Изъ кореня хранилища сотвори:

```powershell
.\eng\build.ps1
```

Скриптъ восстановитъ зависимости, съставитъ рѣшеніе, пуститъ испытанія и
опубликуетъ RuntimeHost и самостоящій съставникъ.

Готовый съставникъ явится здѣсь:

```text
artifacts/slavc/win-x64/slavc.exe
```

Сътвори примѣръ:

```powershell
.\artifacts\slavc\win-x64\slavc.exe сътворити .\examples\hello.slav
.\examples\hello.exe
```

Ожидаемый гласъ:

```text
Здравъ из SlavLang
```

Послѣ публикаціи `slavc.exe` можно нести на иную Windows x64 машину. Для его
работы не нужны .NET SDK и .NET Runtime.

## Рѣчь SlavLang

SlavLang имать свою модель: зависимости възваются, входъ есть `Кнѧзь`, а
скобки, запятыя и знаки `+`, `=`, `<`, `>` въ писаніи не потребны.

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

Полный свитокъ командъ и выраженій лежитъ въ
[`docs/language-reference.md`](docs/language-reference.md). Дѣлающая рядостройка:
[`examples/sort.slav`](examples/sort.slav).

## Повелѣнія `slavc`

Общій образъ:

```text
slavc <повелѣнь> [реченія]
```

### `сътворити`

Творитъ самостоящій исполняемый файлъ:

```powershell
slavc сътворити <file.slav> [знамена]
```

Примѣры:

```powershell
slavc сътворити .\hello.slav
slavc сътворити .\hello.slav --изходъ .\out\hello
slavc сътворити .\hello.slav --образъ испытъ --переписати
slavc сътворити .\application.slav --съсылка .\Library.dll
```

Знамена:

| Знамѧ | Дѣло |
| --- | --- |
| `-и, --изходъ <path>` | путь исходящаго файла |
| `-ц, --цѣль <rid>` | цѣлевая среда; нынѣ въ строю `win-x64` |
| `-о, --образъ <испытъ\|выпускъ>` | образъ съставленія |
| `--съсылка <path>` | прибавити управимую DLL; можно многажды |
| `--основа <path>` | указати RuntimeHost-основу |
| `--безъ-сжатия` | не сжимати записи Brotli |
| `--сохранити-отладку` | хранити portable PDB въ сосудѣ при испытномъ образѣ |
| `--явити-кодъ` | записати рожденный `.generated.cs` |
| `--повторяемо` | явно просити повторяемую съборку |
| `--подробно` | оставлено для широкой вѣдомости |
| `--переписати` | дозволити замѣну сущего исхода |

Аще `--изходъ` не данъ, файлъ творится рядомъ съ писаніемъ и принимаетъ имя
писанія. Для Windows окончаніе `.exe` прибавляется само.

Сущій исходъ не переписывается безъ `--переписати`. Запись идетъ чрезъ временный
файлъ, потомъ сосудъ отворяется и провѣряется; лишь послѣ доброй провѣрки онъ
становится исходомъ.

### `бѣжати`

Сътворяетъ писаніе во временной полатѣ, пускаетъ его и возвращаетъ кодъ исхода:

```powershell
slavc бѣжати .\hello.slav
slavc бѣжати .\args.slav --образъ испытъ -- first second "third value"
```

Реченія послѣ `--` даются самой программѣ.

### `явити`

Явитъ C#, рожденный изъ `.slav`, но не творитъ и не пускаетъ его:

```powershell
slavc явити .\hello.slav
```

### `зрѣти`

Зритъ manifest и записи готоваго SlavPack-файла:

```powershell
slavc зрѣти .\hello.exe
```

### `увѣрити`

Провѣряетъ footer, CRC-32, границы, manifest, SHA-256 payload и каждой записи,
а равно исправность Brotli:

```powershell
slavc увѣрити .\hello.exe
```

Добрый исходъ:

```text
ИСПРАВЕНЪ: C:\полный\путь\hello.exe
```

### `здравіе`

Показываетъ здравіе установленнаго съставника:

```powershell
slavc здравіе
```

Въ гласъ входятъ вѣрсія съставника и языка, TFM, RID, чинъ SlavPack, протоколъ
RuntimeHost и число въстроенныхъ ссылокъ.

### `вѣсть`

```powershell
slavc вѣсть
slavc --вѣсть
```

## Стороннія DLL

Управимая зависимость дается явно:

```powershell
slavc сътворити .\application.slav `
    --съсылка .\libs\Library.dll `
    --съсылка .\libs\Library.Dependency.dll
```

DLL служитъ Roslyn metadata reference и входитъ въ SlavPack какъ
`ManagedAssembly`. Съставникъ не грузитъ её чрезъ reflection и не исполняетъ кодъ
изъ неё.

Транзитивныя зависимости пишутся явно. Нативныя DLL и P/Invoke не входятъ въ
сей чинъ.

## RuntimeHost-основа

Обычный `slavc.exe` содержитъ host template для своего RID и извлекаетъ его въ
безопасный временный кэшъ:

```text
%TEMP%\SlavLang\host-templates\
```

Для разработки можно указати основу знаменемъ:

```powershell
slavc сътворити .\hello.slav --основа .\RuntimeHost.exe
```

или перемѣнною среды:

```powershell
$env:SLAVLANG_HOST_TEMPLATE = 'C:\path\RuntimeHost.exe'
slavc сътворити .\hello.slav
```

Обычнымъ людямъ основу задавати не надо.

## Съставленіе вручную

Полное автоматическое съставленіе:

```powershell
.\eng\build.ps1 -Configuration Release -Rid win-x64
```

Можно дати вѣрсію отъ тэга:

```powershell
.\eng\build.ps1 -Configuration Release -Rid win-x64 -Version 0.1.0
```

Отдѣльныя дѣла:

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

Сначала публикуется RuntimeHost, потомъ `slavc`; при публикаціи съставника готовая
основа входитъ въ `slavc.exe`.

Провѣрка лада кода:

```powershell
dotnet format .\SlavLang.slnx --verify-no-changes --no-restore
```

## Выпускъ

Въ `.github/workflows/release.yml` положенъ чинъ выпуска. На тэгъ вида
`vX.Y.Z` GitHub Actions:

1. провѣряетъ тэгъ;
2. съставляетъ автономный `slavc.exe`;
3. провѣряетъ работу безъ `dotnet` въ `PATH`;
4. творитъ zip и `SHA256SUMS.txt`;
5. создаетъ предварительный выпускъ GitHub.

## Рожденныя тѣла

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

`artifacts` въ Git не входитъ.

## Устройство

```text
program.slav
    ↓
SlavLang чело языка
    ↓
C# + source mapping
    ↓
Roslyn Emit въ память
    ↓
Program.dll + optional portable PDB
    ↓
SlavPackWriter
    ↓
[self-contained RuntimeHost][entries][manifest][footer]
    ↓
единый executable
```

При запускѣ RuntimeHost:

1. беретъ свой путь чрезъ `Environment.ProcessPath`;
2. читаетъ footer съ конца executable;
3. провѣряетъ CRC, вѣрсіи, offsets, limits и SHA-256;
4. читаетъ и вѣритъ manifest;
5. распаковываетъ и вѣритъ управимыя записи;
6. грузитъ entry assembly чрезъ особый `AssemblyLoadContext`;
7. зоветъ `Assembly.EntryPoint`;
8. передаетъ реченія и возвращаетъ кодъ исхода.

Поддержаны входы `void`, `int`, `Task` и `Task<int>` съ `string[] args` или безъ.

## Строеніе хранилища

```text
src/
├── SlavLang.Compiler/       # чело языка и Roslyn-съставленіе
├── SlavLang.Pack.Format/    # модели, footer, manifest, reader
├── SlavLang.Pack.Writer/    # атомная упаковка executable
├── SlavLang.RuntimeHost/    # провѣрка, загрузка и пускъ
└── SlavLang.Cli/            # повелѣнія slavc

tests/
├── SlavLang.Compiler.Tests/
├── SlavLang.Pack.Format.Tests/
├── SlavLang.Pack.Writer.Tests/
└── SlavLang.RuntimeHost.Tests/

docs/                       # устройство, форматъ и безопасность
eng/build.ps1               # чинъ локальнаго съставленія
examples/hello.slav         # малый примѣръ
examples/sort.slav          # рядостройка
```

## Безопасность

Во время `slavc сътворити`:

- пользовательский кодъ не исполняется;
- пользовательскія DLL не грузятся чрезъ reflection;
- не пускаются analyzers, source generators, MSBuild и NuGet scripts;
- не рождаются дочернія процессы `dotnet`, `csc` или shell;
- manifest и offsets считаются недовѣренными;
- провѣряются переполненія, пересѣченія, размѣры и пути записей;
- managed DLL не извлекаются рядомъ съ executable.

SHA-256 зрѣетъ поврежденіе, но не доказываетъ происхожденія. Authenticode
полагается токмо послѣ окончанія упаковки.

## Вѣдомости о погрѣхахъ

Погрѣхи чела языка имѣютъ коды `SLAV1xxx`, CLI — `SLAVC1xxx`, среды
съставленія — `SLAVC2xxx`, формата — `SLAP1xxx`, RuntimeHost — `SLAH1xxx`.

Примѣръ:

```text
C:\project\bad.slav(2,1): погрѣхъ SLAV1001: Невѣдома постройка: ...
```

Для слѣда загрузки съборокъ RuntimeHost поставь:

```powershell
$env:SLAVLANG_HOST_TRACE = '1'
.\program.exe
```

## Свитки

- `docs/architecture.md` — устройство;
- `docs/adr/0001-runtime-host-and-appended-payload.md` — архитектурное рѣшеніе;
- `docs/slavpack-format-v1.md` — чинъ SlavPack v1;
- `docs/runtime-host.md` — RuntimeHost;
- `docs/security-model.md` — безопасность;
- `docs/reproducible-builds.md` — повторяемыя съборки;
- `docs/language-reference.md` — полный свитокъ языка;
- `docs/releasing.md` — чинъ выпуска;
- `docs/spikes/appended-payload.md` — Windows spike.
