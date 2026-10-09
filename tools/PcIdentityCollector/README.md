# Raccolta codici PC

Strumento separato da Wordle, senza accesso alla rete o ai dati di gioco.
Pubblicazione portable Windows x64 self-contained (.NET 8 / WPF).
Windows 10 deve essere una versione supportata da .NET 8.

Copia l'intera cartella portable su ogni PC x64, avvia Wordle-Codice-PC.exe,
premi Copia codice e associa il codice al nome del giocatore.
Lo strumento non autorizza la postazione.

## Contratto di identita v1

Sorgente: HKLM\SOFTWARE\Microsoft\Cryptography, valore MachineGuid di tipo
stringa, vista registro Registry64, apertura in sola lettura.
Normalizzazione: Trim, Guid.TryParse, rifiuto del GUID vuoto, Guid.ToString("D")
e ToLowerInvariant. Nessun identificativo sostitutivo.
Messaggio da sottoporre a hash: prefisso esatto
`WordleItaliano|PC-Installation|v1|` seguito dal GUID normalizzato.
Codifica UTF-8 senza BOM, hash SHA-256 completo, 64 cifre esadecimali maiuscole.
Formato esposto: `WIPC1-` seguito dalle 64 cifre, senza separatori aggiuntivi.
PcIdentity.cs e indipendente dalla finestra ed e riutilizzabile nel gioco.

L'identificativo originale non viene mostrato, esportato o scritto su disco.
L'hash non e un segreto ne una protezione contro reverse engineering.
Una reinstallazione Windows puo cambiare il codice. Una clonazione puo
conservare MachineGuid e quindi duplicarlo: verificare i tre codici prima
di autorizzare i PC. Il codice identifica l'installazione, non il giocatore.

## Compilazione e verifica

```powershell
dotnet publish tools/PcIdentityCollector/PcIdentityCollector.csproj -c Release -r win-x64 --self-contained true -o .tmp/portable-pc-code
$p = Start-Process .tmp/portable-pc-code/Wordle-Codice-PC.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
```

Zero indica test superati: normalizzazione, formato, identificativi diversi,
errori senza codice sostitutivo e letture ripetute della postazione corrente.
Il self-test non mostra identificativi e non legge i salvataggi.
