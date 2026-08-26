# Third-Party Licenses and Notices (SBOM)

This project (`SoundVisualizer`) uses the following third-party open-source libraries, frameworks, and models. We are grateful to the open-source community for their contributions.

| No. | Library Name | Version | License | Official Repository URL | Purpose and Main Features |
| --- | --- | --- | --- | --- | --- |
| 1 | NAudio | 2.3.0 | MIT License | github.com/naudio/NAudio | WASAPI loopback audio capture / Used as a library |
| 2 | Microsoft.ML.OnnxRuntime | 1.24.3 | MIT License | github.com/microsoft/onnxruntime | Model (ONNX) inference engine / Used as a library |
| 3 | WPF (.NET) | 10.0 | MIT License | github.com/dotnet/wpf | Desktop overlay UI rendering / Used as a framework |
| 4 | tensorflow | >=2.12.0 | Apache-2.0 | github.com/tensorflow/tensorflow | AI model transfer learning / Used as a library |
| 5 | tensorflow-hub | >=0.14.0 | Apache-2.0 | github.com/tensorflow/hub | AI model download / Used as a library |
| 6 | numpy | >=1.22.0 | BSD-3-Clause | github.com/numpy/numpy | Arrays and mathematical operations / Used as a library |
| 7 | librosa | >=0.10.1 | ISC License | github.com/librosa/librosa | Audio spectrogram preprocessing / Used as a library |
| 8 | soundfile | >=0.12.1 | BSD-3-Clause | github.com/bastibe/python-soundfile | Audio file I/O / Used as a library |
| 9 | tf2onnx | >=1.14.0 | Apache-2.0 | github.com/onnx/tensorflow-onnx | Convert TF models to ONNX / Executable call |
| 10 | onnx | >=1.12.0 | Apache-2.0 | github.com/onnx/onnx | ONNX format support / Used as a library |
| 11 | onnxruntime (Python) | >=1.18.0 | MIT License | github.com/microsoft/onnxruntime | Validate training results / Used as a library |

## AI Models

### YAMNet (Yet Another Mobile Network)
- **License**: Apache License 2.0
- **Source**: [https://github.com/tensorflow/models/tree/master/research/audioset/yamnet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet)
- **Description**: A pre-trained deep neural network that can predict audio events from the AudioSet-YouTube corpus. It is used as the base model for transfer learning in our sound classification pipeline.

### Custom Trained Models (`gunshot_booster.onnx`, `three_class_score_head.onnx`)
- **License**: Apache License 2.0
- **Description**: These models were generated via transfer learning based on the open-weights model YAMNet. In compliance with the base model's license and the open-source conference regulations, these newly generated weights are released under the Apache License 2.0 without any access restrictions.
