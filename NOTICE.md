# Third-Party Licenses and Notices

This project (`SoundVisualizer`) uses the following third-party open-source libraries, frameworks, and models. We are grateful to the open-source community for their contributions.

## Libraries & Frameworks

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

## AI Models

### YAMNet (Yet Another Mobile Network)
- **License**: Apache License 2.0
- **Source**: [https://github.com/tensorflow/models/tree/master/research/audioset/yamnet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet)
- **Description**: A pre-trained deep neural network that can predict audio events from the AudioSet-YouTube corpus. It is used as the base model for transfer learning in our sound classification pipeline.
