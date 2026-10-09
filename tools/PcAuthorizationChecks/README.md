# Autorizzazione PC e sequenze: verifiche isolate

La release legge esclusivamente MachineGuid attraverso PcIdentity.cs condiviso
con lo strumento portable. La lista dei tre hash e incorporata in PcAuthorization.
I nomi dei giocatori non vengono modificati. Non vengono verificati rete, IP,
nome del computer o sessione remota. Chrome Remote Desktop continua a eseguire
il programma sulla postazione: serve soltanto una prova del normale focus/input.

## Separazione dei dati

La release usa la posizione canonica LocalApplicationData/WordleItaliano.
WORDLE_STORAGE_FOLDER e WORDLE_TEST_PC_CODE sono compilati soltanto nella
build distinta WordleItaliano.TestBuild, usata dai progetti sotto tools.
La build distribuita non contiene questi punti di ingresso.

Su un PC esterno o con identita non leggibile lo StorageService usa soltanto
la sottocartella training. Non recupera il diario ufficiale e non carica
partite/statistiche ufficiali. training/training.json contiene esclusivamente
la partita casuale (soluzione, tentativi, esito e tempo), scritta atomicamente.
Le preferenze copiate possono essere lette; eventuali modifiche vengono salvate
solo in training/userSettings.json, senza sovrascrivere le originali.
Le due raccolte non vengono fuse automaticamente al ritorno sul PC autorizzato.

## Sequenza predisposta, non attiva

OfficialSequence.ActivationDate e null: nessuna data e stata scelta.
Gli identificativi sono legacy-v1 e frozen-v2. Un salvataggio senza identificativo
viene interpretato come legacy-v1; una partita gia iniziata mantiene tale scelta.
Date future, identificativi sconosciuti o v2 non attiva richiedono verifica:
si conserva un backup protetto e si sospende la partita, non si cambia parola.
I nuovi salvataggi non contengono le soluzioni competitive.

frozen-v2 incorpora i quattro dizionari dailyWords e bonusWords5/6/7 della
release corrente; gli hash SHA-256 sono verificati in OfficialSequence sulle
parole normalizzate e deduplicate nell'ordine originale, unite con LF e codificate
UTF-8 senza BOM. Spaziatura JSON e fine riga del checkout non cambiano l'identita.
Le liste vengono normalizzate con WordRepository.Normalize. L'esclusione delle
parole legacy usa l'ordine originale; la selezione v2 usa ordine lessicografico
StringComparer.Ordinal, senza dipendere dalla cultura di Windows.
Per la data si usa yyyy-MM-dd con InvariantCulture. Il messaggio UTF-8 e:
WordleItaliano|frozen-v2|DATA|TIPO, con TIPO daily, bonus oppure length.
SHA-256, primi quattro byte interpretati come uint little-endian, modulo del
numero di candidati. La lunghezza Bonus e 5 + seed(length) modulo 3.
Entrambi i candidati legacy dello stesso giorno sono esclusi da entrambe le
estrazioni v2, anche quando la lunghezza del Bonus coincide con la Giornaliera.
La sequenza legacy mantiene FNV-1a e i sali gia esistenti.
Una modifica dei dizionari incorporati richiede un nuovo identificativo:
l'hash evita cambiamenti accidentali sotto lo stesso nome.

Il confronto copre le risorse della release precedente del progetto. Non puo
garantire differenze da client con dizionari/algoritmi deliberatamente alterati.
Hash, codice e dizionari sono nel client: non sono segreti e non impediscono
reverse engineering, patch dell'eseguibile o clonazione dell'identita Windows.
La sequenza attuale rimane prevedibile con le vecchie release finche non viene
concordata e attivata la nuova data su tutti i PC.

## Esecuzione

```powershell
dotnet build WordleItaliano.sln -c Release --no-restore
dotnet run --project tools/PcAuthorizationChecks/PcAuthorizationChecks.csproj -c Release
```

Queste verifiche usano identita simulate e cartelle GUID isolate, percorsi reali
del ViewModel e dello StorageService, una copia dei dati reali e letture del
registro corrente. Non richiedono di giocare manualmente molte partite.
La release viene caricata separatamente per controllare che ignori le variabili
di test. La copia reale e letta dal percorso della postazione di sviluppo,
mai riscritta nell'originale.
Le altre tre suite restano applicabili tramite la build TestBuild separata.

Da verificare fisicamente: avvio sui due PC degli altri giocatori, avvio esterno
in Allenamento e un normale tentativo tramite Chrome Remote Desktop.
Nessuna pubblicazione e nessuna attivazione della nuova sequenza avvengono qui.
