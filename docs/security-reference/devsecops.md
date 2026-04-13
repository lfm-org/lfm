# DevSecOps — Reference

A language-, stack-, and jurisdiction-agnostic synthesis of authoritative guidance on DevSecOps. Written to be directly usable as the rubric and reasoning substrate for a DevSecOps audit agent and a DevSecOps skill.

## Context

The failure mode this reference addresses: **security controls are routinely added to pipelines as checklist items, but a control that is *present* is not automatically a control that *works*.** A SAST job that never blocks a merge and whose findings nobody reads is indistinguishable, from the outside, from a real security gate. Branch protection on paper is not branch protection when administrators can bypass it. An SBOM archived on a build server that never gets queried for a CVE match is a file, not a program. The goal is to distinguish DevSecOps programs that reduce risk from programs that produce paperwork.

This document does not analyze any specific codebase. It states principles, smells, and rubrics.

---

## 1. The central problem: presence vs. efficacy

Two programs that look identical from a compliance audit:

- **Enforcing control** — a gate, rule, or admission check whose removal would visibly change what ships: builds fail, merges stop landing, deploys get blocked, alerts page humans.
- **Decorative control** — a job, dashboard, report, or policy that exists because a framework required it, but whose removal would change nothing observable: the pipeline still runs, merges still land, deploys still ship.

OWASP DSOMM encodes this directly: activities are "done" only when their definition is satisfied, with no partial credit. NIST SSDF PO.4.1 and PO.4.2 require defined *criteria* for security checks and *mechanisms* to gather the evidence those criteria were met. The 2024 JRC/ENISA CRA standards-mapping grades CRA Annex I coverage against concrete standards rows because "secure by design" is treated as a verifiable property, not an attestation.

**The operational question for every control:**

> If this control were silently disabled for a sprint, would anyone notice — via a failed build, a blocked merge, a missed deploy, an alert someone reads, or a report someone acts on?

If no, the control is decorative. It may still be useful as documentation or training scaffolding, but it should be labeled, bounded, and replaced by enforcing coverage over time.

A secondary question, for every finding:

> If this finding were the one a real attacker used, is there a path from detection to remediation that runs in hours or weeks — not "quarterly review"?

If no, the program has scanning, not response.

---

## 2. Consensus characteristics of a quality DevSecOps program

Synthesized across OWASP DevSecOps Guideline, DSOMM, SAMM v2, ASVS, Proactive Controls, NIST SSDF (SP 800-218), NIST SP 800-207 Zero Trust, SLSA v1.0, ENISA Supply Chain Good Practices, ENISA NIS2 Technical Implementation Guidance, the NIS2 Directive Article 21, the EU CRA, and the Threat Modeling Manifesto.

### 2.1 Lifecycle-wide, not stage-bolted

Security must appear in every phase of the SDLC, not only in a "security phase" before release. The OWASP DevSecOps Guideline defines seven process stages (Design, Develop, Build, Test, Release, Deploy, Operate); OWASP SAMM v2 spans five business functions (Governance, Design, Implementation, Verification, Operations) with three practices each (15 total); NIST SSDF organizes practices into four groups (PO Prepare the Organization, PS Protect the Software, PW Produce Well-Secured Software, RV Respond to Vulnerabilities) covering everything from organizational readiness to post-release vulnerability handling. If your controls cluster in a single stage, the stages before and after it are an unmonitored attack surface.

### 2.2 Automated, policy-driven, and default-on

DSOMM Level 4 is explicitly characterized as "Automation drives most security activities." OpenSSF Scorecard's Branch-Protection, Token-Permissions, and Pinned-Dependencies checks all test whether enforcement is **declarative** — encoded in config that tooling reads — not procedural. OWASP Proactive Control C5 ("Secure by Default Configurations") is the developer-facing version of the same principle: security ships on, not off, and the cost of disabling it is visible.

### 2.3 Measured by evidence, not attestation

A compliance audit asks "is this control present?" A DevSecOps audit asks "what artifact would I produce to prove this control was enforced on *this specific release*?" SLSA v1.0 Build L1 requires signed provenance, not because signing is intrinsically better, but because an artifact either has verifiable provenance or it does not. SSDF PO.3.3 requires tools be "configured to generate artifacts supporting secure development practices." ENISA's 2024 CRA mapping reads as a table of *verifiable evidence rows* deliberately — each CRA requirement becomes something an auditor can point at.

The deliverable of DevSecOps maturity is **evidence per release**, not evidence per year.

### 2.4 Supply-chain-aware

ENISA's 2023 Good Practices for Supply Chain Cybersecurity reported that in 2021, 66% of supplier-side compromises went unattributed versus under 9% on the customer side. OWASP SCVS, SLSA, Sigstore, in-toto, the EU CRA Annex I Part II, NIS2 Article 21(d) and (e), and OpenSSF Scorecard's Pinned-Dependencies + Signed-Releases + Dependency-Update-Tool checks all exist because the industry has shifted from "are your declared dependencies known vulnerable" to "can you prove, cryptographically, which dependencies you actually built with." A program that tracks its own first-party code but not its build inputs is solving the 2015 problem.

### 2.5 Continuously verified

NIST SP 800-207's first tenet: "All data sources and computing services are considered resources." Its core directive: access is evaluated and re-evaluated per session. Applied to the pipeline: every build artifact is untrusted until provenance is verified; every deploy re-checks policy at admission; every credential is short-lived (OIDC federation replacing static PATs and long-lived cloud keys); every dependency update re-triggers SCA. A one-time security review, however thorough, becomes wrong the moment a dependency updates.

### 2.6 Shift-left AND shift-right

"Shift left" is the DSOMM/Guideline/Proactive-Controls directive to push security upstream — threat modeling at design, pre-commit secret scanning, IDE SAST, SCA on PRs. But SSDF RV (Respond to Vulnerabilities), NIS2 Article 21(b–c), and ENISA's Threat Landscape 2024 observations on ransomware and in-product supply-chain compromise make clear that runtime monitoring, coordinated vulnerability disclosure, and incident response are not replaceable by prevention. A program that only shifts left has no answer to the vulnerability that shipped last quarter.

### 2.7 Governed, not improvised

SAMM v2's Governance function (Strategy and Metrics, Policy and Compliance, Education and Guidance) and NIS2 Article 21 letters (a), (f), (g), (i) exist because technical controls without an owner, a policy, an SLA, a training budget, and board-level visibility degrade. DSOMM's "Culture and Organization" dimension is co-equal with the four technical dimensions. Every enforcing control needs a named owner and a written remediation SLA; every dashboard needs an audience.

### 2.8 Incident-ready

SSDF RV.1.3 requires a vulnerability disclosure policy and response procedure. CRA Annex I Part II items 4–8 require public disclosure of fixed vulnerabilities, a coordinated disclosure channel, secure update distribution, and dissemination of patches without delay. NIS2 Article 21(b) and (c) require incident handling, business continuity, and crisis management — and NIS2 imposes a 24-hour early-warning reporting obligation for significant incidents. A pipeline without a runbook for "an exploitation report just landed, here is how we triage, patch, sign, ship, and disclose" is not ready for production.

---

## 3. What to secure — decision heuristics

Audit coverage per SDLC stage, synthesized from OWASP DevSecOps Guideline, DSOMM sub-dimensions, SSDF practices, SCVS control families, and ENISA's IoT Secure SDLC:

| Stage | Controls an audit should expect |
|---|---|
| **Design** | Threat modeling (STRIDE or PASTA); abuse cases; trust boundaries; data classification; ASVS/SCVS target level declared; security requirements captured as stories (SAMM Security Requirements) |
| **Develop** | Pre-commit secret scanning; signed commits; IDE SAST; branch protection; MFA on SCM; least-privilege repo access; role-based security training currency (NIS2 21(g), SAMM E&G) |
| **Build** | SAST, SCA, IaC scanning, container image scan, license compliance, ephemeral runners, pinned toolchain, hermetic/reproducible builds, SBOM generation (CycloneDX and/or SPDX), artifact signing (cosign), SLSA provenance attestation |
| **Test** | DAST against staging; authenticated API scans; fuzzing on attack-surface inputs; IAST where available; business-logic tests covering A04 (Insecure Design) and API1/API3 (BOLA / BOPLA) failures |
| **Release** | Signed artifact + signed provenance; SBOM published with release; CVE disclosure in release notes; SLSA Build Level ≥ L2; release-approver identity distinct from commit identity (CICD-SEC-1) |
| **Deploy** | Admission control verifying signatures (cosign verify / policy-controller); policy-as-code gates (OPA/Rego); secrets injected from vault, never baked into images; OIDC federation for cloud credentials; immutable images |
| **Operate** | Runtime vulnerability scanning; WAF/RASP where justified by risk; SIEM forwarding of CI and runtime events; anomaly detection on pipeline runs; CVE intake from suppliers; patch SLA tracking per severity |
| **Respond** | Coordinated vulnerability disclosure policy; `SECURITY.md` / security.txt; incident runbook; tested backup/restore; forensic log retention; post-incident root-cause review feeding back into PW.9 secure configurations and RV.3 process improvement |

**What to threat-model** (from SAMM Threat Assessment, Microsoft STRIDE guidance, and the Threat Modeling Manifesto's "early and frequent"):

- Every new external interface — HTTP, queue, webhook, RPC, file upload, WebSocket
- Every new trust boundary — process, network, tenant, service account, build host
- Every elevation-of-privilege path — admin endpoints, impersonation, sudo, service role assumption
- Every build input that isn't a pinned digest — upstream dep, GitHub Action, base image, fetched script
- Every data flow that crosses a classification boundary — PII in/out, secret in/out, regulated data in/out
- Every design change to authentication, authorization, session management, cryptography, or audit logging

**Deliberately off the list:**

- **Every CVE in every transitive dependency.** Prioritize reachable + exploitable. Unfiltered SCA output drowns signal under noise.
- **Every theoretical CIS benchmark rule.** CIS benchmarks run to hundreds of checks; pick the Level-1 rules that apply to your workloads and automate them.
- **L3 ASVS coverage for an L1 product.** ASVS is cumulative; declare the right level and verify against it, not above it.
- **Framework code.** Trust the framework; test your use of it. "Does Spring Security's authorization check work" is not your audit's question.
- **Third-party code you don't own and don't build.** SCA is appropriate; source audit is not.

---

## 4. What not to do — the anti-pattern catalog

The OWASP CI/CD Security Top 10 (2022) is the authoritative anti-pattern catalog. None of these are hypothetical — each has a published breach history.

1. **CICD-SEC-1 Insufficient Flow Control Mechanisms.** Commits land without review, deploys run without approval, merges bypass branch protection via admin override or disabled required checks.
2. **CICD-SEC-2 Inadequate Identity and Access Management.** Stale accounts, shared service principals, local accounts bypassing SSO, overly permissive PATs, no separation of duties between dev and deploy identities.
3. **CICD-SEC-3 Dependency Chain Abuse.** Dependency confusion, typosquatting, unmaintained packages, unpinned floating-tag pulls, no checksum verification, missing internal mirror/proxy.
4. **CICD-SEC-4 Poisoned Pipeline Execution (PPE).** Attacker modifies pipeline config (`.github/workflows`, `Jenkinsfile`, `.gitlab-ci.yml`) via a PR, causing malicious code to run in CI with full pipeline secrets. Split into *Direct-PPE* (in-repo workflow file) and *Indirect-PPE* (imported template or script from a weaker repo).
5. **CICD-SEC-5 Insufficient PBAC (Pipeline-Based Access Controls).** Every pipeline runs with production-write credentials; no per-job scoping; no ephemeral identity; no OIDC federation.
6. **CICD-SEC-6 Insufficient Credential Hygiene.** Secrets in source, in logs, in environment variables; long-lived static cloud keys; no rotation; no vault; no masking.
7. **CICD-SEC-7 Insecure System Configuration.** SCM/CI server defaults, public dashboards, outdated plugins, build infrastructure co-located with production networks.
8. **CICD-SEC-8 Ungoverned Usage of Third-Party Services.** Marketplace Actions pulled by floating tag, unvetted OAuth apps granted org-wide scopes, SaaS integrations bypassing review.
9. **CICD-SEC-9 Improper Artifact Integrity Validation.** Unsigned artifacts, no SLSA provenance, deployment accepting any image with the right name, no admission-time verification.
10. **CICD-SEC-10 Insufficient Logging and Visibility.** CI/CD events not forwarded to SIEM, no anomaly detection on runs, no audit of who triggered what, IR teams blind to pipeline compromise.

**Cross-cutting anti-patterns not always captured by the Top 10:**

- **Scan-only, never-block.** SAST/DAST/SCA findings filed as informational, never gating, never triaged. Decorative by §1 definition.
- **SBOM as a checkbox artifact.** Generated once, archived, never queried against new CVEs. ENISA CRA Part II item 1 requires SBOM *maintained*, not just produced.
- **Security training as an annual LMS click-through.** NIS2 Art 21(g) says "basic cyber hygiene practices and cybersecurity training"; SAMM Education & Guidance L2 expects role-specific training. An LMS checkbox is not training.
- **"Security review" as a single meeting at release gate.** If the first security review happens at release, every earlier stage is an anti-pattern.
- **Dependabot or Renovate configured, PRs stale.** A bot without a review cadence is worse than no bot — it manufactures false confidence.
- **Zero negative tests for authentication/authorization code paths.**
- **Policy documents claiming SSDF/SOC2/ISO compliance with no per-release evidence pointer.**

---

## 5. Detectable signals

Split into high-confidence (clear smell) and low-confidence (worth flagging, requires context). A DevSecOps audit agent should favor the high-confidence smells as blocking findings and treat the low-confidence smells as conversation starters.

### 5.1 High-confidence smells

1. **Secrets in source control.** Any cloud key, token, connection string, database URL, or private key committed to git — including history. A real-shape credential in `.env.example` is a smell; a rotated-but-still-present secret is still leaked.
2. **Unpinned CI action / image / dependency.** `uses: foo/bar@main`, `FROM python:latest`, `npm install` without lockfile committed. Scorecard Pinned-Dependencies explicitly tests this.
3. **GitHub Actions workflow without declared `permissions:`.** Token defaults to `write-all` when unspecified. Scorecard Token-Permissions.
4. **No branch protection on default branch.** No required review, no required status checks, direct pushes allowed, or admin bypass enabled. Scorecard Branch-Protection.
5. **`pull_request_target` checking out untrusted ref.** The canonical Direct-PPE vector. Scorecard Dangerous-Workflow.
6. **SAST / SCA / DAST job present with `continue-on-error: true`**, or severity threshold set above "critical" with no comment justifying the exemption.
7. **Long-lived static cloud credentials as CI secrets.** Should be OIDC federation. CICD-SEC-6.
8. **Container image without `@sha256:` digest or from unknown registry.** CICD-SEC-3 + CICD-SEC-9.
9. **Deploy workflow and merge workflow share the same identity / token.** No separation of duties. CICD-SEC-2 + CICD-SEC-5.
10. **No `SECURITY.md` and no `/.well-known/security.txt`.** No disclosure channel. Scorecard Security-Policy; CRA Part II item 5; ENISA CVD guidance; ISO/IEC 29147.
11. **Production artifacts unsigned.** No cosign signature, no SLSA provenance, no in-toto attestation. SLSA L1+ violation; CICD-SEC-9.
12. **CI logs and audit events not forwarded off the build platform.** A compromised build host can delete its own evidence. CICD-SEC-10; SSDF PO.5.1.
13. **Dockerfile `USER root` or no `USER` directive.** Explicit OWASP Docker Security Cheat Sheet violation.
14. **`TODO: security review` or `FIXME: vuln` older than 90 days.** Intent recorded, never acted on.
15. **Vulnerability scanner runs, findings dashboard populated, no written remediation SLA.** Decorative control; SSDF RV.2 violation.
16. **Mixed-content HTTP allowed on a site serving auth.** ASVS V9 violation; trivially detectable from security headers.
17. **Mandatory MFA not enforced on SCM org or cloud root account.** Scorecard can't see this directly but audit logs can.
18. **Kubernetes `PodSecurityPolicy` / `PodSecurity` admission missing or set to `privileged`.** CIS Kubernetes Benchmark baseline violation.
19. **Hardcoded signing keys or long-lived GPG keys in repo / CI secret store.** Sigstore's keyless pattern exists specifically to eliminate this.
20. **`Access-Control-Allow-Origin: *` with `Access-Control-Allow-Credentials: true`.** Browsers reject, so the config is broken *and* it's a security smell — nobody thought about it.

### 5.2 Low-confidence smells (flag, ask for context)

1. **High SAST noise, low triage.** Thousands of findings, no suppressions, age cohort not decreasing. Either the tool is miscalibrated or the team has tuned it out.
2. **Threat model document older than a year with no diffs.** SAMM Threat Assessment expects iteration; the Threat Modeling Manifesto principle 2 expects alignment with development practices.
3. **"Security champion" role exists but no PR review is attributable to it for 3+ months.** DSOMM Culture L3 expects activity, not titles.
4. **> 20 third-party GitHub Actions from unrelated publishers.** CICD-SEC-8 surface; may be legitimate for a large monorepo.
5. **Kubernetes manifests without `runAsNonRoot: true`, `readOnlyRootFilesystem: true`, `allowPrivilegeEscalation: false`.** Sometimes legitimate; usually not. CIS Kubernetes Benchmark.
6. **SBOM present in release but same digest across consecutive releases of different source.** Stale SBOM; ENISA CRA 2024 expects it to track actual build inputs.
7. **IAM policy with `"Resource": "*"` or `"Action": "*"`.** May be a bootstrap placeholder; confirm.
8. **Fuzz targets exist, but no crashes reported in 6+ months on a C/C++/unsafe-Rust component.** Fuzzer may be stuck in a corner. Scorecard Fuzzing.
9. **Dependency update bot configured but PRs stale for weeks.** CICD-SEC-3. A bot without review cadence manufactures false confidence.
10. **Security tests exist but only run on `main`.** Feature branches — where exploitation begins — are uncovered.
11. **Terraform state file stored in repo or on the build agent.** May be intentional for bootstrap; usually a secret leak.
12. **Parameterized pipeline with user-controlled input interpolated into `run:` blocks.** Indirect script injection risk.
13. **Post-incident reviews that don't result in test additions, rule additions, or policy changes.** SSDF RV.3.3/RV.3.4 violation — findings aren't feeding back.

### 5.3 Positive signals (reward these)

1. **Artifact provenance verifiable end-to-end.** Cosign signature + in-toto SLSA provenance + Rekor log entry + admission-controller policy that actually rejects unsigned images.
2. **OIDC federation replacing static cloud credentials.** Short-lived, per-job, identity-scoped. CICD-SEC-5/6 defended.
3. **Pinned dependencies everywhere — `@sha256:`, commit SHAs, committed lockfiles.** Scorecard Pinned-Dependencies green.
4. **Every workflow job declares minimum `permissions:` explicitly**, with `contents: read` as the default and write scopes only where needed.
5. **`SECURITY.md` + security.txt + a coordinated disclosure contact + a documented triage SLA.** ISO/IEC 29147 satisfied.
6. **SBOM generated per build, attached to release, and queryable.** CRA Part II item 1 satisfied; Dependency-Track or equivalent ingest visible.
7. **Written vulnerability remediation SLA tracked with aging cohorts.** "Critical in 7 days, High in 30" with a dashboard that shows who owns the aging work.
8. **Branch protection enforced via repo rules (visible declarative policy) with required reviews from `CODEOWNERS`.**
9. **Secrets scanned pre-commit AND on push (server-side push protection).** Two layers of defense.
10. **Reproducible or hermetic builds.** Bit-for-bit identical output across rebuilds from the same source.
11. **CI/CD audit events forwarded to SIEM with anomaly detection rules on unusual pipeline invocations.** CICD-SEC-10 defended.
12. **Threat model updated in the same PR as the architectural change it covers.** The model is part of the diff, not a parallel track.
13. **Post-incident root-cause review that produces a lint/SAST rule, a test, or a policy-as-code rule.** SSDF RV.3.3/RV.3.4.
14. **Role-targeted security training** (backend engineers, frontend, SRE, platform, data) rather than one annual generic course.
15. **Fakes / in-memory test infrastructure used for external services** — so integration tests don't need the real vault or the real cloud. Consistent with Google's *real > fake > mock* preference order applied to security infrastructure.
16. **SLSA Build Level declared and verifiable** (L1 minimum, L2 or L3 preferred for production artifacts).
17. **ASVS and/or SCVS target level declared in the repo's security documentation**, with a self-assessment of gaps.

---

## 6. Detecting subtle failures

The hardest class to catch because they look like functional features or legitimate configuration. Signals, in decreasing strength:

1. **Hash-pinned but unverified dependency.** Pinning by hash defends against tag-rewrite, not against the hash itself being malicious from a typosquat, dependency-confusion, or compromised-maintainer upload. Check the *source*, not only the digest. CICD-SEC-3.

2. **Indirect Poisoned Pipeline Execution.** Your pipeline imports a shared workflow template or script from another repo; the template repo has weaker controls; the attacker modifies the template; your pipeline executes it with your secrets. CICD-SEC-4. The fact that *your* pipeline looks clean is not evidence.

3. **Shadow / zombie APIs.** OWASP API Security Top 10 API9:2023 (Improper Inventory Management). Old versions still routable, staging endpoints reachable from production subnets, undocumented admin routes. Inventory from the runtime, not from the repo.

4. **BOLA (API1:2023) masquerading as functional auth.** Every endpoint authenticates; per-object authorization is missing. Tests that only check "logged in" pass. Needs a specific test that uses user A's token to access user B's object.

5. **Mass assignment / BOPLA (API3:2023).** The API deserializes into a model with an `isAdmin` or `roles` property; the OpenAPI doc doesn't list the field; the model does. Diff model against documented schema, not against itself.

6. **Logs that reach the SIEM but don't contain the security-relevant fields** (actor identity, resource ID, decision, trace ID). CICD-SEC-10. Presence of logs is not presence of usable logs.

7. **Compliance mapping without evidence pointers.** A policy document claims SSDF or SOC 2 conformance; no artifact shows which specific build satisfied which specific practice. SSDF PO.4.2 explicitly requires mechanisms to *gather* the evidence.

8. **Control scoped to the default branch only.** Secret scanning or SAST set to run on `main` catches nothing on feature branches, where exploitation begins and where the PR reviewer would see the scan result.

9. **XZ-Utils class — long-dwell trusted-maintainer attack.** ENISA ETL 2024 documented. Legitimate git identity, social engineering of upstream maintainers over years, backdoor hidden in test fixtures. No static check catches this; the defense is provenance reconstruction, reproducible builds, maintainer-account hardening, and pre-compile review of non-source build inputs (M4, autotools, test corpora).

10. **"Rules File Backdoor" on AI coding assistants.** ENISA ETL 2024. Malicious instructions embedded in config files that an AI assistant reads as context. Audit what feeds your assistants, not just what they write.

11. **Build-reproducibility regressions.** Two builds from the same source producing different outputs is, in the supply-chain threat model, indistinguishable from a compromise. SCVS and SLSA both treat reproducibility as a first-class signal.

12. **Vulnerability ignored as "not exploitable" without written reachability analysis.** The suppression is the finding.

---

## 7. The scanner / tooling question

A recurring fault line in the literature. A quality audit should take a position rather than equivocate.

- **OWASP DevSecOps Guideline:** every stage has a control category — SAST/SCA at build, DAST/IAST at test, runtime and cloud-posture at operate.
- **OWASP SAMM v2 Security Testing:** L1 tool-based *and* manual, L2 automation plus periodic manual penetration testing, L3 testing embedded in development and deployment. Both mechanical and human required at maturity.
- **NIST SSDF PW.7 / PW.8:** code review/analysis *and* executable testing, scoped and documented, with results recorded.
- **OpenSSF Scorecard:** SAST, Fuzzing, Dependency-Update-Tool, Vulnerabilities are independent checks. Presence of one does not compensate for absence of another.
- **Shift-left maximalism:** push everything to the IDE / pre-commit.
- **Runtime maximalism:** instrument production and trust telemetry.

**Position:** treat each class of scanner as a **different test**, not a substitute. Each answers a question the others cannot.

### 7.1 Tool-class taxonomy

| Tool class | Answers | Cannot answer |
|---|---|---|
| **SAST** | "Does this code pattern resemble known-vulnerable code?" | Whether the pattern is reachable; whether a fix exists in a call site it doesn't see; business-logic flaws |
| **SCA** | "Do any declared dependencies have known CVEs?" | Whether the CVE is reachable in your use; whether undeclared / vendored deps are present |
| **Secret scanning** | "Is there a credential-shaped string in the source?" | Whether credentials exist in runtime config, memory, or environment |
| **IaC scanning** | "Is this Terraform/Bicep/K8s manifest misconfigured against a known rule?" | Whether the rule applies to this workload; whether drift has occurred post-deploy |
| **Container image scanning** | "Does this image contain packages with known CVEs?" | Whether those packages are actually loaded at runtime; malware in the image layers |
| **DAST** | "Does a running instance exhibit known vulnerability classes?" | Anything not in the attack surface exercised during the scan; anything requiring authentication the scanner doesn't have |
| **IAST** | "During a test run, did instrumentation observe a vulnerability path?" | Anything not exercised by the test suite |
| **Fuzzing** | "Does the parser/validator crash on generated inputs?" | Logic bugs that don't crash; protocol-level abuse |
| **Runtime / RASP / eBPF** | "Is current production behavior anomalous against a baseline?" | Anything not observable at runtime; compromise that matches the baseline |
| **SBOM + continuous monitoring** | "Has a component we depend on had a new CVE published?" | Whether the CVE is reachable; whether the SBOM accurately reflects the actual build |

Treat scanners as a **cost**. A scanner is not automatically bad, but it carries a proof obligation: its findings must flow into a place where they change what ships — blocking builds, blocking merges, creating prioritized and aging work. A scanner whose findings only populate a dashboard is decorative by §1 definition.

Google's preference order for test fidelity — **real > fake > mock** — has a direct DevSecOps analog: **enforcing > surfacing > logging**. An enforcing gate changes behavior; a surfacing dashboard changes awareness; a log changes nothing unless someone queries it. Prefer the most-enforcing tier that still fits the finding class.

### 7.2 The detection-latency taxonomy

Per the OWASP DevSecOps Guideline's explicit position that "detection speed is the primary optimization target":

- **Pre-commit** — secret scan, linter, pre-commit hooks (via `pre-commit` or equivalent). Cheapest to fix.
- **PR / pre-merge** — SAST, SCA, IaC scan, bundle-diff, test suite. Fix before review costs another reviewer's time.
- **CI build** — container scan, full SAST, SBOM generation, provenance attestation, license check.
- **Staging / pre-release** — DAST, IAST, fuzz campaigns, manual review for diffs touching authn/authz.
- **Production / post-release** — runtime vulnerability scan, anomaly detection, CVE intake from OSV/NVD/GHSA, bug bounty.
- **Incident-driven** — red team, purple team, chaos engineering, breach-and-attack simulation.

A finding caught at stage *N* is roughly 10× cheaper than the same finding at stage *N+1*. A program whose findings concentrate in production is not operating DevSecOps; it is operating reactive security.

---

## 8. Coverage, mutation, and the limits of static audit

**Signals a static audit of a repo/pipeline can use:**

- **Presence of controls.** Workflow files, branch protection rules, IaC modules, scanner configs. Tells you what exists, not what works.
- **Pinning density.** Ratio of hash-pinned dependencies to total. Scorecard-calculable.
- **Least-privilege density.** Ratio of jobs declaring `permissions:` to total. Scorecard Token-Permissions.
- **Evidence density per release.** For each recent release, which artifacts exist (SBOM, provenance, signatures, scan reports, test results). This is the closest static proxy for "does the pipeline actually enforce policy."
- **Cadence.** Time between CVE disclosure and patched release merge; time between dependency update and next build. Requires issue/PR history but is static-analyzable.
- **Evidence vs. claim ratio.** For each claimed framework (SSDF, ISO 27001, SOC 2, CRA, NIS2): how many of the claimed controls have a machine-verifiable artifact pointer versus a prose paragraph.

**The "mutation testing" analog for DevSecOps:**

Just as mutation testing is the ground-truth signal for unit-test efficacy, the ground-truth signals for DevSecOps efficacy are things a static audit **cannot** produce:

- **Red team / purple team engagement outcomes.** Would a real attacker get stopped by these controls?
- **Breach and attack simulation (BAS).** Continuously-run synthetic attacks against the pipeline and runtime.
- **Chaos engineering with security scenarios.** DSOMM Level 4 activity; verify controls hold under failure.
- **Bug bounty findings per quarter and mean-time-to-remediation.** External researchers will try attacks your own audits won't.
- **Production incident rate and attacker dwell time.** The ultimate outcome measure; ETL 2024 dwell-time data is the industry baseline.
- **Reproducible-build verification** — does the same source, built twice, produce the same artifact? Direct integrity check.

A static audit should recommend these when the program is high-coverage but shallow.

**Things static analysis cannot know** (should admit, not fabricate):

- Whether a declared control is actually *enforced* in CI runs — requires run history.
- Whether a reported vulnerability is *reachable* from your code — requires call-graph + runtime analysis.
- Whether a secret was *used* before it was rotated — requires audit log review.
- Whether a maintainer account is *legitimately* controlled — requires out-of-band verification.
- Whether SBOM-declared dependencies are what actually got linked — requires reproducible-build comparison.
- Whether runtime anomalies have occurred — requires SIEM history.
- Whether a third-party supplier is currently compromised — requires threat intel subscription.

Be explicit about these limits. Ask for commit history, CI run logs, SIEM exports, or a live red-team exercise rather than inventing certainty.

---

## 9. Per-repo / per-pipeline audit rubric

For each project audited, produce:

1. **Scope statement.** Which repos, workflows, environments, and release artifacts are in scope. Anything explicitly excluded.
2. **Declared target levels.** ASVS level, SCVS level, SLSA Build level, DSOMM maturity target, SAMM practice targets, NIS2 applicability (essential/important/neither), CRA applicability. If nothing is declared, **that is itself a finding**.
3. **Stage coverage matrix.** For each of the 8 lifecycle stages in §3, which controls are present, which are enforcing, which are decorative, which are missing. Classification per §1 definition.
4. **Anti-pattern scan.** Which of the 10 CICD-SEC items are observed. One per finding, with file/line or pipeline/job locator.
5. **Smell matches** from §5.1 and §5.2 with evidence pointers.
6. **Positive signals matched** from §5.3 — named and cited.
7. **Supply-chain provenance check.** SBOM presence and format; SLSA level achieved; signing status; pinning density; dependency-update cadence; Scorecard score if available.
8. **Evidence-per-release check.** For the most recent release: does the pipeline produce the artifacts that would let an external auditor verify the claimed levels? List gaps.
9. **Framework coupling report.** Which SSDF practices (PO/PS/PW/RV IDs), ENISA CRA Annex I rows, and NIS2 Article 21 letters are demonstrably covered by evidence. Which are claimed but not evidenced. Which are simply missing.
10. **Presence-vs-efficacy verdict.** Enforcing program / partial / decorative.
11. **Severity per finding.** Block / warn / info. One severity per finding — don't stack.
12. **Recommended action per finding.** Specific diff, config change, or process change. "Improve security" is not an action; "Add `permissions: contents: read` to all workflow jobs and require it via policy-as-code" is.

The rubric is deliberately opinionated. Soft audits produce ignorable noise.

---

## 10. Directive principles

Distilled from §2–§8, written as directives an audit agent can apply directly.

1. **A control that doesn't change what ships is decorative.** Score programs by enforcement, not by presence.
2. **Every release must produce verifiable evidence.** SBOM, provenance, signatures, scan reports — per release, not per year.
3. **Pin everything by digest.** Actions, base images, package versions. Floating tags are a supply-chain attack vector regardless of publisher reputation.
4. **Least privilege is the default, not an exception.** Default-deny for tokens, permissions, network egress, deploy identities; write scopes require a written justification.
5. **Secrets are short-lived or vault-backed.** Long-lived static cloud credentials are a bug regardless of scope.
6. **Provenance and signing are not optional above SLSA L1.** Unsigned artifacts in production are a deployment of unknown origin.
7. **Coverage is a floor, not a goal.** Blocked merges, blocked deploys, and remediated-within-SLA findings are the ceiling-quality signals. Scanner coverage is the floor.
8. **The CI/CD pipeline is production infrastructure.** It holds secrets, writes to prod, and executes untrusted input. Harden it as production — including network segmentation, audit logging, and anomaly detection.
9. **Shift-left without shift-right is half a program.** Prevention and response are non-substitutable. The absence of either is a finding.
10. **Threat modeling is part of the diff.** A design change that introduces a new trust boundary without a threat model is incomplete, even if it compiles and tests pass.
11. **One finding per control is better than ten.** Prioritize the highest-severity smell; don't stack.
12. **Reward positive signals explicitly.** An audit that only complains gets tuned out.
13. **Be honest about what cannot be determined from source alone.** Ask for run history, SIEM exports, reproducible-build comparisons, or red-team results rather than inventing certainty.

---

## Sources

### OWASP — DevSecOps process and maturity

- [OWASP DevSecOps Guideline](https://owasp.org/www-project-devsecops-guideline/) — stage-oriented control catalog
- [OWASP DevSecOpsGuideline (GitHub)](https://github.com/OWASP/DevSecOpsGuideline)
- [OWASP DevSecOps Maturity Model (DSOMM)](https://owasp.org/www-project-devsecops-maturity-model/) — pipeline maturity rubric, 5 dimensions × 4 levels
- [DSOMM interactive site](https://dsomm.owasp.org/)
- [DevSecOps-MaturityModel-data (GitHub)](https://github.com/devsecopsmaturitymodel/DevSecOps-MaturityModel-data)
- [OWASP SAMM v2](https://owaspsamm.org/) — program-level maturity, 5 functions × 3 practices × 3 levels
- [OWASP SAMM v2 model](https://owaspsamm.org/model/)

### OWASP — verification standards

- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) — app-level verification, 14 chapters, L1–L3
- [OWASP ASVS (GitHub)](https://github.com/OWASP/ASVS)
- [OWASP SCVS](https://owasp.org/www-project-software-component-verification-standard/) — supply-chain verification, 6 families × L1–L3

### OWASP — Top 10 risk lists

- [OWASP Top 10 (2021)](https://owasp.org/Top10/)
- [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [OWASP CI/CD Security Top 10 (2022)](https://owasp.org/www-project-top-10-ci-cd-security-risks/)
- [OWASP Top 10 Proactive Controls (2024)](https://top10proactive.owasp.org/)

### OWASP — cheat sheets

- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [CI/CD Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/CI_CD_Security_Cheat_Sheet.html)
- [Docker Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html)
- [Kubernetes Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Kubernetes_Security_Cheat_Sheet.html)
- [Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [Threat Modeling Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Threat_Modeling_Cheat_Sheet.html)

### NIST

- [NIST SP 800-218 SSDF v1.1](https://csrc.nist.gov/pubs/sp/800/218/final) — PO/PS/PW/RV practices
- [NIST SP 800-218 PDF](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-218.pdf)
- [NIST SSDF project page](https://csrc.nist.gov/projects/ssdf)
- [NIST SP 800-207 Zero Trust Architecture](https://csrc.nist.gov/pubs/sp/800/207/final)

### ENISA and EU

- [ENISA — Good Practices for Supply Chain Cybersecurity (2023)](https://www.enisa.europa.eu/publications/good-practices-for-supply-chain-cybersecurity)
- [ENISA — Good Practices for Security of IoT: Secure SDLC (2019)](https://www.enisa.europa.eu/publications/good-practices-for-security-of-iot-1)
- [JRC/ENISA — Cyber Resilience Act Requirements Standards Mapping (2024)](https://www.enisa.europa.eu/publications/cyber-resilience-act-requirements-standards-mapping)
- [ENISA — NIS2 Technical Implementation Guidance v1.0 (2025)](https://www.enisa.europa.eu/publications/nis2-technical-implementation-guidance)
- [ENISA Threat Landscape 2024](https://www.enisa.europa.eu/publications/enisa-threat-landscape-2024)
- [ENISA — Multilayer Framework for Good Cybersecurity Practices for AI (FAICP, 2023)](https://www.enisa.europa.eu/publications/multilayer-framework-for-good-cybersecurity-practices-for-ai)
- [NIS2 Directive (EU 2022/2555), Article 21](https://eur-lex.europa.eu/eli/dir/2022/2555/oj)
- [EU Cyber Resilience Act (Regulation 2024/2847)](https://digital-strategy.ec.europa.eu/en/policies/cyber-resilience-act)

### Supply chain and provenance

- [SLSA v1.0 specification](https://slsa.dev/spec/v1.0/)
- [SLSA v1.0 levels](https://slsa.dev/spec/v1.0/levels)
- [SLSA v1.0 threats overview](https://slsa.dev/spec/v1.0/threats-overview)
- [SLSA + in-toto relationship](https://slsa.dev/blog/2023/05/in-toto-and-slsa)
- [OpenSSF Scorecard](https://github.com/ossf/scorecard)
- [Scorecard checks documentation](https://github.com/ossf/scorecard/blob/main/docs/checks.md)
- [Sigstore](https://www.sigstore.dev/)
- [Sigstore documentation](https://docs.sigstore.dev/)
- [in-toto](https://in-toto.io/)
- [in-toto Attestation Framework](https://github.com/in-toto/attestation)
- [CycloneDX (OWASP)](https://cyclonedx.org/)
- [SPDX (Linux Foundation)](https://spdx.dev/)
- [ISO/IEC 5962:2021 — SPDX](https://www.iso.org/standard/81870.html)

### Hardening benchmarks

- [CIS Benchmarks](https://www.cisecurity.org/cis-benchmarks)
- [CIS Kubernetes Benchmark](https://www.cisecurity.org/benchmark/kubernetes)
- [CIS Docker Benchmark](https://www.cisecurity.org/benchmark/docker)
- [CIS Microsoft Azure Foundations Benchmark](https://www.cisecurity.org/benchmark/azure)
- [kube-bench](https://github.com/aquasecurity/kube-bench)

### Threat modeling

- [Microsoft Learn — STRIDE in Threat Modeling Tool](https://learn.microsoft.com/en-us/azure/security/develop/threat-modeling-tool-threats)
- [VerSprite — PASTA threat modeling](https://versprite.com/blog/what-is-pasta-threat-modeling/)
- UcedaVélez & Morana, *Risk Centric Threat Modeling: Process for Attack Simulation and Threat Analysis* (Wiley, 2015)
- [Threat Modeling Manifesto](https://www.threatmodelingmanifesto.org/)

### Vulnerability disclosure and handling

- ISO/IEC 29147 — Vulnerability disclosure
- ISO/IEC 30111 — Vulnerability handling processes
- [CVE Program](https://www.cve.org/)
- [OSV schema and database](https://osv.dev/)
