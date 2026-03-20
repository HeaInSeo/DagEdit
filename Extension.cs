using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace DagEdit
{
    public static class Extension
    {
        #region Static Methods

        // Subscribe 를 static 에서 사용하기 위해서
        public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> action)
        {
            return observable.Subscribe(new AnonymousObserver<T>(action));
        }

        public static T? GetParentControlOfType<T>(this Control child) where T : Control
        {
            _ = child ?? throw new ArgumentNullException(nameof(child));
            var current = child;
            while (current != null)
            {
                if (current is T target)
                {
                    return target;
                }

                current = current.GetVisualParent() as Control;
            }

            return default;
        }

        public static T? GetParentVisualOfType<T>(this Visual child) where T : Visual
        {
            _ = child ?? throw new ArgumentNullException(nameof(child));
            var current = child;
            while (current != null)
            {
                if (current is T target)
                {
                    return target;
                }

                current = current.GetVisualParent();
            }

            return default;
        }

        public static T? GetParentVisualByName<T>(this Visual child, string name) where T : Visual
        {
            _ = child ?? throw new ArgumentNullException(nameof(child));
            _ = name ?? throw new ArgumentNullException(nameof(name));
            var current = child;
            while (current != null)
            {
                if (current is T target && target.Name == name)
                {
                    return target;
                }

                current = current.GetVisualParent();
            }

            return default;
        }

        // 일단 대략적으로 만든 이걸로 테스트 해보자.
        public static T? GetChildControlByName<T>(this Visual container, string name) where T : Visual
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));
            _ = name ?? throw new ArgumentNullException(nameof(name));
            foreach (var visual in container.GetVisualDescendants())
            {
                if (visual.Name == name && visual is T foundElement)
                {
                    return foundElement;
                }
            }

            return null;
        }

        // TranslatePoint 사용 대신 TransformToVisual 와 Transform 를 썼다.
        // TranslatePoint 를 사용해서 코드를 줄일 수 있는데 일단 원리를 알고자 풀어썻다.
        // TranslatePoint 에서 내부적으로 TransformToVisual 와 Transform 를 사용한다.
        public static T? GetVisualUnderPointer<T>(this Visual container, Point pointerPosition) where T : Visual
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));
            foreach (var visual in container.GetVisualDescendants())
            {
                if (visual is T foundElement)
                {
                    // container로부터 visual로의 변환을 수행
                    var matrix = container.TransformToVisual(visual);
                    if (matrix.HasValue)
                    {
                        // 변환된 좌표 계산
                        var transformedPosition = matrix.Value.Transform(pointerPosition);

                        // 변환된 좌표가 visual의 Bounds 내에 있는지 확인
                        if (visual.Bounds.Contains(transformedPosition))
                        {
                            return foundElement;
                        }
                    }
                }
            }

            return null;
        }

        public static T? GetControlUnderPointer<T>(this Control container, Point pointerPosition) where T : Control
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));
            foreach (var control in container.GetVisualDescendants().OfType<Control>())
            {
                if (control is T foundElement)
                {
                    var matrix = container.TransformToVisual(control);
                    if (matrix.HasValue)
                    {
                        var transformedPosition = matrix.Value.Transform(pointerPosition);
                        if (control.Bounds.Contains(transformedPosition))
                        {
                            return foundElement;
                        }
                    }
                }
            }

            return null;
        }

        // snap 후보가 여러 개일 때 pointer와의 거리(bounds center 기준)가 가장 가까운 컨트롤 반환.
        // 단일 후보이면 GetControlUnderPointer와 동일한 결과. 동점은 tree 순서(first)가 우선.
        public static T? GetClosestControlUnderPointer<T>(this Control container, Point pointerPosition) where T : Control
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));

            T? best = null;
            double bestDistSq = double.MaxValue;

            foreach (var foundElement in container.GetVisualDescendants().OfType<T>())
            {
                var matrix = container.TransformToVisual(foundElement);
                if (!matrix.HasValue)
                {
                    continue;
                }

                var localPtr = matrix.Value.Transform(pointerPosition);
                if (!foundElement.Bounds.Contains(localPtr))
                {
                    continue;
                }

                double cx = foundElement.Bounds.Width / 2.0;
                double cy = foundElement.Bounds.Height / 2.0;
                double dx = localPtr.X - cx;
                double dy = localPtr.Y - cy;
                double distSq = (dx * dx) + (dy * dy);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = foundElement;
                }
            }

            return best;
        }

        // snap 후보 선택 pure function. IReadOnlyList<(Item, DistanceSq)>에서 DistanceSq가
        // 최소인 항목 반환. 동점은 리스트 순서(tree order) 우선. GUI 의존 없이 단위 테스트 가능.
        public static T? PickClosestCandidate<T>(IReadOnlyList<(T Item, double DistanceSq)> candidates) where T : class
        {
            _ = candidates ?? throw new ArgumentNullException(nameof(candidates));

            if (candidates.Count == 0)
            {
                return null;
            }

            T? best = candidates[0].Item;
            double bestDistSq = candidates[0].DistanceSq;

            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].DistanceSq < bestDistSq)
                {
                    bestDistSq = candidates[i].DistanceSq;
                    best = candidates[i].Item;
                }
            }

            return best;
        }

        // TODO 이름 수정할 필요 있을 듯. 너무 김.
        public static bool IsCoordinateSystemMatch(this Visual reference, Visual target)
        {
            _ = reference ?? throw new ArgumentNullException(nameof(reference));
            _ = target ?? throw new ArgumentNullException(nameof(target));
            var matrix = reference.TransformToVisual(target);

            // 변환 행렬이 존재하는 경우
            if (matrix.HasValue)
            {
                // 단위 행렬인지 확인
                return matrix.Value.IsIdentity;
            }

            // 변환 행렬이 없는 경우, 좌표계가 일치하지 않음을 의미
            return false;
        }

        // 전역적으로 좌표계가 일치하는지 찾는 메서드
        // 특화 되어 있음.
        // TODO 이거 필요 없을 수도 있음.
        public static bool IsCanvasMatched(Canvas? sourceCanvas, Canvas? targetCanvas)
        {
            _ = sourceCanvas ?? throw new ArgumentNullException(nameof(sourceCanvas));
            _ = targetCanvas ?? throw new ArgumentNullException(nameof(targetCanvas));

            bool isMatch = sourceCanvas.IsCoordinateSystemMatch(targetCanvas);
            return isMatch;
        }

        #endregion

        #region 개발중 간단한 테스트

        // 사용하지 않을 듯 하지만 일단 남겨 놓는다.
        // 로그 파일을 저장할 폴더와 파일 이름 정의
        private static readonly string logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");
        private static readonly string logFilePath = Path.Combine(logDirectory, "PerformanceLog.txt");
        private static readonly string logErrorsPath = Path.Combine(logDirectory, "ErrorsLog.txt");

        [Conditional("DEBUG")]
        public static void Log(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                string output =
                    DateTime.Now.ToString("hh:mm:ss") + ": " +
                    string.Format(format, args); //+ Environment.NewLine + Environment.StackTrace;

                //Console.WriteLine(output);
                Debug.WriteLine(output);
            }
        }

        public static void LogWriteToFile(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                string output = DateTime.Now.ToString("hh:mm:ss") + ": " + string.Format(format, args);
                WriteToFile(output);
            }
        }

        public static void LogPerformance(string message)
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            Process currentProcess = Process.GetCurrentProcess();
            long memoryUsage = currentProcess.WorkingSet64; // 메모리 사용량
            TimeSpan cpuTime = currentProcess.TotalProcessorTime; // CPU 사용 시간

            // 성능 정보 로깅
            LogWriteToFile(true, "{0} - Memory Usage: {1} bytes, CPU Time: {2} ms", message, memoryUsage,
                cpuTime.TotalMilliseconds);
        }

        public static void WriteToFile(string message)
        {
            try
            {
                // C# 8.0 이상에서 사용할 수 있는 간소화된 using 구문
                using var writer = new StreamWriter(logFilePath, true);
                writer.WriteLine(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Log file write error: " + ex.Message);
            }
        }

        public static void WriteErrorsToFile(string message)
        {
            _ = TryWriteErrorsToFile(message);
        }

        internal static bool TryWriteErrorsToFile(string message, string? errorsPath = null, Action<string>? fallback = null)
        {
            string targetPath = errorsPath ?? logErrorsPath;
            fallback ??= WriteDiagnosticFallback;

            try
            {
                string? directory = Path.GetDirectoryName(targetPath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(targetPath, true);
                writer.WriteLine(message);
                return true;
            }
            catch (Exception ex)
            {
                fallback($"Error log write failed for '{targetPath}': {ex.Message}");
                return false;
            }
        }

        private static void WriteDiagnosticFallback(string message)
        {
            Debug.WriteLine(message);
            Trace.WriteLine(message);
        }

        #endregion
    }
}
