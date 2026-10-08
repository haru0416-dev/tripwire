#!/usr/bin/env python3
"""Checks, before a release of one package of this repository, its dependencies on the others.

For each dev.haru0416.* entry in the package's vpmDependencies:
- the dependency's current version (its package.json here) is released: tag "<dependency>@<version>" exists;
- its folder is unchanged since that tag, so what this package was built and tested against is what VCC installs;
- the declared range (">=x.y.z", "^x.y.z", ">=x.y.z <a.b.c" or "x.y.z") includes that version.
Usage: scripts/check-release-deps.py <package>   (from the repository root; needs the tags fetched)
"""
import json, re, subprocess, sys

def version(text):
    m = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?", text.strip())
    if not m: raise ValueError(f"not a version: {text!r}")
    return tuple(int(x) for x in m.groups()[:3])

def prerelease(text):
    return "-" in text.strip()

def satisfies(current, spec):
    """Whether version text current is in the range spec (the forms in the docstring, space-separated parts all hold).
    A pre-release (1.0.0-beta) is in no range here: semver keeps it out of ranges unless they name one, and a package
    released to the listing should depend on a released version anyway."""
    if prerelease(current): return False
    v = version(current)
    spec = re.sub(r"(>=|<|\^)\s+", r"\1", spec) # ">= 0.1.0" as ">=0.1.0"
    for part in spec.split():
        if part.startswith(">="):
            if v < version(part[2:]): return False
        elif part.startswith("<"):
            if v >= version(part[1:]): return False
        elif part.startswith("^"):
            base = version(part[1:])
            # npm's caret: the left-most non-zero number is fixed (^0.1.2: >=0.1.2 <0.2.0; ^1.2.3: >=1.2.3 <2.0.0).
            if v < base: return False
            if base[0] > 0: upper = (base[0] + 1, 0, 0)
            elif base[1] > 0: upper = (0, base[1] + 1, 0)
            else: upper = (0, 0, base[2] + 1)
            if v >= upper: return False
        elif v != version(part):
            return False
    return True

def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True)

def main(package):
    manifest = json.load(open(f"Packages/{package}/package.json"))
    errors = []
    for dep, spec in (manifest.get("vpmDependencies") or {}).items():
        if not dep.startswith("dev.haru0416."): continue
        current = json.load(open(f"Packages/{dep}/package.json"))["version"]
        tag = f"{dep}@{current}"
        if git("rev-parse", "--verify", "--quiet", f"refs/tags/{tag}").returncode != 0:
            errors.append(f"depends on {dep} {current}, which is not released yet: release it first")
            continue
        if git("diff", "--quiet", tag, "HEAD", "--", f"Packages/{dep}").returncode != 0:
            errors.append(f"{dep} changed since {tag}: raise its version and release it first")
        try: ok = satisfies(current, spec)
        except ValueError:
            errors.append(f"{package} asks for {dep} \"{spec}\": this check reads >=, <, ^ and exact versions only")
            continue
        if not ok:
            errors.append(f"{package} asks for {dep} \"{spec}\", which does not include {current}")
    for e in errors: print(f"::error::{e}")
    return 1 if errors else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
