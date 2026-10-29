#!/usr/bin/env python3
"""Independent, read-only CHR0 v4/v5 wire accountant (Python standard library).

No BrawlLib, native reader, game evaluator, reduction or writer is invoked.
See docs/CHR0_BINARY_FORMAT.md for scope, source pins and unresolved semantics.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

SCHEMA = "fighterport-chr0-forensics-1"
MAX_FILE_BYTES = 512 * 1024 * 1024
MAX_ROWS = 2_000_000
MAX_STREAMS = 200_000
MAX_STRING_BYTES = 1024 * 1024
MAX_TOTAL_STRING_BYTES = 16 * 1024 * 1024
FORMATS = {0: "None", 1: "I4", 2: "I6", 3: "I12", 4: "L1", 5: "L2", 6: "L4"}
SCALAR_ARITHMETIC = ("binary32-steps", "exact-single-round", "native-x86")
CHANNELS = ("SX", "SY", "SZ", "RX", "RY", "RZ", "TX", "TY", "TZ")
FLAG_NAMES = (
    "enabled", "srt_identity", "rotation_translation_zero", "scale_one",
    "scale_isotropic", "rotation_zero__owned_rotation_isotropic",
    "translation_zero__owned_translation_isotropic", "use_model_scale",
    "use_model_rotation", "use_model_translation", "scale_compensate_apply",
    "scale_compensate_parent", "classic_scale_off", "SX_fixed", "SY_fixed",
    "SZ_fixed", "RX_fixed", "RY_fixed", "RZ_fixed", "TX_fixed", "TY_fixed",
    "TZ_fixed", "scale_exists", "rotation_exists", "translation_exists",
    "scale_format_bit0", "scale_format_bit1", "rotation_format_bit0",
    "rotation_format_bit1", "rotation_format_bit2", "translation_format_bit0",
    "translation_format_bit1",
)


class Chr0Error(ValueError):
    """A bounded parse failed; no partial report is represented as success."""


def _bits(value):
    return f"{struct.unpack('>I', struct.pack('>f', value))[0]:08x}"


def _f32(value):
    try:
        result = struct.unpack(">f", struct.pack(">f", value))[0]
    except (OverflowError, struct.error) as error:
        raise Chr0Error("nonfinite binary32 arithmetic result") from error
    if not math.isfinite(result):
        raise Chr0Error("nonfinite binary32 arithmetic result")
    return result


def _value(value):
    return {"value": value, "bits": _bits(value)}


def _row(time, value, tangent, **extra):
    return {"time": time, "time_bits": _bits(time), "value": value,
            "value_bits": _bits(value), "tangent": tangent,
            "tangent_bits": _bits(tangent), **extra}


def _dyadic(value):
    """Exact signed integer * 2**exponent for an already finite binary32."""
    word = struct.unpack(">I", struct.pack(">f", value))[0]
    exponent, significand = (word >> 23) & 255, word & 0x7FFFFF
    if exponent:
        significand |= 1 << 23
    return (-significand if word >> 31 else significand,
            exponent - 150 if exponent else -149)


def _round_dyadic(numerator, exponent, *, negative_zero=False):
    """Correctly round an exact dyadic to binary32, nearest with ties to even.

    Integer arithmetic decides every discarded bit. There is no intermediate
    conversion through binary64, so cancellation/double rounding cannot alter
    the final binary32 word.
    """
    sign = 0x80000000 if numerator < 0 or numerator == 0 and negative_zero else 0
    magnitude = abs(numerator)
    if magnitude == 0:
        return struct.unpack(">f", struct.pack(">I", sign))[0]
    top_exponent = magnitude.bit_length() - 1 + exponent
    quantum = max(top_exponent - 23, -149)
    discarded = quantum - exponent
    if discarded <= 0:
        rounded = magnitude << -discarded
    else:
        rounded, remainder = divmod(magnitude, 1 << discarded)
        halfway = 1 << (discarded - 1)
        if remainder > halfway or remainder == halfway and rounded & 1:
            rounded += 1
    if rounded == 0:
        word = sign
    elif rounded < (1 << 23) and quantum == -149:
        word = sign | rounded
    else:
        final_exponent = rounded.bit_length() - 1 + quantum
        if final_exponent > 127:
            raise Chr0Error("nonfinite binary32 arithmetic result")
        significand = rounded >> (rounded.bit_length() - 24)
        word = sign | ((final_exponent + 127) << 23) | (significand & 0x7FFFFF)
    return struct.unpack(">f", struct.pack(">I", word))[0]


def _decoded(base, integer, step, scalar_arithmetic="binary32-steps"):
    if scalar_arithmetic == "binary32-steps":
        return _f32(base + _f32(integer * step))
    if scalar_arithmetic not in ("exact-single-round", "native-x86"):
        raise Chr0Error(f"unknown scalar arithmetic profile {scalar_arithmetic!r}")
    base_integer, base_exponent = _dyadic(base)
    step_integer, step_exponent = _dyadic(step)
    exponent = min(base_exponent, step_exponent)
    numerator = (base_integer << (base_exponent - exponent)) + (
        integer * step_integer << (step_exponent - exponent))
    # IEEE nearest rounding gives +zero for exact cancellation. Two negative
    # zero addends retain -zero. Unsigned integer quantizers never change sign.
    negative_zero = (_bits(base) == "80000000" and math.copysign(1, step) < 0
                     and (integer == 0 or step == 0))
    return _round_dyadic(numerator, exponent, negative_zero=negative_zero)


class _Reader:
    def __init__(self, data, scalar_arithmetic="binary32-steps"):
        if not isinstance(data, bytes):
            raise TypeError("parse_chr0 expects immutable bytes")
        if len(data) > MAX_FILE_BYTES:
            raise Chr0Error("file exceeds 512 MiB bound")
        self.data = data
        self.end = len(data)
        self.resource_end = self.end
        self.claims = []
        self.strings = {}
        self.string_bytes = 0
        self.warnings = []
        self.rows = 0
        if scalar_arithmetic not in SCALAR_ARITHMETIC:
            raise Chr0Error(f"unknown scalar arithmetic profile {scalar_arithmetic!r}")
        self.scalar_arithmetic = "exact-single-round" if scalar_arithmetic == "native-x86" else scalar_arithmetic

    def check(self, at, size, *, resource=False, end=None):
        limit = min(self.end, self.resource_end) if resource else self.end
        if end is not None:
            limit = min(limit, end)
        if at < 0 or size < 0 or at > limit or size > limit - at:
            raise Chr0Error(f"out-of-bounds range at 0x{at:x}, size {size}, limit 0x{limit:x}")

    def number(self, fmt, at, *, resource=False, end=None):
        self.check(at, struct.calcsize(fmt), resource=resource, end=end)
        return struct.unpack_from(fmt, self.data, at)[0]

    def u16(self, at, **kw):
        return self.number(">H", at, **kw)

    def s16(self, at, **kw):
        return self.number(">h", at, **kw)

    def u32(self, at, **kw):
        return self.number(">I", at, **kw)

    def s32(self, at, **kw):
        return self.number(">i", at, **kw)

    def flt(self, at, **kw):
        result = self.number(">f", at, **kw)
        if not math.isfinite(result):
            raise Chr0Error(f"nonfinite float at 0x{at:x}")
        return result

    def claim(self, at, size, kind, owner):
        self.check(at, size)
        if size:
            self.claims.append({"offset": at, "end": at + size, "bytes": size,
                                "kind": kind, "owner": owner})

    def string(self, at):
        if at == 0:
            return None
        if at in self.strings:
            return self.strings[at]["text"]
        self.check(at, 1)
        end = self.data.find(b"\0", at, min(self.end, at + MAX_STRING_BYTES + 1))
        if end < 0:
            raise Chr0Error(f"unterminated or overlong string at 0x{at:x}")
        raw = self.data[at:end]
        self.string_bytes += len(raw) + 1
        if self.string_bytes > MAX_TOTAL_STRING_BYTES:
            raise Chr0Error("total unique string bytes exceed 16 MiB")
        # Binary strings are preserved even when their historical encoding is
        # not UTF-8. Hex plus a validity flag avoids lossy replacement text.
        try:
            text, utf8 = raw.decode("utf-8"), True
        except UnicodeDecodeError:
            text, utf8 = raw.decode("latin-1"), False
        record = {"offset": at, "text": text, "raw_hex": raw.hex(), "utf8": utf8,
                  "nul_end": end + 1, "outside_resource": at >= self.resource_end,
                  "length_prefix_matches": at >= 4 and self.u32(at - 4) == len(raw)}
        self.strings[at] = record
        self.claim(at, len(raw) + 1, "string_characters", f"string@{at:x}")
        # Claim a matching BRES length word; all unrelated gaps remain unknown.
        if record["length_prefix_matches"]:
            self.claim(at - 4, 4, "string_length", f"string@{at:x}")
        return text

    def rel_string(self, base, cell):
        relative = self.s32(cell)
        return self.string(base + relative) if relative else None

    def reserve_rows(self, count):
        if count < 0 or count > MAX_ROWS - self.rows:
            raise Chr0Error("decoded row/sample bound exceeds two million")
        self.rows += count

    def dictionary(self, at, label, *, end=None):
        self.check(at, 24, resource=True, end=end)
        size, count = self.u32(at), self.u32(at + 4)
        if count > 65535 or size < 24 + count * 16:
            raise Chr0Error(f"invalid {label} dictionary size/count")
        self.check(at, size, resource=True, end=end)
        self.claim(at, 24 + count * 16, "dictionary", label)
        nodes = []
        for index in range(count + 1):
            row = at + 8 + 16 * index
            node = {"index": index, "offset": row, "id": self.u16(row),
                    "flag": self.u16(row + 2), "left": self.u16(row + 4),
                    "right": self.u16(row + 6), "name_relative": self.s32(row + 8),
                    "data_relative": self.s32(row + 12)}
            if node["left"] > count or node["right"] > count:
                raise Chr0Error(f"{label} dictionary tree link exceeds node count")
            if index:
                node["name"] = self.rel_string(at, row + 8)
                node["data_offset"] = at + node["data_relative"]
                self.check(node["data_offset"], 8, resource=True, end=end)
            nodes.append(node)
        return {"offset": at, "declared_size": size, "table_bytes": 24 + count * 16,
                "count": count, "nodes": nodes,
                "tree_claim": "links bounded; Patricia lookup ordering is not reconstructed"}


def _flags(code):
    return [{"bit": index, "name": name, "set": bool(code & (1 << index))}
            for index, name in enumerate(FLAG_NAMES)]


def _independent_presence(code, group):
    # Pinned RiiStudio's omission masks, recorded separately from the owned
    # reader's existence-bit dispatch. No attempt to repair inconsistent flags.
    masks = ((1 << 1) | (1 << 3) | (1 << 7),
             (1 << 1) | (1 << 2) | (1 << 5) | (1 << 8),
             (1 << 1) | (1 << 2) | (1 << 6) | (1 << 9))
    return not bool(code & masks[group])


def _pair_sites(rows):
    result = []
    previous = -math.inf
    index = 0
    while index < len(rows):
        item = rows[index]
        time = item["time"]
        if time < previous:
            raise Chr0Error("indexed times are not ordered")
        end = index + 1
        while end < len(rows) and rows[end]["time"] == time:
            end += 1
        if end - index > 2:
            raise Chr0Error("more than two indexed sides at one time")
        if end - index == 2:
            result.append({"time": time, "incoming_row": index, "outgoing_row": index + 1,
                           "same_value_bits": item["value_bits"] == rows[index + 1]["value_bits"],
                           "same_tangent_bits": item["tangent_bits"] == rows[index + 1]["tangent_bits"]})
        previous, index = time, end
    return result


def _projection(samples):
    result = []
    for index, value in enumerate(samples):
        incoming = _f32(value - samples[index - 1]) if index else (
            _f32(samples[1] - value) if len(samples) > 1 else 0.0)
        outgoing = _f32(samples[index + 1] - value) if index + 1 < len(samples) else incoming
        result.append(_row(float(index), value, incoming))
        if _bits(incoming) != _bits(outgoing):
            result.append(_row(float(index), value, outgoing))
    return result


def _stream(reader, at, fmt, frames, loop):
    name = FORMATS[fmt]
    if fmt <= 3:
        header, stride = (8 if fmt == 3 else 16), {1: 4, 2: 6, 3: 12}[fmt]
        reader.check(at, header, resource=True)
        count = reader.u16(at)
        if count == 0:
            raise Chr0Error("indexed stream has zero stored rows")
        frame_scale = reader.flt(at + 4)
        reserved = reader.u16(at + 2)
        step = reader.flt(at + 8) if fmt != 3 else None
        base = reader.flt(at + 12) if fmt != 3 else None
    else:
        header, stride = (0 if fmt == 6 else 8), {4: 1, 5: 2, 6: 4}[fmt]
        count, frame_scale, reserved = frames + 1, None, None
        reader.check(at, header, resource=True)
        step = reader.flt(at) if fmt != 6 else None
        base = reader.flt(at + 4) if fmt != 6 else None
    raw_size = header + stride * count
    reader.check(at, raw_size, resource=True)
    reader.reserve_rows(count)
    reader.claim(at, header, "codec_header", f"stream@{at:x}")
    reader.claim(at + header, stride * count, "curve_payload", f"stream@{at:x}")
    result = {"id": f"stream@{at:x}", "offset": at, "format": name,
              "codec_header_bytes": header, "payload_bytes": stride * count,
              "raw_bytes": raw_size, "end": at + raw_size,
              "aligned_writer_bytes": (raw_size + 3) & ~3, "stored_count": count,
              "frame_scale": None if frame_scale is None else _value(frame_scale),
              "reserved_u16": reserved, "base": None if base is None else _value(base),
              "step": None if step is None else _value(step), "references": []}
    rows, samples, quantized_samples = [], [], []
    decode_cache = {}

    def decode(integer):
        if integer not in decode_cache:
            alternatives = {}
            for profile in ("binary32-steps", "exact-single-round"):
                try:
                    alternatives[profile] = _value(_decoded(base, integer, step, profile))
                except Chr0Error as error:
                    alternatives[profile] = {"error": str(error)}
            selected = alternatives[reader.scalar_arithmetic]
            if "error" in selected:
                raise Chr0Error(selected["error"])
            decode_cache[integer] = selected["value"], alternatives
        return decode_cache[integer]

    for index in range(count):
        cell = at + header + index * stride
        if fmt == 3:
            rows.append(_row(reader.flt(cell), reader.flt(cell + 4), reader.flt(cell + 8)))
        elif fmt == 1:
            word = reader.u32(cell)
            tangent = word & 4095
            tangent = tangent - 4096 if tangent & 2048 else tangent
            quantized = (word >> 12) & 4095
            value, alternatives = decode(quantized)
            rows.append(_row(float(word >> 24), value, tangent / 32,
                             quantized_value=quantized, quantized_tangent=tangent,
                             arithmetic_values=alternatives, raw_word=f"{word:08x}"))
        elif fmt == 2:
            time, quantized, tangent = reader.s16(cell), reader.u16(cell + 2), reader.s16(cell + 4)
            value, alternatives = decode(quantized)
            rows.append(_row(time / 32, value, tangent / 256,
                             quantized_time=time, quantized_value=quantized, quantized_tangent=tangent,
                             arithmetic_values=alternatives))
        else:
            if fmt == 6:
                value = reader.flt(cell)
            else:
                quantized = reader.data[cell] if fmt == 4 else reader.u16(cell)
                value, alternatives = decode(quantized)
                quantized_samples.append({"quantized_value": quantized, "arithmetic_values": alternatives})
            samples.append(value)
    if fmt <= 3:
        result["rows"] = rows
        result["paired_sites"] = _pair_sites(rows)
        result["projected_key_count"] = len(rows)
        result["current_nonnegative_writer_timing"] = all(row["time"] >= 0 for row in rows)
        result["within_clip_visible_timing"] = all(0 <= row["time"] <= frames - (not loop) for row in rows)
        result["raw_euler_turns_preserved"] = True
    else:
        projected = samples if loop else samples[:-1]
        rows = _projection(projected)
        reader.reserve_rows(len(rows))
        result.update(samples=[{"index": index, **_value(value),
                                **(quantized_samples[index] if fmt != 6 else {})}
                               for index, value in enumerate(samples)],
                      projected_rows=rows, projected_sample_count=len(projected),
                      projected_key_count=len(rows), paired_sites=_pair_sites(rows),
                      terminal_sample={"index": frames, **_value(samples[-1]),
                                       "projected_by_owned_reader": loop},
                      interpolation_claim="physical samples; linear intervals; paired Hermite rows are an owned editor projection")
    result["sha256"] = hashlib.sha256(reader.data[at:at + raw_size]).hexdigest()
    return result


def _metadata(reader, at):
    reader.check(at, 4, resource=True)
    size = reader.u32(at)
    if size < 28:
        raise Chr0Error("user-data block is smaller than header/dictionary")
    reader.check(at, size, resource=True)
    end = at + size
    reader.claim(at, 4, "metadata_header", "user-data")
    dictionary = reader.dictionary(at + 4, "user-data", end=end)
    entries = []
    for node in dictionary["nodes"][1:]:
        start = node["data_offset"]
        reader.check(start, 24, resource=True, end=end)
        length, relative, count, kind, name_relative, identity = struct.unpack_from(">Iiiiii", reader.data, start)
        if length < 24 or count < 0 or count > MAX_ROWS:
            raise Chr0Error("invalid user-data entry length/count")
        reader.check(start, length, resource=True, end=end)
        reader.claim(start, 24, "metadata_entry", f"metadata@{start:x}")
        entry = {"offset": start, "declared_size": length, "count": count, "type": kind,
                 "id": identity, "name": reader.string(start + name_relative) if name_relative else None,
                 "dictionary_name": node["name"], "data_relative": relative}
        if kind in (0, 1):
            reader.reserve_rows(count)
            pointer = start + relative
            reader.check(pointer, count * 4, resource=True, end=start + length)
            reader.claim(pointer, count * 4, "metadata_payload", f"metadata@{start:x}")
            entry["values"] = [reader.s32(pointer + i * 4) if kind == 0 else _value(reader.flt(pointer + i * 4))
                               for i in range(count)]
        elif kind == 2:
            if count > 1:
                raise Chr0Error("string user-data entry has more than one string")
            entry["text"] = reader.string(start + relative) if count else None
        else:
            reader.warnings.append(f"unknown user-data type {kind} at 0x{start:x}; bytes remain unclassified")
        entries.append(entry)
    return {"offset": at, "declared_size": size, "dictionary": dictionary, "entries": entries}


def _ledger(reader):
    # Only identical stream pointers and repeated string pointers are deduped
    # before this pass. Any other overlap is ambiguous and rejected.
    claims = sorted(reader.claims, key=lambda item: (item["offset"], item["end"]))
    regions, previous = [], 0
    for claim in claims:
        if claim["offset"] < previous:
            raise Chr0Error(f"conflicting/overlapping regions at 0x{claim['offset']:x} ({claim['kind']})")
        if claim["offset"] > previous:
            regions.append(_unknown(reader.data, previous, claim["offset"]))
        regions.append(claim)
        previous = claim["end"]
    if previous < reader.end:
        regions.append(_unknown(reader.data, previous, reader.end))
    # Split at the resource boundary so internal and external bytes reconcile.
    split = []
    for region in regions:
        points = [region["offset"]]
        if region["offset"] < reader.resource_end < region["end"]:
            points.append(reader.resource_end)
        points.append(region["end"])
        for left, right in zip(points, points[1:]):
            item = dict(region, offset=left, end=right, bytes=right - left,
                        inside_resource=left < reader.resource_end)
            if item["kind"] == "unclassified":
                item.update(_unknown(reader.data, left, right))
            split.append(item)
    totals = Counter()
    internal = Counter()
    for region in split:
        totals[region["kind"]] += region["bytes"]
        if region["inside_resource"]:
            internal[region["kind"]] += region["bytes"]
    if sum(totals.values()) != reader.end or sum(internal.values()) != reader.resource_end:
        raise Chr0Error("internal byte ledger failed to reconcile")
    return {"physical_bytes": reader.end, "resource_bytes": reader.resource_end,
            "outside_resource_bytes": reader.end - reader.resource_end,
            "by_kind": dict(sorted(totals.items())), "resource_by_kind": dict(sorted(internal.items())),
            "regions": split,
            "unknown_claim": "zero-filled unclassified gaps are observed, never assumed disposable padding"}


def _unknown(data, start, end):
    raw = data[start:end]
    return {"offset": start, "end": end, "bytes": end - start, "kind": "unclassified",
            "owner": None, "zero_filled": not any(raw), "sha256": hashlib.sha256(raw).hexdigest()}


def parse_chr0(data: bytes, scalar_arithmetic="binary32-steps"):
    """Return an independent deterministic physical report; raise Chr0Error on ambiguity."""
    reader = _Reader(data, scalar_arithmetic)
    reader.check(0, 16)
    if data[:4] != b"CHR0":
        raise Chr0Error("expected standalone big-endian CHR0 magic")
    resource_size, version, bres_relative = reader.u32(4), reader.u32(8), reader.s32(12)
    if version not in (4, 5):
        raise Chr0Error(f"unsupported CHR0 version {version}; only v4/v5 are audited")
    header_size = 40 if version == 4 else 44
    if resource_size < header_size or resource_size > len(data):
        raise Chr0Error("declared resource size is truncated or smaller than header")
    reader.resource_end = resource_size
    reader.check(0, header_size, resource=True)
    reader.claim(0, header_size, "resource_header", "CHR0")
    dictionary_at = reader.s32(16)
    info = 20 if version == 4 else 24
    name, original_path = reader.rel_string(0, info), reader.rel_string(0, info + 4)
    frames, entry_count = reader.u16(info + 8), reader.u16(info + 10)
    loop_raw, scaling_rule = reader.u32(info + 12), reader.u32(info + 16)
    if frames < 1:
        raise Chr0Error("zero frame duration is outside the current owned contract")
    if loop_raw not in (0, 1):
        reader.warnings.append("noncanonical wrap field; owned reader treats every nonzero value as loop")
    loop = bool(loop_raw)
    dictionary = reader.dictionary(dictionary_at, "bones")
    if dictionary["count"] != entry_count:
        raise Chr0Error("header/dictionary bone counts disagree")
    metadata_at = reader.s32(20) if version == 5 else 0
    metadata = _metadata(reader, metadata_at) if metadata_at else None
    entries, stream_by_offset = [], {}
    entry_offsets, names = set(), set()
    for node in dictionary["nodes"][1:]:
        start = node["data_offset"]
        if start in entry_offsets:
            raise Chr0Error("aliased bone tables are not admitted by the current document contract")
        entry_offsets.add(start)
        reader.check(start, 8, resource=True)
        entry_name = reader.rel_string(start, start)
        if entry_name is None or entry_name in names or entry_name != node["name"]:
            raise Chr0Error("missing/duplicate/mismatched bone dictionary name")
        names.add(entry_name)
        code = reader.u32(start + 4)
        slots, groups, cell = [], [], start + 8
        for group in range(3):
            exists = bool(code & (1 << (22 + group)))
            shared = group == 0 and bool(code & 16)
            fmt = (code >> (25, 27, 30)[group]) & (3, 7, 3)[group]
            item = {"group": ("scale", "rotation", "translation")[group], "exists": exists,
                    "format": FORMATS.get(fmt, f"Reserved({fmt})"), "scale_isotropic": shared,
                    "use_model": bool(code & (1 << (7 + group))),
                    "independent_presence_from_omit_flags": _independent_presence(code, group)}
            groups.append(item)
            if exists != item["independent_presence_from_omit_flags"]:
                reader.warnings.append(f"{entry_name}: {item['group']} existence/omit flags disagree across readers; no runtime interpretation claimed")
            if not exists:
                continue
            # Bits 5/6 disagree across the primary implementations; a present
            # group with either bit set has conflicting physical interpretations.
            if group in (1, 2) and code & (1 << (4 + group)):
                raise Chr0Error("ambiguous bits 5/6 on a present rotation/translation group")
            if fmt not in FORMATS:
                raise Chr0Error("reserved animation format")
            axes = 1 if shared else 3
            if shared and bool(code & (1 << 13)) != bool(code & (1 << 15)):
                raise Chr0Error("isotropic scale X/Z fixed bits disagree across readers")
            for axis in range(axes):
                channel = group * 3 + axis
                fixed = bool(code & (1 << (13 + (2 if shared else channel))))
                reader.check(cell, 4, resource=True)
                slot = {"offset": cell, "entry_base": start, "channel": CHANNELS[channel],
                        "channel_index": channel, "group": group,
                        "isotropic_channels": list(CHANNELS[:3]) if shared else [CHANNELS[channel]],
                        "fixed": fixed, "bytes": 4}
                if fixed:
                    slot["constant"] = _value(reader.flt(cell))
                else:
                    if fmt == 0:
                        raise Chr0Error("variable slot has None format")
                    if group != 1 and fmt > 3:
                        raise Chr0Error("baked formats cannot occur in scale/translation groups")
                    relative = reader.s32(cell)
                    target = start + relative
                    slot.update(relative=relative, stream_offset=target, pointer_base="bone entry")
                    if target not in stream_by_offset:
                        if len(stream_by_offset) >= MAX_STREAMS:
                            raise Chr0Error("stream bound exceeded")
                        stream_by_offset[target] = _stream(reader, target, fmt, frames, loop)
                    stream = stream_by_offset[target]
                    if stream["format"] != FORMATS[fmt]:
                        raise Chr0Error("same stream pointer has conflicting format aliases")
                    stream["references"].append({"bone": entry_name, **slot})
                    slot["stream_id"] = stream["id"]
                slots.append(slot)
                cell += 4
        reader.claim(start, 8, "bone_header", entry_name)
        for slot in slots:
            reader.claim(slot["offset"], 4, "fixed_cell" if slot["fixed"] else "reference_cell", entry_name + ":" + slot["channel"])
        entries.append({"name": entry_name, "offset": start, "table_bytes": cell - start,
                        "flags_hex": f"{code:08x}", "flags": _flags(code), "groups": groups, "slots": slots})
    streams = sorted(stream_by_offset.values(), key=lambda item: item["offset"])
    duplicate_buckets = {}
    for stream in streams:
        stream["reference_count"] = len(stream["references"])
        stream["logical_axis_count"] = sum(len(ref["isotropic_channels"]) for ref in stream["references"])
        key = (stream["format"], stream["raw_bytes"], stream["sha256"])
        duplicate_buckets.setdefault(key, []).append(stream)
    potentials = []
    for bucket in duplicate_buckets.values():
        if len(bucket) > 1:
            first = bucket[0]
            expected = data[first["offset"]:first["end"]]
            if any(data[item["offset"]:item["end"]] != expected for item in bucket[1:]):
                raise Chr0Error("stream digest collision; byte identity was not established")
            potentials.append({"format": bucket[0]["format"], "stream_offsets": [s["offset"] for s in bucket],
                               "identical_raw_stream_bytes": bucket[0]["raw_bytes"],
                               "duplicate_raw_bytes": sum(s["raw_bytes"] for s in bucket[1:]),
                               "claim": "byte-identical payload sharing opportunity only; no writer/game compatibility or net file saving claim"})
    return {"schema": SCHEMA, "read_only": True, "native_evaluator_used": False,
            "scalar_arithmetic": reader.scalar_arithmetic,
            "scalar_arithmetic_requested": scalar_arithmetic,
            "scalar_arithmetic_claim": "Explicit quantized base+q*step rounding profile; native-x86 aliases exact-single-round for the observed dump comparison, not a universal runtime guarantee.",
            "claim": "Physical standalone v4/v5 decoding/accounting; no motion-quality, game, optimizer or writer qualification.",
            "sha256": hashlib.sha256(data).hexdigest(),
            "header": {"version": version, "header_bytes": header_size, "resource_size": resource_size,
                       "bres_relative": bres_relative, "dictionary_offset": dictionary_at,
                       "user_data_offset": metadata_at, "name": name, "original_path": original_path,
                       "frame_duration": frames, "bone_count": entry_count, "loop_raw": loop_raw,
                       "loop": loop, "scaling_rule_raw": scaling_rule},
            "dictionary": dictionary, "entries": entries, "streams": streams,
            "metadata": metadata, "strings": sorted(reader.strings.values(), key=lambda item: item["offset"]),
            "byte_identical_alias_potential": potentials, "warnings": reader.warnings,
            "ledger": _ledger(reader)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, help="write deterministic JSON to this file; defaults to stdout")
    parser.add_argument("--scalar-arithmetic", choices=SCALAR_ARITHMETIC, default="binary32-steps",
                        help="quantized value rounding; native-x86 is an explicit alias of exact-single-round, not a runtime guarantee")
    args = parser.parse_args(argv)
    if args.output:
        for path in args.files:
            # Resolve symbolic paths and existing hardlinks before opening the
            # output. A read-only forensic command must never overwrite input.
            same = args.output.resolve() == path.resolve()
            try:
                same = same or (args.output.exists() and path.exists() and args.output.samefile(path))
            except OSError:
                pass
            if same:
                parser.error("--output must not identify any input file")
    reports, failed = [], False
    for path in args.files:
        try:
            # Check before allocation and again after the read to bound races.
            if path.stat().st_size > MAX_FILE_BYTES:
                raise Chr0Error("file exceeds 512 MiB bound")
            with path.open("rb") as handle:
                data = handle.read(MAX_FILE_BYTES + 1)
            reports.append({"file": str(path), "ok": True,
                            "report": parse_chr0(data, scalar_arithmetic=args.scalar_arithmetic)})
        except (OSError, Chr0Error) as error:
            failed = True
            reports.append({"file": str(path), "ok": False, "error": str(error)})
    text = json.dumps({"schema": SCHEMA, "files": reports}, sort_keys=True, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.write_text(text, encoding="utf-8", newline="\n")
    else:
        sys.stdout.write(text)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
