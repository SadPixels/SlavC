# Выпускъ SlavLang

Выпускъ творится тэгомъ вида `vX.Y.Z`.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

GitHub Actions чинъ `выпускъ`:

1. увѣритъ образъ тэга;
2. передастъ вѣрсію въ `eng/build.ps1 -Version`;
3. сътворитъ самостоящій `slavc.exe` для `win-x64`;
4. провѣритъ ходъ безъ `dotnet` въ `PATH`;
5. упакуетъ `slavc.exe` и `README.md`;
6. положитъ `SHA256SUMS.txt`;
7. создастъ предварительный выпускъ GitHub.

Команды въ испытаніи употребляютъ древнерусскій CLI:

```powershell
slavc вѣсть
slavc сътворити .\examples\hello.slav --изходъ .\hello --переписати
slavc увѣрити .\hello.exe
```
