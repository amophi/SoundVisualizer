# Third-Party Licenses and Notices (SBOM)

This project (`SoundVisualizer`) uses the following third-party open-source libraries, frameworks, and models. We are grateful to the open-source community for their contributions.

| No. | Library Name | Version | License | Official Repository URL | Purpose and Main Features |
| --- | --- | --- | --- | --- | --- |
| 1 | NAudio | 2.3.0 | MIT License | github.com/naudio/NAudio | WASAPI loopback audio capture / Used as a library |
| 2 | Microsoft.ML.OnnxRuntime | 1.24.3 | MIT License | github.com/microsoft/onnxruntime | Model (ONNX) inference engine / Used as a library |
| 3 | WPF (.NET) | 10.0 | MIT License | github.com/dotnet/wpf | Desktop overlay UI rendering / Used as a framework |
| 4 | Pretendard | 1.3.9 | SIL Open Font License 1.1 | github.com/orioncactus/pretendard | UI font, embedded unmodified (Regular, SemiBold, Bold, ExtraBold, Black) / Bundled font |

## AI Models

### YAMNet (model weights and class map)
- **Files**: `SoundVisualizer/AIModel/yamnet.onnx`, `yamnet.data`, `yamnet_class_map.csv`
- **License**: Apache License 2.0 (Copyright The TensorFlow Authors)
- **Source**: [TensorFlow Model Garden YAMNet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet), included as an ONNX export (see `SoundVisualizer/AIModel/metadata.yaml`).
- **Description**: A pre-trained network that predicts 521 audio event classes. The app runs it unmodified with ONNX Runtime and maps its output to three sound types (ambient, speech, danger). The app checks the SHA-256 of both model files at load.
- **Class names**: The class names come from the AudioSet ontology, distributed under Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0); the AudioSet dataset is distributed under CC BY 4.0. https://research.google.com/audioset/

## Data

### AudioSet ontology (sound names and sound groups in the Sounds tab)
- **Files**: `SoundVisualizer.Core/Sounds/Data/SoundNames/*.json` (the 521 sound names in 18 languages) and `SoundVisualizer.Core/Sounds/Data/sound_groups.tsv` (which group each sound belongs to)
- **License**: Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0), https://creativecommons.org/licenses/by-sa/4.0/
- **Source**: "AudioSet Ontology" by Google Inc., [https://github.com/audioset/ontology](https://github.com/audioset/ontology) (commit d417d32)
- **Description**: The sound names shown in the Sounds tab are the AudioSet class names (the names YAMNet uses) and their translations into the app's languages, which are adaptations of the ontology. The sound groups were made by SoundVisualizer contributors by reading the ontology's hierarchy, and group names that reuse a sound name (Siren, Doorbell, etc.) are part of the same adaptation. Changes: the 521 YAMNet sound names were regrouped into everyday groups and translated. These files are distributed under the same CC BY-SA 4.0 license, as stated in [`SoundVisualizer.Core/Sounds/Data/NOTICE.txt`](SoundVisualizer.Core/Sounds/Data/NOTICE.txt) and in the header of `sound_groups.tsv`. The newly written group names (`GroupTitles/*.json`) and the app code are under this repository's license (GNU AGPL v3).

## Fonts

### Pretendard
- **License**: SIL Open Font License, Version 1.1, with Reserved Font Name "Pretendard"
- **Copyright**: (c) 2021, Kil Hyung-jin
- **Source**: [https://github.com/orioncactus/pretendard](https://github.com/orioncactus/pretendard) (release v1.3.9)
- **Description**: The app's UI font. Five static weights are embedded in the executable without modification. The full license text ships next to the executable as `Fonts/Pretendard-LICENSE.txt` and is kept in this repository at [`SoundVisualizer/Fonts/Pretendard-LICENSE.txt`](SoundVisualizer/Fonts/Pretendard-LICENSE.txt). Characters that Pretendard does not cover (Chinese characters, Thai, Devanagari and Arabic) are drawn with the Windows system fonts.

## Sounds

### Tutorial sounds
- **Files**: `SoundVisualizer/Tutorial/Sounds/tutorial_birds.wav`, `tutorial_chirp.wav`, `tutorial_talk.wav`, `tutorial_honk.wav`
- **License**: CC0 1.0 Universal (public domain dedication), or works of the U.S. government in the public domain. No attribution is required, but the sources are listed below.
- **Description**: Real recordings that the tutorial plays along with its pictures when its sound is turned on. They are the clips of the Android app's tutorial (`app/src/main/res/raw/tutorial_*.ogg`), which were cut short from the originals below, had their low end removed, their ends faded and their levels matched, and were saved as mono Ogg Vorbis. The PC copies are those Ogg files decoded once to PCM WAV (mono, 16-bit, the same 44.1 kHz rate) without other changes, so that the app needs no Ogg decoder.
- **Sources**:
  - `tutorial_birds.wav` (28.0–30.2 s) and `tutorial_chirp.wav` (3.48–3.93 s): "Forêt" by Joseph SARDIN (BigSoundBank #0100), https://commons.wikimedia.org/wiki/File:For%C3%AAt_(Joseph_SARDIN).wav, CC0 1.0 Universal (dual-licensed with the WTFPL), https://creativecommons.org/publicdomain/zero/1.0/
  - `tutorial_talk.wav` (0.70–2.90 s): "Anna Pete, hi! Hi, we are here!", VOA Learning English "Let's Learn English" Lesson 4 (Voice of America), https://commons.wikimedia.org/wiki/File:Anna_Pete,_hi!_Hi,_we_are_here!.ogg, a work of the U.S. government (VOA) in the public domain
  - `tutorial_honk.wav` (0.42–0.92 s, the first honk): "Car Horn" by 15HPanska_Ruttner_Jan (Freesound #461679), https://commons.wikimedia.org/wiki/File:Car_Horn.wav, CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
