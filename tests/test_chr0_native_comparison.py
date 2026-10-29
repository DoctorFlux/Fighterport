"""Original synthetic native-dump mappings; no native reader/writer/process."""
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


wire = load("comparison_wire_fixtures", ROOT / "tests" / "test_chr0_forensics.py")
compare = load("native_comparison", ROOT / "tools" / "compare_chr0_forensics.py")
NAMES = ("SX", "SY", "SZ", "RX", "RY", "RZ", "TX", "TY", "TZ")
FORMATS = ("None", "I4", "I6", "I12", "L1", "L2", "L4")
SHA = lambda data: hashlib.sha256(data).hexdigest()
BITS = lambda value: struct.unpack(">I", struct.pack(">f", value))[0]


def row(index, time, value, tangent):
    result = {"Index": index}
    for name, coefficient in (("Time", time), ("Value", value), ("Tangent", tangent)):
        result[name + "Bits"] = BITS(coefficient)
        result[name + "Hex"] = f"{BITS(coefficient):08x}"
    return result


def native_dump(data, info, rows, retained=None):
    """Author dump structure from supplied literal native rows, not parse_chr0."""
    retained = retained or {}
    version = struct.unpack_from(">I", data, 8)[0]
    hinfo = 20 if version == 4 else 24
    frames, _, loop_raw = struct.unpack_from(">HHI", data, hinfo + 8)
    loop = bool(loop_raw)
    entries = []
    for index, (name, offset) in enumerate(info["entries"].items()):
        code = struct.unpack_from(">I", data, offset + 4)[0]
        channels = []
        for channel in range(9):
            values = rows.get((name, channel), [])
            channels.append({"Index": channel, "Name": NAMES[channel],
                "GroupExists": bool(code & (1 << (22 + channel // 3))),
                "FixedFlag": bool(code & (1 << (13 + channel))),
                "FrameLimit": frames + int(loop), "Loop": loop, "KeyCount": len(values),
                "Rows": [row(i, *values[i]) for i in range(len(values))],
                "RetainedStorage": copy.deepcopy(retained.get((name, channel)))})
        plans = [channel["RetainedStorage"] for channel in channels]
        groups = [{"Index": g, "Name": ("Scale", "Rotation", "Translation")[g],
            "Exists": bool(code & (1 << (22 + g))), "Isotropic": bool(code & (1 << (4 + g))),
            "FormatCode": (code >> (25, 27, 30)[g]) & (3, 7, 3)[g],
            "Format": FORMATS[(code >> (25, 27, 30)[g]) & (3, 7, 3)[g]]} for g in range(3)]
        entries.append({"Index": index, "Name": name, "RawFlags": code, "RawFlagsHex": f"{code:08x}",
            "UseModelScale": bool(code & (1 << 7)), "UseModelRotate": bool(code & (1 << 8)),
            "UseModelTranslate": bool(code & (1 << 9)), "ScaleCompensateApply": bool(code & (1 << 10)),
            "ScaleCompensateParent": bool(code & (1 << 11)), "ClassicScaleOff": bool(code & (1 << 12)),
            "PreserveInterpolation": any(g["Exists"] and g["FormatCode"] in (3, 4, 5, 6) for g in groups),
            "FrameLimit": frames + int(loop), "Loop": loop,
            "StorageCaptureAvailable": any(plan is not None for plan in plans),
            "StorageContentKey": "|".join(plan["ContentKey"] if plan else "I12" for plan in plans)
                if any(plan is not None for plan in plans) else None,
            "Groups": groups, "Channels": channels})
    backing = info["resource_size"]
    return {"ReadOnly": True, "NativeReader": "NodeFactory/CHR0Node.KeyArrays", "WriterUsed": False,
        "ModelLoadedOrPosed": False, "PrivateDesktop": True, "OSInputInjected": False,
        "ScreenCopied": False, "FreshReadsIdentical": True,
        "Model": {"Path": "synthetic-context.mdl0", "Sha256Before": "a" * 64, "Sha256After": "a" * 64},
        "Motion": {"Path": "synthetic.chr0", "FileBytes": len(data), "Sha256Before": SHA(data),
            "Sha256After": SHA(data), "BackingSha256Before": SHA(data[:backing]),
            "BackingSha256After": SHA(data[:backing])},
        "Projection": {"Name": "Independent CHR0 fixture", "Version": version, "Frames": frames,
            "Loop": loop, "OriginalPath": "synthetic/source", "BackingLength": backing, "Entries": entries}}


def compact_plan(data, offset, payload_length, fmt, rows, *, samples=None,
                 base=1.0, step=0.125, loop=False, frames=3):
    """Expected CaptureStorage metadata from authored constants and raw extent."""
    dense = samples is not None
    length = payload_length if dense else (payload_length + 3) & ~3
    key = f"{fmt}:" + (f"{len(rows)}:{len(samples)}:" if dense else "") + SHA(data[offset:offset + length]).upper()
    return {"Format": FORMATS[fmt], "FormatCode": fmt, "IsDense": dense,
        "KeyCount": len(rows), "NativeSampleCount": frames + int(loop) if dense else 0,
        "EncodedSampleCount": len(samples) if dense else 0, "ByteCount": length, "ContentKey": key,
        "BaseBits": BITS(0.0 if fmt == 6 else base), "StepBits": BITS(1.0 if fmt == 6 else step),
        "FrameScaleBits": BITS(0.0 if dense else 0.125), "MatchesNativeCurve": True,
        "EncodedSampleValueBits": [BITS(value) for value in samples] if dense else None,
        "TerminalSampleValueBits": BITS(samples[-1]) if dense else None,
        "TerminalSampleProjected": loop if dense else None}


class Chr0NativeComparisonTests(unittest.TestCase):
    def assert_matches(self, data, dump):
        result = compare.compare_native_dump(data, dump)
        self.assertTrue(result["ok"], json.dumps(result["differences"], indent=2))
        self.assertEqual(result["difference_count"], 0)
        self.assertFalse(result["native_process_invoked"])
        return result

    def fixed_case(self, *, isotropic=False, version=5):
        group = 0 if isotropic else 1
        constants = [1.25] * 3 if isotropic else [-0.0, 720.0, 0.0]
        data, info = wire.fixture([("anonymous", {group: constants}, isotropic)], version=version,
                                  extra_flags=(1 << 10) | (1 << 11) | (1 << 12))
        rows = {("anonymous", group * 3 + axis): [(0, value, 0)] for axis, value in enumerate(constants)}
        return data, native_dump(data, info, rows)

    def format_case(self, fmt, *, loop=False):
        if fmt == 1:
            payload = wire.i4([(0, 0, -2), (2, 8, 4)], base=1)
            rows = [(0, 1, -0.0625), (2, 2, 0.125)]
        elif fmt == 2:
            payload = wire.i6([(0, 0, -256), (16, 4, 128), (64, 8, 0)], base=1)
            rows = [(0, 1, -1), (0.5, 1.5, 0.5), (2, 2, 0)]
        elif fmt == 3:
            rows = [(0, -0.0, -0.0), (.5, 360, 1), (.5, 361, -2), (2, 720, 0)]
            payload = wire.i12(rows)
        else:
            samples = [1, 1.5, 2.5, 9]
            payload = wire.dense(fmt, samples if fmt == 6 else [0, 1, 3, 16])
            rows = [(0, 1, .5), (1, 1.5, .5), (1, 1.5, 1), (2, 2.5, 1)]
            if loop:
                rows += [(2, 2.5, 6.5), (3, 9, 6.5)]
        data, info = wire.fixture([("anonymous", {1: [wire.variable(fmt, payload), 0, 0]}, False)], frames=3, loop=loop)
        authored = {("anonymous", 3): rows, ("anonymous", 4): [(0, 0, 0)], ("anonymous", 5): [(0, 0, 0)]}
        plans = {}
        if fmt != 3:
            plans[("anonymous", 3)] = compact_plan(data, info["streams"][0], len(payload), fmt, rows,
                samples=samples if fmt >= 4 else None, step=.5 if fmt >= 4 else .125, loop=loop)
        return data, native_dump(data, info, authored, plans)

    def test_fixed_absent_semantic_flags_signed_zero_and_v4_v5(self):
        for version in (4, 5):
            with self.subTest(version=version):
                data, dump = self.fixed_case(version=version)
                result = self.assert_matches(data, dump)
                self.assertEqual(result["counts"]["expected_rows"], 3)
                self.assertEqual(result["counts"]["native_actual_rows"], 3)
                self.assertEqual(result["counts"]["fixed_channels"], 3)
                self.assertEqual(result["counts"]["absent_channels"], 6)

    def test_all_six_codecs_and_complete_retained_metadata(self):
        for fmt in range(1, 7):
            with self.subTest(fmt=FORMATS[fmt]):
                data, dump = self.format_case(fmt)
                result = self.assert_matches(data, dump)
                self.assertEqual(result["counts"]["physical_streams_by_format"], {FORMATS[fmt]: 1})
                self.assertEqual(result["counts"]["retained_compact_channels"], int(fmt != 3))
                self.assertEqual(result["counts"]["retained_encoded_samples"], 4 if fmt >= 4 else 0)

    def test_loop_sample_terminal_is_a_native_row(self):
        for fmt in (4, 5, 6):
            with self.subTest(fmt=fmt):
                data, dump = self.format_case(fmt, loop=True)
                result = self.assert_matches(data, dump)
                self.assertEqual(result["counts"]["expected_rows"], 8)

    def test_fixed_and_indexed_isotropic_scale_expands_to_three_axes(self):
        data, dump = self.fixed_case(isotropic=True)
        result = self.assert_matches(data, dump)
        self.assertEqual(result["counts"]["isotropically_expanded_axes"], 2)
        payload = wire.i4([(0, 0, 0), (2, 8, 0)], base=1)
        value = wire.variable(1, payload)
        data, info = wire.fixture([("isotropic", {0: [value, value, value]}, True)], frames=3)
        values = [(0, 1, 0), (2, 2, 0)]
        storage = compact_plan(data, info["streams"][0], len(payload), 1, values)
        dump = native_dump(data, info, {("isotropic", axis): values for axis in range(3)},
                           {("isotropic", axis): storage for axis in range(3)})
        result = self.assert_matches(data, dump)
        self.assertEqual(result["counts"]["expected_rows"], 6)
        self.assertEqual(result["counts"]["retained_compact_channels"], 3)

    def test_changed_bit_including_signed_zero_is_a_mismatch(self):
        data, original = self.fixed_case()
        for field, value in (("Time", 0x80000000), ("Value", 0), ("Tangent", 0x80000000)):
            with self.subTest(field=field):
                dump = copy.deepcopy(original)
                actual = dump["Projection"]["Entries"][0]["Channels"][3]["Rows"][0]
                actual[field + "Bits"], actual[field + "Hex"] = value, f"{value:08x}"
                result = compare.compare_native_dump(data, dump)
                self.assertFalse(result["ok"])
                self.assertTrue(any(field + "Bits" in item["path"] for item in result["differences"]))

    def test_source_hash_and_native_readonly_fresh_guards_are_required(self):
        data, original = self.fixed_case()
        for field, invalid in (("ReadOnly", False), ("WriterUsed", True),
                               ("FreshReadsIdentical", False), ("OSInputInjected", True),
                               ("PrivateDesktop", False), ("ScreenCopied", True)):
            dump = copy.deepcopy(original); dump[field] = invalid
            self.assertFalse(compare.compare_native_dump(data, dump)["ok"], field)
        for field in ("Sha256Before", "Sha256After", "BackingSha256Before", "BackingSha256After"):
            dump = copy.deepcopy(original); dump["Motion"][field] = "0" * 64
            self.assertFalse(compare.compare_native_dump(data, dump)["ok"], field)
        dump = copy.deepcopy(original); dump["Model"]["Sha256After"] = "b" * 64
        self.assertFalse(compare.compare_native_dump(data, dump)["ok"])

    def test_explicit_exact_profile_uses_literal_ieee_witness_without_tolerance(self):
        # Independently established exact dyadic witness: base+44*step is
        # -36100105/4194304. One nearest/even binary32 round is c109b602;
        # separately rounding multiplication/addition gives c109b604.
        base = struct.unpack(">f", bytes.fromhex("c22c1a08"))[0]
        step = struct.unpack(">f", bytes.fromhex("3f4840c5"))[0]
        single = struct.unpack(">f", bytes.fromhex("c109b602"))[0]
        payload = wire.i4([(0, 44, 0), (2, 44, 0)], base=base, step=step)
        data, info = wire.fixture([("literal", {1: [wire.variable(1, payload), 0, 0]}, False)], frames=3)
        values = [(0, single, 0), (2, single, 0)]
        plan = compact_plan(data, info["streams"][0], len(payload), 1, values, base=base, step=step)
        dump = native_dump(data, info, {("literal", 3): values, ("literal", 4): [(0, 0, 0)],
                                      ("literal", 5): [(0, 0, 0)]}, {("literal", 3): plan})
        default = compare.compare_native_dump(data, dump)
        self.assertFalse(default["ok"])
        self.assertEqual(default["scalar_arithmetic"], "binary32-steps")
        self.assertTrue(any(item["path"].endswith("ValueBits") and item["expected"] == 0xc109b604
                            and item["actual"] == 0xc109b602 for item in default["differences"]))
        for requested in ("exact-single-round", "native-x86"):
            result = compare.compare_native_dump(data, dump, scalar_arithmetic=requested)
            self.assertTrue(result["ok"], result["differences"])
            self.assertEqual(result["scalar_arithmetic"], "exact-single-round")
            self.assertEqual(result["scalar_arithmetic_requested"], requested)
        # Choosing the default remains a real exact mismatch; neither the
        # observed profile nor the presence of native evidence auto-selects it.
        self.assertFalse(compare.compare_native_dump(data, dump)["ok"])

    def test_expected_and_actual_row_counts_remain_distinct_on_mismatch(self):
        data, dump = self.format_case(4)
        native = dump["Projection"]["Entries"][0]["Channels"][3]
        native["Rows"].append(row(4, 2, 2.5, 2))
        native["KeyCount"] += 1
        result = compare.compare_native_dump(data, dump)
        self.assertFalse(result["ok"])
        self.assertEqual(result["counts"]["expected_rows"], 6)
        self.assertEqual(result["counts"]["native_actual_rows"], 7)
        self.assertNotIn("native_rows", result["counts"])

    def test_entry_order_names_raw_flags_channels_and_paired_order(self):
        data, original = self.format_case(3)
        for mutation in (lambda entry: entry.update(Name="different"),
                         lambda entry: entry.update(RawFlags=entry["RawFlags"] ^ 1),
                         lambda entry: entry["Channels"].pop(),
                         lambda entry: entry["Channels"][3]["Rows"].reverse()):
            dump = copy.deepcopy(original); mutation(dump["Projection"]["Entries"][0])
            self.assertFalse(compare.compare_native_dump(data, dump)["ok"])
        data, info = wire.fixture([("first", {1: [0, 0, 0]}, False), ("second", {1: [0, 0, 0]}, False)])
        dump = native_dump(data, info, {(name, ch): [(0, 0, 0)] for name in ("first", "second") for ch in (3, 4, 5)})
        self.assert_matches(data, dump)
        dump["Projection"]["Entries"].reverse()
        self.assertFalse(compare.compare_native_dump(data, dump)["ok"])

    def test_each_compact_metadata_field_and_unprojected_terminal_are_checked(self):
        data, original = self.format_case(4)
        for field, invalid in (("Format", "I4"), ("ByteCount", 999), ("BaseBits", 0),
                               ("StepBits", 0), ("FrameScaleBits", 1), ("KeyCount", 99),
                               ("NativeSampleCount", 4), ("EncodedSampleCount", 3),
                               ("TerminalSampleValueBits", 0), ("TerminalSampleProjected", True),
                               ("MatchesNativeCurve", False)):
            with self.subTest(field=field):
                dump = copy.deepcopy(original)
                dump["Projection"]["Entries"][0]["Channels"][3]["RetainedStorage"][field] = invalid
                self.assertFalse(compare.compare_native_dump(data, dump)["ok"])
        dump = copy.deepcopy(original)
        dump["Projection"]["Entries"][0]["Channels"][3]["RetainedStorage"]["EncodedSampleValueBits"][-1] ^= 1
        self.assertFalse(compare.compare_native_dump(data, dump)["ok"])

    def test_missing_dump_is_not_empty_success_and_diffs_are_bounded(self):
        data, _ = self.fixed_case()
        result = compare.compare_native_dump(data, {}, maximum_diffs=2)
        self.assertFalse(result["ok"])
        self.assertEqual(len(result["differences"]), 2)
        self.assertTrue(result["differences_truncated"])
        data, dump = self.fixed_case()
        del dump["Projection"]["Entries"][0]["Channels"][0]["Rows"]
        self.assertFalse(compare.compare_native_dump(data, dump)["ok"])

    def test_cli_readonly_json_exit_status_and_input_overwrite_rejection(self):
        data, dump = self.format_case(6)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); animation = root / "synthetic.chr0"; native = root / "native.json"; output = root / "comparison.json"
            animation.write_bytes(data); native.write_text(json.dumps(dump), encoding="utf-8")
            before = animation.read_bytes(), native.read_bytes()
            self.assertEqual(compare.main([str(animation), str(native), "--output", str(output)]), 0)
            self.assertTrue(json.loads(output.read_text())["ok"])
            self.assertEqual(before, (animation.read_bytes(), native.read_bytes()))
            dump["Motion"]["Sha256Before"] = "0" * 64; native.write_text(json.dumps(dump), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                self.assertEqual(compare.main([str(animation), str(native)]), 1)
            self.assertFalse(json.loads(stdout.getvalue())["ok"])
            for target in (animation, native):
                with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
                    compare.main([str(animation), str(native), "--output", str(target)])
                self.assertEqual(error.exception.code, 2)

    def test_cli_rejects_duplicate_json_fields(self):
        data, _ = self.fixed_case()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); animation = root / "fixture.chr0"; native = root / "native.json"
            animation.write_bytes(data); native.write_text('{"ReadOnly":true,"ReadOnly":false}')
            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                self.assertEqual(compare.main([str(animation), str(native)]), 1)
            self.assertIn("duplicate native JSON field", json.loads(stdout.getvalue())["error"])


if __name__ == "__main__":
    unittest.main()
