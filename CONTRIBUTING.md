# Contributing to LFM

Thanks for your interest in contributing. This project is licensed under
**AGPL-3.0-or-later** (see [`LICENSE`](LICENSE)).

## Inbound = outbound

By opening a pull request, you agree that your contribution is licensed
under **AGPL-3.0-or-later** — the same license as the rest of the project.
There is no separate Contributor License Agreement (CLA) and no
Developer Certificate of Origin (DCO) sign-off required.

This follows the GitHub inbound=outbound convention
(<https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#6-contributions-under-repository-license>):
any contribution you submit to this repository is under the same terms
as the rest of the code.

## How to contribute

1. Open an issue describing the change.
2. Fork, create a branch, implement with tests, open a PR.
3. CI must be green: build, tests, REUSE lint, dep license check,
   secrets scan, IaC analysis.
4. New source files must carry an SPDX header; the REUSE CI gate will
   fail otherwise. See [`REUSE.toml`](REUSE.toml) for globs covered
   collectively (e.g. `.razor`, images, JSON).

## Copyright attribution

The copyright line on new files is:

```
// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors
```

Contributors are not named individually; collective `LFM contributors`
covers all authors. Your commit authorship in git history is your
attribution record.

## Running the test suite

See [`README.md`](README.md) for build and test commands.
