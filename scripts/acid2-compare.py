#!/usr/bin/env python3
"""Acid2 pixel-level image comparison tool.

Compares a Broiler CLI render of Acid2 (at #top anchor) against a Chromium
reference image, producing a colour-coded diff image and a structured report.

Usage:
    python3 scripts/acid2-compare.py <broiler_image> <reference_image> [--output-dir <dir>]

Output:
    - acid2-diff.png        Colour-coded diff image
    - acid2-report.txt      Human-readable comparison report

Colour coding in diff image:
    Green   — pixel match (difference <= tolerance)
    Red     — Content-area mismatch
    Yellow  — Background mismatch
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image


# --- Constants ---------------------------------------------------------------

# Per-channel colour tolerance (matches DeterministicRenderConfig.ColorTolerance)
COLOR_TOLERANCE = 5

# White threshold for content vs background classification
WHITE_THRESHOLD = 250

# Red pixel detection (R>200, G<50, B<50)
RED_THRESHOLD = (200, 50, 50)

# Region definitions: name → (y_start, y_end) for 1024×768 viewport at #top
REGIONS = {
    "forehead": (51, 68),
    "eyes":     (69, 129),
    "nose":     (130, 210),
    "smile":    (196, 260),
    "chin":     (261, 275),
}


# --- Core comparison ---------------------------------------------------------

def load_image(path: str) -> np.ndarray:
    """Load image as RGB numpy array."""
    return np.array(Image.open(path).convert("RGB"), dtype=np.int16)


def compute_match(actual: np.ndarray, reference: np.ndarray,
                  tolerance: int = COLOR_TOLERANCE) -> np.ndarray:
    """Return boolean mask where pixels match within tolerance."""
    diff = np.abs(actual - reference)
    return np.all(diff <= tolerance, axis=2)


def content_mask(actual: np.ndarray, reference: np.ndarray,
                 threshold: int = WHITE_THRESHOLD) -> np.ndarray:
    """Return mask of non-white (content) pixels in either image."""
    return (np.any(actual < threshold, axis=2) |
            np.any(reference < threshold, axis=2))


def count_red_pixels(image: np.ndarray) -> int:
    """Count red pixels (R>200, G<50, B<50) — Acid2 failure indicator."""
    return int(np.sum(
        (image[:, :, 0] > RED_THRESHOLD[0]) &
        (image[:, :, 1] < RED_THRESHOLD[1]) &
        (image[:, :, 2] < RED_THRESHOLD[2])
    ))


def region_match(match: np.ndarray, content: np.ndarray,
                 y_start: int, y_end: int) -> tuple[int, int, float]:
    """Compute content-area match in a row range. Returns (matching, total, pct)."""
    region_content = content[y_start:y_end + 1, :]
    region_match_px = match[y_start:y_end + 1, :] & region_content
    total = int(np.sum(region_content))
    matching = int(np.sum(region_match_px))
    pct = matching / total * 100 if total > 0 else 0.0
    return matching, total, pct


def generate_diff_image(match: np.ndarray, content: np.ndarray) -> Image.Image:
    """Generate colour-coded diff: green=match, red=content diff, yellow=bg diff."""
    h, w = match.shape
    out = np.zeros((h, w, 3), dtype=np.uint8)
    out[match] = [0, 255, 0]
    out[~match & content] = [255, 0, 0]
    out[~match & ~content] = [128, 128, 0]
    return Image.fromarray(out)


# --- Report generation -------------------------------------------------------

def generate_report(
    broiler_path: str,
    reference_path: str,
    actual: np.ndarray,
    match: np.ndarray,
    content: np.ndarray,
    red_count: int,
) -> str:
    """Generate a human-readable Acid2 comparison report."""
    h, w = match.shape
    total = h * w
    full_match = int(np.sum(match))
    content_total = int(np.sum(content))
    content_match = int(np.sum(match & content))

    lines = [
        "=" * 70,
        "Acid2 Pixel Comparison Report",
        "=" * 70,
        "",
        f"Broiler image:    {broiler_path}",
        f"Reference image:  {reference_path}",
        f"Viewport:         {w}×{h}",
        f"Color tolerance:  {COLOR_TOLERANCE} (per-channel)",
        "",
        "--- Overall Pixel Statistics ---",
        f"Full-image match:   {full_match:>10,} / {total:>10,}  "
        f"({full_match / total * 100:.2f}%)",
        f"Content-area match: {content_match:>10,} / {content_total:>10,}  "
        f"({content_match / content_total * 100:.2f}%)" if content_total > 0
        else "Content-area match: N/A",
        f"Red pixel count:    {red_count:>10,}",
        "",
        "--- Per-Region Content-Area Match ---",
        f"{'Region':<25} {'Y Range':>10} {'Match':>8} {'Total':>8} {'Pct':>8}",
        "-" * 65,
    ]

    for name, (y1, y2) in REGIONS.items():
        m, t, p = region_match(match, content, y1, y2)
        lines.append(f"{name:<25} {y1:>4}–{y2:<4} {m:>8,} {t:>8,} {p:>7.2f}%")

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
        description="Compare Broiler Acid2 render against Chromium reference"
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

    # Load images
    actual = load_image(args.broiler_image)
    reference = load_image(args.reference_image)

    # Compute comparison
    match = compute_match(actual, reference)
    content = content_mask(actual, reference)
    red_count = count_red_pixels(actual)

    # Generate diff image
    diff_img = generate_diff_image(match, content)
    diff_path = os.path.join(args.output_dir, "acid2-diff.png")
    diff_img.save(diff_path)
    print(f"Diff image saved to: {diff_path}")

    # Generate report
    report = generate_report(
        args.broiler_image, args.reference_image,
        actual, match, content, red_count,
    )
    report_path = os.path.join(args.output_dir, "acid2-report.txt")
    with open(report_path, "w") as f:
        f.write(report)
    print(f"Report saved to:     {report_path}")

    # Print to stdout
    print()
    print(report)

    return 0


if __name__ == "__main__":
    sys.exit(main())
