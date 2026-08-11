<div align="center">

[English](README.md) | [한국어](README.ko.md)

<br/>

# Sound Visualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)

**Real-Time Audio Visualization & AI Sound Analysis Overlay Engine**

</div>

<br/>

> **Sound Visualizer** is a desktop application that captures real-time system audio and translates it into an intuitive, transparent graphical overlay. 
Going beyond simple waveform rendering, it integrates an AI sound classifier powered by **YAMNet** and **ONNX Runtime** to instantly identify the type of sound playing (ambient, speech, danger) and provide adaptive visual feedback.

---

## 📖 Table of Contents
- [Background & Objectives](#-background--objectives)
- [Key Features](#-key-features)
- [System & AI Architecture](#-system--ai-architecture)
- [Installation & Quick Start](#-installation--quick-start)
- [Build Guide (For Developers)](#-build-guide-for-developers)
- [Team Members](#-team-members)
- [License](#-license)

---

## 🎯 Background & Objectives

### Enhancing Media Accessibility for the Deaf and Hard of Hearing
Modern media content, including YouTube, OTT, and gaming, relies heavily on spatial audio to maximize immersion. However, this creates a severe barrier for the deaf and hard of hearing. Traditional subtitle systems only deliver dialogue, completely omitting crucial acoustic information such as **sound direction, footsteps, or urgent sound effects (e.g., gunshots).**
Sound Visualizer was created to break down these barriers by **visualizing the intensity, classification, and spatial direction of invisible sounds.** Our primary goal is to resolve this information asymmetry and guarantee an equal, fully accessible media consumption environment for everyone.

### Expanding Utility for All Users
This overlay technology is also highly beneficial for gamers, providing a **tactical visual indicator (situational awareness)** in competitive environments. Additionally, it serves as a perfect alternative for users consuming media in public or silent environments where audio output is restricted.

---

## ✨ Key Features

### 1. Diverse Spatial and Intensity Visualizer Modes
We provide 4 unique rendering modes that intuitively map sound intensity and direction (supporting 2.0, 5.1, and 7.1 channels):
- **Wave Mode**: Renders dynamic audio waves along the screen edges that fluctuate based on intensity.
- **Circle Mode**: Radiates circular ripples outward from the center of the screen.
- **Pad Mode**: Displays glowing pads anchored to specific spatial grid directions.
- **Outline Mode**: A minimalist variation of Wave mode, glowing only the thin borders to minimize screen occlusion.

### 2. On-Screen Real-Time Editor (F4)
Modify settings instantly while in a full-screen application or game without minimizing the window.
- **F2 / F3 Hotkeys**: Switch between sound modes and visualization modes on the fly.
- **Editor Mode (F4)**: Drag the guideline boundaries on your screen to physically resize the rendering limits in real-time. Adjust colors, opacity, and AI detection sensitivities directly from the pop-up control panel.

### 3. Hardware-Aware Multi-Channel Support
- Automatically detects the system's audio configuration (Stereo, 5.1, 7.1 Surround) and accurately maps the sound's origin (Front/Back/Left/Right) to produce a 3D visual effect on a 2D screen.

### 4. Real-Time AI Sound Classification (ONNX & YAMNet)
- **3-Class Detection**: Analyzes all incoming audio into `Ambient`, `Speech`, and `Danger` categories. Each category triggers independent, customizable UI colors and dynamic opacity changes.
- **Gunshot Booster**: An auxiliary, highly-sensitive model designed specifically to ensure critical warning sounds (like gunshots in games/movies) are never missed.

### 5. Comprehensive Localization
- Fully supports 8 languages: English, Korean, Japanese, Chinese, Spanish, French, German, and Russian.

---

## ⚙️ System & AI Architecture

The project is rigorously engineered for high-performance, real-time background processing with minimal system overhead.

* **Core Audio API (WASAPI)**: Loopback captures system-wide audio with zero latency.
* **DSP & FFT Computation**: High-speed frequency transformation calculations run entirely on a background audio thread multiple times per second.
* **Zero-Allocation Rendering**: The WPF/C# rendering loop is designed to minimize Garbage Collector (GC) allocation, entirely preventing frame drops.
* **ONNX Inference Pipeline**: Audio is converted to 16kHz mono, processed into Log-mel spectrograms, and fed into a custom-trained YAMNet model via the `Microsoft.ML.OnnxRuntime` engine for instantaneous classification.

---

## 🚀 Installation & Quick Start

Sound Visualizer is provided as a lightweight, **portable (no-install)** open-source package.

1. Go to the **[GitHub Releases](https://github.com/amophi/SoundVisualizer/releases)** page.
2. Download and extract the latest `SoundVisualizer.zip` file to any location on your PC.
3. Run **`SoundVisualizer.exe`**.
4. Configure your initial settings and language in the launcher, then click **Start** to activate the overlay.

---

## 🛠 Build Guide (For Developers)

To build from source to modify the code or contribute to the project:

1. **Prerequisites**: Windows 10/11, Visual Studio 2022 (with .NET Desktop Development workload), and .NET 10.0 SDK.
2. **Clone the repository**:
   ```bash
   git clone https://github.com/amophi/SoundVisualizer.git
   ```
3. Open the `SoundVisualizer.slnx` solution file in Visual Studio 2022.
4. Select `Release` or `Debug` configuration, build (`Ctrl + Shift + B`), and press `F5` to run.

---

## 📄 License

Sound Visualizer is distributed under the **AGPL v3** license to encourage a virtuous cycle in the open-source community. Anyone is welcome to modify the code and share custom UI themes or streaming plugins. See the [LICENSE](LICENSE) file for more information.

For licensing and copyright information regarding third-party libraries (NAudio, ONNX Runtime, etc.) and AI models used in this project, please refer to the [THIRD_PARTY.md](THIRD_PARTY.md) file.

<br/>
<div align="center">
  <b>Built for Accessibility.</b>
</div>
