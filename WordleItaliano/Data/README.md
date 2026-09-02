# Liste parole

- `validWords.json`: parole italiane di 5 lettere accettate come tentativi.
- `validWords6.json` / `validWords7.json`: parole accettate per il bonus random.
- `dailyWords.json`: parole di 5 lettere usabili come soluzioni giornaliere.
- `bonusWords5.json`, `bonusWords6.json`, `bonusWords7.json`: soluzioni del bonus random.

Le soluzioni sono piu' selettive dei tentativi: evitano nomi propri, parole segnalate come brutte, forme molto rare e parole con frequenza italiana troppo bassa. I tentativi restano piu' permissivi per non bloccare parole valide durante la partita.

Fonti principali:

- `pietroppeter/wordle-it`, soprattutto `dict/curated.txt` per le soluzioni da 5 lettere.
- `pietroppeter/wordle-it`, `dict/word_list.txt` e `dict/60_000_parole.txt` per parole accettate e bonus.

La rigenerazione e' gestita da `tools/generate_word_lists.py`. Se trovi una parola da escludere, aggiungila alla `BLOCKLIST` dello script e rigenera i JSON.
