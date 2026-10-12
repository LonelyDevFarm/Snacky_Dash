using System.Collections;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace CheatSystem
{
    /// <summary>
    /// Đo hiệu năng trong vài giây theo bảng chỉ số mục tiêu (UNITY_WORK_RULES mục 9.1): thời gian khung hình CPU,
    /// rác bộ nhớ mỗi khung (GC Alloc), số lần vẽ (Batches), SetPass, thời gian dựng lại UI, số lần Instantiate, bộ nhớ.
    /// Bấm "Measure 5 s" ở tab System; kết quả ghi ra Console, file perf.txt (persistentDataPath) và dòng chữ trên bảng.
    /// Chỉ chạy trong Editor và Development Build (như cả bảng cheat).
    /// </summary>
    public class CheatPerf : MonoBehaviour
    {
        public static string LastReport { get; private set; } = "Perf: not measured yet";
        public static bool Running => _instance != null;

        private static CheatPerf _instance;

        /// <summary>Đo trong <paramref name="seconds"/> giây (bỏ qua nửa giây đầu cho bảng cheat đóng lại).</summary>
        public static void Measure(float seconds)
        {
            if (!CheatManager.Enabled || Running) return;
            var go = new GameObject("CheatPerf");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CheatPerf>();
            _instance.StartCoroutine(_instance.Run(seconds));
        }

        private IEnumerator Run(float seconds)
        {
            LastReport = "Perf: measuring…";
            yield return new WaitForSecondsRealtime(0.5f);
            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
            var ui = ProfilerRecorder.StartNew(new ProfilerCategory("Gui"), "Canvas.SendWillRenderCanvases", 1);
            var instantiate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Instantiate", 1);

            int frames = 0, slowFrames = 0, instantiates = 0;
            double cpuSum = 0, cpuMax = 0, uiSum = 0, uiMax = 0;
            long gcSum = 0, gcMax = 0, batchMax = 0, setPassMax = 0, memoryMax = 0;
            yield return null;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                yield return null;
                frames++;
                double cpu = mainThread.Valid ? mainThread.LastValue / 1e6 : Time.unscaledDeltaTime * 1000.0;
                cpuSum += cpu;
                if (cpu > cpuMax) cpuMax = cpu;
                if (cpu > 33.0) slowFrames++;
                if (gc.Valid) { gcSum += gc.LastValue; if (gc.LastValue > gcMax) gcMax = gc.LastValue; }
                if (batches.Valid && batches.LastValue > batchMax) batchMax = batches.LastValue;
                if (setPass.Valid && setPass.LastValue > setPassMax) setPassMax = setPass.LastValue;
                if (memory.Valid && memory.LastValue > memoryMax) memoryMax = memory.LastValue;
                if (ui.Valid) { double ms = ui.LastValue / 1e6; uiSum += ms; if (ms > uiMax) uiMax = ms; }
                if (instantiate.Valid && instantiate.Count > 0) instantiates += (int)instantiate.GetSample(instantiate.Count - 1).Count;
            }

            var report = new StringBuilder();
            report.Append($"[Perf] {System.DateTime.Now:yyyy-MM-dd HH:mm} scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {frames} frames\n");
            report.Append($"CPU avg {cpuSum / Mathf.Max(1, frames):0.0} ms, max {cpuMax:0.0} ms, >33ms {slowFrames} (target ≤10, 0)\n");
            report.Append(gc.Valid ? $"GC avg {gcSum / Mathf.Max(1, frames)} B, max {gcMax / 1024f:0.0} KB (target 0 B while playing)\n" : "GC n/a\n");
            report.Append(batches.Valid ? $"Batches max {batchMax}, SetPass max {setPassMax} (target ≤100 / ≤60 Home, ≤30)\n" : "Batches n/a\n");
            report.Append(ui.Valid ? $"UI rebuild avg {uiSum / Mathf.Max(1, frames):0.00} ms, max {uiMax:0.00} ms (target ≤1)\n" : "UI rebuild n/a\n");
            report.Append(instantiate.Valid ? $"Instantiate {instantiates} (target 0 while playing)\n" : "Instantiate n/a\n");
            report.Append(memory.Valid ? $"Memory {memoryMax / (1024f * 1024f):0} MB (target ≤400)" : "Memory n/a");

            mainThread.Dispose(); gc.Dispose(); batches.Dispose(); setPass.Dispose(); memory.Dispose(); ui.Dispose(); instantiate.Dispose();
            LastReport = report.ToString();
            Debug.Log(LastReport);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.persistentDataPath, "perf.txt"), LastReport + "\n\n"); }
            catch (System.Exception e) { Debug.LogWarning("[Perf] could not write perf.txt: " + e.Message); }
            Destroy(gameObject);
            _instance = null;
        }
    }
}
