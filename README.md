# Ajazz Mixer

Plugin per il software **Stream Dock** di Ajazz (lo stesso di Mirabox) che controlla il mixer audio
di Windows da manopole e tasti. Pensato per l'Ajazz AKP03E, funziona con i dispositivi gestiti
dal software Stream Dock.

È la versione per il software Ajazz di [opendeck-mixer](https://github.com/TheCorpsDev/opendeck-mixer),
che fa la stessa cosa per [OpenDeck](https://github.com/nekename/OpenDeck). Usa uno solo dei due
programmi alla volta: si contendono il dispositivo.

## Azioni

| Azione | Manopola | Tasto |
|---|---|---|
| **Volume master** | gira: volume del dispositivo di uscita predefinito · premi: muto | premi: muto · il titolo mostra il volume |
| **Volume app** | gira: volume di un'app · premi: muto dell'app | premi: muto dell'app · il titolo mostra il volume |

Impostazioni (pannello dell'azione):

- **App**: nome del processo (`Discord`, `Spotify`, `chrome`…). Con più nomi separati da virgola
  (`Spotify, chrome`) controlla il primo che ha una sessione audio aperta. Il pannello mostra le
  app che stanno usando l'audio: clic per aggiungerle.
- **Passo**: percentuale per ogni scatto della manopola (1–10, default 2).
- **Popup** (attivo di default):
  - master: il volume viene cambiato simulando i tasti volume della tastiera, così compare il
    popup di Windows. In questa modalità il passo va a multipli del 2%.
  - app: compare un popup in stile Windows 11 in basso al centro, con icona e nome dell'app,
    barra e volume. Segue tema chiaro/scuro e colore principale di Windows.

L'app viene cercata su tutti i dispositivi di uscita attivi, non solo su quello predefinito.
Se ha più sessioni (es. un browser) vengono regolate tutte insieme.

## Installazione

1. Scarica `ajazz-mixer.zip` dalle [Release](../../releases/latest) ed estrailo.
2. Tasto destro su `install.ps1` → **Esegui con PowerShell**. Copia il plugin in
   `%APPDATA%\HotSpot\StreamDock\plugins`.
3. Riavvia il software Stream Dock AJAZZ (chiudilo anche dall'icona vicino all'orologio).
4. Trova la categoria **Mixer** tra le azioni e trascina "Volume master" o "Volume app" sulle manopole.

In alternativa puoi copiare a mano la cartella `it.thecorpscloud.mixer.sdPlugin` in quel percorso.

## Build

Serve l'SDK .NET 10. Da Linux/WSL:

```bash
./build.sh
```

Produce `dist/ajazz-mixer.zip`: un `.exe` self-contained (su Windows non serve installare .NET)
più lo script di installazione.

Diagnostica:

- `AjazzMixer.exe --list` stampa il volume master e le app con una sessione audio, utile per
  sapere il nome esatto del processo.
- `AjazzMixer.exe --popup Spotify [--hold 5000]` mostra il popup di un'app senza toccarne il volume.

## Differenze rispetto alla versione OpenDeck

- Le manopole sono dichiarate come `Knob` (il nome usato dall'SDK Stream Dock) invece di `Encoder`.
- Il manifest usa `SDKVersion` 1, il campo `Software` e icone PNG.
- Installazione con `install.ps1` invece di "Install from file".
- Gli eventi che il plugin non gestisce finiscono nel log, per adattarsi alle versioni del software.

## Struttura

- `src/OpenDeckMixer/Plugin.cs`: protocollo plugin (WebSocket) e gestione degli eventi
- `src/OpenDeckMixer/AudioMixer.cs`: Core Audio di Windows tramite NAudio.Wasapi
- `src/OpenDeckMixer/MediaKeys.cs`: tasti volume simulati (popup di Windows per il master)
- `src/OpenDeckMixer/Osd.cs`: popup del volume delle app, con icona presa dall'eseguibile
- `plugin/it.thecorpscloud.mixer.sdPlugin/`: manifest, icone PNG e pannello impostazioni
- `assets/icons/`: sorgenti SVG delle icone

## Licenza

[MIT](LICENSE). Usa [NAudio](https://github.com/naudio/NAudio) (MIT).
