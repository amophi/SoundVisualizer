namespace SoundVisualizer.AIModel
{
    /// <summary>
    /// YAMNet 추론 결과(파일 테스트·통합용).
    /// </summary>
    public readonly struct InferenceResult
    {
        public InferenceResult(
            int yamnetClassIndex,
            string yamnetDisplayName,
            float confidence,
            string coarseClass,
            bool meetsThreshold,
            double inferenceTimeMs)
        {
            YamnetClassIndex = yamnetClassIndex;
            YamnetDisplayName = yamnetDisplayName;
            Confidence = confidence;
            CoarseClass = coarseClass;
            MeetsThreshold = meetsThreshold;
            InferenceTimeMs = inferenceTimeMs;
        }

        public int YamnetClassIndex { get; }
        public string YamnetDisplayName { get; }
        /// <summary>0~1 사이 상위 클래스 확률(softmax 후).</summary>
        public float Confidence { get; }
        /// <summary>ambient | speech | danger</summary>
        public string CoarseClass { get; }
        public bool MeetsThreshold { get; }
        public double InferenceTimeMs { get; }
    }
}
