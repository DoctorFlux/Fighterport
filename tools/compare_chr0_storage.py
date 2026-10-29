#!/usr/bin/env python3
"""Read-only before/after CHR0 storage accounting; never invokes a writer.

Scenario labels describe the caller's context only. They do not prove that a
save, edit, retarget, worker, or fitting operation occurred between these files.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "_fighterport_storage_wire", Path(__file__).with_name("chr0_forensics.py"))
_wire = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_wire)

SCHEMA = "fighterport-chr0-storage-comparison-1"
SCENARIOS = ("unspecified", "no-edit", "one-key", "worker", "retarget", "fitting")
MAX_OUTPUT_BYTES = 64 * 1024 * 1024
MAX_GROUPS = 50_000
_ZERO = "00000000"
_NEGATIVE_ZERO = "80000000"
_SEMANTICS = {"use_model_scale": 7, "use_model_rotation": 8,
              "use_model_translation": 9, "scale_compensate_apply": 10,
              "scale_compensate_parent": 11, "classic_scale_off": 12}
_GROUP_NAMES = ("scale", "rotation", "translation")
_ACCOUNTING = ("slot_bytes", "allocated_curve_header_bytes",
               "allocated_curve_payload_bytes", "allocated_total_bytes",
               "referenced_unique_curve_header_bytes", "referenced_unique_curve_payload_bytes",
               "physical_allocated_stored_records", "referenced_unique_stored_records",
               "logical_stored_records", "native_projected_keys")


class StorageComparisonError(ValueError):
    """A bounded physical comparison could not be completed."""


def _sha(data):
    return hashlib.sha256(data).hexdigest()


def _rows(stream):
    return stream.get("rows", stream.get("projected_rows", []))


def _rows_digest(rows):
    digest = hashlib.sha256()
    for row in rows:
        for field in ("time_bits", "value_bits", "tangent_bits"):
            digest.update(bytes.fromhex(row[field]))
    return digest.hexdigest()


def _constant_observation(rows, samples=None):
    """Conservative decoded-function observation, never an omission approval."""
    if not rows:
        return {"status": "absent", "constant_proven": False,
                "exact_zero_slope_bits": False, "equal_values_nonzero_tangents": False,
                "signed_zero_present": False, "constant_over_complete_stored_domain": False}
    equal_numeric = all(row["value"] == rows[0]["value"] for row in rows)
    equal_bits = all(row["value_bits"] == rows[0]["value_bits"] for row in rows)
    zero_slopes = all(row["tangent"] == 0 for row in rows)
    positive_zero_slopes = all(row["tangent_bits"] == _ZERO for row in rows)
    signed_zero = any(row[key] == _NEGATIVE_ZERO for row in rows
                      for key in ("time_bits", "value_bits", "tangent_bits"))
    full_samples_constant = samples is None or all(
        sample["bits"] == samples[0]["bits"] for sample in samples)
    proven = equal_numeric and zero_slopes
    status = ("constant_zero_slopes" if proven else
              "equal_values_nonzero_tangents_unproved" if equal_numeric else "varying_values")
    return {"status": status, "constant_proven": proven,
            "equal_value_bits": equal_bits, "exact_zero_slope_bits": positive_zero_slopes,
            "equal_values_nonzero_tangents": equal_numeric and not zero_slopes,
            "signed_zero_present": signed_zero,
            "constant_over_complete_stored_domain": proven and full_samples_constant,
            "constant_value_bits": rows[0]["value_bits"] if proven and equal_bits else None,
            "claim": "Equal decoded values plus zero stored slopes prove a scalar constant; this does not authorize dropping a channel, entry, signed zero, protected key, or model dependency."}


def _channel(entry, channel, streams, owner_by_stream, frames):
    name = _wire.CHANNELS[channel]
    slot = next((slot for slot in entry["slots"] if name in slot["isotropic_channels"]), None)
    code = int(entry["flags_hex"], 16)
    result = {"channel": name, "channel_index": channel,
              "group_exists": bool(code & (1 << (22 + channel // 3))),
              "fixed_flag": bool(code & (1 << (13 + channel))),
              "use_model": bool(code & (1 << (7 + channel // 3))),
              "slot_owner_channel": None, "physical_slot_bytes_allocated": 0,
              "storage": "absent", "native_projected_keys": 0,
              "stored_scalar_records": 0, "constant_densely_keyed": False,
              "decoded_row_bits_sha256": _rows_digest([])}
    if slot is None:
        result["constant"] = _constant_observation([])
        return result
    result["slot_owner_channel"] = slot["channel"]
    result["physical_slot_bytes_allocated"] = 4 if name == slot["channel"] else 0
    if slot["fixed"]:
        rows = [{"time": 0.0, "time_bits": _ZERO, "value": slot["constant"]["value"],
                 "value_bits": slot["constant"]["bits"], "tangent": 0.0, "tangent_bits": _ZERO}]
        result.update(storage="fixed", fixed_value=slot["constant"], native_projected_keys=1,
                      decoded_row_bits_sha256=_rows_digest(rows))
        result["constant"] = _constant_observation(rows)
        return result
    stream = streams[slot["stream_id"]]
    rows = _rows(stream)
    distinct_integer_frames = sum(1 for time in {row["time"] for row in rows}
                                  if time.is_integer() and 0 <= time < frames)
    owner = owner_by_stream[stream["id"]]
    result.update(storage=stream["format"], stream_id=stream["id"],
                  stream_offset=stream["offset"], raw_stream_sha256=stream["sha256"],
                  curve_header_bytes=stream["codec_header_bytes"],
                  curve_payload_bytes=stream["payload_bytes"], raw_curve_bytes=stream["raw_bytes"],
                  stored_scalar_records=stream["stored_count"], native_projected_keys=len(rows),
                  decoded_row_bits_sha256=_rows_digest(rows),
                  distinct_native_key_times=len({row["time_bits"] for row in rows}),
                  integer_native_frames_keyed=distinct_integer_frames,
                  every_visible_native_frame_keyed=distinct_integer_frames == frames,
                  paired_sites=len(stream["paired_sites"]),
                  stream_physical_allocation_owner=owner,
                  shared_stream=stream["reference_count"] > 1 or stream["logical_axis_count"] > 1,
                  terminal_sample=stream.get("terminal_sample"),
                  frame_scale=stream["frame_scale"], reserved_u16=stream["reserved_u16"],
                  quantizer_base=stream["base"], quantizer_step=stream["step"])
    result["constant"] = _constant_observation(rows, stream.get("samples"))
    conservative_bits = (result["constant"]["constant_over_complete_stored_domain"] and
                         result["constant"]["equal_value_bits"] and result["constant"]["exact_zero_slope_bits"] and
                         not result["constant"]["signed_zero_present"])
    result["constant_densely_keyed"] = len(rows) > 1 and distinct_integer_frames == frames and conservative_bits
    result["constant_multiple_keys"] = len(rows) > 1 and conservative_bits
    result["mathematical_constant_multiple_keys"] = len(rows) > 1 and result["constant"]["constant_over_complete_stored_domain"]
    return result


def _file_summary(data, arithmetic):
    parsed = _wire.parse_chr0(data, scalar_arithmetic=arithmetic)
    if len(parsed["entries"]) * 3 > MAX_GROUPS:
        raise StorageComparisonError("entry/group report exceeds the 50,000-group bound")
    streams = {stream["id"]: stream for stream in parsed["streams"]}
    owners = {identity: {"bone": stream["references"][0]["bone"],
                          "group": stream["references"][0]["group"],
                          "channel": stream["references"][0]["channel"]}
              for identity, stream in streams.items()}
    entries = []
    for entry in parsed["entries"]:
        channels = [_channel(entry, channel, streams, owners, parsed["header"]["frame_duration"])
                    for channel in range(9)]
        groups = []
        for group, descriptor in enumerate(entry["groups"]):
            slots = [slot for slot in entry["slots"] if slot["group"] == group]
            ids = list(dict.fromkeys(slot["stream_id"] for slot in slots if not slot["fixed"]))
            refs = [streams[identity] for identity in ids]
            allocated = [stream for stream in refs if owners[stream["id"]]["bone"] == entry["name"]
                         and owners[stream["id"]]["group"] == group]
            row = dict(descriptor, index=group, format_bits=(int(entry["flags_hex"], 16) >> (25, 27, 30)[group]) & (3, 7, 3)[group],
                       slot_bytes=4 * len(slots), fixed_slots=sum(slot["fixed"] for slot in slots),
                       variable_slots=sum(not slot["fixed"] for slot in slots),
                       allocated_curve_header_bytes=sum(stream["codec_header_bytes"] for stream in allocated),
                       allocated_curve_payload_bytes=sum(stream["payload_bytes"] for stream in allocated),
                       referenced_unique_curve_header_bytes=sum(stream["codec_header_bytes"] for stream in refs),
                       referenced_unique_curve_payload_bytes=sum(stream["payload_bytes"] for stream in refs),
                       physical_allocated_stored_records=sum(stream["stored_count"] for stream in allocated),
                       referenced_unique_stored_records=sum(stream["stored_count"] for stream in refs),
                       logical_stored_records=sum(channel["stored_scalar_records"] for channel in channels[group * 3:group * 3 + 3]),
                       native_projected_keys=sum(channel["native_projected_keys"] for channel in channels[group * 3:group * 3 + 3]),
                       channels=channels[group * 3:group * 3 + 3],
                       streams=[{"id": stream["id"], "offset": stream["offset"], "format": stream["format"],
                                 "raw_bytes": stream["raw_bytes"], "sha256": stream["sha256"],
                                 "physical_allocation_owner": owners[stream["id"]],
                                 "allocated_here": stream in allocated, "reference_count": stream["reference_count"],
                                 "logical_axis_count": stream["logical_axis_count"]} for stream in refs])
            row["allocated_total_bytes"] = row["slot_bytes"] + row["allocated_curve_header_bytes"] + row["allocated_curve_payload_bytes"]
            groups.append(row)
        entries.append({"name": entry["name"], "offset": entry["offset"], "flags_hex": entry["flags_hex"],
                        "semantic_flags": {name: bool(int(entry["flags_hex"], 16) & (1 << bit)) for name, bit in _SEMANTICS.items()},
                        "bone_header_bytes": 8, "physical_table_bytes": entry["table_bytes"],
                        "allocated_total_bytes": 8 + sum(group["allocated_total_bytes"] for group in groups),
                        "native_projected_keys": sum(channel["native_projected_keys"] for channel in channels), "groups": groups})
    by_kind = parsed["ledger"]["by_kind"]
    allocated_total = sum(entry["allocated_total_bytes"] for entry in entries)
    expected_allocated = sum(by_kind.get(kind, 0) for kind in
                             ("bone_header", "fixed_cell", "reference_cell", "codec_header", "curve_payload"))
    if allocated_total != expected_allocated:
        raise StorageComparisonError("deduplicated entry/group byte accounting does not reconcile")
    format_counts = Counter(stream["format"] for stream in streams.values())
    by_format = {name: {"streams": format_counts[name],
                        "codec_header_bytes": sum(stream["codec_header_bytes"] for stream in streams.values() if stream["format"] == name),
                        "payload_bytes": sum(stream["payload_bytes"] for stream in streams.values() if stream["format"] == name),
                        "stored_rows_or_samples": sum(stream["stored_count"] for stream in streams.values() if stream["format"] == name)}
                 for name in sorted(format_counts)}
    unknown = [region for region in parsed["ledger"]["regions"] if region["kind"] == "unclassified"]
    return {"sha256": parsed["sha256"], "physical_standalone_bytes": len(data),
            "declared_resource_bytes": parsed["header"]["resource_size"],
            "outside_declared_resource_bytes": len(data) - parsed["header"]["resource_size"],
            "header": parsed["header"], "scalar_arithmetic": parsed["scalar_arithmetic"],
            "scalar_arithmetic_requested": arithmetic, "warnings": parsed["warnings"],
            "bone_entries": len(entries), "native_projected_keys": sum(entry["native_projected_keys"] for entry in entries),
            "stored_variable_rows_or_samples": sum(stream["stored_count"] for stream in streams.values()),
            "physical_streams": len(streams), "stream_format_usage": dict(sorted(format_counts.items())),
            "physical_storage_by_format": by_format,
            "allocated_entry_group_bytes": allocated_total,
            "ledger_by_kind": by_kind, "resource_ledger_by_kind": parsed["ledger"]["resource_by_kind"],
            "metadata": parsed["metadata"], "strings": parsed["strings"],
            "alignment_and_padding": {"proven_padding_bytes": None,
                "zero_filled_unclassified_bytes": sum(region["bytes"] for region in unknown if region["zero_filled"]),
                "nonzero_unclassified_bytes": sum(region["bytes"] for region in unknown if not region["zero_filled"]),
                "unclassified_regions": unknown,
                "claim": "Zero-filled gaps are observed; this audit never assumes they are removable alignment/padding."},
            "shared_streams": [{"id": stream["id"], "offset": stream["offset"], "raw_bytes": stream["raw_bytes"],
                "format": stream["format"], "physical_allocation_owner": owners[stream["id"]],
                "references": stream["references"], "logical_axis_count": stream["logical_axis_count"]}
                for stream in streams.values() if stream["reference_count"] > 1 or stream["logical_axis_count"] > 1],
            "byte_identical_alias_opportunities": parsed["byte_identical_alias_potential"], "entries": entries}


def _difference(before, after, names):
    return {name: after.get(name, 0) - before.get(name, 0) for name in names}


def compare_storage(before: bytes, after: bytes, *, scenario="unspecified", scalar_arithmetic="exact-single-round"):
    """Describe actual immutable byte snapshots. This function never serializes CHR0."""
    if scenario not in SCENARIOS:
        raise StorageComparisonError("unknown scenario label")
    left, right = _file_summary(before, scalar_arithmetic), _file_summary(after, scalar_arithmetic)
    old, new = ({entry["name"]: entry for entry in side["entries"]} for side in (left, right))
    order = list(old) + [name for name in new if name not in old]
    changes = []
    for name in order:
        a, b = old.get(name), new.get(name)
        groups = []
        for group in range(3):
            ga, gb = (a["groups"][group] if a else {}), (b["groups"][group] if b else {})
            ca = {channel["channel"]: channel for channel in ga.get("channels", [])}
            cb = {channel["channel"]: channel for channel in gb.get("channels", [])}
            projection_changes = [channel for channel in _wire.CHANNELS[group * 3:group * 3 + 3]
                                  if ca.get(channel, {}).get("decoded_row_bits_sha256") != cb.get(channel, {}).get("decoded_row_bits_sha256")]
            groups.append({"group": _GROUP_NAMES[group], "before": ga or None, "after": gb or None,
                           "delta": _difference(ga, gb, _ACCOUNTING),
                           "decoded_projection_bits_equal": bool(ga and gb) and not projection_changes,
                           "channels_with_changed_decoded_row_bits": projection_changes,
                           "storage_flags_changed": any(ga.get(key) != gb.get(key) for key in
                               ("exists", "format_bits", "scale_isotropic", "use_model")),
                           "channel_fixed_flags_changed": [channel for channel in _wire.CHANNELS[group * 3:group * 3 + 3]
                               if next((c["fixed_flag"] for c in ga.get("channels", []) if c["channel"] == channel), None) !=
                                  next((c["fixed_flag"] for c in gb.get("channels", []) if c["channel"] == channel), None)]})
        changes.append({"name": name, "status": "added" if a is None else "removed" if b is None else "present",
                        "before_flags_hex": a["flags_hex"] if a else None, "after_flags_hex": b["flags_hex"] if b else None,
                        "before_semantic_flags": a["semantic_flags"] if a else None,
                        "after_semantic_flags": b["semantic_flags"] if b else None,
                        "allocated_total_delta_bytes": (b["allocated_total_bytes"] if b else 0) - (a["allocated_total_bytes"] if a else 0),
                        "groups": groups})
    categories = sorted(set(left["ledger_by_kind"]) | set(right["ledger_by_kind"]))
    return {"schema": SCHEMA, "read_only": True, "writer_used": False,
            "native_evaluator_used": False, "scenario_label": scenario,
            "scenario_claim": "Caller-supplied label only; the tool did not witness or perform the named operation.",
            "scalar_arithmetic": left["scalar_arithmetic"], "scalar_arithmetic_requested": scalar_arithmetic,
            "exact_policy_claim": "Physical byte and declared-profile decoded observations only; no lossy operation, curve equivalence, safe omission, runtime compatibility, or optimization eligibility is inferred.",
            "physical_allocation_claim": "Each unique stream is attributed once to its first dictionary-order reference; this explicit accounting attribution can move when entry order changes and is not native ownership or causality.",
            "target_rig_supplied": False,
            "bind_valued_omission_claim": "No model is supplied; zero/one/fixed/absent values do not prove a bind default or permit omitting an entry or group.",
            "byte_identical": before == after, "before": left, "after": right,
            "delta": _difference(left, right, ("physical_standalone_bytes", "declared_resource_bytes",
                "outside_declared_resource_bytes", "bone_entries", "native_projected_keys",
                "stored_variable_rows_or_samples", "physical_streams", "allocated_entry_group_bytes")),
            "ledger_delta_bytes": _difference(left["ledger_by_kind"], right["ledger_by_kind"], categories),
            "added_entries": [name for name in new if name not in old], "removed_entries": [name for name in old if name not in new],
            "entry_order_changed": list(old) != list(new),
            "header_changes": {key: {"before": left["header"].get(key), "after": right["header"].get(key)}
                               for key in sorted(set(left["header"]) | set(right["header"]))
                               if left["header"].get(key) != right["header"].get(key)},
            "entries": changes}


def _same_path(left, right):
    if left.resolve() == right.resolve():
        return True
    try:
        return left.exists() and right.exists() and left.samefile(right)
    except OSError:
        return False


def _read(path):
    if path.stat().st_size > _wire.MAX_FILE_BYTES:
        raise StorageComparisonError("file exceeds the independent parser's 512 MiB bound")
    with path.open("rb") as handle:
        data = handle.read(_wire.MAX_FILE_BYTES + 1)
    if len(data) > _wire.MAX_FILE_BYTES:
        raise StorageComparisonError("file grew beyond the independent parser's 512 MiB bound")
    return data


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("--scenario", choices=SCENARIOS, default="unspecified")
    parser.add_argument("--scalar-arithmetic", choices=_wire.SCALAR_ARITHMETIC, default="exact-single-round")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    if args.output and any(_same_path(args.output, path) for path in (args.before, args.after)):
        parser.error("--output must not overwrite or alias either input")
    try:
        before, after = _read(args.before), _read(args.after)
        report = compare_storage(before, after, scenario=args.scenario, scalar_arithmetic=args.scalar_arithmetic)
        # A source changed during analysis is not an admitted before/after pair.
        if _sha(_read(args.before)) != _sha(before) or _sha(_read(args.after)) != _sha(after):
            raise StorageComparisonError("an input changed during the read-only comparison")
        report["inputs"] = {"before": str(args.before), "after": str(args.after), "hashes_unchanged_after_analysis": True}
        text = json.dumps(report, sort_keys=True, indent=2, allow_nan=False) + "\n"
        if len(text.encode("utf-8")) > MAX_OUTPUT_BYTES:
            raise StorageComparisonError("report exceeds the 64 MiB output bound")
        if args.output:
            args.output.write_text(text, encoding="utf-8", newline="\n")
        else:
            sys.stdout.write(text)
        return 0
    except (OSError, _wire.Chr0Error, StorageComparisonError) as error:
        # Failure is concise; no partial success report replaces an existing output.
        sys.stderr.write("CHR0 storage comparison failed: " + str(error) + "\n")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
