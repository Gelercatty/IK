from __future__ import annotations

import argparse
import csv
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Optional


REACHABLE_METRICS = [
    ("avg_converged_iterations", "Reachable: Avg Success Iterations", "Iterations"),
    ("avg_runtime_all_samples", "Reachable: Avg Runtime at Solve End", "Time (ms)"),
    ("avg_joint_rotation_deviation_deg", "Reachable: Avg Joint Rotation Deviation", "Deviation (deg)"),
]

UNREACHABLE_METRICS = [
    ("avg_final_error", "Unreachable: Avg Final Error", "Position Error"),
    ("avg_runtime_all_samples", "Unreachable: Avg Runtime at Solve End", "Time (ms)"),
    ("avg_joint_rotation_deviation_deg", "Unreachable: Avg Joint Rotation Deviation", "Deviation (deg)"),
]

SERIES_STYLES = {
    "CCD": {"color": "#111111", "linestyle": "-", "marker": "o"},
    "JacobianTranspose": {"color": "#d97904", "linestyle": "-", "marker": "s"},
    "JacobianDLS_lambda_0.03": {"color": "#1f77b4", "linestyle": "-", "marker": "o"},
    "JacobianDLS_lambda_0.10": {"color": "#1f77b4", "linestyle": "--", "marker": "^"},
    "JacobianDLS_lambda_0.30": {"color": "#1f77b4", "linestyle": ":", "marker": "D"},
    "JacobianSvdDLS_default": {"color": "#2ca02c", "linestyle": "-.", "marker": "P"},
}

FIGURE_NOTE = "Fixed settings: maxIterations = 16, positionTolerance = 0.1, samples per category = 200"


def parse_args() -> argparse.Namespace:
    base_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description="Plot IK benchmark line charts and score bar chart.")
    parser.add_argument("--plot-csv", type=Path, default=base_dir / "IKBenchmarkPlotReady.csv", help="Path to IKBenchmarkPlotReady.csv")
    parser.add_argument("--score-csv", type=Path, default=base_dir / "IKBenchmarkBestScores.csv", help="Path to IKBenchmarkBestScores.csv")
    parser.add_argument("--outdir", type=Path, default=base_dir / "Plots", help="Output directory for PNG figures")
    parser.add_argument("--dpi", type=int, default=180, help="Image DPI")
    return parser.parse_args()


def parse_optional_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    value = value.strip()
    if not value:
        return None
    return float(value)


def load_plot_rows(csv_path: Path) -> List[dict]:
    rows: List[dict] = []
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for raw in reader:
            rows.append(
                {
                    "category_name": raw["category_name"],
                    "reachable_category": raw["reachable_category"] == "1",
                    "series_name": raw["series_name"],
                    "method_family": raw["method_family"],
                    "tuning_name": raw["tuning_name"],
                    "tuning_value": parse_optional_float(raw.get("tuning_value")),
                    "step_scale": float(raw["step_scale"]),
                    "metric_name": raw["metric_name"],
                    "metric_value": parse_optional_float(raw.get("metric_value")),
                }
            )
    return rows


def load_best_scores(csv_path: Path) -> List[dict]:
    rows: List[dict] = []
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        for raw in reader:
            rows.append(
                {
                    "series_name": raw["series_name"],
                    "method_family": raw["method_family"],
                    "tuning_name": raw["tuning_name"],
                    "tuning_value": parse_optional_float(raw.get("tuning_value")),
                    "best_step_scale": float(raw["best_step_scale"]),
                    "composite_score": float(raw["composite_score"]),
                }
            )
    return rows


def build_plot_series(rows: List[dict]) -> Dict[str, Dict[str, Dict[str, List[tuple[float, float]]]]]:
    data: Dict[str, Dict[str, Dict[str, List[tuple[float, float]]]]] = defaultdict(
        lambda: defaultdict(lambda: defaultdict(list))
    )

    for row in rows:
        metric_value = row["metric_value"]
        if metric_value is None:
            continue

        data[row["category_name"]][row["metric_name"]][row["series_name"]].append(
            (row["step_scale"], metric_value)
        )

    for category in data.values():
        for metric in category.values():
            for series_name, points in metric.items():
                points.sort(key=lambda item: item[0])

    return data


def build_meta(rows: List[dict]) -> Dict[str, dict]:
    meta: Dict[str, dict] = {}
    for row in rows:
        series_name = row["series_name"]
        if series_name not in meta:
            meta[series_name] = {
                "method_family": row["method_family"],
                "tuning_name": row["tuning_name"],
                "tuning_value": row["tuning_value"],
            }
    return meta


def make_label(series_name: str, meta: dict) -> str:
    tuning_name = meta.get("tuning_name")
    tuning_value = meta.get("tuning_value")
    if tuning_name and tuning_value is not None:
        return f"{series_name} ({tuning_name}={tuning_value:g})"
    return series_name


def series_sort_key(series_name: str) -> tuple[int, str]:
    if series_name.startswith("CCD"):
        return (0, series_name)
    if series_name.startswith("JacobianTranspose"):
        return (1, series_name)
    if series_name.startswith("JacobianDLS"):
        return (2, series_name)
    if series_name.startswith("JacobianSvdDLS"):
        return (3, series_name)
    return (9, series_name)


def plot_category_overview(category_name: str, metrics: List[tuple[str, str, str]], series_data: Dict[str, Dict[str, List[tuple[float, float]]]], meta: Dict[str, dict], outdir: Path, dpi: int) -> None:
    import matplotlib.pyplot as plt

    fig, axes = plt.subplots(1, 3, figsize=(20, 5.6), constrained_layout=True)
    legend_handles = []
    legend_labels = []

    for axis, (metric_name, title, ylabel) in zip(axes, metrics):
        handles, labels = plot_metric_axis(
            axis,
            series_data.get(metric_name, {}),
            title,
            ylabel,
            meta,
        )
        if handles and not legend_handles:
            legend_handles = handles
            legend_labels = labels

    fig.suptitle(f"IK Benchmark - {category_name}\n{FIGURE_NOTE}", fontsize=13)
    if legend_handles:
        fig.legend(
            legend_handles,
            legend_labels,
            loc="center left",
            bbox_to_anchor=(1.01, 0.5),
            ncol=1,
            frameon=True,
        )

    outpath = outdir / f"IKBenchmark_{sanitize_name(category_name)}_overview.png"
    fig.savefig(outpath, dpi=dpi, bbox_inches="tight")
    plt.close(fig)


def plot_metric_axis(axis, series_data: Dict[str, List[tuple[float, float]]], title: str, ylabel: str, meta: Dict[str, dict]):
    from matplotlib.ticker import FuncFormatter

    handles = []
    labels = []

    for series_name in sorted(series_data.keys(), key=series_sort_key):
        points = series_data[series_name]
        xs = [point[0] for point in points]
        ys = [point[1] for point in points]

        if not xs:
            continue

        style = SERIES_STYLES.get(series_name, {"color": None, "linestyle": "-", "marker": "o"})
        line = axis.plot(
            xs,
            ys,
            color=style["color"],
            linestyle=style["linestyle"],
            marker=style["marker"],
            linewidth=2.0,
            markersize=5,
            label=make_label(series_name, meta.get(series_name, {})),
        )[0]
        handles.append(line)
        labels.append(line.get_label())

    axis.set_xscale("log")
    axis.set_xlim(1e-4, 5e-2)
    axis.set_xlabel("Step Scale")
    axis.set_ylabel(ylabel)
    axis.set_title(title)
    axis.grid(True, which="both", alpha=0.25)
    axis.set_xticks([1e-4, 2e-4, 5e-4, 1e-3, 2e-3, 5e-3, 1e-2, 2e-2, 5e-2])
    axis.xaxis.set_major_formatter(FuncFormatter(lambda value, _: f"{value:.4g}"))

    return handles, labels


def plot_best_score_bars(best_scores: List[dict], outdir: Path, dpi: int) -> None:
    import matplotlib.pyplot as plt

    rows = sorted(best_scores, key=lambda item: item["composite_score"], reverse=True)
    labels = [row["series_name"] for row in rows]
    values = [row["composite_score"] for row in rows]
    colors = [SERIES_STYLES.get(row["series_name"], {"color": "#888888"})["color"] for row in rows]

    fig, axis = plt.subplots(figsize=(11.5, 6.0), constrained_layout=True)
    bars = axis.bar(labels, values, color=colors, alpha=0.9)

    for bar, row in zip(bars, rows):
        axis.text(
            bar.get_x() + bar.get_width() / 2.0,
            bar.get_height() + 0.8,
            f"{row['composite_score']:.1f}\nstep={row['best_step_scale']:.4g}",
            ha="center",
            va="bottom",
            fontsize=9,
        )

    axis.set_ylabel("Composite Score")
    axis.set_title("IK Benchmark - Best Composite Score by Solver\nHigher is better; each bar uses the best tested step scale")
    axis.grid(True, axis="y", alpha=0.25)
    axis.set_axisbelow(True)
    axis.tick_params(axis="x", rotation=20)

    outpath = outdir / "IKBenchmark_BestScores.png"
    fig.savefig(outpath, dpi=dpi, bbox_inches="tight")
    plt.close(fig)


def sanitize_name(value: str) -> str:
    return value.replace(" ", "_").replace("/", "_")


def main() -> None:
    try:
        import matplotlib  # noqa: F401
    except ModuleNotFoundError as exc:
        raise SystemExit(
            "matplotlib is required to draw the benchmark figures.\n"
            "Install it with: python -m pip install matplotlib"
        ) from exc

    args = parse_args()
    plot_csv = args.plot_csv.resolve()
    score_csv = args.score_csv.resolve()
    outdir = args.outdir.resolve()
    outdir.mkdir(parents=True, exist_ok=True)

    plot_rows = load_plot_rows(plot_csv)
    plot_series = build_plot_series(plot_rows)
    meta = build_meta(plot_rows)

    plot_category_overview("Reachable", REACHABLE_METRICS, plot_series.get("Reachable", {}), meta, outdir, args.dpi)
    plot_category_overview("Unreachable", UNREACHABLE_METRICS, plot_series.get("Unreachable", {}), meta, outdir, args.dpi)

    best_scores = load_best_scores(score_csv)
    plot_best_score_bars(best_scores, outdir, args.dpi)

    print(f"Saved plots to: {outdir}")


if __name__ == "__main__":
    main()
