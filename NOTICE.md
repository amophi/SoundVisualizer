# Third-Party Licenses and Notices

This project (`SoundVisualizer`) uses the following third-party open-source libraries, frameworks, and models. We are grateful to the open-source community for their contributions.

## Libraries & Frameworks (C# / .NET)

### NAudio
- **License**: MIT License
- **Source**: [https://github.com/naudio/NAudio](https://github.com/naudio/NAudio)
- **Description**: Used for WASAPI Loopback Capture to capture system audio.

### ONNX Runtime
- **License**: MIT License
- **Source**: [https://github.com/microsoft/onnxruntime](https://github.com/microsoft/onnxruntime)
- **Description**: Used for running the ONNX models for sound classification.

### .NET / WPF (Windows Presentation Foundation)
- **License**: MIT License
- **Source**: [https://github.com/dotnet/wpf](https://github.com/dotnet/wpf)
- **Description**: The core framework used for building the Windows desktop application and overlay UI.

## Libraries & Frameworks (Python / Transfer Learning)

### TensorFlow / TensorFlow Hub
- **License**: Apache License 2.0
- **Source**: [https://github.com/tensorflow/tensorflow](https://github.com/tensorflow/tensorflow)

### NumPy
- **License**: BSD 3-Clause License
- **Source**: [https://github.com/numpy/numpy](https://github.com/numpy/numpy)

### Librosa
- **License**: ISC License
- **Source**: [https://github.com/librosa/librosa](https://github.com/librosa/librosa)

### SoundFile
- **License**: BSD 3-Clause License
- **Source**: [https://github.com/bastibe/python-soundfile](https://github.com/bastibe/python-soundfile)

### tf2onnx
- **License**: MIT License
- **Source**: [https://github.com/onnx/tensorflow-onnx](https://github.com/onnx/tensorflow-onnx)

### ONNX
- **License**: Apache License 2.0
- **Source**: [https://github.com/onnx/onnx](https://github.com/onnx/onnx)

## AI Models

### YAMNet (Yet Another Mobile Network)
- **License**: Apache License 2.0
- **Source**: [https://github.com/tensorflow/models/tree/master/research/audioset/yamnet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet)
- **Description**: A pre-trained deep neural network that can predict audio events from the AudioSet-YouTube corpus. It is used as the base model for transfer learning in our sound classification pipeline.

### Custom Trained Models (`gunshot_booster.onnx`, `three_class_score_head.onnx`)
- **License**: Apache License 2.0
- **Description**: These models were generated via transfer learning based on the open-weights model YAMNet. In compliance with the base model's license and the open-source conference regulations, these newly generated weights are released under the Apache License 2.0 without any access restrictions.
