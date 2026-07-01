# Выпуск RusLang

Официальные выпуски создаются GitHub Actions из тегов вида
`v<основная>.<дополнительная>.<исправление>[-суффикс]`.

Первый рекомендуемый тег:

```text
v0.1.0-alpha.1
```

Перед созданием тега ветка `master` должна быть чистой, отправленной на GitHub,
а обычный CI — успешно завершён.

```powershell
git tag -a v0.1.0-alpha.1 -m "RusLang v0.1.0-alpha.1"
git push origin v0.1.0-alpha.1
```

Workflow `.github/workflows/release.yml`:

1. собирает RuntimeHost и `rusc.exe` как self-contained single-file для
   `win-x64`;
2. запускает все тесты;
3. временно убирает `dotnet` из `PATH` и проверяет автономный `rusc.exe`;
4. этим компилятором создаёт программу и запускает её без `dotnet`;
5. упаковывает `rusc.exe` и README;
6. создаёт SHA-256 checksum;
7. публикует GitHub Pre-release.

Пользователю и разработчику программ на RusLang достаточно одного `rusc.exe`.
Устанавливать .NET SDK или .NET Runtime не требуется. SDK нужен только участнику,
который собирает сам компилятор из исходного кода.
