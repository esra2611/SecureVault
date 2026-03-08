# AI usage disclosure

This project may use AI-assisted development (e.g. Cursor, GitHub Copilot, or similar) for:

- **Implementation:** Boilerplate, CRUD, tests, and scaffolding aligned with the architecture in [architecture.md](./architecture.md).
- **Review:** Consistency with Clean Architecture, SOLID, OWASP, and the documented crypto/expiry/concurrency decisions.
- **Documentation:** README, architecture docs, and inline comments.

**Human responsibilities:**

- All architecture decisions, threat model, crypto strategy, and security controls are human-designed and documented in `docs/architecture.md`.
- Code and design are reviewed by maintainers; AI output is not accepted without verification against the architecture and security requirements.
- Final accountability for security, correctness, and compliance remains with the project maintainers.

**Architectural extensions (logged for traceability):**

- **Optional password protection (PBKDF2):** Optional password protection for secrets was implemented as an architectural extension: domain aggregate extended with `IsPasswordProtected`, `PasswordHash`, `PasswordSalt`; application layer uses PBKDF2 (Rfc2898DeriveBytes, SHA-256, ≥100k iterations) for key derivation and verification hash; constant-time comparison and unified error response to prevent enumeration; API and frontend support optional password on create and reveal. Documented in [architecture.md](./architecture.md) (§4 Crypto strategy, §8 Security requirements).

**References:** [architecture.md](./architecture.md), OWASP, NIST, and RFCs cited in the architecture document.

## Workflow

Briefly describe how AI tools were used during development.

AI tools (primarily Cursor) were used as an assistant during development rather than as an autonomous code generator.

Typical workflow:

1. A problem or improvement area was identified manually.
2. Cursor was asked to analyse the codebase or review a specific concern.
3. The output was reviewed and filtered before any implementation step.
4. If the analysis was useful, a follow-up prompt was used to generate scaffolding or test cases.
5. Generated changes were manually reviewed before being accepted.

This workflow helped accelerate repetitive work while keeping architectural and security decisions human-controlled.

## Prompt Log

### Prompt 1 — Unit test coverage audit

-----------
You are a Principal .NET test reviewer.

Goal: Audit unit test coverage quality against a checklist for SecureVault. 
Do NOT implement or modify any code. No refactors. Analysis only.

Steps:
1) Using @codebase, map existing unit tests to the checklist below.
2) For each checklist item:
   - Mark as:  Covered / Partially / Missing
   - Point to evidence: test file + test name
   - If missing/partial: explain why it matters and what a good unit test would assert (high-level, no code)
3) Highlight the top 5 highest-risk gaps (security/correctness) and explain impact.
4) Propose a minimal plan (ordered) to add/fix tests to close gaps, including any small seams needed for testability.

Checklist to audit (unit-testable):
A) Token parsing/base64url/padding/length=32 + SHA-256 hashing
B) Handler outcome mapping for repo/cache states (notfound/expired/viewed)
C) Password gate: missing/wrong/correct, salt/hash null, constant-time compare, no Trim side-effects
D) Encryption invariants: roundtrip, nonce uniqueness (ciphertext differs), tamper (cipher/tag/nonce), wrong key/keyVersion, invalid lengths
E) Create password storage: salt random, hash base64 decodable, same password different hash
F) View-once semantics: decrypt-before-consume regression, consume failure mapping
G) Enumeration policy: wrong password/decrypt fail/invalid token outcomes consistent with design
H) Audit publish called only on success; no secret/token logged
I) Time provider usage in expiry decisions
J) UTF8/non-ascii handling + plaintext length validation
K) Cancellation/exception handling behavior

Output format:
- A table: Checklist item | Status | Evidence | Notes
- Then: Top gaps + recommended next test additions (high-level)

Finally ask:
“Do you want me to implement the missing/weak unit tests now?”
Wait for explicit approval before changing any code.
-----------

#### Purpose

The goal of this prompt was to evaluate whether the existing unit tests cover the critical behaviors of SecureVault.

Instead of focusing only on coverage percentage, the objective was to verify that important areas such as:

token handling

password validation

encryption behavior

reveal-once logic

enumeration policy

audit publishing

expiry handling

were properly tested.

#### Process

Cursor analyzed the current unit tests and mapped them to the checklist.

Each item was classified as:

Covered

Partial

Missing

It also highlighted the highest-risk gaps and suggested a minimal plan for additional tests.

After reviewing the analysis, the recommended missing tests were implemented to improve test coverage and correctness.

#### Outcome

This step helped identify important testing gaps quickly.

Additional unit tests were added based on the suggested plan to cover critical behaviors such as:

failure path auditing

password validation edge cases

encryption error handling

enumeration policy consistency

Cursor was mainly used for test coverage analysis and gap identification, while the final decisions on which tests to implement were made manually.

### Prompt 2 — Security Architecture Review (Enumeration Risk)

-----------
You are a Principal Security Architect reviewing the SecureVault project.

Task:
1) Build a list grouped by scope:
   - Functional flows (create, reveal once, expiry, expired response, optional password)
   - Crypto & key management
   - Token lifecycle & one-time view correctness
   - Redis TTL & expiry enforcement
   - DB persistence (no plaintext secrets)
   - API security (rate limiting, input validation, logging redaction, headers)
   - Tests (unit,integration,e2e) 

2) For each item:
   - Evidence: exact file paths + line numbers + brief snippet reference
   - Impact: why it matters (security/correctness/maintainability)
   - Fix Plan: concrete steps

3) Produce a prioritized TODO backlog:
   - P0 (must fix for acceptance)
   - P1 (strongly recommended)
   - P2 (polish)

4) DO NOT change any code yet. Ask me for approval on the P0 list first.
-----------

#### Purpose

The goal of this prompt was to perform a security-focused architecture review of the SecureVault project.

Instead of reviewing code line-by-line manually, the intention was to identify potential issues across major system areas such as:

secret lifecycle

token handling

encryption and key usage

API behavior

storage security

enumeration risks

The prompt was designed to surface security and correctness gaps and prioritize them.

#### Process

Cursor analyzed the project and produced a structured review grouped by architecture areas.

Most areas were reported as being in good shape, including:

crypto and key management

Redis TTL usage

database persistence without plaintext secrets

API validation and rate limiting

overall functional flow

However, one P0 security issue was identified.

The API returned different responses for different failure states:

404 → "Secret not found"

410 → "This secret has expired or has already been viewed"

This difference could allow token enumeration, where an attacker could distinguish between:

a token that never existed

a token that existed but expired or was already consumed

This would leak information about valid tokens in the system.

#### Override

Cursor initially suggested returning the same user-facing message but keeping different HTTP status codes (404 vs 410).

After reviewing the reasoning, I decided to override this suggestion.

To fully prevent enumeration via both message and status code, the API was changed to return:

one unified message

one unified status code

This ensures the API does not leak whether a token:

never existed

expired

was already viewed

The backend still distinguishes these states internally for logging and auditing, but this distinction is not exposed to the client.

#### Outcome

The API response behavior was updated so that invalid, expired, or already-consumed tokens return the same external response.

This prevents the API from acting as an enumeration oracle, while still allowing internal logic and auditing to differentiate between cases.

Cursor was used to help identify the security issue and reason about its impact, while the final implementation decision (status code unification) was made manually.

### Prompt 3 Security vs Usability Trade-off Evaluation

-----------
I want you to evaluate a deliberate product/security trade-off before changing any code.

Context:
In the password-protected reveal flow, after a wrong password attempt the backend intentionally returns a response that is close to the expired/invalid 
case to reduce token-enumeration / oracle-style information leakage. Because of that, the frontend currently disables the password input and the user 
cannot retry on the same page. The current Playwright test expects same-page retry, so it fails.

I am considering:
Do not change the product flow.
Instead, update the E2E test so that after a wrong password attempt it re-opens the created reveal link and then retries with the correct password.

What I want from you:
1. First, evaluate this approach as an architect/reviewer:
   - Is this a reasonable security/usability trade-off?
   - What are the risks, downsides, and what should be documented?

2. Then, if you think this is acceptable, explain exactly what implementation changes would be needed:
   - which test(s) should change
   - how the scenario should be rewritten
   - whether any docs should be updated to explain the trade-off

3. Do not implement anything yet.
Do not modify files yet.
At the end, give me a short recommended plan and wait for my approval before making changes.
-----------
#### Purpose

The purpose of this prompt was to evaluate a security vs usability trade-off in the password-protected reveal flow.

The backend intentionally hides the difference between:

wrong password

expired secret

already viewed secret

to avoid token or password enumeration attacks.

However, this design prevents the user from retrying the password on the same page, which caused an E2E test to fail.

The goal was to confirm whether keeping the secure behavior and adjusting the test instead of the product logic was a reasonable approach.

#### Process

Cursor evaluated the scenario from a security and architecture perspective.

It confirmed that the current design is consistent with the project's security model:

wrong password returns the same external response as expired or invalid tokens

the UI shows the same expired/invalid state

this prevents the API from acting as an information oracle

Cursor suggested keeping the product behavior unchanged and modifying the E2E test to reflect the intended UX:

user enters wrong password

expired/invalid state is shown

user re-opens the reveal link

user enters the correct password

secret is revealed

The recommendation was therefore to update the E2E test scenario instead of modifying the backend or frontend behavior.

#### Override

During implementation I slightly extended the suggested solution.

Cursor focused only on updating the E2E test, but I also updated the expired state message in the UI to make the behavior clearer for users.

The message was updated to explain that the secret might:

be expired

already viewed

or the password might be incorrect

and that the user can retry by reopening the link.

This change improves usability without changing the backend security model.

#### Outcome

The product behavior remained unchanged to preserve the security design.

Changes applied:

Updated one E2E test (frontend/e2e/secret-flow.spec.ts) to retry by reopening the link.

Updated the expired state message in the frontend to clarify the possible reasons and retry behavior.

Backend logic remained unchanged.

After the update, all runnable secret-flow E2E tests passed.

Cursor was mainly used to evaluate the security trade-off and propose the testing strategy, while the final UX improvement decision was made manually.

### Prompt 4 Password Verification vs Encryption Review
-----------
@src/SecureVault.Application/Secrets/CreateSecret/CreateSecretCommandHandler.cs:59-71 Review the password-protection and encryption-key usage in this handler.

Currently:
- a key is derived from the optional user password
- that derived key is passed into the encryption call

My concern is that this mixes two different responsibilities:
1) user password verification
2) data encryption key management

I think that the password should act only as a reveal-time verification mechanism, while encryption should remain fully managed by the system key.

Question:
Is this concern valid from a security and architecture perspective?

Before suggesting any code changes, first explain your reasoning and possible design options.

Then propose the minimal refactor

Wait for my approval before implementing anything. 
-----------
#### Purpose

I revisited the password-protected secret flow during a later review stage. I wanted to check whether the password handling and encryption design were still sound after implementation.

#### Process

At first, the implementation looked acceptable. During a deeper review, I noticed a possible issue: the password-derived value appeared to be involved in both password verification and encryption. I then used Cursor to analyze that specific concern, compare design options, and suggest the smallest possible refactor before changing any code.

#### Override

During review, I chose not to keep the earlier implementation approach as-is. I used a follow-up prompt to guide this part of the flow toward a different design method, which made this an override of the earlier implementation.

#### Outcome

This review helped identify a serious security issue in the implemented flow. It also showed that AI output can be useful for analysis and refactoring, but still needs careful human review, especially in security-sensitive areas.
