#!/usr/bin/env python3
"""Acid1 pixel-level image comparison tool.

Compares a Broiler CLI render of Acid1 against a Chromium/Playwright
reference image, producing a colour-coded diff image and a structured report.
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image


COLOR_TOLERANCE = 5
WHITE_THRESHOLD = 250

REGIONS = {
    "toggle_column": (18, 118, 16, 341),
    "upper_layout": (118, 487, 16, 197),
    "form_controls": (159, 360, 86, 180),
    "lower_layout": (118, 487, 197, 344),
    "footer_text": (18, 487, 344, 405),
}


def load_and_normalise(path: str, target_size: tuple[int, int]) -> np.ndarray:
    image = Image.open(path).convert("RGB")
    if image.size != target_size:
        image = image.resize(target_size, Image.LANCZOS)
    return np.array(image, dtype=np.int16)


def compute_match(actual: np.ndarray, reference: np.ndarray,
                  tolerance: int = COLOR_TOLERANCE) -> np.ndarray:
    diff = np.abs(actual - reference)
    return np.all(diff <= tolerance, axis=2)


def content_mask(actual: np.ndarray, reference: np.ndarray,
                 threshold: int = WHITE_THRESHOLD) -> np.ndarray:
    return (np.any(actual < threshold, axis=2) |
            np.any(reference < threshold, axis=2))


def region_match(match: np.ndarray, content: np.ndarray,
                 region: tuple[int, int, int, int]) -> tuple[int, int, float]:
    x1, x2, y1, y2 = region
    region_content = content[y1:y2, x1:x2]
    region_match_px = match[y1:y2, x1:x2] & region_content
    total = int(np.sum(region_content))
    matching = int(np.sum(region_match_px))
    pct = matching / total * 100 if total > 0 else 0.0
    return matching, total, pct


def generate_diff_image(match: np.ndarray, content: np.ndarray) -> Image.Image:
    out = np.zeros((*match.shape, 3), dtype=np.uint8)
    out[match] = [0, 180, 0]
    out[~match & content] = [220, 40, 40]
    out[~match & ~content] = [220, 220, 40]
    return Image.fromarray(out)


def mismatch_bounding_box(match: np.ndarray) -> str:
    coords = np.argwhere(~match)
    if coords.size == 0:
        return "none"

    y1, x1 = coords.min(axis=0)
    y2, x2 = coords.max(axis=0)
    return f"x={x1}-{x2}, y={y1}-{y2}"


def generate_report(
    broiler_path: str,
    reference_path: str,
    match: np.ndarray,
    content: np.ndarray,
    region_stats: dict[str, tuple[int, int, float]],
    viewport: tuple[int, int],
) -> str:
    h, w = match.shape
    total = h * w
    full_match = int(np.sum(match))
    content_total = int(np.sum(content))
    content_match = int(np.sum(match & content))
    content_pct = content_match / content_total * 100 if content_total > 0 else 0.0

    lines = [
        "=" * 70,
        "Acid1 Pixel Comparison Report",
        "=" * 70,
        "",
        f"Broiler image:    {broiler_path}",
        f"Reference image:  {reference_path}",
        f"Viewport:         {viewport[0]}×{viewport[1]}",
        f"Color tolerance:  {COLOR_TOLERANCE} (per-channel)",
        "",
        "--- Overall Pixel Statistics ---",
        f"Full-image match:   {full_match:>10,} / {total:>10,}  "
        f"({full_match / total * 100:.2f}%)",
        f"Content-area match: {content_match:>10,} / {content_total:>10,}  "
        f"({content_pct:.2f}%)" if content_total > 0 else "Content-area match: N/A",
        f"Mismatch bounds:    {mismatch_bounding_box(match)}",
        "",
        "--- Per-Region Content-Area Match ---",
        f"{'Region':<20} {'Match':>8} {'Total':>8} {'Pct':>8}",
        "-" * 50,
    ]

    for name, stats in region_stats.items():
        matching, total_pixels, pct = stats
        lines.append(f"{name:<20} {matching:>8,} {total_pixels:>8,} {pct:>7.2f}%")

    lines += [
        "",
        "--- Notes ---",
        "Acid1 mismatches are expected to cluster around native radio controls",
        "and text anti-aliasing when Broiler is compared against Chromium.",
        "",
        "--- Diff Image Colour Key ---",
        f"  Green  — pixel match (per-channel diff ≤ {COLOR_TOLERANCE})",
        "  Red    — content-area mismatch",
        "  Yellow — background-area mismatch",
        "",
        "=" * 70,
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compare Broiler Acid1 render against Chromium reference"
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

    with Image.open(args.reference_image) as image:
        target_size = image.size

    actual = load_and_normalise(args.broiler_image, target_size)
    reference = load_and_normalise(args.reference_image, target_size)

    match = compute_match(actual, reference)
    content = content_mask(actual, reference)
    region_stats = {
        name: region_match(match, content, bounds)
        for name, bounds in REGIONS.items()
    }

    diff_path = os.path.join(args.output_dir, "acid1-diff.png")
    generate_diff_image(match, content).save(diff_path)
    print(f"Diff image saved to: {diff_path}")

    report_path = os.path.join(args.output_dir, "acid1-report.txt")
    report = generate_report(
        args.broiler_image, args.reference_image, match, content, region_stats, target_size,
    )
    with open(report_path, "w", encoding="utf-8") as handle:
        handle.write(report)
    print(f"Report saved to:     {report_path}")
    print()
    print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
