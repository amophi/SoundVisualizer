<div align="center">

# 🎵 SoundVisualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

**A High-Performance Audio Visualization & AI Sound Analysis Overlay Engine**

</div>

<br/>

> **SoundVisualizer** captures real-time system audio and translates it into beautiful, dynamic graphical overlays. Built with WPF, it is highly optimized for gaming and features an AI sound classifier for intelligent audio event detection.

---

## 🌟 Social Impact & Benefits

This project goes beyond simple aesthetics. It serves as a bridge between the auditory and visual worlds, offering significant benefits across different user groups.

### 🦻 For the Deaf and Hard of Hearing
- **Visualizing the Unseen**: Translates critical auditory cues (like footsteps, gunshots in a game, or system alert sounds) into instant visual feedback.
- **Leveling the Playing Field in Gaming**: Allows deaf gamers to react to audio-based gameplay mechanics on equal footing by perceiving spatial and event-based sound visually.
- **Environmental Awareness**: AI-driven sound classification can actively alert users to specific real-world or digital audio events, providing an extra layer of accessibility.

### 🎧 For the General Public & Gamers
- **Immersive Gameplay & Media**: Elevates the sensory experience of listening to music or playing games through fluid, reactive visual overlays.
- **Tactical Advantage in Gaming**: Visually pinpoints the frequency and intensity of sounds, providing extra awareness and faster reaction times in competitive environments.
- **Desktop Customization**: Adds a sleek, high-tech aesthetic to any stream or dual-monitor setup without hindering PC performance.

---

## ✨ Key Features

### 🎨 Diverse Visualizer Modes
- 🌊 **Wave Mode**: Renders audio waveforms as smooth, rounded-rectangle curves that dynamically scale their internal paths based on intensity.
- 📡 **Pulse Mode**: Creates an outward-expanding ripple effect originating from a central transparent ring, reacting beautifully to audio frequencies.
- 📊 **FR (Frequency Response) Mode**: Displays the frequency spectrum across different bands for analytical and aesthetic visualization.

### 🎮 High-Performance Gaming Overlay
- **Click-Through & Transparent**: The overlay seamlessly sits on top of full-screen games or applications without blocking mouse inputs.
- **Zero Stuttering**: Optimized rendering via **WPF Native Composition**. Memory allocations per frame have been eliminated to drastically reduce Garbage Collection (GC) overhead.

### 🤖 AI Sound Classification
- **Deep Learning Integration**: Embedded `SoundClassifier` model detects and categorizes specific audio events (e.g., gunshots).
- **Inference Pipeline**: Includes an offline file inference test runner (`FileInferenceTestRunner`) for high-accuracy event validation.

### ⚙️ Developer UI & Launcher
- Features a modern `LauncherWindow` to manage visualization settings (color, sensitivity, mode toggles) and access developer debug tools effortlessly.

---

## 🛠️ Tech Stack

<details>
<summary><b>Click to expand</b></summary>

- **Framework / UI**: C#, WPF (.NET)
- **Audio Capture & DSP**: CoreAudio API, Real-time Fast Fourier Transform (FFT) signal processing
- **AI & Machine Learning**: ML/AI Model Inference
- **Architecture**: Decoupled rendering and audio-capture threads, object-oriented visualizer modules (`IVisualizerMode`)
</details>

---

## 📁 Directory Structure

```text
SoundVisualizer/
├── AIModel/          # AI classification models and inference runners
├── CoreAudio/        # System audio capture and data pipelines
├── DSP/              # Digital Signal Processing (FFT, filtering)
├── UI/               # Custom User Interface components
├── Visualizers/      # Visualizer implementations (Wave, Pulse, FR)
├── AppSettings.cs    # Global application and visualizer configuration
├── LauncherWindow.xaml # Settings launcher interface
└── MainWindow.xaml     # The actual transparent overlay window
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11
- Visual Studio 2022
- .NET SDK (Compatible Version)

### Build & Run
1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/SoundVisualizer.git
   ```
2. Open the `SoundVisualizer.slnx` solution file in Visual Studio.
3. Build the solution (`Ctrl + Shift + B`).
4. Press `F5` to run. The `LauncherWindow` will appear.
5. Select your preferred visualizer mode, adjust the settings, and click **Start** to launch the overlay.

---

## 🤝 Contributing
Contributions, bug reports, and feature requests are welcome! When contributing code, please adhere to the existing object-oriented structure (e.g., `IVisualizerMode`) and maintain the zero-allocation rendering principles.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.

---
<div align="center">
  <sub>Built with ❤️ for accessible and immersive digital experiences.</sub>
</div>
