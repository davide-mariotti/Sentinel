<p align="center">
  <img src="icon/sentinel.png" width="112" alt="Sentinel icon">
</p>

<h1 align="center">Sentinel</h1>

<p align="center">
  Keeps your Windows PC awake. That's it, that's the app.<br>
  <a href="#english">English</a> · <a href="#italiano">Italiano</a> · <a href="https://davide-mariotti.github.io/apps/sentinel">Website</a>
</p>

<p align="center">
  <a href="https://ko-fi.com/Z0X32ATNHL"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Buy me a coffee on Ko-fi"></a>
</p>

---

## English

### What it does

- Stops Windows from sleeping, turning off the screen or starting the screensaver, as long as it's on.
- Keeps Teams, Slack and friends from marking you "away" while you read a long document without touching anything.
- Presses **F15** only after 55 seconds without mouse or keyboard. F15 is a key your keyboard doesn't have, so no app reacts to it. While you're working, Sentinel does nothing at all.
- Takes a lunch break, if you want: pick a time slot (13:00-14:00 by default) and during that slot the PC is free to sleep.
- When you lock the PC (Win+L) the screen can turn off, but the PC stays awake: downloads and builds keep going.
- Optional battery alerts for laptops: unplug at X%, plug in at Y%. A battery that doesn't sit at 100% all day lives longer.
- Starts with Windows if you ask it to, quietly, in the tray.
- Speaks Italian or English, following the language of Windows.

### What it doesn't do

- It doesn't move the mouse or type anything you'd see.
- It doesn't need admin rights, doesn't install anything, doesn't touch your power plan. Settings live in one registry key of your user (`HKCU\SOFTWARE\SentinelApp`).
- It doesn't connect to the internet. Ever. No telemetry, no auto-updates, no account.
- It doesn't stop you from locking or shutting down the PC. Turn it off from the settings and Windows is back in charge right away.
- It doesn't run on macOS or Linux.
- It doesn't do your work for you. I tried.

### Download

1. Download [Sentinel.exe](https://github.com/davide-mariotti/Sentinel/raw/main/Sentinel.exe) (75 KB). Windows 10 or 11, nothing else to install.
2. Put it in a folder that stays put (say `Documents\Tools`), not in Downloads.
3. Double-click it. The first time, SmartScreen may complain because the exe isn't signed: *More info* → *Run anyway*. A signing certificate costs more than all the coffees this app has earned so far.

### How to use it

- The **eye** in the notification area, next to the clock, means it's working. **Crossed-out eye**: it's off, or on its lunch break.
- Double-click the icon for the settings, right-click → *Exit* to close it.
- Settings are saved as you change them. There's no Save button to forget.

### How it works

Every 60 seconds Sentinel:

1. checks whether you're in the scheduled pause, and if so hands control back to Windows;
2. tells Windows "keep the system and the screen on" (`SetThreadExecutionState`), or only the system if the PC is locked;
3. if mouse and keyboard have been still for 55 seconds, presses and releases F15.

Step 2 is what prevents sleep. Step 3 also resets the idle timers that the screensaver and chat apps look at.

### Build from source

No Visual Studio needed: Windows already ships the C# compiler of .NET Framework 4.

```cmd
build.bat
```

You get `Sentinel.exe` next to the sources. The icons come from `icon/make-icons.py` (Python, Pillow and Chrome), drawn with [Lucide](https://lucide.dev) glyphs.

### Support

If Sentinel saved you from an "appears away" or from a screensaver in the middle of a presentation, you can [buy me a coffee on Ko-fi](https://ko-fi.com/Z0X32ATNHL). That's how the next app gets written.

### License

[MIT](LICENSE). Do what you like with it, just don't blame me if your PC stays on all weekend.

---

## Italiano

### Cosa fa

- Impedisce a Windows di andare in standby, spegnere lo schermo o far partire lo screensaver, finché è attivo.
- Evita che Teams, Slack e compagnia ti segnino "assente" mentre leggi un documento lungo senza toccare niente.
- Preme **F15** solo dopo 55 secondi senza mouse né tastiera. F15 è un tasto che la tua tastiera non ha, quindi nessun programma ci reagisce. Mentre lavori, Sentinel non fa proprio niente.
- Va in pausa pranzo, se vuoi: scegli una fascia oraria (di base 13:00-14:00) e in quella fascia il PC è libero di dormire.
- Quando blocchi il PC (Win+L) lo schermo si può spegnere, ma il PC resta sveglio: download e build vanno avanti.
- Avvisi batteria per i portatili, se li accendi: stacca il caricatore al X%, attaccalo al Y%. Una batteria che non sta tutto il giorno al 100% dura di più.
- Si avvia con Windows, se glielo chiedi, e resta zitto nella tray.
- Parla italiano o inglese, in base alla lingua di Windows.

### Cosa non fa

- Non muove il mouse e non scrive niente che si veda.
- Non chiede permessi da amministratore, non si installa, non tocca le impostazioni di risparmio energia. Le impostazioni stanno in una chiave di registro del tuo utente (`HKCU\SOFTWARE\SentinelApp`).
- Non si collega a internet. Mai. Niente telemetria, niente aggiornamenti automatici, niente account.
- Non ti impedisce di bloccare o spegnere il PC. Se lo disattivi dalle impostazioni, Windows torna a decidere subito.
- Non gira su macOS o Linux.
- Non fa il lavoro al posto tuo. Ci ho provato.

### Scaricarlo

1. Scarica [Sentinel.exe](https://github.com/davide-mariotti/Sentinel/raw/main/Sentinel.exe) (75 KB). Windows 10 o 11, nient'altro da installare.
2. Mettilo in una cartella che non sposti (tipo `Documenti\Strumenti`), non in Download.
3. Doppio clic. La prima volta SmartScreen potrebbe lamentarsi perché l'exe non è firmato: *Ulteriori informazioni* → *Esegui comunque*. Il certificato per firmarlo costa più di tutti i caffè che questa app ha guadagnato finora.

### Come si usa

- L'**occhio** nell'area di notifica, vicino all'orologio, vuol dire che sta lavorando. **Occhio barrato**: è spento, o in pausa pranzo.
- Doppio clic sull'icona per le impostazioni, tasto destro → *Esci* per chiuderlo.
- Le impostazioni si salvano mentre le cambi. Non c'è un tasto Salva da dimenticare.

### Come funziona

Ogni 60 secondi Sentinel:

1. guarda se sei nella pausa programmata, e se sì restituisce il controllo a Windows;
2. dice a Windows "tieni acceso il sistema e lo schermo" (`SetThreadExecutionState`), o solo il sistema se il PC è bloccato;
3. se mouse e tastiera sono fermi da 55 secondi, preme e rilascia F15.

Il punto 2 è quello che evita lo standby. Il punto 3 azzera anche i contatori di inattività che guardano lo screensaver e le app di chat.

### Compilarlo

Non serve Visual Studio: Windows ha già il compilatore C# di .NET Framework 4.

```cmd
build.bat
```

Ottieni `Sentinel.exe` accanto ai sorgenti. Le icone le genera `icon/make-icons.py` (Python, Pillow e Chrome), con i glifi di [Lucide](https://lucide.dev).

### Supporto

Se Sentinel ti ha salvato da un "sembri assente" o da uno screensaver a metà presentazione, puoi [offrirmi un caffè su Ko-fi](https://ko-fi.com/Z0X32ATNHL). È così che nasce la prossima app.

### Licenza

[MIT](LICENSE). Fanne quello che vuoi, ma se il PC resta acceso tutto il weekend non prendertela con me.
