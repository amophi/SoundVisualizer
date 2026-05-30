<div align="center">

[🇺🇸 English](README.md) | [🇰🇷 한국어](README.ko.md)

<br/>

# 🎵 Sound Visualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)

**A High-Performance Audio Visualization & AI Sound Analysis Overlay Engine**

</div>

<br/>

> **Sound Visualizer** captures real-time system audio and translates it into graphical overlays. Built with WPF, it is highly optimized for gaming and features an AI sound classifier using YAMNet and ONNX Runtime for intelligent audio event detection (such as gunshots, speech, and ambient sounds).

---

## 🌟 Social Impact & Benefits

This project goes beyond simple aesthetics. It serves as a bridge between the auditory and visual worlds, offering significant benefits across different user groups.

### 🦻 For the Deaf and Hard of Hearing
- **Visualizing the Unseen**: Translates critical auditory cues (like footsteps, gunshots in a game, or system alert sounds) into instant visual feedback.
- **Leveling the Playing Field in Gaming**: Allows deaf gamers to react to audio-based gameplay mechanics on equal footing by perceiving spatial and event-based sound visually.

### 🎧 For the General Public & Gamers
- **Gameplay & Media Overlay**: Provides real-time visual overlays for audio output during gaming or media consumption.
- **Tactical Advantage in Gaming**: Visually pinpoints the frequency and intensity of sounds, providing extra awareness in competitive environments.
- **Desktop Customization**: Provides a customizable overlay for streams or desktop setups with minimal performance overhead.

---

## ✨ Key Features

### 🎨 Diverse Visualizer Modes (Upgraded)
- 🌊 **Wave Mode (`WaveVisualizer`)**: Renders audio waveforms as curves that dynamically scale based on intensity.
- ⭕ **Circle Ripple Mode (`CircleRippleVisualizer`)**: Creates a circular equalizer and pulse effects based on center Core Radius and frequency.
- 🎛 **Pad Mode (`PadVisualizer`)**: Displays frequency spectrum responses across spatial grid pad directions (2.0 / 5.1 / 7.1).
- 🔲 **Outline Mode (`OutlineVisualizer`)**: Emphasizes border lighting waves running around the frame edges.

### 🎮 Real-Time Overlay Editor (F4 Key)
- **Interactive Drag & Resize**: Activate editor mode by pressing **F4**. You can directly drag the boundaries of guidelines on the screen to resize limits of graphics in real-time.
- **On-Screen Control Panel**: Tweak colors, sensitivity, speeds, glows, and AI speech labels dynamically via the overlay control panel.

### ⚡ Seamless Hotkey Control
- Backstage hotkeys (**F2** for Sound Mode, **F3** for Visual Mode, **F4** for Overlay Editor) allow you to toggle modes instantly without minimizing your active fullscreen games.

### 🔊 Advanced Multi-Channel Audio Support
- **Hardware-Aware Design**: Automatically detects and adjusts configurations for **2.0 Stereo**, **5.1 Surround**, and **7.1 Surround** channels.
- **Virtual 7.1 Surround Support**: Detailed guides and links for setting up virtual audio tools (like **VB-CABLE**) to experience immersive 7.1 surround sound overlays even on stereo-only setups.

### 🤖 AI Sound Classification (ONNX & YAMNet)
- **Real-Time Classification**: Embedded `SoundClassifier` model detects and labels specific audio events (Ambient, Speech, and Danger/Gunshots) natively.
- **Distinctive Visual Cues**: Assign custom UI colors to each category for instant intuitive recognition.

### 🌐 Global Multi-Language Support (8 Languages)
- Fully localized with complete support for **Korean, English, Japanese, Chinese, Spanish, French, German, and Russian**.

---

## 🛠️ Tech Stack

<details>
<summary><b>Click to expand</b></summary>

- **Framework / UI**: C#, WPF (.NET 9.0/10.0)
- **Audio Capture & DSP**: WASAPI Loopback Capture (via NAudio), Real-time Fast Fourier Transform (FFT) signal processing
- **AI & Machine Learning**: Python (training scripts), ONNX Runtime, YAMNet (transfer-learned)
- **Aesthetics & Performance**: High-efficiency double-buffered rendering pipelines minimizing GC allocations (GC-Free implementations).
</details>

---

## 📁 Directory Structure

```text
├── SoundVisualizer/      # Main WPF Application
│   ├── AIModel/          # ONNX models (YAMNet, boosters) and C# SoundClassifier
│   ├── CoreAudio/        # System audio capture pipeline (AudioCaptureEngine)
│   ├── DSP/              # Digital Signal Processing (FFT, VectorCalculator)
│   ├── Visualizers/      # Visualizer implementations (Wave, Pad, CircleRipple, Outline)
│   ├── AppSettings.cs    # Global application and visualizer configuration
│   ├── ColorPickerWindow.xaml # Custom color picker for visualizers
│   ├── LauncherWindow.xaml # Settings launcher and localization management
│   └── MainWindow.xaml     # The actual transparent overlay window and real-time editor
└── tools/
    └── transfer_learning/  # Python scripts for training custom ONNX models
```

## 🚀 Installation & Running

### 💻 For General Users (Quick Start via Releases)
No installation required! Sound Visualizer is distributed as a portable standalone package.

1. Go to the **[GitHub Releases](https://github.com/amophi/SoundVisualizer/releases)** page.
2. Download the latest `SoundVisualizer.zip` release.
3. Extract the downloaded `.zip` archive to any folder on your PC.
4. Double-click **`SoundVisualizer.exe`** to open the Launcher Settings.
5. Choose your preferred language, customize the configurations, and click **Start** to launch the overlay!

---

### 🛠️ For Developers (Build from Source)
If you wish to modify, contribute, or build the application from source:

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
3. Build the solution in Release or Debug mode (`Ctrl + Shift + B`).
4. Press `F5` to execute the launcher.

---

## 🤝 Contributing
Contributions, bug reports, and feature requests are welcome! When contributing code, please adhere to the existing object-oriented structure (e.g., `IVisualizerMode`) and maintain the zero-allocation rendering principles.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.

---
<div align="center">
  <sub>Built with ❤️ for accessible and immersive digital experiences.</sub>
</div>
