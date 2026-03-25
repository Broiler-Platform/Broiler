#!/usr/bin/env python3
"""Acid3 pixel-level image comparison tool.

Compares a Broiler CLI render of Acid3 against a Chromium reference image,
producing a colour-coded diff image and a structured report.

Background pixels are identified and excluded from the primary pass/fail
content-area match metric, so that only foreground/content rendering
fidelity is assessed.

Usage:
    python3 scripts/acid3-compare.py <broiler_image> <reference_image> [--output-dir <dir>]

Output:
    - acid3-diff.png        Colour-coded diff image
    - acid3-report.txt      Human-readable comparison report

Colour coding in diff image:
    Green   — pixel match (per-channel difference <= tolerance)
    Red     — Content-area mismatch
    Yellow  — Background-area mismatch
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image


# --- Constants ---------------------------------------------------------------

# Per-channel colour tolerance (matches DeterministicRenderConfig.ColorTolerance)
COLOR_TOLERANCE = 5

# Background pixel threshold: pixels where all channels > this value in
# *both* images are classified as background and excluded from the primary
# content-area match metric.
BACKGROUND_THRESHOLD = 240

# Region definitions (x_start, x_end, y_start, y_end) for 1024x768 viewport
REGIONS = {
    "score_area":  (350, 700, 0, 80),
    "bucket_area": (0, 1024, 80, 400),
    "bottom_area": (0, 1024, 400, 768),
}


# --- Core comparison ---------------------------------------------------------

def load_and_normalise(path: str, target_size: tuple[int, int]) -> np.ndarray:
    """Load image, convert to RGB, and resize to target dimensions."""
    img = Image.open(path).convert("RGB")
    if img.size != target_size:
        img = img.resize(target_size, Image.LANCZOS)
    return np.array(img, dtype=np.int16)


def compute_match(actual: np.ndarray, reference: np.ndarray,
                  tolerance: int = COLOR_TOLERANCE) -> np.ndarray:
    """Return boolean mask where pixels match within tolerance."""
    diff = np.abs(actual - reference)
    return np.all(diff <= tolerance, axis=2)


def content_mask(actual: np.ndarray, reference: np.ndarray,
                 threshold: int = BACKGROUND_THRESHOLD) -> np.ndarray:
    """Return mask of non-background (content) pixels in either image.

    A pixel is considered background only if *all* channels exceed the
    threshold in *both* images.  Any pixel that has visible content in
    either image is classified as content.
    """
    return (np.any(actual <= threshold, axis=2) |
            np.any(reference <= threshold, axis=2))


def region_content_match(match: np.ndarray, content: np.ndarray,
                         region: tuple[int, int, int, int],
                         ) -> tuple[int, int, float]:
    """Compute content-area match within a named region.

    Returns (matching_content_pixels, total_content_pixels, pct).
    """
    x1, x2, y1, y2 = region
    r_content = content[y1:y2, x1:x2]
    r_match = match[y1:y2, x1:x2] & r_content
    total = int(np.sum(r_content))
    matching = int(np.sum(r_match))
    pct = matching / total * 100 if total > 0 else 0.0
    return matching, total, pct


def generate_diff_image(match: np.ndarray,
                        content: np.ndarray) -> Image.Image:
    """Generate a colour-coded diff image.

    Green  — pixel match (per-channel diff ≤ tolerance)
    Red    — content-area mismatch
    Yellow — background-area mismatch
    """
    h, w = match.shape
    out = np.zeros((h, w, 3), dtype=np.uint8)

    out[match] = [0, 180, 0]                   # Green: match
    out[~match & content] = [220, 40, 40]       # Red: content mismatch
    out[~match & ~content] = [180, 180, 40]     # Yellow: background mismatch

    return Image.fromarray(out)


# --- Report generation -------------------------------------------------------

def generate_report(
    broiler_path: str,
    reference_path: str,
    match: np.ndarray,
    content: np.ndarray,
    region_stats: dict[str, tuple[int, int, float]],
    viewport: tuple[int, int],
) -> str:
    """Generate a human-readable comparison report.

    The primary metric is the **content-area match** which ignores
    background pixels, focusing only on foreground/content fidelity.
    """
    h, w = match.shape
    total = h * w
    full_match = int(np.sum(match))

    content_total = int(np.sum(content))
    content_match = int(np.sum(match & content))
    content_pct = content_match / content_total * 100 if content_total > 0 else 0.0

    bg_total = total - content_total

    lines = [
        "=" * 70,
        "Acid3 Pixel Comparison Report",
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
    ]

    if content_total > 0:
        lines.append(
            f"Content-area match: {content_match:>10,} / {content_total:>10,}  "
            f"({content_pct:.2f}%)"
        )
    else:
        lines.append("Content-area match: N/A (no content pixels detected)")

    lines += [
        "",
        "--- Background / Content Classification ---",
        f"  Background pixels:  {bg_total:>10,}  "
        f"({bg_total / total * 100:.1f}%)",
        f"  Content pixels:     {content_total:>10,}  "
        f"({content_total / total * 100:.1f}%)",
        "",
        "--- Per-Region Content-Area Match ---",
        f"{'Region':<20} {'Match':>8} {'Total':>8} {'Pct':>8}",
        "-" * 50,
    ]

    for name, (m, t, p) in region_stats.items():
        lines.append(f"{name:<20} {m:>8,} {t:>8,} {p:>7.2f}%")

    lines += [
        "",
        "--- Diff Image Colour Key ---",
        f"  Green  — pixel match (per-channel diff ≤ {COLOR_TOLERANCE})",
        "  Red    — content-area mismatch",
        "  Yellow — background-area mismatch",
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

    # Compute comparison masks
    match = compute_match(broiler, reference)
    content = content_mask(broiler, reference)

    # Per-region content-area match statistics
    region_stats: dict[str, tuple[int, int, float]] = {}
    for name, bounds in REGIONS.items():
        region_stats[name] = region_content_match(match, content, bounds)

    # Generate diff image (content vs background colour coding)
    diff_img = generate_diff_image(match, content)
    diff_path = os.path.join(args.output_dir, "acid3-diff.png")
    diff_img.save(diff_path)
    print(f"Diff image saved to: {diff_path}")

    # Generate report
    report = generate_report(
        args.broiler_image, args.reference_image,
        match, content, region_stats, target_size,
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
