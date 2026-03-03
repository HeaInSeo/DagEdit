#!/usr/bin/env python3
"""
visualize_performance.py — 벤치마크 성능 추이 시각화

benchmarks/history/ 폴더의 모든 BDN JSON 파일을 읽어
docs/performance_trend.png 그래프를 생성합니다.

사용법:
    pip install matplotlib
    python3 scripts/visualize_performance.py
    python3 scripts/visualize_performance.py --history-dir benchmarks/history --output docs/performance_trend.png
"""
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

try:
    import matplotlib
    matplotlib.use("Agg")  # 헤드리스 환경 (CI 서버) 지원
    import matplotlib.pyplot as plt
    import matplotlib.dates as mdates
    import numpy as np
except ImportError as exc:
    print(f"오류: 필수 패키지 없음 — {exc}", file=sys.stderr)
    print("설치: pip install matplotlib numpy", file=sys.stderr)
    sys.exit(1)


# ─── 타입 ────────────────────────────────────────────────────────────────────

# {method_name: {param_label: [(date, mean_ns, allocated_bytes)]}}
SeriesMap = dict[str, dict[str, list[tuple[datetime, float, float | None]]]]


# ─── 날짜 파싱 ───────────────────────────────────────────────────────────────

def parse_date(filename: str, mtime: float) -> datetime:
    """
    파일명 'YYYY-MM-DD_HHMMSS-*.json' 에서 날짜를 파싱한다.
    파싱에 실패하면 파일 수정 시간을 폴백으로 사용한다.
    """
    try:
        date_part = filename.split("_")[0]
        return datetime.strptime(date_part, "%Y-%m-%d")
    except (ValueError, IndexError):
        return datetime.fromtimestamp(mtime)


# ─── 히스토리 로드 ───────────────────────────────────────────────────────────

def load_history(history_dir: Path) -> SeriesMap:
    """
    히스토리 폴더의 모든 BDN JSON 파일을 읽어
    {method: {params: [(date, mean_ns, allocated_bytes)]}} 형태로 반환한다.
    """
    series: SeriesMap = {}

    json_files = sorted(history_dir.glob("*.json"))
    if not json_files:
        return series

    for jf in json_files:
        date = parse_date(jf.name, jf.stat().st_mtime)
        try:
            with jf.open(encoding="utf-8") as f:
                data: dict[str, Any] = json.load(f)
        except (json.JSONDecodeError, OSError):
            print(f"  경고: {jf.name} 파싱 실패, 건너뜁니다.", file=sys.stderr)
            continue

        for bench in data.get("Benchmarks", []):
            method:  str  = bench.get("Method", "Unknown")
            params:  str  = bench.get("Parameters", "")
            stats        = bench.get("Statistics") or {}
            memory       = bench.get("Memory") or {}
            mean: float | None = stats.get("Mean")
            alloc: float | None = memory.get("BytesAllocatedPerOperation")

            if mean is None:
                continue

            series.setdefault(method, {}).setdefault(params, []).append(
                (date, float(mean), float(alloc) if alloc is not None else None)
            )

    # 날짜 오름차순 정렬
    for method in series:
        for params in series[method]:
            series[method][params].sort(key=lambda t: t[0])

    return series


# ─── 단위 결정 ───────────────────────────────────────────────────────────────

def best_ns_unit(values: list[float]) -> tuple[str, float]:
    """평균값 기준으로 최적 단위를 결정한다."""
    avg = sum(values) / len(values) if values else 0
    if avg >= 1_000_000_000:
        return "s", 1_000_000_000.0
    if avg >= 1_000_000:
        return "ms", 1_000_000.0
    if avg >= 1_000:
        return "μs", 1_000.0
    return "ns", 1.0


# ─── 그래프 생성 ─────────────────────────────────────────────────────────────

_PARAM_COLORS = {
    "NodeCount=10":   "#2196F3",   # 파랑
    "NodeCount=100":  "#FF9800",   # 주황
    "NodeCount=1000": "#E91E63",   # 빨강
}
_DEFAULT_COLORS = plt.rcParams["axes.prop_cycle"].by_key()["color"]


def plot_trends(series: SeriesMap, output_path: Path) -> None:
    """
    메서드별 성능 추이를 2×2 그리드로 시각화하여 PNG로 저장한다.
    각 서브플롯에는 NodeCount 파라미터별 색상 구분 선이 그려진다.
    """
    methods = sorted(series.keys())
    if not methods:
        print("⚠️  시각화할 데이터 없음 (benchmarks/history/ 폴더가 비어있습니다).")
        return

    n_cols = 2
    n_rows = (len(methods) + 1) // n_cols
    fig, axes = plt.subplots(
        n_rows, n_cols,
        figsize=(16, n_rows * 5),
        squeeze=False,
    )
    fig.suptitle(
        "DagEdit 벤치마크 성능 추이\n(BenchmarkDotNet · Short Job · 소수점 2자리 정밀도)",
        fontsize=15,
        fontweight="bold",
        y=1.02,
    )
    fig.patch.set_facecolor("#f8f9fa")

    for idx, method in enumerate(methods):
        row, col = divmod(idx, n_cols)
        ax = axes[row][col]
        ax.set_facecolor("white")

        param_series = series[method]

        # 단위 결정: 모든 파라미터의 평균을 고려
        all_means: list[float] = []
        for pts in param_series.values():
            all_means.extend(m for _, m, _ in pts)
        unit, divisor = best_ns_unit(all_means)

        for p_idx, (params, points) in enumerate(sorted(param_series.items())):
            dates  = [p[0] for p in points]
            means  = [p[1] / divisor for p in points]
            color  = _PARAM_COLORS.get(params, _DEFAULT_COLORS[p_idx % len(_DEFAULT_COLORS)])
            label  = params if params else "default"

            # 선 + 마커
            ax.plot(dates, means, "o-", color=color, linewidth=2, markersize=5, label=label)

            # 수치 레이블 (소수점 2자리)
            for d, v in zip(dates, means):
                ax.annotate(
                    f"{v:.2f}",
                    xy=(d, v),
                    xytext=(0, 7),
                    textcoords="offset points",
                    ha="center",
                    fontsize=7.5,
                    color=color,
                )

            # 추세선 (데이터 3점 이상)
            if len(dates) >= 3:
                x_ord = [d.toordinal() for d in dates]
                z = np.polyfit(x_ord, means, 1)
                p = np.poly1d(z)
                x_line = np.array([min(x_ord), max(x_ord)])
                y_line = p(x_line)
                d_line = [datetime.fromordinal(int(x)) for x in x_line]
                trend_color = "#d32f2f" if z[0] > 0 else "#388e3c"
                ax.plot(
                    d_line, y_line,
                    "--", color=trend_color, alpha=0.55, linewidth=1.2,
                    label=f"{label} 추세",
                )

        ax.set_title(method, fontsize=11, fontweight="bold", pad=8)
        ax.set_ylabel(f"Mean ({unit})", fontsize=9)
        ax.set_xlabel("날짜", fontsize=9)
        ax.xaxis.set_major_formatter(mdates.DateFormatter("%y-%m-%d"))
        ax.xaxis.set_major_locator(mdates.AutoDateLocator())
        plt.setp(ax.xaxis.get_majorticklabels(), rotation=30, ha="right", fontsize=8)
        ax.grid(True, alpha=0.3, linestyle="--")
        ax.legend(fontsize=8, loc="upper left", framealpha=0.8)

    # 남은 빈 서브플롯 숨기기
    for idx in range(len(methods), n_rows * n_cols):
        row, col = divmod(idx, n_cols)
        axes[row][col].set_visible(False)

    plt.tight_layout()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    plt.savefig(output_path, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
    plt.close(fig)
    print(f"✅ 그래프 저장 완료: {output_path}")


# ─── 진입점 ─────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(
        description="BenchmarkDotNet 히스토리 JSON → 성능 추이 PNG 생성",
    )
    parser.add_argument(
        "--history-dir",
        type=Path,
        default=Path("benchmarks/history"),
        help="히스토리 JSON 폴더 경로 (기본: benchmarks/history)",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("docs/performance_trend.png"),
        help="출력 PNG 경로 (기본: docs/performance_trend.png)",
    )
    args = parser.parse_args()

    if not args.history_dir.exists():
        print(f"오류: 히스토리 폴더 없음 — {args.history_dir}", file=sys.stderr)
        return 1

    print(f"📁 히스토리 폴더: {args.history_dir}")
    series = load_history(args.history_dir)

    if not series:
        print("⚠️  JSON 파일 없음. CI 실행 후 history/ 폴더에 데이터가 누적됩니다.")
        return 0

    total_pts = sum(
        len(pts)
        for method_data in series.values()
        for pts in method_data.values()
    )
    print(f"   벤치마크 메서드: {len(series)}개")
    print(f"   총 데이터 포인트: {total_pts}개")

    plot_trends(series, args.output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
