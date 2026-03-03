#!/usr/bin/env python3
"""
compare_benchmarks.py — BenchmarkDotNet JSON 성능 회귀 비교기

사용법:
    python3 scripts/compare_benchmarks.py <baseline.json> <current.json>

종료 코드:
    0  모든 지표가 허용 한도 내 (또는 기준선 없음 → 첫 실행)
    1  10% 이상 성능 회귀 감지 → 빌드 실패
    2  인자 오류 또는 JSON 파싱 실패
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

# ─── 설정 ───────────────────────────────────────────────────────────────────
REGRESSION_THRESHOLD = 0.10   # 10% 이상 악화 시 회귀로 판정

# 비교할 지표: {internal_key: (표시_이름, BDN_섹션, BDN_필드)}
METRICS: dict[str, tuple[str, str, str]] = {
    "Mean":      ("실행 시간",  "Statistics", "Mean"),
    "Allocated": ("메모리 할당", "Memory",     "BytesAllocatedPerOperation"),
}


# ─── 단위 포맷 ──────────────────────────────────────────────────────────────

def fmt_ns(ns: float) -> str:
    """나노초 → 가독성 단위 (소수점 2자리)."""
    if ns >= 1_000_000_000:
        return f"{ns / 1_000_000_000:.2f} s"
    if ns >= 1_000_000:
        return f"{ns / 1_000_000:.2f} ms"
    if ns >= 1_000:
        return f"{ns / 1_000:.2f} μs"
    return f"{ns:.2f} ns"


def fmt_bytes(b: float) -> str:
    """바이트 → 가독성 단위 (소수점 2자리)."""
    if b >= 1_048_576:
        return f"{b / 1_048_576:.2f} MB"
    if b >= 1_024:
        return f"{b / 1_024:.2f} KB"
    return f"{b:.2f} B"


def fmt_metric(key: str, value: float) -> str:
    """지표 키에 맞는 포맷 적용."""
    return fmt_ns(value) if key == "Mean" else fmt_bytes(value)


# ─── JSON 로드 ───────────────────────────────────────────────────────────────

def load_results(path: Path) -> dict[str, dict[str, float | None]]:
    """
    BDN JSON 파일을 파싱하여
    {FullName: {Mean: float | None, Allocated: float | None}} 형태로 반환.
    """
    with path.open(encoding="utf-8") as f:
        data: dict[str, Any] = json.load(f)

    out: dict[str, dict[str, float | None]] = {}
    for bench in data.get("Benchmarks", []):
        name: str = bench.get("FullName", "")
        row: dict[str, float | None] = {}
        for key, (_label, section, field) in METRICS.items():
            section_data = bench.get(section) or {}
            raw = section_data.get(field)
            row[key] = float(raw) if raw is not None else None
        out[name] = row
    return out


# ─── 비교 로직 ───────────────────────────────────────────────────────────────

def compare(
    baseline: dict[str, dict[str, float | None]],
    current:  dict[str, dict[str, float | None]],
) -> list[dict[str, Any]]:
    """
    기준선과 현재 결과를 비교하여 회귀 목록을 반환한다.
    테이블 형태로 전체 비교 결과를 출력한다.
    """
    regressions: list[dict[str, Any]] = []

    # 테이블 헤더
    W_NAME, W_METRIC, W_VAL, W_PCT = 70, 12, 17, 11
    header = (
        f"  {'벤치마크':<{W_NAME}} "
        f"{'지표':<{W_METRIC}} "
        f"{'기준값':>{W_VAL}} "
        f"{'현재값':>{W_VAL}} "
        f"{'변화율':>{W_PCT}}"
    )
    print(f"\n{header}")
    print("  " + "─" * (W_NAME + W_METRIC + W_VAL * 2 + W_PCT + 6))

    for name in sorted(current):
        curr = current[name]
        if name not in baseline:
            print(f"  [NEW]  {name[:W_NAME]}")
            continue

        base = baseline[name]

        for key, (label, _sec, _fld) in METRICS.items():
            c_val = curr.get(key)
            b_val = base.get(key)
            if c_val is None or b_val is None or b_val == 0.0:
                continue

            delta    = (c_val - b_val) / b_val
            pct      = delta * 100.0
            sign     = "+" if pct >= 0 else ""
            is_regr  = delta > REGRESSION_THRESHOLD
            icon     = "❌" if is_regr else "✅"

            print(
                f"  {icon} {name:<{W_NAME - 3}} "
                f"{label:<{W_METRIC}} "
                f"{fmt_metric(key, b_val):>{W_VAL}} "
                f"{fmt_metric(key, c_val):>{W_VAL}} "
                f"{sign}{pct:>{W_PCT - 1}.2f}%"
            )

            if is_regr:
                regressions.append({
                    "name":      name,
                    "metric":    label,
                    "key":       key,
                    "base_val":  b_val,
                    "curr_val":  c_val,
                    "delta_pct": pct,
                })

    return regressions


# ─── 진입점 ─────────────────────────────────────────────────────────────────

def main() -> int:
    if len(sys.argv) != 3:
        print(
            f"사용법: {sys.argv[0]} <baseline.json> <current.json>",
            file=sys.stderr,
        )
        return 2

    baseline_path = Path(sys.argv[1])
    current_path  = Path(sys.argv[2])

    if not current_path.exists():
        print(f"오류: 현재 결과 파일 없음 — {current_path}", file=sys.stderr)
        return 2

    # 기준선 없음 → 첫 번째 실행, 성공으로 처리
    if not baseline_path.exists():
        print(f"\n📋 기준선 파일 없음 ({baseline_path.name})")
        print("   → 첫 번째 실행으로 판단합니다. 빌드를 성공으로 처리합니다.")
        print("   → 현재 결과가 다음 빌드의 기준선으로 저장됩니다.\n")
        return 0

    print(f"\n📊 성능 회귀 검사 (BenchmarkDotNet)")
    print(f"   기준선 : {baseline_path}")
    print(f"   현재   : {current_path}")
    print(f"   임계값 : 평균 실행 시간 또는 메모리 할당이 +{REGRESSION_THRESHOLD * 100:.0f}% 이상 증가 시 실패")

    try:
        baseline = load_results(baseline_path)
        current  = load_results(current_path)
    except (json.JSONDecodeError, KeyError, ValueError) as exc:
        print(f"\n오류: JSON 파싱 실패 — {exc}", file=sys.stderr)
        return 2

    regressions = compare(baseline, current)

    sep = "=" * 72
    if regressions:
        print(f"\n{sep}")
        print(f"❌  성능 회귀 {len(regressions)}건 감지 — 빌드 실패")
        print(f"{sep}")
        for r in regressions:
            b_fmt = fmt_metric(r["key"], r["base_val"])
            c_fmt = fmt_metric(r["key"], r["curr_val"])
            print(f"\n  벤치마크 : {r['name']}")
            print(f"  지표     : {r['metric']}")
            print(f"  기준값   : {b_fmt}")
            print(f"  현재값   : {c_fmt}")
            print(f"  변화율   : +{r['delta_pct']:.2f}%  (허용 한도 +{REGRESSION_THRESHOLD * 100:.0f}%)")
        print()
        return 1

    print(f"\n{sep}")
    print(f"✅  성능 회귀 없음 — 모든 지표가 허용 범위 내 (소수점 2자리 정밀도)")
    print(f"{sep}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
