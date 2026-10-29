"""Original synthetic wire fixtures; no native writer, game data or BrawlLib."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "chr0_forensics.py"
SPEC = importlib.util.spec_from_file_location("chr0_forensics", MODULE_PATH)
chr0 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(chr0)


def put(data, offset, fmt, *values):
    struct.pack_into(">" + fmt, data, offset, *values)


def i12(rows, reserved=0):
    return struct.pack(">HHf", len(rows), reserved, 0.125) + b"".join(
        struct.pack(">fff", *row) for row in rows)


def i4(rows, step=0.125, base=0.0):
    # Literal field equation, independent of any production packer.
    return struct.pack(">HHfff", len(rows), 0, 0.125, step, base) + b"".join(
        struct.pack(">I", (time << 24) | (value << 12) | (tangent & 4095))
        for time, value, tangent in rows)


def i6(rows, step=0.125, base=0.0):
    return struct.pack(">HHfff", len(rows), 0, 0.125, step, base) + b"".join(
        struct.pack(">hHh", time, value, tangent) for time, value, tangent in rows)


def dense(fmt, samples, step=0.5, base=1.0):
    if fmt == 6:
        return b"".join(struct.pack(">f", item) for item in samples)
    return struct.pack(">ff", step, base) + b"".join(
        struct.pack(">B" if fmt == 4 else ">H", item) for item in samples)


def variable(fmt, payload, alias=None):
    return {"fmt": fmt, "payload": payload, "alias": alias}


def fixture(entries, *, version=5, frames=17, loop=False, metadata=False,
            footer_gap=b"\0\0\0", extra_flags=0, resource_tail=b""):
    """Build bytes from the documented independent field equations.

    entries: (name, groups, isotropic_scale); groups maps S/R/T index to
    three literal constants or variable format/payload dictionaries.
    """
    header = 40 if version == 4 else 44
    count = len(entries)
    directory = header
    data = bytearray(header + 24 + count * 16)
    info = {"entries": {}, "slots": {}, "streams": [], "directory": directory}
    records = []
    for name, groups, isotropic in entries:
        start = len(data)
        slots, code = [], 1 | extra_flags
        for group in range(3):
            if group not in groups:
                code |= 1 << (7 + group)
                continue
            code |= 1 << (22 + group)
            axes = groups[group]
            varying = [axis for axis in axes if isinstance(axis, dict)]
            if varying:
                code |= varying[0]["fmt"] << (25, 27, 30)[group]
            if group == 0 and isotropic:
                code |= 16
                axes = axes[:1]
            for axis, value in enumerate(axes):
                channel = group * 3 + axis
                if not isinstance(value, dict):
                    code |= 1 << (13 + channel)
                    if group == 0 and isotropic:
                        code |= (1 << 14) | (1 << 15)
                slots.append((channel, value))
        data.extend(b"\0" * (8 + 4 * len(slots)))
        put(data, start + 4, "I", code)
        info["entries"][name] = start
        records.append((name, start, slots))
    aliases = {}
    for name, start, slots in records:
        for index, (channel, value) in enumerate(slots):
            cell = start + 8 + 4 * index
            info["slots"][(name, channel)] = cell
            if isinstance(value, dict):
                key = value["alias"]
                if key is None or key not in aliases:
                    data.extend(b"\0" * (-len(data) % 4))
                    stream = len(data)
                    data.extend(value["payload"])
                    info["streams"].append(stream)
                    if key is not None:
                        aliases[key] = stream
                else:
                    stream = aliases[key]
                put(data, cell, "i", stream - start)
            else:
                put(data, cell, "f", value)
    metadata_fixups = []
    if metadata:
        if version != 5:
            raise ValueError("v5 metadata only")
        data.extend(b"\0" * (-len(data) % 4))
        meta = len(data)
        info["metadata"] = meta
        # Two metadata dictionary entries: signed integers and a string.
        meta_dict = meta + 4
        data.extend(b"\0" * (4 + 24 + 32))
        put(data, meta_dict, "II", 56, 2)
        put(data, meta_dict + 8, "H", 65535)
        for index, (label, values, kind) in enumerate((
                ("integers", [-7, 2147483647], 0), ("note", ["terminal"], 2))):
            start = len(data)
            length = 24 + (len(values) * 4 if kind == 0 else 0)
            data.extend(b"\0" * length)
            put(data, start, "Iiiiii", length, 24 if kind == 0 else 0, len(values), kind, 0, index)
            if kind == 0:
                for offset, value in enumerate(values):
                    put(data, start + 24 + offset * 4, "i", value)
            row = meta_dict + 24 + 16 * index
            put(data, row + 12, "i", start - meta_dict)
            metadata_fixups.extend([(meta_dict, row + 8, label), (start, start + 16, label)])
            if kind == 2:
                metadata_fixups.append((start, start + 4, values[0]))
        put(data, meta, "I", len(data) - meta)
        put(data, 20, "i", meta)
    data.extend(resource_tail)
    resource_size = len(data)
    data.extend(footer_gap)
    string_addresses = {}

    def string(text):
        if text not in string_addresses:
            data.extend(b"\0" * (-len(data) % 4))
            raw = text.encode("utf-8")
            data.extend(struct.pack(">I", len(raw)))
            string_addresses[text] = len(data)
            data.extend(raw + b"\0")
        return string_addresses[text]

    put(data, 0, "4sIIi", b"CHR0", resource_size, version, -4096)
    put(data, 16, "i", directory)
    hinfo = 20 if version == 4 else 24
    title = string("Independent CHR0 fixture")
    original = string("synthetic/source")
    put(data, hinfo, "iiHHII", title, original, frames, count, int(loop), 2)
    put(data, directory, "II", 24 + count * 16, count)
    put(data, directory + 8, "H", 65535)
    for index, (name, start, _) in enumerate(records):
        address = string(name)
        row = directory + 24 + index * 16
        put(data, row, "HHHHii", index, 0, 0, 0, address - directory, start - directory)
        put(data, start, "i", address - start)
    for base, cell, text in metadata_fixups:
        address = string(text)
        put(data, cell, "i", address - base)
    info.update(resource_size=resource_size, strings=string_addresses)
    return bytes(data), info


class Chr0ForensicsTests(unittest.TestCase):
    def parse(self, data):
        result = chr0.parse_chr0(data)
        ledger = result["ledger"]
        self.assertEqual(sum(ledger["by_kind"].values()), len(data))
        self.assertEqual(sum(ledger["resource_by_kind"].values()), result["header"]["resource_size"])
        self.assertEqual(sum(region["bytes"] for region in ledger["regions"]), len(data))
        return result

    def test_v4_v5_fixed_sparse_absent_and_all_flags(self):
        for version in (4, 5):
            with self.subTest(version=version):
                data, _ = fixture([("odd joint", {1: [-0.0, 720.0, 0.0]}, False)],
                                  version=version, extra_flags=(1 << 10) | (1 << 11) | (1 << 12))
                result = self.parse(data)
                self.assertEqual(result["header"]["header_bytes"], 40 if version == 4 else 44)
                self.assertEqual(result["header"]["bres_relative"], -4096)
                self.assertEqual(result["header"]["scaling_rule_raw"], 2)
                self.assertEqual(result["entries"][0]["slots"][0]["constant"]["bits"], "80000000")
                self.assertEqual(result["entries"][0]["slots"][1]["constant"]["value"], 720.0)
                self.assertEqual(len(result["entries"][0]["flags"]), 32)
                self.assertEqual([flag["bit"] for flag in result["entries"][0]["flags"]], list(range(32)))
                self.assertTrue(all(result["entries"][0]["flags"][bit]["set"] for bit in (10, 11, 12)))
                self.assertEqual(result["streams"], [])
                self.assertEqual(result["ledger"]["by_kind"]["fixed_cell"], 12)

    def test_i4_literal_bit_fields_frames_255_and_signed_tangent(self):
        payload = i4([(0, 0, -2048), (255, 4095, 2047)], base=-1)
        data, _ = fixture([("joint", {1: [variable(1, payload), 0, 0]}, False)], frames=256)
        result = self.parse(data)
        stream = result["streams"][0]
        self.assertEqual(stream["raw_bytes"], 24)
        self.assertEqual(stream["rows"][0]["tangent"], -64)
        self.assertEqual(stream["rows"][1]["tangent"], 63.96875)
        self.assertEqual(stream["rows"][1]["value"], 510.875)
        self.assertEqual(stream["rows"][1]["time"], 255)

    def test_i6_signed_s10_5_index_and_s7_8_tangent(self):
        data, _ = fixture([("joint", {2: [variable(2, i6([
            (-32768, 0, -32768), (8192, 8, 0), (32767, 65535, 32767)])), 0, 0]}, False)], frames=1025)
        stream = self.parse(data)["streams"][0]
        self.assertEqual([row["time"] for row in stream["rows"]], [-1024.0, 256.0, 1023.96875])
        self.assertEqual([row["tangent"] for row in stream["rows"]], [-128.0, 0.0, 127.99609375])
        self.assertFalse(stream["current_nonnegative_writer_timing"])
        # 0x8000 is -1024 in S10.5; it never means positive frame 1024.
        self.assertEqual(stream["rows"][0]["quantized_time"], -32768)

    def test_i12_fractional_1024_paired_sides_and_720_turn(self):
        rows = [(0, -0.0, -0.0), (0.5, 360, 1), (0.5, 361, -2), (1024, 720, 0)]
        data, _ = fixture([("joint", {1: [variable(3, i12(rows, reserved=0x1234)), 0, 0]}, False)], frames=1025)
        stream = self.parse(data)["streams"][0]
        self.assertEqual(stream["reserved_u16"], 0x1234)
        self.assertEqual(stream["rows"][-1]["value"], 720)
        self.assertEqual(stream["rows"][-1]["time"], 1024)
        self.assertEqual(stream["rows"][0]["value_bits"], "80000000")
        self.assertEqual(stream["rows"][0]["tangent_bits"], "80000000")
        self.assertFalse(stream["paired_sites"][0]["same_value_bits"])
        self.assertEqual(stream["paired_sites"][0]["incoming_row"], 1)
        self.assertEqual(stream["paired_sites"][0]["outgoing_row"], 2)

    def test_all_dense_formats_terminal_samples_and_projection(self):
        for fmt in (4, 5, 6):
            for loop in (False, True):
                with self.subTest(fmt=fmt, loop=loop):
                    values = [1.0, 1.5, 2.5, 9.0] if fmt == 6 else [0, 1, 3, 16]
                    data, _ = fixture([("dense", {1: [variable(fmt, dense(fmt, values)), 0, 0]}, False)], frames=3, loop=loop)
                    stream = self.parse(data)["streams"][0]
                    self.assertEqual(stream["stored_count"], 4)
                    self.assertEqual([sample["value"] for sample in stream["samples"]], [1, 1.5, 2.5, 9])
                    self.assertEqual(stream["projected_sample_count"], 4 if loop else 3)
                    self.assertEqual(stream["terminal_sample"]["value"], 9)
                    self.assertEqual(stream["terminal_sample"]["projected_by_owned_reader"], loop)
                    self.assertEqual(stream["projected_rows"][0]["tangent"], 0.5)
                    frame1 = [row for row in stream["projected_rows"] if row["time"] == 1]
                    self.assertEqual([row["tangent"] for row in frame1], [0.5, 1.0])
                    self.assertEqual(stream["raw_bytes"], (0 if fmt == 6 else 8) + 4 * {4: 1, 5: 2, 6: 4}[fmt])

    def test_one_frame_dense_still_owns_two_samples(self):
        data, _ = fixture([("one", {1: [variable(4, dense(4, [3, 99])), 0, 0]}, False)], frames=1)
        stream = self.parse(data)["streams"][0]
        self.assertEqual(stream["stored_count"], 2)
        self.assertEqual(stream["projected_key_count"], 1)
        self.assertEqual(stream["samples"][1]["value"], 50.5)

    def test_scale_isotropy_and_shared_cross_bone_pointer(self):
        payload = i12([(0, 1, 0), (16, 2, 0)])
        track = variable(3, payload, alias="shared")
        data, info = fixture([("scale", {0: [track, track, track]}, True),
                              ("other", {2: [track, 0, 0]}, False)])
        result = self.parse(data)
        self.assertEqual(len(result["streams"]), 1)
        self.assertEqual(result["streams"][0]["reference_count"], 2)
        self.assertEqual(result["streams"][0]["logical_axis_count"], 4)
        self.assertEqual(result["entries"][0]["table_bytes"], 12)
        self.assertEqual(result["entries"][0]["slots"][0]["isotropic_channels"], ["SX", "SY", "SZ"])
        self.assertEqual(result["ledger"]["by_kind"]["curve_payload"], 24)
        self.assertEqual(result["streams"][0]["offset"], info["streams"][0])

    def test_duplicate_bytes_are_potential_not_double_counted_alias(self):
        payload = i4([(0, 0, 0), (16, 8, 0)])
        data, _ = fixture([("joint", {1: [variable(1, payload), variable(1, payload), 0]}, False)])
        result = self.parse(data)
        self.assertEqual(len(result["streams"]), 2)
        self.assertEqual(result["byte_identical_alias_potential"][0]["duplicate_raw_bytes"], 24)
        self.assertEqual(result["ledger"]["by_kind"]["curve_payload"], 16)

    def test_metadata_footer_alignment_and_unknown_nonzero_regions(self):
        data, info = fixture([("joint", {0: [1, 1, 1]}, True)], metadata=True,
                             resource_tail=b"opaque\0", footer_gap=b"\0\0\0")
        result = self.parse(data)
        entries = result["metadata"]["entries"]
        self.assertEqual(entries[0]["values"], [-7, 2147483647])
        self.assertEqual(entries[1]["text"], "terminal")
        self.assertEqual(result["header"]["resource_size"], info["resource_size"])
        self.assertGreater(result["ledger"]["outside_resource_bytes"], 0)
        self.assertTrue(all(string["outside_resource"] for string in result["strings"]))
        self.assertTrue(any(region["kind"] == "unclassified" and not region["zero_filled"]
                            for region in result["ledger"]["regions"]))
        self.assertTrue(any(region["kind"] == "unclassified" and region["zero_filled"]
                            for region in result["ledger"]["regions"]))

    def test_rejects_truncation_and_does_not_read_payload_into_footer(self):
        data, info = fixture([("joint", {1: [variable(4, dense(4, [0, 1, 3, 9])), 0, 0]}, False)], frames=3)
        with self.assertRaises(chr0.Chr0Error):
            chr0.parse_chr0(data[:info["resource_size"] - 1])
        mutated = bytearray(data)
        put(mutated, 4, "I", info["streams"][0] + 8 + 3)
        with self.assertRaisesRegex(chr0.Chr0Error, "out-of-bounds"):
            chr0.parse_chr0(bytes(mutated))

    def test_rejects_nonfinite_typed_fields(self):
        for field in ("time", "value", "tangent", "frame_scale", "fixed"):
            with self.subTest(field=field):
                data, info = fixture([("joint", {1: [variable(3, i12([(0, 0, 0), (16, 1, 0)])), 0, 0]}, False)])
                mutated = bytearray(data)
                at = (info["streams"][0] + {"time": 8, "value": 12, "tangent": 16, "frame_scale": 4}[field]
                      if field != "fixed" else info["slots"][("joint", 4)])
                put(mutated, at, "I", 0x7FC00000)
                with self.assertRaisesRegex(chr0.Chr0Error, "nonfinite"):
                    chr0.parse_chr0(bytes(mutated))

    def test_rejects_conflicting_alias_and_partial_overlap(self):
        payload = i4([(0, 0, 0), (16, 8, 0)])
        data, info = fixture([("first", {1: [variable(1, payload), 0, 0]}, False),
                              ("second", {1: [variable(2, i6([(0, 0, 0), (512, 8, 0)])), 0, 0]}, False)])
        mutated = bytearray(data)
        put(mutated, info["slots"][("second", 3)], "i", info["streams"][0] - info["entries"]["second"])
        with self.assertRaisesRegex(chr0.Chr0Error, "conflicting format"):
            chr0.parse_chr0(bytes(mutated))
        data, info = fixture([("joint", {1: [variable(3, i12([(0, 0, 0), (16, 1, 0)])), 0, 0]}, False)])
        mutated = bytearray(data)
        # A variable payload starts inside its own fixed table cell.
        put(mutated, info["slots"][("joint", 3)], "i", 12)
        with self.assertRaises(chr0.Chr0Error):
            chr0.parse_chr0(bytes(mutated))

    def test_rejects_dictionary_count_links_and_pointer_bounds(self):
        data, info = fixture([("joint", {1: [0, 0, 0]}, False)])
        mutations = [(info["directory"] + 4, "I", 65536),
                     (info["directory"] + 12, "H", 2),
                     (info["directory"] + 24 + 12, "i", -65536),
                     (24, "i", len(data) + 1)]
        for offset, fmt, value in mutations:
            with self.subTest(offset=offset):
                mutated = bytearray(data)
                put(mutated, offset, fmt, value)
                with self.assertRaises(chr0.Chr0Error):
                    chr0.parse_chr0(bytes(mutated))

    def test_rejects_ambiguous_isotropic_flags_without_guessing(self):
        data, _ = fixture([("joint", {1: [0, 0, 0]}, False)], extra_flags=1 << 5)
        with self.assertRaisesRegex(chr0.Chr0Error, "ambiguous bits 5/6"):
            chr0.parse_chr0(data)
        data, info = fixture([("scale", {0: [1, 1, 1]}, True)])
        mutated = bytearray(data)
        code = struct.unpack_from(">I", mutated, info["entries"]["scale"] + 4)[0]
        put(mutated, info["entries"]["scale"] + 4, "I", code ^ (1 << 15))
        with self.assertRaisesRegex(chr0.Chr0Error, "X/Z fixed"):
            chr0.parse_chr0(bytes(mutated))

    def test_rejects_unordered_or_three_sided_knots(self):
        for rows in ([(1, 0, 0), (0, 1, 0)], [(0, 0, 0), (0, 0, 1), (0, 0, 2)]):
            data, _ = fixture([("joint", {1: [variable(3, i12(rows)), 0, 0]}, False)])
            with self.assertRaises(chr0.Chr0Error):
                chr0.parse_chr0(data)

    def test_unrecognized_version_magic_and_zero_duration(self):
        data, _ = fixture([])
        for offset, fmt, value in ((0, "4s", b"SRT0"), (8, "I", 3), (32, "H", 0)):
            mutated = bytearray(data)
            put(mutated, offset, fmt, value)
            with self.assertRaises(chr0.Chr0Error):
                chr0.parse_chr0(bytes(mutated))

    def test_cli_json_determinism_read_only_and_errors(self):
        data, _ = fixture([])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "synthetic.chr0"
            output = Path(folder) / "report.json"
            path.write_bytes(data)
            self.assertEqual(chr0.main([str(path), "--output", str(output)]), 0)
            first = output.read_bytes()
            self.assertEqual(chr0.main([str(path), "--output", str(output)]), 0)
            self.assertEqual(first, output.read_bytes())
            self.assertEqual(path.read_bytes(), data)
            self.assertTrue(json.loads(first)["files"][0]["ok"])
            with contextlib.redirect_stdout(io.StringIO()) as stream:
                self.assertEqual(chr0.main([str(Path(folder) / "missing.chr0")]), 1)
            self.assertFalse(json.loads(stream.getvalue())["files"][0]["ok"])

    def test_output_cannot_overwrite_input(self):
        data, _ = fixture([])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "synthetic.chr0"
            path.write_bytes(data)
            with contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as error:
                    chr0.main([str(path), "--output", str(path)])
            self.assertEqual(error.exception.code, 2)
            self.assertEqual(path.read_bytes(), data)

    def test_row_and_file_work_limits(self):
        data, _ = fixture([("joint", {1: [variable(3, i12([(0, 0, 0), (16, 1, 0)])), 0, 0]}, False)])
        with patch.object(chr0, "MAX_ROWS", 1):
            with self.assertRaisesRegex(chr0.Chr0Error, "row/sample bound"):
                chr0.parse_chr0(data)
        with patch.object(chr0, "MAX_FILE_BYTES", len(data) - 1):
            with self.assertRaisesRegex(chr0.Chr0Error, "file exceeds"):
                chr0.parse_chr0(data)

    def test_omit_flags_disagreement_is_explicit(self):
        data, _ = fixture([("joint", {1: [0, 0, 0]}, False)], extra_flags=1 << 8)
        result = self.parse(data)
        self.assertTrue(any("existence/omit flags disagree" in warning for warning in result["warnings"]))
        self.assertFalse(result["entries"][0]["groups"][1]["independent_presence_from_omit_flags"])

    def test_flag_decoder_keeps_every_raw_bit(self):
        self.assertTrue(all(flag["set"] for flag in chr0._flags(0xFFFFFFFF)))
        self.assertFalse(any(flag["set"] for flag in chr0._flags(0)))
        for bit in range(32):
            self.assertEqual([item["bit"] for item in chr0._flags(1 << bit) if item["set"]], [bit])

    def test_nonfinite_quantizers_and_dense_samples_are_rejected(self):
        for fmt, payload, relative in (
                (1, i4([(0, 0, 0), (16, 8, 0)]), 8),
                (2, i6([(0, 0, 0), (512, 8, 0)]), 12),
                (4, dense(4, [0, 1, 2]), 0),
                (5, dense(5, [0, 1, 2]), 4),
                (6, dense(6, [0, 1, 2]), 8)):
            with self.subTest(fmt=fmt):
                data, info = fixture([("joint", {1: [variable(fmt, payload), 0, 0]}, False)], frames=2)
                mutated = bytearray(data)
                put(mutated, info["streams"][0] + relative, "I", 0x7F800000)
                with self.assertRaisesRegex(chr0.Chr0Error, "nonfinite"):
                    chr0.parse_chr0(bytes(mutated))

    def test_metadata_floats_signed_zero_unknown_types_and_extent_bounds(self):
        data, info = fixture([], metadata=True)
        # Independent fixture layout: block length (4), dictionary (56), entry.
        entry = info["metadata"] + 60
        mutated = bytearray(data)
        put(mutated, entry + 12, "i", 1)
        put(mutated, entry + 24, "ff", -0.0, 1.25)
        report = self.parse(bytes(mutated))
        self.assertEqual(report["metadata"]["entries"][0]["values"], [
            {"value": -0.0, "bits": "80000000"}, {"value": 1.25, "bits": "3fa00000"}])
        put(mutated, entry + 28, "I", 0x7F800000)
        with self.assertRaisesRegex(chr0.Chr0Error, "nonfinite"):
            chr0.parse_chr0(bytes(mutated))
        mutated = bytearray(data)
        put(mutated, entry + 12, "i", 17)
        report = self.parse(bytes(mutated))
        self.assertTrue(any("unknown user-data type 17" in warning for warning in report["warnings"]))
        self.assertTrue(any(region["kind"] == "unclassified" and region["offset"] == entry + 24
                            for region in report["ledger"]["regions"]))
        put(mutated, entry, "I", 0x7FFFFFFF)
        with self.assertRaisesRegex(chr0.Chr0Error, "out-of-bounds"):
            chr0.parse_chr0(bytes(mutated))

    def test_explicit_arithmetic_profiles_literal_cancellation(self):
        # (1+2^-23)*4095 -4095 = 4095*2^-23 exactly. One rounding gives
        # 0x39fff000; rounding the product first gives 2^-11, 0x3a000000.
        step = struct.unpack(">f", bytes.fromhex("3f800001"))[0]
        payload = i4([(0, 4095, 0), (16, 0, 0)], step=step, base=-4095.0)
        data, _ = fixture([("joint", {1: [variable(1, payload), 0, 0]}, False)])
        default = chr0.parse_chr0(data)
        single = chr0.parse_chr0(data, scalar_arithmetic="exact-single-round")
        native_alias = chr0.parse_chr0(data, scalar_arithmetic="native-x86")
        self.assertEqual(default["scalar_arithmetic"], "binary32-steps")
        self.assertEqual(default["streams"][0]["rows"][0]["value_bits"], "3a000000")
        row = single["streams"][0]["rows"][0]
        self.assertEqual(row["value_bits"], "39fff000")
        self.assertEqual(row["arithmetic_values"]["binary32-steps"]["bits"], "3a000000")
        self.assertEqual(row["arithmetic_values"]["exact-single-round"]["bits"], "39fff000")
        self.assertEqual(row["quantized_value"], 4095)
        self.assertEqual(single["streams"][0]["step"]["bits"], "3f800001")
        self.assertEqual(native_alias["scalar_arithmetic_requested"], "native-x86")
        self.assertEqual(native_alias["scalar_arithmetic"], "exact-single-round")
        self.assertEqual(native_alias["streams"], single["streams"])
        self.assertEqual(default["ledger"], single["ledger"])
        self.assertEqual(default["sha256"], single["sha256"])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "cancellation.chr0"
            path.write_bytes(data)
            with contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(chr0.main([str(path), "--scalar-arithmetic", "exact-single-round"]), 0)
            cli_report = json.loads(output.getvalue())["files"][0]["report"]
            self.assertEqual(cli_report["scalar_arithmetic"], "exact-single-round")
            self.assertEqual(cli_report["streams"][0]["rows"][0]["value_bits"], "39fff000")

    def test_exact_dyadic_rounding_ties_subnormals_and_no_binary64_rounding(self):
        cases = [((1 << 24) + 1, -24, "3f800000"),  # tie -> even lower
                 ((1 << 24) + 3, -24, "3f800002"),  # tie -> even upper
                 (1, -150, "00000000"),  # half the least subnormal
                 (3, -150, "00000002"),
                 (-1, -150, "80000000"),
                 ((1 << 23) - 1, -149, "007fffff"),
                 (1 << 23, -149, "00800000"),
                 ((1 << 24) - 1, 104, "7f7fffff"),
                 # Infinitesimally above an f32 midpoint. Converting through
                 # binary64 first loses bit -80 and would choose 0x3f800000.
                 ((1 << 80) + (1 << 56) + 1, -80, "3f800001")]
        for numerator, exponent, expected in cases:
            with self.subTest(numerator=numerator, exponent=exponent):
                actual = chr0._round_dyadic(numerator, exponent)
                self.assertEqual(struct.pack(">f", actual).hex(), expected)
        with self.assertRaisesRegex(chr0.Chr0Error, "nonfinite"):
            chr0._round_dyadic((1 << 25) - 1, 103)

    def test_single_round_dense_samples_and_projection_use_selected_values(self):
        # 255*(1+2^-23)-255 = 255*2^-23: literal 0x37ff0000.
        # With a separately rounded product the result is 0x38000000.
        step = struct.unpack(">f", bytes.fromhex("3f800001"))[0]
        data, _ = fixture([("joint", {1: [variable(4, dense(4, [255, 0, 255], step=step, base=-255)), 0, 0]}, False)], frames=2)
        report = chr0.parse_chr0(data, scalar_arithmetic="exact-single-round")
        stream = report["streams"][0]
        self.assertEqual(stream["samples"][0]["bits"], "37ff0000")
        self.assertEqual(stream["samples"][0]["quantized_value"], 255)
        self.assertEqual(stream["samples"][0]["arithmetic_values"]["binary32-steps"]["bits"], "38000000")
        self.assertEqual(stream["projected_rows"][0]["value_bits"], "37ff0000")
        self.assertEqual(stream["terminal_sample"]["bits"], "37ff0000")

    def test_exact_single_round_retains_ieee_zero_addition_signs(self):
        for base, integer, step, expected in [
                (-0.0, 0, -1.0, "80000000"), (-0.0, 3, -0.0, "80000000"),
                (0.0, 3, -0.0, "00000000"), (-1.0, 1, 1.0, "00000000")]:
            self.assertEqual(struct.pack(">f", chr0._decoded(base, integer, step, "exact-single-round")).hex(), expected)
        with self.assertRaisesRegex(chr0.Chr0Error, "unknown scalar arithmetic"):
            chr0.parse_chr0(fixture([])[0], scalar_arithmetic="implicit-native")

    def test_single_round_i6_l2_and_finite_cancellation_after_product_overflow(self):
        step = struct.unpack(">f", bytes.fromhex("3f800001"))[0]
        for fmt, payload in ((2, i6([(0, 65535, 0), (512, 0, 0)], step=step, base=-65535)),
                             (5, dense(5, [65535, 0, 65535], step=step, base=-65535))):
            data, _ = fixture([("joint", {1: [variable(fmt, payload), 0, 0]}, False)], frames=2)
            stream = chr0.parse_chr0(data, scalar_arithmetic="exact-single-round")["streams"][0]
            value = stream["rows"][0] if fmt == 2 else stream["samples"][0]
            self.assertEqual(value.get("value_bits", value.get("bits")), "3bffff00")
            self.assertEqual(value["arithmetic_values"]["binary32-steps"]["bits"], "3c000000")
        largest = struct.unpack(">f", bytes.fromhex("7f7fffff"))[0]
        data, _ = fixture([("joint", {1: [variable(2, i6([(0, 2, 0), (512, 1, 0)], step=largest, base=-largest)), 0, 0]}, False)])
        with self.assertRaisesRegex(chr0.Chr0Error, "nonfinite"):
            chr0.parse_chr0(data)
        stream = chr0.parse_chr0(data, scalar_arithmetic="exact-single-round")["streams"][0]
        self.assertEqual(stream["rows"][0]["value_bits"], "7f7fffff")
        self.assertIn("error", stream["rows"][0]["arithmetic_values"]["binary32-steps"])

    def test_typed_partial_overlap_is_rejected_after_both_decode(self):
        tiny = struct.unpack(">f", bytes.fromhex("00020000"))[0]
        # Stream B at A+8 decodes its header from A's first time/value,
        # and two valid rows from the overlapping remaining A coefficients.
        payload = i12([(tiny, 0.125, 0), (0, 0, 1), (1, 1, 0)])
        data, info = fixture([("joint", {1: [variable(3, payload), variable(3, payload), 0]}, False)])
        mutated = bytearray(data)
        put(mutated, info["slots"][("joint", 4)], "i", info["streams"][0] + 8 - info["entries"]["joint"])
        # A's own original rows are ordered; B decodes at times 0 and 1.
        # Change A's second time to 0.25 so A remains monotonic, but preserve
        # B's first time (A's first tangent = 0).
        put(mutated, info["streams"][0] + 20, "f", 0.25)
        with self.assertRaisesRegex(chr0.Chr0Error, "overlapping regions"):
            chr0.parse_chr0(bytes(mutated))


if __name__ == "__main__":
    unittest.main()
