# OpenDeck Mixer

Plugin per [OpenDeck](https://github.com/nekename/OpenDeck) che controlla il mixer audio di Windows
da manopole e tasti. Pensato per l'Ajazz AKP03E (con il plugin
[opendeck-akp03](https://github.com/4ndv/opendeck-akp03)), ma funziona con qualsiasi dispositivo
supportato da OpenDeck.

## Azioni

| Azione | Manopola | Tasto |
|---|---|---|
| **Volume master** | gira: volume del dispositivo di uscita predefinito · premi: muto | premi: muto · il titolo mostra il volume |
| **Volume app** | gira: volume di un'app · premi: muto dell'app | premi: muto dell'app · il titolo mostra il volume |

Impostazioni (pannello di OpenDeck):

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

1. Installa OpenDeck e il plugin del dispositivo (per l'AKP03E: opendeck-akp03).
2. Compila il pacchetto (vedi sotto) oppure prendi `it.thecorpscloud.mixer.plugin.zip`.
3. In OpenDeck: **Plugins → Install from file** e scegli lo zip.
4. Trascina "Volume master" o "Volume app" sulle manopole.

## Build

Serve l'SDK .NET 10. Da Linux/WSL:

```bash
./build.sh
```

Produce `dist/it.thecorpscloud.mixer.plugin.zip`: un `.exe` self-contained, quindi su Windows non
serve installare .NET.

Diagnostica:

- `OpenDeckMixer.exe --list` stampa il volume master e le app con una sessione audio, utile per
  sapere il nome esatto del processo.
- `OpenDeckMixer.exe --popup Spotify [--hold 5000]` mostra il popup di un'app senza toccarne il volume.

## Struttura

- `src/OpenDeckMixer/Plugin.cs`: protocollo plugin (WebSocket) e gestione degli eventi
- `src/OpenDeckMixer/AudioMixer.cs`: Core Audio di Windows tramite NAudio.Wasapi
- `src/OpenDeckMixer/MediaKeys.cs`: tasti volume simulati (popup di Windows per il master)
- `src/OpenDeckMixer/Osd.cs`: popup del volume delle app, con icona presa dall'eseguibile
- `plugin/it.thecorpscloud.mixer.sdPlugin/`: manifest, icone e pannello impostazioni
