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
- **Popup** (solo master, attivo di default): il volume master viene cambiato simulando i tasti
  volume della tastiera, così compare il popup del volume di Windows. In questa modalità il passo
  va a multipli del 2%. Il popup di Windows mostra solo il master, quindi le app non lo hanno.

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

Diagnostica: `OpenDeckMixer.exe --list` stampa il volume master e le app con una sessione audio,
utile per sapere il nome esatto del processo.

## Struttura

- `src/OpenDeckMixer/Plugin.cs`: protocollo plugin (WebSocket) e gestione degli eventi
- `src/OpenDeckMixer/AudioMixer.cs`: Core Audio di Windows tramite NAudio.Wasapi
- `plugin/it.thecorpscloud.mixer.sdPlugin/`: manifest, icone e pannello impostazioni
