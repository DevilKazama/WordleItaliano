# Wordle Italiano

Applicazione desktop Windows WPF offline in C# .NET 8.

## Funzioni

- Parola italiana di 5 lettere, 6 tentativi.
- Colori Wordle classici con gestione corretta delle lettere duplicate.
- Tastiera fisica e tastiera virtuale opzionale.
- Parola del giorno deterministica: data base `2026-01-01`, indice calcolato dai giorni trascorsi.
- Salvataggio locale automatico di partita e statistiche.
- Tema chiaro/scuro, schermata iniziale, statistiche e guida.
- Nessuna connessione internet necessaria durante l'uso.

## Parole

I file modificabili sono in `WordleItaliano/Data`:

- `validWords.json`: parole accettate nei tentativi.
- `dailyWords.json`: parole usate come soluzione giornaliera.

Per cambiare il nome del collega nella schermata iniziale, modifica `WordleItaliano/appsettings.json`.

## Compilazione

Da questa cartella:

```powershell
cd .\WordleItaliano
$root=(Resolve-Path ..).Path
$env:TEMP=Join-Path $root '.tmp'
$env:TMP=$env:TEMP
$env:APPDATA=Join-Path $root '.appdata'
$env:LOCALAPPDATA=Join-Path $root '.localappdata'
$env:DOTNET_CLI_HOME=Join-Path $root '.dotnet'
$env:NUGET_PACKAGES=Join-Path $root '.nuget\packages'
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:APPDATA,$env:LOCALAPPDATA,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES | Out-Null
dotnet restore --configfile .\NuGet.Config
dotnet build -c Release --no-restore
```

## Pubblicazione self-contained Windows x64

```powershell
.\publish-win-x64.ps1
```

L'eseguibile e tutti i file necessari saranno in `WordleItaliano/publish/win-x64`.
