# Sentinel ([English](README.md) | **Italiano**)

**Sentinel** è una leggera applicazione per la system tray di Windows progettata per prevenire lo standby o la sospensione del computer. Funziona simulando una pressione di un tasto (F15) solo quando l'utente è inattivo, garantendo che la sessione rimanga attiva senza interferire con il lavoro (es. Excel).

## Funzionalità

- **Previene lo Standby**: Mantiene il PC attivo quando lo stato è "Attivo".
- **Intelligente**: Simula l'attività **solo** se non stai usando il PC. Se stai scrivendo o muovendo il mouse, Sentinel non interferisce.
- **Icona System Tray**: Supporta icone personalizzate (`sentinel.png`) o l'icona predefinita.
- **Finestra Impostazioni**: Gestisci tutte le opzioni da un'unica interfaccia.
- **Sospensione Programmata**: Imposta un intervallo orario (es. 13:00 - 14:00) in cui Sentinel va automaticamente in **pausa** (permettendo il blocco schermo durante la pausa pranzo).
- **Avvio Automatico**: Opzione per avviare Sentinel all'accensione di Windows.

## Installazione

1.  Scarica l'ultima release.
2.  Posiziona `Sentinel.exe` una cartella a tua scelta.
3.  Avvia `Sentinel.exe`.

## Utilizzo

- **Attivo/Inattivo**: Il tool parte attivo. Puoi disattivarlo dalla finestra Impostazioni.
- **Impostazioni**: Doppio click sull'icona nella tray (o tasto destro -> "Impostazioni...") per configurare.
- **Sospensione Programmata**: Abilita la spunta nella finestra Impostazioni e scegli l'orario di pausa.
- **Esci**: Tasto destro sull'icona -> "Esci".

## Compilazione

Il progetto usa C# e .NET Framework 4.0.
Per compilare, esegui semplicemente il file `build.bat` incluso.

## Licenza
MIT License
