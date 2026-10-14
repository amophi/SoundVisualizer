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

## Fonts

### Pretendard
- **License**: SIL Open Font License, Version 1.1, with Reserved Font Name "Pretendard"
- **Copyright**: (c) 2021, Kil Hyung-jin
- **Source**: [https://github.com/orioncactus/pretendard](https://github.com/orioncactus/pretendard) (release v1.3.9)
- **Description**: The app's UI font. Five static weights are embedded in the executable without modification. The full license text ships next to the executable as `Fonts/Pretendard-LICENSE.txt` and is kept in this repository at [`SoundVisualizer/Fonts/Pretendard-LICENSE.txt`](SoundVisualizer/Fonts/Pretendard-LICENSE.txt). Characters that Pretendard does not cover (Chinese characters, Thai, Devanagari and Arabic) are drawn with the Windows system fonts.
