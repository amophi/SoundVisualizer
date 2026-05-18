<div align="center">

# 🎵 SoundVisualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)

**A High-Performance Audio Visualization & AI Sound Analysis Overlay Engine**

</div>

<br/>

> **SoundVisualizer** captures real-time system audio and translates it into beautiful, dynamic graphical overlays. Built with WPF, it is highly optimized for gaming and features an AI sound classifier using YAMNet and ONNX Runtime for intelligent audio event detection (like in-game gunshots).

---

## 🌟 Social Impact & Benefits

This project goes beyond simple aesthetics. It serves as a bridge between the auditory and visual worlds, offering significant benefits across different user groups.

### 🦻 For the Deaf and Hard of Hearing
- **Visualizing the Unseen**: Translates critical auditory cues (like footsteps, gunshots in a game, or system alert sounds) into instant visual feedback.
- **Leveling the Playing Field in Gaming**: Allows deaf gamers to react to audio-based gameplay mechanics on equal footing by perceiving spatial and event-based sound visually.

### 🎧 For the General Public & Gamers
- **Immersive Gameplay & Media**: Elevates the sensory experience of listening to music or playing games through fluid, reactive visual overlays.
- **Tactical Advantage in Gaming**: Visually pinpoints the frequency and intensity of sounds, providing extra awareness and faster reaction times in competitive environments.
- **Desktop Customization**: Adds a sleek, high-tech aesthetic to any stream or dual-monitor setup without hindering PC performance.

---

## ✨ Key Features

### 🎨 Diverse Visualizer Modes
- 🌊 **Wave Mode (`WaveVisualizer`)**: Renders audio waveforms as smooth curves that dynamically scale their internal paths based on intensity.
- ⭕ **Circle Ripple Mode (`CircleRippleVisualizer`)**: Creates an outward-expanding ripple and ring effect to emphasize audio impact and beats.
- 🎛 **Pad Mode (`PadVisualizer`)**: Displays the frequency spectrum and intensity responses across different grid pad layouts.

### 🎮 High-Performance Gaming Overlay
- **Click-Through & Transparent**: The overlay seamlessly sits on top of full-screen games or applications without blocking mouse inputs.
- **Zero Stuttering**: Optimized rendering via **WPF Native Composition**. Memory allocations per frame have been minimized to drastically reduce Garbage Collection (GC) overhead.

### 🤖 AI Sound Classification (ONNX & YAMNet)
- **Deep Learning Integration**: Embedded `SoundClassifier` model detects and categorizes specific audio events natively via ONNX Runtime.
- **Transfer Learning Customization**: Includes custom Python scripts (`tools/transfer_learning`) to train a custom model (`three_class_score_head`, `gunshot_booster`) on top of YAMNet to dramatically improve specific sound classification accuracy.

### ⚙️ Developer UI & Launcher
- Features a modern `LauncherWindow` to seamlessly manage visualization settings (color, sensitivity, mode toggles) and access tools effortlessly.

---

## 🛠️ Tech Stack

<details>
<summary><b>Click to expand</b></summary>

- **Framework / UI**: C#, WPF (.NET 9.0/10.0)
- **Audio Capture & DSP**: CoreAudio API, Real-time Fast Fourier Transform (FFT) signal processing
- **AI & Machine Learning**: Python (training scripts), ONNX Runtime, YAMNet
- **Architecture**: Decoupled rendering and audio-capture threads, object-oriented visualizer modules (`IVisualizerMode`)
</details>

---

## 📁 Directory Structure

```text
SoundVisualizer/
├── AIModel/          # ONNX models (YAMNet, boosters) and C# SoundClassifier
├── CoreAudio/        # System audio capture pipeline (AudioCaptureEngine)
├── DSP/              # Digital Signal Processing (FFT, Vector calculations)
├── Visualizers/      # Visualizer implementations (Wave, CircleRipple, Pad)
├── AppSettings.cs    # Global application and visualizer configuration
├── LauncherWindow.xaml # Settings launcher interface
└── MainWindow.xaml     # The actual transparent overlay window

tools/
└── transfer_learning/ # Python scripts for audio preprocessing and custom ONNX model training
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11
- Visual Studio 2022
- .NET 9.0/10.0 SDK

### Quick Start (Portable Release)
1. Go to the **Releases** tab on GitHub: [Latest Release](https://github.com/amophi/SoundVisualizer/releases).
2. Download the latest portable `.zip` file.
3. Extract the contents to a folder of your choice.
4. Double-click `SoundVisualizer.exe` to run. The `LauncherWindow` will appear.
5. Select your preferred visualizer mode, adjust the settings, and click **Start** to launch the overlay.

### Build from Source
1. Clone this repository:
   ```bash
   git clone https://github.com/amophi/SoundVisualizer.git
   ```
2. Open the `SoundVisualizer.slnx` solution file in Visual Studio.
3. Build the solution (`Ctrl + Shift + B`).
4. Press `F5` to run.

---

## 🤝 Contributing
Contributions, bug reports, and feature requests are welcome! When contributing code, please adhere to the existing object-oriented structure (e.g., `IVisualizerMode`) and maintain the zero-allocation rendering principles.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.

---
<div align="center">
  <sub>Built with ❤️ for accessible and immersive digital experiences.</sub>
</div>
