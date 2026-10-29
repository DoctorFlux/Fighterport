"""Literal read-only storage accounting fixtures; no native writer or game data."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


wire = load("storage_original_fixtures", ROOT / "tests/test_chr0_forensics.py")
compare = load("storage_comparison", ROOT / "tools/compare_chr0_storage.py")


def animated(rows, name="joint", group=1):
    return wire.fixture([(name, {group: [wire.variable(3, wire.i12(rows)), 0, 0]}, False)], frames=3)[0]


class Chr0StorageComparisonTests(unittest.TestCase):
    def report(self, before, after=None, **kwargs):
        result = compare.compare_storage(before, before if after is None else after, **kwargs)
        for side in (result["before"], result["after"]):
            self.assertEqual(sum(side["ledger_by_kind"].values()), side["physical_standalone_bytes"])
            self.assertEqual(sum(side["resource_ledger_by_kind"].values()), side["declared_resource_bytes"])
            self.assertEqual(sum(entry["allocated_total_bytes"] for entry in side["entries"]), side["allocated_entry_group_bytes"])
        return result

    def test_no_edit_exact_physical_counts_and_context_is_not_operation_evidence(self):
        data = animated([(0, 1, 0), (1, 2, 0), (2, 3, 0)])
        result = self.report(data, scenario="no-edit")
        self.assertTrue(result["byte_identical"])
        self.assertTrue(result["read_only"])
        self.assertFalse(result["writer_used"])
        self.assertIn("did not witness", result["scenario_claim"])
        self.assertTrue(all(value == 0 for value in result["delta"].values()))
        group = result["before"]["entries"][0]["groups"][1]
        self.assertEqual(group["format"], "I12")
        self.assertEqual(group["format_bits"], 3)
        self.assertEqual(group["slot_bytes"], 12)
        self.assertEqual(group["allocated_curve_header_bytes"], 8)
        self.assertEqual(group["allocated_curve_payload_bytes"], 36)
        self.assertEqual(group["allocated_total_bytes"], 56)
        self.assertEqual(group["native_projected_keys"], 5)
        self.assertEqual(group["physical_allocated_stored_records"], 3)
        self.assertFalse(result["target_rig_supplied"])

    def test_proved_constant_dense_keys_are_distinguished_from_nonzero_tangents(self):
        before = animated([(0, 7, 0), (1, 7, 0), (2, 7, 0)])
        after = animated([(0, 7, 3), (1, 7, 0), (2, 7, -3)])
        result = self.report(before, after)
        a = result["before"]["entries"][0]["groups"][1]["channels"][0]
        b = result["after"]["entries"][0]["groups"][1]["channels"][0]
        self.assertTrue(a["constant_densely_keyed"])
        self.assertTrue(a["constant"]["constant_proven"])
        self.assertEqual(a["integer_native_frames_keyed"], 3)
        self.assertFalse(b["constant_densely_keyed"])
        self.assertTrue(b["constant"]["equal_values_nonzero_tangents"])
        self.assertEqual(b["constant"]["status"], "equal_values_nonzero_tangents_unproved")

    def test_constant_sparse_signed_zero_and_singleton_have_explicit_limits(self):
        for rows, expected_multiple in [([(0, 7, 0), (2, 7, 0)], True),
                                        ([(0, -0.0, 0), (1, -0.0, 0), (2, -0.0, 0)], False),
                                        ([(1, 7, 0)], False)]:
            with self.subTest(rows=rows):
                result = self.report(animated(rows))
                channel = result["before"]["entries"][0]["groups"][1]["channels"][0]
                self.assertTrue(channel["constant"]["constant_proven"])
                self.assertEqual(channel["constant_multiple_keys"], expected_multiple)
                self.assertFalse(channel["constant_densely_keyed"])
        self.assertFalse(self.report(animated([(0, 1, 0)]))["before"]["entries"][0]["groups"][0]["channels"][0]["constant"]["constant_proven"])

    def test_dense_terminal_can_vary_despite_constant_visible_projection(self):
        data, _ = wire.fixture([("joint", {1: [wire.variable(4, wire.dense(4, [5, 5, 5, 9])), 0, 0]}, False)], frames=3)
        channel = self.report(data)["before"]["entries"][0]["groups"][1]["channels"][0]
        self.assertTrue(channel["constant"]["constant_proven"])
        self.assertFalse(channel["constant"]["constant_over_complete_stored_domain"])
        self.assertFalse(channel["constant_densely_keyed"])
        self.assertEqual(channel["stored_scalar_records"], 4)
        self.assertEqual(channel["terminal_sample"]["index"], 3)
        self.assertFalse(channel["terminal_sample"]["projected_by_owned_reader"])

    def test_isotropic_scale_counts_one_cell_one_stream_and_three_native_axes(self):
        payload = wire.i12([(0, 1, 0), (2, 2, 0)])
        data, _ = wire.fixture([("joint", {0: [wire.variable(3, payload)] * 3}, True)], frames=3)
        result = self.report(data)
        group = result["before"]["entries"][0]["groups"][0]
        self.assertEqual(group["slot_bytes"], 4)
        self.assertEqual(group["allocated_total_bytes"], 36)
        self.assertEqual(group["native_projected_keys"], 6)
        self.assertEqual(group["physical_allocated_stored_records"], 2)
        self.assertEqual(group["logical_stored_records"], 6)
        self.assertEqual(len(result["before"]["shared_streams"]), 1)
        self.assertEqual([channel["physical_slot_bytes_allocated"] for channel in group["channels"]], [4, 0, 0])

    def test_shared_cross_bone_stream_is_physically_allocated_only_once(self):
        payload = wire.i4([(0, 1, 0), (2, 2, 0)])
        data, _ = wire.fixture([
            ("first", {1: [wire.variable(1, payload, "same"), 0, 0]}, False),
            ("second", {0: [wire.variable(1, payload, "same"), 1, 1]}, False)], frames=3)
        result = self.report(data)
        a, b = result["before"]["entries"]
        self.assertEqual(result["before"]["physical_streams"], 1)
        self.assertEqual(result["before"]["stored_variable_rows_or_samples"], 2)
        self.assertEqual(a["groups"][1]["allocated_curve_header_bytes"], 16)
        self.assertEqual(a["groups"][1]["allocated_curve_payload_bytes"], 8)
        self.assertEqual(b["groups"][0]["allocated_curve_header_bytes"], 0)
        self.assertEqual(b["groups"][0]["referenced_unique_curve_header_bytes"], 16)
        self.assertEqual(result["before"]["shared_streams"][0]["physical_allocation_owner"]["bone"], "first")

    def test_duplicate_raw_payloads_are_not_deduplicated_until_pointers_share(self):
        payload = wire.i12([(0, 3, 0), (2, 4, 0)])
        def fixture(shared):
            return wire.fixture([(name, {1: [wire.variable(3, payload, "alias" if shared else None), 0, 0]}, False)
                                 for name in ("a", "b")], frames=3)[0]
        result = self.report(fixture(False), fixture(True))
        self.assertEqual(result["before"]["physical_streams"], 2)
        self.assertEqual(result["after"]["physical_streams"], 1)
        self.assertEqual(result["delta"]["physical_standalone_bytes"], -32)
        self.assertEqual(result["ledger_delta_bytes"]["codec_header"], -8)
        self.assertEqual(result["ledger_delta_bytes"]["curve_payload"], -24)
        self.assertEqual(result["before"]["byte_identical_alias_opportunities"][0]["duplicate_raw_bytes"], 32)

    def test_added_removed_entries_fixed_absent_flags_and_group_bytes(self):
        before, _ = wire.fixture([("old", {1: [0, 0, 0]}, False), ("keep", {}, False)], frames=3)
        after, _ = wire.fixture([("keep", {1: [0, 1, 0]}, False), ("new", {2: [1, 2, 3]}, False)],
                                frames=3, extra_flags=(1 << 10) | (1 << 11))
        result = self.report(before, after, scenario="retarget")
        self.assertEqual(result["added_entries"], ["new"])
        self.assertEqual(result["removed_entries"], ["old"])
        self.assertTrue(result["entry_order_changed"])
        keep = next(entry for entry in result["entries"] if entry["name"] == "keep")
        self.assertTrue(keep["groups"][1]["storage_flags_changed"])
        self.assertTrue(keep["before_semantic_flags"]["use_model_rotation"])
        self.assertFalse(keep["after_semantic_flags"]["use_model_rotation"])
        self.assertEqual(keep["groups"][1]["delta"]["slot_bytes"], 12)
        self.assertEqual(keep["groups"][1]["channel_fixed_flags_changed"], ["RX", "RY", "RZ"])

    def test_all_six_format_counts_metadata_strings_and_unclassified_regions(self):
        formats = [(1, wire.i4([(0, 1, 0), (2, 2, 0)])),
                   (2, wire.i6([(0, 1, 0), (64, 2, 0)])),
                   (3, wire.i12([(0, 1, 0), (2, 2, 0)])),
                   (4, wire.dense(4, [1, 2, 3, 4])),
                   (5, wire.dense(5, [1, 2, 3, 4])),
                   (6, wire.dense(6, [1, 2, 3, 4]))]
        data, _ = wire.fixture([(str(fmt), {1: [wire.variable(fmt, payload), 0, 0]}, False)
                                for fmt, payload in formats], frames=3, metadata=True, footer_gap=b"\0\x77\0")
        result = self.report(data)
        self.assertEqual(result["before"]["stream_format_usage"], {name: 1 for name in ("I4", "I6", "I12", "L1", "L2", "L4")})
        self.assertEqual(len(result["before"]["metadata"]["entries"]), 2)
        self.assertGreater(result["before"]["ledger_by_kind"]["string_characters"], 0)
        self.assertIsNone(result["before"]["alignment_and_padding"]["proven_padding_bytes"])
        self.assertGreater(result["before"]["alignment_and_padding"]["nonzero_unclassified_bytes"], 0)
        self.assertEqual(result["before"]["physical_standalone_bytes"] - result["before"]["declared_resource_bytes"],
                         result["before"]["outside_declared_resource_bytes"])

    def test_explicit_arithmetic_profile_and_invalid_scenario(self):
        data, _ = wire.fixture([("joint", {1: [wire.variable(1, wire.i4([(0, 4095, 0), (2, 4095, 0)], step=1 + 2 ** -23, base=-4095)), 0, 0]}, False)], frames=3)
        a = self.report(data, scalar_arithmetic="exact-single-round")
        b = self.report(data, scalar_arithmetic="binary32-steps")
        self.assertEqual(a["scalar_arithmetic"], "exact-single-round")
        ca = a["before"]["entries"][0]["groups"][1]["channels"][0]
        cb = b["before"]["entries"][0]["groups"][1]["channels"][0]
        self.assertEqual(ca["constant"]["constant_value_bits"], "39fff000")
        self.assertEqual(cb["constant"]["constant_value_bits"], "3a000000")
        with self.assertRaises(compare.StorageComparisonError):
            self.report(data, scenario="pretend-save-witness")

    def test_payload_header_change_is_distinct_from_changed_decoded_key_bits(self):
        data, info = wire.fixture([("joint", {1: [wire.variable(3, wire.i12([(0, 1, 0), (2, 2, 0)])), 0, 0]}, False)], frames=3)
        header_only = bytearray(data)
        header_only[info["streams"][0] + 7] ^= 1
        result = self.report(data, bytes(header_only))
        group = result["entries"][0]["groups"][1]
        self.assertTrue(group["decoded_projection_bits_equal"])
        self.assertFalse(result["byte_identical"])
        self.assertNotEqual(group["before"]["channels"][0]["frame_scale"], group["after"]["channels"][0]["frame_scale"])
        edited = animated([(0, 1, 0), (2, 2.25, 0)])
        group = self.report(data, edited)["entries"][0]["groups"][1]
        self.assertFalse(group["decoded_projection_bits_equal"])
        self.assertEqual(group["channels_with_changed_decoded_row_bits"], ["RX"])

    def test_cli_deterministic_readonly_output_guard_and_failed_output_preservation(self):
        data = animated([(0, 1, 0), (2, 2, 0)])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); before = root / "before.chr0"; after = root / "after.chr0"; output = root / "out.json"
            before.write_bytes(data); after.write_bytes(data)
            args = [str(before), str(after), "--scenario", "one-key", "--scalar-arithmetic", "exact-single-round", "--output", str(output)]
            self.assertEqual(compare.main(args), 0)
            first = output.read_bytes(); self.assertEqual(compare.main(args), 0); self.assertEqual(output.read_bytes(), first)
            value = json.loads(first)
            self.assertTrue(value["inputs"]["hashes_unchanged_after_analysis"])
            self.assertEqual(value["scenario_label"], "one-key")
            self.assertEqual(before.read_bytes(), data); self.assertEqual(after.read_bytes(), data)
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                compare.main([str(before), str(after), "--output", str(before)])
            alias = root / "alias"; alias.write_bytes(data)
            # Sandbox policies need not permit constructing NTFS hardlinks.
            # Exercise the samefile admission branch independently of that OS
            # privilege; ordinary distinct paths still use their real identity.
            original_samefile = Path.samefile
            def samefile(path, other):
                return True if path == alias and other == before else original_samefile(path, other)
            with patch.object(Path, "samefile", samefile), contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                compare.main([str(before), str(after), "--output", str(alias)])
            after.write_bytes(b"invalid")
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(compare.main(args), 1)
            self.assertEqual(output.read_bytes(), first)

    def test_bounded_reads_report_limits_and_input_change_fail_without_publication(self):
        data = animated([(0, 1, 0), (2, 2, 0)])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); before = root / "a"; after = root / "b"; output = root / "out"
            before.write_bytes(data); after.write_bytes(data); output.write_text("retained")
            argv = [str(before), str(after), "--output", str(output)]
            with patch.object(compare._wire, "MAX_FILE_BYTES", 1), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(compare.main(argv), 1)
            with patch.object(compare, "MAX_OUTPUT_BYTES", 1), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(compare.main(argv), 1)
            with patch.object(compare, "MAX_GROUPS", 1), self.assertRaises(compare.StorageComparisonError):
                self.report(data)
            with patch.object(compare, "_read", side_effect=[data, data, data + b"changed", data]), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(compare.main(argv), 1)
            self.assertEqual(output.read_text(), "retained")


if __name__ == "__main__":
    unittest.main()
