"""Structural tests for the repository handoff ledger and generated support view."""

from __future__ import annotations

import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "Check-ProjectMemory.py"
spec = importlib.util.spec_from_file_location("project_memory", SCRIPT)
memory = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(memory)

REVISION = "a" * 40
TASK = "TASK-RETARGET-NONIDENTICAL-001"
FEATURE = "FP-RETARGET-QUALITY"


def verification(*, numerical_pass: bool = False) -> dict:
    result = {area: {"state": "NOT_RUN", "evidence": []} for area in memory.VERIFY_AREAS}
    if numerical_pass:
        result["numerical"] = {"state": "PASS_HISTORICAL", "evidence": [
            {"path": "docs/receipt.md", "revision": REVISION, "coverage": "Synthetic same-rig fixture"}
        ]}
    return result


def capability(game: str, direction: str, *, number: int) -> dict:
    return {
        "id": f"CAP-{number:03d}", "feature_id": FEATURE, "game": game,
        "platform": "fixture", "build": "fixture", "direction": direction,
        "asset": "animation", "channel": "skeletal", "operation": "inventory",
        "status": "NOT_STARTED", "evidence": [], "verification": verification(),
        "limitations": ["Fixture only"], "next_step": "Implement and verify",
    }


def ledger() -> dict:
    destination_games = [
        {"id": name, "title": name.title(), "platform": "fixture", "build": "fixture"}
        for name in sorted(memory.DESTINATIONS)
    ]
    source_only_games = [
        {"id": name, "title": name.title(), "platform": "fixture", "build": "fixture"}
        for name in sorted(memory.REQUIRED_DONORS)
    ]
    caps = []
    for game in destination_games:
        for direction in ("source", "destination"):
            caps.append(capability(game["id"], direction, number=len(caps) + 1))
    for game in source_only_games:
        caps.append(capability(game["id"], "source", number=len(caps) + 1))
    evidence = {"path": "docs/receipt.md", "revision": REVISION, "coverage": "Synthetic same-rig fixture"}
    return {
        "schema_version": 1, "accepted_revision": REVISION, "active_task_id": TASK,
        "destination_games": destination_games, "source_only_games": source_only_games,
        "requirements": [{"id": "REQ01", "title": "Retarget animations", "disposition": "APPROVED",
                          "source": "docs/MASTER_PLAN.md", "feature_ids": [FEATURE]}],
        "features": [{"id": FEATURE, "requirement_ids": ["REQ01"], "scope": "APPROVED",
                      "outcome": "Preserve native rig motion", "acceptance": ["Reopen and compare motion"],
                      "priority": "P0", "depends_on": [], "status": "VERIFIED_BOUNDED",
                      "evidence": [evidence], "references": {
                          "code": ["scripts/Check-ProjectMemory.py"], "tests": ["tests/test_project_memory.py"],
                          "receipts": ["docs/receipt.md"]},
                      "limitations": ["Same-rig fixture only"], "next_step": "Test unequal rigs",
                      "verification": verification(numerical_pass=True)}],
        "capabilities": caps, "retirements": [],
    }


class ProjectMemoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for folder in ("docs/plans", "scripts", "tests"):
            (self.root / folder).mkdir(parents=True, exist_ok=True)
        (self.root / "docs/receipt.md").write_text("Historical fixture evidence\n", encoding="utf-8")
        (self.root / "scripts/Check-ProjectMemory.py").write_text("# fixture\n", encoding="utf-8")
        (self.root / "tests/test_project_memory.py").write_text("# fixture\n", encoding="utf-8")
        (self.root / "AGENTS.md").write_text(
            "Read docs/PROJECT_STATE.md, docs/plans/current.md, docs/FEATURE_STATUS.json, "
            "and docs/MASTER_PLAN.md at startup. Keep checkpoints current.\n", encoding="utf-8")
        (self.root / "docs/PROJECT_STATE.md").write_text(
            f"Active task ID: {TASK}\nLast accepted revision: {REVISION}\n", encoding="utf-8")
        (self.root / "docs/plans/current.md").write_text(
            f"Task ID: {TASK}\nFeature: {FEATURE}\n", encoding="utf-8")
        (self.root / "docs/MASTER_PLAN.md").write_text("REQ01: Retarget animations\n", encoding="utf-8")
        self.data = ledger()
        (self.root / "docs/FORMAT_SUPPORT.md").write_text(memory.render_support(self.data), encoding="utf-8")

    def errors(self, data: dict | None = None) -> str:
        return "\n".join(memory.validate_repository(self.root, data or self.data))

    def test_valid_fixture_and_deterministic_support_view(self) -> None:
        self.assertEqual("", self.errors())
        self.assertEqual(memory.render_support(self.data), memory.render_support(copy.deepcopy(self.data)))

    def test_missing_approved_requirement_coverage(self) -> None:
        self.data["requirements"][0]["feature_ids"] = []
        self.assertIn("approved requirement REQ01", self.errors())

    def test_requirement_source_reference_must_exist(self) -> None:
        self.data["requirements"][0]["source"] = "docs/private-or-missing.md"
        self.assertIn("missing or private source reference", self.errors())

    def test_duplicate_id_and_unknown_dependency(self) -> None:
        self.data["features"].append(copy.deepcopy(self.data["features"][0]))
        self.data["features"][0]["depends_on"] = ["FP-NOT-THERE"]
        errors = self.errors()
        self.assertIn("duplicate features ID", errors)
        self.assertIn("unknown or self dependency", errors)

    def test_verified_requires_evidence(self) -> None:
        self.data["features"][0]["evidence"] = []
        self.data["features"][0]["verification"]["numerical"]["evidence"] = []
        errors = self.errors()
        self.assertIn("VERIFIED_BOUNDED without bounded passing evidence", errors)
        self.assertIn("marks PASS without evidence", errors)

    def test_blocked_reason_is_required(self) -> None:
        self.data["features"][0]["status"] = "BLOCKED"
        self.assertIn("BLOCKED without blocker_reason", self.errors())

    def test_retirement_required_for_removed_historic_requirement(self) -> None:
        errors = memory.validate_ledger(self.data, self.root, baseline_requirements={"REQ01", "REQ02"})
        self.assertTrue(any("REQ02 was removed without retirement" in error for error in errors))
        self.data["retirements"].append({"id": "REQ02", "disposition": "RETIRED", "reason": "Approved retirement"})
        errors = memory.validate_ledger(self.data, self.root, baseline_requirements={"REQ01", "REQ02"})
        self.assertFalse(any("REQ02 was removed without retirement" in error for error in errors))

    def test_active_task_mismatch_and_stale_support(self) -> None:
        (self.root / "docs/PROJECT_STATE.md").write_text("Active task ID: TASK-WRONG\n", encoding="utf-8")
        (self.root / "docs/FORMAT_SUPPORT.md").write_text("stale\n", encoding="utf-8")
        errors = self.errors()
        self.assertIn("active task ID differs", errors)
        self.assertIn("FORMAT_SUPPORT.md is stale", errors)

    def test_source_only_game_cannot_be_a_destination(self) -> None:
        self.data["capabilities"][-1]["direction"] = "destination"
        errors = self.errors()
        self.assertIn("source-only game", errors)
        self.assertIn("missing its source capability", errors)

    def test_aggregate_cannot_claim_unfinished_required_child(self) -> None:
        self.data["features"][0]["aggregate"] = True
        self.data["capabilities"][0]["required"] = True
        self.assertIn("required child capabilities remain unfinished", self.errors())

    def test_short_bootstrap_boundary(self) -> None:
        (self.root / "AGENTS.md").write_text("x" * 6145, encoding="utf-8")
        self.assertIn("AGENTS.md is 6145 bytes", self.errors())

    def test_malformed_schema_reports_errors_instead_of_crashing(self) -> None:
        self.assertIn("ledger root must be an object", "\n".join(memory.validate_repository(self.root, [])))
        self.data["capabilities"][0]["game"] = {"invalid": True}
        self.data["features"][0]["depends_on"] = [1]
        self.data["features"][0]["verification"]["visual"]["state"] = []
        errors = self.errors()
        self.assertIn("unknown game", errors)
        self.assertIn("depends_on must be a unique list", errors)
        self.assertIn("needs a valid state", errors)


if __name__ == "__main__":
    unittest.main()
