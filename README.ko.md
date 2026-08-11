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

**실시간 오디오 시각화 및 AI 기반 사운드 분석 오버레이 엔진**

</div>

<br/>

> **Sound Visualizer**는 컴퓨터의 시스템 오디오를 실시간으로 캡처하여 직관적인 그래픽 오버레이로 변환하는 데스크톱 애플리케이션입니다. 
단순한 파형(Waveform) 시각화를 넘어, **YAMNet**과 **ONNX Runtime** 기반의 AI 사운드 분류기를 내장하여 현재 재생 중인 소리의 종류(배경음, 대화음, 위협음 등)를 실시간으로 인지하고 피드백을 제공합니다.

---

## 📖 목차
- [프로젝트 배경 및 목적](#-프로젝트-배경-및-목적)
- [주요 기능](#-주요-기능)
- [시스템 및 AI 아키텍처](#-시스템-및-ai-아키텍처)
- [설치 및 실행 방법](#-설치-및-실행-방법)
- [빌드 가이드 (개발자용)](#-빌드-가이드-개발자용)
- [팀원 및 역할](#-팀원-및-역할)
- [라이선스](#-라이선스)

---

## 🎯 프로젝트 배경 및 목적

### 청각장애인의 미디어 접근성 향상
유튜브, OTT, 게임 등 현대 미디어 콘텐츠의 입체적인 공간 음향 기술은 놀라운 몰입감을 제공하지만, 청각장애인에게는 오히려 심각한 진입 장벽이 됩니다. 기존의 자막 시스템은 단순 대사 전달에 그쳐, **소리의 발생 방향이나 발소리, 총소리 등의 위급한 효과음 정보**를 제공하지 못합니다. 
본 프로젝트는 **보이지 않는 소리의 세기, 종류, 방향을 시각화**하여 이러한 정보의 불균형을 해소하고 모두가 평등하게 미디어를 즐길 수 있는 환경을 제공하는 것을 최우선 목표로 기획되었습니다.

### 모두를 위한 활용성 확장
이러한 시각화 오버레이 기술은 비장애인 게이머들에게도 치열한 경쟁 게임 환경에서 추가적인 **전술적 시각 지표(상황 인지력)**를 제공하며, 공공장소나 무음 환경 등 청각적 제약이 있는 상황에서도 완벽한 대안으로 활용될 수 있습니다.

---

## ✨ 주요 기능

### 1. 다양한 공간 및 강도 시각화 모드
소리의 크기와 방향(2.0, 5.1, 7.1 채널)을 직관적으로 표현하는 4가지 고유 렌더링 모드를 제공합니다.
- **파도 모드 (Wave)**: 화면의 상/하/좌/우 모서리를 따라 소리의 강도에 비례해 요동치는 파동을 렌더링합니다.
- **원형 모드 (Circle)**: 화면 중앙을 기점으로 바깥쪽으로 퍼져나가는 원형 파동을 렌더링합니다.
- **패드 모드 (Pad)**: 지정된 공간 그리드(방향)에 고정된 발광 패드 형태로 소리를 표현합니다.
- **외곽선 모드 (Outline)**: 얇고 세련된 외곽선 테두리만 발광하여 화면 가림을 최소화합니다.

### 2. 온스크린 실시간 제어 및 에디터 (F4)
전체 화면 게임을 내리지 않고도 오버레이 상태에서 즉각적인 수정이 가능합니다.
- **F2 / F3 단축키**: 사운드 모드 및 시각화 모드 실시간 변경.
- **에디터 모드 (F4)**: 화면에 나타나는 가이드라인 경계를 직접 드래그하여 시각화 영역의 크기와 한계를 자유롭게 리사이징합니다. 색상, 투명도, AI 감지 민감도 또한 패널에서 조정 가능합니다.

### 3. 하드웨어 인식 7.1 다중 채널 지원
- 시스템의 오디오 채널 구성(Stereo, 5.1, 7.1 Surround)을 자동 인식하고, 소리의 발생 방향(전/후/좌/우)을 정확하게 매핑하여 입체적인 시각 효과를 연출합니다.

### 4. AI 실시간 사운드 분류 (ONNX & YAMNet)
- **3-Class 분류 체계**: 모든 소리를 `Ambient(환경음)`, `Speech(대화음)`, `Danger(위협음)` 3가지로 분석하여 카테고리별로 지정된 UI 색상과 투명도로 동적 시각 피드백을 제공합니다.
- **Gunshot Booster**: 게임 및 영상 내의 총소리를 놓치지 않도록 특수 설계된 부스터 모델이 가동되어 위협음 탐지 민감도를 극대화합니다.

### 5. 글로벌 다국어 지원
- 한국어, 영어, 일본어, 중국어, 스페인어, 프랑스어, 독일어, 러시아어 등 8개 국어를 완벽 지원합니다.

---

## ⚙️ 시스템 및 AI 아키텍처

본 프로젝트는 고성능 실시간 처리를 위해 철저한 메모리 관리 및 백그라운드 최적화로 설계되었습니다.

* **Core Audio API (WASAPI)**: Windows 시스템 전체 오디오를 딜레이 없이 루프백 캡처.
* **DSP & FFT 연산**: 백그라운드 오디오 스레드에서 초당 수십 번의 고속 주파수 변환 연산 수행.
* **Zero-Allocation 렌더링**: C# / WPF 환경에서 GC(가비지 컬렉터)의 개입을 최소화하여 프레임 드랍 방지.
* **ONNX 추론 파이프라인**: 16kHz 모노 변환 및 Log-mel 스펙트로그램 전처리를 거친 후, `Microsoft.ML.OnnxRuntime` 엔진으로 전이 학습된 YAMNet 가중치 기반 실시간 분류 수행.

---

## 🚀 설치 및 실행 방법

Sound Visualizer는 설치가 필요 없는 가벼운 **포터블(Portable)** 패키지로 제공됩니다.

1. **[GitHub Releases](https://github.com/amophi/SoundVisualizer/releases)** 페이지에 접속합니다.
2. 최신 버전의 `SoundVisualizer.zip` 파일을 다운로드 및 압축 해제합니다.
3. 폴더 내의 **`SoundVisualizer.exe`**를 실행합니다.
4. 런처에서 언어 및 초기 설정을 마친 후, **Start**를 클릭하여 오버레이를 활성화합니다.

---

## 🛠 빌드 가이드 (개발자용)

코드 수정 및 기여를 위해 소스에서 직접 빌드하는 방법입니다.

1. **요구 사항**: Windows 10/11, Visual Studio 2022 (.NET 데스크톱 개발 워크로드), .NET 10.0 SDK
2. **리포지토리 클론**:
   ```bash
   git clone https://github.com/amophi/SoundVisualizer.git
   ```
3. Visual Studio에서 `SoundVisualizer.slnx` 솔루션을 엽니다.
4. `Release` 또는 `Debug` 구성 선택 후 솔루션을 빌드(`Ctrl + Shift + B`)하고 실행(`F5`)합니다.

---

## 📄 라이선스

이 프로젝트는 오픈소스 생태계 선순환을 위해 **AGPL v3** 라이선스 하에 배포됩니다. 누구나 코드를 수정하고 새로운 기능(커스텀 UI 테마, 플러그인 등)을 개발하여 공유할 수 있습니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 확인하세요.

사용된 서드파티 라이브러리(NAudio, ONNX Runtime 등)와 오픈소스 AI 모델에 대한 저작권 및 라이선스 정보는 [THIRD_PARTY.md](THIRD_PARTY.md)에서 확인하실 수 있습니다.

<br/>
<div align="center">
  <b>Built for Accessibility.</b>
</div>
