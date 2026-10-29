#!/usr/bin/env python3
"""Compare original CHR0 bytes with the read-only native coefficient dump.

Uses the independent Python parser, never a native process or serialization
codec. This corroborates decoded representation, not motion or game behavior.
"""
from __future__ import annotations

import argparse
from bisect import bisect_left
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "_fighterport_independent_chr0", Path(__file__).with_name("chr0_forensics.py"))
_parser = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_parser)
parse_chr0 = _parser.parse_chr0
SCHEMA = "fighterport-chr0-native-comparison-1"
MAX_DUMP_BYTES = 128 * 1024 * 1024
_MISSING = "<missing>"
_SEMANTICS = {"UseModelScale": 7, "UseModelRotate": 8, "UseModelTranslate": 9,
              "ScaleCompensateApply": 10, "ScaleCompensateParent": 11,
              "ClassicScaleOff": 12}


class ComparisonError(ValueError):
    """The evidence is incomplete or ambiguous; success cannot be reported."""


def _sha(data):
    return hashlib.sha256(data).hexdigest()


def _object(value):
    return value if isinstance(value, dict) else {}


def _list(value):
    return value if isinstance(value, list) else []


def _bits(value):
    return int(value["bits"], 16)


def _expected_channels(entry, streams):
    """Map independent physical slots to the native reader's nine arrays."""
    channels = [{"rows": [], "stream": None, "fixed": False} for _ in range(9)]
    for slot in entry["slots"]:
        if slot["fixed"]:
            rows = [(0, _bits(slot["constant"]), 0)]
            stream = None
        else:
            stream = streams[slot["stream_id"]]
            raw_rows = stream.get("rows", stream.get("projected_rows", []))
            rows = [(int(row["time_bits"], 16), int(row["value_bits"], 16),
                     int(row["tangent_bits"], 16)) for row in raw_rows]
        for name in slot["isotropic_channels"]:
            channels[_parser.CHANNELS.index(name)] = {
                "rows": rows, "stream": stream, "fixed": slot["fixed"]}
    return channels


def _retained_metadata(data, report, stream, backing_length, following, table_starts, table_ends):
    """Metadata expected when an independently bounded compact plan is captured.

    No scalar decoding occurs here. Values/rows come from parse_chr0; the byte
    extent accounts for the native retained plan's optional alignment bytes.
    I12 has no CaptureStorage plan and remains represented by its native rows.
    """
    if stream is None or stream["format"] == "I12":
        return None
    dense = "samples" in stream
    if not dense and (stream["stored_count"] < 2 or
                      not stream["current_nonnegative_writer_timing"]):
        return None
    raw = stream["raw_bytes"]
    length = stream["aligned_writer_bytes"]
    if dense:
        next_curve = following[stream["offset"]]
        later = [next_curve] if next_curve is not None else []
        later += [backing_length] if backing_length > stream["offset"] else []
        metadata = report["header"]["user_data_offset"]
        if metadata > stream["offset"]:
            later.append(metadata)
        next_start = min(later, default=-1)
        if next_start == stream["offset"] + raw:
            length = raw
        elif next_start != stream["offset"] + length:
            return None
        previous_table = bisect_left(table_starts, stream["offset"] + length) - 1
        if previous_table >= 0 and table_ends[previous_table] > stream["offset"]:
            return None
    if stream["offset"] + length > len(data):
        return None
    fmt = next(code for code, name in _parser.FORMATS.items() if name == stream["format"])
    key_count = stream["projected_key_count"]
    digest = _sha(data[stream["offset"]:stream["offset"] + length]).upper()
    content = f"{fmt}:" + (f"{key_count}:{stream['stored_count']}:" if dense else "") + digest
    return {
        "Format": stream["format"], "FormatCode": fmt, "IsDense": dense,
        "KeyCount": key_count,
        "NativeSampleCount": stream["projected_sample_count"] if dense else 0,
        "EncodedSampleCount": stream["stored_count"] if dense else 0,
        "ByteCount": length, "ContentKey": content,
        "BaseBits": _bits(stream["base"]) if stream["base"] is not None else 0,
        "StepBits": _bits(stream["step"]) if stream["step"] is not None else 0x3f800000,
        "FrameScaleBits": 0 if dense else _bits(stream["frame_scale"]),
        "MatchesNativeCurve": True,
        "EncodedSampleValueBits": [_bits(sample) for sample in stream["samples"]] if dense else None,
        "TerminalSampleValueBits": _bits(stream["terminal_sample"]) if dense else None,
        "TerminalSampleProjected": stream["terminal_sample"]["projected_by_owned_reader"] if dense else None,
    }


def compare_native_dump(animation: bytes, native_dump: dict, *, maximum_diffs=200,
                        scalar_arithmetic="binary32-steps"):
    """Return deterministic JSON-ready evidence; exact mismatches set ok=False."""
    if maximum_diffs < 1 or maximum_diffs > 10000:
        raise ValueError("maximum_diffs must be in 1..10000")
    report = parse_chr0(animation, scalar_arithmetic=scalar_arithmetic)
    differences, difference_count = [], 0
    streams = {stream["id"]: stream for stream in report["streams"]}
    expected_channels = [_expected_channels(entry, streams) for entry in report["entries"]]
    counts = {"entries": len(report["entries"]), "channels": 0,
              "expected_rows": sum(len(channel["rows"]) for channels in expected_channels for channel in channels),
              "native_actual_rows": 0,
              "absent_channels": 0, "fixed_channels": 0,
              "isotropically_expanded_axes": 0, "retained_compact_channels": 0,
              "retained_encoded_samples": 0, "physical_streams_by_format": {}}
    for stream in report["streams"]:
        formats = counts["physical_streams_by_format"]
        formats[stream["format"]] = formats.get(stream["format"], 0) + 1

    def equal(path, expected, actual):
        nonlocal difference_count
        if type(expected) is type(actual) and expected == actual:
            return
        difference_count += 1
        if len(differences) < maximum_diffs:
            differences.append({"path": path, "expected": expected, "actual": actual})

    dump = _object(native_dump)
    for name, expected in {"ReadOnly": True, "WriterUsed": False,
                           "ModelLoadedOrPosed": False, "PrivateDesktop": True,
                           "OSInputInjected": False, "ScreenCopied": False,
                           "FreshReadsIdentical": True,
                           "NativeReader": "NodeFactory/CHR0Node.KeyArrays"}.items():
        equal("guards." + name, expected, dump.get(name, _MISSING))
    motion, model = _object(dump.get("Motion")), _object(dump.get("Model"))
    for field in ("Sha256Before", "Sha256After"):
        equal("Motion." + field, report["sha256"], str(motion.get(field, _MISSING)).lower())
    equal("Motion.FileBytes", len(animation), motion.get("FileBytes", _MISSING))
    model_hash = model.get("Sha256Before", _MISSING)
    equal("Model.Sha256Before.valid", True, isinstance(model_hash, str) and bool(re.fullmatch(r"[0-9a-fA-F]{64}", model_hash)))
    equal("Model.Sha256After", str(model_hash).lower(), str(model.get("Sha256After", _MISSING)).lower())
    projection = _object(dump.get("Projection"))
    backing_length = projection.get("BackingLength", -1)
    valid_backing = type(backing_length) is int and 0 < backing_length <= len(animation)
    equal("Projection.BackingLength.valid", True, valid_backing)
    if not valid_backing:
        backing_length = report["header"]["resource_size"]
    for field in ("BackingSha256Before", "BackingSha256After"):
        equal("Motion." + field, _sha(animation[:backing_length]), str(motion.get(field, _MISSING)).lower())
    header = report["header"]
    for name, expected in {"Name": header["name"], "Version": header["version"],
                           "Frames": header["frame_duration"], "Loop": header["loop"],
                           "OriginalPath": header["original_path"]}.items():
        equal("Projection." + name, expected, projection.get(name, _MISSING))
    equal("Projection.Entries.array", True, isinstance(projection.get("Entries"), list))
    native_entries = _list(projection.get("Entries"))
    counts["native_actual_rows"] = sum(len(_list(_object(channel).get("Rows")))
        for entry in native_entries for channel in _list(_object(entry).get("Channels")))
    equal("Projection.Entries.count", len(report["entries"]), len(native_entries))
    offsets = [stream["offset"] for stream in report["streams"]]
    following = {offset: offsets[index + 1] if index + 1 < len(offsets) else None
                 for index, offset in enumerate(offsets)}
    tables = sorted((entry["offset"], entry["offset"] + entry["table_bytes"]) for entry in report["entries"])
    table_starts, table_ends = [item[0] for item in tables], [item[1] for item in tables]
    retained_by_stream = {stream["id"]: _retained_metadata(animation, report, stream, backing_length,
                          following, table_starts, table_ends) for stream in report["streams"]}
    frame_limit = header["frame_duration"] + int(header["loop"])
    for index, (entry, native_value) in enumerate(zip(report["entries"], native_entries)):
        native = _object(native_value); prefix = f"Entries[{index}]"
        code = int(entry["flags_hex"], 16)
        for name, expected in {"Index": index, "Name": entry["name"], "RawFlags": code,
                               "RawFlagsHex": entry["flags_hex"], "FrameLimit": frame_limit,
                               "Loop": header["loop"]}.items():
            equal(prefix + "." + name, expected, native.get(name, _MISSING))
        for name, bit in _SEMANTICS.items():
            equal(prefix + "." + name, bool(code & (1 << bit)), native.get(name, _MISSING))
        channels = expected_channels[index]
        equal(prefix + ".PreserveInterpolation", any(channel["stream"] is not None and
              channel["stream"]["format"] in ("I12", "L1", "L2", "L4") for channel in channels),
              native.get("PreserveInterpolation", _MISSING))
        equal(prefix + ".Groups.array", True, isinstance(native.get("Groups"), list))
        native_groups = _list(native.get("Groups"))
        equal(prefix + ".Groups.count", 3, len(native_groups))
        for group, (expected, actual_value) in enumerate(zip(entry["groups"], native_groups)):
            actual = _object(actual_value)
            for name, value in {"Index": group, "Name": ("Scale", "Rotation", "Translation")[group],
                                "Exists": expected["exists"], "Isotropic": bool(code & (1 << (4 + group))),
                                "Format": expected["format"],
                                "FormatCode": (code >> (25, 27, 30)[group]) & (3, 7, 3)[group]}.items():
                equal(f"{prefix}.Groups[{group}].{name}", value, actual.get(name, _MISSING))
        retained = [retained_by_stream[channel["stream"]["id"]] if channel["stream"] is not None else None for channel in channels]
        # CaptureStorage rejects the entire entry if any variable sampled
        # rotation cannot retain its complete independently bounded terminal.
        if any(channel["stream"] is not None and "samples" in channel["stream"] and retained[channel_index] is None
               for channel_index, channel in enumerate(channels)):
            retained = [None] * 9
        equal(prefix + ".StorageCaptureAvailable", any(item is not None for item in retained), native.get("StorageCaptureAvailable", _MISSING))
        equal(prefix + ".StorageContentKey", "|".join(item["ContentKey"] if item else "I12" for item in retained)
              if any(item is not None for item in retained) else None, native.get("StorageContentKey", _MISSING))
        equal(prefix + ".Channels.array", True, isinstance(native.get("Channels"), list))
        native_channels = _list(native.get("Channels"))
        equal(prefix + ".Channels.count", 9, len(native_channels))
        for channel, (expected, native_channel_value) in enumerate(zip(channels, native_channels)):
            actual = _object(native_channel_value); path = f"{prefix}.Channels[{channel}]"
            counts["channels"] += 1
            counts["absent_channels"] += int(not entry["groups"][channel // 3]["exists"])
            counts["fixed_channels"] += int(expected["fixed"])
            counts["isotropically_expanded_axes"] += int(channel in (1, 2) and bool(code & 16) and entry["groups"][0]["exists"])
            for name, value in {"Index": channel, "Name": _parser.CHANNELS[channel],
                                "GroupExists": entry["groups"][channel // 3]["exists"],
                                "FixedFlag": bool(code & (1 << (13 + channel))),
                                "FrameLimit": frame_limit, "Loop": header["loop"], "KeyCount": len(expected["rows"])}.items():
                equal(path + "." + name, value, actual.get(name, _MISSING))
            equal(path + ".Rows.array", True, isinstance(actual.get("Rows"), list))
            actual_rows = _list(actual.get("Rows"))
            equal(path + ".Rows.count", len(expected["rows"]), len(actual_rows))
            for row, (bits, actual_row_value) in enumerate(zip(expected["rows"], actual_rows)):
                actual_row = _object(actual_row_value)
                equal(f"{path}.Rows[{row}].Index", row, actual_row.get("Index", _MISSING))
                for name, value in zip(("Time", "Value", "Tangent"), bits):
                    equal(f"{path}.Rows[{row}].{name}Bits", value, actual_row.get(name + "Bits", _MISSING))
                    equal(f"{path}.Rows[{row}].{name}Hex", f"{value:08x}", actual_row.get(name + "Hex", _MISSING))
            expected_storage, actual_storage = retained[channel], actual.get("RetainedStorage", _MISSING)
            if expected_storage is None:
                equal(path + ".RetainedStorage", None, actual_storage)
                continue
            counts["retained_compact_channels"] += 1
            counts["retained_encoded_samples"] += expected_storage["EncodedSampleCount"]
            if not isinstance(actual_storage, dict):
                equal(path + ".RetainedStorage.type", "object", type(actual_storage).__name__)
                continue
            for name, value in expected_storage.items():
                if name != "EncodedSampleValueBits" or value is None:
                    equal(path + ".RetainedStorage." + name, value, actual_storage.get(name, _MISSING))
                else:
                    actual_samples = _list(actual_storage.get(name))
                    equal(path + ".RetainedStorage.EncodedSampleValueBits.count", len(value), len(actual_samples))
                    for sample, (expected_bits, actual_bits) in enumerate(zip(value, actual_samples)):
                        equal(f"{path}.RetainedStorage.EncodedSampleValueBits[{sample}]", expected_bits, actual_bits)
    return {"schema": SCHEMA, "ok": difference_count == 0, "read_only": True,
            "native_process_invoked": False, "animation_sha256": report["sha256"],
            "scalar_arithmetic": report["scalar_arithmetic"],
            "scalar_arithmetic_requested": report["scalar_arithmetic_requested"],
            "scalar_arithmetic_claim": report["scalar_arithmetic_claim"],
            "independent_parser_schema": report["schema"], "counts": counts,
            "difference_count": difference_count, "differences": differences,
            "differences_truncated": difference_count > len(differences),
            "claim": "Exact native decoded coefficients, flags and retained compact metadata; no motion-quality or game claim."}


def _json_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ComparisonError("duplicate native JSON field: " + key)
        result[key] = value
    return result


def _read(path, limit):
    if path.stat().st_size > limit:
        raise ComparisonError(f"{path.name}: input exceeds {limit} bytes")
    with path.open("rb") as handle:
        data = handle.read(limit + 1)
    if len(data) > limit:
        raise ComparisonError(f"{path.name}: input grew beyond the byte limit")
    return data


def main(argv=None):
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("animation", type=Path)
    cli.add_argument("native_dump", type=Path)
    cli.add_argument("--output", type=Path)
    cli.add_argument("--max-diffs", type=int, default=200)
    cli.add_argument("--scalar-arithmetic", choices=_parser.SCALAR_ARITHMETIC,
                     default="binary32-steps",
                     help="explicit quantized decode rounding profile; no tolerance or automatic profile selection")
    args = cli.parse_args(argv)
    if not 1 <= args.max_diffs <= 10000:
        cli.error("--max-diffs must be in 1..10000")
    if args.output:
        for source in (args.animation, args.native_dump):
            same = args.output.resolve() == source.resolve()
            try:
                same = same or (args.output.exists() and args.output.samefile(source))
            except OSError:
                pass
            if same:
                cli.error("--output must not identify either input file")
    try:
        animation = _read(args.animation, _parser.MAX_FILE_BYTES)
        native_bytes = _read(args.native_dump, MAX_DUMP_BYTES)
        native = json.loads(native_bytes.decode("utf-8-sig"), object_pairs_hook=_json_object,
                            parse_constant=lambda value: (_ for _ in ()).throw(ComparisonError("nonfinite native JSON number: " + value)))
        result = compare_native_dump(animation, native, maximum_diffs=args.max_diffs,
                                     scalar_arithmetic=args.scalar_arithmetic)
        unchanged = _sha(_read(args.animation, _parser.MAX_FILE_BYTES)) == _sha(animation) and _sha(_read(args.native_dump, MAX_DUMP_BYTES)) == _sha(native_bytes)
        result["input_snapshots_unchanged"] = unchanged
        result["ok"] = result["ok"] and unchanged
    except (OSError, ValueError, _parser.Chr0Error) as error:
        result = {"schema": SCHEMA, "ok": False, "read_only": True,
                  "native_process_invoked": False,
                  "scalar_arithmetic_requested": args.scalar_arithmetic, "error": str(error)}
    text = json.dumps(result, sort_keys=True, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8", newline="\n")
    else:
        sys.stdout.write(text)
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
