[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)

<div align="center">

[English](README.md) | [한국어](README.ko.md)

<br/>

# 🎵 Sound Visualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)

**An Audio Visualization & AI Sound Analysis Overlay Engine**

</div>

<br/>

> **Sound Visualizer** captures real-time system audio and translates it into graphical overlays. Built with WPF, it features an AI sound classifier using YAMNet and ONNX Runtime to detect audio events such as danger, speech, and ambient sounds.

---

## 🌟 Use Cases & Applications

This project connects auditory cues with visual responses, providing the following features for different user groups:

### 🦻 For the Deaf and Hard of Hearing
- **Visualizing Sound**: Translates hard-to-perceive auditory cues like footsteps, gunshots, or system alerts into visual feedback.
- **Gaming Accessibility**: Helps visually recognize audio-based gameplay elements (spatial awareness and events).

### 🎧 For Everyone
- **Gameplay & Media Overlay**: Provides real-time visual overlays for audio output during music listening or gaming.
- **Tactical Advantage in Gaming**: Visually pinpoints the frequency and intensity of sounds, providing extra situational awareness in competitive environments.
- **Customization**: Provides a custom overlay for streaming or dual-monitor setups with minimal performance overhead.

---

## ✨ Key Features

### 🎨 Diverse Visualizer Modes
- 🌊 **Wave Mode (`WaveVisualizer`)**: Renders audio waves that scale based on the intensity of the sound.
- ⭕ **Circle Mode (`CircleRippleVisualizer`)**: Renders audio waves spreading circularly outwards from the center of the screen.
- 🎛 **Pad Mode (`PadVisualizer`)**: Displays sound at fixed points based on 2.0 / 5.1 / 7.1 spatial grid directions.
- 🔲 **Outline Mode (`OutlineVisualizer`)**: Based on Wave Mode, but excludes filling towards the edges.

### 🎮 Real-Time Overlay Editor (Default F4 Key)
- **Drag & Resize**: Activate editor mode by pressing **F4**. You can directly drag the boundaries of guidelines on the screen to resize limits of graphics in real-time.
- **On-Screen Control Panel**: You can dynamically change all settings including colors, sensitivity, speeds, glow effects, and AI speech detection labels via the real-time overlay control panel.

### ⚡ Seamless Hotkey Control
- Supports user-defined hotkeys (**F2**: Change Sound Mode, **F3**: Change Visualizer Mode, **F4**: Toggle Overlay Editor), allowing you to switch modes instantly without minimizing full-screen games.

### 🔊 Advanced Multi-Channel Audio Support
- **Hardware-Aware Design**: Automatically detects and adjusts configurations for **2.0 Stereo**, **5.1 Surround**, and **7.1 Surround** channels. Users can also manually set the channels if needed.
- **Virtual 7.1 Surround Support**: Through virtual audio tool settings like **VB-CABLE**, you can experience immersive 7.1 surround visual overlays even on stereo environments.

### 🤖 AI Sound Classification (ONNX & YAMNet)
- **Real-Time Classification**: Embedded `SoundClassifier` model detects and labels audio events such as Ambient, Speech, and Danger.
- **Visual Cues**: Assign custom UI colors to each detection category for easy recognition.

### 🌐 Multi-Language Support (8 Languages)
- Supports **Korean**, English, Japanese, Chinese, Spanish, French, German, and Russian.

---

## 🛠️ Tech Stack

<details>
<summary><b>Click to expand</b></summary>

- **Framework / UI**: C#, WPF (.NET 9.0/10.0)
- **Audio Capture & DSP (Signal Processing)**: WASAPI Loopback Capture (via NAudio), Real-time Fast Fourier Transform (FFT)
- **AI & Machine Learning**: Python (model training scripts), ONNX Runtime, YAMNet (transfer learning model)
- **Graphics & Performance**: High-efficiency double-buffered rendering architecture designed to minimize GC (Garbage Collector) allocation (Zero-allocation) for optimized rendering.
</details>

---

## 📁 Directory Structure

```text
├── SoundVisualizer/      # Main WPF Application
│   ├── AIModel/          # ONNX models (YAMNet, boosters) and C# SoundClassifier
│   ├── CoreAudio/        # System audio capture pipeline (AudioCaptureEngine)
│   ├── DSP/              # Digital Signal Processing (FFT, VectorCalculator)
│   ├── Visualizers/      # Overlay visualizer class implementations (Wave, Pad, CircleRipple, Outline)
│   ├── AppSettings.cs    # Global runtime settings for app and visualizers
│   ├── ColorPickerWindow.xaml  # Custom color picker window for UI rendering
│   ├── LauncherWindow.xaml     # Initial setup launcher screen (Language, Mode)
│   └── MainWindow.xaml         # The actual transparent overlay window and real-time rendering editor area
└── tools/
    └── transfer_learning/  # Scripts for creating and transfer learning custom ONNX models
```

## 🚀 Installation & Running

### 💻 For General Users (Quick Start via Releases)
No installation required! Sound Visualizer is provided as a lightweight portable (no-install) open-source package.

1. Go to the **[GitHub Releases](https://github.com/amophi/SoundVisualizer/releases)** page.
2. Download the latest `SoundVisualizer.zip` file published.
3. Extract the downloaded `.zip` file to any folder on your PC.
4. Double-click **`SoundVisualizer.exe`** to open the launcher screen.
5. Choose your preferred localization language, finish settings, and click **Start** to run the overlay.

---

### 🛠️ For Developers (Build from Source)
Follow these steps to build from source to modify the code or contribute a patch.

#### Prerequisites
- Windows 10 / 11
- Visual Studio 2022 (with .NET Desktop Development workload)
- .NET 9.0 / 10.0 SDK

#### Build Steps
1. Clone the repository:
   ```bash
   git clone https://github.com/amophi/SoundVisualizer.git
   ```
2. Open the `SoundVisualizer.slnx` solution file in Visual Studio 2022.
3. Build the solution in Release or Debug mode according to your environment (`Ctrl + Shift + B`).
4. Press `F5` to execute the environment window launcher.

---

## 🤝 Contributing
Code contributions, bug reports, and new design feature proposals are all welcome as nourishment to grow together! When contributing code, please adhere to the existing object-oriented design structure (e.g., `IVisualizerMode`), and especially follow the performance guide principles to minimize memory overhead (Zero-allocation) occurring during rendering.

## 📝 Software License Policy
This project can be freely distributed and modified under the AGPL v3 License - see the core LICENSE file for details.

---
<div align="center">
  <sub>Built for accessibility.</sub>
</div>
