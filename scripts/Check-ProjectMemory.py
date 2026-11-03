#!/usr/bin/env python3
"""Check Fighterport's repository memory and render its derived support view.

This checks documentation consistency. It does not validate animation quality,
native game compatibility, or whether a future agent followed the bootstrap.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from typing import Any
from urllib.parse import quote


ROOT = Path(__file__).resolve().parents[1]
STATUSES = {
    "NOT_STARTED", "RESEARCH", "IN_PROGRESS", "IMPLEMENTED_UNVERIFIED",
    "VERIFIED_BOUNDED", "BLOCKED", "REGRESSED", "DEFERRED",
}
SCOPES = {"APPROVED", "PROPOSED", "DEFERRED", "EXCLUDED", "SUPERSEDED", "RETIRED"}
VERIFY_STATES = {
    "PASS_HISTORICAL", "PASS_CURRENT", "NOT_RUN", "FAIL", "UNAVAILABLE", "NOT_APPLICABLE",
}
VERIFY_AREAS = (
    "numerical", "native_reopen", "visual", "human_input", "external_tool", "game_runtime",
)
DESTINATIONS = {"melee", "brawl", "ultimate", "rivals2"}
REQUIRED_DONORS = {
    "bt3", "fighterz", "sparking_zero", "zenkai", "battle_stadium_don",
    "budokai3", "xenoverse", "naruto", "one_piece", "jump_force",
}
SHA = re.compile(r"^(?:[0-9a-f]{7,40}|HEAD\+diff:[0-9a-f]{7,64})$")
ID = re.compile(r"^[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*$")
GAME_ID = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")


def _text(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _repo_path(root: Path, value: Any) -> bool:
    if not _text(value) or "\\" in value:
        return False
    plain = value.split("#", 1)[0]
    if not plain:
        return False
    path = Path(plain)
    if path.is_absolute() or ".." in path.parts or not path.parts:
        return False
    resolved = (root / path).resolve()
    return resolved == root.resolve() or root.resolve() in resolved.parents


def _path_exists(root: Path, value: Any) -> bool:
    return _repo_path(root, value) and (root / value.split("#", 1)[0]).exists()


def _unique(rows: Any, kind: str, errors: list[str], *, pattern: re.Pattern[str] = ID) -> dict[str, dict[str, Any]]:
    found: dict[str, dict[str, Any]] = {}
    if not isinstance(rows, list):
        errors.append(f"{kind} must be a list")
        return found
    for number, row in enumerate(rows):
        if not isinstance(row, dict) or not _text(row.get("id")) or not pattern.fullmatch(row["id"]):
            errors.append(f"{kind}[{number}] has no valid stable ID")
            continue
        key = row["id"]
        if key in found:
            errors.append(f"duplicate {kind} ID: {key}")
        else:
            found[key] = row
    return found


def _evidence(root: Path, items: Any, where: str, errors: list[str]) -> bool:
    if not isinstance(items, list):
        errors.append(f"{where}.evidence must be a list")
        return False
    for number, item in enumerate(items):
        label = f"{where}.evidence[{number}]"
        if not isinstance(item, dict):
            errors.append(f"{label} must have path, revision and coverage")
            continue
        if not _path_exists(root, item.get("path")):
            errors.append(f"{label}.path is missing, private or outside the repository: {item.get('path')}")
        if not _text(item.get("revision")) or not SHA.fullmatch(item["revision"]):
            errors.append(f"{label}.revision must pin a Git SHA or HEAD+diff fingerprint")
        if not _text(item.get("coverage")):
            errors.append(f"{label}.coverage is required")
    return bool(items)


def _verification(root: Path, record: dict[str, Any], where: str, errors: list[str]) -> bool:
    verification = record.get("verification")
    if not isinstance(verification, dict):
        errors.append(f"{where}.verification must separate all six evidence areas")
        return False
    passed = False
    for area in VERIFY_AREAS:
        value = verification.get(area)
        if not isinstance(value, dict) or not isinstance(value.get("state"), str) or value["state"] not in VERIFY_STATES:
            errors.append(f"{where}.verification.{area} needs a valid state")
            continue
        has_evidence = _evidence(root, value.get("evidence"), f"{where}.verification.{area}", errors)
        if value["state"].startswith("PASS_"):
            passed = True
            if not has_evidence:
                errors.append(f"{where}.verification.{area} marks PASS without evidence")
    return passed


def _references(root: Path, record: dict[str, Any], where: str, errors: list[str]) -> None:
    references = record.get("references")
    if not isinstance(references, dict):
        errors.append(f"{where}.references must contain code, tests and receipts")
        return
    for kind in ("code", "tests", "receipts"):
        paths = references.get(kind)
        if not isinstance(paths, list):
            errors.append(f"{where}.references.{kind} must be a list")
            continue
        for path in paths:
            if not _path_exists(root, path):
                errors.append(f"{where}.references.{kind} has a missing or private path: {path}")


def _baseline_from_git(root: Path, revision: str, file: str) -> Any | None:
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "show", f"{revision}:{file}"],
            check=True, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=10,
        )
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        return None
    return result.stdout


def _historic_ids(root: Path, data: dict[str, Any], warnings: list[str]) -> tuple[set[str], dict[str, set[str]]]:
    accepted = data.get("accepted_revision")
    old_master = _baseline_from_git(root, accepted, "docs/MASTER_PLAN.md") if _text(accepted) else None
    if old_master is None:
        warnings.append("Git baseline master unavailable; historic requirement deletion cannot be checked here")
        requirement_ids: set[str] = set()
    else:
        requirement_ids = set(re.findall(r"\bREQ\d{2}\b", old_master))
    old_ledger = _baseline_from_git(root, "HEAD", "docs/FEATURE_STATUS.json")
    ids: dict[str, set[str]] = {}
    if old_ledger:
        try:
            previous = json.loads(old_ledger)
            for kind in ("requirements", "features", "capabilities"):
                ids[kind] = {x["id"] for x in previous.get(kind, []) if isinstance(x, dict) and _text(x.get("id"))}
        except (ValueError, KeyError, TypeError):
            warnings.append("Committed ledger could not be parsed for ID-removal comparison")
    else:
        warnings.append("No committed feature ledger yet; future ID-removal comparison starts after this setup")
    return requirement_ids, ids


def validate_ledger(
    data: Any, root: Path, *, baseline_requirements: set[str] | None = None,
    baseline_ids: dict[str, set[str]] | None = None,
) -> list[str]:
    """Return every structural error; fixtures can supply explicit baselines."""
    errors: list[str] = []
    if not isinstance(data, dict):
        return ["ledger root must be an object"]
    if data.get("schema_version") != 1:
        errors.append("schema_version must be 1")
    if not _text(data.get("accepted_revision")) or not re.fullmatch(r"[0-9a-f]{40}", data["accepted_revision"]):
        errors.append("accepted_revision must be a full Git SHA")
    if not _text(data.get("active_task_id")) or not ID.fullmatch(data["active_task_id"]):
        errors.append("active_task_id must be a stable task ID")

    destinations = _unique(data.get("destination_games"), "destination_games", errors, pattern=GAME_ID)
    donors = _unique(data.get("source_only_games"), "source_only_games", errors, pattern=GAME_ID)
    if set(destinations) != DESTINATIONS:
        errors.append(f"destination game IDs must be exactly {', '.join(sorted(DESTINATIONS))}")
    if not REQUIRED_DONORS <= set(donors):
        errors.append("source-only policy is missing: " + ", ".join(sorted(REQUIRED_DONORS - set(donors))))
    if set(destinations) & set(donors):
        errors.append("a game cannot be both a destination and source-only")
    for kind, rows in (("destination_games", destinations), ("source_only_games", donors)):
        for key, game in rows.items():
            if any(not _text(game.get(field)) for field in ("title", "platform", "build")):
                errors.append(f"{kind}.{key} needs title, platform and build")

    requirements = _unique(data.get("requirements"), "requirements", errors)
    features = _unique(data.get("features"), "features", errors)
    capabilities = _unique(data.get("capabilities"), "capabilities", errors)
    retirements = _unique(data.get("retirements", []), "retirements", errors)
    all_ids = list(requirements) + list(features) + list(capabilities) + list(retirements)
    if len(all_ids) != len(set(all_ids)):
        errors.append("stable IDs must be unique across requirements, features, capabilities and retirements")

    for key, requirement in requirements.items():
        if not isinstance(requirement.get("disposition"), str) or requirement["disposition"] not in SCOPES:
            errors.append(f"requirement {key} has invalid disposition")
        if any(not _text(requirement.get(field)) for field in ("title", "source")):
            errors.append(f"requirement {key} needs title and source")
        elif not _path_exists(root, requirement["source"]):
            errors.append(f"requirement {key} has a missing or private source reference: {requirement['source']}")
        linked = requirement.get("feature_ids")
        if not isinstance(linked, list) or not all(isinstance(x, str) for x in linked) or len(linked) != len(set(linked)):
            errors.append(f"requirement {key}.feature_ids must be a unique list")
            continue
        for feature_id in linked:
            if feature_id not in features:
                errors.append(f"requirement {key} links unknown feature {feature_id}")
            elif key not in (features[feature_id].get("requirement_ids") if isinstance(features[feature_id].get("requirement_ids"), list) else []):
                errors.append(f"requirement {key} / feature {feature_id} reverse link differs")
        if requirement.get("disposition") == "APPROVED":
            if not linked or not any(features.get(f, {}).get("scope") in {"APPROVED", "DEFERRED"} for f in linked):
                errors.append(f"approved requirement {key} has no approved or deferred feature coverage")
        if requirement.get("disposition") in {"SUPERSEDED", "RETIRED"} and not _text(requirement.get("reason")):
            errors.append(f"retired requirement {key} needs a reason")

    for key, feature in features.items():
        where = f"feature {key}"
        if not isinstance(feature.get("scope"), str) or feature["scope"] not in SCOPES or not isinstance(feature.get("status"), str) or feature["status"] not in STATUSES:
            errors.append(f"{where} needs valid scope and status")
        if not isinstance(feature.get("requirement_ids"), list) or not feature["requirement_ids"] or not all(isinstance(x, str) for x in feature["requirement_ids"]):
            errors.append(f"{where} needs linked requirement IDs")
        else:
            for requirement_id in feature["requirement_ids"]:
                if requirement_id not in requirements:
                    errors.append(f"{where} links unknown requirement {requirement_id}")
                elif key not in (requirements[requirement_id].get("feature_ids") if isinstance(requirements[requirement_id].get("feature_ids"), list) else []):
                    errors.append(f"{where} / requirement {requirement_id} reverse link differs")
        if not _text(feature.get("outcome")) or not _text(feature.get("priority")) or not _text(feature.get("next_step")):
            errors.append(f"{where} needs outcome, priority and next_step")
        if not isinstance(feature.get("acceptance"), list) or not feature["acceptance"] or not all(map(_text, feature["acceptance"])):
            errors.append(f"{where}.acceptance needs observable criteria")
        if not isinstance(feature.get("limitations"), list):
            errors.append(f"{where}.limitations must be a list")
        dependencies = feature.get("depends_on")
        if not isinstance(dependencies, list) or not all(isinstance(x, str) for x in dependencies) or len(dependencies) != len(set(dependencies)):
            errors.append(f"{where}.depends_on must be a unique list")
        else:
            for dependency in dependencies:
                if dependency not in features or dependency == key:
                    errors.append(f"{where} has unknown or self dependency {dependency}")
        verified = _evidence(root, feature.get("evidence"), where, errors)
        passed = _verification(root, feature, where, errors)
        _references(root, feature, where, errors)
        if feature.get("status") == "VERIFIED_BOUNDED" and not (verified and passed):
            errors.append(f"{where} is VERIFIED_BOUNDED without bounded passing evidence")
        if feature.get("status") == "BLOCKED" and not _text(feature.get("blocker_reason")):
            errors.append(f"{where} is BLOCKED without blocker_reason")
        if feature.get("aggregate"):
            required = [c for c in capabilities.values() if c.get("feature_id") == key and c.get("required", False)]
            if feature.get("status") == "VERIFIED_BOUNDED":
                if not required:
                    errors.append(f"aggregate {key} is verified without declared required child capabilities")
                elif any(c.get("status") != "VERIFIED_BOUNDED" for c in required):
                    errors.append(f"aggregate {key} is verified while required child capabilities remain unfinished")

    # A dependency cycle cannot be an executable sequence, even when every ID exists.
    visited: set[str] = set()
    walking: set[str] = set()

    def visit(key: str) -> None:
        if key in walking:
            errors.append(f"feature dependency cycle includes {key}")
            return
        if key in visited:
            return
        walking.add(key)
        for dependency in features[key].get("depends_on", []) if isinstance(features[key].get("depends_on"), list) else []:
            if isinstance(dependency, str) and dependency in features:
                visit(dependency)
        walking.remove(key)
        visited.add(key)

    for feature_id in features:
        visit(feature_id)

    for key, capability in capabilities.items():
        where = f"capability {key}"
        if not isinstance(capability.get("feature_id"), str) or capability["feature_id"] not in features:
            errors.append(f"{where} links unknown feature {capability.get('feature_id')}")
        game = capability.get("game")
        if not isinstance(game, str) or (game not in destinations and game not in donors and game not in {"portable", "shared"}):
            errors.append(f"{where} has unknown game {game}")
        direction = str(capability.get("direction", "")).lower()
        if direction not in {"source", "destination", "interchange", "shared"}:
            errors.append(f"{where} has invalid direction")
        if isinstance(game, str) and game in donors and direction == "destination":
            errors.append(f"source-only game {game} cannot have destination capability {key}")
        for field in ("platform", "build", "asset", "channel", "operation", "next_step"):
            if not _text(capability.get(field)):
                errors.append(f"{where}.{field} is required")
        if not isinstance(capability.get("status"), str) or capability["status"] not in STATUSES:
            errors.append(f"{where} has invalid status")
        if not isinstance(capability.get("limitations"), list):
            errors.append(f"{where}.limitations must be a list")
        verified = _evidence(root, capability.get("evidence"), where, errors)
        passed = _verification(root, capability, where, errors)
        if capability.get("status") == "VERIFIED_BOUNDED" and not (verified and passed):
            errors.append(f"{where} is VERIFIED_BOUNDED without bounded passing evidence")
        if capability.get("status") == "BLOCKED" and not _text(capability.get("blocker_reason")):
            errors.append(f"{where} is BLOCKED without blocker_reason")
    for game in DESTINATIONS:
        for direction in ("source", "destination"):
            if not any(c.get("game") == game and str(c.get("direction", "")).lower() == direction for c in capabilities.values()):
                errors.append(f"{game} is missing its {direction} capability record")
    for game in donors:
        if not any(c.get("game") == game and str(c.get("direction", "")).lower() == "source" for c in capabilities.values()):
            errors.append(f"source-only game {game} is missing its source capability record")

    for key, retirement in retirements.items():
        if not isinstance(retirement.get("disposition"), str) or retirement["disposition"] not in {"RETIRED", "SUPERSEDED"} or not _text(retirement.get("reason")):
            errors.append(f"retirement {key} needs RETIRED/SUPERSEDED and a reason")
        if retirement.get("disposition") == "SUPERSEDED" and (
            not isinstance(retirement.get("replaced_by"), str)
            or retirement["replaced_by"] not in set(requirements) | set(features) | set(capabilities)
        ):
            errors.append(f"superseded ID {key} needs a valid replacement")
    baseline_ids = baseline_ids or {}
    for kind, previous in baseline_ids.items():
        now = {"requirements": requirements, "features": features, "capabilities": capabilities}.get(kind, {})
        for vanished in previous - set(now):
            if vanished not in retirements:
                errors.append(f"removed {kind} ID {vanished} lacks explicit retirement")
    if baseline_requirements:
        for vanished in baseline_requirements - set(requirements):
            if vanished not in retirements:
                errors.append(f"historic approved requirement {vanished} was removed without retirement")
    return errors


def _cell(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ").strip()


def render_support(data: dict[str, Any]) -> str:
    """Derive the readable format table; never author a second status source."""
    games = {item["id"]: item for item in data["destination_games"] + data["source_only_games"]}
    games.update({
        "shared": {"title": "Shared rig/motion bridge"},
        "portable": {"title": "Portable interchange"},
    })
    rows = sorted(data["capabilities"], key=lambda row: (row["game"], row["direction"], row["asset"], row["operation"], row["id"]))
    lines = [
        "# Fighterport format support (generated)", "",
        "This view is generated from [FEATURE_STATUS.json](FEATURE_STATUS.json) by",
        "`python scripts/Check-ProjectMemory.py --generate-support`. Edit the ledger, then regenerate.",
        "It describes bounded evidence, not blanket game compatibility.", "",
        "**Only Melee, Brawl, Ultimate and Rivals of Aether 2 are destination games.**",
        "All four are also intended sources. Other listed games are source-only.", "",
        "Verification marks: H = historical pass; C = current-session pass;",
        "– = not run; F = failed; U = unavailable; · = not applicable.",
        "Columns N/R/V/H/T/G mean numerical, native reopen, visual, human input,",
        "external tool import and game runtime respectively.", "",
    ]
    marks = {"PASS_HISTORICAL": "H", "PASS_CURRENT": "C", "NOT_RUN": "–", "FAIL": "F", "UNAVAILABLE": "U", "NOT_APPLICABLE": "·"}
    sections = (
        ("Four destination games", set(DESTINATIONS)),
        ("Source-only games", {item["id"] for item in data["source_only_games"]}),
        ("Shared and portable capabilities", {"shared", "portable"}),
    )
    for heading, selected in sections:
        lines += [f"## {heading}", "",
                  "| Game / build | Direction | Asset / channel | Operation | Feature | Status | N/R/V/H/T/G | Evidence | Next step |",
                  "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
        for row in rows:
            if row["game"] not in selected:
                continue
            game = games[row["game"]]
            ver = row.get("verification", {})
            coverage = "/".join(marks.get(ver.get(area, {}).get("state"), "?") for area in VERIFY_AREAS)
            evidence = row.get("evidence", [])
            if evidence:
                path, _, fragment = evidence[0]["path"].partition("#")
                relative = os.path.relpath(path, "docs").replace("\\", "/")
                link = f"[record]({quote(relative, safe='/-_.')}{'#' + quote(fragment) if fragment else ''})"
            else:
                link = "—"
            lines.append("| " + " | ".join(_cell(value) for value in (
                f"{game['title']} ({row['platform']}; {row['build']})", row["direction"],
                f"{row['asset']} / {row['channel']}", row["operation"], row["feature_id"], row["status"], coverage,
                link, row["next_step"],
            )) + " |")
        lines.append("")
    lines += ["This generated table does not infer target compatibility from a saved path,",
              "successful build, reader result or parseable export.", ""]
    return "\n".join(lines)


def validate_repository(root: Path, data: dict[str, Any], *, check_support: bool = True,
                        baseline_requirements: set[str] | None = None,
                        baseline_ids: dict[str, set[str]] | None = None) -> list[str]:
    ledger_errors = validate_ledger(data, root, baseline_requirements=baseline_requirements, baseline_ids=baseline_ids)
    errors = list(ledger_errors)
    if not isinstance(data, dict):
        return errors
    paths = {
        "AGENTS.md": 6144,
        "docs/PROJECT_STATE.md": 12000,
        "docs/plans/current.md": 16000,
    }
    contents: dict[str, str] = {}
    for name, limit in paths.items():
        file = root / name
        if not file.is_file():
            errors.append(f"missing short memory file: {name}")
            continue
        raw = file.read_bytes()
        if len(raw) > limit:
            errors.append(f"{name} is {len(raw)} bytes; limit is {limit}")
        contents[name] = raw.decode("utf-8-sig")
    agents = contents.get("AGENTS.md", "")
    if re.search(r"(?i)preview\.\d+", agents):
        errors.append("AGENTS.md contains release narrative; keep only stable bootstrap/rules")
    startup = agents[:2048]
    for path in ("docs/PROJECT_STATE.md", "docs/plans/current.md", "docs/FEATURE_STATUS.json", "docs/MASTER_PLAN.md"):
        if path not in startup:
            errors.append(f"AGENTS.md startup read order omits {path} near the top")
    if "checkpoint" not in agents.lower():
        errors.append("AGENTS.md must require progress checkpoints and handoff updates")
    state = contents.get("docs/PROJECT_STATE.md", "")
    current = contents.get("docs/plans/current.md", "")
    task = data.get("active_task_id") if _text(data.get("active_task_id")) else ""
    if len(re.findall(r"(?m)^Active task ID:", state)) != 1:
        errors.append("PROJECT_STATE.md must name exactly one active task")
    if len(re.findall(r"(?m)^Task ID:", current)) != 1:
        errors.append("plans/current.md must name exactly one bounded task")
    if not re.search(r"(?m)^Active task ID:\s*" + re.escape(task) + r"\s*$", state):
        errors.append("PROJECT_STATE.md active task ID differs from the ledger")
    if not re.search(r"(?m)^Task ID:\s*" + re.escape(task) + r"\s*$", current):
        errors.append("plans/current.md task ID differs from the ledger")
    if _text(data.get("accepted_revision")) and data["accepted_revision"] not in state:
        errors.append("PROJECT_STATE.md lacks the ledger's last accepted code revision")
    feature_rows = data.get("features") if isinstance(data.get("features"), list) else []
    if not any(feature.get("id") in current for feature in feature_rows if isinstance(feature, dict) and _text(feature.get("id"))):
        errors.append("plans/current.md does not link an active ledger feature")
    master = root / "docs/MASTER_PLAN.md"
    if not master.is_file():
        errors.append("missing canonical docs/MASTER_PLAN.md")
    else:
        master_ids = set(re.findall(r"\bREQ\d{2}\b", master.read_text(encoding="utf-8-sig")))
        requirement_rows = data.get("requirements") if isinstance(data.get("requirements"), list) else []
        ledger_ids = {row.get("id") for row in requirement_rows if isinstance(row, dict)}
        for missing in master_ids - ledger_ids:
            errors.append(f"MASTER_PLAN requirement {missing} has no ledger record")
        for missing in ledger_ids - master_ids:
            retirement_rows = data.get("retirements") if isinstance(data.get("retirements"), list) else []
            if missing not in {item.get("id") for item in retirement_rows if isinstance(item, dict)}:
                errors.append(f"ledger requirement {missing} is absent from MASTER_PLAN")
    if check_support and not ledger_errors:
        support = root / "docs/FORMAT_SUPPORT.md"
        if not support.is_file() or support.read_text(encoding="utf-8-sig") != render_support(data):
            errors.append("docs/FORMAT_SUPPORT.md is stale; regenerate it from FEATURE_STATUS.json")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--generate-support", action="store_true")
    parser.add_argument("--skip-support", action="store_true", help="for incomplete fixtures only")
    parser.add_argument("--baseline-ledger", type=Path, help="optional prior ledger JSON for ID-retirement checks")
    args = parser.parse_args()
    root = args.root.resolve()
    ledger = root / "docs/FEATURE_STATUS.json"
    if not ledger.is_file() or ledger.stat().st_size > 2 * 1024 * 1024:
        print("missing or unbounded docs/FEATURE_STATUS.json", file=sys.stderr)
        return 1
    try:
        data = json.loads(ledger.read_text(encoding="utf-8-sig"))
    except (UnicodeError, ValueError) as error:
        print(f"invalid ledger JSON: {error}", file=sys.stderr)
        return 1
    warnings: list[str] = []
    baseline_requirements, baseline_ids = _historic_ids(root, data, warnings)
    if args.baseline_ledger:
        prior = json.loads(args.baseline_ledger.read_text(encoding="utf-8-sig"))
        baseline_ids = {
            kind: {item["id"] for item in prior.get(kind, [])}
            for kind in ("requirements", "features", "capabilities")
        }
    errors = validate_repository(root, data, check_support=not (args.generate_support or args.skip_support),
                                 baseline_requirements=baseline_requirements, baseline_ids=baseline_ids)
    if not errors and args.generate_support:
        (root / "docs/FORMAT_SUPPORT.md").write_text(render_support(data), encoding="utf-8", newline="\n")
    for warning in warnings:
        print(f"NOTE {warning}", file=sys.stderr)
    for error in errors:
        print(f"ERROR {error}", file=sys.stderr)
    if errors:
        print(f"Project memory FAILED: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(f"Project memory OK: {len(data['requirements'])} requirements, "
          f"{len(data['features'])} features, {len(data['capabilities'])} capabilities; "
          f"active {data['active_task_id']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
