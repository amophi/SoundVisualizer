<div align="center">

[English](README.md) | [한국어](README.ko.md)

<br/>

# 🎵 Sound Visualizer

<img width="1024" height="818" alt="SoundVisualizer" src="https://github.com/user-attachments/assets/b11aa5b3-c995-4e36-8ff6-3e2f2c2b2388" />

[![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![ONNX](https://img.shields.io/badge/ONNX-005CED?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)

**오디오 시각화 및 AI 사운드 분석 오버레이 엔진**

</div>

<br/>

> **Sound Visualizer**는 실시간으로 시스템 오디오를 캡처하여 그래픽 오버레이로 변환합니다. WPF로 구동되며, YAMNet과 ONNX Runtime을 기반으로 한 AI 사운드 분류기를 탑재해 총소리, 음성, 배경음 등의 오디오 이벤트를 감지합니다.

---

## 🌟 사용 사례 및 활용

이 프로젝트는 청각 신호와 시각적 반응을 연결하여, 사용자 층별로 다음과 같은 기능을 제공합니다.

### 🦻 청각 장애인 및 난청 사용자를 위해
- **소리 시각화**: 파악하기 힘든 게임 내의 발소리, 총소리 또는 시스템 알림음과 같은 청각적 신호를 시각적 피드백으로 나타냅니다.
- **게임 접근성 제공**: 오디오 기반의 게임 플레이 요소(공간감 및 이벤트)를 시각적으로 인지할 수 있도록 돕습니다.

### 🎧 일반 사용자 및 게이머를 위해
- **게임 및 미디어 오버레이**: 음악 감상이나 게임 플레이 중 오디오 출력에 대한 시각적 피드백을 오버레이로 제공합니다.
- **게임 내 전술적 우위**: 소리의 주파수와 강도를 시각적으로 파악하여, 경쟁 게임 환경에서 추가적인 상황 인지력을 제공합니다.
- **데스크탑 커스터마이징**: PC 성능 저하를 최소화하면서 스트리밍 또는 듀얼 모니터 환경에 커스텀 오버레이를 제공합니다.

---

## ✨ 주요 기능

### 🎨 다양한 시각화 모드 (업그레이드됨)
- 🌊 **파형 모드 (`WaveVisualizer`)**: 소리의 강도에 따라 스케일이 조정되는 오디오 파형을 렌더링합니다.
- ⭕ **원형 파동 모드 (`CircleRippleVisualizer`)**: 중심 반경(Core Radius)과 주파수를 기반으로 원형 이퀄라이저와 리플(파동) 효과를 생성합니다.
- 🎛 **패드 모드 (`PadVisualizer`)**: 2.0 / 5.1 / 7.1 공간 그리드 방향에 따른 주파수 스펙트럼 반응을 표시합니다.
- 🔲 **테두리 모드 (`OutlineVisualizer`)**: 화면 외곽을 따라 테두리 조명(Lighting wave) 효과를 렌더링합니다.

### 🎮 실시간 오버레이 에디터 (F4 키)
- **드래그 앤 리사이즈**: **F4** 키를 눌러 에디터 모드를 활성화할 수 있습니다. 화면의 가이드라인 경계를 직접 드래그하여 그래픽의 렌더링 한계를 실시간으로 조정합니다.
- **온스크린 제어 패널**: 오버레이 제어 패널을 통해 색상, 민감도, 속도, 발광(Glow) 효과 및 AI 음성 감지 라벨 등을 동적으로 변경할 수 있습니다.

### ⚡ 매끄러운 단축키 제어
- 백스테이지 환경에서 동작하는 단축키(**F2**: 사운드 모드 변경, **F3**: 시각화 모드 변경, **F4**: 오버레이 에디터 토글)를 지원하여, 전체 화면 게임을 최소화하지 않고도 모드를 즉시 전환할 수 있습니다.

### 🔊 고급 다중 채널 오디오 지원
- **하드웨어 인식 설계**: **2.0 스테레오**, **5.1 서라운드**, **7.1 서라운드** 채널을 자동으로 감지하고 구성합니다.
- **가상 7.1 서라운드 지원**: **VB-CABLE**과 같은 가상 오디오 툴 설정을 통해 스테레오 환경에서도 7.1 서라운드의 입체감 있는 시각화 오버레이를 경험할 수 있습니다.

### 🤖 AI 사운드 분류 (ONNX & YAMNet)
- **실시간 분류 기능**: 소프트웨어에 내장된 `SoundClassifier` 모델이 배경음(Ambient), 음성(Speech), 총소리(Gunshots) 등의 오디오 이벤트를 감지하고 라벨링합니다.
- **시각 피드백**: 각 감지 카테고리마다 사용자 지정 UI 색상을 할당하여 인지할 수 있습니다.

### 🌐 다국어 지원 (8개 국어)
- **한국어**, 영어, 일본어, 중국어, 스페인어, 프랑스어, 독일어, 러시아어를 완벽하게 지원합니다.

---

## 🛠️ 기술 스택

<details>
<summary><b>클릭하여 펼치기</b></summary>

- **프레임워크 / UI**: C#, WPF (.NET 9.0/10.0)
- **오디오 캡처 & DSP (신호 처리)**: WASAPI 루프백 캡처 (NAudio 활용), 실시간 고속 푸리에 변환 (FFT)
- **AI & 머신러닝**: Python (모델 학습 스크립트), ONNX Runtime, YAMNet (전이 학습 모델)
- **그래픽 및 성능**: 최적화 렌더링(Zero-allocation)을 지향하며 GC(가비지 컬렉터) 할당을 최소화한 고효율 이중 버퍼링 구조 설계.
</details>

---

## 📁 디렉토리 구조

```text
├── SoundVisualizer/      # 메인 WPF 애플리케이션
│   ├── AIModel/          # ONNX 모델 (YAMNet, 부스터) 및 C# SoundClassifier
│   ├── CoreAudio/        # 시스템 오디오 캡처 파이프라인 (AudioCaptureEngine)
│   ├── DSP/              # 디지털 신호 처리 (FFT, VectorCalculator)
│   ├── Visualizers/      # 오버레이 시각화 클래스 구현부 (Wave, Pad, CircleRipple, Outline)
│   ├── AppSettings.cs    # 앱 및 시각화 관련 글로벌 런타임 설정
│   ├── ColorPickerWindow.xaml  # UI 렌더링 커스텀 컬러 지정 창
│   ├── LauncherWindow.xaml     # 초기 환경(언어, 모드) 세팅 런처 화면
│   └── MainWindow.xaml         # 실제 투명 오버레이 윈도우 및 실시간 렌더링 에디터 영역
└── tools/
    └── transfer_learning/  # 자체 커스텀 ONNX 모델 생성 밑 전이 학습을 위한 스크립트 구역
```

## 🚀 설치 및 실행

### 💻 일반 사용자용 (Releases 탭을 이용한 빠른 시작)
설치가 필요 없습니다! Sound Visualizer는 가벼운 포터블(무설치) 패키지로 오픈소스로 제공됩니다.

1. **[GitHub Releases](https://github.com/amophi/SoundVisualizer/releases)** 페이지로 이동합니다.
2. 게시된 최신 버전의 `SoundVisualizer.zip` 파일을 얻어 다운로드합니다.
3. PC의 원하는 폴더에 해당 `.zip` 파일의 압축을 풉니다.
4. **`SoundVisualizer.exe`**를 더블 클릭하여 런처 화면을 띄웁니다.
5. 편한 현지화 언어를 고르고 설정을 마친 뒤, **Start** 버튼을 클릭하여 오버레이를 구동시켜 보세요.

---

### 🛠️ 개발자용 (소스 코드 직접 빌드)
코드를 수정하거나 패치를 기여하기 위해 소스 코드 단에서 직접 빌드하려면 다음 단계를 따르세요.

#### 필수 요구 사항
- Windows 10 / 11
- Visual Studio 2022 (.NET 데스크톱 개발 워크로드 포함)
- .NET 9.0 / 10.0 SDK

#### 빌드 방법
1. 깃 리포지토리 클론:
   ```bash
   git clone https://github.com/amophi/SoundVisualizer.git
   ```
2. Visual Studio 2022에서 `SoundVisualizer.slnx` 솔루션 파일을 엽니다.
3. 환경에 맞추어 Release 또는 Debug 모드로 지정한 뒤 솔루션을 빌드합니다 (`Ctrl + Shift + B`).
4. `F5`를 눌러 환경 윈도우 런처를 실행합니다.

---

## 🤝 프로젝트 기여하기 (Contributing)
코드 기여, 버그 리포트, 새로운 디자인 기능 제안은 모두 함께 성장할 양분이며 환영합니다! 코드를 기여해주실 때는 기존의 객체 지향적 설계 구조(예: `IVisualizerMode`)를 지켜 주시고, 특히 렌더링 도중 발생하는 메모리 오버헤드(Zero-allocation)를 최소화하는 퍼포먼스 가이드 원칙을 지켜주세요.

## 📝 소프트웨어 라이선스 정책
본 프로젝트는 MIT 라이선스에 따라 자유롭게 배포되고 수정될수 있습니다 - 자세한 내용은 코어의 LICENSE 파일을 참조해 주세요.

---
<div align="center">
  <sub>접근성을 위해 제작되었습니다.</sub>
</div>