#!/usr/bin/env python3
"""Acid3 pixel-level image comparison tool.

Compares a Broiler CLI render of Acid3 against a Chromium reference image,
producing a colour-coded diff image and a structured report.

Usage:
    python3 scripts/acid3-compare.py <broiler_image> <reference_image> [--output-dir <dir>]

Output:
    - acid3-diff.png        Colour-coded diff image
    - acid3-report.txt      Human-readable comparison report

Colour coding in diff image:
    Green   — pixel match (difference <= 10)
    Red     — Broiler has darker content where reference is lighter
    Blue    — Reference has darker content where Broiler is lighter
    Yellow  — Other colour differences
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image


# --- Constants ---------------------------------------------------------------

# Tolerance thresholds per RGB channel (0–255)
EXACT_THRESHOLD = 0       # Exact match
NEAR_THRESHOLD = 5        # Near match (anti-aliasing, sub-pixel)
SIGNIFICANT_THRESHOLD = 25  # Significant visual difference

# Region definitions (x_start, x_end, y_start, y_end) for 1024x768 viewport
REGIONS = {
    "score_area":  (350, 700, 0, 80),
    "bucket_area": (0, 1024, 80, 400),
    "bottom_area": (0, 1024, 400, 768),
}

# Background pixel threshold: mean channel > 240 is considered background
BACKGROUND_THRESHOLD = 240


# --- Core comparison ---------------------------------------------------------

def load_and_normalise(path: str, target_size: tuple[int, int]) -> np.ndarray:
    """Load image, convert to RGB, and resize to target dimensions."""
    img = Image.open(path).convert("RGB")
    if img.size != target_size:
        img = img.resize(target_size, Image.LANCZOS)
    return np.array(img, dtype=np.int16)


def compute_diff(broiler: np.ndarray, reference: np.ndarray) -> np.ndarray:
    """Compute per-pixel absolute difference (signed for direction)."""
    return broiler - reference


def classify_pixels(diff: np.ndarray) -> dict[str, int]:
    """Classify pixels into exact, near, and significant difference buckets."""
    abs_diff = np.abs(diff)
    max_channel_diff = abs_diff.max(axis=2)
    total = max_channel_diff.size

    exact = int(np.sum(max_channel_diff == EXACT_THRESHOLD))
    near = int(np.sum(max_channel_diff <= NEAR_THRESHOLD))
    significant = int(np.sum(max_channel_diff > SIGNIFICANT_THRESHOLD))

    return {
        "total_pixels": total,
        "exact_matches": exact,
        "exact_pct": exact / total * 100,
        "near_matches": near,
        "near_pct": near / total * 100,
        "significant_differences": significant,
        "significant_pct": significant / total * 100,
    }


def analyse_region(diff: np.ndarray, region: tuple[int, int, int, int]) -> float:
    """Compute mean absolute pixel difference in a named region."""
    x1, x2, y1, y2 = region
    region_diff = np.abs(diff[y1:y2, x1:x2])
    return float(np.mean(region_diff))


def classify_background(reference: np.ndarray) -> dict[str, float]:
    """Classify pixels as background (white) or content and report percentages."""
    mean_channels = reference.mean(axis=2)
    bg_mask = mean_channels > BACKGROUND_THRESHOLD
    total = mean_channels.size
    bg_count = int(np.sum(bg_mask))
    return {
        "background_pct": bg_count / total * 100,
        "content_pct": (total - bg_count) / total * 100,
    }


def generate_diff_image(diff: np.ndarray) -> Image.Image:
    """Generate a colour-coded diff image.

    Green  — match (diff <= 10)
    Red    — Broiler darker (broiler > reference in luminance)
    Blue   — Reference darker (reference > broiler in luminance)
    Yellow — Other colour differences
    """
    abs_diff = np.abs(diff)
    max_diff = abs_diff.max(axis=2)

    # Luminance difference (signed): positive = Broiler brighter
    luminance = diff.astype(np.float32).mean(axis=2)

    h, w = max_diff.shape
    out = np.zeros((h, w, 3), dtype=np.uint8)

    # Green: match region
    match_mask = max_diff <= 10
    out[match_mask] = [0, 180, 0]

    # Red: Broiler has darker content where reference is lighter
    broiler_darker = (~match_mask) & (luminance < -10)
    out[broiler_darker] = [220, 40, 40]

    # Blue: Reference has darker content where Broiler is lighter
    ref_darker = (~match_mask) & (luminance > 10)
    out[ref_darker] = [40, 40, 220]

    # Yellow: Other colour differences
    other = (~match_mask) & (~broiler_darker) & (~ref_darker)
    out[other] = [220, 220, 40]

    return Image.fromarray(out)


# --- Report generation -------------------------------------------------------

def generate_report(
    broiler_path: str,
    reference_path: str,
    stats: dict,
    regions: dict[str, float],
    bg_info: dict,
) -> str:
    """Generate a human-readable comparison report."""
    lines = [
        "=" * 70,
        "Acid3 Pixel Comparison Report",
        "=" * 70,
        "",
        f"Broiler image:    {broiler_path}",
        f"Reference image:  {reference_path}",
        "",
        "--- Overall Pixel Statistics ---",
        f"Total pixels compared:       {stats['total_pixels']:>10,}",
        f"Exact matches:               {stats['exact_matches']:>10,}  ({stats['exact_pct']:.1f}%)",
        f"Near matches (≤{NEAR_THRESHOLD}):          {stats['near_matches']:>10,}  ({stats['near_pct']:.1f}%)",
        f"Significant differences (>{SIGNIFICANT_THRESHOLD}): {stats['significant_differences']:>10,}  ({stats['significant_pct']:.1f}%)",
        "",
        "--- Region Analysis (Mean Pixel Difference) ---",
    ]
    for name, mean_diff in regions.items():
        lines.append(f"  {name:20s}  {mean_diff:7.1f}")

    lines += [
        "",
        "--- Background / Content Classification ---",
        f"  Background pixels:  {bg_info['background_pct']:.1f}%",
        f"  Content pixels:     {bg_info['content_pct']:.1f}%",
        "",
        "--- Diff Image Colour Key ---",
        "  Green  — pixel match (difference ≤ 10)",
        "  Red    — Broiler has darker content where reference is lighter",
        "  Blue   — Reference has darker content where Broiler is lighter",
        "  Yellow — Other colour differences",
        "",
        "=" * 70,
    ]
    return "\n".join(lines)


# --- Main entry point --------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compare Broiler Acid3 render against Chromium reference"
    )
    parser.add_argument("broiler_image", help="Path to Broiler CLI rendered image")
    parser.add_argument("reference_image", help="Path to Chromium reference image")
    parser.add_argument(
        "--output-dir",
        default=".",
        help="Directory for diff image and report (default: current directory)",
    )
    args = parser.parse_args()

    if not os.path.isfile(args.broiler_image):
        print(f"Error: Broiler image not found: {args.broiler_image}", file=sys.stderr)
        return 1
    if not os.path.isfile(args.reference_image):
        print(f"Error: Reference image not found: {args.reference_image}", file=sys.stderr)
        return 1

    os.makedirs(args.output_dir, exist_ok=True)

    # Load images — normalise to reference dimensions
    ref_img = Image.open(args.reference_image)
    target_size = ref_img.size  # (width, height)
    ref_img.close()

    reference = load_and_normalise(args.reference_image, target_size)
    broiler = load_and_normalise(args.broiler_image, target_size)

    # Compute difference
    diff = compute_diff(broiler, reference)

    # Statistics
    stats = classify_pixels(diff)
    region_results = {}
    for name, bounds in REGIONS.items():
        region_results[name] = analyse_region(diff, bounds)
    bg_info = classify_background(reference)

    # Generate diff image
    diff_img = generate_diff_image(diff)
    diff_path = os.path.join(args.output_dir, "acid3-diff.png")
    diff_img.save(diff_path)
    print(f"Diff image saved to: {diff_path}")

    # Generate report
    report = generate_report(
        args.broiler_image, args.reference_image, stats, region_results, bg_info
    )
    report_path = os.path.join(args.output_dir, "acid3-report.txt")
    with open(report_path, "w") as f:
        f.write(report)
    print(f"Report saved to:     {report_path}")

    # Print report to stdout
    print()
    print(report)

    return 0


if __name__ == "__main__":
    sys.exit(main())
