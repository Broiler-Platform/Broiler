#!/usr/bin/env python3
"""Run Broiler against a small, observational corpus of public web sites.

The live comparison is the quality gate.  A second Broiler capture replays the
Chromium-produced ``snapshot.html`` and is diagnostic only: it helps separate
network/script failures from layout/paint failures, but is not an end-to-end
test and cannot preserve canvas pixels or closed-shadow-root state.

This module deliberately keeps manifest validation and image analysis in pure
helpers.  Besides making the runner easier to test, that keeps the meaning of a
reported percentage independent of process execution and report formatting.
"""

from __future__ import annotations

import argparse
import base64
import copy
import datetime as dt
import hashlib
import html
import json
import math
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import sys
import time
from typing import Any, Mapping, Sequence
from urllib.parse import urlparse

import numpy as np
from PIL import Image, UnidentifiedImageError
import psutil


SCHEMA_VERSION = 1
DEFAULT_MANIFEST = "tests/real-world-sites/sites.json"
DEFAULT_OUTPUT_DIR = "artifacts/real-world-render-tests"
DEFAULT_CAPTURE_ATTEMPTS = 2
DEFAULT_MEMORY_LIMIT_MB = 1024
PROCESS_POLL_SECONDS = 0.1
LOG_EXCERPT_BYTES = 64 * 1024
QUANTIZATION_STEP = 16
EDGE_THRESHOLD = 24
EDGE_SPATIAL_TOLERANCE = 1
TILE_COLUMNS = 4
TILE_ROWS = 3

OBSERVATIONAL_NOTICE = (
    "The live-site suite is observational and non-deterministic. Public pages, "
    "experiments, consent flows, geolocation, advertisements, and network state "
    "can change without a Broiler code change."
)
REPLAY_NOTICE = (
    "Snapshot replay isolates more of the layout and painting path, but it is "
    "not an end-to-end live-site result and the serialized DOM cannot preserve "
    "canvas pixels or closed-shadow-root state."
)

DEFAULT_KEYS = {
    "viewport",
    "navigationTimeoutSeconds",
    "settleMilliseconds",
    "locale",
    "timezoneId",
    "pixelTolerance",
    "thresholds",
}
SITE_KEYS = {
    "id",
    "name",
    "url",
    "category",
    "description",
    "tags",
    "viewport",
    "navigationTimeoutSeconds",
    "settleMilliseconds",
    "locale",
    "timezoneId",
    "pixelTolerance",
    "thresholds",
}
OVERRIDE_KEYS = DEFAULT_KEYS
THRESHOLD_KEYS = {
    "pixelMatchPercent",
    "contentMatchPercent",
    "structureMatchPercent",
}
SITE_ID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
TAG_RE = SITE_ID_RE


class ManifestError(ValueError):
    """Raised when the real-world site manifest is not schema version 1."""


def utc_now() -> str:
    """Return a compact, JSON-friendly UTC timestamp."""

    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def _require_object(value: Any, location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ManifestError(f"{location} must be an object")
    return value


def _require_string(value: Any, location: str, minimum: int = 1) -> str:
    if not isinstance(value, str) or (minimum > 0 and len(value.strip()) < minimum):
        raise ManifestError(f"{location} must be a string of at least {minimum} character(s)")
    return value


def _require_integer(value: Any, location: str, minimum: int, maximum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or not minimum <= value <= maximum:
        raise ManifestError(f"{location} must be an integer in [{minimum}, {maximum}]")
    return value


def _require_percentage(value: Any, location: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ManifestError(f"{location} must be a number in [0, 100]")
    number = float(value)
    if not math.isfinite(number) or not 0 <= number <= 100:
        raise ManifestError(f"{location} must be a number in [0, 100]")
    return number


def validate_viewport(value: Any, location: str = "viewport") -> dict[str, int]:
    viewport = _require_object(value, location)
    unknown = set(viewport) - {"width", "height"}
    missing = {"width", "height"} - set(viewport)
    if unknown or missing:
        raise ManifestError(
            f"{location} must contain only width and height"
            + (f" (unknown: {', '.join(sorted(unknown))})" if unknown else "")
        )
    return {
        "width": _require_integer(viewport["width"], f"{location}.width", 320, 4096),
        "height": _require_integer(viewport["height"], f"{location}.height", 240, 4096),
    }


def validate_thresholds(value: Any, location: str = "thresholds", *, partial: bool = False) -> dict[str, float]:
    thresholds = _require_object(value, location)
    unknown = set(thresholds) - THRESHOLD_KEYS
    missing = THRESHOLD_KEYS - set(thresholds)
    if unknown:
        raise ManifestError(f"{location} has unknown field(s): {', '.join(sorted(unknown))}")
    if missing and not partial:
        raise ManifestError(f"{location} is missing: {', '.join(sorted(missing))}")
    if partial and not thresholds:
        raise ManifestError(f"{location} must override at least one threshold")
    return {key: _require_percentage(number, f"{location}.{key}") for key, number in thresholds.items()}


def _validate_config_fields(config: Mapping[str, Any], location: str, *, partial: bool) -> dict[str, Any]:
    unknown = set(config) - OVERRIDE_KEYS
    if unknown:
        raise ManifestError(f"{location} has unknown field(s): {', '.join(sorted(unknown))}")
    if not partial:
        missing = DEFAULT_KEYS - set(config)
        if missing:
            raise ManifestError(f"{location} is missing: {', '.join(sorted(missing))}")

    normalized: dict[str, Any] = {}
    if "viewport" in config:
        normalized["viewport"] = validate_viewport(config["viewport"], f"{location}.viewport")
    if "navigationTimeoutSeconds" in config:
        normalized["navigationTimeoutSeconds"] = _require_integer(
            config["navigationTimeoutSeconds"], f"{location}.navigationTimeoutSeconds", 1, 300
        )
    if "settleMilliseconds" in config:
        normalized["settleMilliseconds"] = _require_integer(
            config["settleMilliseconds"], f"{location}.settleMilliseconds", 0, 30_000
        )
    if "locale" in config:
        normalized["locale"] = _require_string(config["locale"], f"{location}.locale", 2)
    if "timezoneId" in config:
        normalized["timezoneId"] = _require_string(config["timezoneId"], f"{location}.timezoneId")
    if "pixelTolerance" in config:
        normalized["pixelTolerance"] = _require_integer(
            config["pixelTolerance"], f"{location}.pixelTolerance", 0, 255
        )
    if "thresholds" in config:
        normalized["thresholds"] = validate_thresholds(
            config["thresholds"], f"{location}.thresholds", partial=partial
        )
    return normalized


def validate_manifest(data: Any, source: str = "manifest") -> dict[str, Any]:
    """Validate and normalize the version-1 manifest without external schemas."""

    manifest = _require_object(data, source)
    allowed_top = {"$schema", "schemaVersion", "defaults", "sites"}
    unknown_top = set(manifest) - allowed_top
    if unknown_top:
        raise ManifestError(f"{source} has unknown field(s): {', '.join(sorted(unknown_top))}")
    if manifest.get("schemaVersion") != SCHEMA_VERSION:
        raise ManifestError(f"{source}.schemaVersion must be {SCHEMA_VERSION}")
    if "$schema" in manifest:
        _require_string(manifest["$schema"], f"{source}.$schema")
    if "defaults" not in manifest or "sites" not in manifest:
        raise ManifestError(f"{source} must contain defaults and sites")

    defaults = _validate_config_fields(_require_object(manifest["defaults"], f"{source}.defaults"), f"{source}.defaults", partial=False)
    sites_value = manifest["sites"]
    if not isinstance(sites_value, list) or not sites_value:
        raise ManifestError(f"{source}.sites must be a non-empty array")

    normalized_sites: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
    for index, raw_site in enumerate(sites_value):
        location = f"{source}.sites[{index}]"
        site = _require_object(raw_site, location)
        unknown = set(site) - SITE_KEYS
        if unknown:
            raise ManifestError(f"{location} has unknown field(s): {', '.join(sorted(unknown))}")
        missing = {"id", "name", "url", "category"} - set(site)
        if missing:
            raise ManifestError(f"{location} is missing: {', '.join(sorted(missing))}")
        site_id = _require_string(site["id"], f"{location}.id")
        if not SITE_ID_RE.fullmatch(site_id):
            raise ManifestError(f"{location}.id must be lower-kebab-case")
        if site_id in seen_ids:
            raise ManifestError(f"duplicate site id: {site_id}")
        seen_ids.add(site_id)
        name = _require_string(site["name"], f"{location}.name")
        url = _require_string(site["url"], f"{location}.url")
        parsed = urlparse(url)
        if parsed.scheme != "https" or not parsed.netloc:
            raise ManifestError(f"{location}.url must be an absolute HTTPS URL")
        category = site["category"]
        if category not in {"stable", "dynamic"}:
            raise ManifestError(f"{location}.category must be stable or dynamic")

        normalized_site: dict[str, Any] = {"id": site_id, "name": name, "url": url, "category": category}
        if "description" in site:
            normalized_site["description"] = _require_string(site["description"], f"{location}.description", 0)
        if "tags" in site:
            tags = site["tags"]
            if not isinstance(tags, list) or any(not isinstance(tag, str) or not TAG_RE.fullmatch(tag) for tag in tags):
                raise ManifestError(f"{location}.tags must contain lower-kebab-case strings")
            if len(tags) != len(set(tags)):
                raise ManifestError(f"{location}.tags must be unique")
            normalized_site["tags"] = list(tags)

        direct_overrides = {key: site[key] for key in DEFAULT_KEYS if key in site}
        normalized_site.update(_validate_config_fields(direct_overrides, location, partial=True))
        normalized_sites.append(normalized_site)

    normalized_manifest = {"schemaVersion": SCHEMA_VERSION, "defaults": defaults, "sites": normalized_sites}
    if "$schema" in manifest:
        normalized_manifest["$schema"] = manifest["$schema"]
    return normalized_manifest


def load_manifest(path: Path) -> dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8") as stream:
            return validate_manifest(json.load(stream), str(path))
    except FileNotFoundError as exc:
        raise ManifestError(f"manifest not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ManifestError(f"invalid JSON in {path}: {exc}") from exc


def parse_viewport(value: str) -> tuple[int, int]:
    match = re.fullmatch(r"\s*(\d+)\s*[xX\u00d7]\s*(\d+)\s*", value)
    if not match:
        raise argparse.ArgumentTypeError("viewport must be WIDTHxHEIGHT (for example 1024x768)")
    width, height = int(match.group(1)), int(match.group(2))
    try:
        viewport = validate_viewport({"width": width, "height": height}, "--viewport")
    except ManifestError as exc:
        raise argparse.ArgumentTypeError(str(exc)) from exc
    return viewport["width"], viewport["height"]


def parse_site_selection(values: Sequence[str] | None) -> list[str]:
    """Expand repeatable and comma-separated ``--sites`` values, preserving order."""

    selected: list[str] = []
    seen: set[str] = set()
    for value in values or []:
        for item in value.split(","):
            site_id = item.strip()
            if site_id and site_id not in seen:
                selected.append(site_id)
                seen.add(site_id)
    return selected


def resolve_effective_config(
    defaults: Mapping[str, Any],
    site: Mapping[str, Any],
    *,
    viewport: tuple[int, int] | None = None,
    pixel_tolerance: int | None = None,
    pixel_match_percent: float | None = None,
    content_match_percent: float | None = None,
    structure_match_percent: float | None = None,
) -> dict[str, Any]:
    """Merge defaults, per-site values, and command-line values."""

    result = copy.deepcopy(dict(defaults))
    for key in DEFAULT_KEYS - {"thresholds"}:
        if key in site:
            result[key] = copy.deepcopy(site[key])
    result["thresholds"] = {**defaults["thresholds"], **site.get("thresholds", {})}
    if viewport is not None:
        result["viewport"] = {"width": viewport[0], "height": viewport[1]}
    if pixel_tolerance is not None:
        result["pixelTolerance"] = pixel_tolerance
    cli_thresholds = {
        "pixelMatchPercent": pixel_match_percent,
        "contentMatchPercent": content_match_percent,
        "structureMatchPercent": structure_match_percent,
    }
    for key, value in cli_thresholds.items():
        if value is not None:
            result["thresholds"][key] = value
    return result


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgba_image(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGBA"), dtype=np.uint8)


def image_artifact(path: Path, output_root: Path) -> dict[str, Any]:
    artifact: dict[str, Any] = {
        "available": False,
        "path": path.relative_to(output_root).as_posix(),
    }
    if not path.is_file():
        artifact["error"] = "file is missing"
        return artifact
    try:
        with Image.open(path) as image:
            image.verify()
        with Image.open(path) as image:
            width, height = image.size
            image_format = image.format
        artifact.update(
            available=True,
            bytes=path.stat().st_size,
            sha256=sha256_file(path),
            dimensions={"width": width, "height": height},
            format=image_format,
        )
    except (OSError, ValueError, UnidentifiedImageError) as exc:
        artifact["error"] = f"invalid image: {exc}"
    return artifact


def dominant_quantized_background(rgb: np.ndarray, step: int = QUANTIZATION_STEP) -> dict[str, Any]:
    """Find the modal RGB bucket and return its mask and portable metadata."""

    if rgb.ndim != 3 or rgb.shape[2] < 3:
        raise ValueError("expected an HxWxRGB(A) image")
    pixels = rgb[..., :3].astype(np.uint16, copy=False)
    if not 1 <= step <= 256:
        raise ValueError("quantization step must be in [1, 256]")
    quantized = (pixels // step).astype(np.uint32)
    bins_per_channel = (256 + step - 1) // step
    codes = (quantized[..., 0] * bins_per_channel + quantized[..., 1]) * bins_per_channel + quantized[..., 2]
    counts = np.bincount(codes.reshape(-1).astype(np.int64), minlength=bins_per_channel**3)
    code = int(np.argmax(counts))
    blue_bin = code % bins_per_channel
    code //= bins_per_channel
    green_bin = code % bins_per_channel
    red_bin = code // bins_per_channel
    bucket = np.array([red_bin, green_bin, blue_bin], dtype=np.uint16)
    mask = np.all(quantized == bucket, axis=2)
    representative = np.minimum(bucket * step + step // 2, 255).astype(int).tolist()
    total = int(mask.size)
    count = int(np.count_nonzero(mask))
    return {
        "mask": mask,
        "rgb": representative,
        "bucket": bucket.astype(int).tolist(),
        "pixelCount": count,
        "coveragePercent": round(count * 100.0 / total, 4) if total else 100.0,
        "quantizationStep": step,
    }


def compute_match_mask(actual: np.ndarray, reference: np.ndarray, tolerance: int) -> np.ndarray:
    """WPT-style match: every RGBA channel must be within the tolerance."""

    if actual.shape != reference.shape:
        raise ValueError("images must have identical dimensions")
    delta = np.abs(actual.astype(np.int16) - reference.astype(np.int16))
    return np.all(delta <= tolerance, axis=2)


def dilate_mask(mask: np.ndarray, radius: int = 1) -> np.ndarray:
    if radius <= 0:
        return mask.copy()
    height, width = mask.shape
    padded = np.pad(mask, radius, mode="constant", constant_values=False)
    result = np.zeros_like(mask, dtype=bool)
    for y_offset in range(2 * radius + 1):
        for x_offset in range(2 * radius + 1):
            result |= padded[y_offset : y_offset + height, x_offset : x_offset + width]
    return result


def edge_mask(image: np.ndarray, threshold: int = EDGE_THRESHOLD) -> np.ndarray:
    rgb = image[..., :3].astype(np.int16, copy=False)
    edges = np.zeros(rgb.shape[:2], dtype=bool)
    horizontal = np.max(np.abs(rgb[:, 1:] - rgb[:, :-1]), axis=2) >= threshold
    vertical = np.max(np.abs(rgb[1:, :] - rgb[:-1, :]), axis=2) >= threshold
    edges[:, 1:] |= horizontal
    edges[:, :-1] |= horizontal
    edges[1:, :] |= vertical
    edges[:-1, :] |= vertical
    return edges


def compute_structure_metrics(
    actual: np.ndarray,
    reference: np.ndarray,
    *,
    edge_threshold: int = EDGE_THRESHOLD,
    spatial_tolerance: int = EDGE_SPATIAL_TOLERANCE,
) -> dict[str, Any]:
    actual_edges = edge_mask(actual, edge_threshold)
    reference_edges = edge_mask(reference, edge_threshold)
    actual_count = int(np.count_nonzero(actual_edges))
    reference_count = int(np.count_nonzero(reference_edges))
    if actual_count == 0 and reference_count == 0:
        precision = recall = f1 = 1.0
        matched_actual = matched_reference = 0
    elif actual_count == 0 or reference_count == 0:
        precision = 0.0 if actual_count else 1.0
        recall = 0.0 if reference_count else 1.0
        f1 = 0.0
        matched_actual = matched_reference = 0
    else:
        matched_actual = int(np.count_nonzero(actual_edges & dilate_mask(reference_edges, spatial_tolerance)))
        matched_reference = int(np.count_nonzero(reference_edges & dilate_mask(actual_edges, spatial_tolerance)))
        precision = matched_actual / actual_count
        recall = matched_reference / reference_count
        f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
    return {
        "edgeThreshold": edge_threshold,
        "spatialTolerancePixels": spatial_tolerance,
        "actualEdgePixels": actual_count,
        "referenceEdgePixels": reference_count,
        "matchedActualEdgePixels": matched_actual,
        "matchedReferenceEdgePixels": matched_reference,
        "precisionPercent": round(precision * 100, 4),
        "recallPercent": round(recall * 100, 4),
        "f1Percent": round(f1 * 100, 4),
    }


def mismatch_bounding_box(match: np.ndarray) -> dict[str, int] | None:
    ys, xs = np.nonzero(~match)
    if not len(xs):
        return None
    left, right = int(xs.min()), int(xs.max())
    top, bottom = int(ys.min()), int(ys.max())
    return {
        "left": left,
        "top": top,
        "right": right,
        "bottom": bottom,
        "width": right - left + 1,
        "height": bottom - top + 1,
    }


def compute_worst_tiles(
    match: np.ndarray,
    absolute_delta: np.ndarray,
    content: np.ndarray,
    columns: int = TILE_COLUMNS,
    rows: int = TILE_ROWS,
) -> list[dict[str, Any]]:
    height, width = match.shape
    tiles: list[dict[str, Any]] = []
    for row in range(rows):
        top, bottom = height * row // rows, height * (row + 1) // rows
        for column in range(columns):
            left, right = width * column // columns, width * (column + 1) // columns
            tile_match = match[top:bottom, left:right]
            tile_delta = absolute_delta[top:bottom, left:right]
            tile_content = content[top:bottom, left:right]
            total = int(tile_match.size)
            matching = int(np.count_nonzero(tile_match))
            content_total = int(np.count_nonzero(tile_content))
            content_matching = int(np.count_nonzero(tile_match & tile_content))
            tiles.append(
                {
                    "row": row,
                    "column": column,
                    "bounds": {"left": left, "top": top, "right": right - 1, "bottom": bottom - 1},
                    "pixelMatchPercent": round(matching * 100.0 / total, 4) if total else 100.0,
                    "mismatchedPixels": total - matching,
                    "contentMatchPercent": round(content_matching * 100.0 / content_total, 4) if content_total else 100.0,
                    "meanAbsoluteError": round(float(np.mean(tile_delta)), 4) if tile_delta.size else 0.0,
                }
            )
    return sorted(tiles, key=lambda tile: (-tile["mismatchedPixels"], -tile["meanAbsoluteError"], tile["row"], tile["column"]))


def create_diff_image(match: np.ndarray, content: np.ndarray, reference: np.ndarray) -> Image.Image:
    """Create a contextual diff: content mismatches red, background mismatches yellow."""

    muted = np.mean(reference[..., :3], axis=2, keepdims=True) * 0.22
    output = np.repeat(muted, 3, axis=2).astype(np.uint8)
    output[~match & content] = [230, 40, 40]
    output[~match & ~content] = [235, 205, 35]
    return Image.fromarray(output, mode="RGB")


def _portable_background(background: Mapping[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in background.items() if key != "mask"}


def compare_image_arrays(
    actual: np.ndarray,
    reference: np.ndarray,
    *,
    tolerance: int,
    thresholds: Mapping[str, float],
    diff_path: Path | None = None,
) -> dict[str, Any]:
    """Compare two RGBA arrays and optionally write the red/yellow diff PNG."""

    actual_dimensions = {"width": int(actual.shape[1]), "height": int(actual.shape[0])}
    reference_dimensions = {"width": int(reference.shape[1]), "height": int(reference.shape[0])}
    size_matches = actual.shape == reference.shape
    if not size_matches:
        height = max(actual.shape[0], reference.shape[0])
        width = max(actual.shape[1], reference.shape[1])
        actual_canvas = np.full((height, width, 4), 255, dtype=np.uint8)
        reference_canvas = np.full((height, width, 4), 255, dtype=np.uint8)
        actual_canvas[: actual.shape[0], : actual.shape[1]] = actual
        reference_canvas[: reference.shape[0], : reference.shape[1]] = reference
        absolute_delta = np.abs(actual_canvas.astype(np.int16) - reference_canvas.astype(np.int16))
        match = np.zeros((height, width), dtype=bool)
        actual_background = dominant_quantized_background(actual_canvas)
        reference_background = dominant_quantized_background(reference_canvas)
        actual_content = ~actual_background["mask"]
        reference_content = ~reference_background["mask"]
        content = actual_content | reference_content
        structure = compute_structure_metrics(actual_canvas, reference_canvas)
        if diff_path is not None:
            diff_path.parent.mkdir(parents=True, exist_ok=True)
            create_diff_image(match, content, reference_canvas).save(diff_path)
        result = {
            "available": True,
            "sizeMatches": False,
            "actualDimensions": actual_dimensions,
            "referenceDimensions": reference_dimensions,
            "pixelTolerance": tolerance,
            "totalPixels": int(match.size),
            "matchingPixels": 0,
            "mismatchedPixels": int(match.size),
            "pixelMatchPercent": 0.0,
            "contentMatchPercent": 0.0,
            "structureMatchPercent": 0.0,
            "contentCoverage": {
                "actualPercent": round(np.count_nonzero(actual_content) * 100.0 / match.size, 4),
                "referencePercent": round(np.count_nonzero(reference_content) * 100.0 / match.size, 4),
                "unionPercent": round(np.count_nonzero(content) * 100.0 / match.size, 4),
            },
            "dominantBackground": {
                "actual": _portable_background(actual_background),
                "reference": _portable_background(reference_background),
            },
            "structure": structure,
            "meanAbsoluteError": round(float(np.mean(absolute_delta)), 4),
            "rootMeanSquareError": round(float(np.sqrt(np.mean(np.square(absolute_delta.astype(np.float64))))), 4),
            "mismatchBoundingBox": {
                "left": 0,
                "top": 0,
                "right": max(actual.shape[1], reference.shape[1]) - 1,
                "bottom": max(actual.shape[0], reference.shape[0]) - 1,
                "width": max(actual.shape[1], reference.shape[1]),
                "height": max(actual.shape[0], reference.shape[0]),
            },
            "worstTiles": compute_worst_tiles(match, absolute_delta, content),
        }
        result["quality"] = evaluate_quality(result, thresholds)
        return result

    match = compute_match_mask(actual, reference, tolerance)
    absolute_delta = np.abs(actual.astype(np.int16) - reference.astype(np.int16))
    total = int(match.size)
    matching = int(np.count_nonzero(match))
    actual_background = dominant_quantized_background(actual)
    reference_background = dominant_quantized_background(reference)
    actual_content = ~actual_background["mask"]
    reference_content = ~reference_background["mask"]
    content = actual_content | reference_content
    content_total = int(np.count_nonzero(content))
    content_matching = int(np.count_nonzero(match & content))
    structure = compute_structure_metrics(actual, reference)

    if diff_path is not None:
        diff_path.parent.mkdir(parents=True, exist_ok=True)
        create_diff_image(match, content, reference).save(diff_path)

    result = {
        "available": True,
        "sizeMatches": True,
        "actualDimensions": actual_dimensions,
        "referenceDimensions": reference_dimensions,
        "pixelTolerance": tolerance,
        "totalPixels": total,
        "matchingPixels": matching,
        "mismatchedPixels": total - matching,
        "pixelMatchPercent": round(matching * 100.0 / total, 4) if total else 100.0,
        "contentPixels": content_total,
        "matchingContentPixels": content_matching,
        "contentMatchPercent": round(content_matching * 100.0 / content_total, 4) if content_total else 100.0,
        "contentCoverage": {
            "actualPercent": round(np.count_nonzero(actual_content) * 100.0 / total, 4) if total else 0.0,
            "referencePercent": round(np.count_nonzero(reference_content) * 100.0 / total, 4) if total else 0.0,
            "unionPercent": round(content_total * 100.0 / total, 4) if total else 0.0,
        },
        "dominantBackground": {
            "actual": _portable_background(actual_background),
            "reference": _portable_background(reference_background),
        },
        "structure": structure,
        "structureMatchPercent": structure["f1Percent"],
        "meanAbsoluteError": round(float(np.mean(absolute_delta)), 4) if absolute_delta.size else 0.0,
        "rootMeanSquareError": round(float(np.sqrt(np.mean(np.square(absolute_delta.astype(np.float64))))), 4)
        if absolute_delta.size
        else 0.0,
        "mismatchBoundingBox": mismatch_bounding_box(match),
        "worstTiles": compute_worst_tiles(match, absolute_delta, content),
    }
    result["quality"] = evaluate_quality(result, thresholds)
    return result


def evaluate_quality(comparison: Mapping[str, Any], thresholds: Mapping[str, float]) -> dict[str, Any]:
    metric_names = {
        "pixelMatchPercent": "pixelMatchPercent",
        "contentMatchPercent": "contentMatchPercent",
        "structureMatchPercent": "structureMatchPercent",
    }
    gates: dict[str, Any] = {}
    for metric, threshold_key in metric_names.items():
        actual = comparison.get(metric)
        minimum = float(thresholds[threshold_key])
        gates[metric] = {
            "actual": actual,
            "minimum": minimum,
            "passed": actual is not None and float(actual) >= minimum,
        }
    return {"passed": bool(comparison.get("sizeMatches", True)) and all(gate["passed"] for gate in gates.values()), "gates": gates}


def compare_image_files(
    actual_path: Path,
    reference_path: Path,
    *,
    tolerance: int,
    thresholds: Mapping[str, float],
    diff_path: Path,
    output_root: Path,
    reference_usable: bool = True,
    reference_error: str | None = None,
) -> dict[str, Any]:
    try:
        diff_path.unlink()
    except FileNotFoundError:
        pass
    actual_artifact = image_artifact(actual_path, output_root)
    reference_artifact = image_artifact(reference_path, output_root)
    if not reference_usable:
        return {
            "available": False,
            "error": reference_error or "reference capture was not completed",
            "actual": actual_artifact,
            "reference": reference_artifact,
        }
    if not actual_artifact["available"] or not reference_artifact["available"]:
        return {
            "available": False,
            "error": "actual image unavailable" if not actual_artifact["available"] else "reference image unavailable",
            "actual": actual_artifact,
            "reference": reference_artifact,
        }
    try:
        comparison = compare_image_arrays(
            load_rgba_image(actual_path),
            load_rgba_image(reference_path),
            tolerance=tolerance,
            thresholds=thresholds,
            diff_path=diff_path,
        )
    except (OSError, ValueError, UnidentifiedImageError) as exc:
        return {"available": False, "error": f"comparison failed: {exc}", "actual": actual_artifact, "reference": reference_artifact}
    comparison.update(
        actual={**actual_artifact, "sha256": actual_artifact.get("sha256")},
        reference={**reference_artifact, "sha256": reference_artifact.get("sha256")},
        diffPath=diff_path.relative_to(output_root).as_posix() if diff_path.is_file() else None,
    )
    return comparison


def _diagnostic_text(capture: Mapping[str, Any] | None) -> str:
    if not capture:
        return ""
    pieces: list[str] = []
    for attempt in capture.get("attempts", []):
        pieces.extend(str(attempt.get(key, "")) for key in ("stdout", "stderr", "error"))
    return "\n".join(pieces).lower()


def classify_result(
    live_comparison: Mapping[str, Any] | None,
    replay_comparison: Mapping[str, Any] | None = None,
    live_capture: Mapping[str, Any] | None = None,
) -> str | None:
    """Classify a failed/incomplete live result into an actionable broad bucket."""

    if not live_comparison or not live_comparison.get("available"):
        return "CaptureUnavailable"
    if live_comparison.get("quality", {}).get("passed"):
        return None

    live_pixel = float(live_comparison.get("pixelMatchPercent", 0))
    live_content = float(live_comparison.get("contentMatchPercent", 0))
    live_structure = float(live_comparison.get("structureMatchPercent", 0))
    gates = live_comparison.get("quality", {}).get("gates", {})
    pixel_gate_passed = bool(gates.get("pixelMatchPercent", {}).get("passed"))
    coverage = live_comparison.get("contentCoverage", {})
    actual_coverage = float(coverage.get("actualPercent", 0))
    reference_coverage = float(coverage.get("referencePercent", 0))

    if pixel_gate_passed and (
        not gates.get("contentMatchPercent", {}).get("passed")
        or not gates.get("structureMatchPercent", {}).get("passed")
    ):
        return "BackgroundDominated"
    if reference_coverage - actual_coverage >= 2.0 and actual_coverage < max(1.0, reference_coverage * 0.55):
        return "MissingContent"

    if replay_comparison and replay_comparison.get("available"):
        replay_quality = replay_comparison.get("quality", {}).get("passed")
        replay_content = float(replay_comparison.get("contentMatchPercent", 0))
        replay_structure = float(replay_comparison.get("structureMatchPercent", 0))
        replay_pixel = float(replay_comparison.get("pixelMatchPercent", 0))
        if replay_quality or (
            replay_pixel >= live_pixel + 15
            or replay_content >= live_content + 20
            or replay_structure >= live_structure + 20
        ):
            return "InputLoadingOrScripting"

    diagnostic = _diagnostic_text(live_capture)
    input_terms = (
        "timeout",
        "timed out",
        "httprequest",
        "network",
        "fetch",
        "dns",
        "certificate",
        "tls",
        "javascript",
        "script error",
        "navigation",
        "connection",
    )
    if any(term in diagnostic for term in input_terms):
        return "InputLoadingOrScripting"
    return "LayoutOrPainting"


def _command_result(command: Sequence[str], *, cwd: Path, env: Mapping[str, str] | None = None, timeout: float | None = None) -> dict[str, Any]:
    started_at = utc_now()
    start = time.monotonic()
    process: subprocess.Popen[str] | None = None
    try:
        process = subprocess.Popen(
            list(command),
            cwd=str(cwd),
            env=dict(env) if env is not None else None,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        try:
            stdout, stderr = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            _terminate_process_tree(process)
            try:
                stdout, stderr = process.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                stdout, stderr = process.communicate()
            return {
                "command": list(command),
                "commandLine": shlex.join(command),
                "startedAt": started_at,
                "finishedAt": utc_now(),
                "durationMilliseconds": round((time.monotonic() - start) * 1000, 1),
                "exitCode": None,
                "timedOut": True,
                "stdout": stdout or "",
                "stderr": stderr or "",
                "error": f"hard timeout after {timeout:g} seconds" if timeout is not None else "hard timeout",
            }
        return {
            "command": list(command),
            "commandLine": shlex.join(command),
            "startedAt": started_at,
            "finishedAt": utc_now(),
            "durationMilliseconds": round((time.monotonic() - start) * 1000, 1),
            "exitCode": process.returncode,
            "timedOut": False,
            "stdout": stdout,
            "stderr": stderr,
        }
    except OSError as exc:
        if process is not None and process.poll() is None:
            _terminate_process_tree(process)
        return {
            "command": list(command),
            "commandLine": shlex.join(command),
            "startedAt": started_at,
            "finishedAt": utc_now(),
            "durationMilliseconds": round((time.monotonic() - start) * 1000, 1),
            "exitCode": None,
            "timedOut": False,
            "stdout": "",
            "stderr": "",
            "error": str(exc),
        }


def _command_text(command: Sequence[str], cwd: Path) -> str | None:
    result = _command_result(command, cwd=cwd, timeout=15)
    if result.get("exitCode") != 0:
        return None
    text = str(result.get("stdout") or result.get("stderr") or "").strip()
    return text or None


def collect_run_environment(repo_root: Path, node: str, dotnet: str) -> dict[str, Any]:
    git = shutil.which("git") or "git"
    revision = os.environ.get("GITHUB_SHA") or _command_text([git, "rev-parse", "HEAD"], repo_root)
    tracked_status = _command_text([git, "status", "--porcelain", "--untracked-files=no"], repo_root)
    github_fields = {
        "runId": os.environ.get("GITHUB_RUN_ID"),
        "runAttempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
        "workflow": os.environ.get("GITHUB_WORKFLOW"),
        "ref": os.environ.get("GITHUB_REF"),
        "runnerOs": os.environ.get("RUNNER_OS"),
        "runnerArch": os.environ.get("RUNNER_ARCH"),
    }
    return {
        "repositoryRevision": revision,
        "trackedWorkingTreeDirty": bool(tracked_status),
        "submodules": (_command_text([git, "submodule", "status", "--recursive"], repo_root) or "").splitlines(),
        "os": platform.platform(),
        "machine": platform.machine(),
        "pythonVersion": platform.python_version(),
        "nodeVersion": _command_text([node, "--version"], repo_root),
        "dotnetSdkVersion": _command_text([dotnet, "--version"], repo_root),
        "githubActions": {key: value for key, value in github_fields.items() if value},
    }


def _terminate_process_tree(process: subprocess.Popen[bytes]) -> None:
    """Best-effort termination of a capture and every child it created."""

    try:
        descendants = psutil.Process(process.pid).children(recursive=True)
    except (psutil.Error, OSError):
        descendants = []
    for target in reversed(descendants):
        try:
            target.terminate()
        except (psutil.Error, OSError):
            pass
    if process.poll() is None:
        try:
            process.terminate()
        except OSError:
            pass
    _, alive = psutil.wait_procs(descendants, timeout=2)
    for target in alive:
        try:
            target.kill()
        except (psutil.Error, OSError):
            pass
    if process.poll() is None:
        try:
            process.kill()
        except OSError:
            pass


def _process_tree_rss_bytes(pid: int) -> int:
    try:
        parent = psutil.Process(pid)
        processes = [parent, *parent.children(recursive=True)]
    except (psutil.Error, OSError):
        return 0
    total = 0
    for process in processes:
        try:
            total += int(process.memory_info().rss)
        except (psutil.Error, OSError):
            continue
    return total


def _read_log_excerpt(path: Path, limit: int = LOG_EXCERPT_BYTES) -> tuple[str, int, bool]:
    try:
        size = path.stat().st_size
        with path.open("rb") as stream:
            if size <= limit:
                data = stream.read()
                truncated = False
            else:
                half = limit // 2
                beginning = stream.read(half)
                stream.seek(max(0, size - half))
                ending = stream.read(half)
                data = beginning + b"\n... log excerpt truncated ...\n" + ending
                truncated = True
        return data.decode("utf-8", "replace"), size, truncated
    except OSError as exc:
        return f"Could not read log: {exc}", 0, False


def _monitored_command_result(
    command: Sequence[str],
    *,
    cwd: Path,
    stdout_path: Path,
    stderr_path: Path,
    timeout: float,
    memory_limit_mb: int,
) -> dict[str, Any]:
    """Run one untrusted render with wall-clock and process-tree RSS limits."""

    started_at = utc_now()
    start = time.monotonic()
    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    stderr_path.parent.mkdir(parents=True, exist_ok=True)
    timed_out = False
    memory_limit_exceeded = False
    peak_rss_bytes = 0
    error: str | None = None
    process: subprocess.Popen[bytes] | None = None
    try:
        with stdout_path.open("wb") as stdout_stream, stderr_path.open("wb") as stderr_stream:
            process = subprocess.Popen(
                list(command),
                cwd=str(cwd),
                stdout=stdout_stream,
                stderr=stderr_stream,
            )
            while process.poll() is None:
                elapsed = time.monotonic() - start
                rss_bytes = _process_tree_rss_bytes(process.pid)
                peak_rss_bytes = max(peak_rss_bytes, rss_bytes)
                if memory_limit_mb > 0 and rss_bytes > memory_limit_mb * 1024 * 1024:
                    memory_limit_exceeded = True
                    error = (
                        f"process-tree RSS exceeded {memory_limit_mb} MiB "
                        f"(observed {rss_bytes / (1024 * 1024):.1f} MiB)"
                    )
                    _terminate_process_tree(process)
                    break
                if elapsed >= timeout:
                    timed_out = True
                    error = f"hard timeout after {timeout:g} seconds"
                    _terminate_process_tree(process)
                    break
                time.sleep(PROCESS_POLL_SECONDS)
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                _terminate_process_tree(process)
                process.wait(timeout=5)
    except OSError as exc:
        error = str(exc)
    stdout, stdout_bytes, stdout_truncated = _read_log_excerpt(stdout_path)
    stderr, stderr_bytes, stderr_truncated = _read_log_excerpt(stderr_path)
    return {
        "command": list(command),
        "commandLine": shlex.join(command),
        "startedAt": started_at,
        "finishedAt": utc_now(),
        "durationMilliseconds": round((time.monotonic() - start) * 1000, 1),
        "exitCode": process.returncode if process is not None else None,
        "timedOut": timed_out,
        "memoryLimitMb": memory_limit_mb,
        "memoryLimitExceeded": memory_limit_exceeded,
        "peakRssBytes": peak_rss_bytes,
        "peakRssMiB": round(peak_rss_bytes / (1024 * 1024), 2),
        "stdout": stdout,
        "stderr": stderr,
        "stdoutBytes": stdout_bytes,
        "stderrBytes": stderr_bytes,
        "stdoutTruncatedInJson": stdout_truncated,
        "stderrTruncatedInJson": stderr_truncated,
        **({"error": error} if error else {}),
    }


def _remove_capture_artifacts(output_path: Path) -> None:
    """Remove exactly one capture and sidecar, so stale output is never accepted."""

    for path in (output_path, Path(f"{output_path}.metadata.json")):
        try:
            path.unlink()
        except FileNotFoundError:
            pass


def _remove_reference_artifacts(site_dir: Path) -> None:
    """Remove only the three Chromium outputs for one selected manifest case."""

    for name in ("reference.png", "snapshot.html", "reference.json"):
        try:
            (site_dir / name).unlink()
        except FileNotFoundError:
            pass


def _read_json_if_present(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as stream:
            return json.load(stream)
    except (OSError, json.JSONDecodeError):
        return None


def run_broiler_capture(
    dotnet: str,
    cli_dll: Path,
    source: str,
    output_path: Path,
    effective_config: Mapping[str, Any],
    attempts: int,
    repo_root: Path,
    output_root: Path,
    memory_limit_mb: int = DEFAULT_MEMORY_LIMIT_MB,
) -> dict[str, Any]:
    """Capture one URL in fresh child processes with a hard parent deadline."""

    output_path.parent.mkdir(parents=True, exist_ok=True)
    viewport = effective_config["viewport"]
    navigation_timeout = int(effective_config["navigationTimeoutSeconds"])
    hard_timeout = navigation_timeout + math.ceil(int(effective_config["settleMilliseconds"]) / 1000) + 30
    attempt_results: list[dict[str, Any]] = []
    succeeded = False

    for attempt_number in range(1, attempts + 1):
        _remove_capture_artifacts(output_path)
        command = [
            dotnet,
            str(cli_dll),
            "--capture-image",
            source,
            "--output",
            str(output_path),
            "--width",
            str(viewport["width"]),
            "--height",
            str(viewport["height"]),
            "--timeout",
            str(navigation_timeout),
            "--diagnostics",
        ]
        log_prefix = output_path.with_suffix("")
        stdout_path = Path(f"{log_prefix}-attempt-{attempt_number}.stdout.log")
        stderr_path = Path(f"{log_prefix}-attempt-{attempt_number}.stderr.log")
        result = _monitored_command_result(
            command,
            cwd=repo_root,
            stdout_path=stdout_path,
            stderr_path=stderr_path,
            timeout=hard_timeout,
            memory_limit_mb=memory_limit_mb,
        )
        result["stdoutPath"] = stdout_path.relative_to(output_root).as_posix()
        result["stderrPath"] = stderr_path.relative_to(output_root).as_posix()
        artifact = image_artifact(output_path, output_root)
        expected_dimensions = viewport
        dimensions_match = artifact.get("dimensions") == expected_dimensions
        accepted = (
            result.get("exitCode") == 0
            and not result.get("timedOut")
            and not result.get("memoryLimitExceeded")
            and not result.get("error")
            and artifact.get("available")
            and dimensions_match
        )
        result.update(
            attempt=attempt_number,
            hardTimeoutSeconds=hard_timeout,
            output=artifact,
            dimensionsMatch=dimensions_match,
            accepted=bool(accepted),
        )
        sidecar_path = Path(f"{output_path}.metadata.json")
        sidecar = _read_json_if_present(sidecar_path)
        if sidecar is not None:
            result["imageMetadata"] = sidecar
            result["metadataPath"] = sidecar_path.relative_to(output_root).as_posix()
        attempt_results.append(result)
        if accepted:
            succeeded = True
            break
        # A failed attempt may have left a partial PNG.  Remove it now as well
        # as before the next attempt, so reports can never mistake it for output.
        _remove_capture_artifacts(output_path)

    return {
        "status": "success" if succeeded else "failed",
        "source": source,
        "path": output_path.relative_to(output_root).as_posix(),
        "attemptCount": len(attempt_results),
        "attempts": attempt_results,
        "artifact": image_artifact(output_path, output_root),
    }


def locate_cli_dll(repo_root: Path, configuration: str) -> Path | None:
    expected = repo_root / "src" / "Broiler.Cli" / "bin" / configuration / "net10.0" / "Broiler.Cli.dll"
    if expected.is_file():
        return expected
    candidates = sorted(
        (repo_root / "src" / "Broiler.Cli" / "bin" / configuration).glob("**/Broiler.Cli.dll"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    return candidates[0] if candidates else None


def _reference_capture(
    site_dir: Path,
    output_root: Path,
    site: Mapping[str, Any],
    effective_config: Mapping[str, Any],
) -> dict[str, Any]:
    reference_path = site_dir / "reference.png"
    snapshot_path = site_dir / "snapshot.html"
    metadata_path = site_dir / "reference.json"
    metadata = _read_json_if_present(metadata_path)
    metadata_status = metadata.get("status") if isinstance(metadata, dict) else None
    reference = image_artifact(reference_path, output_root)
    snapshot: dict[str, Any] = {
        "available": snapshot_path.is_file(),
        "path": snapshot_path.relative_to(output_root).as_posix(),
    }
    if snapshot_path.is_file():
        snapshot.update(bytes=snapshot_path.stat().st_size, sha256=sha256_file(snapshot_path))
    else:
        snapshot["error"] = "file is missing"
    expected_reference_hash = metadata.get("referenceImageSha256") if isinstance(metadata, dict) else None
    expected_snapshot_hash = metadata.get("snapshotSha256") if isinstance(metadata, dict) else None
    expected_capture_config = {
        "requestedUrl": site["url"],
        "viewport": effective_config["viewport"],
        "locale": effective_config["locale"],
        "timezoneId": effective_config["timezoneId"],
        "navigationTimeoutSeconds": effective_config["navigationTimeoutSeconds"],
        "settleMilliseconds": effective_config["settleMilliseconds"],
    }
    capture_config_mismatches: list[str] = []
    if isinstance(metadata, dict):
        for key, expected in expected_capture_config.items():
            if metadata.get(key) != expected:
                capture_config_mismatches.append(key)
    else:
        capture_config_mismatches.extend(expected_capture_config)
    capture_config_matches = not capture_config_mismatches
    reference_hash_matches = bool(
        expected_reference_hash
        and reference.get("sha256")
        and expected_reference_hash == reference.get("sha256")
    )
    snapshot_hash_matches = bool(
        expected_snapshot_hash
        and snapshot.get("sha256")
        and expected_snapshot_hash == snapshot.get("sha256")
    )
    reference["expectedSha256"] = expected_reference_hash
    reference["hashMatchesMetadata"] = reference_hash_matches
    snapshot["expectedSha256"] = expected_snapshot_hash
    snapshot["hashMatchesMetadata"] = snapshot_hash_matches
    usable = (
        metadata_status == "captured"
        and capture_config_matches
        and bool(reference.get("available"))
        and reference_hash_matches
    )
    snapshot_usable = (
        metadata_status == "captured"
        and capture_config_matches
        and bool(snapshot.get("available"))
        and snapshot_hash_matches
    )
    error = None
    if not usable:
        if isinstance(metadata, dict) and metadata.get("error"):
            error = str(metadata["error"])
        elif metadata_status and metadata_status != "captured":
            error = f"reference capture status is {metadata_status}"
        elif metadata is None:
            error = "reference metadata is missing or invalid"
        elif not capture_config_matches:
            error = "reference capture configuration does not match: " + ", ".join(capture_config_mismatches)
        elif not reference.get("available"):
            error = str(reference.get("error") or "reference image is unavailable")
        elif not expected_reference_hash:
            error = "reference metadata does not contain referenceImageSha256"
        elif not reference_hash_matches:
            error = "reference image SHA-256 does not match reference metadata"
    if not snapshot_usable:
        if not snapshot.get("available"):
            snapshot["error"] = snapshot.get("error") or "snapshot file is missing"
        elif not capture_config_matches:
            snapshot["error"] = "reference capture configuration does not match: " + ", ".join(
                capture_config_mismatches
            )
        elif not expected_snapshot_hash:
            snapshot["error"] = "reference metadata does not contain snapshotSha256"
        elif not snapshot_hash_matches:
            snapshot["error"] = "snapshot SHA-256 does not match reference metadata"
    return {
        "status": metadata_status or "missing",
        "usable": usable,
        "snapshotUsable": snapshot_usable,
        "captureConfigMatches": capture_config_matches,
        "captureConfigMismatches": capture_config_mismatches,
        "expectedCaptureConfig": expected_capture_config,
        "error": error,
        "reference": reference,
        "snapshot": snapshot,
        "metadataPath": metadata_path.relative_to(output_root).as_posix(),
        "metadata": metadata,
    }


def _parse_timestamp(value: Any) -> dt.datetime | None:
    if not isinstance(value, str) or not value:
        return None
    try:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None


def _capture_timing(reference_capture: Mapping[str, Any], live_capture: Mapping[str, Any]) -> dict[str, Any]:
    metadata = reference_capture.get("metadata")
    reference_at_text = None
    if isinstance(metadata, Mapping):
        reference_at_text = metadata.get("screenshotAt") or metadata.get("capturedAt")
    attempts = live_capture.get("attempts", [])
    selected_attempt = next((attempt for attempt in attempts if attempt.get("accepted")), None)
    if selected_attempt is None and attempts:
        selected_attempt = attempts[-1]
    live_at_text = selected_attempt.get("startedAt") if selected_attempt else None
    reference_at = _parse_timestamp(reference_at_text)
    live_at = _parse_timestamp(live_at_text)
    delta = (live_at - reference_at).total_seconds() if reference_at and live_at else None
    return {
        "referenceScreenshotAt": reference_at_text,
        "broilerLiveStartedAt": live_at_text,
        "broilerLiveAttempt": selected_attempt.get("attempt") if selected_attempt else None,
        "referenceToLiveStartSeconds": round(delta, 3) if delta is not None else None,
    }


def _build_site_result(
    site: Mapping[str, Any],
    effective_config: Mapping[str, Any],
    reference_capture: Mapping[str, Any],
    live_capture: Mapping[str, Any],
    replay_capture: Mapping[str, Any],
    live_comparison: Mapping[str, Any],
    replay_comparison: Mapping[str, Any],
) -> dict[str, Any]:
    if not live_comparison.get("available"):
        status = "incomplete"
    elif live_comparison.get("quality", {}).get("passed"):
        status = "passed"
    else:
        status = "failed"
    classification = classify_result(live_comparison, replay_comparison, live_capture)
    failing_gates = [
        name
        for name, gate in live_comparison.get("quality", {}).get("gates", {}).items()
        if not gate.get("passed")
    ]
    if status == "passed":
        message = "Live Broiler render met all observational quality thresholds."
    elif status == "failed":
        message = "Live Broiler render missed: " + ", ".join(failing_gates) + "."
    else:
        message = "No live Broiler/reference image pair was available for comparison."
    return {
        "schemaVersion": SCHEMA_VERSION,
        "generatedAt": utc_now(),
        "site": dict(site),
        "effectiveConfig": copy.deepcopy(dict(effective_config)),
        "captures": {
            "reference": reference_capture,
            "broilerLive": live_capture,
            "broilerReplay": replay_capture,
        },
        "comparisons": {
            "live": {**live_comparison, "gating": True},
            "snapshotReplay": {**replay_comparison, "gating": False},
        },
        "captureTiming": _capture_timing(reference_capture, live_capture),
        "result": {
            "status": status,
            "comparable": bool(live_comparison.get("available")),
            "qualityPassed": status == "passed",
            "classification": classification,
            "gatingComparison": "live",
            "failingGates": failing_gates,
            "message": message,
        },
    }


def summarize_results(results: Sequence[Mapping[str, Any]], *, category: str | None = None) -> dict[str, Any]:
    selected = [result for result in results if category is None or result.get("site", {}).get("category") == category]
    counts = {"passed": 0, "failed": 0, "incomplete": 0}
    comparable: list[Mapping[str, Any]] = []
    for result in selected:
        status = result.get("result", {}).get("status", "incomplete")
        counts[status if status in counts else "incomplete"] += 1
        comparison = result.get("comparisons", {}).get("live", {})
        if comparison.get("available"):
            comparable.append(comparison)

    averages: dict[str, float | None] = {}
    for metric in ("pixelMatchPercent", "contentMatchPercent", "structureMatchPercent"):
        values = [float(comparison[metric]) for comparison in comparable if comparison.get(metric) is not None]
        averages[metric] = round(sum(values) / len(values), 4) if values else None
    classification_counts: dict[str, int] = {}
    for result in selected:
        classification = result.get("result", {}).get("classification")
        if classification:
            classification_counts[classification] = classification_counts.get(classification, 0) + 1
    return {
        "scope": category or "all",
        "total": len(selected),
        "comparable": len(comparable),
        **counts,
        "qualityPassRatePercent": round(counts["passed"] * 100.0 / len(comparable), 4) if comparable else None,
        "averageLiveMetrics": averages,
        "classifications": classification_counts,
    }


def _metric(value: Any) -> str:
    return "n/a" if value is None else f"{float(value):.2f}%"


def _capture_failure(capture: Mapping[str, Any]) -> str | None:
    metadata = capture.get("metadata")
    if isinstance(metadata, Mapping) and metadata.get("error"):
        return str(metadata["error"])
    if capture.get("error"):
        return str(capture["error"])
    attempts = capture.get("attempts", [])
    if attempts:
        attempt = attempts[-1]
        if attempt.get("error"):
            return str(attempt["error"])
        if attempt.get("stderr"):
            return str(attempt["stderr"])
    return None


def _capture_attempt_summaries(capture: Mapping[str, Any]) -> list[dict[str, Any]]:
    keys = (
        "attempt",
        "startedAt",
        "finishedAt",
        "durationMilliseconds",
        "exitCode",
        "timedOut",
        "hardTimeoutSeconds",
        "memoryLimitMb",
        "memoryLimitExceeded",
        "peakRssMiB",
        "accepted",
        "dimensionsMatch",
        "stdoutPath",
        "stderrPath",
        "metadataPath",
        "error",
    )
    return [{key: attempt.get(key) for key in keys if key in attempt} for attempt in capture.get("attempts", [])]


def _single_line_excerpt(value: str, limit: int = 500) -> str:
    compact = " ".join(value.split())
    return compact if len(compact) <= limit else compact[: limit - 1] + "…"


def _markdown_code(value: Any, fallback: str = "n/a", limit: int = 500) -> str:
    """Make untrusted page metadata safe inside a one-line Markdown code span."""

    if value is None or value == "":
        text = fallback
    else:
        text = _single_line_excerpt(str(value), limit)
    return text.replace("`", "'")


def render_markdown_report(aggregate: Mapping[str, Any]) -> str:
    summary = aggregate["summary"]
    stable = aggregate["stableSummary"]
    dynamic = aggregate["dynamicSummary"]
    environment = aggregate["environment"]
    corpus = aggregate["corpus"]
    lines = [
        "# Broiler real-world render-test results",
        "",
        "> **Observational / non-deterministic:** " + OBSERVATIONAL_NOTICE,
        "",
        "> **Snapshot replay:** " + REPLAY_NOTICE,
        "",
        f"Generated: `{aggregate['generatedAt']}`",
        f"Repository revision: `{_markdown_code(environment.get('repositoryRevision'))}`",
        f"Corpus manifest SHA-256: `{_markdown_code(corpus.get('manifestSha256'))}`",
        f"Environment: `{_markdown_code(environment.get('os'))}` / `{_markdown_code(environment.get('machine'))}`; "
        f"Python `{_markdown_code(environment.get('pythonVersion'))}`, "
        f"Node `{_markdown_code(environment.get('nodeVersion'))}`, "
        f".NET SDK `{_markdown_code(environment.get('dotnetSdkVersion'))}`",
        "",
        "## Summary",
        "",
        "| Scope | Sites | Comparable | Passed | Failed | Incomplete | Pass rate | Pixel | Content | Structure |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for label, item in (("All", summary), ("Stable sites", stable), ("Dynamic sites", dynamic)):
        averages = item["averageLiveMetrics"]
        lines.append(
            f"| {label} | {item['total']} | {item['comparable']} | {item['passed']} | {item['failed']} | "
            f"{item['incomplete']} | {_metric(item['qualityPassRatePercent'])} | "
            f"{_metric(averages['pixelMatchPercent'])} | {_metric(averages['contentMatchPercent'])} | "
            f"{_metric(averages['structureMatchPercent'])} |"
        )
    lines += [
        "",
        "## Live quality results",
        "",
        "| Site | Category | Result | Classification | Pixel | Content | Structure | Ref→live | Artifacts |",
        "|---|---|---|---|---:|---:|---:|---:|---|",
    ]
    for result in aggregate["results"]:
        site = result["site"]
        outcome = result["result"]
        comparison = result["comparisons"]["live"]
        delta = result.get("captureTiming", {}).get("referenceToLiveStartSeconds")
        site_dir = f"sites/{site['id']}"
        links = f"[result]({site_dir}/site-result.json) · [reference]({site_dir}/reference.png) · [live]({site_dir}/broiler-live.png) · [diff]({site_dir}/live-diff.png)"
        lines.append(
            f"| {site['name']} | {site['category']} | {outcome['status']} | {outcome.get('classification') or '—'} | "
            f"{_metric(comparison.get('pixelMatchPercent'))} | {_metric(comparison.get('contentMatchPercent'))} | "
            f"{_metric(comparison.get('structureMatchPercent'))} | "
            f"{f'{float(delta):.1f}s' if delta is not None else 'n/a'} | {links} |"
        )

    lines += ["", "## Per-site detail", ""]
    for result in aggregate["results"]:
        site = result["site"]
        outcome = result["result"]
        live = result["comparisons"]["live"]
        replay = result["comparisons"]["snapshotReplay"]
        effective = result["effectiveConfig"]
        reference_capture = result["captures"]["reference"]
        reference_metadata = reference_capture.get("metadata") or {}
        site_dir = f"sites/{site['id']}"
        thresholds = effective["thresholds"]
        delta = result.get("captureTiming", {}).get("referenceToLiveStartSeconds")
        attempt_summaries = _capture_attempt_summaries(result["captures"]["broilerLive"])
        accepted_attempt = next((attempt for attempt in attempt_summaries if attempt.get("accepted")), None)
        if accepted_attempt is None and attempt_summaries:
            accepted_attempt = attempt_summaries[-1]
        lines += [
            f"### {site['name']} (`{site['id']}`)",
            "",
            f"- URL: {site['url']}",
            f"- Category: `{site['category']}`",
            f"- Result: **{outcome['status']}** — {outcome['message']}",
            f"- Classification: `{outcome.get('classification') or 'none'}`",
            f"- Reference navigation: HTTP `{_markdown_code(reference_metadata.get('responseStatus'))}`, "
            f"final URL `{_markdown_code(reference_metadata.get('finalUrl'))}`, "
            f"title `{_markdown_code(reference_metadata.get('title'))}`",
            f"- Reference runtime: Playwright `{_markdown_code(reference_metadata.get('playwrightVersion'))}`, "
            f"Chromium `{_markdown_code(reference_metadata.get('chromiumVersion'))}`, "
            f"locale `{effective['locale']}`, timezone `{effective['timezoneId']}`",
            f"- Thresholds: pixel {_metric(thresholds['pixelMatchPercent'])}, "
            f"content {_metric(thresholds['contentMatchPercent'])}, "
            f"structure {_metric(thresholds['structureMatchPercent'])}; "
            f"per-channel tolerance `{effective['pixelTolerance']}`",
            f"- Live metrics: pixel {_metric(live.get('pixelMatchPercent'))}, content {_metric(live.get('contentMatchPercent'))}, structure {_metric(live.get('structureMatchPercent'))}",
            f"- Replay diagnostics: pixel {_metric(replay.get('pixelMatchPercent'))}, content {_metric(replay.get('contentMatchPercent'))}, structure {_metric(replay.get('structureMatchPercent'))}",
            f"- Reference-to-live start delta: `{f'{float(delta):.1f} seconds' if delta is not None else 'n/a'}`",
            "",
            f"![Chromium reference]({site_dir}/reference.png) ![Broiler live]({site_dir}/broiler-live.png) ![Live diff]({site_dir}/live-diff.png)",
            "",
            f"![Broiler snapshot replay]({site_dir}/broiler-replay.png) ![Replay diff]({site_dir}/replay-diff.png)",
            "",
        ]
        if accepted_attempt:
            lines.append(
                f"- Live process: attempt `{accepted_attempt.get('attempt', 'n/a')}`, "
                f"duration `{float(accepted_attempt.get('durationMilliseconds') or 0) / 1000:.2f}s`, "
                f"peak RSS `{float(accepted_attempt.get('peakRssMiB') or 0):.2f} MiB`, "
                f"limit `{accepted_attempt.get('memoryLimitMb', 'n/a')} MiB`"
            )
        coverage = live.get("contentCoverage")
        if coverage:
            lines.append(
                f"Content coverage: Broiler {_metric(coverage.get('actualPercent'))}, "
                f"Chromium {_metric(coverage.get('referencePercent'))}, "
                f"union {_metric(coverage.get('unionPercent'))}."
            )
        if live.get("mismatchBoundingBox"):
            bounds = live["mismatchBoundingBox"]
            lines.append(
                f"Mismatch bounds: `{bounds['left']},{bounds['top']}–{bounds['right']},{bounds['bottom']}` "
                f"({bounds['width']}×{bounds['height']} px)."
            )
        failures = [
            ("Chromium reference", _capture_failure(reference_capture)),
            ("Broiler live", _capture_failure(result["captures"]["broilerLive"])),
            ("Broiler replay", _capture_failure(result["captures"]["broilerReplay"])),
        ]
        for label, failure in failures:
            if failure:
                lines.append(f"- {label} error: `{_markdown_code(failure)}`")
        lines += [
            "",
            "Reproduce this case:",
            "",
            f"`python scripts/run-real-world-render-tests.py --sites {site['id']} --fail-on-mismatch`",
            "",
        ]
        worst_tiles = live.get("worstTiles", [])[:4]
        if worst_tiles:
            lines += ["Worst live 4×3 grid tiles:", ""]
            for tile in worst_tiles:
                bounds = tile["bounds"]
                lines.append(
                    f"- row {tile['row'] + 1}, column {tile['column'] + 1} "
                    f"(`{bounds['left']},{bounds['top']}–{bounds['right']},{bounds['bottom']}`): "
                    f"{_metric(tile['pixelMatchPercent'])} pixel match, MAE {tile['meanAbsoluteError']:.2f}"
                )
            lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def render_job_summary(aggregate: Mapping[str, Any]) -> str:
    """Render Actions-safe Markdown with no artifact-relative links or images."""

    summary = aggregate["summary"]
    stable = aggregate["stableSummary"]
    dynamic = aggregate["dynamicSummary"]
    environment = aggregate["environment"]
    lines = [
        "# Broiler real-world render-test results",
        "",
        "> **Observational / non-deterministic:** " + OBSERVATIONAL_NOTICE,
        "",
        f"Generated: `{aggregate['generatedAt']}`",
        f"Repository revision: `{_markdown_code(environment.get('repositoryRevision'))}`",
        "",
        "## Summary",
        "",
        "| Scope | Sites | Comparable | Passed | Failed | Incomplete | Pass rate | Pixel | Content | Structure |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for label, item in (("All", summary), ("Stable sites", stable), ("Dynamic sites", dynamic)):
        averages = item["averageLiveMetrics"]
        lines.append(
            f"| {label} | {item['total']} | {item['comparable']} | {item['passed']} | {item['failed']} | "
            f"{item['incomplete']} | {_metric(item['qualityPassRatePercent'])} | "
            f"{_metric(averages['pixelMatchPercent'])} | {_metric(averages['contentMatchPercent'])} | "
            f"{_metric(averages['structureMatchPercent'])} |"
        )
    lines += [
        "",
        "## Sites",
        "",
        "| Site | Category | Result | Classification | Pixel | Content | Structure | Ref→live |",
        "|---|---|---|---|---:|---:|---:|---:|",
    ]
    for result in aggregate["results"]:
        site = result["site"]
        outcome = result["result"]
        comparison = result["comparisons"]["live"]
        delta = result.get("captureTiming", {}).get("referenceToLiveStartSeconds")
        lines.append(
            f"| {site['name']} | {site['category']} | {outcome['status']} | "
            f"{outcome.get('classification') or '—'} | {_metric(comparison.get('pixelMatchPercent'))} | "
            f"{_metric(comparison.get('contentMatchPercent'))} | "
            f"{_metric(comparison.get('structureMatchPercent'))} | "
            f"{f'{float(delta):.1f}s' if delta is not None else 'n/a'} |"
        )
    lines += [
        "",
        "The uploaded `real-world-render-results` artifact contains the standalone HTML report, "
        "complete JSON/Markdown, screenshots, red/yellow diffs, metadata, and process logs.",
        "",
    ]
    return "\n".join(lines)


def _image_data_uri(path: Path) -> str | None:
    if not path.is_file():
        return None
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def render_html_report(aggregate: Mapping[str, Any], output_root: Path) -> str:
    summary = aggregate["summary"]
    stable = aggregate["stableSummary"]
    dynamic = aggregate["dynamicSummary"]
    environment = aggregate["environment"]
    corpus = aggregate["corpus"]
    cards: list[str] = []
    for result in aggregate["results"]:
        site = result["site"]
        outcome = result["result"]
        live = result["comparisons"]["live"]
        replay = result["comparisons"]["snapshotReplay"]
        diagnostic_payload = {
            "effectiveConfig": result["effectiveConfig"],
            "reference": result["captures"]["reference"].get("metadata"),
            "liveCaptureStatus": result["captures"]["broilerLive"].get("status"),
            "replayCaptureStatus": result["captures"]["broilerReplay"].get("status"),
            "captureErrors": {
                "reference": _capture_failure(result["captures"]["reference"]),
                "broilerLive": _capture_failure(result["captures"]["broilerLive"]),
                "broilerReplay": _capture_failure(result["captures"]["broilerReplay"]),
            },
            "broilerLiveAttempts": _capture_attempt_summaries(result["captures"]["broilerLive"]),
            "broilerReplayAttempts": _capture_attempt_summaries(result["captures"]["broilerReplay"]),
            "liveComparison": live,
            "snapshotReplayComparison": replay,
            "captureTiming": result.get("captureTiming"),
        }
        diagnostic_json = html.escape(json.dumps(diagnostic_payload, indent=2, ensure_ascii=False))
        images = [
            ("Chromium reference", output_root / "sites" / site["id"] / "reference.png"),
            ("Broiler live", output_root / "sites" / site["id"] / "broiler-live.png"),
            ("Broiler snapshot replay", output_root / "sites" / site["id"] / "broiler-replay.png"),
            ("Live diff — red content / yellow background", output_root / "sites" / site["id"] / "live-diff.png"),
            ("Replay diff — red content / yellow background", output_root / "sites" / site["id"] / "replay-diff.png"),
        ]
        figures: list[str] = []
        for label, path in images:
            uri = _image_data_uri(path)
            body = f'<img loading="lazy" src="{uri}" alt="{html.escape(label)}">' if uri else '<div class="missing">capture unavailable</div>'
            figures.append(f"<figure>{body}<figcaption>{html.escape(label)}</figcaption></figure>")
        cards.append(
            f'<section class="site {html.escape(outcome["status"])}">'
            f'<h2>{html.escape(site["name"])} <code>{html.escape(site["id"])}</code></h2>'
            f'<p><a href="{html.escape(site["url"], quote=True)}">{html.escape(site["url"])}</a> · '
            f'{html.escape(site["category"])} · <strong>{html.escape(outcome["status"])}</strong> · '
            f'{html.escape(outcome.get("classification") or "no failure classification")}</p>'
            f'<p>{html.escape(outcome["message"])}</p>'
            f'<table><thead><tr><th>Comparison</th><th>Pixel</th><th>Content</th><th>Structure</th><th>Gating</th></tr></thead><tbody>'
            f'<tr><td>Live</td><td>{_metric(live.get("pixelMatchPercent"))}</td><td>{_metric(live.get("contentMatchPercent"))}</td><td>{_metric(live.get("structureMatchPercent"))}</td><td>yes</td></tr>'
            f'<tr><td>Snapshot replay</td><td>{_metric(replay.get("pixelMatchPercent"))}</td><td>{_metric(replay.get("contentMatchPercent"))}</td><td>{_metric(replay.get("structureMatchPercent"))}</td><td>no</td></tr>'
            f'</tbody></table><div class="gallery">{"".join(figures)}</div>'
            f'<details><summary>Capture and metric details</summary><pre>{diagnostic_json}</pre></details></section>'
        )
    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Broiler real-world render-test results</title>
<style>
:root {{ color-scheme: light dark; font-family: system-ui, sans-serif; }} body {{ max-width: 1500px; margin: auto; padding: 1rem; }}
.notice {{ border-left: .35rem solid #d69e00; padding: .7rem 1rem; background: color-mix(in srgb, #d69e00 12%, Canvas); }}
table {{ border-collapse: collapse; margin: .7rem 0; }} th,td {{ border: 1px solid #8887; padding: .4rem .65rem; text-align: left; }}
.site {{ border: 1px solid #8887; border-radius: .5rem; padding: 1rem; margin: 1.2rem 0; }} .failed {{ border-color: #d33; }} .passed {{ border-color: #298a40; }}
.gallery {{ display: grid; grid-template-columns: repeat(auto-fit,minmax(300px,1fr)); gap: .75rem; }} figure {{ margin: 0; }}
img,.missing {{ display:block; width:100%; aspect-ratio:4/3; object-fit:contain; background:#222; }} .missing {{ display:grid;place-items:center;color:#ddd; }}
figcaption {{ font-size:.9rem; padding:.3rem 0; }} code {{ font-size:.72em; }}
pre {{ overflow:auto; max-height:36rem; padding:.75rem; background:#8882; }} details {{ margin-top:1rem; }}
</style></head><body>
<h1>Broiler real-world render-test results</h1>
<p class="notice"><strong>Observational / non-deterministic.</strong> {html.escape(OBSERVATIONAL_NOTICE)}</p>
<p class="notice"><strong>Snapshot replay limitation.</strong> {html.escape(REPLAY_NOTICE)}</p>
<p>Generated <code>{html.escape(str(aggregate['generatedAt']))}</code>.</p>
<p>Repository <code>{html.escape(str(environment.get('repositoryRevision') or 'n/a'))}</code> · manifest
<code>{html.escape(str(corpus.get('manifestSha256') or 'n/a'))}</code> ·
{html.escape(str(environment.get('os') or 'unknown OS'))} / {html.escape(str(environment.get('machine') or 'unknown architecture'))} ·
Python {html.escape(str(environment.get('pythonVersion') or 'n/a'))} · Node {html.escape(str(environment.get('nodeVersion') or 'n/a'))} ·
.NET SDK {html.escape(str(environment.get('dotnetSdkVersion') or 'n/a'))}</p>
<table><thead><tr><th>Scope</th><th>Sites</th><th>Comparable</th><th>Passed</th><th>Failed</th><th>Incomplete</th><th>Pass rate</th></tr></thead><tbody>
<tr><td>All</td><td>{summary['total']}</td><td>{summary['comparable']}</td><td>{summary['passed']}</td><td>{summary['failed']}</td><td>{summary['incomplete']}</td><td>{_metric(summary['qualityPassRatePercent'])}</td></tr>
<tr><td>Stable</td><td>{stable['total']}</td><td>{stable['comparable']}</td><td>{stable['passed']}</td><td>{stable['failed']}</td><td>{stable['incomplete']}</td><td>{_metric(stable['qualityPassRatePercent'])}</td></tr>
<tr><td>Dynamic</td><td>{dynamic['total']}</td><td>{dynamic['comparable']}</td><td>{dynamic['passed']}</td><td>{dynamic['failed']}</td><td>{dynamic['incomplete']}</td><td>{_metric(dynamic['qualityPassRatePercent'])}</td></tr>
</tbody></table>
{''.join(cards)}
</body></html>"""


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")
    os.replace(temporary, path)


def _percentage_argument(value: str) -> float:
    try:
        return _require_percentage(float(value), "threshold")
    except (ValueError, ManifestError) as exc:
        raise argparse.ArgumentTypeError(str(exc)) from exc


def _tolerance_argument(value: str) -> int:
    try:
        return _require_integer(int(value), "pixel tolerance", 0, 255)
    except (ValueError, ManifestError) as exc:
        raise argparse.ArgumentTypeError(str(exc)) from exc


def create_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Compare Broiler against Chromium captures of commonly used public sites")
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST, help=f"site manifest (default: {DEFAULT_MANIFEST})")
    parser.add_argument("--output-dir", default=DEFAULT_OUTPUT_DIR, help=f"artifact directory (default: {DEFAULT_OUTPUT_DIR})")
    parser.add_argument("--sites", action="append", help="site IDs, comma-separated or repeatable")
    parser.add_argument("--skip-reference-capture", action="store_true", help="reuse existing reference.png/snapshot.html artifacts")
    parser.add_argument("--skip-build", action="store_true", help="reuse the existing Broiler.Cli build")
    parser.add_argument("--configuration", default="Release", help="dotnet build configuration (default: Release)")
    parser.add_argument("--viewport", type=parse_viewport, metavar="WIDTHxHEIGHT", help="override every site viewport")
    parser.add_argument("--pixel-tolerance", type=_tolerance_argument, help="override per-channel RGBA tolerance")
    parser.add_argument("--pixel-match-percent", "--pixel-match-threshold", "--pixel-threshold", type=_percentage_argument, dest="pixel_match_percent")
    parser.add_argument("--content-match-percent", "--content-match-threshold", "--content-threshold", type=_percentage_argument, dest="content_match_percent")
    parser.add_argument("--structure-match-percent", "--structure-match-threshold", "--structure-threshold", type=_percentage_argument, dest="structure_match_percent")
    parser.add_argument("--capture-attempts", type=int, default=DEFAULT_CAPTURE_ATTEMPTS, help="fresh attempts per Broiler capture (default: 2)")
    parser.add_argument(
        "--memory-limit-mb",
        type=int,
        default=DEFAULT_MEMORY_LIMIT_MB,
        help=f"maximum RSS for each Broiler process tree; 0 disables (default: {DEFAULT_MEMORY_LIMIT_MB})",
    )
    parser.add_argument("--fail-on-mismatch", action="store_true", help="exit 1 when a comparable live render misses a threshold")
    parser.add_argument("--fail-on-incomplete", action="store_true", help="exit 2 when any selected site is incomplete")
    return parser


def _resolve_input_path(value: str, repo_root: Path) -> Path:
    path = Path(value).expanduser()
    if not path.is_absolute():
        path = repo_root / path
    return path.resolve()


def _empty_capture(source: str, output_path: Path, output_root: Path, reason: str) -> dict[str, Any]:
    return {
        "status": "failed",
        "source": source,
        "path": output_path.relative_to(output_root).as_posix(),
        "attemptCount": 0,
        "attempts": [],
        "artifact": {"available": False, "path": output_path.relative_to(output_root).as_posix(), "error": reason},
        "error": reason,
    }


def main(argv: Sequence[str] | None = None) -> int:
    args = create_argument_parser().parse_args(argv)
    if args.capture_attempts < 1:
        print("error: --capture-attempts must be at least 1", file=sys.stderr)
        return 2
    if args.memory_limit_mb < 0:
        print("error: --memory-limit-mb must be non-negative", file=sys.stderr)
        return 2

    repo_root = Path(__file__).resolve().parent.parent
    manifest_path = _resolve_input_path(args.manifest, repo_root)
    output_root = _resolve_input_path(args.output_dir, repo_root)
    output_root.mkdir(parents=True, exist_ok=True)
    try:
        manifest = load_manifest(manifest_path)
    except ManifestError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    requested_ids = parse_site_selection(args.sites)
    known = {site["id"]: site for site in manifest["sites"]}
    unknown = [site_id for site_id in requested_ids if site_id not in known]
    if unknown:
        print(f"error: unknown site id(s): {', '.join(unknown)}", file=sys.stderr)
        return 2
    sites = [known[site_id] for site_id in requested_ids] if requested_ids else list(manifest["sites"])
    effective_configs = {
        site["id"]: resolve_effective_config(
            manifest["defaults"],
            site,
            viewport=args.viewport,
            pixel_tolerance=args.pixel_tolerance,
            pixel_match_percent=args.pixel_match_percent,
            content_match_percent=args.content_match_percent,
            structure_match_percent=args.structure_match_percent,
        )
        for site in sites
    }

    print("=== Broiler real-world render tests (observational / non-deterministic) ===")
    print(OBSERVATIONAL_NOTICE)
    print(REPLAY_NOTICE)
    print(f"Manifest: {manifest_path}")
    print(f"Output:   {output_root}")
    print(f"Sites:    {', '.join(site['id'] for site in sites)}")

    # Build before visiting any live page. Besides failing fast on compilation
    # errors, this keeps dynamic Chromium references as close as possible to
    # the Broiler captures that consume them.
    dotnet = shutil.which("dotnet") or "dotnet"
    build_result: dict[str, Any]
    if args.skip_build:
        build_result = {"skipped": True, "reason": "--skip-build"}
    else:
        print("\n--- Building Broiler.Cli once ---")
        build_command = [
            dotnet,
            "build",
            str(repo_root / "src" / "Broiler.Cli" / "Broiler.Cli.csproj"),
            "--configuration",
            args.configuration,
            "--nologo",
            "--verbosity",
            "minimal",
        ]
        build_result = _command_result(build_command, cwd=repo_root, timeout=900)
        (output_root / "broiler-build.stdout.log").write_text(
            build_result.get("stdout", ""), encoding="utf-8", newline="\n"
        )
        (output_root / "broiler-build.stderr.log").write_text(
            build_result.get("stderr", ""), encoding="utf-8", newline="\n"
        )
        if build_result.get("stdout"):
            print(build_result["stdout"], end="" if build_result["stdout"].endswith("\n") else "\n")
        if build_result.get("exitCode") != 0 and build_result.get("stderr"):
            print(build_result["stderr"], file=sys.stderr)
    cli_dll = locate_cli_dll(repo_root, args.configuration)
    cli_ready = cli_dll is not None and (args.skip_build or build_result.get("exitCode") == 0)

    node = shutil.which("node") or "node"
    run_environment = collect_run_environment(repo_root, node, dotnet)
    generator_result: dict[str, Any]
    if args.skip_reference_capture:
        generator_result = {"skipped": True, "reason": "--skip-reference-capture"}
    else:
        # The orchestrator clears first as well as the Node helper. This closes
        # the stale-output gap even if the two validators ever disagree and the
        # helper exits before it reaches browser setup.
        for site in sites:
            _remove_reference_artifacts(output_root / "sites" / site["id"])
        generator = repo_root / "scripts" / "generate-real-world-references.js"
        command = [node, str(generator), "--manifest", str(manifest_path), "--output-dir", str(output_root)]
        for site in sites:
            command.extend(["--site", site["id"]])
        if args.viewport:
            command.extend(["--width", str(args.viewport[0]), "--height", str(args.viewport[1])])
        generator_env = dict(os.environ)
        node_modules = str(repo_root / "tests" / "wpt" / "node_modules")
        generator_env["NODE_PATH"] = node_modules + (os.pathsep + generator_env["NODE_PATH"] if generator_env.get("NODE_PATH") else "")
        print("\n--- Capturing Chromium references once ---")
        # Two bounded attempts per site, plus browser setup/teardown slack. The
        # child also has per-navigation and per-render deadlines; this parent
        # deadline is the final defence against a wedged browser driver.
        generator_timeout = 90 + sum(
            2
            * (
                int(effective_configs[site["id"]]["navigationTimeoutSeconds"])
                + math.ceil(int(effective_configs[site["id"]]["settleMilliseconds"]) / 1000)
                + 60
            )
            for site in sites
        )
        generator_result = _command_result(
            command, cwd=repo_root, env=generator_env, timeout=generator_timeout
        )
        (output_root / "reference-generator.stdout.log").write_text(
            generator_result.get("stdout", ""), encoding="utf-8", newline="\n"
        )
        (output_root / "reference-generator.stderr.log").write_text(
            generator_result.get("stderr", ""), encoding="utf-8", newline="\n"
        )
        if generator_result.get("stdout"):
            print(generator_result["stdout"], end="" if generator_result["stdout"].endswith("\n") else "\n")
        if generator_result.get("exitCode") != 0:
            print("Reference capture did not complete successfully; partial artifacts will still be reported.", file=sys.stderr)
            if generator_result.get("stderr"):
                print(generator_result["stderr"], file=sys.stderr)

    results: list[dict[str, Any]] = []
    for ordinal, site in enumerate(sites, 1):
        site_id = site["id"]
        site_dir = output_root / "sites" / site_id
        site_dir.mkdir(parents=True, exist_ok=True)
        effective = effective_configs[site_id]
        print(f"\n--- [{ordinal}/{len(sites)}] {site['name']} ({site_id}) ---")
        reference_capture = _reference_capture(site_dir, output_root, site, effective)
        live_path = site_dir / "broiler-live.png"
        replay_path = site_dir / "broiler-replay.png"
        if cli_ready and cli_dll is not None:
            live_capture = run_broiler_capture(
                dotnet,
                cli_dll,
                site["url"],
                live_path,
                effective,
                args.capture_attempts,
                repo_root,
                output_root,
                args.memory_limit_mb,
            )
            snapshot_path = site_dir / "snapshot.html"
            if reference_capture.get("snapshotUsable"):
                replay_capture = run_broiler_capture(
                    dotnet,
                    cli_dll,
                    snapshot_path.resolve().as_uri(),
                    replay_path,
                    effective,
                    args.capture_attempts,
                    repo_root,
                    output_root,
                    args.memory_limit_mb,
                )
            else:
                _remove_capture_artifacts(replay_path)
                replay_capture = _empty_capture(
                    "snapshot.html",
                    replay_path,
                    output_root,
                    str(reference_capture.get("snapshot", {}).get("error") or "snapshot.html is unavailable"),
                )
        else:
            reason = "Broiler.Cli build is unavailable"
            _remove_capture_artifacts(live_path)
            _remove_capture_artifacts(replay_path)
            live_capture = _empty_capture(site["url"], live_path, output_root, reason)
            replay_capture = _empty_capture("snapshot.html", replay_path, output_root, reason)

        reference_path = site_dir / "reference.png"
        live_comparison = compare_image_files(
            live_path,
            reference_path,
            tolerance=effective["pixelTolerance"],
            thresholds=effective["thresholds"],
            diff_path=site_dir / "live-diff.png",
            output_root=output_root,
            reference_usable=bool(reference_capture.get("usable")),
            reference_error=reference_capture.get("error"),
        )
        replay_comparison = compare_image_files(
            replay_path,
            reference_path,
            tolerance=effective["pixelTolerance"],
            thresholds=effective["thresholds"],
            diff_path=site_dir / "replay-diff.png",
            output_root=output_root,
            reference_usable=bool(reference_capture.get("usable")),
            reference_error=reference_capture.get("error"),
        )
        site_result = _build_site_result(
            site, effective, reference_capture, live_capture, replay_capture, live_comparison, replay_comparison
        )
        write_json(site_dir / "site-result.json", site_result)
        results.append(site_result)
        live = site_result["comparisons"]["live"]
        print(
            f"{site_result['result']['status'].upper()}: pixel {_metric(live.get('pixelMatchPercent'))}, "
            f"content {_metric(live.get('contentMatchPercent'))}, structure {_metric(live.get('structureMatchPercent'))}"
        )

    aggregate = {
        "schemaVersion": SCHEMA_VERSION,
        "generatedAt": utc_now(),
        "observational": {
            "liveSuiteIsNonDeterministic": True,
            "liveSuiteNotice": OBSERVATIONAL_NOTICE,
            "snapshotReplayIsGating": False,
            "snapshotReplayNotice": REPLAY_NOTICE,
        },
        "manifest": str(manifest_path),
        "corpus": {
            "manifestPath": str(manifest_path),
            "manifestSha256": sha256_file(manifest_path),
            "schemaVersion": manifest["schemaVersion"],
        },
        "environment": run_environment,
        "harness": {
            "runnerSha256": sha256_file(Path(__file__).resolve()),
            "referenceGeneratorSha256": sha256_file(repo_root / "scripts" / "generate-real-world-references.js"),
        },
        "selectedSites": [site["id"] for site in sites],
        "run": {
            "configuration": args.configuration,
            "captureAttempts": args.capture_attempts,
            "memoryLimitMb": args.memory_limit_mb,
            "referenceGenerator": generator_result,
            "build": build_result,
            "cliDll": str(cli_dll) if cli_dll else None,
        },
        "summary": summarize_results(results),
        "stableSummary": summarize_results(results, category="stable"),
        "dynamicSummary": summarize_results(results, category="dynamic"),
        "results": results,
    }
    write_json(output_root / "results.json", aggregate)
    (output_root / "report.md").write_text(render_markdown_report(aggregate), encoding="utf-8", newline="\n")
    (output_root / "job-summary.md").write_text(render_job_summary(aggregate), encoding="utf-8", newline="\n")
    (output_root / "report.html").write_text(render_html_report(aggregate, output_root), encoding="utf-8", newline="\n")

    summary = aggregate["summary"]
    print("\n=== Result ===")
    print(
        f"{summary['passed']} passed, {summary['failed']} failed, {summary['incomplete']} incomplete; "
        f"{summary['comparable']}/{summary['total']} comparable"
    )
    print(f"JSON:     {output_root / 'results.json'}")
    print(f"Markdown: {output_root / 'report.md'}")
    print(f"CI summary: {output_root / 'job-summary.md'}")
    print(f"HTML:     {output_root / 'report.html'}")

    if summary["comparable"] == 0:
        return 2
    if args.fail_on_incomplete and summary["incomplete"]:
        return 2
    if args.fail_on_mismatch and summary["failed"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
