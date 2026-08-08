# Third-Party Licenses and Notices (SBOM)

This project (`SoundVisualizer`) uses the following third-party open-source libraries, frameworks, and models. We are grateful to the open-source community for their contributions.

| 번호 | 라이브러리명 | 버전 | 라이선스 | 공식 저장소 URL | 사용 목적 및 주요 기능 |
| --- | --- | --- | --- | --- | --- |
| 1 | NAudio | 2.3.0 | MIT License | github.com/naudio/NAudio | WASAPI 루프백 오디오 캡처 / 라이브러리로 불러 씀 |
| 2 | Microsoft.ML.OnnxRuntime | 1.24.3 | MIT License | github.com/microsoft/onnxruntime | 모델(ONNX) 추론 엔진 / 라이브러리로 불러 씀 |
| 3 | WPF (.NET) | 10.0 | MIT License | github.com/dotnet/wpf | 데스크톱 오버레이 UI 렌더링 / 프레임워크 사용 |
| 4 | tensorflow | >=2.12.0 | Apache-2.0 | github.com/tensorflow/tensorflow | AI 모델 전이 학습 / 라이브러리로 불러 씀 |
| 5 | tensorflow-hub | >=0.14.0 | Apache-2.0 | github.com/tensorflow/hub | AI 모델 다운로드 / 라이브러리로 불러 씀 |
| 6 | numpy | >=1.22.0 | BSD-3-Clause | github.com/numpy/numpy | 배열 및 수학 연산 / 라이브러리로 불러 씀 |
| 7 | librosa | >=0.10.1 | ISC License | github.com/librosa/librosa | 오디오 스펙트로그램 전처리 / 라이브러리로 불러 씀 |
| 8 | soundfile | >=0.12.1 | BSD-3-Clause | github.com/bastibe/python-soundfile | 오디오 파일 입출력 / 라이브러리로 불러 씀 |
| 9 | tf2onnx | >=1.14.0 | MIT License | github.com/onnx/tensorflow-onnx | TF 모델을 ONNX로 변환 / 실행 파일 호출 방식으로 사용 |
| 10 | onnx | >=1.12.0 | Apache-2.0 | github.com/onnx/onnx | ONNX 포맷 지원 / 라이브러리로 불러 씀 |
| 11 | onnxruntime (Python) | >=1.18.0 | MIT License | github.com/microsoft/onnxruntime | 학습 결과물 검증 / 라이브러리로 불러 씀 |

## AI Models

### YAMNet (Yet Another Mobile Network)
- **License**: Apache License 2.0
- **Source**: [https://github.com/tensorflow/models/tree/master/research/audioset/yamnet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet)
- **Description**: A pre-trained deep neural network that can predict audio events from the AudioSet-YouTube corpus. It is used as the base model for transfer learning in our sound classification pipeline.

### Custom Trained Models (`gunshot_booster.onnx`, `three_class_score_head.onnx`)
- **License**: Apache License 2.0
- **Description**: These models were generated via transfer learning based on the open-weights model YAMNet. In compliance with the base model's license and the open-source conference regulations, these newly generated weights are released under the Apache License 2.0 without any access restrictions.
