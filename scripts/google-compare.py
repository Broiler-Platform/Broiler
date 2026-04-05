#!/usr/bin/env python3
"""Google Search pixel-level image comparison tool.

Compares a Broiler CLI render of Google Search against a Chromium reference
image, producing a colour-coded diff image and a structured report.

Usage:
    python3 scripts/google-compare.py <broiler_image> <reference_image> [--output-dir <dir>]

Output:
    - google-diff.png        Colour-coded diff image
    - google-report.txt      Human-readable comparison report

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
WHITE_THRESHOLD = 245

# Region definitions for Google Search homepage at 1024×768
# Approximate Y-ranges for key content areas
REGIONS = {
    "top-bar":       (0, 50),        # Gmail, Images, Sign in, apps icon
    "logo-area":     (150, 310),     # Google logo and padding
    "search-box":    (310, 380),     # Search input field
    "buttons":       (380, 440),     # "Google Search" and "I'm Feeling Lucky"
    "footer":        (700, 768),     # Footer links (About, Advertising, etc.)
}


# --- Core comparison ---------------------------------------------------------

def load_image(path: str) -> np.ndarray:
    """Load image as RGB numpy array, resizing to 1024×768 if needed."""
    img = Image.open(path).convert("RGB")
    if img.size != (1024, 768):
        img = img.resize((1024, 768), Image.LANCZOS)
    return np.array(img, dtype=np.int16)


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
) -> str:
    """Generate a human-readable Google Search comparison report."""
    h, w = match.shape
    total = h * w
    full_match = int(np.sum(match))
    content_total = int(np.sum(content))
    content_match = int(np.sum(match & content))

    lines = [
        "=" * 70,
        "Google Search Pixel Comparison Report",
        "=" * 70,
        "",
        f"Broiler image:    {broiler_path}",
        f"Reference image:  {reference_path}",
        f"Viewport:         {w}x{h}",
        f"Color tolerance:  {COLOR_TOLERANCE} (per-channel)",
        "",
        "--- Overall Pixel Statistics ---",
        f"Full-image match:   {full_match:>10,} / {total:>10,}  "
        f"({full_match / total * 100:.2f}%)",
    ]

    if content_total > 0:
        lines.append(
            f"Content-area match: {content_match:>10,} / {content_total:>10,}  "
            f"({content_match / content_total * 100:.2f}%)"
        )
    else:
        lines.append("Content-area match: N/A")

    lines += [
        "",
        "--- Per-Region Content-Area Match ---",
        f"{'Region':<25} {'Y Range':>10} {'Match':>8} {'Total':>8} {'Pct':>8}",
        "-" * 65,
    ]

    for name, (y1, y2) in REGIONS.items():
        m, t, p = region_match(match, content, y1, y2)
        lines.append(f"{name:<25} {y1:>4}-{y2:<4} {m:>8,} {t:>8,} {p:>7.2f}%")

    lines += [
        "",
        "--- Diff Image Colour Key ---",
        f"  Green  -- pixel match (per-channel diff <= {COLOR_TOLERANCE})",
        "  Red    -- content-area mismatch",
        "  Yellow -- background-area mismatch",
        "",
        "=" * 70,
    ]
    return "\n".join(lines)


# --- Main entry point --------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compare Broiler Google Search render against Chromium reference"
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

    # Generate diff image
    diff_img = generate_diff_image(match, content)
    diff_path = os.path.join(args.output_dir, "google-diff.png")
    diff_img.save(diff_path)
    print(f"Diff image saved to: {diff_path}")

    # Generate report
    report = generate_report(
        args.broiler_image, args.reference_image,
        actual, match, content,
    )
    report_path = os.path.join(args.output_dir, "google-report.txt")
    with open(report_path, "w") as f:
        f.write(report)
    print(f"Report saved to:     {report_path}")

    # Print to stdout
    print()
    print(report)

    return 0


if __name__ == "__main__":
    sys.exit(main())
